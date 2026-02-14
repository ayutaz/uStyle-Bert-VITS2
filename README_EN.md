# uStyle-Bert-VITS2

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-6000.3+-black.svg)](https://unity.com/)

[日本語](README.md)

A Unity library for real-time inference of [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) Japanese text-to-speech models. Runs ONNX-converted models via [Unity Sentis (AI Inference Engine)](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) or [ONNX Runtime + DirectML](https://github.com/asus4/onnxruntime-unity).

## Table of Contents

- [Demo](#demo)
- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Performance](#performance)
- [ONNX Conversion](#onnx-conversion)
- [Project Structure](#project-structure)
- [Limitations](#limitations)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [Security](#security)
- [Acknowledgements](#acknowledgements)
- [License](#license)

## Demo

<!-- Demo video coming soon -->

## Features

- **Pure C# implementation** — Complete pipeline from G2P (OpenJTalk P/Invoke) to DeBERTa inference and TTS synthesis, all within Unity
- **Async pipeline** — UniTask-based `SynthesizeAsync` for non-blocking speech synthesis
- **GPU inference** — SynthesizerTrn runs on GPUCompute backend for fast inference (~621ms)
- **BERT GPU inference (ORT+DirectML)** — Up to 14.6x faster BERT inference with ONNX Runtime + DirectML (multi-backend via `IBertRunner`)
- **Builder pattern** — Concise setup with `TTSPipelineBuilder`
- **LRU cache** — `CachedBertRunner` automatically caches BERT inference for identical text
- **Burst optimization** — Burst jobs for BertAlignment and audio normalization
- **Low GC pressure** — ArrayPool, dest buffer overloads, scalar buffer reuse, and unsafe MemCpy reduce ~470KB GC allocation per inference call
- **Benchmark tool** — `BertBenchmark` for comparing BERT backend performance

## Requirements

- **Unity** 6000.3.6f1 (Unity 6) or later
- **Unity AI Inference (Sentis)** 2.5.0
- **UniTask** 2.5.10+
- **ZString** 2.6.0+
- **ONNX Runtime (asus4)** 0.4.4+ (optional — required for ORT+DirectML BERT inference)
- **Platform** Windows x86_64 (OpenJTalk native plugin)

## Installation

### UPM (git URL)

Install via Unity Package Manager by adding the following to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.ustyle.bert-vits2": "https://github.com/ayutaz/uStyle-Bert-VITS2.git?path=Assets/uStyleBertVITS2"
  }
}
```

> **Note**: Sentis is resolved as a package dependency. UniTask / ZString / ONNX Runtime must be installed separately.

### Place model files

Place the following files under `Assets/StreamingAssets/uStyleBertVITS2/`:

Prebuilt assets are published on Hugging Face:  
https://huggingface.co/ayousanz/uStyle-Bert-VITS2

```
StreamingAssets/uStyleBertVITS2/
  Models/
    sbv2_model.onnx          # SynthesizerTrn (FP32, current distribution)
    deberta_model.onnx        # DeBERTa for Sentis (FP32, int32)
    deberta_for_ort.onnx      # DeBERTa for ORT (FP32, int64) *ORT only
    style_vectors.npy         # Style vectors
  OpenJTalkDic/               # NAIST JDIC dictionary (8 files)
    char.bin, left-id.def, matrix.bin, pos-id.def,
    right-id.def, sys.dic, unk.dic, rewrite.def
  Tokenizer/
    vocab.json                # DeBERTa vocabulary
```

> Model files can be converted and downloaded from HuggingFace using `scripts/convert_sbv2_for_sentis.py`.
> See [ONNX Conversion](#onnx-conversion) for details.
>
> ```bash
> cd scripts && uv sync
> uv run convert_sbv2_for_sentis.py --repo <hf-repo-id> --no-fp16 --no-simplify
> ```
>
> `deberta_for_ort.onnx` for ORT requires a different conversion from the Sentis version (int64 preserved, FP32 output with Cast).

### Create TTSSettings

Create a ScriptableObject via `Assets > Create > uStyleBertVITS2 > TTS Settings` and assign model asset references.

## Quick Start

```csharp
using uStyleBertVITS2.Configuration;
using uStyleBertVITS2.Services;

// Build pipeline
ITTSPipeline pipeline = new TTSPipelineBuilder()
    .WithSettings(ttsSettings)
    .Build();

// Create request
var request = new TTSRequest(
    text: "こんにちは、世界！",
    speakerId: 0,
    styleId: 0,
    sdpRatio: 0.2f,
    lengthScale: 1.0f);

// Async synthesis
// Switch BertEngineType to OnnxRuntime in TTSSettings for ORT+DirectML BERT inference
AudioClip clip = await pipeline.SynthesizeAsync(request, cancellationToken);

// Play
audioSource.clip = clip;
audioSource.Play();

// Dispose
pipeline.Dispose();
```

Sample scene is available at `Assets/uStyleBertVITS2/Samples~/BasicTTS/`. Import it from Package Manager Samples.

## Configuration

Configure BERT and TTS backends independently via `TTSSettings` ScriptableObject:

| Setting | Default | Description |
|---|---|---|
| `BertEngineType` | `Sentis` | BERT inference engine (`Sentis` / `OnnxRuntime`) |
| `BertBackend` | `CPU` | Backend for Sentis. **CPU required** (FP32 GPU causes D3D12 device lost) |
| `TTSBackend` | `GPUCompute` | SynthesizerTrn inference. GPU recommended |
| `UseDirectML` | `true` | Enable DirectML (GPU) when using ORT |
| `DirectMLDeviceId` | `0` | DirectML device ID (0 = default GPU) |

> **Recommended**: BERT=ORT DirectML + TTS=GPUCompute for fastest inference. Falls back to BERT=Sentis CPU + TTS=GPUCompute when ORT is unavailable.

## Architecture

8-stage inference pipeline:

```
Text ─→ [G2P] ─→ [add_blank] ─→ [Tokenize] ─→ [BERT] ─→ [Alignment] ─→ [StyleVector] ─→ [TTS] ─→ AudioClip
         │           │               │             │           │               │              │
    OpenJTalk   PhonemeUtils    SBV2Tokenizer  IBertRunner BertAligner  StyleVectorProvider  SBV2ModelRunner
    P/Invoke    Intersperse     (DeBERTa)      ├BertRunner  word2ph      npy lookup          (Sentis)
                                               │ (Sentis CPU)
                                               └OnnxRuntimeBertRunner
                                                 (ORT+DirectML)
```

| Stage | Description | Thread |
|---|---|---|
| G2P | Japanese text → phoneme IDs + tones + word2ph | ThreadPool |
| add_blank | Insert blank(0) tokens (N → 2N+1) | ThreadPool |
| Tokenize | DeBERTa character tokenization | ThreadPool |
| BERT | DeBERTa inference → 1024-dim embeddings | Main (Sentis CPU) or DirectML (ORT GPU) |
| Alignment | Expand BERT output to phoneme sequence via word2ph | ThreadPool |
| StyleVector | Lookup from style_vectors.npy | ThreadPool |
| TTS | SynthesizerTrn inference → audio waveform | Main (GPU) |
| AudioClip | Normalize + silence trim → AudioClip | Main |

## Performance

Measured on Windows desktop:

| Configuration | Latency |
|---|---|
| BERT=CPU + TTS=CPU | ~753ms |
| BERT=CPU + TTS=GPU (initial) | ~969ms |
| BERT=CPU + TTS=GPU (cached) | ~621ms |

> Running DeBERTa (FP32) on GPUCompute with Sentis causes D3D12 device lost. CPU backend is required for Sentis. Use ORT+DirectML for GPU BERT inference.

### BERT Backend Benchmark

Measured on RTX 4070 Ti SUPER, Editor:

| Input Size | Sentis CPU | ORT DirectML | ORT CPU | DirectML Speedup |
|---|---|---|---|---|
| 5 tokens | ~965 ms | ~66 ms | ~440 ms | 14.6x |
| 20 tokens | ~829 ms | ~285 ms | ~468 ms | 2.9x |
| 40 tokens | ~898 ms | ~266 ms | ~461 ms | 3.4x |

### GC Optimization

Reduces ~470KB of GC allocation per inference call. See [Performance Optimization](docs/05_performance_optimization.md) for details.

## ONNX Conversion

Conversion scripts for generating Sentis-compatible ONNX models are available in the `scripts/` directory.

```bash
# Set up Python environment (uv recommended)
cd scripts
uv sync

# Batch conversion from HuggingFace model
uv run convert_sbv2_for_sentis.py --repo <hf-repo-id> --no-fp16 --no-simplify

# Individual conversion
uv run convert_for_sentis.py <model_path>          # SynthesizerTrn
uv run convert_bert_for_sentis.py <deberta_path>    # DeBERTa

# Validation
uv run validate_onnx.py <onnx_path>
```

> For detailed conversion notes, see [ONNX Export Guide](docs/01_onnx_export.md).

## Project Structure

```
Assets/uStyleBertVITS2/
  Runtime/
    Core/
      Inference/         # IBertRunner, BertRunner, OnnxRuntimeBertRunner, SBV2ModelRunner, CachedBertRunner, BertBenchmark
      TextProcessing/    # JapaneseG2P, SBV2Tokenizer, BertAligner, PhonemeUtils
      Audio/             # AudioClip generation, Burst normalization job
      Configuration/     # TTSSettings (ScriptableObject)
      Services/          # TTSPipeline, TTSPipelineBuilder, TTSRequestQueue
      Data/              # NpyReader, StyleVectorProvider, LRUCache
      Native/            # OpenJTalk P/Invoke
      Diagnostics/       # TTSDebugLog
  Editor/                # Custom Inspector, Import Validator, OrtDirectMLPostProcessBuild
  Tests/                 # Runtime & Editor tests (21 files, 159+ tests)
  Plugins/               # openjtalk_wrapper.dll, onnxruntime.dll (DirectML), DirectML.dll (Windows x86_64)
  Samples~/              # BasicTTS demo scene
scripts/                 # Python ONNX conversion scripts
docs/                    # Detailed design documents
```

The `docs/` directory contains [detailed documentation](docs/) covering the implementation roadmap, ONNX export specification, architecture design, and more.

## Limitations

- **Windows x86_64 only** — Depends on OpenJTalk native plugin (macOS / Linux not supported)
- **Japanese speech synthesis only** — Uses JP-Extra models
- **Sentis BERT requires CPU** — Running DeBERTa FP32 on GPUCompute causes D3D12 device lost (GPU inference available via ORT+DirectML)
- **Sentis 2.5.0 FP16 limitation** — DeBERTa FP16 quantization not supported

## Troubleshooting

### D3D12 Device Lost Error
Set `TTSSettings > BertBackend` to `CPU`, or switch `BertEngineType` to `OnnxRuntime`.

### EntryPointNotFoundException (DirectML)
Verify that `onnxruntime.dll` (DirectML version), `onnxruntime_providers_shared.dll`, and `DirectML.dll` are present in `Plugins/Windows/x86_64/`. If missing, ORT will automatically fall back to CPU.

### Model Load Failure
Verify that model files are correctly placed under `Assets/StreamingAssets/uStyleBertVITS2/Models/`.

### OpenJTalk P/Invoke Error
Ensure you are running on Windows x86_64. macOS / Linux are not supported.

## Contributing

Issues and Pull Requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.  
Please report bugs and feature requests via [GitHub Issues](https://github.com/ayutaz/uStyle-Bert-VITS2/issues).

## Security

For vulnerability reporting policy, see [SECURITY.md](SECURITY.md).

## Acknowledgements

- [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — Original model and training code
- [Bert-VITS2](https://github.com/fishaudio/Bert-VITS2) — ONNX inference reference implementation
- [sbv2-api](https://github.com/neodyland/sbv2-api) — Rust ONNX inference implementation
- [uPiper](https://github.com/Macoron/uPiper) — OpenJTalk native plugin
- [UniTask](https://github.com/Cysharp/UniTask) — Async library for Unity
- [ZString](https://github.com/Cysharp/ZString) — Zero-allocation string formatting
- [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) — AI inference engine
- [ONNX Runtime](https://onnxruntime.ai/) — Cross-platform ML inference engine
- [onnxruntime-unity (asus4)](https://github.com/asus4/onnxruntime-unity) — ONNX Runtime Unity plugin
- [DirectML](https://github.com/microsoft/DirectML) — Windows GPU acceleration

## License

Apache License 2.0 — See [LICENSE](LICENSE) for details.

Third-party assets (models/dictionaries) may have separate licenses and terms. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for details.
