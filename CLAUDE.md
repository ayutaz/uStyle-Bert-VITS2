# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

Style-Bert-VITS2（日本語TTS）のモデルをONNXに変換し、Unity Sentis（AI Inference Engine）でリアルタイム推論するプロジェクト。

- **Unity バージョン**: 6000.3.6f1 (Unity 6)
- **推論エンジン**: Unity AI Inference (Sentis) 2.5.0 (`com.unity.ai.inference`)
- **非同期**: UniTask (Cysharp)
- **文字列**: ZString (Cysharp)
- **元モデル**: [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — VITS2ベースの日本語音声合成（JP-Extra版）
- **言語**: C# (Unity側), Python (ONNX変換側)

## セットアップ

以下のパッケージは `manifest.json` に追加済み:
- `com.unity.ai.inference: 2.5.0` (Sentis)
- UniTask (UPM Git URL)
- ZString (UPM Git URL)

手動でのセットアップは不要。

## アーキテクチャ: Style-Bert-VITS2 推論パイプライン

推論は以下の8ステージで構成される:

```
1. G2P        : テキスト → 音素ID + トーン + 言語ID + word2ph
2. add_blank  : PhonemeUtils.Intersperse で blank(0) を挿入 (N → 2N+1)
3. Tokenize   : テキスト → DeBERTa トークンID + attention_mask
4. BERT推論   : DeBERTa で文脈埋め込み [1, 1024, token_len]
5. Alignment  : word2ph で BERT 出力を音素列長に展開 [1, 1024, phone_seq_len]
6. StyleVector: style_vectors.npy からルックアップ [1, 256]
7. TTS推論    : SynthesizerTrn で音声波形生成 [1, 1, audio_samples]
8. AudioClip  : 正規化 + トリム → Unity AudioClip (44100Hz mono)
```

### 前処理の詳細

- **G2P (Grapheme-to-Phoneme)**: OpenJTalk P/Invoke (`openjtalk_wrapper.dll`) で日本語テキスト→音素列+アクセント(tone)変換。`JapaneseG2P` クラスが統合
- **add_blank**: SBV2モデルは `add_blank=true` で学習されているため、`PhonemeUtils.Intersperse()` で各音素間に blank トークン(0) を挿入。`PhonemeUtils.AdjustWord2PhForBlanks()` で word2ph も調整
- **DeBERTa (BERT)**: テキスト→1024次元の文脈埋め込み。`hidden_states[-3]` (3番目に最後の隠れ層) を単独使用。BertBackend=CPU が必須（GPUCompute は D3D12 デバイスロスト）
- **スタイルベクトル**: `style_vectors.npy`から256次元ベクトルをルックアップ。`mean + (style - mean) * weight` で計算

### メイン合成モデル (SynthesizerTrn)

monolithic方式（1ファイル）でエクスポート。6つのサブモジュールを内包:

| サブモデル | 役割 |
|---|---|
| `enc_p` (TextEncoder) | 音素+BERT埋め込み→潜在表現 |
| `dp` (DurationPredictor) | 確定的な音素継続長予測 |
| `sdp` (StochasticDurationPredictor) | 確率的な継続長予測 |
| `flow` (NormalizingFlow) | 潜在空間変換（韻律） |
| `dec` (Decoder/HiFi-GAN) | 潜在表現→音声波形 |
| `emb_g` (SpeakerEmbedding) | 話者ID→埋め込みベクトル |

### 出力
- float32 PCM音声波形（44100Hz mono）

## SynthesizerTrn の入力テンソル (JP-Extra)

| 入力名 | 型 | Shape | 説明 |
|---|---|---|---|
| `x_tst` | int32 | `[1, seq_len]` | 音素トークンID（add_blank後） |
| `x_tst_lengths` | int32 | `[1]` | 音素列の長さ |
| `tones` | int32 | `[1, seq_len]` | トーン/アクセント値（add_blank後） |
| `language` | int32 | `[1, seq_len]` | 言語ID（日本語=1, add_blank後） |
| `bert` | float32 | `[1, 1024, seq_len]` | BERT埋め込み（JP-Extraは単一入力） |
| `style_vec` | float32 | `[1, 256]` | スタイルベクトル |
| `sid` | int32 | `[1]` | 話者ID |
| `sdp_ratio` | float32 | scalar | SDP/DP混合比 (0.0-1.0) |
| `noise_scale` | float32 | scalar | 生成ノイズ (default 0.667) |
| `noise_scale_w` | float32 | scalar | 継続長ノイズ |
| `length_scale` | float32 | scalar | 話速倍率 |

> **Note**: JP-Extraモデルは `bert` 単一入力。通常モデル（多言語）は `ja_bert`/`bert`/`en_bert` の3入力だが、`SBV2ModelRunner` が自動判定する。

## バックエンド設定

`TTSSettings` ScriptableObject で BERT と TTS のバックエンドを個別に設定:

| 設定 | デフォルト | 説明 |
|---|---|---|
| `BertEngineType` | `Sentis` | BERT推論エンジン (`Sentis` / `OnnxRuntime`) |
| `BertBackend` | `CPU` | Sentis使用時のバックエンド。**CPU 必須**（FP32 GPU → D3D12 デバイスロスト） |
| `TTSBackend` | `GPUCompute` | SynthesizerTrn 推論。GPU 推奨 |
| `UseDirectML` | `true` | ORT使用時にDirectML (GPU) を有効化 |
| `DirectMLDeviceId` | `0` | DirectML デバイスID (0=デフォルトGPU) |

### 実測パフォーマンス (Windows デスクトップ)

| 構成 | レイテンシ |
|---|---|
| BERT=CPU + TTS=CPU | ~753ms |
| BERT=CPU + TTS=GPU (初回) | ~969ms |
| BERT=CPU + TTS=GPU (2回目以降) | ~621ms |

### BERT バックエンド別ベンチマーク (RTX 4070 Ti SUPER, Editor)

| 入力サイズ | Sentis CPU | ORT DirectML | ORT CPU | DirectML スピードアップ |
|---|---|---|---|---|
| 5 tokens | ~965 ms | ~66 ms | ~440 ms | 14.6x |
| 20 tokens | ~829 ms | ~285 ms | ~468 ms | 2.9x |
| 40 tokens | ~898 ms | ~266 ms | ~461 ms | 3.4x |

## ONNX変換

### 変換スクリプト

| スクリプト | 説明 |
|---|---|
| `scripts/convert_for_sentis.py` | SynthesizerTrn エクスポート（opset 15, FP16, int64→int32） |
| `scripts/convert_bert_for_sentis.py` | DeBERTa エクスポート（opset 15, FP32） |
| `scripts/convert_sbv2_for_sentis.py` | HuggingFace モデルからの一括変換 |
| `scripts/validate_onnx.py` | OnnxRuntime 推論検証 |

### 変換時の注意点
- **opset 15を指定すること** — Sentisはopset 7-15をサポート（実態として16+も動作可能）
- **SBV2 は FP16 推奨** — メモリ使用量が半減、品質劣化はほぼなし
- **DeBERTa は FP32 必須** — Sentis 2.5.0 は FP16 定数テンソルデータをインポート不可
- **int64→int32変換** — Sentis は `Tensor<int>` = int32 のみ対応。Constant ノード(5000+個)、Cast ノード、中間 value_info もすべて変換が必要
- **SBV2 静的エクスポート** — `--no-fp16 --no-simplify` オプションで Sentis 互換の静的シェイプ ONNX を生成

## Unity Sentis (AI Inference) の基本API

```csharp
using Unity.InferenceEngine;

Model model = ModelLoader.Load(modelAsset);
Worker worker = new Worker(model, BackendType.GPUCompute);
worker.SetInput("input_name", tensor);
worker.Schedule();
Tensor<float> output = worker.PeekOutput("output_name") as Tensor<float>;
output.ReadbackAndClone();
float[] data = output.DownloadToArray();
```

### 注意すべき制約
- **DeBERTa FP32 + GPUCompute → D3D12 デバイスロスト** — BertBackend=CPU が必須
- **FP16 定数テンソル非対応** — Sentis 2.5.0 の ONNX インポーターの制約
- **ConvTranspose**: `dilations`, `group`, `output_shape`パラメータ非対応（SBV2では問題なし）
- **制御フロー(Loop, If)非対応**: エクスポート時に `torch.jit.trace` で排除
- **動的シェイプ**: サポートされるが BertRunner/SBV2ModelRunner は固定 padLen にパディングして実行

## 実装済みの構成

### G2P（テキスト→音素変換）
- **OpenJTalk P/Invoke**: uPiper の `openjtalk_wrapper.dll` を流用。`JapaneseG2P` クラスが OpenJTalk → SBV2PhonemeMapper → PhonemeCharacterAligner を統合

### BERT埋め込み
- **IBertRunner インターフェース**: `BertRunner` (Sentis) と `OnnxRuntimeBertRunner` (ORT+DirectML) を統一的に扱う
- **BertRunner (Sentis)**: padLen 自動検出・パディング・トリム処理。`CachedBertRunner` で LRU キャッシュによる重複推論回避
- **OnnxRuntimeBertRunner (ORT)**: DirectML (GPU) 優先で `EntryPointNotFoundException` 時は CPU にフォールバック。動的シェイプをネイティブサポートしパディング不要
- **dest バッファオーバーロード**: `Run(tokenIds, mask, dest)` で事前確保バッファに書き込み、GC アロケーション回避。出力トリムは `UnsafeUtility.MemCpy` で高速化
- `token_type_ids` は不要（onnxsim で定数化済み）

### 非同期パイプライン
- **UniTask ベース**: `SynthesizeAsync(TTSRequest, CancellationToken)` → `UniTask<AudioClip>`
- G2P/Tokenize は `UniTask.SwitchToThreadPool()` でバックグラウンド実行
- Sentis 推論は `UniTask.SwitchToMainThread()` でメインスレッド実行

## ディレクトリ構成

```
Assets/uStyleBertVITS2/
  Runtime/
    Core/
      Inference/
        IBertRunner.cs             # BERT推論インターフェース
        BertRunner.cs              # Sentis DeBERTa推論（padLen自動検出・パディング・dest overload・unsafe MemCpy）
        OnnxRuntimeBertRunner.cs   # ORT+DirectML DeBERTa推論（動的シェイプ・DirectMLフォールバック）
        SBV2ModelRunner.cs         # TTS推論（JP-Extra自動判定・unsafe パディング・スカラーバッファ再利用）
        ModelAssetManager.cs       # モデルロード・ライフサイクル管理
        CachedBertRunner.cs        # LRUキャッシュ付きBERT推論
        BertBenchmark.cs           # BERTバックエンド別ベンチマーク
        TTSWarmup.cs               # シェーダコンパイル事前ウォームアップ
      TextProcessing/
        IG2P.cs                    # G2Pインターフェース
        JapaneseG2P.cs             # OpenJTalkベースG2P統合パイプライン
        SBV2PhonemeMapper.cs       # OpenJTalk音素→SBV2トークンID (112シンボル)
        SBV2Tokenizer.cs           # DeBERTa用文字レベルトークナイザ
        TextNormalizer.cs          # テキスト正規化
        G2PResult.cs               # G2P出力のreadonly struct
        BertAligner.cs             # word2phベースBERT→音素アライメント
        BertAlignmentJob.cs        # Burst IJobParallelFor (並列BERT展開)
        PhonemeUtils.cs            # add_blank (Intersperse + AdjustWord2Ph)
        PhonemeCharacterAligner.cs # かな→音素数テーブルによるword2ph計算
      Audio/
        TTSAudioUtility.cs         # PCM→AudioClip変換
        AudioClipGenerator.cs      # バッチAudioClip生成
        NormalizeAudioJob.cs       # Burst音声正規化ジョブ
      Configuration/
        TTSSettings.cs             # ScriptableObject (BertBackend/TTSBackend分離)
        ModelConfiguration.cs      # モデルパス設定
      Services/
        ITTSPipeline.cs            # パイプラインインターフェース
        TTSPipeline.cs             # メインオーケストレータ (8ステージ, ArrayPool, GetTrimmedLength)
        TTSRequest.cs              # リクエストstruct (StyleWeight含む)
        TTSPipelineBuilder.cs      # Builderパターン
        TTSRequestQueue.cs         # UniTask Channelベースのリクエストキュー
      Data/
        NpyReader.cs               # NumPy .npy パーサー
        StyleVectorProvider.cs     # スタイルベクトルルックアップ
        LRUCache.cs                # 汎用LRUキャッシュ
      Native/
        OpenJTalkNative.cs         # P/Invoke定義
        OpenJTalkConstants.cs      # 辞書パス定数
        OpenJTalkHandle.cs         # SafeHandle
      Diagnostics/
        TTSDebugLog.cs             # パイプライン各段のデバッグログ制御
    AssemblyInfo.cs
    uStyleBertVITS2.Runtime.asmdef  # refs: InferenceEngine, UniTask, UniTask.Linq, Burst
  Editor/
    TTSSettingsEditor.cs           # カスタムInspector
    ModelImportValidator.cs        # ONNXインポート検証
    OrtDirectMLPostProcessBuild.cs # ビルド後DirectML.dllコピー
    uStyleBertVITS2.Editor.asmdef
  Tests/
    Runtime/                       # G2P, Tokenizer, Aligner, Audio, Cache, Async等
      uStyleBertVITS2.Tests.Runtime.asmdef
    Editor/                        # Configuration, Symbol, Tone等の整合性テスト
      uStyleBertVITS2.Tests.Editor.asmdef
  Plugins/
    Windows/x86_64/
      openjtalk_wrapper.dll
      onnxruntime.dll              # DirectML対応版 (NuGet Microsoft.ML.OnnxRuntime.DirectML)
      onnxruntime_providers_shared.dll
      DirectML.dll                 # DirectMLランタイム (NuGet Microsoft.AI.DirectML)
  Samples~/
    BasicTTS/
      SBV2TTSDemo.cs
      SampleScene.unity
  StreamingAssets/
    uStyleBertVITS2/
      OpenJTalkDic/                # NAIST JDIC辞書 (8ファイル)
      Tokenizer/vocab.json         # DeBERTa語彙
      Models/                      # ONNXモデル
      StyleVectors/                # style_vectors.npy
scripts/
  convert_for_sentis.py            # SBV2 ONNX変換
  convert_bert_for_sentis.py       # DeBERTa ONNX変換
  convert_sbv2_for_sentis.py       # HuggingFace一括変換
  validate_onnx.py                 # ONNX検証
```

## 参考リンク

- [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — 元リポジトリ
- [Bert-VITS2 ONNX推論](https://github.com/fishaudio/Bert-VITS2/blob/master/onnx_infer.py) — 6分割ONNXの参考実装
- [sbv2-api (Rust実装)](https://github.com/neodyland/sbv2-api) — Python外でのONNX推論の実例
- [Unity Sentis ドキュメント](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html)
- [Sentis対応オペレータ一覧](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/supported-operators.html)
