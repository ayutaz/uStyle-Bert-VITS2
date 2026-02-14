# Changelog

All notable changes to this project are documented in this file.

## [0.1.0] - 2026-02-14

### Added

- Initial OSS package release for Unity (`com.ustyle.bert-vits2`).
- End-to-end C# TTS pipeline for Japanese text: G2P, tokenization, BERT embedding, alignment, style vector lookup, and SynthesizerTrn inference.
- Multi-backend BERT inference with Sentis and ONNX Runtime + DirectML (`IBertRunner` / `OnnxRuntimeBertRunner`), plus LRU caching support.
- ONNX conversion utilities in `scripts/` for Sentis-compatible model preparation.
- OSS project governance files (`LICENSE`, `CONTRIBUTING.md`, `SECURITY.md`, issue/PR templates).

### Changed

- Runtime asset layout aligned to `Assets/StreamingAssets/uStyleBertVITS2/`.
- Model distribution guidance standardized around FP32 artifacts (`sbv2_model.onnx` as current distribution).
- README and README_EN compacted for public OSS usage, with detailed content moved to `docs/`.

### Notes

- Current scope is Windows x86_64 (OpenJTalk native plugin dependency).
- For BERT GPU inference, use ONNX Runtime + DirectML. Sentis BERT should use CPU backend.
