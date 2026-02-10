using NUnit.Framework;
using Unity.Collections;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class BertAlignmentJobTests
    {
        [Test]
        public void ResultMatchesCPU()
        {
            int tokenLen = 3;
            int phoneSeqLen = 5;
            int embDim = 1024;
            int[] word2ph = { 2, 2, 1 };

            // テストデータ作成
            float[] bertFlat = new float[embDim * tokenLen];
            for (int d = 0; d < embDim; d++)
                for (int t = 0; t < tokenLen; t++)
                    bertFlat[d * tokenLen + t] = d * 100f + t;

            // CPU版 (BertAligner)
            float[] cpuResult = BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen);

            // Burst Job版
            var phoneToToken = new NativeArray<int>(phoneSeqLen, Allocator.TempJob);
            var bertNative = new NativeArray<float>(bertFlat, Allocator.TempJob);
            var alignedNative = new NativeArray<float>(embDim * phoneSeqLen, Allocator.TempJob);

            try
            {
                // word2ph → phoneToToken マッピング
                int phoneIdx = 0;
                for (int w = 0; w < word2ph.Length; w++)
                {
                    for (int p = 0; p < word2ph[w]; p++)
                        phoneToToken[phoneIdx++] = w;
                }

                var job = new BertAlignmentJob
                {
                    BertFlat = bertNative,
                    PhoneToToken = phoneToToken,
                    AlignedBert = alignedNative,
                    TokenLen = tokenLen,
                    PhoneSeqLen = phoneSeqLen,
                    EmbDim = embDim,
                };

                job.Schedule(phoneSeqLen, 64).Complete();

                float[] burstResult = alignedNative.ToArray();

                // CPU版とBurst版が一致
                Assert.AreEqual(cpuResult.Length, burstResult.Length);
                for (int i = 0; i < cpuResult.Length; i++)
                {
                    Assert.AreEqual(cpuResult[i], burstResult[i], 1e-6f,
                        $"Mismatch at index {i}");
                }
            }
            finally
            {
                phoneToToken.Dispose();
                bertNative.Dispose();
                alignedNative.Dispose();
            }
        }
    }
}
