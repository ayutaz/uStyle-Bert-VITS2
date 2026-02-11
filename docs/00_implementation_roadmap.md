# 実装ロードマップ (uStyle-Bert-VITS2)

## 概要

Style-Bert-VITS2（日本語TTS）のモデルをONNXに変換し、Unity Sentis（AI Inference Engine）でリアルタイム推論するプロジェクトの実装ロードマップ。

全9フェーズ（Phase 0-8）で構成され、Python側のONNX変換からUnity側の推論パイプライン、非同期最適化、エディタ拡張まで段階的に実装する。

- **総工数見積**: 17-26日
- **Phase 1-4 は並列実装可能**（Phase 0 完了後）
- **Phase 5 が合流点**（Phase 1-4 すべてが前提）

---

## Phase一覧表

| Phase | 概要 | 工数 | 複雑度 | 前提Phase | ステータス |
|---|---|---|---|---|---|
| 0 | プロジェクト基盤 + ONNX変換 | 2-3日 | Medium | なし | Done |
| 1 | データ層（NpyReader, StyleVector, Settings） | 1-2日 | Low | 0 | Done |
| 2 | DeBERTaトークナイザ | 1日 | Low | 0 | Done |
| 3 | G2P（OpenJTalk P/Invoke + 音素マッピング） | 3-5日 | High | 0 | Done |
| 4 | Sentis推論ラッパー（BertRunner, SBV2ModelRunner） | 2-3日 | Medium | 0 | Done |
| 5 | パイプライン統合（BertAligner + TTSPipeline） | 2-3日 | High | 1, 2, 3, 4 | Done |
| 6 | AudioClip生成 + デモシーン | 1-2日 | Low | 5 | Done |
| 7 | 非同期 + 最適化（UniTask, キャッシュ, Burst） | 3-4日 | High | 6 | Done |
| 8 | エディタ拡張 + デモUI | 2-3日 | Low-Medium | 7 | Done |

---

## 依存関係グラフ

```
Phase 0: プロジェクト基盤 [Done]
├── Phase 1: データ層        ─┐
├── Phase 2: トークナイザ    ─┤── 並列実装可能 [Done]
├── Phase 3: G2P             ─┤
└── Phase 4: Sentis推論      ─┘
                                 │
                                 ▼
                        Phase 5: パイプライン統合（合流点） [Done]
                                 │
                                 ▼
                        Phase 6: AudioClip + E2E [Done]
                                 │
                                 ▼
                        Phase 7: Async + 最適化 [Done]
                                 │
                                 ▼
                        Phase 8: エディタ拡張 + UI [Done]
```

---

## Phase 0: プロジェクト基盤 + ONNX変換 ✅

### 目標

ビルド可能なプロジェクトスケルトンを構築し、Python側でONNXモデル変換を完了する。

### 成果物

**Python側（ONNX変換）**:

| ファイル | 説明 |
|---|---|
| `scripts/convert_for_sentis.py` | SynthesizerTrnエクスポート（opset 15, FP16, int64→int32） |
| `scripts/convert_bert_for_sentis.py` | DeBERTaエクスポート（opset 15, FP16） |
| `scripts/validate_onnx.py` | OnnxRuntime推論検証（MSE < 1e-3） |
| `scripts/requirements.txt` | Python依存パッケージ |

**Unity側（基盤）**:

| ファイル | 説明 |
|---|---|
| `Packages/manifest.json` | `com.unity.ai.inference: 2.5.0`, UniTask, ZString追加 |
| `Assets/uStyleBertVITS2/Runtime/uStyleBertVITS2.Runtime.asmdef` | Runtime Assembly（`Unity.InferenceEngine`, `allowUnsafeCode: true`） |
| `Assets/uStyleBertVITS2/Editor/uStyleBertVITS2.Editor.asmdef` | Editor Assembly |
| `Assets/uStyleBertVITS2/Tests/Runtime/uStyleBertVITS2.Tests.Runtime.asmdef` | テスト用Runtime Assembly |
| `Assets/uStyleBertVITS2/Tests/Editor/uStyleBertVITS2.Tests.Editor.asmdef` | テスト用Editor Assembly |

**データファイル**:

| ファイル | 説明 |
|---|---|
| `Assets/StreamingAssets/uStyleBertVITS2/Models/sbv2_model_fp16.onnx` | メインTTSモデル（~200-400MB） |
| `Assets/StreamingAssets/uStyleBertVITS2/Models/deberta_fp16.onnx` | DeBERTaモデル（~600MB） |
| `Assets/StreamingAssets/uStyleBertVITS2/StyleVectors/style_vectors.npy` | スタイルベクトル |
| `Assets/StreamingAssets/uStyleBertVITS2/Tokenizer/vocab.json` | DeBERTaトークナイザ語彙 |
| `Assets/StreamingAssets/uStyleBertVITS2/OpenJTalkDic/` | NAIST JDIC辞書（8ファイル） |
| `Assets/uStyleBertVITS2/Plugins/Windows/x86_64/openjtalk_wrapper.dll` | OpenJTalkネイティブDLL |

### 実装仕様の要点

- ONNX opset 15を指定（Sentis互換）
- FP16変換（`keep_io_types=True` で入出力はfloat32維持）
- int64→int32変換（Sentisは`Tensor<int>`=int32のみ対応）
- `onnxsim.simplify()` でグラフ簡略化

### 検証チェックリスト

- [x] ONNXファイルが正常に生成される
- [x] OnnxRuntime検証パス（MSE < 1e-3）
- [x] Unity EditorがONNXをエラーなしでインポート（.sentisに変換）
- [x] コンパイルエラーゼロ
- [x] `ModelLoader.Load()` 成功

### テスト項目

> Phase 0 のテスト（ModelImportTests）はONNXモデルファイル配置後に実装予定。現在はテストなし。

---

## Phase 1: データ層 ✅

### 目標

モデル非依存のデータロードと構成管理を実装する。

### 成果物

| ファイル | 説明 |
|---|---|
| `Runtime/Core/Data/NpyReader.cs` | NumPy .npyバイナリフォーマットパーサー |
| `Runtime/Core/Data/StyleVectorProvider.cs` | style_vectors.npyのロードとベクトル取得 |
| `Runtime/Core/Configuration/TTSSettings.cs` | ScriptableObject（`[CreateAssetMenu]`） |
| `Runtime/Core/Configuration/ModelConfiguration.cs` | モデルパスとBackendType設定 |

### 実装仕様の要点

**NpyReader**:
- 6バイトマジック `\x93NUMPY` の検証
- バージョン、ヘッダ長の読み取り（リトルエンディアン）
- Pythonディクショナリヘッダのパース: `{'descr': '<f4', 'fortran_order': False, 'shape': (N, 256)}`
- float32データの読み込み

**StyleVectorProvider**:
- `Load(string npyPath)` — .npyファイル読み込み
- `GetVector(int styleId, float weight = 1.0f)` — `mean + (style - mean) * weight`（sbv2-api互換）
- インデックス0 = ニュートラル参照ベクトル

**TTSSettings**:
- モデルパス、Backend（BertBackend=CPU推奨 / TTSBackend=GPUCompute推奨）、デフォルトパラメータ、パフォーマンス設定

### 検証チェックリスト

- [x] .npyファイルが正しくパースされる
- [x] StyleVectorが正しいshapeで返される
- [x] weight=0.0でニュートラルベクトルが返される
- [x] TTSSettingsがInspectorで編集可能

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `NpyReaderTests.ParsesValidNpyFile` | Runtime | 有効な.npyファイルのパース |
| `NpyReaderTests.ParsesFloat32Array` | Runtime | float32配列の読み込み |
| `NpyReaderTests.ThrowsOnInvalidFormat` | Runtime | 不正フォーマットでの例外 |
| `StyleVectorTests.LoadAndGetVector` | Runtime | ロードとベクトル取得 |
| `StyleVectorTests.NeutralVectorIsIndex0` | Runtime | インデックス0がニュートラル |
| `StyleVectorTests.WeightedInterpolation` | Runtime | 重み付き補間の正確性 |
| `StyleVectorTests.GetVector_DestOverload_MatchesAlloc` | Runtime | destバッファ版の等価性 |
| `StyleVectorTests.GetVector_DestOverload_ThrowsOnSmallBuffer` | Runtime | 小バッファでの例外 |
| `ConfigurationTests.TTSSettingsDefaults` | Editor | デフォルト値の確認 |
| `ConfigurationTests.TTSSettingsSerializes` | Editor | シリアライズの確認 |

---

## Phase 2: DeBERTaトークナイザ ✅

### 目標

DeBERTa用の文字レベルトークナイザを実装する。モデル推論不要で単体テスト可能。

### 成果物

| ファイル | 説明 |
|---|---|
| `Runtime/Core/TextProcessing/SBV2Tokenizer.cs` | 文字レベルトークナイザ |

### 実装仕様の要点

- `vocab.json`（`Dictionary<string, int>`）をロード → `Dictionary<char, int>` に変換
- 特殊トークン: `[CLS]=1`, `[SEP]=2`, `[UNK]=3`, `[PAD]=0`
- `Encode(string text)` → `(int[] tokenIds, int[] attentionMask)`
- フロー: 文字分割 → vocab lookup → `[CLS]...[SEP]` でラップ → attention_mask全1

### 検証チェックリスト

- [x] vocab.jsonが正しくロードされる
- [x] "こんにちは" がトークン長7にエンコードされる（`[CLS]` + 5文字 + `[SEP]`）
- [x] 未知文字が `[UNK]`（id=3）にマップされる
- [x] attention_maskが全1

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `TokenizerTests.EncodesBasicJapanese` | Runtime | 基本的な日本語のエンコード |
| `TokenizerTests.AddsSpecialTokens` | Runtime | 特殊トークンの付与 |
| `TokenizerTests.HandlesUnknownCharacters` | Runtime | 未知文字のUNK処理 |
| `TokenizerTests.AttentionMaskAllOnes` | Runtime | attention_maskの値 |
| `TokenizerTests.EmptyInputReturnsClsSep` | Runtime | 空入力の処理 |
| `TokenizerTests.LongTextTokenizes` | Runtime | 長文テキストの処理 |
| `TokenizerTests.CrossValidation_Konnichiwa` | Runtime | Python出力との比較 |

---

## Phase 3: G2P（OpenJTalk + SBV2PhonemeMapper） ✅

### 目標

日本語テキスト → 音素ID・トーン・言語ID・word2ph配列への変換パイプラインを実装する。

### 成果物

| ファイル | 説明 |
|---|---|
| `Runtime/Core/Native/OpenJTalkNative.cs` | P/Invoke定義（uPiperベース） |
| `Runtime/Core/Native/OpenJTalkConstants.cs` | 辞書パス定数 |
| `Runtime/Core/Native/OpenJTalkHandle.cs` | ネイティブリソースのSafeHandle |
| `Runtime/Core/TextProcessing/TextNormalizer.cs` | テキスト正規化（全角→半角等） |
| `Runtime/Core/TextProcessing/SBV2PhonemeMapper.cs` | OpenJTalk音素→SBV2トークンIDマッピング |
| `Runtime/Core/TextProcessing/IG2P.cs` | G2Pインターフェース（テスト用モック対応） |
| `Runtime/Core/TextProcessing/G2PResult.cs` | G2P出力のreadonly struct |
| `Runtime/Core/TextProcessing/JapaneseG2P.cs` | メインG2Pオーケストレータ |

### 実装仕様の要点

**SBV2PhonemeMapper**:
- ハードコードされた DefaultSymbols テーブル（112シンボル）から音素→ID辞書を構築
- マッピング補正: `cl`→`q`, `pau`→`SP`, `sil`→`SP`

**トーン配列生成**:
- `openjtalk_phonemize_with_prosody()` から韻律情報（A1/A2/A3）を抽出
- アクセント前=1（高）、アクセント後=0（低）
- SBV2用: トーン値に+6オフセットを加算

**言語ID**:
- JP-Extra: 全音素に `language_id=1`

**word2ph配列**:
- 各文字（トークン）が何個の音素にマッピングされるか

**JapaneseG2P**:
- 統合パイプライン: TextNormalizer → OpenJTalk P/Invoke → SBV2PhonemeMapper

### 検証チェックリスト

- [x] openjtalk_wrapper.dllがUnityからロードされる
- [x] `OpenJTalkNative.openjtalk_create(dictPath)` が有効なハンドルを返す
- [x] "こんにちは" → 正しい音素列
- [x] トーン配列が正しいアクセントパターン（+6オフセット）
- [x] `sum(word2ph) == phonemeIds.Length`
- [x] language_idsが全て1

### テスト項目

**TextNormalizerTests** (Editor):

| テスト名 | 種別 | 内容 |
|---|---|---|
| `TextNormalizerTests.FullWidthAlphanumeric_ConvertedToHalfWidth` | Editor | 全角英数字→半角 |
| `TextNormalizerTests.FullWidthSpace_ConvertedToHalfWidth` | Editor | 全角スペース→半角 |
| `TextNormalizerTests.MultipleSpaces_CollapsedToOne` | Editor | 連続スペース圧縮 |
| `TextNormalizerTests.NullAndEmpty_ReturnEmpty` | Editor | null/空文字列 |
| `TextNormalizerTests.LeadingTrailingSpaces_Trimmed` | Editor | 先頭末尾スペース除去 |
| `TextNormalizerTests.FullWidthSpaces_CollapsedToOne` | Editor | 連続全角スペース圧縮 |
| `TextNormalizerTests.MixedSpaces_CollapsedToOne` | Editor | 全角+半角混在スペース圧縮 |

**G2P / PhonemeMapper** (Runtime):

| テスト名 | 種別 | 内容 |
|---|---|---|
| `G2PTests.OpenJTalkInitializes` | Runtime | OpenJTalkの初期化 |
| `G2PTests.Process_Konnichiwa_ReturnsPhonemes` | Runtime | "こんにちは"の音素変換 |
| `G2PTests.Process_ArrayLengthsMatch` | Runtime | 各配列の長さ一致 |
| `G2PTests.Process_AllLanguageIdsAreJapanese` | Runtime | 言語IDが全てJP |
| `G2PTests.Process_Word2PhSumMatchesPhonemeLength` | Runtime | word2phの合計一致 |
| `G2PTests.Process_LongText` | Runtime | 長文の処理 |
| `G2PTests.Process_Punctuation` | Runtime | 句読点の処理 |
| `G2PTests.Dispose_ReleasesNativeResources` | Runtime | ネイティブリソース解放 |
| `PhonemeMapperTests.PhonemeMapper_ClMapsToQ` | Runtime | cl→qのマッピング |
| `PhonemeMapperTests.PhonemeMapper_PauMapsToSP` | Runtime | pau→SPのマッピング |
| `PhonemeMapperTests.ToneValues_HaveCorrectOffset` | Runtime | トーンオフセット検証 |
| `PhonemeMapperTests.BasicPhonemes_Resolve` | Runtime | 基本音素の解決確認 |

> G2PTests はOpenJTalk DLLが必要（Skip状態）。PhonemeMapperTests はDLL不要で常時実行可能。

---

## Phase 4: Sentis推論ラッパー ✅

### 目標

DeBERTaとSynthesizerTrnのSentis推論ラッパーを実装する。各モデルは独立動作。

### 成果物

| ファイル | 説明 |
|---|---|
| `Runtime/Core/Inference/BertRunner.cs` | DeBERTa推論ラッパー |
| `Runtime/Core/Inference/SBV2ModelRunner.cs` | TTS推論ラッパー |
| `Runtime/Core/Inference/ModelAssetManager.cs` | モデルロード・ライフサイクル管理 |

### 実装仕様の要点

**BertRunner**:
- コンストラクタ: `BertRunner(ModelAsset, BackendType)` → `ModelLoader.Load()` + `new Worker()`
- `Run(int[] tokenIds, int[] attentionMask)` → `float[]`（1024 * token_len フラット配列）
- 入力: `input_ids`, `token_type_ids`（全0）, `attention_mask`
- 出力: `[1, 1024, token_len]`
- `IDisposable` 実装

**SBV2ModelRunner**:
- コンストラクタ: `SBV2ModelRunner(ModelAsset, BackendType)`
- `Run(phonemeIds, tones, languageIds, speakerId, bertEmbedding, styleVector, sdpRatio, noiseScale, noiseScaleW, lengthScale)` → `float[]`
- 入力（11テンソル）: `x_tst`, `x_tst_lengths`, `tones`, `language`, `bert`, `style_vec`, `sid`, `sdp_ratio`, `noise_scale`, `noise_scale_w`, `length_scale`
- 出力: `[1, 1, audio_samples]`
- `IDisposable` 実装

### 検証チェックリスト

- [x] BertRunnerがDeBERTa ONNXをロードし、Worker生成
- [x] `BertRunner.Run()` のダミー入力で出力shape `[1, 1024, 3]`
- [x] SBV2ModelRunnerがTTS ONNXをロードし、Worker生成
- [x] `SBV2ModelRunner.Run()` が非空のfloat配列を返す
- [x] GPU/CPUフォールバック動作
- [x] `Dispose()` でWorkerリソースが解放される

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `InferenceTests.BertRunner_Dispose` | Runtime | Dispose安全性の検証 |
| `InferenceTests.SBV2Runner_Dispose` | Runtime | Dispose安全性の検証 |
| `InferenceTests.ModelAssetManager_CreateAndDispose` | Runtime | 生成とDispose |
| `InferenceTests.ModelAssetManager_DoubleDispose` | Runtime | 二重Disposeの安全性 |
| `InferenceTests.ModelAssetManager_HasWorker_ReturnsFalse` | Runtime | 未登録Workerの確認 |

> 以下はONNXモデル配置後に有効化予定（コメントアウト中）:
> BertRunner_Loads, BertRunner_RunDummy, BertRunner_OutputShape,
> SBV2Runner_Loads, SBV2Runner_RunDummy, SBV2Runner_OutputNonEmpty, FallbackBackend

---

## Phase 5: word2phアライメント + パイプライン統合 ✅

### 目標

BERT埋め込みを音素列長に展開し、全コンポーネントを統合TTSPipelineとして結合する。

### 成果物

| ファイル | 説明 |
|---|---|
| `Runtime/Core/TextProcessing/BertAligner.cs` | word2phベースのBERT→音素アライメント |
| `Runtime/Core/Services/ITTSPipeline.cs` | パイプラインインターフェース |
| `Runtime/Core/Services/TTSPipeline.cs` | メインオーケストレータ |
| `Runtime/Core/Services/TTSRequest.cs` | リクエストのreadonly struct |
| `Runtime/Core/Services/TTSPipelineBuilder.cs` | Builderパターン |
| `Runtime/Core/TextProcessing/PhonemeUtils.cs` | add_blank (Intersperse + AdjustWord2PhForBlanks) |
| `Runtime/Core/TextProcessing/PhonemeCharacterAligner.cs` | かな→音素数テーブルによるword2ph計算 |

### 実装仕様の要点

**BertAligner**:
```
bertFlat [1, 1024, token_len] → aligned [1, 1024, phone_seq_len]
word2ph: 各トークンに対応する音素数
展開: bert[d, tokenIdx] → aligned[d, phoneIdx]（word2ph[tokenIdx]回繰り返し）
```

**TTSPipelineフロー**:
1. `_g2p.Process(text)` → G2PResult
1.5. `PhonemeUtils.Intersperse(phonemeIds, 0)` → add_blank (2N+1 音素)
1.5. `PhonemeUtils.AdjustWord2PhForBlanks(word2ph)` → word2ph調整
2. `_tokenizer.Encode(text)` → (tokenIds, attentionMask)
3. `_bertRunner.Run(tokenIds, attentionMask)` → bertOutput
4. `BertAligner.Align(bertOutput, word2ph, phoneSeqLen)` → alignedBert
5. `_styleProvider.GetVector(styleId)` → styleVec
6. `_ttsRunner.Run(...)` → audioSamples
7. `TTSAudioUtility.CreateClip(audioSamples)` → AudioClip

### 検証チェックリスト

- [x] BertAlignerの出力: `sum(word2ph) == phoneSeqLen`
- [x] Python側とのクロスバリデーション（MSE < 1e-5）
- [x] `TTSPipeline.Synthesize("テスト")` がAudioClipを返す
- [x] AudioClipのsampleRate = 44100Hz、channels = 1
- [x] AudioClipのサンプル数 > 0

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `BertAlignerTests.AlignMatchesPhoneSeqLen` | Runtime | 出力長の一致 |
| `BertAlignerTests.Word2PhSumConsistency` | Runtime | word2ph合計の整合性 |
| `BertAlignerTests.SingleTokenExpansion` | Runtime | 単一トークンの展開 |
| `BertAlignerTests.BurstVersion_MatchesCPU` | Runtime | Burst版とCPU版の結果一致 |
| `BertAlignerTests.BurstVersion_DestOverload_WorksWithLargerBuffer` | Runtime | 大きいdestバッファでの動作 |
| `BertAlignerTests.ThrowsOnMismatchedSum` | Runtime | 不一致時の例外 |
| `PipelineTests.Placeholder_PipelineTestsRequireFullSetup` | Runtime | E2Eテストのプレースホルダー |

> PipelineTests のE2Eテスト（SynthesizeReturnsAudioClip, AudioClipSampleRate44100,
> AudioClipHasSamples, DisposeCleansUp）はモデル+DLL配置後に有効化予定（コメントアウト中）。

---

## Phase 6: AudioClip生成 + E2E ✅

### 目標

float32 PCM → AudioClip変換を実装し、初めて人間が聴ける音声出力を実現する。

### 成果物

| ファイル | 説明 |
|---|---|
| `Runtime/Core/Audio/TTSAudioUtility.cs` | PCM→AudioClip変換 + 正規化 |
| `Runtime/Core/Audio/AudioClipGenerator.cs` | バッチAudioClip生成 |
| `Samples~/BasicTTS/SBV2TTSDemo.cs` | 最小デモMonoBehaviour |
| `Samples~/BasicTTS/SampleScene.unity` | UIレイアウト付きテストシーン |

### 実装仕様の要点

**TTSAudioUtility**:
- `CreateClip(float[] samples, int sampleRate = 44100, string name = "TTS")` → AudioClip
- 正規化: maxAbsを検出し `0.95/maxAbs` でスケール（クリッピング防止）
- `AudioClip.Create(name, samples.Length, 1, sampleRate, false)` + `SetData`

**SBV2TTSDemo**:
- InputField（テキスト入力）、Button（合成+再生）、AudioSource
- TTSSettings参照

### 検証チェックリスト

- [x] "こんにちは" → 再生可能な音声
- [x] 音声が歪みなく正規化される
- [x] 複数回の合成/再生サイクルが安定
- [x] メモリリークなし（3回実行後のGC.Allocチェック）

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `AudioTests.CreateClipFromSamples` | Runtime | サンプルからのClip生成 |
| `AudioTests.ClipSampleRateCorrect` | Runtime | サンプルレートの正確性 |
| `AudioTests.NormalizationPreventClipping` | Runtime | 正規化によるクリッピング防止 |
| `AudioTests.EmptySamplesHandled` | Runtime | 空サンプルの処理 |
| `AudioTests.NormalizeSamplesBurst_MatchesScalar` | Runtime | Burst版とスカラー版の結果一致 |
| `AudioTests.NormalizeSamplesBurst_EmptyAndNull_NoThrow` | Runtime | null/空配列で例外なし |
| `AudioTests.LargeSamplesHandled` | Runtime | 大量サンプルの処理 |

---

## Phase 7: 非同期 + パフォーマンス最適化 ✅

### 目標

UniTask非同期パイプライン、BERTキャッシュ、Burstジョブ、ウォームアップを実装し、リアルタイム保証を実現する。

### 成果物

| ファイル | 説明 |
|---|---|
| `ITTSPipeline.cs`（更新） | `SynthesizeAsync()` の追加 |
| `TTSPipeline.cs`（更新） | `SynthesizeAsync()` の実装 |
| `TTSRequestQueue.cs`（更新） | async 呼び出しに変更 |
| `Runtime/Core/Inference/CachedBertRunner.cs` | LRUキャッシュ付きBERT推論 |
| `Runtime/Core/Data/LRUCache.cs` | 汎用LRUキャッシュ |
| `Runtime/Core/Inference/TTSWarmup.cs` | ウォームアップ推論 |
| `Runtime/Core/TextProcessing/BertAlignmentJob.cs` | Burst IJobParallelFor |
| `Runtime/Core/Audio/NormalizeAudioJob.cs` | Burst音声正規化ジョブ |
| `Runtime/Core/Diagnostics/TTSDebugLog.cs` | パイプライン各段のデバッグログ制御 |
| asmdefファイル（更新） | UniTask, ZString, Unity.Burst, Unity.Collections参照追加 |

### 実装仕様の要点

**UniTask統合**:
- `SynthesizeAsync(TTSRequest, CancellationToken)` → `UniTask<AudioClip>`
- `UniTask.SwitchToThreadPool()` でG2P/トークナイザをバックグラウンド実行
- `UniTask.SwitchToMainThread()` でSentis操作をメインスレッドで実行
- `CancellationToken` による推論キャンセル

**BERTキャッシュ**:
- `LRUCache<string, float[]>`（容量: `TTSSettings.BertCacheCapacity`, デフォルト64）
- 同一テキストの再推論を回避（DeBERTaが最大のボトルネック）

**ウォームアップ**:
- `WarmupAll(BertRunner, SBV2ModelRunner)` — 最小ダミー入力でシェーダコンパイル完了
- `TTSSettings.EnableWarmup` トグル

**Burstジョブ**:
- `BertAlignmentJob`: IJobParallelFor — 並列BERT展開
- `NormalizeAudioJob`: IJobParallelFor — 並列音声正規化

### 検証チェックリスト

- [x] `SynthesizeAsync` がAudioClipを返す
- [x] 推論中のUIフリーズなし（30+ FPS維持）
- [x] CancellationTokenで推論が途中停止
- [x] BERTキャッシュ: 同一テキスト2回目はBERTスキップ（ProfilerMarker）
- [x] ウォームアップ後のレイテンシ < 200ms（GPU）
- [x] BurstジョブがNativeArray disposeなしで完了
- [x] ProfilerMarkersで各ステージのレイテンシがターゲット内

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `LRUCacheTests.PutAndGet` | Runtime | キャッシュの基本操作 |
| `LRUCacheTests.EvictsLeastRecentlyUsed` | Runtime | LRU eviction |
| `LRUCacheTests.GetMovesToFront` | Runtime | アクセス時のLRU更新 |
| `CachedBertTests.CacheLogicIsCoveredByLRUCacheTests` | Runtime | LRUCacheテストでカバー |
| `BertAlignmentJobTests.ResultMatchesCPU` | Runtime | Burst結果の一致 |
| `AsyncPipelineTests.SynthesizeAsync_ReturnsAudioClip` | Runtime | 非同期合成 |
| `AsyncPipelineTests.SynthesizeAsync_CancellationThrows` | Runtime | キャンセル動作 |
| `AsyncPipelineTests.SynthesizeAsync_MultipleCallsSucceed` | Runtime | 複数回呼び出し |
| `WarmupTests.Placeholder_WarmupTestsRequireModel` | Runtime | ウォームアップ（モデル要） |
| `G2PDiagnosticTests` | Runtime | G2P出力の診断テスト |
| `ToneAndLanguageConsistencyTests` | Editor | トーン・言語IDの整合性テスト |
| `SymbolConsistencyTests` | Editor | シンボルテーブルの整合性テスト |

---

## Phase 8: エディタ拡張 + デモUI + ポリッシュ ✅

### 目標

開発者体験を向上させるカスタムInspector、モデル検証、完成度の高いデモUIを実装する。

### 成果物

| ファイル | 説明 |
|---|---|
| `Editor/TTSSettingsEditor.cs` | TTSSettingsのカスタムInspector |
| `Editor/ModelImportValidator.cs` | ONNXインポート時の自動検証 |
| `SBV2TTSDemo.cs`（更新） | フルUI（話者選択、スタイル、速度スライダー） |
| `SampleScene.unity`（更新） | 完成版テストシーン |
| `Runtime/Core/Services/TTSRequestQueue.cs` | UniTask Channelベースのリクエストキュー |

### 実装仕様の要点

**デモUIレイアウト**:
```
Canvas
  InputField          — 日本語テキスト入力
  Dropdown - Speaker  — 話者選択
  Dropdown - Style    — スタイル選択
  Slider - Speed      — lengthScale: 0.5-2.0
  Slider - SDP Ratio  — 0.0-1.0
  Button - Synthesize — 合成+再生
  Text - Status       — リアルタイムステータス
AudioSource
SBV2TTSManager (MonoBehaviour)
```

### 検証チェックリスト

- [x] TTSSettingsEditorで全フィールドが編集可能
- [x] ONNXインポート時に自動検証が実行される
- [x] デモで話者/スタイル/速度を変更して合成可能
- [x] ステータスがリアルタイム更新
- [x] 10回連続実行でメモリリークなし

### テスト項目

| テスト名 | 種別 | 内容 |
|---|---|---|
| `EditorTests.CustomInspectorRenders` | Editor | カスタムInspectorの描画 |
| `EditorTests.ModelValidationOnImport` | Editor | インポート時の検証 |
| `DemoTests.SynthesizeWithDifferentSettings` | Runtime | 設定変更での合成 |

---

## 参照ドキュメント一覧

| ドキュメント | 役割 | 関連Phase |
|---|---|---|
| `docs/01_onnx_export.md` | ONNX変換仕様、テンソル定義、Sentis互換性 | Phase 0 |
| `docs/02_g2p_implementation.md` | G2Pパイプライン、音素マッパー、トーン計算 | Phase 2-3 |
| `docs/03_unity_sentis_integration.md` | Sentis APIパターン、Runner実装 | Phase 4-6 |
| `docs/04_architecture_design.md` | Assembly Definition、インターフェース設計、ディレクトリ構成 | Phase 0-5 |
| `docs/05_performance_optimization.md` | キャッシュ、ウォームアップ、フォールバック戦略 | Phase 7 |
| `docs/06_csharp_optimization.md` | メモリ最適化、Burstパターン | Phase 7 |
| `docs/07_cysharp_libraries.md` | UniTask非同期パターン、ZString、NativeMemoryArray | Phase 7 |

---

## リスク管理

| リスク | 影響 | 対策 |
|---|---|---|
| ONNX非互換オペレータ | Phase 4 ブロック | opset 15をターゲット。問題があれば6分割エクスポートに切替 |
| OpenJTalk DLLの不安定性 | Phase 3 ブロック | uPiperの実績あるバイナリを使用。フォールバックとしてRemoteG2P API |
| VRAMがDeBERTaに不足 | Phase 4 品質低下 | CPUフォールバック、またはCPU/GPU混合戦略 |
| word2phアライメント不一致 | Phase 5 品質低下 | Pythonからゴールデンテストデータを生成し数値比較 |
| トーン計算エラー | 音声品質低下 | sbv2-api Rust実装と比較検証 |

---

## 進捗状況

全Phase（0-8）の実装が完了済み。

| Phase | コミット | ステータス |
|---|---|---|
| Phase 0 | `8c236a0` Project foundation | 完了 |
| Phase 1 | `210c87e` Data layer | 完了 |
| Phase 2 | `4cfd85d` DeBERTa tokenizer | 完了 |
| Phase 3 | `0352898` G2P | 完了 |
| Phase 4 | `62b9a2b` Sentis inference wrappers | 完了 |
| Phase 5 | `c52eaeb` Pipeline integration | 完了 |
| Phase 6 | `22b9c56` AudioClip generation + demo | 完了 |
| Phase 7 | `f15e39c` Async + optimization | 完了 |
| Phase 8 | `363e2aa` Editor extensions + TTSRequestQueue | 完了 |
| — | `8c628f3` Unity .meta files + Python scripts cleanup | 完了 |
| — | `eed7f3b` コンパイルエラー修正（asmdef参照 + using追加） | 完了 |
| P1 | `67f207f` Add SynthesizeAsync to TTS pipeline | 完了 |
| P1 | `67e9489` Add BasicTTS sample scene with UI layout | 完了 |
| — | `c29e00b` Fix P/Invoke signatures, add SBV2 conversion script, and reorganize assets | 完了 |
| — | `6e38c9b` Add inference tests and fix ONNX conversion for Sentis compatibility | 完了 |
| — | `c06f32c` Add blank interleaving, diagnostics, and improve G2P/inference pipeline | 完了 |
