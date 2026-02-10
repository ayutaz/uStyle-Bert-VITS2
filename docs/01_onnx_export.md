# ONNX変換ガイド (Style-Bert-VITS2 JP-Extra → Unity Sentis)

## 変換対象モデル

| モデル | 変換元 | 推奨精度 | 推定サイズ |
|---|---|---|---|
| SynthesizerTrn (メインTTS) | `*.safetensors` | FP16 | ~200-400MB |
| DeBERTa-v2-large-japanese | `ku-nlp/deberta-v2-large-japanese-char-wwm` | FP16 | ~600MB |

JP-Extra版のため `ja_bert` のみ使用。`en_bert` / `bert`(中国語) は不要。

---

## SynthesizerTrn — monolithic方式の入力テンソル仕様

sbv2-apiのRust実装 (`crates/sbv2_core/src/model.rs`) から確認した仕様:

| 入力名 | 型 | Shape | 説明 |
|---|---|---|---|
| `x_tst` | int64→**int32** | `[1, seq_len]` | 音素トークンID |
| `x_tst_lengths` | int64→**int32** | `[1]` | 音素列の長さ |
| `tones` | int64→**int32** | `[1, seq_len]` | トーン/アクセント値 |
| `language` | int64→**int32** | `[1, seq_len]` | 言語ID（日本語=1） |
| `bert` | float32 | `[1, 1024, seq_len]` | 日本語BERT埋め込み (ja_bert) |
| `style_vec` | float32 | `[1, 256]` | スタイルベクトル |
| `sid` | int64→**int32** | `[1]` | 話者ID |
| `sdp_ratio` | float32 | scalar | SDP/DP混合比 (0.0-1.0) |
| `length_scale` | float32 | scalar | 話速倍率 |
| `noise_scale` | float32 | scalar | 生成ノイズ (default 0.667) |
| `noise_scale_w` | float32 | scalar | 継続長ノイズ |

**出力**: `output [1, 1, audio_samples]` float32 (44100Hz PCM)

---

## DeBERTa — 入力テンソル仕様

| 入力名 | 型 | Shape | 説明 |
|---|---|---|---|
| `input_ids` | int64→**int32** | `[1, token_len]` | トークンID |
| `token_type_ids` | int64→**int32** | `[1, token_len]` | トークンタイプ（全0） |
| `attention_mask` | int64→**int32** | `[1, token_len]` | アテンションマスク（全1） |

**出力**: `output [1, 1024, token_len]` float32

DeBERTaの出力は最終3隠れ層を結合した1024次元ベクトル。これをword2phアライメントで音素列長に展開してSynthesizerTrnに渡す。

---

## Sentis互換性チェック

### 互換あり
| オペレータ | 用途 | 状況 |
|---|---|---|
| ConvTranspose1d (groups=1, dilation=1) | HiFi-GANデコーダ | **互換** |
| Conv1d with dilation [1,3,5] | WaveNet残差ブロック | **互換** |
| RandomNormal | ノイズ生成 | **互換** |
| LayerNormalization | DeBERTa、各エンコーダ | **互換** |
| Gather / GatherElements | 埋め込みルックアップ | **互換** |

### 非対応・要注意
| オペレータ | 用途 | 対策 |
|---|---|---|
| If / Loop 制御フロー | SDPの条件分岐 | `torch.jit.trace` で排除される |
| ConvTranspose (dilation>1, group>1) | 該当なし（SBV2では使わない） | — |

### opset制約の実態
- **公式ドキュメント**: opset 7-15推奨
- **実態**: uCosyVoiceはopset 14-18のモデルを全て動作させている（118テスト全パス）
- **結論**: opset 15をターゲットにしつつ、必要なら16+も使用可能

---

## int64→int32変換

SentisはTensor\<int\>(int32)のみ対応。変換スクリプトで対処:

```python
import onnx

def convert_int64_to_int32(model_path, output_path):
    model = onnx.load(model_path)

    # グラフ入力のint64→int32
    for input_tensor in model.graph.input:
        if input_tensor.type.tensor_type.elem_type == onnx.TensorProto.INT64:
            input_tensor.type.tensor_type.elem_type = onnx.TensorProto.INT32

    # 初期化テンソル(weights)のint64→int32
    for initializer in model.graph.initializer:
        if initializer.data_type == onnx.TensorProto.INT64:
            initializer.data_type = onnx.TensorProto.INT32

    # Cast/Constantノードのint64参照も変換が必要な場合あり

    onnx.save(model, output_path)
```

---

## 変換スクリプトの構成

### `scripts/convert_for_sentis.py` — メインTTSモデル変換

処理フロー:
1. Style-Bert-VITS2の `convert_onnx.py` をベースに改修
2. `torch.onnx.export()` で opset 15 を明示指定
3. `onnxsim.simplify()` でグラフ簡略化
4. int64→int32キャスト（上記スクリプト）
5. FP16変換（`onnxconverter-common` の `convert_float_to_float16()`, `keep_io_types=True`）

```python
# 概要（擬似コード）
import torch
import onnx
from onnxsim import simplify
from onnxconverter_common import float16

# 1. モデルロード
model = load_sbv2_model("model.safetensors", config="config.json")
model.eval()

# 2. ダミー入力作成
dummy_inputs = create_dummy_inputs(seq_len=10)

# 3. ONNX export (opset 15)
torch.onnx.export(
    model, dummy_inputs, "sbv2_model.onnx",
    opset_version=15,
    input_names=["x_tst", "x_tst_lengths", "tones", "language",
                 "bert", "style_vec", "sid",
                 "sdp_ratio", "length_scale", "noise_scale", "noise_scale_w"],
    output_names=["output"],
    dynamic_axes={
        "x_tst": {1: "seq_len"},
        "tones": {1: "seq_len"},
        "language": {1: "seq_len"},
        "bert": {2: "seq_len"},
        "output": {2: "audio_len"},
    }
)

# 4. 簡略化
model_onnx = onnx.load("sbv2_model.onnx")
model_simplified, check = simplify(model_onnx)

# 5. int64→int32
convert_int64_to_int32(model_simplified)

# 6. FP16変換
model_fp16 = float16.convert_float_to_float16(model_simplified, keep_io_types=True)
onnx.save(model_fp16, "sbv2_model_fp16.onnx")
```

### `scripts/convert_bert_for_sentis.py` — DeBERTa変換

処理フロー:
1. `convert_bert_onnx.py` をベースに改修
2. 最終3隠れ層を結合して1024次元出力を返すラッパーモデル
3. opset 15で export
4. onnxsim 簡略化
5. int64→int32キャスト
6. FP16変換

```python
# DeBERTaラッパー（最終3隠れ層結合）
class DeBERTaWrapper(torch.nn.Module):
    def __init__(self, deberta_model):
        super().__init__()
        self.model = deberta_model

    def forward(self, input_ids, token_type_ids, attention_mask):
        outputs = self.model(
            input_ids=input_ids,
            token_type_ids=token_type_ids,
            attention_mask=attention_mask,
            output_hidden_states=True
        )
        # 最終3隠れ層を結合 → [batch, token_len, 768*3] or [batch, token_len, 1024]
        # SBV2の実装に合わせて結合方法を確認すること
        hidden_states = outputs.hidden_states
        # 最終3層の合計/結合
        result = (hidden_states[-1] + hidden_states[-2] + hidden_states[-3]) / 3
        return result.transpose(1, 2)  # [batch, 1024, token_len]
```

---

## 変換後のファイル配置

```
Assets/
  Models/
    sbv2_model_fp16.onnx       # メインTTSモデル (~200-400MB)
    deberta_fp16.onnx          # DeBERTaモデル (~600MB)
  StreamingAssets/
    StyleVectors/
      style_vectors.npy        # スタイルベクトル
```

Unity Editorでインポートすると自動的に `.sentis` アセットに変換される（Sentis 2.5.0）。

---

## 検証方法

1. **Python側でONNXRuntime推論テスト**: 変換後のONNXをonnxruntimeで実行し、元モデルと出力を比較
2. **Unity Editorインポート**: ONNXファイルをAssetsに配置、コンソールにエラーがないか確認
3. **ModelLoader.Load()テスト**: C#側で `ModelLoader.Load()` が成功するか確認

---

## 注意事項

- **DeBERTaのサイズ**: FP16で~600MB。GPUメモリ消費大。`BackendType.GPUCompute` 失敗時は `BackendType.CPU` フォールバックを検討
- **monolithic vs 分割**: まずmonolithic（1ファイル）で試す。Sentis互換性問題が出たら enc_p/dp/sdp/flow/dec/emb_g の6分割を検討
- **FP16の`keep_io_types=True`**: 入出力はfloat32のまま、内部のみFP16にする。Sentisとのテンソル受け渡しが安定する
