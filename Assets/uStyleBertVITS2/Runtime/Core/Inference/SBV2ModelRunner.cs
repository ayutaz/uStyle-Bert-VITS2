using System;
using Unity.InferenceEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// SynthesizerTrn (Style-Bert-VITS2 メインTTS) 推論ラッパー。
    /// 入力: 音素ID, トーン, 言語ID, 話者ID, BERT埋め込み, スタイルベクトル, 各スカラーパラメータ
    /// 出力: [1, 1, audio_samples] の PCM 音声波形 (44100Hz)
    /// </summary>
    public class SBV2ModelRunner : IDisposable
    {
        private const int HiddenSize = 1024;

        private Worker _worker;
        private readonly int _maxSeqLen;
        private bool _disposed;

        public SBV2ModelRunner(ModelAsset modelAsset, BackendType backendType, int maxSeqLen = 20)
        {
            var model = ModelLoader.Load(modelAsset);
            _worker = new Worker(model, backendType);
            _maxSeqLen = maxSeqLen;
        }

        internal SBV2ModelRunner(Model model, BackendType backendType, int maxSeqLen = 20)
        {
            _worker = new Worker(model, backendType);
            _maxSeqLen = maxSeqLen;
        }

        /// <summary>
        /// メインTTS推論を実行する（日本語専用）。
        /// bert/en_bert は内部で零テンソルを生成する。
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
            int padLen = _maxSeqLen;
            int copyLen = Math.Min(seqLen, padLen);

            // 入力配列を maxSeqLen にパディング
            int[] paddedPhonemes = new int[padLen];
            int[] paddedTones = new int[padLen];
            int[] paddedLangs = new int[padLen];
            Array.Copy(phonemeIds, paddedPhonemes, copyLen);
            Array.Copy(tones, paddedTones, copyLen);
            Array.Copy(languageIds, paddedLangs, copyLen);

            // BERT 埋め込みのパディング: [1, 1024, seqLen] → [1, 1024, padLen]
            float[] paddedJaBert = new float[HiddenSize * padLen];
            for (int h = 0; h < HiddenSize; h++)
            {
                Array.Copy(jaBertEmbedding, h * seqLen, paddedJaBert, h * padLen, copyLen);
            }

            using var xTst = new Tensor<int>(new TensorShape(1, padLen), paddedPhonemes);
            using var xTstLengths = new Tensor<int>(new TensorShape(1), new[] { seqLen });
            using var tonesTensor = new Tensor<int>(new TensorShape(1, padLen), paddedTones);
            using var langTensor = new Tensor<int>(new TensorShape(1, padLen), paddedLangs);
            using var sidTensor = new Tensor<int>(new TensorShape(1), new[] { speakerId });
            // bert / en_bert は日本語専用なので零テンソル
            using var bertTensor = new Tensor<float>(new TensorShape(1, HiddenSize, padLen));
            using var jaBertTensor = new Tensor<float>(new TensorShape(1, HiddenSize, padLen), paddedJaBert);
            using var enBertTensor = new Tensor<float>(new TensorShape(1, HiddenSize, padLen));
            using var styleTensor = new Tensor<float>(new TensorShape(1, 256), styleVector);
            using var sdpTensor = new Tensor<float>(new TensorShape(1), new[] { sdpRatio });
            using var noiseTensor = new Tensor<float>(new TensorShape(1), new[] { noiseScale });
            using var noiseWTensor = new Tensor<float>(new TensorShape(1), new[] { noiseScaleW });
            using var lengthTensor = new Tensor<float>(new TensorShape(1), new[] { lengthScale });

            _worker.SetInput("x_tst", xTst);
            _worker.SetInput("x_tst_lengths", xTstLengths);
            _worker.SetInput("tones", tonesTensor);
            _worker.SetInput("language", langTensor);
            _worker.SetInput("bert", bertTensor);
            _worker.SetInput("ja_bert", jaBertTensor);
            _worker.SetInput("en_bert", enBertTensor);
            _worker.SetInput("style_vec", styleTensor);
            _worker.SetInput("sid", sidTensor);
            _worker.SetInput("sdp_ratio", sdpTensor);
            _worker.SetInput("noise_scale", noiseTensor);
            _worker.SetInput("noise_scale_w", noiseWTensor);
            _worker.SetInput("length_scale", lengthTensor);

            _worker.Schedule();

            var output = _worker.PeekOutput("output") as Tensor<float>;
            output.ReadbackAndClone();
            return output.DownloadToArray(); // [1, 1, audio_samples] flattened
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
