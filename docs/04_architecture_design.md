# アーキテクチャ設計ガイド

## 概要

Style-Bert-VITS2 Unity実装のプロジェクト構成・Assembly Definition設計・モデル配置戦略をまとめる。
uPiper・uCosyVoiceの実プロジェクト構造を参考に、Sentis 2.5.0向けの推奨パターンを定義する。

---

## 推奨ディレクトリレイアウト

> **Note**: 本ドキュメントの推奨構成は、01/02/03で記載したシンプルな構成（`Assets/Scripts/G2P/...`, `Assets/Models/` 等）を、パッケージ化・再配布に対応できるよう発展させたものである。初期プロトタイプでは01/02の構成で開発し、安定後に以下の構成へ移行することを推奨する。

```
Assets/
  uStyleBertVITS2/
    Runtime/
      Core/
        Inference/
          BertRunner.cs              # DeBERTa推論ラッパー
          SBV2ModelRunner.cs         # メインTTSモデル推論
          ModelAssetManager.cs       # モデルロード・ライフサイクル管理
        TextProcessing/
          IG2P.cs                    # G2Pインターフェース
          JapaneseG2P.cs             # OpenJTalkベースG2P実装
          SBV2PhonemeMapper.cs       # OpenJTalk音素→SBV2トークンID
          SBV2Tokenizer.cs           # DeBERTa用文字レベルトークナイザ
          TextNormalizer.cs          # 全角→半角等のテキスト正規化
        Audio/
          AudioClipGenerator.cs      # Tensor→AudioClip変換
          TTSAudioUtility.cs         # 音声正規化・ユーティリティ
        Configuration/
          TTSSettings.cs             # ScriptableObjectベース設定
          ModelConfiguration.cs      # モデルパス・バックエンド設定
        Services/
          TTSPipeline.cs             # 推論パイプラインオーケストレータ
          ITTSPipeline.cs            # パイプラインインターフェース
        Native/
          OpenJTalkNative.cs         # OpenJTalk P/Invoke (uPiper流用)
          OpenJTalkConstants.cs      # 辞書パス定数 (uPiper流用)
        Data/
          StyleVectorProvider.cs     # style_vectors.npy 読み込み
          NpyReader.cs               # NumPy .npy パーサー
      uStyleBertVITS2.Runtime.asmdef
    Editor/
      TTSSettingsEditor.cs           # カスタムInspector
      ModelImportValidator.cs        # ONNXインポート検証
      uStyleBertVITS2.Editor.asmdef
    Tests/
      Runtime/
        G2PTests.cs                  # G2P単体テスト
        TokenizerTests.cs            # トークナイザテスト
        InferenceTests.cs            # 推論テスト（要モデル）
        PipelineTests.cs             # E2Eテスト
        uStyleBertVITS2.Tests.Runtime.asmdef
      Editor/
        ModelImportTests.cs          # ONNXインポートテスト
        ConfigurationTests.cs        # 設定バリデーション
        uStyleBertVITS2.Tests.Editor.asmdef
    Plugins/
      Windows/x86_64/
        openjtalk_wrapper.dll        # OpenJTalkネイティブライブラリ
      macOS/
        libopenjtalk_wrapper.dylib
      Linux/x86_64/
        libopenjtalk_wrapper.so
      Android/
        libs/
          arm64-v8a/
            libopenjtalk_wrapper.so
    Samples~/
      BasicTTS/
        SampleScene.unity
        SBV2TTSDemo.cs
    StreamingAssets/
      uStyleBertVITS2/
        OpenJTalkDic/                # NAIST JDIC辞書 (8ファイル)
        Tokenizer/
          vocab.json                 # DeBERTa語彙
        Models/
          sbv2_model_fp16.onnx       # メインTTSモデル (~200-400MB)
          deberta_fp16.onnx          # DeBERTaモデル (~600MB)
        StyleVectors/
          style_vectors.npy          # スタイルベクトル
```

### ディレクトリ設計の原則

| 原則 | 説明 |
|---|---|
| **Runtime/Editor/Tests分離** | Assembly Definitionで明確に分離。Editor APIがRuntimeに混入しない |
| **Core以下の機能分割** | Inference, TextProcessing, Audio, Configuration, Services |
| **Native分離** | P/Invoke呼び出しを`Native/`に集約。プラットフォーム差異を局所化 |
| **Samples~隠蔽** | `~`サフィックスでUnity Editorのインポート対象外。Package Manager経由で展開 |

---

## Assembly Definition (.asmdef) 設計

### 4つのAssembly Definition

#### 1. Runtime (`uStyleBertVITS2.Runtime.asmdef`)

```json
{
    "name": "uStyleBertVITS2.Runtime",
    "rootNamespace": "uStyleBertVITS2",
    "references": [
        "Unity.InferenceEngine"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": true,
    "autoReferenced": true,
    "defineConstraints": [],
    "noEngineReferences": false
}
```

- `allowUnsafeCode: true` — stackalloc、Span操作、ネイティブメモリアクセスに必要
- `Unity.InferenceEngine` — Sentis 2.5.0の実際のアセンブリ名
- Newtonsoft.Json が必要な場合は `"com.unity.nuget.newtonsoft-json"` を追加

#### 2. Editor (`uStyleBertVITS2.Editor.asmdef`)

```json
{
    "name": "uStyleBertVITS2.Editor",
    "rootNamespace": "uStyleBertVITS2.Editor",
    "references": [
        "uStyleBertVITS2.Runtime",
        "Unity.InferenceEngine"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

#### 3. Tests.Runtime (`uStyleBertVITS2.Tests.Runtime.asmdef`)

```json
{
    "name": "uStyleBertVITS2.Tests.Runtime",
    "rootNamespace": "uStyleBertVITS2.Tests",
    "references": [
        "uStyleBertVITS2.Runtime",
        "Unity.InferenceEngine",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"]
}
```

#### 4. Tests.Editor (`uStyleBertVITS2.Tests.Editor.asmdef`)

```json
{
    "name": "uStyleBertVITS2.Tests.Editor",
    "rootNamespace": "uStyleBertVITS2.Tests.Editor",
    "references": [
        "uStyleBertVITS2.Runtime",
        "uStyleBertVITS2.Editor",
        "Unity.InferenceEngine",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"]
}
```

### 依存関係図

```
uStyleBertVITS2.Tests.Editor
  └──→ uStyleBertVITS2.Editor
         └──→ uStyleBertVITS2.Runtime
                └──→ Unity.InferenceEngine

uStyleBertVITS2.Tests.Runtime
  └──→ uStyleBertVITS2.Runtime
         └──→ Unity.InferenceEngine
```

---

## コアアーキテクチャ

### クラス構成と責務

```
TTSPipeline (Services/)
├── IG2P → JapaneseG2P (TextProcessing/)
│     ├── OpenJTalkNative (Native/)
│     ├── SBV2PhonemeMapper
│     └── TextNormalizer
├── SBV2Tokenizer (TextProcessing/)
├── BertRunner (Inference/)
├── SBV2ModelRunner (Inference/)
├── StyleVectorProvider (Data/)
└── TTSSettings (Configuration/)
```

### インターフェース設計

```csharp
namespace uStyleBertVITS2
{
    /// <summary>
    /// G2Pバックエンドの抽象化。OpenJTalk以外のバックエンドへの切替を可能にする。
    /// </summary>
    public interface IG2P : IDisposable
    {
        G2PResult Process(string text);
    }

    public readonly struct G2PResult
    {
        public readonly int[] PhonemeIds;
        public readonly int[] Tones;
        public readonly int[] LanguageIds;
        public readonly int[] Word2Ph;

        public G2PResult(int[] phonemeIds, int[] tones, int[] languageIds, int[] word2ph)
        {
            PhonemeIds = phonemeIds;
            Tones = tones;
            LanguageIds = languageIds;
            Word2Ph = word2ph;
        }
    }

    /// <summary>
    /// TTSパイプラインの抽象化。同期/非同期の切替を可能にする。
    /// </summary>
    public interface ITTSPipeline : IDisposable
    {
        AudioClip Synthesize(TTSRequest request);
        Task<AudioClip> SynthesizeAsync(TTSRequest request);
    }

    public readonly struct TTSRequest
    {
        public readonly string Text;
        public readonly int SpeakerId;
        public readonly int StyleId;
        public readonly float SdpRatio;
        public readonly float NoiseScale;
        public readonly float NoiseScaleW;
        public readonly float LengthScale;

        public TTSRequest(
            string text,
            int speakerId = 0,
            int styleId = 0,
            float sdpRatio = 0.2f,
            float noiseScale = 0.6f,
            float noiseScaleW = 0.8f,
            float lengthScale = 1.0f)
        {
            Text = text;
            SpeakerId = speakerId;
            StyleId = styleId;
            SdpRatio = sdpRatio;
            NoiseScale = noiseScale;
            NoiseScaleW = noiseScaleW;
            LengthScale = lengthScale;
        }
    }
}
```

### ModelAssetManager — モデルライフサイクル管理

```csharp
namespace uStyleBertVITS2
{
    /// <summary>
    /// モデルのロード・キャッシュ・Dispose管理を一元化
    /// </summary>
    public class ModelAssetManager : IDisposable
    {
        private readonly Dictionary<string, Worker> _workers = new();
        private bool _disposed;

        public Worker GetOrCreateWorker(ModelAsset asset, BackendType backendType, string key)
        {
            if (_workers.TryGetValue(key, out var existing))
                return existing;

            var model = ModelLoader.Load(asset);
            var worker = new Worker(model, backendType);
            _workers[key] = worker;
            return worker;
        }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (var worker in _workers.Values)
                worker?.Dispose();
            _workers.Clear();
            _disposed = true;
        }
    }
}
```

---

## Configuration — ScriptableObjectベースの設定管理

```csharp
using UnityEngine;
using Unity.InferenceEngine;

namespace uStyleBertVITS2
{
    [CreateAssetMenu(fileName = "TTSSettings", menuName = "uStyleBertVITS2/TTS Settings")]
    public class TTSSettings : ScriptableObject
    {
        [Header("Models")]
        public ModelAsset BertModel;
        public ModelAsset TTSModel;

        [Header("Backend")]
        public BackendType PreferredBackend = BackendType.GPUCompute;
        public BackendType FallbackBackend = BackendType.CPU;

        [Header("Default Parameters")]
        [Range(0f, 1f)] public float DefaultSdpRatio = 0.2f;
        [Range(0f, 1f)] public float DefaultNoiseScale = 0.6f;
        [Range(0f, 1f)] public float DefaultNoiseScaleW = 0.8f;
        [Range(0.5f, 2f)] public float DefaultLengthScale = 1.0f;

        [Header("Paths (relative to StreamingAssets)")]
        public string DictionaryPath = "uStyleBertVITS2/OpenJTalkDic";
        public string VocabPath = "uStyleBertVITS2/Tokenizer/vocab.json";
        public string StyleVectorPath = "uStyleBertVITS2/StyleVectors/style_vectors.npy";

        [Header("Performance")]
        public bool EnableWarmup = true;
        public bool EnableBertCache = true;
        [Range(16, 256)] public int BertCacheCapacity = 64;
    }
}
```

---

## モデル配置戦略

### 2つの配置パターン

| 用途 | 配置先 | ロード方法 | 説明 |
|---|---|---|---|
| **Editor開発・プロトタイプ** | `Assets/Models/*.onnx` | `ModelAsset` としてInspectorから参照 | 01/03で記載の方式。最もシンプル |
| **ビルド後配布・動的差し替え** | `StreamingAssets/uStyleBertVITS2/Models/` | ファイルパスから動的ロード | 大容量モデルの配布・更新に対応 |

Editor開発時は `Assets/Models/` に配置して `ModelAsset` 参照を使うのが推奨。StreamingAssetsへの配置はビルド後のモデル差し替えや、DLC配信が必要な場合のオプション。

### 方式比較表

| 方式 | 最大サイズ | ロード方法 | ビルドサイズ影響 | 推奨用途 |
|---|---|---|---|---|
| **StreamingAssets** | 制限なし | ファイルパス直接読み込み | ビルドに含まれる | 大容量モデル（推奨） |
| **Resources** | ~4GB | `Resources.Load<ModelAsset>()` | ビルドに含まれる | 小規模アセット |
| **Addressables** | 制限なし | 非同期ロード、リモート配信可 | 柔軟 | DLC・更新配信 |

### StreamingAssets推奨理由

1. **大容量対応**: DeBERTa (~600MB) + SBV2 (~200-400MB) = 約1GBのモデルを格納可能
2. **プラットフォーム互換性**: 全プラットフォームで `Application.streamingAssetsPath` が使用可能
3. **Unity Editorでの透過性**: ONNXファイルをStreamingAssetsに置くとSentisが自動的に`.sentis`に変換
4. **動的差し替え**: ビルド後もファイルを差し替えることでモデル更新が可能（デスクトップ環境）

### StreamingAssetsからのモデルロード

```csharp
// Editor: Resources.Load<ModelAsset>() が最もシンプル
// Runtime (StreamingAssets): ファイルパスからバイト読み込み→ModelLoader
public static Model LoadFromStreamingAssets(string relativePath)
{
    string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

    // Android等では直接ファイルアクセスできないため、
    // UnityWebRequest経由で読み込む必要がある場合あり
#if UNITY_ANDROID && !UNITY_EDITOR
    // UnityWebRequest経由
    // ...
#else
    byte[] modelBytes = File.ReadAllBytes(fullPath);
    return ModelLoader.Load(modelBytes);
#endif
}
```

**注意**: Editor上ではONNXファイルをAssets内に配置して`ModelAsset`としてInspectorから参照するのが最もシンプル。StreamingAssetsからの動的ロードはビルド後の差し替えが必要な場合に限定する。

---

## テスト構造

### テストカテゴリ

| カテゴリ | Assembly | 内容 | モデル依存 |
|---|---|---|---|
| **G2P単体テスト** | Tests.Runtime | 音素変換・トーン生成の正確性 | No (辞書のみ) |
| **トークナイザテスト** | Tests.Runtime | DeBERTaトークナイズの正確性 | No (vocab.jsonのみ) |
| **推論テスト** | Tests.Runtime | BERT/TTSモデル推論のshape検証 | Yes |
| **E2Eテスト** | Tests.Runtime | テキスト→音声の全パイプライン | Yes |
| **インポートテスト** | Tests.Editor | ONNX→ModelAsset変換の検証 | Yes |
| **設定テスト** | Tests.Editor | ScriptableObjectのバリデーション | No |

### テスト例

```csharp
using NUnit.Framework;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class G2PTests
    {
        private IG2P _g2p;

        [OneTimeSetUp]
        public void Setup()
        {
            string dictPath = Path.Combine(Application.streamingAssetsPath, "uStyleBertVITS2/OpenJTalkDic");
            _g2p = new JapaneseG2P(dictPath);
        }

        [Test]
        public void Process_Konnichiwa_ReturnsCorrectPhonemes()
        {
            var result = _g2p.Process("こんにちは");

            Assert.IsNotNull(result.PhonemeIds);
            Assert.IsTrue(result.PhonemeIds.Length > 0);
            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length);
            Assert.AreEqual(result.PhonemeIds.Length, result.LanguageIds.Length);
        }

        [Test]
        public void Process_AllLanguageIdsAreJapanese()
        {
            var result = _g2p.Process("テスト");

            foreach (int langId in result.LanguageIds)
                Assert.AreEqual(1, langId, "JP-Extraでは全言語IDが1(日本語)");
        }

        [Test]
        public void Process_Word2PhSumMatchesPhonemeLength()
        {
            var result = _g2p.Process("東京タワー");

            int sum = 0;
            foreach (int w in result.Word2Ph) sum += w;
            Assert.AreEqual(result.PhonemeIds.Length, sum);
        }

        [OneTimeTearDown]
        public void Teardown() => _g2p?.Dispose();
    }
}
```

---

## Dependency Injectionパターン

### バックエンド切替の実現

```csharp
// 1. OpenJTalkベースの本番実装
IG2P g2p = new JapaneseG2P(dictPath);

// 2. テスト用のモック実装
IG2P mockG2p = new MockG2P(predefinedResults);

// 3. リモートAPIベースの実装（Python G2Pサーバー）
IG2P remoteG2p = new RemoteG2P("http://localhost:8080/g2p");

// パイプラインに注入
ITTSPipeline pipeline = new TTSPipeline(g2p, tokenizer, bertRunner, ttsRunner, styleProvider);
```

### パイプライン構築パターン

```csharp
public class TTSPipelineBuilder
{
    private IG2P _g2p;
    private SBV2Tokenizer _tokenizer;
    private BertRunner _bertRunner;
    private SBV2ModelRunner _ttsRunner;
    private StyleVectorProvider _styleProvider;
    private TTSSettings _settings;

    public TTSPipelineBuilder WithSettings(TTSSettings settings)
    {
        _settings = settings;
        return this;
    }

    public TTSPipelineBuilder WithG2P(IG2P g2p)
    {
        _g2p = g2p;
        return this;
    }

    public ITTSPipeline Build()
    {
        _g2p ??= new JapaneseG2P(
            Path.Combine(Application.streamingAssetsPath, _settings.DictionaryPath));
        _tokenizer ??= new SBV2Tokenizer(
            Path.Combine(Application.streamingAssetsPath, _settings.VocabPath));
        _bertRunner ??= new BertRunner(_settings.BertModel, _settings.PreferredBackend);
        _ttsRunner ??= new SBV2ModelRunner(_settings.TTSModel, _settings.PreferredBackend);
        _styleProvider ??= new StyleVectorProvider();

        _styleProvider.Load(
            Path.Combine(Application.streamingAssetsPath, _settings.StyleVectorPath));

        return new TTSPipeline(_g2p, _tokenizer, _bertRunner, _ttsRunner, _styleProvider);
    }
}
```

---

## uPiperからの流用ガイドライン

### 流用対象と変更点

| コンポーネント | 変更内容 |
|---|---|
| `OpenJTalkNative.cs` | namespace変更 (`uPiper.Core.Phonemizers.Native` → `uStyleBertVITS2.Native`) |
| `OpenJTalkConstants.cs` | namespace変更、辞書パスを`TTSSettings`から取得するよう変更 |
| `TextNormalizer.cs` | namespace変更、`PiperLogger`依存を`Debug.Log`に置換 |
| `CustomDictionary.cs` | namespace変更 |

### 流用しないもの

- `BasePhonemizer` — SBV2用に`IG2P`インターフェースを新規定義
- `PiperModel`/`PiperTTS` — SBV2固有のパイプラインを構築
- ONNX変換スクリプト — SBV2専用の変換スクリプトを使用（`docs/01_onnx_export.md`参照）

---

## 注意事項

- **モデルサイズ**: DeBERTa + SBV2 = ~1GBのため、git管理にはGit LFSを使用すること
- **プラットフォーム制約**: OpenJTalkネイティブライブラリは各プラットフォーム用のビルドが必要。Plugins/以下にプラットフォーム別に配置
- **Unity 6互換性**: `using Unity.InferenceEngine;` (旧`Unity.Sentis`)。APIリネームに注意
- **unsafe code**: `stackalloc`やネイティブメモリ操作のためRuntime asmdefで`allowUnsafeCode: true`が必要
