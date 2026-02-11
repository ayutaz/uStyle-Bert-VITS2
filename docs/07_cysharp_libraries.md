# Cysharpライブラリ活用ガイド

## 概要

Style-Bert-VITS2 Unity実装の推論パイプラインにおけるGC圧力削減・非同期制御・文字列処理・ネイティブメモリ管理の最適化課題に対し、Cysharp（neuecc氏）のOSSライブラリ群を活用する方針をまとめる。

### 導入状況

| ライブラリ | 状況 | 用途 |
|---|---|---|
| **UniTask** | **導入済み** | 非同期パイプライン (SynthesizeAsync, TTSRequestQueue) |
| **ZString** | **導入済み** | ゼロアロケーション文字列処理 |
| NativeMemoryArray | 未導入 | ネイティブメモリバッファ |
| ZLinq | 未導入 | ゼロアロケーションLINQ |
| csbindgen | 未導入 | P/Invoke自動生成 |
| MemoryPack | 未導入 | 高速バイナリシリアライザ |
| R3 | 未導入 | Reactive Extensions |

本ドキュメントは `docs/05_performance_optimization.md`（パフォーマンス最適化）と `docs/06_csharp_optimization.md`（C#高速化）の手法を前提とし、Cysharpライブラリによる改善・置き換えを提案する。

---

## Tier 1: 強く推奨（推論パイプラインに直接効果）

### 1. UniTask — ゼロアロケーション非同期

- **リポジトリ**: https://github.com/Cysharp/UniTask
- **バージョン**: 2.5.10

**概要**: struct-based `UniTask<T>` によるゼロアロケーション async/await。コルーチンと `Task<T>` の両方を置き換える。

**Unity 6互換性**: 完全互換。Unity 6の `Awaitable` は `.AsUniTask()` で変換可能。neuecc氏はアプリケーション開発にはUniTaskを推奨。

**本プロジェクトでの活用**:
- 推論パイプライン全体（G2P → BERT → TTS → AudioClip）の非同期オーケストレーション
- `UniTask.SwitchToThreadPool()` / `SwitchToMainThread()` でG2P/トークナイズをバックグラウンド実行
- `UniTask.WhenAll()` で複数サブモデルの並列推論を管理
- `CancellationToken` 統合で推論中断をサポート
- `Channel<T>` でTTSリクエストのプロデューサー・コンシューマーキュー実装

**現状との比較**: 05/06では `Awaitable` + コルーチンを手書きしているが、UniTaskはより豊富なAPIとゼロアロケーション保証を提供。

**導入コスト**: 低。UPM Git URLで即導入可能。既存のAwaitableコードと共存可能。

**UPMインストール**:
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10
```

---

### 2. ZString — ゼロアロケーション文字列処理

- **リポジトリ**: https://github.com/Cysharp/ZString
- **バージョン**: 2.6.0

**概要**: struct-basedの `ZString.CreateStringBuilder()` / `ZString.Concat()` でヒープアロケーションなしの文字列操作。

**Unity 6互換性**: Unity 2021.3+対応。.NET Standard 2.0+。

**本プロジェクトでの活用**:
- トークナイザ/G2Pの文字列処理（音素列連結、トークン文字列構築）でのGC削減
- デバッグログ `Debug.Log($"...")` の補間文字列によるGCアロケーション排除
- `ZString.Format()` でProfiler計測値のフォーマットをGCフリーに

**現状との比較**: 06では `Span<char>` + `ToString()` の手書きパターン。ZStringはより包括的なゼロアロケーション文字列API。

**注意**: 後継の `Utf8StringInterpolation` はC# 10+必要のためUnity 6では使用不可。ZStringが正解。

**UPMインストール**:
```
https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString#2.6.0
```

> **依存**: `System.Runtime.CompilerServices.Unsafe` (6.0.0) が必要。Git URLインストール時は別途追加すること。unitypackageには同梱。

---

### 3. NativeMemoryArray — ネイティブメモリバッファ

- **リポジトリ**: https://github.com/Cysharp/NativeMemoryArray
- **バージョン**: 1.2.2

**概要**: `NativeMemory.Alloc` によるGCヒープ外の配列。`Span<T>` / `Memory<T>` ビューを提供。

**Unity 6互換性**: .NET Standard 2.0 + System.Memory対応。

**本プロジェクトでの活用**:
- BERT埋め込みバッファ `float[1024 * seq_len]`、スタイルベクトル `float[256]`、音声出力バッファをGCヒープ外に配置
- Sentis `Tensor<float>` 作成時のステージングバッファとして使用（Span経由で直接書き込み）
- 長時間保持する大容量バッファ（音声PCMデータ等）のGC圧力を完全に排除

**現状との比較**: 06では `ArrayPool<T>.Shared` を使用。ArrayPoolは一時的なレンタルには適するが、長期保持バッファやGC完全回避には NativeMemoryArray が優位。

**注意**: 手動Disposeが必須（IDisposable）。忘れるとネイティブメモリリーク。

**UPMインストール**:

初回はunitypackage（依存DLL同梱）を推奨:
```
https://github.com/Cysharp/NativeMemoryArray/releases
```

Git URL（依存DLLは別途追加が必要）:
```
https://github.com/Cysharp/NativeMemoryArray.git?path=src/NativeMemoryArray.Unity/Assets/Plugins/NativeMemoryArray
```

> **依存**: `System.Memory.dll`, `System.Buffers.dll`, `System.Runtime.CompilerServices.Unsafe.dll` が必要。

---

### 4. ZLinq — ゼロアロケーションLINQ

- **リポジトリ**: https://github.com/Cysharp/ZLinq
- **バージョン**: 1.5.4

**概要**: struct-based enumerableによるゼロアロケーションLINQ。SIMD自動ベクトル化、LINQ to Span含む。

**Unity 6互換性**: Unity 2022.3.12f1+。.NET Standard 2.1対応。LINQ to Span機能はC# 13必要のためUnity 6では使用不可だが、コアのゼロアロケーションLINQは動作。

**本プロジェクトでの活用**:
- トークン配列処理の `.Select()`, `.Where()`, `.ToArray()` をゼロアロケーション化
- 音声データの集約演算（Sum, Min, Max, Average）でSIMD自動適用の可能性

**現状との比較**: 06ではLINQを避けて手書きループを推奨しているが、ZLinqならLINQの可読性とゼロアロケーションを両立。

**注意**: Unity 6ではSIMD完全対応は限定的（.NET 8+で最大効果）。

**UPMインストール**（2段階）:

1. [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) で `ZLinq` コアパッケージをインストール
2. Git URLでUnity統合パッケージを追加:
```
https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity
```

---

## Tier 2: 推奨（アーキテクチャ改善に有効）

### 5. csbindgen — P/Invoke自動生成

- **リポジトリ**: https://github.com/Cysharp/csbindgen
- **バージョン**: 1.9.6

**概要**: Rust/CのFFI関数からC# P/Invokeバインディングを自動生成。

**本プロジェクトでの活用**:
- OpenJTalk P/Invokeの手書きDllImportを自動生成に置き換え
- プラットフォーム別DLL名の自動切り替え（`csharp_dll_name_if`）
- 構造体マーシャリングの安全性向上

**注意**: ビルド時にRustツールチェーンが必要（生成されたC#にはRust依存なし）。Unityパッケージではなく、Rust側のビルド依存。

**インストール** (Rustプロジェクト側):
```toml
[build-dependencies]
csbindgen = "1.9.6"
```

---

### 6. MemoryPack — 高速バイナリシリアライザ

- **リポジトリ**: https://github.com/Cysharp/MemoryPack
- **バージョン**: 1.21.4

**概要**: Source Generatorベースのバイナリシリアライザ。C#メモリを直接コピーしてゼロエンコーディング。

**Unity 6互換性**: Unity 2022.3.12f1+。.NET Standard 2.1。IL2CPP対応（AOT-safe）。

**本プロジェクトでの活用**:
- 事前計算BERT埋め込みのキャッシュ保存/読み込み（JSONの10倍以上高速）
- G2P辞書データのバイナリシリアライズ（起動時ロード高速化）
- `style_vectors.npy` 相当のデータをMemoryPackで保持

**注意**: バイナリ形式のためPython側との相互運用には追加作業が必要。`.npy`ファイルには専用リーダーの方がシンプル。

**UPMインストール**（2段階）:

1. [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) で `MemoryPack` コアパッケージをインストール
2. Git URLでUnity統合パッケージを追加:
```
https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity#1.21.4
```

---

### 7. R3 — Reactive Extensions (UniRx後継)

- **リポジトリ**: https://github.com/Cysharp/R3
- **バージョン**: 1.3.0

**概要**: UniRxの完全リライト。フレームベースオペレータ、ReactiveProperty、100+ LINQオペレータ。

**Unity 6互換性**: Unity 2021.3+。.NET Standard 2.1。

**本プロジェクトでの活用**:
- TTSパイプラインをリアクティブストリームとしてモデル化（入力テキスト→推論→音声出力）
- `ReactiveProperty<string>` でUI入力バインディング
- `DebounceFrame()` でテキスト入力のデバウンス（推論の無駄な再実行を防止）

**注意**: UniTaskだけで非同期制御が十分な場合はオーバーエンジニアリングになる可能性。

**UPMインストール**（2段階）:

1. [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) で `R3` コアパッケージをインストール
2. Git URLでUnity統合パッケージを追加:
```
https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.0
```

---

## Tier 3: 条件付き推奨

| ライブラリ | 用途 | 導入条件 |
|---|---|---|
| **ZLogger** | ゼロアロケーション構造化ログ | `csc.rsp` でC# 10有効化が必要。ログ量が多い場合に有効 |
| **StructureOfArraysGenerator** | SoAデータレイアウト | バッチ推論（複数文同時処理）を行う場合に有効 |
| **NativeCompressions** | LZ4/Zstd圧縮 | モデル配布サイズが問題になる場合 |
| **MessagePipe** | Pub/Subメッセージング | DIコンテナ（VContainer等）を使う場合 |
| **ObservableCollections** | Observable集合 | UI表示が複雑な場合 |

---

## 対象外ライブラリ

| ライブラリ | 除外理由 |
|---|---|
| MagicOnion | gRPCフレームワーク。ローカルAPIサーバー方式を採用しない限り不要 |
| Utf8StringInterpolation | C# 10+必須。Unity 6ではZStringを使用 |
| SimdLinq | ZLinqに後継統合済み。ZLinqを使用 |
| MasterMemory | 組み込みDB。G2P辞書には通常のDictionaryで十分 |

---

## プロジェクト課題とライブラリの対応マッピング

| 最適化課題 (docs/05,06) | 現在の手法 | Cysharpライブラリ | 改善ポイント |
|---|---|---|---|
| 非同期推論オーケストレーション | `Awaitable` + コルーチン手書き | **UniTask** | ゼロアロケーション、WhenAll、Channel、CancellationToken |
| float[]/int[] GC圧力 | `ArrayPool<T>.Shared` | **NativeMemoryArray** | GCヒープ完全回避、Span\<T\>直接アクセス |
| 文字列処理GC | `Span<char>` + `ToString()` | **ZString** | ゼロアロケーション文字列操作API |
| LINQのGCアロケーション | 手書きループ | **ZLinq** | LINQの可読性 + ゼロアロケーション |
| P/Invoke安全性 | 手書き `DllImport` + `SafeHandle` | **csbindgen** | 自動生成で型安全性・クロスプラットフォーム対応 |
| データシリアライズ速度 | JSON (`JsonConvert`) | **MemoryPack** | 10倍以上高速なバイナリシリアライズ |
| デバッグログGC | `Debug.Log($"...")` | **ZString** or **ZLogger** | ログのGCアロケーション排除 |
| LRUCache (手書き) | `Dictionary` + `LinkedList` | (該当なし) | Cysharpには汎用キャッシュなし。現行実装で十分 |

---

## UPMインストール手順まとめ

### 前提: NuGetForUnityの導入

ZLinq、MemoryPack、R3はNuGetForUnityが必要。先にインストールしておく:

```
https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
```

### Tier 1 ライブラリのインストール

`Packages/manifest.json` の `dependencies` に以下を追加:

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.10",
    "com.cysharp.zstring": "https://github.com/Cysharp/ZString.git?path=src/ZString.Unity/Assets/Scripts/ZString#2.6.0"
  }
}
```

NativeMemoryArrayは依存DLLの都合上、unitypackageからの初回導入を推奨:
1. [Releases](https://github.com/Cysharp/NativeMemoryArray/releases) から `.unitypackage` をダウンロード
2. Unity Editorで `Assets > Import Package > Custom Package` からインポート

ZLinqはNuGetForUnity経由:
1. `Window > NuGet > Manage NuGet Packages` で `ZLinq` を検索してインストール
2. `manifest.json` に追加:
```json
"com.cysharp.zlinq": "https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity"
```

### Tier 2 ライブラリのインストール

MemoryPack:
1. NuGetForUnityで `MemoryPack` をインストール
2. `manifest.json` に追加:
```json
"com.cysharp.memorypack": "https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity#1.21.4"
```

R3:
1. NuGetForUnityで `R3` をインストール
2. `manifest.json` に追加:
```json
"com.cysharp.r3": "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity#1.3.0"
```

---

## Before/After コード例

### 1. UniTask: 推論パイプラインの非同期オーケストレーション

**Before** (docs/05 — Awaitable + コルーチン):

```csharp
// docs/05: async Awaitable パターン
public async Awaitable<AudioClip> SynthesizeAsync(TTSRequest request)
{
    G2PResult g2p;
    await Awaitable.BackgroundThreadAsync();
    g2p = _g2p.Process(request.Text);

    var (tokens, mask) = _tokenizer.Encode(request.Text);

    await Awaitable.MainThreadAsync();

    // BERT推論
    SetBertInputs(tokens, mask);
    _bertWorker.Schedule();
    await Awaitable.EndOfFrameAsync();

    var bertOut = _bertWorker.PeekOutput() as Tensor<float>;
    bertOut.ReadbackAndClone();
    float[] bertData = bertOut.DownloadToArray();

    float[] aligned;
    await Awaitable.BackgroundThreadAsync();
    aligned = AlignBertToPhonemes(bertData, g2p.Word2Ph, g2p.PhonemeIds.Length);

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

**After** (UniTask):

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;

public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken ct = default)
{
    // G2P + トークナイズをバックグラウンドスレッドで実行
    await UniTask.SwitchToThreadPool();
    ct.ThrowIfCancellationRequested();

    var g2p = _g2p.Process(request.Text);
    var (tokens, mask) = _tokenizer.Encode(request.Text);

    // Sentis Worker はメインスレッドでのみ Schedule 可能
    await UniTask.SwitchToMainThread(ct);

    // BERT推論
    SetBertInputs(tokens, mask);
    _bertWorker.Schedule();
    await UniTask.Yield(ct);  // フレーム境界 — ゼロアロケーション

    var bertOut = _bertWorker.PeekOutput() as Tensor<float>;
    bertOut.ReadbackAndClone();
    float[] bertData = bertOut.DownloadToArray();

    // アライメントをバックグラウンドで
    await UniTask.SwitchToThreadPool();
    float[] aligned = AlignBertToPhonemes(bertData, g2p.Word2Ph, g2p.PhonemeIds.Length);

    // TTS推論（メインスレッド）
    await UniTask.SwitchToMainThread(ct);
    SetTTSInputs(g2p, aligned, request);
    _ttsWorker.Schedule();
    await UniTask.Yield(ct);

    var audioOut = _ttsWorker.PeekOutput("output") as Tensor<float>;
    audioOut.ReadbackAndClone();
    float[] samples = audioOut.DownloadToArray();

    return TTSAudioUtility.CreateClip(samples);
}
```

**UniTask 固有の追加パターン**:

```csharp
// 1. キャンセル対応の呼び出し
public class TTSManager : MonoBehaviour
{
    private CancellationTokenSource _cts;

    public void Synthesize(string text)
    {
        // 前回の推論をキャンセル
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        SynthesizeAsync(new TTSRequest(text), _cts.Token).Forget();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

// 2. Channel<T> による TTSリクエストキュー
public class TTSRequestQueue : MonoBehaviour
{
    private readonly Channel<TTSRequest> _channel = Channel.CreateSingleConsumerUnbounded<TTSRequest>();

    private void Start()
    {
        ProcessQueueAsync(destroyCancellationToken).Forget();
    }

    public void Enqueue(TTSRequest request)
    {
        _channel.Writer.TryWrite(request);
    }

    private async UniTaskVoid ProcessQueueAsync(CancellationToken ct)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(ct))
        {
            var clip = await SynthesizeAsync(request, ct);
            _audioSource.PlayOneShot(clip);
        }
    }
}

// 3. WhenAll で複数文を並列推論（BERT部分のみ並列化の例）
public async UniTask<float[][]> RunBertBatchAsync(string[] texts, CancellationToken ct)
{
    var tasks = texts.Select(text =>
    {
        var (tokens, mask) = _tokenizer.Encode(text);
        return RunBertSingleAsync(tokens, mask, ct);
    });
    return await UniTask.WhenAll(tasks);
}
```

**改善ポイント**:
- `Awaitable.EndOfFrameAsync()` → `UniTask.Yield()`: ゼロアロケーション
- `CancellationToken` 統合: 推論中断が容易
- `Channel<T>`: リクエストキューイングが簡潔
- `.Forget()`: Fire-and-forget の警告なし呼び出し

---

### 2. ZString: G2P/トークナイザの文字列処理

**Before** (docs/06 — Span + ToString):

```csharp
// docs/06: デバッグログで $"..." を使用（GCアロケーション発生）
Debug.Log($"G2P result: {g2pResult.PhonemeIds.Length} phonemes, time={elapsed.TotalMilliseconds:F1}ms");

// docs/06: 音素列の文字列結合（デバッグ用）
string phonemeStr = string.Join(", ", phonemes);
Debug.Log($"Phonemes: [{phonemeStr}]");

// docs/05: フォールバック時のログ
Debug.LogWarning($"Backend {preferred} failed: {e.Message}. Falling back to {fallback}.");
```

**After** (ZString):

```csharp
using Cysharp.Text;

// ZString.Format: 補間文字列のGCアロケーションを排除
Debug.Log(ZString.Format("G2P result: {0} phonemes, time={1:F1}ms",
    g2pResult.PhonemeIds.Length, elapsed.TotalMilliseconds));

// ZString.CreateStringBuilder: StringBuilder相当のゼロアロケーション結合
using (var sb = ZString.CreateStringBuilder())
{
    sb.Append("Phonemes: [");
    for (int i = 0; i < phonemes.Length; i++)
    {
        if (i > 0) sb.Append(", ");
        sb.Append(phonemes[i]);
    }
    sb.Append(']');
    Debug.Log(sb.ToString());
}

// ZString.Concat: 単純な連結
Debug.LogWarning(ZString.Concat("Backend ", preferred, " failed: ", e.Message,
    ". Falling back to ", fallback, "."));
```

**G2P音素列構築での活用**:

```csharp
// Before: string.Join による音素列構築（デバッグ/ログ用途）
public string PhonemeSequenceToString(string[] phonemes)
{
    // string.Join は内部で string[] + StringBuilder を確保 → GC圧力
    return string.Join(" ", phonemes);
}

// After: ZString によるゼロアロケーション構築
public string PhonemeSequenceToString(ReadOnlySpan<string> phonemes)
{
    using var sb = ZString.CreateStringBuilder();
    for (int i = 0; i < phonemes.Length; i++)
    {
        if (i > 0) sb.Append(' ');
        sb.Append(phonemes[i]);
    }
    return sb.ToString();
}
```

**改善ポイント**:
- `$"..."` → `ZString.Format()`: 補間文字列のboxingとstring確保を排除
- `string.Join()` → `ZString.CreateStringBuilder()`: StringBuilder内部バッファのGCアロケーション回避
- 推論パイプライン中のログ出力がGCフリーに

---

### 3. NativeMemoryArray: BERT埋め込みバッファ管理

**Before** (docs/06 — ArrayPool):

```csharp
using System.Buffers;

public class BertAligner
{
    public (float[] Buffer, int Length) AlignBertToPhonemes(
        float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
    {
        int embDim = 1024;
        int requiredSize = embDim * phoneSeqLen;

        // ArrayPoolからレンタル（GCアロケーション回避だが、GCヒープ上に存在）
        float[] aligned = ArrayPool<float>.Shared.Rent(requiredSize);

        int phoneIdx = 0;
        int tokenIdx = 0;
        for (int w = 0; w < word2ph.Length; w++)
        {
            for (int p = 0; p < word2ph[w]; p++)
            {
                for (int d = 0; d < embDim; d++)
                    aligned[d * phoneSeqLen + phoneIdx] = bertFlat[d * tokenLen + tokenIdx];
                phoneIdx++;
            }
            tokenIdx++;
        }

        return (aligned, requiredSize);
    }
}

// 呼び出し側: Return忘れの危険あり
var (buffer, length) = aligner.AlignBertToPhonemes(bertData, tokenLen, word2ph, seqLen);
try
{
    using var tensor = new Tensor<float>(new TensorShape(1, 1024, seqLen), buffer);
    worker.SetInput("bert", tensor);
    worker.Schedule();
}
finally
{
    ArrayPool<float>.Shared.Return(buffer);  // 忘れるとプール枯渇
}
```

**After** (NativeMemoryArray):

```csharp
using Cysharp.Collections;

/// <summary>
/// 推論パイプラインで使い回すネイティブメモリバッファ群。
/// GCヒープ外に配置されるため、GCスキャン対象にならない。
/// </summary>
public class InferenceBuffers : IDisposable
{
    // 長期保持バッファ — GCヒープ外
    private NativeMemoryArray<float> _bertAligned;
    private NativeMemoryArray<float> _styleVector;
    private NativeMemoryArray<float> _audioOutput;
    private int _currentPhoneSeqLen;

    public InferenceBuffers(int maxPhoneSeqLen = 512, int maxAudioSamples = 44100 * 10)
    {
        _bertAligned = new NativeMemoryArray<float>(1024 * maxPhoneSeqLen);
        _styleVector = new NativeMemoryArray<float>(256);
        _audioOutput = new NativeMemoryArray<float>(maxAudioSamples);
    }

    /// <summary>
    /// BERT展開をネイティブメモリ上で直接実行。
    /// Span<float> 経由でアクセスするためコピー不要。
    /// </summary>
    public Span<float> AlignBertToPhonemes(
        float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
    {
        int embDim = 1024;
        int requiredSize = embDim * phoneSeqLen;
        _currentPhoneSeqLen = phoneSeqLen;

        // 必要に応じてバッファを拡張
        if (_bertAligned.Length < requiredSize)
        {
            _bertAligned.Dispose();
            _bertAligned = new NativeMemoryArray<float>(requiredSize);
        }

        // Span<float> でネイティブメモリに直接書き込み
        var span = _bertAligned.AsSpan(0, requiredSize);
        int phoneIdx = 0;
        int tokenIdx = 0;
        for (int w = 0; w < word2ph.Length; w++)
        {
            for (int p = 0; p < word2ph[w]; p++)
            {
                for (int d = 0; d < embDim; d++)
                    span[d * phoneSeqLen + phoneIdx] = bertFlat[d * tokenLen + tokenIdx];
                phoneIdx++;
            }
            tokenIdx++;
        }

        return span;
    }

    /// <summary>
    /// スタイルベクトルをネイティブメモリに書き込み
    /// </summary>
    public Span<float> GetStyleVectorBuffer()
    {
        return _styleVector.AsSpan(0, 256);
    }

    public void Dispose()
    {
        _bertAligned?.Dispose();
        _styleVector?.Dispose();
        _audioOutput?.Dispose();
    }
}

// 使用例: バッファはパイプライン寿命と一致（フレームごとのReturn不要）
public class TTSPipeline : IDisposable
{
    private readonly InferenceBuffers _buffers = new(maxPhoneSeqLen: 512);

    public AudioClip Synthesize(TTSRequest request)
    {
        var g2p = _g2p.Process(request.Text);
        var (tokens, mask) = _tokenizer.Encode(request.Text);
        float[] bertData = _bert.Run(tokens, mask);

        // ネイティブメモリ上で直接BERT展開 — GCアロケーションゼロ
        Span<float> aligned = _buffers.AlignBertToPhonemes(
            bertData, tokens.Length, g2p.Word2Ph, g2p.PhonemeIds.Length);

        // Span<float> から Tensor を作成
        // (Tensor<float> コンストラクタが Span を受け取る場合)
        float[] alignedArray = aligned.ToArray(); // Sentis API制約でToArray必要な場合
        using var tensor = new Tensor<float>(
            new TensorShape(1, 1024, g2p.PhonemeIds.Length), alignedArray);
        // ...

        return clip;
    }

    public void Dispose() => _buffers?.Dispose();
}
```

**改善ポイント**:
- `ArrayPool.Rent/Return` → `NativeMemoryArray`: Return忘れのリスクが解消、ライフサイクルが明確
- バッファがGCヒープ外: 大容量バッファ（BERT: ~400KB、音声: 数MB）がGCスキャン対象外に
- `Span<T>` 経由の直接アクセス: 中間コピーを最小化
- `IDisposable` パターンでライフサイクル管理が統一

**ArrayPoolとの使い分け**:

| 用途 | 推奨 | 理由 |
|---|---|---|
| 推論1回限りの一時バッファ | `ArrayPool<T>.Shared` | Rent/Returnが簡潔 |
| パイプライン寿命の長期バッファ | `NativeMemoryArray<T>` | GC完全回避、Return不要 |
| 音声出力の蓄積バッファ | `NativeMemoryArray<T>` | 数MB規模のGC圧力を排除 |
| スタイルベクトル (256要素) | `NativeMemoryArray<T>` | 固定サイズ、使い回し |

---

## Unity 6 互換性サマリー

| ライブラリ | 最小Unity | .NET Standard | C#要件 | NuGetForUnity | 備考 |
|---|---|---|---|---|---|
| UniTask | 2018.4.13 | 2.0+ | C# 7.0+ | 不要 | Awaitable変換対応 |
| ZString | 2021.3 | 2.0+ | — | 不要 | Unsafe.dll依存 |
| NativeMemoryArray | — | 2.0+ | — | 不要 | System.Memory依存 |
| ZLinq | 2022.3.12 | 2.1 | — | **必要** | LINQ to SpanはC# 13のため不可 |
| csbindgen | N/A | N/A | N/A | N/A | Rustビルドツール |
| MemoryPack | 2022.3.12 | 2.1 | — | **必要** | Source Generator、IL2CPP対応 |
| R3 | 2021.3 | 2.1 | — | **必要** | PlayerLoop統合 |

全ライブラリがUnity 6 (6000.x) + C# 9.0環境で動作する。

---

## 注意事項

- **段階的導入を推奨**: まずUniTaskを導入し、次にZString、その後NativeMemoryArrayの順に導入する。一度に全てを導入すると問題切り分けが困難になる
- **NativeMemoryArrayのDispose**: `using`文またはIDisposableパターンで必ずDisposeすること。ネイティブメモリリークはUnity Profilerでは検出しにくい
- **ZLinqのNuGetForUnity依存**: NuGetForUnityの導入自体がプロジェクトにDLLを追加するため、ビルドサイズへの影響を確認すること
- **過剰最適化の回避**: docs/06の注意事項と同様、まず動作するパイプラインを構築し、Profilerでボトルネックを特定してからCysharpライブラリを適用する
