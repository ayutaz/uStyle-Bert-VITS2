using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests.Editor
{
    /// <summary>
    /// TextNormalizer.Normalize() の単体テスト。
    /// </summary>
    [TestFixture]
    public class TextNormalizerTests
    {
        [Test]
        public void FullWidthAlphanumeric_ConvertedToHalfWidth()
        {
            Assert.AreEqual("A123Z", TextNormalizer.Normalize("\uFF21\uFF11\uFF12\uFF13\uFF3A"));
        }

        [Test]
        public void FullWidthSpace_ConvertedToHalfWidth()
        {
            Assert.AreEqual("\u3053\u3093\u306B\u3061\u306F \u4E16\u754C",
                TextNormalizer.Normalize("\u3053\u3093\u306B\u3061\u306F\u3000\u4E16\u754C"));
        }

        [Test]
        public void MultipleSpaces_CollapsedToOne()
        {
            Assert.AreEqual("hello world", TextNormalizer.Normalize("hello   world"));
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        public void NullAndEmpty_ReturnEmpty(string input, string expected)
        {
            Assert.AreEqual(expected, TextNormalizer.Normalize(input));
        }

        [Test]
        public void LeadingTrailingSpaces_Trimmed()
        {
            Assert.AreEqual("hello", TextNormalizer.Normalize("  hello  "));
        }

        [Test]
        public void FullWidthSpaces_CollapsedToOne()
        {
            Assert.AreEqual("\u3042 \u3044",
                TextNormalizer.Normalize("\u3042\u3000\u3000\u3044"));
        }

        [Test]
        public void MixedSpaces_CollapsedToOne()
        {
            Assert.AreEqual("\u3042 \u3044",
                TextNormalizer.Normalize("\u3042\u3000 \u3044"));
        }
    }
}
