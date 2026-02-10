using System;
using Unity.InferenceEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// DeBERTa (ku-nlp/deberta-v2-large-japanese-char-wwm) 推論ラッパー。
    /// 入力: tokenIds, attentionMask, tokenTypeIds
    /// 出力: [1, 1024, token_len] の BERT 埋め込み (flattened float[])
    /// </summary>
    public class BertRunner : IDisposable
    {
        private Worker _worker;
        private bool _disposed;

        public BertRunner(ModelAsset modelAsset, BackendType backendType)
        {
            var model = ModelLoader.Load(modelAsset);
            _worker = new Worker(model, backendType);
        }

        /// <summary>
        /// DeBERTa推論を実行する。
        /// </summary>
        /// <param name="tokenIds">[CLS] ... [SEP] のトークンID配列</param>
        /// <param name="attentionMask">アテンションマスク (全1)</param>
        /// <returns>BERT埋め込み [1, 1024, token_len] を flatten した float[]</returns>
        public float[] Run(int[] tokenIds, int[] attentionMask)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BertRunner));

            int tokenLen = tokenIds.Length;

            using var inputIds = new Tensor<int>(new TensorShape(1, tokenLen), tokenIds);
            using var tokenTypes = new Tensor<int>(new TensorShape(1, tokenLen), new int[tokenLen]); // 全0
            using var mask = new Tensor<int>(new TensorShape(1, tokenLen), attentionMask);

            _worker.SetInput("input_ids", inputIds);
            _worker.SetInput("token_type_ids", tokenTypes);
            _worker.SetInput("attention_mask", mask);
            _worker.Schedule();

            var output = _worker.PeekOutput() as Tensor<float>;
            output.ReadbackAndClone();
            return output.DownloadToArray(); // [1, 1024, token_len] flattened
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
