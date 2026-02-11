using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

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

        /// <summary>
        /// Burst ジョブを使用して BERT 埋め込みを音素列長に展開する。
        /// </summary>
        public static float[] AlignBertToPhonemesBurst(
            float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen)
        {
            float[] aligned = new float[EmbeddingDimension * phoneSeqLen];
            AlignBertToPhonemesBurst(bertFlat, tokenLen, word2ph, phoneSeqLen, aligned);
            return aligned;
        }

        /// <summary>
        /// Burst ジョブを使用して BERT 埋め込みを事前確保済みバッファに展開する。
        /// </summary>
        public static void AlignBertToPhonemesBurst(
            float[] bertFlat, int tokenLen, int[] word2ph, int phoneSeqLen, float[] dest)
        {
            if (bertFlat == null) throw new ArgumentNullException(nameof(bertFlat));
            if (word2ph == null) throw new ArgumentNullException(nameof(word2ph));
            if (dest == null) throw new ArgumentNullException(nameof(dest));

            // word2ph合計の検証
            int sum = 0;
            for (int w = 0; w < word2ph.Length; w++)
                sum += word2ph[w];

            if (sum != phoneSeqLen)
                throw new ArgumentException(
                    $"word2ph sum ({sum}) does not match phoneSeqLen ({phoneSeqLen}).");

            // phoneToToken マッピング構築
            var phoneToToken = new NativeArray<int>(phoneSeqLen, Allocator.TempJob);
            int phoneIdx = 0;
            for (int w = 0; w < word2ph.Length; w++)
            {
                for (int p = 0; p < word2ph[w]; p++)
                {
                    phoneToToken[phoneIdx++] = w;
                }
            }

            var bertNative = new NativeArray<float>(bertFlat, Allocator.TempJob);
            var alignedNative = new NativeArray<float>(EmbeddingDimension * phoneSeqLen, Allocator.TempJob);

            var job = new BertAlignmentJob
            {
                BertFlat = bertNative,
                PhoneToToken = phoneToToken,
                AlignedBert = alignedNative,
                TokenLen = tokenLen,
                PhoneSeqLen = phoneSeqLen,
                EmbDim = EmbeddingDimension
            };

            job.Schedule(phoneSeqLen, 64).Complete();

            // NativeArray.CopyTo requires exact length match, but dest may be larger (ArrayPool).
            // Use unsafe copy to avoid the length check.
            int copyLen = EmbeddingDimension * phoneSeqLen;
            unsafe
            {
                fixed (float* destPtr = dest)
                {
                    Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemCpy(
                        destPtr, alignedNative.GetUnsafeReadOnlyPtr(), copyLen * sizeof(float));
                }
            }

            alignedNative.Dispose();
            bertNative.Dispose();
            phoneToToken.Dispose();
        }
    }
}
