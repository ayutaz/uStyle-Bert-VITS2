# uStyle-Bert-VITS2

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-6000.3+-black.svg)](https://unity.com/)

[日本語](README.md)

A Unity library for running [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) Japanese TTS models in real time.  
ONNX models run on [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) or [ONNX Runtime + DirectML](https://github.com/asus4/onnxruntime-unity).

## Demo

[![uStyle-Bert-VITS2 Demo](https://img.youtube.com/vi/zooXK0Arm24/hqdefault.jpg)](https://youtu.be/zooXK0Arm24)

Video: https://youtu.be/zooXK0Arm24

## Features

- Pure C# pipeline in Unity (G2P -> BERT -> TTS)
- Async synthesis with `SynthesizeAsync`
- Switchable BERT backend: `Sentis` or `OnnxRuntime`
- GPU-backed TTS inference via `GPUCompute`
- Reduced GC pressure with caching and runtime optimizations

## Requirements

- Unity 6000.3.6f1 (Unity 6) or later
- Unity AI Inference (Sentis) 2.5.0
- UniTask 2.5.10+
- ZString 2.6.0+
- ONNX Runtime (asus4) 0.4.4+ (for ORT+DirectML)
- Windows x86_64 (OpenJTalk native plugin)

## Installation

### 1) Install via UPM

`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.ustyle.bert-vits2": "https://github.com/ayutaz/uStyle-Bert-VITS2.git?path=Assets/uStyleBertVITS2"
  }
}
```

Sentis is resolved as a dependency. Install UniTask / ZString / ONNX Runtime separately.

### 2) Place required assets

Prebuilt assets: https://huggingface.co/ayousanz/uStyle-Bert-VITS2

Place the following under `Assets/StreamingAssets/uStyleBertVITS2/`.

```text
StreamingAssets/uStyleBertVITS2/
  Models/
    sbv2_model.onnx       # SynthesizerTrn (FP32, current distribution)
    deberta_model.onnx    # DeBERTa for Sentis (FP32, int32)
    deberta_for_ort.onnx  # DeBERTa for ORT (FP32, int64) *ORT only
    style_vectors.npy
  OpenJTalkDic/
    char.bin, left-id.def, matrix.bin, pos-id.def,
    right-id.def, sys.dic, unk.dic, rewrite.def
  Tokenizer/
    vocab.json
```

### 3) (Optional) ONNX conversion

```bash
cd scripts
uv sync
uv run convert_sbv2_for_sentis.py --repo <hf-repo-id> --no-fp16 --no-simplify
```

For details, see [docs/01_onnx_export.md](docs/01_onnx_export.md).

### 4) Create `TTSSettings`

Create via `Assets > Create > uStyleBertVITS2 > TTS Settings` and assign model references.

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

Sample scene: `Assets/uStyleBertVITS2/Samples~/BasicTTS/`

## Configuration

Key `TTSSettings` fields:

| Setting | Default | Notes |
|---|---|---|
| `BertEngineType` | `Sentis` | `Sentis` / `OnnxRuntime` |
| `BertBackend` | `CPU` | Use `CPU` for Sentis BERT (GPU may cause D3D12 device lost) |
| `TTSBackend` | `GPUCompute` | GPU recommended for TTS |
| `UseDirectML` | `true` | Enables DirectML when using ORT |
| `DirectMLDeviceId` | `0` | GPU device ID |

Recommended: `BERT=OnnxRuntime(DirectML) + TTS=GPUCompute`

## Performance

Measurement environment: Windows desktop (Editor)

| Configuration | Latency |
|---|---|
| BERT=Sentis CPU + TTS=CPU | ~753ms |
| BERT=Sentis CPU + TTS=GPU (initial) | ~969ms |
| BERT=Sentis CPU + TTS=GPU (cached) | ~621ms |

For detailed benchmarks, see [docs/05_performance_optimization.md](docs/05_performance_optimization.md) and [docs/06_csharp_optimization.md](docs/06_csharp_optimization.md).

## More Docs

- Release notes: [CHANGELOG.md](CHANGELOG.md)
- Implementation roadmap: [docs/00_implementation_roadmap.md](docs/00_implementation_roadmap.md)
- ONNX conversion: [docs/01_onnx_export.md](docs/01_onnx_export.md)
- Unity/Sentis integration: [docs/03_unity_sentis_integration.md](docs/03_unity_sentis_integration.md)
- Architecture details: [docs/04_architecture_design.md](docs/04_architecture_design.md)
- Performance optimization: [docs/05_performance_optimization.md](docs/05_performance_optimization.md)
- C# optimization: [docs/06_csharp_optimization.md](docs/06_csharp_optimization.md)
- Contributing: [CONTRIBUTING.md](CONTRIBUTING.md)
- Security policy: [SECURITY.md](SECURITY.md)
- Third-party notices: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)

## License

Apache License 2.0: [LICENSE](LICENSE)  
Third-party assets (models/dictionaries) may have separate terms. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
