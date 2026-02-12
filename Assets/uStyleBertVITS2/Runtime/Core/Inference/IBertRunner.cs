using System;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// BERT推論の抽象インターフェース。
    /// Sentis (BertRunner) と ONNX Runtime (OnnxRuntimeBertRunner) で共通。
    /// </summary>
    public interface IBertRunner : IDisposable
    {
        /// <summary>
        /// BERT隠れ層の次元数 (DeBERTa = 1024)。
        /// </summary>
        int HiddenSize { get; }

        /// <summary>
        /// BERT推論を実行し、結果を事前確保済みバッファに書き込む。
        /// </summary>
        /// <param name="tokenIds">[CLS] ... [SEP] のトークンID配列</param>
        /// <param name="attentionMask">アテンションマスク (有効トークン=1)</param>
        /// <param name="dest">結果格納先。最低 HiddenSize × tokenIds.Length 要素が必要</param>
        void Run(int[] tokenIds, int[] attentionMask, float[] dest);

        /// <summary>
        /// BERT推論を実行し、結果を新規配列で返す。
        /// </summary>
        /// <param name="tokenIds">[CLS] ... [SEP] のトークンID配列</param>
        /// <param name="attentionMask">アテンションマスク (有効トークン=1)</param>
        /// <returns>BERT埋め込み [1, HiddenSize, token_len] を flatten した float[]</returns>
        float[] Run(int[] tokenIds, int[] attentionMask);
    }
}
