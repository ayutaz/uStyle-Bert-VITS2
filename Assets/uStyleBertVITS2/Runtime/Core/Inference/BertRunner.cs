using System;
using Unity.InferenceEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// DeBERTa (ku-nlp/deberta-v2-large-japanese-char-wwm) 推論ラッパー。
    /// 入力: tokenIds, attentionMask, tokenTypeIds
    /// 出力: [1, 1024, token_len] の BERT 埋め込み (flattened float[])
    /// 静的エクスポートモデルの場合、入力を maxSeqLen にパディングし、出力を実際のトークン長にトリムする。
    /// </summary>
    public class BertRunner : IDisposable
    {
        private const int HiddenSize = 1024;

        private Worker _worker;
        private readonly int _maxSeqLen;
        private bool _disposed;

        public BertRunner(ModelAsset modelAsset, BackendType backendType, int maxSeqLen = 50)
        {
            var model = ModelLoader.Load(modelAsset);
            _worker = new Worker(model, backendType);
            _maxSeqLen = maxSeqLen;
        }

        internal BertRunner(Model model, BackendType backendType, int maxSeqLen = 50)
        {
            _worker = new Worker(model, backendType);
            _maxSeqLen = maxSeqLen;
        }

        /// <summary>
        /// DeBERTa推論を実行する。
        /// 入力が maxSeqLen より短い場合はゼロパディングし、出力は実トークン長にトリムして返す。
        /// </summary>
        /// <param name="tokenIds">[CLS] ... [SEP] のトークンID配列</param>
        /// <param name="attentionMask">アテンションマスク (有効トークン=1)</param>
        /// <returns>BERT埋め込み [1, 1024, token_len] を flatten した float[]</returns>
        public float[] Run(int[] tokenIds, int[] attentionMask)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BertRunner));

            int tokenLen = tokenIds.Length;
            int padLen = _maxSeqLen;

            // パディング: tokenIds, attentionMask を maxSeqLen に合わせる
            int[] paddedIds = new int[padLen];
            int[] paddedMask = new int[padLen];
            int copyLen = Math.Min(tokenLen, padLen);
            Array.Copy(tokenIds, paddedIds, copyLen);
            Array.Copy(attentionMask, paddedMask, copyLen);

            using var inputIds = new Tensor<int>(new TensorShape(1, padLen), paddedIds);
            using var mask = new Tensor<int>(new TensorShape(1, padLen), paddedMask);

            _worker.SetInput("input_ids", inputIds);
            _worker.SetInput("attention_mask", mask);
            _worker.Schedule();

            var output = _worker.PeekOutput() as Tensor<float>;
            output.ReadbackAndClone();
            float[] fullOutput = output.DownloadToArray(); // [1, 1024, padLen]

            // 出力トリム: 実際のトークン長分だけ返す
            if (tokenLen >= padLen)
                return fullOutput;

            float[] trimmed = new float[HiddenSize * tokenLen];
            for (int h = 0; h < HiddenSize; h++)
            {
                Array.Copy(fullOutput, h * padLen, trimmed, h * tokenLen, tokenLen);
            }
            return trimmed;
        }

        /// <summary>
        /// 出力テンソルの token_len 次元を返す。
        /// </summary>
        public int GetOutputTokenLen(int inputTokenLen)
        {
            return inputTokenLen;
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
