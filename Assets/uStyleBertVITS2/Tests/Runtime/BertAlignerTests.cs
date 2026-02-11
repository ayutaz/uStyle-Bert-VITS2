using System;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class BertAlignerTests
    {
        [Test]
        public void AlignMatchesPhoneSeqLen()
        {
            // tokenLen=3, phoneSeqLen=5, word2ph=[2,2,1]
            int tokenLen = 3;
            int phoneSeqLen = 5;
            int[] word2ph = { 2, 2, 1 };

            // bertFlat: [1024 * 3] — 各次元dのtokenIdx tでの値 = d * 1000 + t
            float[] bertFlat = new float[BertAligner.EmbeddingDimension * tokenLen];
            for (int d = 0; d < BertAligner.EmbeddingDimension; d++)
                for (int t = 0; t < tokenLen; t++)
                    bertFlat[d * tokenLen + t] = d * 1000f + t;

            float[] aligned = BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen);

            Assert.AreEqual(BertAligner.EmbeddingDimension * phoneSeqLen, aligned.Length);
        }

        [Test]
        public void Word2PhSumConsistency()
        {
            int tokenLen = 4;
            int[] word2ph = { 1, 3, 2, 1 };
            int phoneSeqLen = 7; // 1+3+2+1

            float[] bertFlat = new float[BertAligner.EmbeddingDimension * tokenLen];
            float[] aligned = BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen);

            Assert.AreEqual(BertAligner.EmbeddingDimension * phoneSeqLen, aligned.Length);
        }

        [Test]
        public void SingleTokenExpansion()
        {
            // word2ph=[3] — 1トークンを3音素に展開
            int tokenLen = 1;
            int phoneSeqLen = 3;
            int[] word2ph = { 3 };

            float[] bertFlat = new float[BertAligner.EmbeddingDimension * tokenLen];
            // 各次元に固有値を設定
            for (int d = 0; d < BertAligner.EmbeddingDimension; d++)
                bertFlat[d] = d * 0.01f;

            float[] aligned = BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen);

            // 3つの音素すべてが同じBERTベクトル（token 0のもの）になるはず
            for (int d = 0; d < BertAligner.EmbeddingDimension; d++)
            {
                float expected = d * 0.01f;
                for (int p = 0; p < phoneSeqLen; p++)
                {
                    Assert.AreEqual(expected, aligned[d * phoneSeqLen + p], 1e-6f,
                        $"dim={d}, phone={p}: 同一ベクトルが展開されるべき");
                }
            }
        }

        [Test]
        public void BurstVersion_MatchesCPU()
        {
            int tokenLen = 4;
            int[] word2ph = { 1, 3, 2, 1 };
            int phoneSeqLen = 7;

            float[] bertFlat = new float[BertAligner.EmbeddingDimension * tokenLen];
            for (int d = 0; d < BertAligner.EmbeddingDimension; d++)
                for (int t = 0; t < tokenLen; t++)
                    bertFlat[d * tokenLen + t] = d * 100f + t * 0.1f;

            float[] cpuResult = BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen);
            float[] burstResult = BertAligner.AlignBertToPhonemesBurst(bertFlat, tokenLen, word2ph, phoneSeqLen);

            Assert.AreEqual(cpuResult.Length, burstResult.Length);
            for (int i = 0; i < cpuResult.Length; i++)
                Assert.AreEqual(cpuResult[i], burstResult[i], 1e-6f, $"index {i} mismatch");
        }

        [Test]
        public void BurstVersion_DestOverload_WorksWithLargerBuffer()
        {
            int tokenLen = 2;
            int[] word2ph = { 2, 1 };
            int phoneSeqLen = 3;

            float[] bertFlat = new float[BertAligner.EmbeddingDimension * tokenLen];
            for (int i = 0; i < bertFlat.Length; i++)
                bertFlat[i] = i * 0.01f;

            // ArrayPool scenario: dest is larger than needed
            int requiredLen = BertAligner.EmbeddingDimension * phoneSeqLen;
            float[] dest = new float[requiredLen + 512];
            BertAligner.AlignBertToPhonemesBurst(bertFlat, tokenLen, word2ph, phoneSeqLen, dest);

            float[] expected = BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen);
            for (int i = 0; i < requiredLen; i++)
                Assert.AreEqual(expected[i], dest[i], 1e-6f, $"index {i} mismatch");
        }

        [Test]
        public void ThrowsOnMismatchedSum()
        {
            int tokenLen = 2;
            int[] word2ph = { 2, 3 };  // 合計5
            int phoneSeqLen = 10;       // 5 != 10

            float[] bertFlat = new float[BertAligner.EmbeddingDimension * tokenLen];

            Assert.Throws<ArgumentException>(() =>
                BertAligner.AlignBertToPhonemes(bertFlat, tokenLen, word2ph, phoneSeqLen));
        }
    }
}
