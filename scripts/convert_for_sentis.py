"""
Style-Bert-VITS2 SynthesizerTrn ONNX変換スクリプト (Unity Sentis向け)

処理フロー:
1. SBV2モデル (.safetensors + config.json) をロード
2. torch.onnx.export() で opset 15 エクスポート
3. onnxsim.simplify() でグラフ簡略化
4. int64→int32 キャスト (Sentis互換)
5. FP16変換 (keep_io_types=True)

使用方法:
    uv run python convert_for_sentis.py \
        --model-dir /path/to/sbv2_model/ \
        --output sbv2_model_fp16.onnx
"""

import argparse
from pathlib import Path

import numpy as np
import onnx
from onnxconverter_common import float16
from onnxsim import simplify


def _convert_tensor_int64_to_int32(tensor: onnx.TensorProto) -> None:
    """TensorProto 内の int64 データを int32 に変換する (in-place)。"""
    if tensor.data_type != onnx.TensorProto.INT64:
        return
    if tensor.raw_data:
        data = np.frombuffer(tensor.raw_data, dtype=np.int64).astype(np.int32)
        tensor.raw_data = data.tobytes()
    elif tensor.int64_data:
        data = np.array(tensor.int64_data, dtype=np.int64).astype(np.int32)
        del tensor.int64_data[:]
        tensor.int32_data.extend(data.tolist())
    tensor.data_type = onnx.TensorProto.INT32


def convert_int64_to_int32(model: onnx.ModelProto) -> onnx.ModelProto:
    """ONNX モデル内の全ての int64 テンソルを int32 に変換する。

    Unity Sentis は int64 をサポートしないため、全ての int64 を int32 に統一する。
    これは ONNX 仕様上は axes/shape 入力が int64 を要求するが、
    Sentis は全て int32 で処理するため問題ない。
    ※ onnxruntime での検証には変換前のモデルを使うこと。

    変換対象:
    - グラフ入力/出力の型宣言
    - 初期化テンソル (weights)
    - Constant ノードの value 属性
    - Cast ノードの to=INT64 を to=INT32 に
    - 中間テンソルの value_info
    """
    count = 0

    # グラフ入力
    for input_tensor in model.graph.input:
        if input_tensor.type.tensor_type.elem_type == onnx.TensorProto.INT64:
            input_tensor.type.tensor_type.elem_type = onnx.TensorProto.INT32

    # グラフ出力
    for output_tensor in model.graph.output:
        if output_tensor.type.tensor_type.elem_type == onnx.TensorProto.INT64:
            output_tensor.type.tensor_type.elem_type = onnx.TensorProto.INT32

    # 初期化テンソル (weights)
    for initializer in model.graph.initializer:
        if initializer.data_type == onnx.TensorProto.INT64:
            _convert_tensor_int64_to_int32(initializer)
            count += 1

    # Constant ノードの value 属性
    for node in model.graph.node:
        if node.op_type == "Constant":
            for attr in node.attribute:
                if attr.name == "value" and attr.t.data_type == onnx.TensorProto.INT64:
                    _convert_tensor_int64_to_int32(attr.t)
                    count += 1

    # Cast ノードの to=INT64 を to=INT32 に変更
    for node in model.graph.node:
        if node.op_type == "Cast":
            for attr in node.attribute:
                if attr.name == "to" and attr.i == onnx.TensorProto.INT64:
                    attr.i = onnx.TensorProto.INT32
                    count += 1

    # 中間テンソルの value_info
    for vi in model.graph.value_info:
        if vi.type.tensor_type.elem_type == onnx.TensorProto.INT64:
            vi.type.tensor_type.elem_type = onnx.TensorProto.INT32

    print(f"  Converted {count} int64 tensors/nodes to int32")
    return model


def main():
    parser = argparse.ArgumentParser(
        description="Convert Style-Bert-VITS2 SynthesizerTrn to Sentis-compatible ONNX"
    )
    parser.add_argument(
        "--input", type=str, required=True, help="Input ONNX model path"
    )
    parser.add_argument(
        "--output",
        type=str,
        default="sbv2_model_fp16.onnx",
        help="Output ONNX model path",
    )
    parser.add_argument(
        "--no-fp16", action="store_true", help="Skip FP16 conversion"
    )
    parser.add_argument(
        "--no-simplify", action="store_true", help="Skip onnxsim simplification"
    )
    args = parser.parse_args()

    print(f"Loading ONNX model: {args.input}")
    model = onnx.load(args.input)

    # 1. 簡略化
    if not args.no_simplify:
        print("Simplifying model with onnxsim...")
        model, check = simplify(model)
        if not check:
            print("Warning: onnxsim simplification check failed")

    # 2. int64→int32 変換
    print("Converting int64 → int32...")
    model = convert_int64_to_int32(model)

    # 3. FP16変換
    if not args.no_fp16:
        print("Converting to FP16 (keep_io_types=True)...")
        model = float16.convert_float_to_float16(model, keep_io_types=True)

    # 4. 保存
    output_path = Path(args.output)
    print(f"Saving to: {output_path}")
    onnx.save(model, str(output_path))
    print(f"Done! Model size: {output_path.stat().st_size / 1024 / 1024:.1f} MB")


if __name__ == "__main__":
    main()
