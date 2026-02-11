using System;
using Unity.InferenceEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// DeBERTa (ku-nlp/deberta-v2-large-japanese-char-wwm) 推論ラッパー。
    /// 入力: tokenIds, attentionMask
    /// 出力: [1, 1024, token_len] の BERT 埋め込み (flattened float[])
    /// Sentis はモデルの固定 shape を要求するため、入力を padLen にパディングし、
    /// 出力は実際のトークン長にトリムして返す。
    /// padLen はモデルの入力 shape から自動取得する。
    /// </summary>
    public class BertRunner : IDisposable
    {
        private const int HiddenSize = 1024;

        private Worker _worker;
        private readonly int _padLen;
        private bool _disposed;

        public BertRunner(ModelAsset modelAsset, BackendType backendType)
        {
            var model = ModelLoader.Load(modelAsset);
            _padLen = GetSeqLenFromModel(model);
            _worker = new Worker(model, backendType);
        }

        internal BertRunner(Model model, BackendType backendType)
        {
            _padLen = GetSeqLenFromModel(model);
            _worker = new Worker(model, backendType);
        }

        /// <summary>
        /// モデルが要求する固定シーケンス長。
        /// </summary>
        public int PadLen => _padLen;

        /// <summary>
        /// DeBERTa推論を実行する。
        /// 入力を padLen にゼロパディングし、出力は実トークン長にトリムして返す。
        /// </summary>
        /// <param name="tokenIds">[CLS] ... [SEP] のトークンID配列</param>
        /// <param name="attentionMask">アテンションマスク (有効トークン=1)</param>
        /// <returns>BERT埋め込み [1, 1024, token_len] を flatten した float[]</returns>
        public float[] Run(int[] tokenIds, int[] attentionMask)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BertRunner));

            int tokenLen = tokenIds.Length;
            if (tokenLen > _padLen)
                throw new ArgumentException(
                    $"Token length {tokenLen} exceeds model capacity {_padLen}. " +
                    "Re-export the ONNX model with a larger seq_len.");

            // パディング: tokenIds, attentionMask を padLen に合わせる
            int[] paddedIds = new int[_padLen];
            int[] paddedMask = new int[_padLen];
            Array.Copy(tokenIds, paddedIds, tokenLen);
            Array.Copy(attentionMask, paddedMask, tokenLen);

            using var inputIds = new Tensor<int>(new TensorShape(1, _padLen), paddedIds);
            using var mask = new Tensor<int>(new TensorShape(1, _padLen), paddedMask);

            _worker.SetInput("input_ids", inputIds);
            _worker.SetInput("attention_mask", mask);
            _worker.Schedule();

            var output = _worker.PeekOutput() as Tensor<float>;
            output.ReadbackAndClone();
            float[] fullOutput = output.DownloadToArray(); // [1, 1024, padLen]

            // 出力トリム: 実際のトークン長分だけ返す
            if (tokenLen == _padLen)
                return fullOutput;

            float[] trimmed = new float[HiddenSize * tokenLen];
            for (int h = 0; h < HiddenSize; h++)
            {
                Array.Copy(fullOutput, h * _padLen, trimmed, h * tokenLen, tokenLen);
            }
            return trimmed;
        }

        private static int GetSeqLenFromModel(Model model)
        {
            foreach (var input in model.inputs)
            {
                if (input.name == "input_ids" && input.shape.rank >= 2)
                    return input.shape.Get(1);
            }
            return 50; // fallback
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
