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


def convert_int64_to_int32(model: onnx.ModelProto) -> onnx.ModelProto:
    """ONNX モデル内の int64 テンソルを int32 に変換する。"""
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
            if initializer.raw_data:
                data = np.frombuffer(initializer.raw_data, dtype=np.int64).astype(np.int32)
                initializer.raw_data = data.tobytes()
            elif initializer.int64_data:
                data = np.array(initializer.int64_data, dtype=np.int64).astype(np.int32)
                initializer.int64_data[:] = []
                initializer.int32_data.extend(data.tolist())
            initializer.data_type = onnx.TensorProto.INT32

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
