# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

Style-Bert-VITS2（日本語TTS）のモデルをONNXに変換し、Unity Sentis（AI Inference Engine）でリアルタイム推論するプロジェクト。

- **Unity バージョン**: 6000.3.6f1 (Unity 6)
- **推論エンジン**: Unity AI Inference (Sentis) 2.5.0 (`com.unity.ai.inference`)
- **元モデル**: [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — VITS2ベースの日本語音声合成
- **言語**: C# (Unity側), Python (ONNX変換側)

## セットアップ

Sentisパッケージの追加（まだ`manifest.json`に未追加）:
```
Window > Package Manager > Add package by name > com.unity.ai.inference > 2.5.0
```

## アーキテクチャ: Style-Bert-VITS2 推論パイプライン

推論は以下の3段階に分かれる:

### 1. 前処理（テキスト→特徴量）
- **G2P (Grapheme-to-Phoneme)**: 日本語テキスト→音素列+アクセント(tone)変換。Python側では`pyopenjtalk`等を使用。C#で再実装 or 事前変換が必要
- **DeBERTa (ja_bert)**: テキスト→1024次元の文脈埋め込み `[1, 1024, seq_len]`。モデルが大きい(DeBERTa-v2)ためランタイム実行はコストが高い
- **スタイルベクトル**: `style_vectors.npy`から256次元ベクトルをルックアップ `[1, 256]`

### 2. メイン合成モデル (SynthesizerTrn)
6つのサブモジュールで構成。それぞれ別ONNXとしてエクスポート可能:

| サブモデル | 役割 |
|---|---|
| `enc_p` (TextEncoder) | 音素+BERT埋め込み→潜在表現 |
| `dp` (DurationPredictor) | 確定的な音素継続長予測 |
| `sdp` (StochasticDurationPredictor) | 確率的な継続長予測 |
| `flow` (NormalizingFlow) | 潜在空間変換（韻律） |
| `dec` (Decoder/HiFi-GAN) | 潜在表現→音声波形 |
| `emb_g` (SpeakerEmbedding) | 話者ID→埋め込みベクトル |

### 3. 出力
- float32 PCM音声波形（通常44100Hz）

## SynthesizerTrn.infer() の入力テンソル

| 入力名 | 型 | Shape | 説明 |
|---|---|---|---|
| `x` | int64 | `[1, seq_len]` | 音素トークンID |
| `x_lengths` | int64 | `[1]` | 音素列の長さ |
| `tone` | int64 | `[1, seq_len]` | トーン/アクセント |
| `language` | int64 | `[1, seq_len]` | 言語ID |
| `ja_bert` | float32 | `[1, 1024, seq_len]` | 日本語BERT埋め込み |
| `bert` | float32 | `[1, 1024, seq_len]` | 中国語BERT（日本語のみなら零テンソル） |
| `en_bert` | float32 | `[1, 1024, seq_len]` | 英語BERT（日本語のみなら零テンソル） |
| `sid` | int64 | `[1]` | 話者ID |
| `style_vec` | float32 | `[1, 256]` | スタイルベクトル |
| `sdp_ratio` | float | scalar | SDP/DP混合比 (0.0-1.0) |
| `noise_scale` | float | scalar | 生成ノイズ (default 0.667) |
| `noise_scale_w` | float | scalar | 継続長ノイズ |
| `length_scale` | float | scalar | 話速倍率 |

## ONNX変換

元リポジトリに変換スクリプトあり:
- `convert_onnx.py` — メイン合成モデル
- `convert_bert_onnx.py` — DeBERTaモデル（FP16対応）

### 変換時の注意点
- **opset 15を指定すること** — Sentisはopset 7-15をサポート（opset 16+は非対応）
- **FP16推奨** — メモリ使用量が半減、品質劣化はほぼなし
- **サブモデル分割を推奨** — 1つの巨大ONNXより、enc_p/dp/sdp/flow/decに分割した方がSentisでの互換性が高い
- 参考: [Bert-VITS2 onnx_infer.py](https://github.com/fishaudio/Bert-VITS2/blob/master/onnx_infer.py) は6分割エクスポートの実装

## Unity Sentis (AI Inference) の制約

### 基本API
```csharp
using Unity.Sentis;

Model model = ModelLoader.Load(modelAsset);
Worker worker = new Worker(model, BackendType.GPUCompute);
worker.SetInput("input_name", tensor);
worker.Schedule();
Tensor<float> output = worker.PeekOutput("output_name") as Tensor<float>;
```

### BackendType
- `GPUCompute` — 最速、DirectML使用。推奨
- `CPU` — Burstコンパイラ使用。小規模モデル向け
- `GPUPixel` — Compute Shader非対応環境用フォールバック

### 注意すべき制約
- **ConvTranspose**: `dilations`, `group`, `output_shape`パラメータ非対応 → HiFi-GANデコーダの転置畳み込みで問題になる可能性あり
- **GRU/RNN非対応**: VITS2はTransformer/Conv主体なので基本問題なし
- **制御フロー(Loop, If)非対応**: SDPの条件分岐はエクスポート時に除去する必要あり
- **動的シェイプ**: サポートされるが最適化は制限される。`seq_len`は可変
- **テンソル最大8次元**

## 実装戦略の選択肢

### G2P（テキスト→音素変換）
1. **C#で再実装**: OpenJTalkの辞書を移植。最も柔軟だが工数大
2. **事前変換テーブル**: よく使うフレーズを事前にPythonで変換しJSONで保持
3. **ローカルAPIサーバー**: Python側でG2P+BERTを処理しUnityから呼び出し

### BERT埋め込み
1. **ランタイム推論**: DeBERTa ONNXをSentisで実行（重い、300M+パラメータ）
2. **事前計算**: Pythonで事前にBERT埋め込みを計算し`.npy`等で保存
3. **BERT省略**: 零テンソルで代替（品質は低下するが動作は可能）

## ディレクトリ構成（推奨）

```
Assets/
  Models/              # ONNXモデルファイル (.onnx)
  Scripts/
    Inference/         # Sentis推論ラッパー
    TextProcessing/    # G2P、トークナイザ
    Audio/             # AudioClip生成、再生
  Data/
    StyleVectors/      # style_vectors.npy
    Dictionaries/      # G2P辞書データ
  Scenes/
    SampleScene.unity  # テストシーン
```

## 参考リンク

- [Style-Bert-VITS2](https://github.com/litagin02/Style-Bert-VITS2) — 元リポジトリ
- [Bert-VITS2 ONNX推論](https://github.com/fishaudio/Bert-VITS2/blob/master/onnx_infer.py) — 6分割ONNXの参考実装
- [sbv2-api (Rust実装)](https://github.com/neodyland/sbv2-api) — Python外でのONNX推論の実例
- [Unity Sentis ドキュメント](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html)
- [Sentis対応オペレータ一覧](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/supported-operators.html)
