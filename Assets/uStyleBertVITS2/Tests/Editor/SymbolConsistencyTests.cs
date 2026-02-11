using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests.Editor
{
    /// <summary>
    /// DefaultSymbols の Python 側定義との整合性テスト。
    /// SYMBOLS リスト不一致バグ (52→112要素) の再発防止。
    /// </summary>
    [TestFixture]
    public class SymbolConsistencyTests
    {
        [Test]
        public void DefaultSymbols_LengthIs112()
        {
            Assert.AreEqual(112, SBV2PhonemeMapper.DefaultSymbols.Length);
        }

        [Test]
        public void DefaultSymbols_PadAtIndex0()
        {
            Assert.AreEqual("_", SBV2PhonemeMapper.DefaultSymbols[0]);
        }

        [Test]
        public void DefaultSymbols_SPAtIndex110()
        {
            Assert.AreEqual("SP", SBV2PhonemeMapper.DefaultSymbols[110]);
        }

        [Test]
        public void DefaultSymbols_UNKAtIndex111()
        {
            Assert.AreEqual("UNK", SBV2PhonemeMapper.DefaultSymbols[111]);
        }

        [TestCase("N", 5)]
        [TestCase("a", 8)]
        [TestCase("k", 57)]
        [TestCase("q", 73)]
        [TestCase("sh", 77)]
        public void DefaultSymbols_KeyJPPhonemeIndices(string phoneme, int expectedIndex)
        {
            Assert.AreEqual(phoneme, SBV2PhonemeMapper.DefaultSymbols[expectedIndex]);
        }

        [Test]
        public void DefaultSymbols_AllJPSymbolsPresent()
        {
            // Python: JP_SYMBOLS の42要素
            string[] jpSymbols =
            {
                "N", "a", "a:", "b", "by", "ch", "d", "dy", "e", "e:",
                "f", "g", "gy", "h", "hy", "i", "i:", "j", "k", "ky",
                "m", "my", "n", "ny", "o", "o:", "p", "py", "q", "r",
                "ry", "s", "sh", "t", "ts", "ty", "u", "u:", "v", "w",
                "y", "z"
            };

            var symbolSet = new HashSet<string>(SBV2PhonemeMapper.DefaultSymbols);
            foreach (string jp in jpSymbols)
            {
                Assert.IsTrue(symbolSet.Contains(jp),
                    $"JP symbol '{jp}' is missing from DefaultSymbols");
            }
        }

        [Test]
        public void DefaultSymbols_PunctuationAtEnd()
        {
            string[] expectedTail = { "!", "?", "\u2026", ",", ".", "'", "-", "SP", "UNK" };
            int offset = SBV2PhonemeMapper.DefaultSymbols.Length - expectedTail.Length;

            for (int i = 0; i < expectedTail.Length; i++)
            {
                Assert.AreEqual(expectedTail[i], SBV2PhonemeMapper.DefaultSymbols[offset + i],
                    $"Mismatch at tail index {i} (absolute index {offset + i})");
            }
        }

        [Test]
        public void DefaultSymbols_NoDuplicates()
        {
            var seen = new HashSet<string>();
            foreach (string s in SBV2PhonemeMapper.DefaultSymbols)
            {
                Assert.IsTrue(seen.Add(s), $"Duplicate symbol found: '{s}'");
            }
        }

        /// <summary>
        /// OpenJTalk の無声母音 (大文字 A/I/U/E/O) が小文字にマッピングされ、
        /// UNK にならないことを検証する。
        /// </summary>
        [TestCase("A", "a")]
        [TestCase("I", "i")]
        [TestCase("U", "u")]
        [TestCase("E", "e")]
        [TestCase("O", "o")]
        public void UnvoicedVowels_MappedToLowercase(string openjtalk, string expected)
        {
            var mapper = new SBV2PhonemeMapper();
            int id = mapper.GetId(openjtalk);
            int expectedId = mapper.GetId(expected);
            Assert.AreEqual(expectedId, id,
                $"Unvoiced '{openjtalk}' should map same as '{expected}'");
            Assert.AreNotEqual(mapper.UnkId, id,
                $"Unvoiced '{openjtalk}' should NOT map to UNK");
        }

        /// <summary>
        /// SBV2 の句読点シンボルが正しいインデックスにあることを検証する。
        /// Python の PUNCTUATIONS = ["!", "?", "…", ",", ".", "'", "-"]
        /// </summary>
        [TestCase(",", 106)]
        [TestCase(".", 107)]
        [TestCase("!", 103)]
        [TestCase("?", 104)]
        [TestCase("'", 108)]
        [TestCase("-", 109)]
        public void PunctuationSymbols_CorrectIds(string symbol, int expectedId)
        {
            var mapper = new SBV2PhonemeMapper();
            int id = mapper.GetId(symbol);
            Assert.AreEqual(expectedId, id,
                $"Punctuation '{symbol}' should have ID {expectedId}");
        }

        [Test]
        public void DefaultSymbols_MiddleSectionIsSorted()
        {
            // Index 1..102 (the sorted union of ZH/JP/EN symbols) should be in ordinal order
            for (int i = 2; i <= 102; i++)
            {
                int cmp = string.Compare(
                    SBV2PhonemeMapper.DefaultSymbols[i - 1],
                    SBV2PhonemeMapper.DefaultSymbols[i],
                    System.StringComparison.Ordinal);
                Assert.Less(cmp, 0,
                    $"Symbols not sorted at index {i - 1}..{i}: " +
                    $"'{SBV2PhonemeMapper.DefaultSymbols[i - 1]}' >= '{SBV2PhonemeMapper.DefaultSymbols[i]}'");
            }
        }
    }
}
