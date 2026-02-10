using System.Collections.Generic;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class TokenizerTests
    {
        private SBV2Tokenizer _tokenizer;

        [OneTimeSetUp]
        public void Setup()
        {
            // テスト用の小さな語彙を構築
            var vocab = new Dictionary<string, int>
            {
                { "[PAD]", 0 },
                { "[CLS]", 1 },
                { "[SEP]", 2 },
                { "[UNK]", 3 },
                { "こ", 100 },
                { "ん", 101 },
                { "に", 102 },
                { "ち", 103 },
                { "は", 104 },
                { "テ", 105 },
                { "ス", 106 },
                { "ト", 107 },
                { "A", 200 },
                { "B", 201 },
            };
            _tokenizer = new SBV2Tokenizer(vocab);
        }

        [Test]
        public void EncodesBasicJapanese()
        {
            // "こんにちは" → [CLS] こ ん に ち は [SEP] = 7 tokens
            var (tokenIds, _) = _tokenizer.Encode("こんにちは");
            Assert.AreEqual(7, tokenIds.Length);
        }

        [Test]
        public void AddsSpecialTokens()
        {
            var (tokenIds, _) = _tokenizer.Encode("こんにちは");
            Assert.AreEqual(1, tokenIds[0], "先頭は[CLS]=1");
            Assert.AreEqual(2, tokenIds[tokenIds.Length - 1], "末尾は[SEP]=2");
        }

        [Test]
        public void HandlesUnknownCharacters()
        {
            // "X" は語彙にない → [UNK]=3
            var (tokenIds, _) = _tokenizer.Encode("X");
            Assert.AreEqual(3, tokenIds.Length); // [CLS] X [SEP]
            Assert.AreEqual(3, tokenIds[1], "未知文字は[UNK]=3");
        }

        [Test]
        public void AttentionMaskAllOnes()
        {
            var (_, attentionMask) = _tokenizer.Encode("こんにちは");
            for (int i = 0; i < attentionMask.Length; i++)
                Assert.AreEqual(1, attentionMask[i], $"attentionMask[{i}] should be 1");
        }

        [Test]
        public void EmptyInputReturnsClsSep()
        {
            var (tokenIds, attentionMask) = _tokenizer.Encode("");
            Assert.AreEqual(2, tokenIds.Length, "空文字列は[CLS][SEP]のみ");
            Assert.AreEqual(1, tokenIds[0]);
            Assert.AreEqual(2, tokenIds[1]);
            Assert.AreEqual(2, attentionMask.Length);
        }

        [Test]
        public void LongTextTokenizes()
        {
            // 100文字のテキストを生成
            string longText = new string('こ', 100);
            var (tokenIds, attentionMask) = _tokenizer.Encode(longText);

            Assert.AreEqual(102, tokenIds.Length, "100文字 + [CLS] + [SEP] = 102");
            Assert.AreEqual(102, attentionMask.Length);
            Assert.AreEqual(1, tokenIds[0]);
            Assert.AreEqual(2, tokenIds[101]);
        }

        [Test]
        public void CrossValidation_Konnichiwa()
        {
            var (tokenIds, _) = _tokenizer.Encode("こんにちは");

            // 期待値: [CLS]=1, こ=100, ん=101, に=102, ち=103, は=104, [SEP]=2
            int[] expected = { 1, 100, 101, 102, 103, 104, 2 };
            Assert.AreEqual(expected.Length, tokenIds.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], tokenIds[i], $"tokenIds[{i}]");
        }
    }
}
