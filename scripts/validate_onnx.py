"""
ONNX モデル検証スクリプト。
OnnxRuntime でダミー推論を実行し、出力shape・値域を確認する。

使用方法:
    uv run python validate_onnx.py --model sbv2_model_fp16.onnx --type tts
    uv run python validate_onnx.py --model deberta_fp16.onnx --type bert
"""

import argparse

import numpy as np
import onnxruntime as ort


def validate_bert(model_path: str):
    """DeBERTa ONNX の検証。"""
    print(f"Loading BERT model: {model_path}")
    session = ort.InferenceSession(model_path)

    # 入力情報表示
    print("\nInputs:")
    for inp in session.get_inputs():
        print(f"  {inp.name}: {inp.type} {inp.shape}")

    print("\nOutputs:")
    for out in session.get_outputs():
        print(f"  {out.name}: {out.type} {out.shape}")

    # ダミー推論
    token_len = 10
    input_ids = np.ones((1, token_len), dtype=np.int32)
    token_type_ids = np.zeros((1, token_len), dtype=np.int32)
    attention_mask = np.ones((1, token_len), dtype=np.int32)

    print(f"\nRunning dummy inference (token_len={token_len})...")
    outputs = session.run(
        None,
        {
            "input_ids": input_ids,
            "token_type_ids": token_type_ids,
            "attention_mask": attention_mask,
        },
    )

    output = outputs[0]
    print(f"Output shape: {output.shape}")
    print(f"Output dtype: {output.dtype}")
    print(f"Output range: [{output.min():.4f}, {output.max():.4f}]")
    print(f"Output mean: {output.mean():.4f}")

    expected_shape = (1, 1024, token_len)
    assert output.shape == expected_shape, (
        f"Expected shape {expected_shape}, got {output.shape}"
    )
    print("\n✓ BERT model validation passed!")


def validate_tts(model_path: str):
    """SynthesizerTrn ONNX の検証。"""
    print(f"Loading TTS model: {model_path}")
    session = ort.InferenceSession(model_path)

    # 入力情報表示
    print("\nInputs:")
    for inp in session.get_inputs():
        print(f"  {inp.name}: {inp.type} {inp.shape}")

    print("\nOutputs:")
    for out in session.get_outputs():
        print(f"  {out.name}: {out.type} {out.shape}")

    # ダミー推論
    seq_len = 10
    feeds = {
        "x_tst": np.array([[0, 1, 2, 3, 4, 5, 6, 7, 8, 9]], dtype=np.int32),
        "x_tst_lengths": np.array([seq_len], dtype=np.int32),
        "tones": np.zeros((1, seq_len), dtype=np.int32),
        "language": np.ones((1, seq_len), dtype=np.int32),
        "bert": np.zeros((1, 1024, seq_len), dtype=np.float32),
        "style_vec": np.zeros((1, 256), dtype=np.float32),
        "sid": np.array([0], dtype=np.int32),
        "sdp_ratio": np.array([0.2], dtype=np.float32),
        "noise_scale": np.array([0.6], dtype=np.float32),
        "noise_scale_w": np.array([0.8], dtype=np.float32),
        "length_scale": np.array([1.0], dtype=np.float32),
    }

    print(f"\nRunning dummy inference (seq_len={seq_len})...")
    outputs = session.run(None, feeds)

    output = outputs[0]
    print(f"Output shape: {output.shape}")
    print(f"Output dtype: {output.dtype}")
    print(f"Output range: [{output.min():.4f}, {output.max():.4f}]")
    print(f"Audio samples: {output.shape[-1]}")
    print(f"Audio duration: {output.shape[-1] / 44100:.2f}s")

    assert output.ndim == 3, f"Expected 3D output, got {output.ndim}D"
    assert output.shape[0] == 1 and output.shape[1] == 1, (
        f"Expected [1,1,N], got {output.shape}"
    )
    print("\n✓ TTS model validation passed!")


def main():
    parser = argparse.ArgumentParser(description="Validate ONNX model for Sentis")
    parser.add_argument("--model", type=str, required=True, help="ONNX model path")
    parser.add_argument(
        "--type",
        type=str,
        required=True,
        choices=["bert", "tts"],
        help="Model type",
    )
    args = parser.parse_args()

    if args.type == "bert":
        validate_bert(args.model)
    else:
        validate_tts(args.model)


if __name__ == "__main__":
    main()
