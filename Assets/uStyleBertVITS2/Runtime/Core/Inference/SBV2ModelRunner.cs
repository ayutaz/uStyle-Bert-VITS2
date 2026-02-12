using System;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using Unity.InferenceEngine;
using uStyleBertVITS2.Diagnostics;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// SynthesizerTrn (Style-Bert-VITS2 メインTTS) 推論ラッパー。
    /// 入力: 音素ID, トーン, 言語ID, 話者ID, BERT埋め込み, スタイルベクトル, 各スカラーパラメータ
    /// 出力: [1, 1, audio_samples] の PCM 音声波形 (44100Hz)
    /// JP-Extra / 通常モデルを自動判定し、BERT入力名を適切に切り替える。
    /// Sentis はモデルの固定 shape を要求するため、padLen にパディングして送信する。
    /// </summary>
    public class SBV2ModelRunner : IDisposable
    {
        private const int HiddenSize = 1024;

        private Worker _worker;
        private readonly bool _isJPExtra;
        private readonly int _padLen;
        private readonly int[] _paddedPhonemes;
        private readonly int[] _paddedTones;
        private readonly int[] _paddedLangs;
        private readonly float[] _paddedJaBert;
        private readonly float[] _zeroBertBuffer;   // 非JP-Extra用
        private readonly float[] _zeroEnBertBuffer;  // 非JP-Extra用
        private readonly int[] _scalarIntBuf = new int[1];
        private readonly float[] _scalarFloatBuf = new float[1];
        private bool _disposed;

        public SBV2ModelRunner(ModelAsset modelAsset, BackendType backendType)
        {
            var model = ModelLoader.Load(modelAsset);
            _isJPExtra = !model.inputs.Exists(i => i.name == "ja_bert");
            _padLen = GetSeqLenFromModel(model);
            _paddedPhonemes = new int[_padLen];
            _paddedTones = new int[_padLen];
            _paddedLangs = new int[_padLen];
            _paddedJaBert = new float[HiddenSize * _padLen];
            if (!_isJPExtra)
            {
                _zeroBertBuffer = new float[HiddenSize * _padLen];
                _zeroEnBertBuffer = new float[HiddenSize * _padLen];
            }
            _worker = new Worker(model, backendType);
        }

        internal SBV2ModelRunner(Model model, BackendType backendType)
        {
            _isJPExtra = !model.inputs.Exists(i => i.name == "ja_bert");
            _padLen = GetSeqLenFromModel(model);
            _paddedPhonemes = new int[_padLen];
            _paddedTones = new int[_padLen];
            _paddedLangs = new int[_padLen];
            _paddedJaBert = new float[HiddenSize * _padLen];
            if (!_isJPExtra)
            {
                _zeroBertBuffer = new float[HiddenSize * _padLen];
                _zeroEnBertBuffer = new float[HiddenSize * _padLen];
            }
            _worker = new Worker(model, backendType);
        }

        /// <summary>
        /// JP-Extra モデルかどうか。
        /// JP-Extra は "bert" 入力のみ、通常モデルは "bert"/"ja_bert"/"en_bert" の3入力。
        /// </summary>
        public bool IsJPExtra => _isJPExtra;

        /// <summary>
        /// モデルが要求する固定シーケンス長。
        /// </summary>
        public int PadLen => _padLen;

        /// <summary>
        /// メインTTS推論を実行する（日本語専用）。
        /// 通常モデルでは bert/en_bert に零テンソルを送信。
        /// JP-Extraモデルでは "bert" に日本語BERT埋め込みを送信。
        /// 入力が padLen より長い場合は ArgumentException をスローする。
        /// </summary>
        /// <returns>音声サンプル (44100Hz float32 PCM) を flatten した float[]</returns>
        public float[] Run(
            int[] phonemeIds,
            int[] tones,
            int[] languageIds,
            int speakerId,
            float[] jaBertEmbedding,
            float[] styleVector,
            float sdpRatio,
            float noiseScale,
            float noiseScaleW,
            float lengthScale)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SBV2ModelRunner));

            int seqLen = phonemeIds.Length;
            if (seqLen > _padLen)
                throw new ArgumentException(
                    $"Phoneme sequence length {seqLen} exceeds model capacity {_padLen}. " +
                    "Re-export the ONNX model with a larger seq_len.");

            // 入力配列を padLen にパディング (バッファ再利用)
            Array.Clear(_paddedPhonemes, 0, _padLen);
            Array.Clear(_paddedTones, 0, _padLen);
            Array.Clear(_paddedLangs, 0, _padLen);
            Array.Copy(phonemeIds, _paddedPhonemes, seqLen);
            Array.Copy(tones, _paddedTones, seqLen);
            Array.Copy(languageIds, _paddedLangs, seqLen);

            // BERT 埋め込みのパディング: [1, 1024, seqLen] → [1, 1024, padLen] (unsafe バッファ再利用)
            unsafe
            {
                fixed (float* srcPtr = jaBertEmbedding, dstPtr = _paddedJaBert)
                {
                    UnsafeUtility.MemClear(dstPtr, (long)HiddenSize * _padLen * sizeof(float));
                    for (int h = 0; h < HiddenSize; h++)
                        UnsafeUtility.MemCpy(
                            dstPtr + h * _padLen,
                            srcPtr + h * seqLen,
                            seqLen * sizeof(float));
                }
            }

            using var xTst = new Tensor<int>(new TensorShape(1, _padLen), _paddedPhonemes);
            _scalarIntBuf[0] = seqLen;
            using var xTstLengths = new Tensor<int>(new TensorShape(1), _scalarIntBuf);
            using var tonesTensor = new Tensor<int>(new TensorShape(1, _padLen), _paddedTones);
            using var langTensor = new Tensor<int>(new TensorShape(1, _padLen), _paddedLangs);
            _scalarIntBuf[0] = speakerId;
            using var sidTensor = new Tensor<int>(new TensorShape(1), _scalarIntBuf);
            using var jaBertTensor = new Tensor<float>(new TensorShape(1, HiddenSize, _padLen), _paddedJaBert);
            using var styleTensor = new Tensor<float>(new TensorShape(1, 256), styleVector);
            _scalarFloatBuf[0] = sdpRatio;
            using var sdpTensor = new Tensor<float>(new TensorShape(1), _scalarFloatBuf);
            _scalarFloatBuf[0] = noiseScale;
            using var noiseTensor = new Tensor<float>(new TensorShape(1), _scalarFloatBuf);
            _scalarFloatBuf[0] = noiseScaleW;
            using var noiseWTensor = new Tensor<float>(new TensorShape(1), _scalarFloatBuf);
            _scalarFloatBuf[0] = lengthScale;
            using var lengthTensor = new Tensor<float>(new TensorShape(1), _scalarFloatBuf);

            _worker.SetInput("x_tst", xTst);
            _worker.SetInput("x_tst_lengths", xTstLengths);
            _worker.SetInput("tones", tonesTensor);
            _worker.SetInput("language", langTensor);
            _worker.SetInput("sid", sidTensor);
            _worker.SetInput("style_vec", styleTensor);
            _worker.SetInput("sdp_ratio", sdpTensor);
            _worker.SetInput("noise_scale", noiseTensor);
            _worker.SetInput("noise_scale_w", noiseWTensor);
            _worker.SetInput("length_scale", lengthTensor);

            if (TTSDebugLog.Enabled)
            {
                int dumpLen = Math.Min(seqLen, 32);
                var sb = new StringBuilder();
                sb.Append($"Inputs: seqLen={seqLen}, padLen={_padLen}, speakerId={speakerId}\n");
                sb.Append($"  x_tst[0..{dumpLen - 1}]: ");
                for (int i = 0; i < dumpLen; i++) { if (i > 0) sb.Append(' '); sb.Append(_paddedPhonemes[i]); }
                sb.Append($"\n  tones[0..{dumpLen - 1}]: ");
                for (int i = 0; i < dumpLen; i++) { if (i > 0) sb.Append(' '); sb.Append(_paddedTones[i]); }
                TTSDebugLog.Log("TTS.Model", sb.ToString());
            }

            if (_isJPExtra)
            {
                // JP-Extra: "bert" = 日本語BERT埋め込み (唯一のBERT入力)
                _worker.SetInput("bert", jaBertTensor);
            }
            else
            {
                // 通常モデル: bert(中国語)=零, ja_bert=日本語, en_bert(英語)=零 (バッファ再利用)
                using var bertTensor = new Tensor<float>(new TensorShape(1, HiddenSize, _padLen), _zeroBertBuffer);
                using var enBertTensor = new Tensor<float>(new TensorShape(1, HiddenSize, _padLen), _zeroEnBertBuffer);
                _worker.SetInput("bert", bertTensor);
                _worker.SetInput("ja_bert", jaBertTensor);
                _worker.SetInput("en_bert", enBertTensor);
            }

            _worker.Schedule();

            var output = _worker.PeekOutput("output") as Tensor<float>;
            output.ReadbackAndClone();
            return output.DownloadToArray(); // [1, 1, audio_samples] flattened
        }

        private static int GetSeqLenFromModel(Model model)
        {
            foreach (var input in model.inputs)
            {
                if (input.name == "x_tst" && input.shape.rank >= 2)
                {
                    int dim = input.shape.Get(1);
                    // 動的軸の場合、シンボリック値 (0 以下) が返る。
                    // エクスポート時のデフォルト seq_len=128 をフォールバック値とする。
                    return dim > 0 ? dim : 128;
                }
            }
            return 128; // fallback
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _worker?.Dispose();
                _worker = null;
                _disposed = true;
            }
        }
    }
}
