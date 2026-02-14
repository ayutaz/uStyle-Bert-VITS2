# uStyle-Bert-VITS2

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-6000.3+-black.svg)](https://unity.com/)

[English](README_EN.md)

[Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) 系の日本語TTSモデルを Unity で実行するライブラリです。  
ONNX モデルを [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) または [ONNX Runtime + DirectML](https://github.com/asus4/onnxruntime-unity) で推論します。

## Demo

[![uStyle-Bert-VITS2 Demo](https://img.youtube.com/vi/zooXK0Arm24/hqdefault.jpg)](https://youtu.be/zooXK0Arm24)

Video: https://youtu.be/zooXK0Arm24

## Features

- Unity内で完結する C# パイプライン（G2P -> BERT -> TTS）
- `SynthesizeAsync` による非同期推論
- BERT は `Sentis` / `OnnxRuntime` を切り替え可能
- TTS (SynthesizerTrn) は `GPUCompute` 実行に対応
- LRUキャッシュと各種最適化でGC圧力を削減

## Requirements

- Unity 6000.3.6f1 (Unity 6) 以降
- Unity AI Inference (Sentis) 2.5.0
- UniTask 2.5.10+
- ZString 2.6.0+
- ONNX Runtime (asus4) 0.4.4+（ORT+DirectML を使う場合）
- Windows x86_64（OpenJTalk ネイティブプラグイン）

## Installation

### 1) UPM で導入

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.ustyle.bert-vits2": "https://github.com/ayutaz/uStyle-Bert-VITS2.git?path=Assets/uStyleBertVITS2"
  }
}
```

Sentis は依存として解決されます。UniTask / ZString / ONNX Runtime は別途導入してください。

### 2) 必要ファイルを配置

公開済みアセット: https://huggingface.co/ayousanz/uStyle-Bert-VITS2

以下を `Assets/StreamingAssets/uStyleBertVITS2/` に配置します。

```text
StreamingAssets/uStyleBertVITS2/
  Models/
    sbv2_model.onnx       # SynthesizerTrn (FP32, current distribution)
    deberta_model.onnx    # DeBERTa for Sentis (FP32, int32)
    deberta_for_ort.onnx  # DeBERTa for ORT (FP32, int64) *ORT時のみ
    style_vectors.npy
  OpenJTalkDic/
    char.bin, left-id.def, matrix.bin, pos-id.def,
    right-id.def, sys.dic, unk.dic, rewrite.def
  Tokenizer/
    vocab.json
```

### 3) （任意）ONNX 変換

```bash
cd scripts
uv sync
uv run convert_sbv2_for_sentis.py --repo <hf-repo-id> --no-fp16 --no-simplify
```

詳細は `docs/01_onnx_export.md` を参照してください。

### 4) `TTSSettings` を作成

`Assets > Create > uStyleBertVITS2 > TTS Settings` から作成し、モデル参照を設定します。

## Quick Start

```csharp
using uStyleBertVITS2.Configuration;
using uStyleBertVITS2.Services;

ITTSPipeline pipeline = new TTSPipelineBuilder()
    .WithSettings(ttsSettings)
    .Build();

var request = new TTSRequest(
    text: "こんにちは、世界！",
    speakerId: 0,
    styleId: 0,
    sdpRatio: 0.2f,
    lengthScale: 1.0f);

AudioClip clip = await pipeline.SynthesizeAsync(request, cancellationToken);

audioSource.clip = clip;
audioSource.Play();

pipeline.Dispose();
```

サンプルシーン: `Assets/uStyleBertVITS2/Samples~/BasicTTS/`

## Configuration

`TTSSettings` の主要設定:

| Setting | Default | Notes |
|---|---|---|
| `BertEngineType` | `Sentis` | `Sentis` / `OnnxRuntime` |
| `BertBackend` | `CPU` | Sentis時は `CPU` 推奨（GPUでD3D12 device lost） |
| `TTSBackend` | `GPUCompute` | TTS推論はGPU推奨 |
| `UseDirectML` | `true` | ORT時にDirectML (GPU) を有効化 |
| `DirectMLDeviceId` | `0` | 使用GPUのデバイスID |

推奨構成: `BERT=OnnxRuntime(DirectML) + TTS=GPUCompute`

## Performance

測定環境: Windows desktop（Editor）

| Configuration | Latency |
|---|---|
| BERT=Sentis CPU + TTS=CPU | ~753ms |
| BERT=Sentis CPU + TTS=GPU（初回） | ~969ms |
| BERT=Sentis CPU + TTS=GPU（キャッシュ後） | ~621ms |

詳細ベンチマークは [docs/05_performance_optimization.md](docs/05_performance_optimization.md) と [docs/06_csharp_optimization.md](docs/06_csharp_optimization.md) を参照してください。

## More Docs

- リリースノート: [CHANGELOG.md](CHANGELOG.md)
- 実装方針: `docs/00_implementation_roadmap.md`
- ONNX変換: `docs/01_onnx_export.md`
- Unity/Sentis統合: `docs/03_unity_sentis_integration.md`
- アーキテクチャ詳細: `docs/04_architecture_design.md`
- 性能最適化: `docs/05_performance_optimization.md`
- C#最適化: `docs/06_csharp_optimization.md`
- 貢献方法: `CONTRIBUTING.md`
- セキュリティ報告: `SECURITY.md`
- サードパーティライセンス: `THIRD_PARTY_NOTICES.md`

## License

Apache License 2.0: `LICENSE`  
モデルや辞書など第三者由来アセットの条件は `THIRD_PARTY_NOTICES.md` を参照してください。
