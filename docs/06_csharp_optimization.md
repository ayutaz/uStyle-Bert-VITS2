# C#高速化ガイド

## 概要

Style-Bert-VITS2 Unity実装のC#コードにおける高速化テクニック。
メモリアロケーション削減、Burst対応、データ構造最適化、P/Invoke最適化の具体的手法をまとめる。

---

## メモリアロケーション最適化

### ArrayPool\<T\>.Shared — バッファ再利用

推論パイプラインで繰り返し確保される`float[]`/`int[]`のGC圧力を削減する。

**実装済みの使用箇所:**

1. **TTSPipeline**: BERT 推論結果 (`bertData`) と BERT 展開結果 (`alignedBert`) を `ArrayPool` で管理
2. **BertRunner dest overload**: 呼び出し側が事前確保したバッファに結果を書き込む

```csharp
// TTSPipeline での実際のパターン
int bertLen = BertAligner.EmbeddingDimension * tokenIds.Length;
float[] bertData = ArrayPool<float>.Shared.Rent(bertLen);
float[] alignedBert = null;
try
{
    _bert.Run(tokenIds, attentionMask, bertData); // dest overload
    int alignedLen = BertAligner.EmbeddingDimension * phoneSeqLen;
    alignedBert = ArrayPool<float>.Shared.Rent(alignedLen);
    BertAligner.AlignBertToPhonemesBurst(
        bertData, tokenIds.Length, word2ph, phoneSeqLen, alignedBert);
    // ... TTS 推論 ...
}
finally
{
    if (alignedBert != null) ArrayPool<float>.Shared.Return(alignedBert);
    ArrayPool<float>.Shared.Return(bertData);
}
```

**効果**: 推論呼び出しごとに ~250KB (bertData + alignedBert) のGCアロケーションを回避。

### スカラーバッファ再利用

`SBV2ModelRunner` では Sentis テンソル作成時のスカラー値（speakerId, sdpRatio 等）にフィールドバッファを再利用：

```csharp
private readonly int[] _scalarIntBuf = new int[1];
private readonly float[] _scalarFloatBuf = new float[1];

// 推論時: バッファの値を上書きして再利用（GC alloc 0）
_scalarIntBuf[0] = speakerId;
using var sidTensor = new Tensor<int>(new TensorShape(1), _scalarIntBuf);
_scalarFloatBuf[0] = sdpRatio;
using var sdpTensor = new Tensor<float>(new TensorShape(1), _scalarFloatBuf);
```

**効果**: 推論呼び出しごとに 6 個の小規模配列アロケーション (150 bytes) を除去。

### stackalloc — 小規模一時配列

音素列長が短い（< 256）場合に安全に使用できる：

```csharp
public unsafe void ProcessShortText(string text)
{
    var g2pResult = _g2p.Process(text);
    int seqLen = g2pResult.PhonemeIds.Length;

    if (seqLen <= 256)
    {
        // stackalloc: ヒープアロケーション完全回避
        Span<int> languageIds = stackalloc int[seqLen];
        languageIds.Fill(1);  // 全て日本語(1)

        // Span<int> → int[] 変換が必要な場合
        int[] langArray = languageIds.ToArray();
    }
    else
    {
        // 長い文は通常のアロケーション
        int[] languageIds = new int[seqLen];
        Array.Fill(languageIds, 1);
    }
}
```

**適用場面**: `language_ids`配列（全要素1）、短いトーン配列、アテンションマスク（全要素1）など。

### Span\<T\> / ReadOnlySpan\<char\> — Spanによる文字列処理の効率化

> **Note**: Unity (C# 9.0) では `Dictionary` の Span キーによるルックアップがサポートされないため、辞書検索時に `ToString()` が必要になり、完全なゼロアロケーションは実現できない。文字レベルのトークナイザでは後述の `Dictionary<char, int>` パターンを推奨する。

```csharp
public class SBV2Tokenizer
{
    private readonly Dictionary<string, int> _vocab;

    /// <summary>
    /// 文字レベルトークナイズ（substring を避け Span で処理）
    /// </summary>
    public void Encode(ReadOnlySpan<char> text, Span<int> output, out int length)
    {
        int idx = 0;
        output[idx++] = 1; // [CLS]

        // 1文字ずつ処理（Substringのアロケーションを回避）
        for (int i = 0; i < text.Length; i++)
        {
            ReadOnlySpan<char> ch = text.Slice(i, 1);

            // Span→string変換は辞書ルックアップで必要
            // .NET 9+ なら Dictionary の AlternateKey で回避可能
            // Unity (C# 9) では ToString() が必要
            string key = ch.ToString();

            if (_vocab.TryGetValue(key, out int tokenId))
                output[idx++] = tokenId;
            else
                output[idx++] = 3; // [UNK]
        }

        output[idx++] = 2; // [SEP]
        length = idx;
    }
}
```

**注意**: Unity (C# 9.0) では `Dictionary` の Span キーによるルックアップはサポートされない。文字レベルのトークナイザでは `char → int` の直接マッピング (`Dictionary<char, int>`) の方が効率的。

```csharp
// より効率的: char→int の直接辞書
private readonly Dictionary<char, int> _charVocab;

public void EncodeEfficient(ReadOnlySpan<char> text, Span<int> output, out int length)
{
    int idx = 0;
    output[idx++] = 1; // [CLS]

    for (int i = 0; i < text.Length; i++)
    {
        if (_charVocab.TryGetValue(text[i], out int tokenId))
            output[idx++] = tokenId;
        else
            output[idx++] = 3; // [UNK]
    }

    output[idx++] = 2; // [SEP]
    length = idx;
}
```

---

## Burst対応コンポーネント

### BertAlignmentJob — BERT展開の並列化

BERT埋め込みの音素列展開はホットパスであり、Burst + IJobParallelFor で4-8倍の高速化が見込める：

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct BertAlignmentJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> BertFlat;     // [1024 * tokenLen]
    [ReadOnly] public NativeArray<int> PhoneToToken;   // [phoneSeqLen] → 対応するtokenIndex
    public NativeArray<float> AlignedBert;             // [1024 * phoneSeqLen]

    public int TokenLen;
    public int PhoneSeqLen;
    public int EmbDim; // 1024

    public void Execute(int phoneIdx)
    {
        int tokenIdx = PhoneToToken[phoneIdx];
        for (int d = 0; d < EmbDim; d++)
        {
            AlignedBert[d * PhoneSeqLen + phoneIdx] = BertFlat[d * TokenLen + tokenIdx];
        }
    }
}
```

使用例：

```csharp
public float[] AlignBertBurst(float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
{
    // word2ph → phoneToToken マッピングを構築
    var phoneToToken = new NativeArray<int>(phoneSeqLen, Allocator.TempJob);
    var bertNative = new NativeArray<float>(bertFlat, Allocator.TempJob);
    var aligned = new NativeArray<float>(1024 * phoneSeqLen, Allocator.TempJob);
    try
    {
        int phoneIdx = 0;
        for (int w = 0; w < word2ph.Length; w++)
        {
            for (int p = 0; p < word2ph[w]; p++)
                phoneToToken[phoneIdx++] = w;
        }

        var job = new BertAlignmentJob
        {
            BertFlat = bertNative,
            PhoneToToken = phoneToToken,
            AlignedBert = aligned,
            TokenLen = tokenLen,
            PhoneSeqLen = phoneSeqLen,
            EmbDim = 1024,
        };

        job.Schedule(phoneSeqLen, 64).Complete(); // batch size = 64

        return aligned.ToArray();
    }
    finally
    {
        phoneToToken.Dispose();
        bertNative.Dispose();
        aligned.Dispose();
    }
}
```

### AudioNormalizationJob — 音声正規化の並列化

2パス方式: 最大絶対値の探索はO(n)のシングルスレッドで十分高速。スケーリングのみBurstで並列化する。

```csharp
[BurstCompile]
public struct NormalizeAudioJob : IJobParallelFor
{
    public NativeArray<float> Samples;
    public float Scale;

    public void Execute(int i)
    {
        Samples[i] *= Scale;
    }
}

public static void NormalizeBurst(float[] samples, float targetPeak = 0.95f)
{
    // Pass 1: 最大絶対値を探索（シングルスレッドで十分高速）
    float maxAbs = 0f;
    for (int i = 0; i < samples.Length; i++)
        maxAbs = Mathf.Max(maxAbs, Mathf.Abs(samples[i]));

    if (maxAbs <= 0f)
        return;

    // Pass 2: スケーリング（Burstで並列化）
    var native = new NativeArray<float>(samples, Allocator.TempJob);
    try
    {
        float scale = targetPeak / maxAbs;
        var normalizeJob = new NormalizeAudioJob
        {
            Samples = native,
            Scale = scale,
        };
        normalizeJob.Schedule(native.Length, 1024).Complete();

        native.CopyTo(samples);
    }
    finally
    {
        native.Dispose();
    }
}
```

### Burst非互換の注意事項

| コンポーネント | Burst互換性 | 理由 |
|---|---|---|
| BertAlignmentJob | 互換 | NativeArray + 数値演算のみ |
| AudioNormalizationJob | 互換 | NativeArray + 数値演算のみ |
| G2P (OpenJTalk P/Invoke) | **非互換** | マネージド文字列、P/Invoke呼び出し |
| SBV2Tokenizer | **非互換** | Dictionary、文字列処理 |
| Sentis Worker操作 | **非互換** | マネージドAPI |

---

## BERT展開アルゴリズム最適化 (ホットパス)

### Buffer.BlockCopy — 一括メモリコピー

要素ごとのコピーループを一括コピーに置き換える（約2倍高速）：

```csharp
public static float[] AlignBertBlockCopy(
    float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
{
    int embDim = 1024;
    float[] aligned = new float[embDim * phoneSeqLen];

    int phoneIdx = 0;
    int tokenIdx = 0;

    for (int w = 0; w < word2ph.Length; w++)
    {
        for (int p = 0; p < word2ph[w]; p++)
        {
            // 1024次元ベクトルを一括コピー
            // bertFlat: [embDim, tokenLen] (row-major) → 列方向アクセス
            // ストライドアクセスのため BlockCopy は連続領域のみ有効
            //
            // bertFlat のレイアウトが [1, 1024, tokenLen] (Sentis出力) の場合、
            // 各次元 d のオフセット: d * tokenLen + tokenIdx
            // → 連続でないためBlockCopy不可。要素コピーが必要。

            for (int d = 0; d < embDim; d++)
            {
                aligned[d * phoneSeqLen + phoneIdx] = bertFlat[d * tokenLen + tokenIdx];
            }
            phoneIdx++;
        }
        tokenIdx++;
    }

    return aligned;
}
```

**注意**: Sentisの出力テンソルレイアウト `[1, 1024, tokenLen]` では、同一トークンの1024次元がメモリ上で非連続（ストライドアクセス）。`Buffer.BlockCopy` が効果的なのは連続メモリ領域のコピーのみ。

**転置バッファを用いた最適化**:

```csharp
public static float[] AlignBertTransposed(
    float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
{
    int embDim = 1024;

    // Step 1: [1024, tokenLen] → [tokenLen, 1024] に転置
    float[] transposed = ArrayPool<float>.Shared.Rent(tokenLen * embDim);
    try
    {
        for (int t = 0; t < tokenLen; t++)
        {
            for (int d = 0; d < embDim; d++)
                transposed[t * embDim + d] = bertFlat[d * tokenLen + t];
        }

        // Step 2: 転置後は各トークンの1024次元が連続 → BlockCopy可能
        float[] aligned = new float[embDim * phoneSeqLen];
        float[] alignedTransposed = ArrayPool<float>.Shared.Rent(phoneSeqLen * embDim);
        try
        {
            int phoneIdx = 0;
            int tokenIdx = 0;
            for (int w = 0; w < word2ph.Length; w++)
            {
                for (int p = 0; p < word2ph[w]; p++)
                {
                    Buffer.BlockCopy(
                        transposed, tokenIdx * embDim * sizeof(float),
                        alignedTransposed, phoneIdx * embDim * sizeof(float),
                        embDim * sizeof(float));  // 4096 bytes per copy
                    phoneIdx++;
                }
                tokenIdx++;
            }

            // Step 3: [phoneSeqLen, 1024] → [1024, phoneSeqLen] に再転置
            for (int p = 0; p < phoneSeqLen; p++)
            {
                for (int d = 0; d < embDim; d++)
                    aligned[d * phoneSeqLen + p] = alignedTransposed[p * embDim + d];
            }

            return aligned;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(alignedTransposed);
        }
    }
    finally
    {
        ArrayPool<float>.Shared.Return(transposed);
    }
}
```

### SIMD (System.Numerics.Vector) について

転置のような非連続アクセスパターンでは `System.Numerics.Vector<T>` によるSIMD化のメリットは限定的。BERT展開の高速化には**転置+BlockCopyパターン**か**Burst IJobParallelFor**（前述）を推奨する。

---

## 効率的なデータ構造

### 音素→ID辞書

```csharp
public class SBV2PhonemeMapper
{
    // StringComparer.Ordinal で大文字小文字を区別したハッシュ比較
    private readonly Dictionary<string, int> _phonemeToId;

    public SBV2PhonemeMapper(string configJsonPath)
    {
        // config.json の "symbols" リストから辞書を構築
        string json = File.ReadAllText(configJsonPath);
        var config = JsonConvert.DeserializeObject<SBV2Config>(json);

        _phonemeToId = new Dictionary<string, int>(
            config.Symbols.Count, StringComparer.Ordinal);

        for (int i = 0; i < config.Symbols.Count; i++)
            _phonemeToId[config.Symbols[i]] = i;
    }

    public int GetId(string phoneme)
    {
        return _phonemeToId.TryGetValue(phoneme, out int id)
            ? id
            : _phonemeToId.GetValueOrDefault("UNK", 0);
    }
}
```

**ポイント**:
- `StringComparer.Ordinal` でハッシュ計算を最速化（Culture依存の比較を回避）
- 初期化時に辞書を構築し、ランタイムでは `TryGetValue` のみ
- `ToLower()` などのランタイム文字列変換を排除

### char→int 直接マッピング（DeBERTaトークナイザ用）

```csharp
public class CharTokenizer
{
    // char (2 bytes) をキーにした高速ルックアップ
    private readonly Dictionary<char, int> _charToId;
    private readonly int _unkId;
    private readonly int _clsId;
    private readonly int _sepId;

    public CharTokenizer(string vocabJsonPath)
    {
        string json = File.ReadAllText(vocabJsonPath);
        var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);

        _charToId = new Dictionary<char, int>(vocab.Count);
        foreach (var (token, id) in vocab)
        {
            if (token.Length == 1)
                _charToId[token[0]] = id;
        }

        _unkId = vocab.GetValueOrDefault("[UNK]", 3);
        _clsId = vocab.GetValueOrDefault("[CLS]", 1);
        _sepId = vocab.GetValueOrDefault("[SEP]", 2);
    }

    public (int[] TokenIds, int[] AttentionMask) Encode(string text)
    {
        int len = text.Length + 2; // [CLS] + text + [SEP]
        int[] tokenIds = new int[len];
        int[] mask = new int[len];

        tokenIds[0] = _clsId;
        mask[0] = 1;

        for (int i = 0; i < text.Length; i++)
        {
            tokenIds[i + 1] = _charToId.TryGetValue(text[i], out int id) ? id : _unkId;
            mask[i + 1] = 1;
        }

        tokenIds[len - 1] = _sepId;
        mask[len - 1] = 1;

        return (tokenIds, mask);
    }
}
```

---

## P/Invoke最適化

### UTF-8バッファプーリング

OpenJTalk P/InvokeではマネージドC#文字列をUTF-8に変換してネイティブに渡す。uPiperで実証済みのバッファプーリングパターン：

```csharp
using System.Buffers;
using System.Text;

public static class Utf8BufferPool
{
    private static readonly Encoding Utf8 = Encoding.UTF8;

    /// <summary>
    /// C#文字列をUTF-8バイト列に変換（ArrayPoolを使用してGC回避）
    /// </summary>
    public static (byte[] Buffer, int Length) GetUtf8Bytes(string text)
    {
        int byteCount = Utf8.GetByteCount(text);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount + 1); // +1 for null terminator
        int written = Utf8.GetBytes(text, 0, text.Length, buffer, 0);
        buffer[written] = 0; // null terminator for C string
        return (buffer, written + 1);
    }

    public static void Return(byte[] buffer)
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}

// 使用例
public NativePhonemeResult Phonemize(string text)
{
    var (utf8Buffer, length) = Utf8BufferPool.GetUtf8Bytes(text);
    try
    {
        unsafe
        {
            fixed (byte* ptr = utf8Buffer)
            {
                return openjtalk_phonemize(_handle, ptr, out var result);
            }
        }
    }
    finally
    {
        Utf8BufferPool.Return(utf8Buffer);
    }
}
```

**効果**: 毎回の `Encoding.UTF8.GetBytes()` による `byte[]` アロケーションを回避。uPiperで実証済み（GC ~90%削減）。

### SafeHandle — ネイティブリソース管理

```csharp
using System.Runtime.InteropServices;

public class OpenJTalkHandle : SafeHandle
{
    public OpenJTalkHandle() : base(IntPtr.Zero, true) { }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            OpenJTalkNative.openjtalk_destroy(handle);
            return true;
        }
        return false;
    }
}

// 使用例
public class JapaneseG2P : IG2P
{
    private readonly OpenJTalkHandle _handle;

    public JapaneseG2P(string dictPath)
    {
        _handle = OpenJTalkNative.openjtalk_create(dictPath);
        if (_handle.IsInvalid)
            throw new InvalidOperationException("Failed to initialize OpenJTalk");
    }

    public void Dispose() => _handle?.Dispose(); // SafeHandleが確実にnativeリソースを解放
}
```

### Marshal.Copy — ネイティブ結果のデータ抽出

```csharp
/// <summary>
/// ネイティブ側が確保した音素データをマネージド配列にコピー
/// </summary>
public static int[] CopyNativePhonemes(IntPtr nativePtr, int count)
{
    int[] result = new int[count];
    Marshal.Copy(nativePtr, result, 0, count);
    return result;
}

/// <summary>
/// ネイティブ構造体を読み取り（P/Invoke結果の解析）
/// </summary>
public static T ReadNativeStruct<T>(IntPtr ptr) where T : struct
{
    return Marshal.PtrToStructure<T>(ptr);
}
```

---

## 非同期パターン

### ScheduleIterable + コルーチン統合

```csharp
public IEnumerator SynthesizeAsync(TTSRequest request, Action<AudioClip> callback)
{
    // G2P（CPU、同期）
    var g2p = _g2p.Process(request.Text);
    yield return null;

    // BERT推論（フレーム分散）
    var (tokens, mask) = _tokenizer.Encode(request.Text);
    SetBertInputs(tokens, mask);
    var bertEnum = _bertWorker.ScheduleIterable();
    while (bertEnum.MoveNext())
        yield return null;

    // BERT出力取得・展開
    var bertOut = _bertWorker.PeekOutput() as Tensor<float>;
    bertOut.ReadbackAndClone();
    float[] bertData = bertOut.DownloadToArray();
    float[] aligned = AlignBertToPhonemes(bertData, g2p.Word2Ph, g2p.PhonemeIds.Length);
    yield return null;

    // TTS推論（フレーム分散）
    SetTTSInputs(g2p, aligned, request);
    var ttsEnum = _ttsWorker.ScheduleIterable();
    while (ttsEnum.MoveNext())
        yield return null;

    // 音声生成
    var audioOut = _ttsWorker.PeekOutput("output") as Tensor<float>;
    audioOut.ReadbackAndClone();
    float[] samples = audioOut.DownloadToArray();
    var clip = TTSAudioUtility.CreateClip(samples);

    callback?.Invoke(clip);
}
```

### Unity 6 Awaitable 対応

```csharp
public async Awaitable<AudioClip> SynthesizeAsync(TTSRequest request)
{
    // CPU処理はバックグラウンドスレッドで実行可能
    G2PResult g2p;
    await Awaitable.BackgroundThreadAsync();
    g2p = _g2p.Process(request.Text);

    var (tokens, mask) = _tokenizer.Encode(request.Text);

    // Sentis WorkerはメインスレッドでのみSchedule可能
    await Awaitable.MainThreadAsync();

    // BERT推論
    SetBertInputs(tokens, mask);
    _bertWorker.Schedule();
    await Awaitable.EndOfFrameAsync();

    var bertOut = _bertWorker.PeekOutput() as Tensor<float>;
    bertOut.ReadbackAndClone();
    float[] bertData = bertOut.DownloadToArray();

    // アライメントはバックグラウンドで
    float[] aligned;
    await Awaitable.BackgroundThreadAsync();
    aligned = AlignBertToPhonemes(bertData, g2p.Word2Ph, g2p.PhonemeIds.Length);

    // TTS推論（メインスレッド）
    await Awaitable.MainThreadAsync();
    SetTTSInputs(g2p, aligned, request);
    _ttsWorker.Schedule();
    await Awaitable.EndOfFrameAsync();

    var audioOut = _ttsWorker.PeekOutput("output") as Tensor<float>;
    audioOut.ReadbackAndClone();
    float[] samples = audioOut.DownloadToArray();

    return TTSAudioUtility.CreateClip(samples);
}
```

**注意**: `Awaitable.BackgroundThreadAsync()` と `Awaitable.MainThreadAsync()` のスレッド切り替えにはオーバーヘッドがある。G2Pが十分高速（< 30ms）なら、全てメインスレッドで実行した方がシンプル。

---

## プロファイリング

> **Note**: 以下の目標レイテンシは `docs/05_performance_optimization.md` のレイテンシ分析に基づく。条件: `seq_len ≈ 20-50`（日本語20-50文字相当）の典型的な文。

### ProfilerMarkerの設置

```csharp
using Unity.Profiling;
using System.Buffers;

public class TTSPipeline
{
    static readonly ProfilerMarker s_G2P = new("TTS.G2P");
    static readonly ProfilerMarker s_Tokenize = new("TTS.Tokenize");
    static readonly ProfilerMarker s_BertInfer = new("TTS.BERT.Inference");
    static readonly ProfilerMarker s_BertAlign = new("TTS.BERT.Alignment");
    static readonly ProfilerMarker s_TTSInfer = new("TTS.SynthesizerTrn");
    static readonly ProfilerMarker s_Audio = new("TTS.AudioClip");

    public AudioClip Synthesize(TTSRequest request)
    {
        G2PResult g2p;
        using (s_G2P.Auto()) { g2p = _g2p.Process(request.Text); }

        int[] tokens; int[] mask;
        using (s_Tokenize.Auto()) { (tokens, mask) = _tokenizer.Encode(request.Text); }

        // ArrayPool で BERT バッファを管理（GC 圧力削減）
        int bertLen = 1024 * tokens.Length;
        float[] bertData = ArrayPool<float>.Shared.Rent(bertLen);
        float[] alignedBert = null;
        try
        {
            using (s_BertInfer.Auto()) { _bert.Run(tokens, mask, bertData); }

            int alignedLen = 1024 * phoneSeqLen;
            alignedBert = ArrayPool<float>.Shared.Rent(alignedLen);
            using (s_BertAlign.Auto())
            {
                BertAligner.AlignBertToPhonemesBurst(
                    bertData, tokens.Length, word2ph, phoneSeqLen, alignedBert);
            }

            float[] audio;
            using (s_TTSInfer.Auto()) { audio = _tts.Run(...); }

            AudioClip clip;
            using (s_Audio.Auto())
            {
                TTSAudioUtility.NormalizeSamplesBurst(audio, 0.95f);
                int trimmedLength = GetTrimmedLength(audio); // 配列コピーなし
                clip = AudioClip.Create("TTS", trimmedLength, 1, 44100, false);
                clip.SetData(audio, 0);
            }
            return clip;
        }
        finally
        {
            if (alignedBert != null) ArrayPool<float>.Shared.Return(alignedBert);
            ArrayPool<float>.Shared.Return(bertData);
        }
    }
}
```

### ステージ別目標レイテンシ

| ステージ | マーカー名 | 目標 | 最適化手法 |
|---|---|---|---|
| G2P | `TTS.G2P` | < 30ms (GPU) / < 50ms (CPU) | UTF-8バッファプール、結果キャッシュ |
| トークナイズ | `TTS.Tokenize` | < 5ms | char→int直接辞書 |
| BERT推論 | `TTS.BERT.Inference` | < 50ms (GPU) / < 300ms (CPU) | GPUCompute、FP16 |
| BERT展開 | `TTS.BERT.Alignment` | < 5ms (GPU) / < 20ms (CPU) | Burst Job、Buffer.BlockCopy |
| TTS推論 | `TTS.SynthesizerTrn` | < 50ms (GPU) / < 200ms (CPU) | GPUCompute、FP16 |
| 音声生成 | `TTS.AudioClip` | < 5ms (GPU) / < 10ms (CPU) | Burst正規化 |
| **合計** | — | **< 140ms (GPU)** | — |

### メモリプロファイリング

Unity Profilerの **Memory** モジュールで以下を監視：

- **GC.Alloc**: 推論1回あたりのGCアロケーション量
  - 目標: < 1MB/call（ArrayPool使用時）
  - NG: > 5MB/call（バッファ再利用なし）
- **GPU Memory**: VRAM使用量
  - 目標: < 1.5GB（DeBERTa + SBV2 + バッファ）
- **Native Memory**: OpenJTalkのネイティブメモリ
  - SafeHandleで確実に解放されていることを確認

---

## 注意事項

- **過剰最適化を避ける**: まず動作するパイプラインを構築し、ProfilerMarkerでボトルネックを特定してから最適化する
- **Burst + NativeArray のDispose**: `Allocator.TempJob` で確保した `NativeArray` はジョブ完了後に必ず `Dispose()` すること。漏れるとメモリリーク
- **スレッドセーフティ**: `Worker.Schedule()` はメインスレッドからのみ呼び出し可能。バックグラウンドスレッドからのSentis API呼び出しは未サポート
- **C# 9.0制約**: Unity 6はC# 9.0。`Span<T>`はサポートされるが、`required`プロパティ、`file`スコープ型、`AlternateKey`辞書ルックアップなどC# 11+機能は使用不可
