# uStyle-Bert-VITS2

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-6000.3+-black.svg)](https://unity.com/)

[English](README_EN.md)

[Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) の日本語音声合成モデルを Unity 上でリアルタイム推論するライブラリです。ONNX に変換したモデルを [Unity Sentis (AI Inference Engine)](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) または [ONNX Runtime + DirectML](https://github.com/asus4/onnxruntime-unity) で実行します。

## Table of Contents

- [Demo](#demo)
- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Performance](#performance)
- [ONNX 変換](#onnx-変換)
- [Project Structure](#project-structure)
- [Limitations](#limitations)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [Security](#security)
- [Acknowledgements](#acknowledgements)
- [License](#license)

## Demo

<!-- デモ動画をここに追加予定 -->

## Features

- **完全な C# 実装** — G2P (OpenJTalk P/Invoke) からDeBERTa推論、TTS合成まで Unity 内で完結
- **非同期パイプライン** — UniTask ベースの `SynthesizeAsync` でメインスレッドをブロックしない音声合成
- **GPU 推論** — SynthesizerTrn は GPUCompute バックエンドで高速推論（~621ms）
- **BERT GPU 推論 (ORT+DirectML)** — ONNX Runtime + DirectML で BERT 推論を最大 14.6x 高速化（`IBertRunner` によるマルチバックエンド対応）
- **Builder パターン** — `TTSPipelineBuilder` による簡潔なセットアップ
- **LRU キャッシュ** — `CachedBertRunner` で同一テキストの BERT 推論を自動キャッシュ
- **Burst 最適化** — BertAlignment と音声正規化に Burst ジョブを活用
- **GC 圧力削減** — ArrayPool、dest バッファオーバーロード、スカラーバッファ再利用、unsafe MemCpy で推論1回あたり ~470KB の GC アロケーション削減
- **ベンチマークツール** — `BertBenchmark` で BERT バックエンド別の性能比較

## Requirements

- **Unity** 6000.3.6f1 (Unity 6) 以降
- **Unity AI Inference (Sentis)** 2.5.0
- **UniTask** 2.5.10+
- **ZString** 2.6.0+
- **ONNX Runtime (asus4)** 0.4.4+（optional — ORT+DirectML BERT 推論を使用する場合）
- **Platform** Windows x86_64（OpenJTalk ネイティブプラグイン）

## Installation

### UPM (git URL)

Unity Package Manager から git URL でインストールできます。`Packages/manifest.json` に以下を追加してください:

```json
{
  "dependencies": {
    "com.ustyle.bert-vits2": "https://github.com/ayutaz/uStyle-Bert-VITS2.git?path=Assets/uStyleBertVITS2"
  }
}
```

> **Note**: Sentis は package dependency として解決されます。UniTask / ZString / ONNX Runtime は別途インストールが必要です。

### モデルファイルを配置

以下のファイルを `Assets/StreamingAssets/uStyleBertVITS2/` に配置してください:

公開済みアセットは Hugging Face から取得できます:  
https://huggingface.co/ayousanz/uStyle-Bert-VITS2

```
StreamingAssets/uStyleBertVITS2/
  Models/
    sbv2_model.onnx          # SynthesizerTrn (FP32, 現行配布)
    deberta_model.onnx        # DeBERTa for Sentis (FP32, int32)
    deberta_for_ort.onnx      # DeBERTa for ORT (FP32, int64) ※ORT使用時のみ
    style_vectors.npy         # スタイルベクトル
  OpenJTalkDic/               # NAIST JDIC辞書 (8ファイル)
    char.bin, left-id.def, matrix.bin, pos-id.def,
    right-id.def, sys.dic, unk.dic, rewrite.def
  Tokenizer/
    vocab.json                # DeBERTa語彙ファイル
```

> モデルファイルは `scripts/convert_sbv2_for_sentis.py` で HuggingFace から変換・取得できます。
> 詳細は [ONNX 変換](#onnx-変換) を参照してください。
>
> ```bash
> cd scripts && uv sync
> uv run convert_sbv2_for_sentis.py --repo <hf-repo-id> --no-fp16 --no-simplify
> ```
>
> ORT 用の `deberta_for_ort.onnx` は Sentis 用とは別の変換が必要です（int64 維持、FP32 出力 Cast 追加）。

### TTSSettings を作成

`Assets > Create > uStyleBertVITS2 > TTS Settings` から ScriptableObject を作成し、モデルアセットの参照を設定します。

## Quick Start

```csharp
using uStyleBertVITS2.Configuration;
using uStyleBertVITS2.Services;

// パイプライン構築
ITTSPipeline pipeline = new TTSPipelineBuilder()
    .WithSettings(ttsSettings)
    .Build();

// リクエスト作成
var request = new TTSRequest(
    text: "こんにちは、世界！",
    speakerId: 0,
    styleId: 0,
    sdpRatio: 0.2f,
    lengthScale: 1.0f);

// 非同期合成
// TTSSettings で BertEngineType を OnnxRuntime に切り替えると ORT+DirectML で BERT 推論
AudioClip clip = await pipeline.SynthesizeAsync(request, cancellationToken);

// 再生
audioSource.clip = clip;
audioSource.Play();

// 解放
pipeline.Dispose();
```

サンプルシーンは `Assets/uStyleBertVITS2/Samples~/BasicTTS/` にあります。Package Manager の Samples からインポートできます。

## Configuration

`TTSSettings` ScriptableObject で BERT と TTS のバックエンドを個別に設定できます:

| 設定 | デフォルト | 説明 |
|---|---|---|
| `BertEngineType` | `Sentis` | BERT 推論エンジン (`Sentis` / `OnnxRuntime`) |
| `BertBackend` | `CPU` | Sentis 使用時のバックエンド。**CPU 必須**（FP32 GPU → D3D12 デバイスロスト） |
| `TTSBackend` | `GPUCompute` | SynthesizerTrn 推論。GPU 推奨 |
| `UseDirectML` | `true` | ORT 使用時に DirectML (GPU) を有効化 |
| `DirectMLDeviceId` | `0` | DirectML デバイス ID (0=デフォルト GPU) |

> **推奨構成**: BERT=ORT DirectML + TTS=GPUCompute で最速の推論が可能です。ORT が利用できない環境では BERT=Sentis CPU + TTS=GPUCompute にフォールバックします。

## Architecture

8 ステージの推論パイプライン:

```
Text ─→ [G2P] ─→ [add_blank] ─→ [Tokenize] ─→ [BERT] ─→ [Alignment] ─→ [StyleVector] ─→ [TTS] ─→ AudioClip
         │           │               │             │           │               │              │
    OpenJTalk   PhonemeUtils    SBV2Tokenizer  IBertRunner BertAligner  StyleVectorProvider  SBV2ModelRunner
    P/Invoke    Intersperse     (DeBERTa)      ├BertRunner  word2ph展開  npy lookup          (Sentis)
                                               │ (Sentis CPU)
                                               └OnnxRuntimeBertRunner
                                                 (ORT+DirectML)
```

| Stage | Description | Thread |
|---|---|---|
| G2P | 日本語テキスト → 音素ID + トーン + word2ph | ThreadPool |
| add_blank | blank(0) トークン挿入 (N → 2N+1) | ThreadPool |
| Tokenize | DeBERTa 文字トークナイズ | ThreadPool |
| BERT | DeBERTa 推論 → 1024次元埋め込み | Main (Sentis CPU) or DirectML (ORT GPU) |
| Alignment | word2ph で BERT 出力を音素列長に展開 | ThreadPool |
| StyleVector | style_vectors.npy からルックアップ | ThreadPool |
| TTS | SynthesizerTrn 推論 → 音声波形 | Main (GPU) |
| AudioClip | 正規化 + 無音トリム → AudioClip 生成 | Main |

## Performance

Windows デスクトップでの実測値:

| Configuration | Latency |
|---|---|
| BERT=CPU + TTS=CPU | ~753ms |
| BERT=CPU + TTS=GPU (initial) | ~969ms |
| BERT=CPU + TTS=GPU (cached) | ~621ms |

> Sentis で DeBERTa (FP32) を GPUCompute で実行すると D3D12 デバイスロストが発生するため、Sentis 使用時は CPU バックエンド必須です。ORT+DirectML を使用すると GPU 推論が可能になります。

### BERT バックエンド別ベンチマーク

RTX 4070 Ti SUPER, Editor での実測値:

| Input Size | Sentis CPU | ORT DirectML | ORT CPU | DirectML Speedup |
|---|---|---|---|---|
| 5 tokens | ~965 ms | ~66 ms | ~440 ms | 14.6x |
| 20 tokens | ~829 ms | ~285 ms | ~468 ms | 2.9x |
| 40 tokens | ~898 ms | ~266 ms | ~461 ms | 3.4x |

### GC 最適化

推論1回あたり ~470KB の GC アロケーション削減を実現しています。詳細は [Performance Optimization](docs/05_performance_optimization.md) を参照。

## ONNX 変換

`scripts/` ディレクトリに Sentis 互換の ONNX を生成する変換スクリプトがあります。

```bash
# Python環境セットアップ (uv推奨)
cd scripts
uv sync

# HuggingFace モデルからの一括変換
uv run convert_sbv2_for_sentis.py --repo <hf-repo-id> --no-fp16 --no-simplify

# 個別変換
uv run convert_for_sentis.py <model_path>          # SynthesizerTrn
uv run convert_bert_for_sentis.py <deberta_path>    # DeBERTa

# 検証
uv run validate_onnx.py <onnx_path>
```

> 変換の詳細・注意点は [ONNX Export Guide](docs/01_onnx_export.md) を参照してください。

## Project Structure

```
Assets/uStyleBertVITS2/
  Runtime/
    Core/
      Inference/         # IBertRunner, BertRunner, OnnxRuntimeBertRunner, SBV2ModelRunner, CachedBertRunner, BertBenchmark
      TextProcessing/    # JapaneseG2P, SBV2Tokenizer, BertAligner, PhonemeUtils
      Audio/             # AudioClip生成, Burst正規化ジョブ
      Configuration/     # TTSSettings (ScriptableObject)
      Services/          # TTSPipeline, TTSPipelineBuilder, TTSRequestQueue
      Data/              # NpyReader, StyleVectorProvider, LRUCache
      Native/            # OpenJTalk P/Invoke
      Diagnostics/       # TTSDebugLog
  Editor/                # Custom Inspector, Import Validator, OrtDirectMLPostProcessBuild
  Tests/                 # Runtime & Editor テスト (21 files, 159+ tests)
  Plugins/               # openjtalk_wrapper.dll, onnxruntime.dll (DirectML), DirectML.dll (Windows x86_64)
  Samples~/              # BasicTTS デモシーン
scripts/                 # Python ONNX変換スクリプト
docs/                    # 詳細な設計ドキュメント
```

`docs/` ディレクトリに実装ロードマップ、ONNX エクスポート仕様、アーキテクチャ設計などの[詳細なドキュメント](docs/)があります。

## Limitations

- **Windows x86_64 のみ** — OpenJTalk ネイティブプラグイン依存（macOS / Linux 非対応）
- **日本語音声合成のみ** — JP-Extra モデルを使用
- **Sentis BERT は CPU 必須** — DeBERTa FP32 を GPUCompute で実行すると D3D12 デバイスロスト（ORT+DirectML で GPU 推論可能）
- **Sentis 2.5.0 の FP16 制約** — DeBERTa の FP16 量子化非対応

## Troubleshooting

### D3D12 Device Lost エラー
`TTSSettings > BertBackend` を `CPU` に設定するか、`BertEngineType` を `OnnxRuntime` に切り替えてください。

### EntryPointNotFoundException (DirectML)
`Plugins/Windows/x86_64/` に `onnxruntime.dll` (DirectML 版)、`onnxruntime_providers_shared.dll`、`DirectML.dll` が配置されているか確認してください。未配置の場合、ORT は自動的に CPU にフォールバックします。

### モデルロード失敗
`Assets/StreamingAssets/uStyleBertVITS2/Models/` にモデルファイルが正しく配置されているか確認してください。

### OpenJTalk P/Invoke エラー
Windows x86_64 環境で実行しているか確認してください。macOS / Linux では動作しません。

## Contributing

Issue や Pull Request を歓迎します。詳細は [CONTRIBUTING.md](CONTRIBUTING.md) を参照してください。  
バグ報告・機能リクエストは [GitHub Issues](https://github.com/ayutaz/uStyle-Bert-VITS2/issues) にお願いします。

## Security

脆弱性報告ポリシーは [SECURITY.md](SECURITY.md) を参照してください。

## Acknowledgements

- [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — 元モデル・学習コード
- [Bert-VITS2](https://github.com/fishaudio/Bert-VITS2) — ONNX 推論の参考実装
- [sbv2-api](https://github.com/neodyland/sbv2-api) — Rust による ONNX 推論実装
- [uPiper](https://github.com/Macoron/uPiper) — OpenJTalk ネイティブプラグイン
- [UniTask](https://github.com/Cysharp/UniTask) — Unity 向け非同期ライブラリ
- [ZString](https://github.com/Cysharp/ZString) — ゼロアロケーション文字列フォーマット
- [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) — AI 推論エンジン
- [ONNX Runtime](https://onnxruntime.ai/) — クロスプラットフォーム ML 推論エンジン
- [onnxruntime-unity (asus4)](https://github.com/asus4/onnxruntime-unity) — ONNX Runtime Unity プラグイン
- [DirectML](https://github.com/microsoft/DirectML) — Windows GPU アクセラレーション

## License

Apache License 2.0 — 詳細は [LICENSE](LICENSE) を参照してください。

モデルや辞書などの第三者由来アセットには別ライセンス/利用条件が適用される場合があります。詳細は [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
