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
