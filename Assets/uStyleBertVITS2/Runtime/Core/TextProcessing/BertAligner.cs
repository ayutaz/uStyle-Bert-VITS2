using System;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// BERT出力 [1, 1024, token_len] を word2ph ベースで音素列長 [1, 1024, phone_seq_len] に展開する。
    /// 各BERTトークンの埋め込みベクトルを、対応する音素数分だけ繰り返しコピーする。
    /// </summary>
    public static class BertAligner
    {
        public const int EmbeddingDimension = 1024;

        /// <summary>
        /// BERT埋め込みを音素列長に展開する。
        /// </summary>
        /// <param name="bertFlat">BERT出力 [1, 1024, tokenLen] を flatten した配列</param>
        /// <param name="tokenLen">BERTトークン列の長さ</param>
        /// <param name="word2ph">各BERTトークンに対応する音素数の配列 [tokenLen]</param>
        /// <param name="phoneSeqLen">音素列の長さ (= word2phの合計)</param>
        /// <returns>展開後の配列 [1, 1024, phoneSeqLen] を flatten した float[]</returns>
        public static float[] AlignBertToPhonemes(
            float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
        {
            if (bertFlat == null)
                throw new ArgumentNullException(nameof(bertFlat));
            if (word2ph == null)
                throw new ArgumentNullException(nameof(word2ph));

            // word2ph合計の検証
            int sum = 0;
            for (int w = 0; w < word2ph.Length; w++)
                sum += word2ph[w];

            if (sum != phoneSeqLen)
                throw new ArgumentException(
                    $"word2ph sum ({sum}) does not match phoneSeqLen ({phoneSeqLen}).");

            float[] aligned = new float[EmbeddingDimension * phoneSeqLen];

            int phoneIdx = 0;
            int tokenIdx = 0;

            for (int w = 0; w < word2ph.Length; w++)
            {
                for (int p = 0; p < word2ph[w]; p++)
                {
                    // bertFlat[d * tokenLen + tokenIdx] → aligned[d * phoneSeqLen + phoneIdx]
                    for (int d = 0; d < EmbeddingDimension; d++)
                    {
                        aligned[d * phoneSeqLen + phoneIdx] = bertFlat[d * tokenLen + tokenIdx];
                    }
                    phoneIdx++;
                }
                tokenIdx++;
            }

            return aligned;
        }
    }
}
