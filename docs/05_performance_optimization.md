# パフォーマンス最適化ガイド

## 概要

Style-Bert-VITS2 Unity実装における推論パフォーマンスの最適化戦略。
Sentis GPU/CPU選択、メモリ管理、非同期推論、レイテンシ削減の具体的手法をまとめる。

---

## BackendType選択戦略

### 比較

| BackendType | 速度 | メモリ | 安定性 | 推奨環境 |
|---|---|---|---|---|
| `GPUCompute` | 最速 | VRAM使用 | GPU依存 | デスクトップ (DirectX 12 / Vulkan) |
| `CPU` | 低速 | RAM使用 | 高安定 | モバイル、ローエンド、フォールバック |
| `GPUPixel` | 中速 | VRAM使用 | 限定的 | Compute Shader非対応環境 |

### プラットフォーム別推奨設定

```csharp
public static BackendType GetRecommendedBackend()
{
    // デスクトップ: GPUCompute優先
    if (Application.platform == RuntimePlatform.WindowsPlayer ||
        Application.platform == RuntimePlatform.LinuxPlayer)
        return BackendType.GPUCompute;

    // macOS: Metal対応GPUCompute
    if (Application.platform == RuntimePlatform.OSXPlayer)
        return BackendType.GPUCompute;

    // モバイル: CPU推奨（GPU精度問題を回避）
    if (Application.platform == RuntimePlatform.Android ||
        Application.platform == RuntimePlatform.IPhonePlayer)
        return BackendType.CPU;

    // Editor: GPUCompute
    if (Application.isEditor)
        return BackendType.GPUCompute;

    return BackendType.CPU;
}
```

### GPU/CPUフォールバックパターン

```csharp
public Worker CreateWorkerWithFallback(Model model, BackendType preferred, BackendType fallback)
{
    try
    {
        var worker = new Worker(model, preferred);
        // ダミー推論でGPU動作を検証
        TestWorker(worker, model);
        return worker;
    }
    catch (Exception e)
    {
        Debug.LogWarning($"Backend {preferred} failed: {e.Message}. Falling back to {fallback}.");
        return new Worker(model, fallback);
    }
}
```

---

## レイテンシ分析

### 推論ステージ別レイテンシ（目安）

| ステージ | GPU (RTX 3060相当) | CPU (Ryzen 5 3600相当) | 備考 |
|---|---|---|---|
| G2P (OpenJTalk P/Invoke) | ~10-30ms | ~10-30ms | CPU処理、GPU無関係 |
| DeBERTa トークナイズ | ~1-5ms | ~1-5ms | 文字列処理のみ |
| DeBERTa 推論 | ~30-40ms | ~150-300ms | 最も重いステージ |
| word2ph BERT展開 | ~1-5ms | ~1-5ms | メモリコピー主体 |
| SynthesizerTrn 推論 | ~20-50ms | ~100-200ms | 入力長に依存 |
| AudioClip生成 | ~1-5ms | ~1-5ms | CPU処理 |
| **合計（典型的な文）** | **~60-130ms** | **~260-540ms** | seq_len ≈ 20-50 (日本語20-50文字相当) |

### 初回推論ペナルティ

初回の`Schedule()`呼び出し時にSentisがシェーダコンパイルを行うため、大幅なレイテンシが発生する：

| モデル | 初回推論 | 2回目以降 |
|---|---|---|
| DeBERTa | ~1-3秒 | ~30-40ms |
| SynthesizerTrn | ~1-3秒 | ~20-50ms |

→ ウォームアップ推論で事前にシェーダコンパイルを完了させることが重要（後述）。

### 実測値 (Windows デスクトップ)

| 構成 | 合計レイテンシ | 備考 |
|---|---|---|
| BERT=CPU + TTS=CPU | ~753ms | 安定、CPU負荷高 |
| BERT=CPU + TTS=GPU (初回) | ~969ms | シェーダコンパイル含む |
| BERT=CPU + TTS=GPU (2回目以降) | ~621ms | ウォームアップ後 |

> **警告**: DeBERTa FP32 を `BackendType.GPUCompute` で実行すると D3D12 デバイスロストが発生する。BERT推論には必ず `BackendType.CPU` を使用すること。

### BERT バックエンド別ベンチマーク

環境: NVIDIA GeForce RTX 4070 Ti SUPER / Warmup 3回 / 計測 10回平均

| 入力サイズ | Sentis CPU | ORT CPU | スピードアップ |
|---|---|---|---|
| 5 tokens | ~1000 ms | ~410 ms | **1.9x** |
| 20 tokens | ~860-1090 ms | ~410-554 ms | **2.0x** |
| 40 tokens | ~860-1060 ms | ~460-530 ms | **2.0x** |

> ORT CPU は Sentis CPU の約2倍高速。ORT DirectML は Editor 環境では DML DLL 未ロードのためスキップ（ビルド済みアプリで計測可）。
> ベンチマーク実行: `BertInferenceBenchmarkTests` (PlayMode, Category: Benchmark)

---

## フレーム分散推論 (ScheduleIterable)

### 問題

`worker.Schedule()` は同期実行のため、大規模モデルの推論でメインスレッドをブロックし、フレームレートが低下する。

### ScheduleIterable による分散実行

```csharp
using System.Collections;
using Unity.InferenceEngine;

public class FrameDistributedInference : MonoBehaviour
{
    private Worker _worker;

    /// <summary>
    /// 推論をフレーム間に分散するコルーチン
    /// </summary>
    public IEnumerator RunInferenceDistributed(Worker worker)
    {
        // 入力テンソル設定
        worker.SetInput("x_tst", xTstTensor);
        // ... 他の入力設定 ...

        // フレーム分散実行
        var enumerator = worker.ScheduleIterable();
        while (enumerator.MoveNext())
        {
            // 各フレームで一部のオペレーションを実行
            yield return null;
        }

        // 推論完了、出力取得
        var output = worker.PeekOutput("output") as Tensor<float>;
        output.ReadbackAndClone();
        float[] samples = output.DownloadToArray();

        // AudioClip生成・再生
        var clip = TTSAudioUtility.CreateClip(samples);
        GetComponent<AudioSource>().PlayOneShot(clip);
    }
}
```

### ステージ間yield戦略

マルチモデルパイプラインでは、各ステージ間でyieldして60fpsを維持する：

```csharp
public IEnumerator SynthesizeCoroutine(TTSRequest request)
{
    // Stage 1: G2P (CPU処理、通常は十分高速)
    var g2pResult = _g2p.Process(request.Text);

    // Stage 2: DeBERTaトークナイズ (軽量)
    var (tokenIds, mask) = _tokenizer.Encode(request.Text);

    // Stage 3: BERT推論（重い → フレーム分散）
    _bertWorker.SetInput("input_ids", CreateTensor(tokenIds));
    _bertWorker.SetInput("attention_mask", CreateTensor(mask));
    _bertWorker.SetInput("token_type_ids", CreateZeroTensor(tokenIds.Length));
    var bertEnum = _bertWorker.ScheduleIterable();
    while (bertEnum.MoveNext())
        yield return null;

    // BERT出力取得
    var bertOutput = _bertWorker.PeekOutput() as Tensor<float>;
    bertOutput.ReadbackAndClone();
    float[] bertData = bertOutput.DownloadToArray();

    yield return null; // フレーム境界

    // Stage 4: word2phアライメント (CPU)
    float[] alignedBert = AlignBertToPhonemes(bertData, g2pResult.Word2Ph, g2pResult.PhonemeIds.Length);

    // Stage 5: TTS推論（重い → フレーム分散）
    SetTTSInputs(g2pResult, alignedBert, request);
    var ttsEnum = _ttsWorker.ScheduleIterable();
    while (ttsEnum.MoveNext())
        yield return null;

    // Stage 6: 出力取得＋AudioClip生成
    var audioOutput = _ttsWorker.PeekOutput("output") as Tensor<float>;
    audioOutput.ReadbackAndClone();
    float[] samples = audioOutput.DownloadToArray();

    var clip = TTSAudioUtility.CreateClip(samples);
    OnSynthesisComplete?.Invoke(clip);
}
```

### async/awaitパターン (Unity 6 Awaitable)

> **Note**: 本プロジェクトでは UniTask ベースの非同期処理を採用している（Unity 6 の `Awaitable` ではなく）。以下のサンプルコードは参考実装としての Awaitable 版。実際のコードは `UniTask<AudioClip> SynthesizeAsync(TTSRequest, CancellationToken)` を使用。

> **Schedule() vs ScheduleIterable() の違い**:
> - `Schedule()`: 全オペレーションを即座に実行（同期的）。低レイテンシだがフレームをブロックする
> - `ScheduleIterable()`: オペレーションをフレーム間に分散実行。UI応答性を維持できるが合計レイテンシは増加する

Unity 6では`Awaitable`を使ったasync/await統合が利用可能：

```csharp
public async Awaitable<AudioClip> SynthesizeAsync(TTSRequest request)
{
    // G2P + トークナイズ（同期、軽量）
    var g2pResult = _g2p.Process(request.Text);
    var (tokenIds, mask) = _tokenizer.Encode(request.Text);

    // BERT推論
    SetBertInputs(tokenIds, mask);
    _bertWorker.Schedule();
    await Awaitable.EndOfFrameAsync(); // フレーム境界まで待機

    var bertOutput = _bertWorker.PeekOutput() as Tensor<float>;
    bertOutput.ReadbackAndClone();
    float[] bertData = bertOutput.DownloadToArray();

    // アライメント
    float[] aligned = AlignBertToPhonemes(bertData, g2pResult.Word2Ph, g2pResult.PhonemeIds.Length);

    // TTS推論
    SetTTSInputs(g2pResult, aligned, request);
    _ttsWorker.Schedule();
    await Awaitable.EndOfFrameAsync();

    var audio = _ttsWorker.PeekOutput("output") as Tensor<float>;
    audio.ReadbackAndClone();
    float[] samples = audio.DownloadToArray();

    return TTSAudioUtility.CreateClip(samples);
}
```

---

## メモリ最適化

### FP16 vs FP32 トレードオフ

| 精度 | DeBERTaサイズ | SBV2サイズ | 合計 | 品質影響 |
|---|---|---|---|---|
| FP32 | ~1.2GB | ~400-800MB | ~1.6-2GB | ベースライン |
| **FP16** | **~600MB** | **~200-400MB** | **~800MB-1.2GB** | **ほぼ無し** |

**FP16を推奨**。`keep_io_types=True` でI/OはFP32のまま内部のみFP16にすることで、Sentisとのテンソル受け渡しが安定する。

### 合計メモリ見積

| 項目 | GPU (VRAM) | CPU (RAM) |
|---|---|---|
| DeBERTa FP16モデル | ~600MB | ~600MB |
| SBV2 FP16モデル | ~200-400MB | ~200-400MB |
| Sentisワーキングメモリ | ~100-200MB | ~100-200MB |
| テンソルバッファ | ~50MB | ~50MB |
| **合計** | **~950MB-1.25GB** | **~950MB-1.25GB** |

### モデルロード戦略

#### 遅延ロード（推奨）

必要になったタイミングでモデルをロード。初回推論にレイテンシが加算されるが、メモリを節約できる。

```csharp
public class LazyModelLoader
{
    private Worker _worker;
    private readonly ModelAsset _asset;
    private readonly BackendType _backend;

    public LazyModelLoader(ModelAsset asset, BackendType backend)
    {
        _asset = asset;
        _backend = backend;
    }

    public Worker Worker
    {
        get
        {
            if (_worker == null)
            {
                var model = ModelLoader.Load(_asset);
                _worker = new Worker(model, _backend);
            }
            return _worker;
        }
    }
}
```

#### 事前ロード

アプリ起動時・シーン遷移時に一括ロード。初回推論の応答が良いが、起動が遅くなる。

```csharp
private async Awaitable PreloadModels()
{
    // ロード画面表示中にバックグラウンドで実行
    await Awaitable.BackgroundThreadAsync();

    var bertModel = ModelLoader.Load(_settings.BertModel);
    var ttsModel = ModelLoader.Load(_settings.TTSModel);

    await Awaitable.MainThreadAsync();

    _bertWorker = new Worker(bertModel, _settings.BertBackend);
    _ttsWorker = new Worker(ttsModel, _settings.TTSBackend);
}
```

### テンソルDispose管理

メモリリーク防止のための必須パターン：

```csharp
// NG: Disposeを忘れるとGPU/CPUメモリがリークする
var tensor = new Tensor<float>(shape, data);
worker.SetInput("name", tensor);
worker.Schedule();
// tensor が破棄されない！

// OK: using文で確実にDispose
using var tensor = new Tensor<float>(shape, data);
worker.SetInput("name", tensor);
worker.Schedule();
// スコープ終了時にDispose

// OK: try-finallyパターン（条件分岐がある場合）
Tensor<float> tensor = null;
try
{
    tensor = new Tensor<float>(shape, data);
    worker.SetInput("name", tensor);
    worker.Schedule();
}
finally
{
    tensor?.Dispose();
}
```

### PeekOutput のライフサイクル

```csharp
// PeekOutputはWorkerが所有するテンソルへの参照を返す
// → Disposeは不要（Workerが管理）
// → ただしWorkerのDispose後はアクセス不可
var output = worker.PeekOutput("output") as Tensor<float>;
output.ReadbackAndClone();  // GPU→CPUコピー
float[] data = output.DownloadToArray();  // CPUメモリに新規配列を確保
// ↑ この float[] は GC管理対象。大規模音声データではGC圧力に注意
```

---

## マルチモデルパイプライン最適化

### Worker間テンソル転送

SentisではWorker間でテンソルを直接共有できない。CPU経由の転送が必須：

```
BERT Worker (GPU)
  → PeekOutput() → ReadbackAndClone() → DownloadToArray()  [GPU→CPU]
  → new Tensor<float>(shape, data)                          [CPU→新テンソル]
  → TTS Worker.SetInput()                                   [CPU→GPU]
```

**ボトルネック**: `ReadbackAndClone()` でGPU→CPU転送が発生。BERT出力は `[1, 1024, token_len]` のため、token_len=50で約200KBの転送。実測では1-5ms程度。

### Worker lifecycle

```csharp
// NG: 推論ごとにWorkerを作り直す（毎回シェーダコンパイルが走る）
public float[] RunBert(int[] tokenIds)
{
    var model = ModelLoader.Load(_asset);
    var worker = new Worker(model, BackendType.GPUCompute);  // 毎回作成
    // ...
    worker.Dispose();  // 毎回破棄
}

// OK: Workerは一度作成して再利用
public class BertRunner : IDisposable
{
    private readonly Worker _worker;

    public BertRunner(ModelAsset asset, BackendType backend)
    {
        var model = ModelLoader.Load(asset);
        _worker = new Worker(model, backend);  // 初回のみ
    }

    public float[] Run(int[] tokenIds, int[] mask)
    {
        // _workerを再利用（SetInput→Schedule→PeekOutput）
        // ...
    }

    public void Dispose() => _worker?.Dispose();
}
```

### BERT埋め込みキャッシュ

同じテキストに対するBERT推論結果をキャッシュして重複計算を回避：

```csharp
public class CachedBertRunner : IDisposable
{
    private readonly BertRunner _runner;
    private readonly LRUCache<string, float[]> _cache;

    public CachedBertRunner(BertRunner runner, int capacity = 64)
    {
        _runner = runner;
        _cache = new LRUCache<string, float[]>(capacity);
    }

    public float[] Run(string text, int[] tokenIds, int[] mask)
    {
        if (_cache.TryGet(text, out var cached))
            return cached;

        float[] result = _runner.Run(tokenIds, mask);
        _cache.Put(text, result);
        return result;
    }

    public void Dispose() => _runner?.Dispose();
}
```

### LRUCache 実装

```csharp
public class LRUCache<TKey, TValue>
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map;
    private readonly LinkedList<(TKey Key, TValue Value)> _list;

    public LRUCache(int capacity)
    {
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity);
        _list = new LinkedList<(TKey, TValue)>();
    }

    public bool TryGet(TKey key, out TValue value)
    {
        if (_map.TryGetValue(key, out var node))
        {
            _list.Remove(node);
            _list.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = default;
        return false;
    }

    public void Put(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            _list.Remove(existing);
            _map.Remove(key);
        }
        else if (_map.Count >= _capacity)
        {
            var last = _list.Last;
            _list.RemoveLast();
            _map.Remove(last.Value.Key);
        }
        var node = _list.AddFirst((key, value));
        _map[key] = node;
    }
}
```

> **Note**: この実装はメインスレッドからの単一スレッドアクセスを前提としている。複数スレッドから並行アクセスが必要な場合は `lock` による排他制御を追加すること。

---

## ウォームアップ推論

### 目的

初回のシェーダコンパイルをユーザーの操作前に完了させ、初回発話のレイテンシを削減する。

### 実装

```csharp
public class TTSWarmup
{
    /// <summary>
    /// ダミーデータで推論パイプラインを事前ウォームアップする。
    /// ロード画面やスプラッシュ画面中に実行する。
    /// </summary>
    public static IEnumerator WarmupAll(BertRunner bert, SBV2ModelRunner tts)
    {
        // BERT ウォームアップ（最小入力）
        int[] dummyTokens = { 1, 100, 2 };  // [CLS] x [SEP]
        int[] dummyMask = { 1, 1, 1 };
        bert.Run(dummyTokens, dummyMask);
        yield return null;

        // TTS ウォームアップ（最小入力）
        int dummySeqLen = 3;
        int[] dummyPhonemes = { 0, 1, 2 };
        int[] dummyTones = { 0, 0, 0 };
        int[] dummyLangs = { 1, 1, 1 };
        float[] dummyBert = new float[1 * 1024 * dummySeqLen];
        float[] dummyStyle = new float[256];

        tts.Run(dummyPhonemes, dummyTones, dummyLangs,
                0, dummyBert, dummyStyle,
                0.2f, 0.6f, 0.8f, 1.0f);
        yield return null;

        Debug.Log("TTS warmup complete.");
    }
}
```

### MonoBehaviour統合

```csharp
public class TTSManager : MonoBehaviour
{
    [SerializeField] private TTSSettings _settings;
    private BertRunner _bert;
    private SBV2ModelRunner _tts;
    private bool _isReady;

    private IEnumerator Start()
    {
        // モデルロード
        _bert = new BertRunner(_settings.BertModel, _settings.BertBackend);
        _tts = new SBV2ModelRunner(_settings.TTSModel, _settings.TTSBackend);

        // ウォームアップ
        if (_settings.EnableWarmup)
        {
            yield return TTSWarmup.WarmupAll(_bert, _tts);
        }

        _isReady = true;
    }

    public void Synthesize(string text)
    {
        if (!_isReady)
        {
            Debug.LogWarning("TTS is still warming up.");
            return;
        }
        StartCoroutine(SynthesizeCoroutine(new TTSRequest(text)));
    }
}
```

---

## GC圧力削減

### 問題

推論パイプラインで頻繁に `float[]` や `int[]` を確保するとGCスパイクが発生し、フレームレートが不安定になる。

### 実装済みの最適化 (A1-A4)

以下の最適化により、推論1回あたり **~470KB の GC アロケーション** を削減している。

| 最適化 | 対象 | 効果 |
|--------|------|------|
| A1: dest バッファオーバーロード | `BertRunner.Run(tokenIds, mask, dest)` | 出力配列の再利用 (~32KB/call) |
| A2: unsafe MemCpy | `SBV2ModelRunner` BERT パディング | Array.Copy → UnsafeUtility.MemCpy (2.0x) |
| A3: スカラーバッファ再利用 | `SBV2ModelRunner` `_scalarIntBuf`/`_scalarFloatBuf` | 6個の `new[]` 除去 (150 bytes/call) |
| A4: ArrayPool + GetTrimmedLength | `TTSPipeline` bertData/alignedBert + 末尾無音トリム | ~436KB/call のプーリング + コピー除去 |

### A1: BertRunner dest バッファオーバーロード

```csharp
// TTSPipeline での使用パターン: ArrayPool + dest overload
int bertLen = BertAligner.EmbeddingDimension * tokenIds.Length;
float[] bertData = ArrayPool<float>.Shared.Rent(bertLen);
try
{
    _bert.Run(tokenIds, attentionMask, bertData); // dest overload: GC alloc なし
    // bertData を使用...
}
finally
{
    ArrayPool<float>.Shared.Return(bertData);
}
```

### A2: SBV2ModelRunner unsafe パディング

```csharp
// BERT 埋め込みの [1024, seqLen] → [1024, padLen] パディング
// Before: Array.Clear + Array.Copy ループ (1024 回の呼び出し)
// After:  UnsafeUtility.MemClear + MemCpy (bounds check なし、2.0x 高速)
unsafe
{
    fixed (float* srcPtr = jaBertEmbedding, dstPtr = _paddedJaBert)
    {
        UnsafeUtility.MemClear(dstPtr, (long)HiddenSize * _padLen * sizeof(float));
        for (int h = 0; h < HiddenSize; h++)
            UnsafeUtility.MemCpy(
                dstPtr + h * _padLen,
                srcPtr + h * seqLen,
                seqLen * sizeof(float));
    }
}
```

### A3: スカラーバッファ再利用

```csharp
// Before: 推論ごとに 6 個の配列を確保
using var sidTensor = new Tensor<int>(new TensorShape(1), new[] { speakerId });      // GC alloc
using var sdpTensor = new Tensor<float>(new TensorShape(1), new[] { sdpRatio });     // GC alloc

// After: フィールドに事前確保したバッファを再利用
private readonly int[] _scalarIntBuf = new int[1];
private readonly float[] _scalarFloatBuf = new float[1];

_scalarIntBuf[0] = speakerId;
using var sidTensor = new Tensor<int>(new TensorShape(1), _scalarIntBuf);            // 0 GC
_scalarFloatBuf[0] = sdpRatio;
using var sdpTensor = new Tensor<float>(new TensorShape(1), _scalarFloatBuf);        // 0 GC
```

### A4: ArrayPool + GetTrimmedLength

```csharp
// TTSPipeline: bertData と alignedBert を ArrayPool で管理
int bertLen = BertAligner.EmbeddingDimension * tokenIds.Length;
float[] bertData = ArrayPool<float>.Shared.Rent(bertLen);
float[] alignedBert = null;
try
{
    _bert.Run(tokenIds, attentionMask, bertData);
    int alignedLen = BertAligner.EmbeddingDimension * phoneSeqLen;
    alignedBert = ArrayPool<float>.Shared.Rent(alignedLen);
    BertAligner.AlignBertToPhonemesBurst(bertData, tokenIds.Length, word2ph, phoneSeqLen, alignedBert);
    // ... TTS 推論 ...

    // 末尾無音トリム: 配列コピーせず長さのみ返す
    int trimmedLength = GetTrimmedLength(audioSamples);
    clip = AudioClip.Create("TTS", trimmedLength, 1, _sampleRate, false);
    clip.SetData(audioSamples, 0); // 元の配列から直接 SetData
}
finally
{
    if (alignedBert != null) ArrayPool<float>.Shared.Return(alignedBert);
    ArrayPool<float>.Shared.Return(bertData);
}
```

**注意**: Sentis の `DownloadToArray()` 自体が新規配列を確保するため、完全なGC回避にはならない。Sentis APIの制約として受け入れ、その後の処理で`ArrayPool`を活用する。

---

## プロファイリング推奨手法

### Unity Profilerマーカー

```csharp
using Unity.Profiling;

public class TTSPipeline
{
    private static readonly ProfilerMarker s_G2PMarker = new("TTS.G2P");
    private static readonly ProfilerMarker s_BertMarker = new("TTS.BertInference");
    private static readonly ProfilerMarker s_AlignMarker = new("TTS.BertAlignment");
    private static readonly ProfilerMarker s_TTSMarker = new("TTS.SynthesizerTrn");
    private static readonly ProfilerMarker s_AudioMarker = new("TTS.AudioGeneration");

    public AudioClip Synthesize(TTSRequest request)
    {
        G2PResult g2p;
        using (s_G2PMarker.Auto())
            g2p = _g2p.Process(request.Text);

        float[] bertData;
        using (s_BertMarker.Auto())
        {
            var (tokens, mask) = _tokenizer.Encode(request.Text);
            bertData = _bert.Run(tokens, mask);
        }

        float[] aligned;
        using (s_AlignMarker.Auto())
            aligned = AlignBertToPhonemes(bertData, g2p.Word2Ph, g2p.PhonemeIds.Length);

        float[] audio;
        using (s_TTSMarker.Auto())
            audio = _tts.Run(g2p, aligned, request);

        AudioClip clip;
        using (s_AudioMarker.Auto())
            clip = TTSAudioUtility.CreateClip(audio);

        return clip;
    }
}
```

### ステージ別目標レイテンシ

| ステージ | 目標 (GPU) | 目標 (CPU) | 計測方法 |
|---|---|---|---|
| G2P | < 30ms | < 50ms | `TTS.G2P` Profilerマーカー |
| BERT推論 | < 50ms | < 300ms | `TTS.BertInference` マーカー |
| BERT展開 | < 5ms | < 20ms | `TTS.BertAlignment` マーカー |
| TTS推論 | < 50ms | < 200ms | `TTS.SynthesizerTrn` マーカー |
| 音声生成 | < 5ms | < 10ms | `TTS.AudioGeneration` マーカー |
| **合計** | **< 140ms** | **< 580ms** | 全マーカー合計 |

---

## 注意事項

- **DeBERTaがボトルネック**: 全体レイテンシの半分以上をDeBERTa推論が占める。BERT埋め込みキャッシュが最も効果的な最適化
- **GPU VRAM不足**: 合計~1GB以上のVRAMが必要。VRAM不足時は`BackendType.CPU`にフォールバック、またはDeBERTaのみCPUで実行する混合戦略も有効
- **ScheduleIterable vs Schedule**: リアルタイム性が求められる場合は`Schedule()`（ブロッキングだが低レイテンシ）、UI応答性が求められる場合は`ScheduleIterable()`を使い分ける
- **プロファイリング必須**: 実機での計測なしに最適化を進めない。Unity Profilerの`TTS.*`マーカーで各ステージのボトルネックを特定してから対処する
