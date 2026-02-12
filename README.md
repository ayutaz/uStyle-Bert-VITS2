# uStyle-Bert-VITS2

[Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) の日本語音声合成モデルを Unity 上でリアルタイム推論するライブラリです。ONNX に変換したモデルを [Unity Sentis (AI Inference Engine)](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) で実行します。

## Features

- **完全な C# 実装** — G2P (OpenJTalk P/Invoke) からDeBERTa推論、TTS合成まで Unity 内で完結
- **非同期パイプライン** — UniTask ベースの `SynthesizeAsync` でメインスレッドをブロックしない音声合成
- **GPU 推論** — SynthesizerTrn は GPUCompute バックエンドで高速推論（~621ms）
- **Builder パターン** — `TTSPipelineBuilder` による簡潔なセットアップ
- **LRU キャッシュ** — `CachedBertRunner` で同一テキストの BERT 推論を自動キャッシュ
- **Burst 最適化** — BertAlignment と音声正規化に Burst ジョブを活用
- **GC 圧力削減** — ArrayPool、dest バッファオーバーロード、スカラーバッファ再利用、unsafe MemCpy で推論1回あたり ~470KB の GC アロケーション削減

## Requirements

- **Unity** 6000.3.6f1 (Unity 6) 以降
- **Unity AI Inference (Sentis)** 2.5.0
- **UniTask** 2.5.10+
- **ZString** 2.6.0+
- **Platform** Windows x86_64（OpenJTalk ネイティブプラグイン）

## Installation

### 1. Unity プロジェクトを開く

Unity Hub から Unity 6 (6000.3.6f1+) でプロジェクトを開きます。依存パッケージ（Sentis, UniTask, ZString）は `manifest.json` で自動解決されます。

### 2. モデルファイルを配置

以下のファイルを `Assets/StreamingAssets/uStyleBertVITS2/` に配置してください:

```
StreamingAssets/uStyleBertVITS2/
  Models/
    sbv2_model.onnx          # SynthesizerTrn (FP16推奨)
    deberta_model.onnx        # DeBERTa (FP32必須)
  StyleVectors/
    style_vectors.npy         # スタイルベクトル
  OpenJTalkDic/               # NAIST JDIC辞書 (8ファイル)
    char.bin, left-id.def, matrix.bin, pos-id.def,
    right-id.def, sys.dic, unk.dic, rewrite.def
  Tokenizer/
    vocab.json                # DeBERTa語彙ファイル
```

> モデルファイルはサイズが大きいため Git 管理外です。ONNX 変換方法は後述の [ONNX 変換](#onnx-変換) を参照してください。

### 3. TTSSettings を作成

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
AudioClip clip = await pipeline.SynthesizeAsync(request, cancellationToken);

// 再生
audioSource.clip = clip;
audioSource.Play();

// 解放
pipeline.Dispose();
```

サンプルシーンは `Assets/uStyleBertVITS2/Samples~/BasicTTS/` にあります。Package Manager の Samples からインポートできます。

## Architecture

8 ステージの推論パイプライン:

```
Text ─→ [G2P] ─→ [add_blank] ─→ [Tokenize] ─→ [BERT] ─→ [Alignment] ─→ [StyleVector] ─→ [TTS] ─→ AudioClip
         │           │               │             │           │               │              │
    OpenJTalk   PhonemeUtils    SBV2Tokenizer  BertRunner  BertAligner  StyleVectorProvider  SBV2ModelRunner
    P/Invoke    Intersperse     (DeBERTa)      (Sentis)    word2ph展開   npy lookup          (Sentis)
```

| Stage | Description | Thread |
|---|---|---|
| G2P | 日本語テキスト → 音素ID + トーン + word2ph | ThreadPool |
| add_blank | blank(0) トークン挿入 (N → 2N+1) | ThreadPool |
| Tokenize | DeBERTa 文字トークナイズ | ThreadPool |
| BERT | DeBERTa 推論 → 1024次元埋め込み | Main (CPU) |
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

> DeBERTa (FP32) を GPUCompute で実行すると D3D12 デバイスロストが発生するため、BERT は CPU バックエンド必須です。

### GC 最適化

| 最適化 | 効果 |
|--------|------|
| BertRunner dest オーバーロード | BERT 出力バッファの再利用 (~32KB/call) |
| SBV2ModelRunner unsafe MemCpy | BERT パディング 2.0x 高速化 |
| SBV2ModelRunner スカラーバッファ再利用 | 6 個のスカラー配列アロケーション除去 |
| TTSPipeline ArrayPool | bertData + alignedBert のプーリング (~250KB/call) |
| GetTrimmedLength (in-place) | 末尾無音トリムの配列コピー除去 (~186KB/call) |
| **合計** | **~470KB/call の GC 圧力削減** |

## ONNX 変換

`scripts/` ディレクトリに Sentis 互換の ONNX を生成する変換スクリプトがあります。

```bash
# Python環境セットアップ (uv推奨)
cd scripts
uv sync

# HuggingFace モデルからの一括変換
uv run convert_sbv2_for_sentis.py --repo-id <hf-repo-id> --no-fp16 --no-simplify

# 個別変換
uv run convert_for_sentis.py <model_path>          # SynthesizerTrn
uv run convert_bert_for_sentis.py <deberta_path>    # DeBERTa

# 検証
uv run validate_onnx.py <onnx_path>
```

### 変換の注意点

- **opset 15** — Sentis は opset 7-15 をサポート
- **SBV2 は FP16 推奨** — メモリ半減、品質劣化なし
- **DeBERTa は FP32 必須** — Sentis 2.5.0 は FP16 定数テンソルをインポート不可
- **int64 → int32 変換** — Sentis は int32 のみ対応。変換スクリプトが自動処理

## Project Structure

```
Assets/uStyleBertVITS2/
  Runtime/
    Core/
      Inference/         # BertRunner, SBV2ModelRunner, CachedBertRunner
      TextProcessing/    # JapaneseG2P, SBV2Tokenizer, BertAligner, PhonemeUtils
      Audio/             # AudioClip生成, Burst正規化ジョブ
      Configuration/     # TTSSettings (ScriptableObject)
      Services/          # TTSPipeline, TTSPipelineBuilder, TTSRequestQueue
      Data/              # NpyReader, StyleVectorProvider, LRUCache
      Native/            # OpenJTalk P/Invoke
      Diagnostics/       # TTSDebugLog
  Editor/                # Custom Inspector, Import Validator
  Tests/                 # Runtime & Editor テスト (18 files, 145 tests)
  Plugins/               # openjtalk_wrapper.dll (Windows x86_64)
  Samples~/              # BasicTTS デモシーン
scripts/                 # Python ONNX変換スクリプト
docs/                    # 詳細な設計ドキュメント (8 files)
```

## Documentation

`docs/` ディレクトリに詳細なドキュメントがあります:

| File | Description |
|---|---|
| [00_implementation_roadmap.md](docs/00_implementation_roadmap.md) | 実装ロードマップと進捗 |
| [01_onnx_export.md](docs/01_onnx_export.md) | ONNX エクスポート仕様 |
| [02_g2p_implementation.md](docs/02_g2p_implementation.md) | G2P 実装詳細 |
| [03_unity_sentis_integration.md](docs/03_unity_sentis_integration.md) | Sentis 統合ガイド |
| [04_architecture_design.md](docs/04_architecture_design.md) | アーキテクチャ設計 |
| [05_performance_optimization.md](docs/05_performance_optimization.md) | パフォーマンス最適化 |
| [06_csharp_optimization.md](docs/06_csharp_optimization.md) | C# 最適化テクニック |
| [07_cysharp_libraries.md](docs/07_cysharp_libraries.md) | Cysharp ライブラリ活用 |

## Acknowledgements

- [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — 元モデル・学習コード
- [Bert-VITS2](https://github.com/fishaudio/Bert-VITS2) — ONNX 推論の参考実装
- [sbv2-api](https://github.com/neodyland/sbv2-api) — Rust による ONNX 推論実装
- [uPiper](https://github.com/Macoron/uPiper) — OpenJTalk ネイティブプラグイン
- [UniTask](https://github.com/Cysharp/UniTask) — Unity 向け非同期ライブラリ
- [ZString](https://github.com/Cysharp/ZString) — ゼロアロケーション文字列フォーマット
- [Unity Sentis](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html) — AI 推論エンジン

## License

This project is for personal/research use. The original [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) model and weights are subject to their respective licenses.
