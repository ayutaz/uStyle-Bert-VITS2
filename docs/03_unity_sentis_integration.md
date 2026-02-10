# Unity Sentis統合ガイド (Style-Bert-VITS2)

## 基本情報

- **パッケージ**: `com.unity.ai.inference` 2.5.0
- **Namespace**: `Unity.InferenceEngine`（`Unity.Sentis`は旧名）
- **参考実装**: [uCosyVoice](https://github.com/ayutaz/uCosyVoice) — マルチモデルSentisパイプラインの実証済みパターン

---

## Sentis基本API (uCosyVoiceで実証済み)

### モデル読み込み

```csharp
using Unity.InferenceEngine;

// Resources/Models/ にONNXを配置
var asset = Resources.Load<ModelAsset>("Models/sbv2_model");
Model model = ModelLoader.Load(asset);
Worker worker = new Worker(model, BackendType.GPUCompute);
```

### Worker実行サイクル

```csharp
// 1. 入力テンソル設定
worker.SetInput("input_name", tensor);

// 2. 実行
worker.Schedule();

// 3. 出力取得
var output = worker.PeekOutput("output_name") as Tensor<float>;

// 4. GPU→CPU読み戻し (データアクセスが必要な場合)
output.ReadbackAndClone();
float[] data = output.DownloadToArray();
```

### BackendType

| タイプ | 用途 | 備考 |
|---|---|---|
| `BackendType.GPUCompute` | 速度優先（推奨） | DirectML使用。精度問題が出る場合あり |
| `BackendType.CPU` | 安定性優先 | Burstコンパイラ使用。uCosyVoiceでは一部CPU推奨 |

---

## モデル間データチェーン (uCosyVoiceパターン)

複数モデルのパイプラインでは、モデル間のデータ受け渡しにCPUメモリを経由する:

```csharp
// Worker A の出力を Worker B の入力にする
workerA.SetInput("input", inputTensor);
workerA.Schedule();

// 出力をCPUに読み戻す
var outputA = workerA.PeekOutput() as Tensor<float>;
outputA.ReadbackAndClone();
var data = outputA.DownloadToArray();

// 新しいテンソルとしてWorker Bに渡す
var inputB = new Tensor<float>(new TensorShape(1, 1024, seqLen), data);
workerB.SetInput("bert", inputB);
workerB.Schedule();

// テンソルの破棄
inputB.Dispose();
```

**重要**: Worker間でテンソルを直接共有する方法はない。必ず `DownloadToArray()` → 新 `Tensor` の流れ。

---

## テンソル作成パターン

### int32テンソル

```csharp
// 1次元
var sid = new Tensor<int>(new TensorShape(1), new int[] { speakerId });

// 2次元 (音素ID列)
var phonemes = new Tensor<int>(new TensorShape(1, seqLen), phonemeIdArray);
```

### float32テンソル

```csharp
// スカラー (SBV2のsdp_ratio等)
var sdpRatio = new Tensor<float>(new TensorShape(1), new float[] { 0.2f });

// 2次元 (スタイルベクトル)
var styleVec = new Tensor<float>(new TensorShape(1, 256), styleVectorArray);

// 3次元 (BERT埋め込み)
var bert = new Tensor<float>(new TensorShape(1, 1024, seqLen), bertDataArray);
```

---

## IDisposableパターン

uCosyVoiceで実証されたリソース管理:

### クラス階層

```
SBV2TTSManager : MonoBehaviour, IDisposable
  ├── BertRunner : IDisposable        (Worker × 1)
  ├── SBV2ModelRunner : IDisposable   (Worker × 1)
  └── StyleVectorProvider             (データのみ、Worker不要)
```

### Dispose実装

```csharp
public class BertRunner : IDisposable
{
    private Worker _worker;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _worker?.Dispose();
        _disposed = true;
    }
}
```

### テンソルの using パターン

```csharp
// 一時テンソルは using で確実に破棄
using var tokenTensor = new Tensor<int>(new TensorShape(1, tokenLen), tokenIds);
using var maskTensor = new Tensor<int>(new TensorShape(1, tokenLen), attentionMask);

worker.SetInput("input_ids", tokenTensor);
worker.SetInput("attention_mask", maskTensor);
worker.Schedule();
```

---

## AudioClip生成

```csharp
public static class TTSAudioUtility
{
    /// <summary>
    /// float32 PCM配列からAudioClipを生成 (44100Hz mono)
    /// </summary>
    public static AudioClip CreateClip(float[] samples, int sampleRate = 44100, string name = "TTS")
    {
        // 正規化
        float maxAbs = 0f;
        for (int i = 0; i < samples.Length; i++)
            maxAbs = Mathf.Max(maxAbs, Mathf.Abs(samples[i]));

        if (maxAbs > 0f)
        {
            float scale = 0.95f / maxAbs;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= scale;
        }

        var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
```

SBV2の出力は 44100Hz mono。uCosyVoiceは 24000Hz なので注意。

---

## 推論パイプライン全体像

### BertRunner

```csharp
public class BertRunner : IDisposable
{
    private Worker _worker;

    public BertRunner(ModelAsset modelAsset, BackendType backendType)
    {
        var model = ModelLoader.Load(modelAsset);
        _worker = new Worker(model, backendType);
    }

    /// <summary>
    /// DeBERTa推論: テキストトークン → BERT埋め込み
    /// </summary>
    /// <param name="tokenIds">トークンID配列 [CLS] ... [SEP]</param>
    /// <param name="attentionMask">アテンションマスク (全1)</param>
    /// <returns>BERT埋め込み [1, 1024, token_len]</returns>
    public float[] Run(int[] tokenIds, int[] attentionMask)
    {
        int tokenLen = tokenIds.Length;

        using var inputIds = new Tensor<int>(new TensorShape(1, tokenLen), tokenIds);
        using var tokenTypes = new Tensor<int>(new TensorShape(1, tokenLen), new int[tokenLen]); // 全0
        using var mask = new Tensor<int>(new TensorShape(1, tokenLen), attentionMask);

        _worker.SetInput("input_ids", inputIds);
        _worker.SetInput("token_type_ids", tokenTypes);
        _worker.SetInput("attention_mask", mask);
        _worker.Schedule();

        var output = _worker.PeekOutput() as Tensor<float>;
        output.ReadbackAndClone();
        return output.DownloadToArray();  // [1, 1024, token_len] flattened
    }

    public void Dispose() { _worker?.Dispose(); }
}
```

### SBV2ModelRunner

```csharp
public class SBV2ModelRunner : IDisposable
{
    private Worker _worker;

    public SBV2ModelRunner(ModelAsset modelAsset, BackendType backendType)
    {
        var model = ModelLoader.Load(modelAsset);
        _worker = new Worker(model, backendType);
    }

    /// <summary>
    /// メインTTS推論
    /// </summary>
    public float[] Run(
        int[] phonemeIds, int[] tones, int[] languageIds,
        int speakerId, float[] bertEmbedding, float[] styleVector,
        float sdpRatio, float noiseScale, float noiseScaleW, float lengthScale)
    {
        int seqLen = phonemeIds.Length;

        using var xTst = new Tensor<int>(new TensorShape(1, seqLen), phonemeIds);
        using var xTstLengths = new Tensor<int>(new TensorShape(1), new int[] { seqLen });
        using var tonesTensor = new Tensor<int>(new TensorShape(1, seqLen), tones);
        using var langTensor = new Tensor<int>(new TensorShape(1, seqLen), languageIds);
        using var sidTensor = new Tensor<int>(new TensorShape(1), new int[] { speakerId });
        using var bertTensor = new Tensor<float>(new TensorShape(1, 1024, seqLen), bertEmbedding);
        using var styleTensor = new Tensor<float>(new TensorShape(1, 256), styleVector);
        using var sdpTensor = new Tensor<float>(new TensorShape(1), new float[] { sdpRatio });
        using var noiseTensor = new Tensor<float>(new TensorShape(1), new float[] { noiseScale });
        using var noiseWTensor = new Tensor<float>(new TensorShape(1), new float[] { noiseScaleW });
        using var lengthTensor = new Tensor<float>(new TensorShape(1), new float[] { lengthScale });

        _worker.SetInput("x_tst", xTst);
        _worker.SetInput("x_tst_lengths", xTstLengths);
        _worker.SetInput("tones", tonesTensor);
        _worker.SetInput("language", langTensor);
        _worker.SetInput("bert", bertTensor);
        _worker.SetInput("style_vec", styleTensor);
        _worker.SetInput("sid", sidTensor);
        _worker.SetInput("sdp_ratio", sdpTensor);
        _worker.SetInput("noise_scale", noiseTensor);
        _worker.SetInput("noise_scale_w", noiseWTensor);
        _worker.SetInput("length_scale", lengthTensor);

        _worker.Schedule();

        var output = _worker.PeekOutput("output") as Tensor<float>;
        output.ReadbackAndClone();
        return output.DownloadToArray();  // [1, 1, audio_samples] flattened
    }

    public void Dispose() { _worker?.Dispose(); }
}
```

### StyleVectorProvider

```csharp
public class StyleVectorProvider
{
    private float[,] _vectors;  // [num_styles, 256]

    /// <summary>
    /// .npyファイルからスタイルベクトルを読み込む
    /// </summary>
    public void Load(string npyPath)
    {
        // NumPy .npy バイナリフォーマット:
        // - 6 bytes magic: \x93NUMPY
        // - 1 byte major version
        // - 1 byte minor version
        // - 2 bytes header length (little-endian)
        // - header (Python dict string): {'descr': '<f4', 'fortran_order': False, 'shape': (N, 256)}
        // - data: float32 * N * 256
        byte[] bytes = File.ReadAllBytes(npyPath);
        // ... パース処理 ...
    }

    /// <summary>
    /// スタイルベクトル取得 (sbv2-api方式: mean + (style - mean) * weight)
    /// </summary>
    public float[] GetVector(int styleId, float weight = 1.0f)
    {
        float[] mean = GetRow(0);    // index 0 = ニュートラル基準
        float[] style = GetRow(styleId);
        float[] result = new float[256];
        for (int i = 0; i < 256; i++)
            result[i] = mean[i] + (style[i] - mean[i]) * weight;
        return result;
    }
}
```

---

## 統合マネージャ

```csharp
public class SBV2TTSManager : MonoBehaviour, IDisposable
{
    [Header("Models")]
    [SerializeField] private ModelAsset _bertModelAsset;
    [SerializeField] private ModelAsset _ttsModelAsset;

    [Header("Settings")]
    [SerializeField] private BackendType _backendType = BackendType.GPUCompute;

    private SBV2TextProcessor _textProcessor;
    private SBV2Tokenizer _tokenizer;
    private BertRunner _bertRunner;
    private SBV2ModelRunner _ttsRunner;
    private StyleVectorProvider _styleProvider;

    private void Awake()
    {
        _textProcessor = new SBV2TextProcessor(/* dictPath, configPath */);
        _tokenizer = new SBV2Tokenizer(/* vocabPath */);
        _bertRunner = new BertRunner(_bertModelAsset, _backendType);
        _ttsRunner = new SBV2ModelRunner(_ttsModelAsset, _backendType);
        _styleProvider = new StyleVectorProvider();
        _styleProvider.Load(/* npyPath */);
    }

    /// <summary>
    /// テキスト→音声合成
    /// </summary>
    public AudioClip Synthesize(
        string text,
        int speakerId = 0,
        int styleId = 0,
        float sdpRatio = 0.2f,
        float noiseScale = 0.6f,
        float noiseScaleW = 0.8f,
        float lengthScale = 1.0f)
    {
        // 1. G2P: テキスト→音素ID+トーン+言語ID+word2ph
        var (phoneIds, tones, langIds, word2ph) = _textProcessor.Process(text);

        // 2. DeBERTaトークナイズ + BERT推論
        var (tokenIds, attentionMask) = _tokenizer.Encode(text);
        float[] bertOutput = _bertRunner.Run(tokenIds, attentionMask);
        // bertOutput: [1, 1024, token_len] flattened

        // 3. word2phでBERT埋め込みを音素列長に展開
        float[] alignedBert = AlignBertToPhonemes(bertOutput, word2ph, phoneIds.Length);
        // alignedBert: [1, 1024, seq_len] flattened

        // 4. Style Vector
        float[] styleVec = _styleProvider.GetVector(styleId);

        // 5. TTS推論
        float[] audioSamples = _ttsRunner.Run(
            phoneIds, tones, langIds, speakerId,
            alignedBert, styleVec,
            sdpRatio, noiseScale, noiseScaleW, lengthScale);

        // 6. AudioClip生成
        return TTSAudioUtility.CreateClip(audioSamples, 44100);
    }

    /// <summary>
    /// BERTの出力をword2phに基づいて音素列長に展開する
    /// </summary>
    private float[] AlignBertToPhonemes(float[] bertFlat, int[] word2ph, int phoneSeqLen)
    {
        int tokenLen = word2ph.Sum();  // or bertのtoken_len
        int embDim = 1024;
        float[] aligned = new float[1 * embDim * phoneSeqLen];

        int phoneIdx = 0;
        int tokenIdx = 0;
        for (int w = 0; w < word2ph.Length; w++)
        {
            for (int p = 0; p < word2ph[w]; p++)
            {
                // bertFlat[0, :, tokenIdx] → aligned[0, :, phoneIdx]
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

    private void OnDestroy() => Dispose();

    public void Dispose()
    {
        _bertRunner?.Dispose();
        _ttsRunner?.Dispose();
    }
}
```

---

## opset制約の実態

| 項目 | 公式 | uCosyVoice実証 |
|---|---|---|
| サポートopset | 7-15 | 14-18全て動作 |
| テスト結果 | — | 118テスト全パス |
| 推奨 | opset 15ターゲット | 必要なら16+も使用可能 |

---

## パフォーマンス考慮事項

### 初回推論のレイテンシ
Sentisはシェーダコンパイルで初回が遅い。ウォームアップ推論を実装:

```csharp
private void WarmUp()
{
    // ダミーデータで1回推論を走らせてシェーダをコンパイル
    var dummyPhonemes = new int[] { 0, 1, 2 };
    // ... 最小限のダミー入力で Schedule() を実行
}
```

### UIスレッドブロック対策
同期推論(`Schedule()`)はメインスレッドをブロックする。対策:
- `ScheduleIterable()` でフレーム分散（Sentis API）
- async/await パターン（uCosyVoiceではCoroutineベース）

### メモリ
- DeBERTa FP16: ~600MB GPU/CPU メモリ
- SBV2 FP16: ~200-400MB
- 合計: ~800MB-1GB → GPU メモリ不足時は CPU フォールバック

---

## テストシーン構成

```
SampleScene
├── Canvas
│   ├── InputField (日本語テキスト入力)
│   ├── Dropdown - Speaker (話者選択)
│   ├── Dropdown - Style (スタイル選択)
│   ├── Slider - Speed (lengthScale: 0.5-2.0)
│   ├── Slider - SDP Ratio (0.0-1.0)
│   ├── Button - Synthesize (合成＆再生)
│   └── Text - Status (処理状態表示)
├── AudioSource (再生用)
└── SBV2TTSManager (MonoBehaviour)
    ├── ModelAsset (BERT) [Inspector]
    ├── ModelAsset (TTS) [Inspector]
    └── BackendType [Inspector]
```

---

## 検証チェックリスト

1. [ ] Sentisパッケージインストール確認 (`com.unity.ai.inference` 2.5.0)
2. [ ] ONNXインポート: エラーなしでModelAssetに変換されるか
3. [ ] BertRunner単体: テキスト→BERT埋め込みのshapeが [1, 1024, token_len] か
4. [ ] SBV2ModelRunner単体: ダミー入力→音声波形が出力されるか
5. [ ] G2P単体: 「こんにちは」→正しい音素ID配列か
6. [ ] word2phアライメント: BERT出力と音素列の長さが一致するか
7. [ ] エンドツーエンド: テキスト入力→音声再生
8. [ ] GPU/CPUフォールバック: GPUCompute失敗時にCPUで動作するか
