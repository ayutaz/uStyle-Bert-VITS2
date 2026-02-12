"""
DeBERTa (ku-nlp/deberta-v2-large-japanese-char-wwm) ONNX変換スクリプト

処理フロー:
1. HuggingFace から DeBERTa モデルをロード
2. 隠れ層 -3 を選択するラッパーモデルを作成
3. torch.onnx.export() で opset 15 エクスポート
4. onnxsim 簡略化
5. int64→int32 キャスト
6. FP16変換

使用方法:
    uv run python convert_bert_for_sentis.py \
        --output deberta_fp16.onnx
"""

import argparse
from pathlib import Path

import numpy as np
import onnx
import torch
import torch.nn as nn
from onnxconverter_common import float16
from onnxsim import simplify
from transformers import AutoModel

from convert_for_sentis import convert_int64_to_int32


class DeBERTaWrapper(nn.Module):
    """
    DeBERTaの隠れ層 -3 を選択し[batch, 1024, token_len]形式で出力するラッパー。
    Style-Bert-VITS2のBERT埋め込み仕様に準拠（bert_feature.py:61 と同じ層を使用）。
    """

    def __init__(self, deberta_model):
        super().__init__()
        self.model = deberta_model

    def forward(self, input_ids, token_type_ids, attention_mask):
        outputs = self.model(
            input_ids=input_ids,
            token_type_ids=token_type_ids,
            attention_mask=attention_mask,
            output_hidden_states=True,
        )
        hidden_states = outputs.hidden_states
        # 隠れ層 -3 のみ (Python SBV2 bert_feature.py と同じ)
        result = hidden_states[-3]
        return result.transpose(1, 2)  # [batch, 1024, token_len]


def main():
    parser = argparse.ArgumentParser(
        description="Convert DeBERTa to Sentis-compatible ONNX"
    )
    parser.add_argument(
        "--model-name",
        type=str,
        default="ku-nlp/deberta-v2-large-japanese-char-wwm",
        help="HuggingFace model name",
    )
    parser.add_argument(
        "--output",
        type=str,
        default="deberta_fp16.onnx",
        help="Output ONNX model path",
    )
    parser.add_argument(
        "--no-fp16", action="store_true", help="Skip FP16 conversion"
    )
    parser.add_argument(
        "--seq-len", type=int, default=128, help="Dummy sequence length for export"
    )
    parser.add_argument(
        "--no-dynamic",
        action="store_true",
        help="Export with fixed sequence length (no dynamic axes)",
    )
    parser.add_argument(
        "--no-int32",
        action="store_true",
        help="Skip int64→int32 conversion (for ONNX Runtime which supports int64 natively)",
    )
    parser.add_argument(
        "--no-simplify",
        action="store_true",
        help="Skip onnxsim simplification",
    )
    args = parser.parse_args()

    print(f"Loading model: {args.model_name}")
    base_model = AutoModel.from_pretrained(args.model_name)
    base_model.eval()

    wrapper = DeBERTaWrapper(base_model)
    wrapper.eval()

    # ダミー入力
    seq_len = args.seq_len
    dummy_input_ids = torch.ones(1, seq_len, dtype=torch.long)
    dummy_token_type_ids = torch.zeros(1, seq_len, dtype=torch.long)
    dummy_attention_mask = torch.ones(1, seq_len, dtype=torch.long)

    # ONNX エクスポート
    temp_path = "deberta_temp.onnx"
    dynamic_axes = (
        None
        if args.no_dynamic
        else {
            "input_ids": {1: "token_len"},
            "token_type_ids": {1: "token_len"},
            "attention_mask": {1: "token_len"},
            "output": {2: "token_len"},
        }
    )
    print(f"Exporting ONNX (opset 15, dynamic={not args.no_dynamic})...")
    torch.onnx.export(
        wrapper,
        (dummy_input_ids, dummy_token_type_ids, dummy_attention_mask),
        temp_path,
        opset_version=15,
        dynamo=False,
        input_names=["input_ids", "token_type_ids", "attention_mask"],
        output_names=["output"],
        dynamic_axes=dynamic_axes,
    )

    # 後処理
    print("Loading exported ONNX...")
    model = onnx.load(temp_path)

    if not args.no_simplify:
        print("Simplifying...")
        model, check = simplify(model)
    else:
        print("Skipping simplification")

    if not args.no_int32:
        print("Converting int64 → int32...")
        model = convert_int64_to_int32(model)
    else:
        print("Skipping int64 → int32 conversion (ORT mode)")

    if not args.no_fp16:
        print("Converting to FP16...")
        try:
            model = float16.convert_float_to_float16(model, keep_io_types=True)
        except ValueError:
            # onnxsim may partially convert to fp16, retry with check disabled
            model = float16.convert_float_to_float16(
                model, keep_io_types=True, check_fp16_ready=False
            )

    # 出力が FP16 の場合、FP32 にキャストするノードを追加 (ORT 互換)
    output_type = model.graph.output[0].type.tensor_type.elem_type
    if output_type == onnx.TensorProto.FLOAT16:
        print("Output is FP16 -- appending Cast to FP32...")
        old_output = model.graph.output[0]
        old_name = old_output.name
        intermediate_name = old_name + "_fp16"

        # 既存の出力ノードを中間名にリネーム
        for node in model.graph.node:
            for i, out in enumerate(node.output):
                if out == old_name:
                    node.output[i] = intermediate_name

        # Cast ノード追加
        cast_node = onnx.helper.make_node(
            "Cast", inputs=[intermediate_name], outputs=[old_name],
            to=onnx.TensorProto.FLOAT,
        )
        model.graph.node.append(cast_node)

        # 出力の型を FP32 に更新
        old_output.type.tensor_type.elem_type = onnx.TensorProto.FLOAT

    output_path = Path(args.output)
    print(f"Saving to: {output_path}")
    onnx.save(model, str(output_path))
    print(f"Done! Model size: {output_path.stat().st_size / 1024 / 1024:.1f} MB")

    # 一時ファイル削除
    Path(temp_path).unlink(missing_ok=True)


if __name__ == "__main__":
    main()
