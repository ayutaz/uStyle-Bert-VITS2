"""
Style-Bert-VITS2 モデルを Sentis 互換 ONNX に変換するスクリプト

処理フロー:
1. HuggingFace からモデルをダウンロード (safetensors + config.json + style_vectors.npy)
2. SBV2 の公式モデル定義を使って PyTorch モデルを復元
3. torch.onnx.export() でエクスポート
4. onnxsim 簡略化
5. int64→int32 キャスト (Sentis互換)
6. FP16 変換

使用方法:
    uv run python convert_sbv2_for_sentis.py \
        --repo ayousanz/tsukuyomi-chan-style-bert-vits2-model \
        --output ../Assets/StreamingAssets/uStyleBertVITS2/Models/sbv2_model_fp16.onnx

前提:
    - scripts/_sbv2_src/ に Style-Bert-VITS2 リポジトリが clone 済み
      git clone --depth 1 https://github.com/litagin02/Style-Bert-VITS2.git _sbv2_src
"""

import argparse
import json
import sys
import time
from pathlib import Path
from typing import cast

import numpy as np
import onnx
import torch
from huggingface_hub import hf_hub_download
from onnxconverter_common import float16
from onnxsim import simplify

from convert_for_sentis import convert_int64_to_int32

# SBV2 のモデル定義を import するために sys.path に追加
SBV2_SRC = Path(__file__).parent / "_sbv2_src"
if str(SBV2_SRC) not in sys.path:
    sys.path.insert(0, str(SBV2_SRC))

from style_bert_vits2.models.hyper_parameters import HyperParameters
from style_bert_vits2.models.models_jp_extra import (
    SynthesizerTrn as SynthesizerTrnJPExtra,
)
from style_bert_vits2.models.models import SynthesizerTrn
from style_bert_vits2.nlp.symbols import SYMBOLS
from safetensors.torch import load_file as load_safetensors


def download_model(repo_id: str, cache_dir: Path) -> tuple[Path, Path, Path]:
    """HuggingFace からモデルファイルをダウンロード"""
    cache_dir.mkdir(parents=True, exist_ok=True)

    # config.json をダウンロードして最適なチェックポイントを特定
    config_path = Path(hf_hub_download(repo_id, "config.json", cache_dir=cache_dir))
    style_vec_path = Path(
        hf_hub_download(repo_id, "style_vectors.npy", cache_dir=cache_dir)
    )

    # 最もステップ数が大きい safetensors を探す
    from huggingface_hub import list_repo_files

    files = list_repo_files(repo_id)
    safetensors_files = [f for f in files if f.endswith(".safetensors")]
    if not safetensors_files:
        raise FileNotFoundError(f"No safetensors files found in {repo_id}")

    # ステップ数でソート (e.g., tsukuyomi-chan_e200_s5200.safetensors)
    import re

    def extract_step(name: str) -> int:
        m = re.search(r"_s(\d+)", name)
        return int(m.group(1)) if m else 0

    best_ckpt = max(safetensors_files, key=extract_step)
    print(f"Selected checkpoint: {best_ckpt}")

    model_path = Path(hf_hub_download(repo_id, best_ckpt, cache_dir=cache_dir))
    return model_path, config_path, style_vec_path


def build_model(
    config_path: Path, model_path: Path, device: str = "cpu"
) -> tuple[torch.nn.Module, HyperParameters]:
    """config.json と safetensors からモデルを構築"""
    hps = HyperParameters.load_from_json(config_path)
    is_jp_extra = hps.version.endswith("JP-Extra")

    if is_jp_extra:
        print("Using JP-Extra model architecture")
        net_g = SynthesizerTrnJPExtra(
            n_vocab=len(SYMBOLS),
            spec_channels=hps.data.filter_length // 2 + 1,
            segment_size=hps.train.segment_size // hps.data.hop_length,
            n_speakers=hps.data.n_speakers,
            use_spk_conditioned_encoder=hps.model.use_spk_conditioned_encoder,
            use_noise_scaled_mas=hps.model.use_noise_scaled_mas,
            use_mel_posterior_encoder=hps.model.use_mel_posterior_encoder,
            use_duration_discriminator=hps.model.use_duration_discriminator,
            use_wavlm_discriminator=hps.model.use_wavlm_discriminator,
            inter_channels=hps.model.inter_channels,
            hidden_channels=hps.model.hidden_channels,
            filter_channels=hps.model.filter_channels,
            n_heads=hps.model.n_heads,
            n_layers=hps.model.n_layers,
            kernel_size=hps.model.kernel_size,
            p_dropout=hps.model.p_dropout,
            resblock=hps.model.resblock,
            resblock_kernel_sizes=hps.model.resblock_kernel_sizes,
            resblock_dilation_sizes=hps.model.resblock_dilation_sizes,
            upsample_rates=hps.model.upsample_rates,
            upsample_initial_channel=hps.model.upsample_initial_channel,
            upsample_kernel_sizes=hps.model.upsample_kernel_sizes,
            n_layers_q=hps.model.n_layers_q,
            use_spectral_norm=hps.model.use_spectral_norm,
            gin_channels=hps.model.gin_channels,
            slm=hps.model.slm,
        ).to(device)
    else:
        print("Using standard model architecture")
        net_g = SynthesizerTrn(
            n_vocab=len(SYMBOLS),
            spec_channels=hps.data.filter_length // 2 + 1,
            segment_size=hps.train.segment_size // hps.data.hop_length,
            n_speakers=hps.data.n_speakers,
            use_spk_conditioned_encoder=hps.model.use_spk_conditioned_encoder,
            use_noise_scaled_mas=hps.model.use_noise_scaled_mas,
            use_mel_posterior_encoder=hps.model.use_mel_posterior_encoder,
            use_duration_discriminator=hps.model.use_duration_discriminator,
            use_wavlm_discriminator=hps.model.use_wavlm_discriminator,
            inter_channels=hps.model.inter_channels,
            hidden_channels=hps.model.hidden_channels,
            filter_channels=hps.model.filter_channels,
            n_heads=hps.model.n_heads,
            n_layers=hps.model.n_layers,
            kernel_size=hps.model.kernel_size,
            p_dropout=hps.model.p_dropout,
            resblock=hps.model.resblock,
            resblock_kernel_sizes=hps.model.resblock_kernel_sizes,
            resblock_dilation_sizes=hps.model.resblock_dilation_sizes,
            upsample_rates=hps.model.upsample_rates,
            upsample_initial_channel=hps.model.upsample_initial_channel,
            upsample_kernel_sizes=hps.model.upsample_kernel_sizes,
            n_layers_q=hps.model.n_layers_q,
            use_spectral_norm=hps.model.use_spectral_norm,
            gin_channels=hps.model.gin_channels,
            slm=hps.model.slm,
        ).to(device)

    # safetensors から重みを読み込み
    print(f"Loading weights from: {model_path}")
    state_dict = load_safetensors(str(model_path), device=device)
    # strict=False で推論に不要なキーを無視
    missing, unexpected = net_g.load_state_dict(state_dict, strict=False)
    if missing:
        print(f"Missing keys ({len(missing)}): {missing[:5]}...")
    if unexpected:
        print(f"Unexpected keys ({len(unexpected)}): {unexpected[:5]}...")

    net_g.eval()
    return net_g, hps


def export_onnx(
    net_g: torch.nn.Module,
    hps: HyperParameters,
    output_path: Path,
    no_fp16: bool = False,
    no_dynamic: bool = False,
    no_simplify: bool = False,
    opset_version: int = 15,
    seq_len: int = 128,
):
    """モデルを Sentis 互換 ONNX にエクスポート"""
    device = "cpu"
    is_jp_extra = hps.version.endswith("JP-Extra")
    x_tst = torch.randint(0, 100, (1, seq_len), dtype=torch.long, device=device)
    x_tst_lengths = torch.tensor([seq_len], dtype=torch.long, device=device)
    sid = torch.tensor([0], dtype=torch.long, device=device)
    tones = torch.zeros(1, seq_len, dtype=torch.long, device=device)
    lang_ids = torch.ones(1, seq_len, dtype=torch.long, device=device)
    ja_bert = torch.randn(1, 1024, seq_len, device=device)
    style_vec = torch.randn(1, 256, device=device)
    length_scale = torch.tensor(1.0)
    sdp_ratio = torch.tensor(0.0)
    noise_scale = torch.tensor(0.667)
    noise_scale_w = torch.tensor(0.8)

    temp_path = str(output_path.with_suffix(".temp.onnx"))

    if is_jp_extra:

        def forward_jp_extra(
            x, x_lengths, sid, tone, language, bert, style_vec,
            length_scale=1.0, sdp_ratio=0.0, noise_scale=0.667, noise_scale_w=0.8,
        ):
            o, _, _, _ = cast(SynthesizerTrnJPExtra, net_g).infer(
                x, x_lengths, sid, tone, language, bert, style_vec,
                length_scale=length_scale, sdp_ratio=sdp_ratio,
                noise_scale=noise_scale, noise_scale_w=noise_scale_w,
            )
            return o

        net_g.forward = forward_jp_extra  # type: ignore

        jp_extra_dynamic_axes = (
            None
            if no_dynamic
            else {
                "x_tst": {0: "batch_size", 1: "x_tst_max_length"},
                "x_tst_lengths": {0: "batch_size"},
                "sid": {0: "batch_size"},
                "tones": {0: "batch_size", 1: "x_tst_max_length"},
                "language": {0: "batch_size", 1: "x_tst_max_length"},
                "bert": {0: "batch_size", 2: "x_tst_max_length"},
                "style_vec": {0: "batch_size"},
            }
        )
        print(f"Exporting ONNX (JP-Extra, dynamic={not no_dynamic})...")
        export_start = time.time()
        torch.onnx.export(
            model=net_g,
            args=(
                x_tst, x_tst_lengths, sid, tones, lang_ids,
                ja_bert, style_vec, length_scale, sdp_ratio, noise_scale, noise_scale_w,
            ),
            f=temp_path,
            verbose=False,
            opset_version=opset_version,
            dynamo=False,
            input_names=[
                "x_tst", "x_tst_lengths", "sid", "tones", "language",
                "bert", "style_vec",
                "length_scale", "sdp_ratio", "noise_scale", "noise_scale_w",
            ],
            output_names=["output"],
            dynamic_axes=jp_extra_dynamic_axes,
        )
        print(f"ONNX exported ({time.time() - export_start:.1f}s)")
    else:
        bert = torch.zeros(1, 1024, seq_len, device=device)
        en_bert = torch.zeros(1, 1024, seq_len, device=device)

        def forward_non_jp_extra(
            x, x_lengths, sid, tone, language, bert, ja_bert, en_bert, style_vec,
            length_scale=1.0, sdp_ratio=0.0, noise_scale=0.667, noise_scale_w=0.8,
        ):
            o, _, _, _ = cast(SynthesizerTrn, net_g).infer(
                x, x_lengths, sid, tone, language, bert, ja_bert, en_bert, style_vec,
                length_scale=length_scale, sdp_ratio=sdp_ratio,
                noise_scale=noise_scale, noise_scale_w=noise_scale_w,
            )
            return o

        net_g.forward = forward_non_jp_extra  # type: ignore

        non_jp_dynamic_axes = (
            None
            if no_dynamic
            else {
                "x_tst": {0: "batch_size", 1: "x_tst_max_length"},
                "x_tst_lengths": {0: "batch_size"},
                "sid": {0: "batch_size"},
                "tones": {0: "batch_size", 1: "x_tst_max_length"},
                "language": {0: "batch_size", 1: "x_tst_max_length"},
                "bert": {0: "batch_size", 2: "x_tst_max_length"},
                "ja_bert": {0: "batch_size", 2: "x_tst_max_length"},
                "en_bert": {0: "batch_size", 2: "x_tst_max_length"},
                "style_vec": {0: "batch_size"},
            }
        )
        print(f"Exporting ONNX (Non-JP-Extra, dynamic={not no_dynamic})...")
        export_start = time.time()
        torch.onnx.export(
            model=net_g,
            args=(
                x_tst, x_tst_lengths, sid, tones, lang_ids,
                bert, ja_bert, en_bert, style_vec,
                length_scale, sdp_ratio, noise_scale, noise_scale_w,
            ),
            f=temp_path,
            verbose=False,
            opset_version=opset_version,
            dynamo=False,
            input_names=[
                "x_tst", "x_tst_lengths", "sid", "tones", "language",
                "bert", "ja_bert", "en_bert", "style_vec",
                "length_scale", "sdp_ratio", "noise_scale", "noise_scale_w",
            ],
            output_names=["output"],
            dynamic_axes=non_jp_dynamic_axes,
        )
        print(f"ONNX exported ({time.time() - export_start:.1f}s)")

    # 後処理
    print("Loading exported ONNX...")
    model = onnx.load(temp_path)

    if not no_simplify:
        print("Simplifying with onnxsim...")
        model, check = simplify(model)
        if not check:
            print("Warning: onnxsim simplification check failed")
    else:
        print("Skipping onnxsim simplification.")

    print("Converting int64 -> int32...")
    model = convert_int64_to_int32(model)

    # Sentis は0次元テンソル (scalar []) を扱えないため [1] に変換
    for inp in model.graph.input:
        if len(inp.type.tensor_type.shape.dim) == 0:
            new_dim = inp.type.tensor_type.shape.dim.add()
            new_dim.dim_value = 1
            print(f"  Fixed scalar input: {inp.name} [] -> [1]")

    if not no_fp16:
        print("Converting to FP16...")
        try:
            model = float16.convert_float_to_float16(model, keep_io_types=True)
        except ValueError:
            # onnxsim may partially convert to fp16, retry with check disabled
            model = float16.convert_float_to_float16(
                model, keep_io_types=True, check_fp16_ready=False
            )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    print(f"Saving to: {output_path}")
    onnx.save(model, str(output_path))
    print(f"Done! Model size: {output_path.stat().st_size / 1024 / 1024:.1f} MB")

    # 一時ファイル削除
    Path(temp_path).unlink(missing_ok=True)


def main():
    parser = argparse.ArgumentParser(
        description="Convert Style-Bert-VITS2 model to Sentis-compatible ONNX"
    )
    parser.add_argument(
        "--repo",
        type=str,
        default="ayousanz/tsukuyomi-chan-style-bert-vits2-model",
        help="HuggingFace repo ID",
    )
    parser.add_argument(
        "--output",
        type=str,
        default="../Assets/StreamingAssets/uStyleBertVITS2/Models/sbv2_model_fp16.onnx",
        help="Output ONNX model path",
    )
    parser.add_argument(
        "--style-vec-output",
        type=str,
        default="../Assets/StreamingAssets/uStyleBertVITS2/Models/style_vectors.npy",
        help="Output path for style_vectors.npy",
    )
    parser.add_argument(
        "--no-fp16", action="store_true", help="Skip FP16 conversion"
    )
    parser.add_argument(
        "--no-dynamic",
        action="store_true",
        help="Export with fixed sequence length (no dynamic axes)",
    )
    parser.add_argument(
        "--no-simplify",
        action="store_true",
        help="Skip onnxsim simplification",
    )
    parser.add_argument(
        "--seq-len",
        type=int,
        default=128,
        help="Sequence length for dummy input (default: 128)",
    )
    parser.add_argument(
        "--cache-dir",
        type=str,
        default=".cache/sbv2",
        help="Cache directory for downloaded files",
    )
    args = parser.parse_args()

    # 1. モデルダウンロード
    print(f"Downloading model from: {args.repo}")
    model_path, config_path, style_vec_path = download_model(
        args.repo, Path(args.cache_dir)
    )

    # 2. style_vectors.npy をコピー
    style_vec_out = Path(args.style_vec_output)
    style_vec_out.parent.mkdir(parents=True, exist_ok=True)
    import shutil
    shutil.copy2(style_vec_path, style_vec_out)
    print(f"Copied style_vectors.npy to: {style_vec_out}")

    # 3. モデル構築
    net_g, hps = build_model(config_path, model_path)

    # 4. ONNX エクスポート + 後処理
    export_onnx(
        net_g, hps, Path(args.output),
        no_fp16=args.no_fp16, no_dynamic=args.no_dynamic,
        no_simplify=args.no_simplify,
        seq_len=args.seq_len,
    )


if __name__ == "__main__":
    main()
