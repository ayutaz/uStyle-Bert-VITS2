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
        private Worker _worker;
        private bool _disposed;

        public SBV2ModelRunner(ModelAsset modelAsset, BackendType backendType)
        {
            var model = ModelLoader.Load(modelAsset);
            _worker = new Worker(model, backendType);
        }

        /// <summary>
        /// メインTTS推論を実行する。
        /// </summary>
        /// <returns>音声サンプル (44100Hz float32 PCM) を flatten した float[]</returns>
        public float[] Run(
            int[] phonemeIds,
            int[] tones,
            int[] languageIds,
            int speakerId,
            float[] bertEmbedding,
            float[] styleVector,
            float sdpRatio,
            float noiseScale,
            float noiseScaleW,
            float lengthScale)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SBV2ModelRunner));

            int seqLen = phonemeIds.Length;

            using var xTst = new Tensor<int>(new TensorShape(1, seqLen), phonemeIds);
            using var xTstLengths = new Tensor<int>(new TensorShape(1), new[] { seqLen });
            using var tonesTensor = new Tensor<int>(new TensorShape(1, seqLen), tones);
            using var langTensor = new Tensor<int>(new TensorShape(1, seqLen), languageIds);
            using var sidTensor = new Tensor<int>(new TensorShape(1), new[] { speakerId });
            using var bertTensor = new Tensor<float>(new TensorShape(1, 1024, seqLen), bertEmbedding);
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
