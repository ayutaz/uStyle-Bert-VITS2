using System;
using System.Linq;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// Phase 3: word2ph 計算と句読点マッピングのテスト。
    /// Section 1: PhonemeCharacterAligner.ComputeWord2Ph (pure C#, ネイティブDLL不要)
    /// Section 2: DotNetG2PJapaneseG2P 句読点テスト
    /// Section 3: TextNormalizer 非干渉テスト (pure C#)
    /// Section 4: sil/pau スキップ・出力構造検証
    /// </summary>
    [TestFixture]
    public class Word2PhAndPunctuationTests
    {
        #region Section 1: PhonemeCharacterAligner.ComputeWord2Ph (30+ cases)

        // --- Sum(word2ph) == phoneSeqLen ---

        [Test]
        public void ComputeWord2Ph_Konnichiwa_SumEqualsPhoneSeqLen()
        {
            int phoneSeqLen = 12;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("こんにちは", phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, w2ph.Sum(),
                "Sum(word2ph) must equal phoneSeqLen");
        }

        [Test]
        public void ComputeWord2Ph_SingleChar_SumEqualsPhoneSeqLen()
        {
            int phoneSeqLen = 5;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あ", phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, w2ph.Sum());
        }

        [Test]
        public void ComputeWord2Ph_LongText_SumEqualsPhoneSeqLen()
        {
            string text = "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。";
            int phoneSeqLen = 100;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, w2ph.Sum());
        }

        [Test]
        public void ComputeWord2Ph_AllPunctuation_SumEqualsPhoneSeqLen()
        {
            string text = "、。！？";
            int phoneSeqLen = 10;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, w2ph.Sum());
        }

        [Test]
        [TestCase("あいうえお", 12)]
        [TestCase("かきくけこ", 15)]
        [TestCase("東京タワー", 20)]
        [TestCase("テスト", 10)]
        [TestCase("ん", 4)]
        public void ComputeWord2Ph_VariousInputs_SumEqualsPhoneSeqLen(string text, int phoneSeqLen)
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, w2ph.Sum(),
                $"[{text}] Sum(word2ph) must equal phoneSeqLen ({phoneSeqLen})");
        }

        // --- [CLS] gets 1, [SEP] gets 1 ---

        [Test]
        public void ComputeWord2Ph_CLSGets1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あ", 5);
            Assert.AreEqual(1, w2ph[0], "[CLS] should get 1");
        }

        [Test]
        public void ComputeWord2Ph_SEPGets1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あ", 5);
            Assert.AreEqual(1, w2ph[w2ph.Length - 1], "[SEP] should get 1");
        }

        [Test]
        public void ComputeWord2Ph_Konnichiwa_CLSAndSEPAre1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("こんにちは", 12);
            Assert.AreEqual(1, w2ph[0], "[CLS] should be 1");
            Assert.AreEqual(1, w2ph[w2ph.Length - 1], "[SEP] should be 1");
        }

        // --- Array length = text.Length + 2 ---

        [Test]
        public void ComputeWord2Ph_ArrayLength_EqualsTextLenPlus2()
        {
            string text = "こんにちは";
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, 12);
            Assert.AreEqual(text.Length + 2, w2ph.Length,
                "word2ph length should be text.Length + 2 ([CLS] + text + [SEP])");
        }

        [Test]
        public void ComputeWord2Ph_SingleChar_ArrayLengthIs3()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あ", 4);
            Assert.AreEqual(3, w2ph.Length, "Single char → [CLS] + 1 + [SEP] = 3");
        }

        // --- Hiragana character mapping ---

        [Test]
        public void ComputeWord2Ph_HiraganaKa_Maps2Phonemes()
        {
            // "か" → k,a → 2 phonemes; total = CLS(1) + か(2) + SEP(1) = 4
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("か", 4);
            Assert.AreEqual(1, w2ph[0], "[CLS]=1");
            Assert.AreEqual(2, w2ph[1], "か should map to 2 phonemes");
            Assert.AreEqual(1, w2ph[2], "[SEP]=1");
        }

        [Test]
        public void ComputeWord2Ph_HiraganaA_Maps1Phoneme()
        {
            // "あ" → a → 1 phoneme; total = CLS(1) + あ(1) + SEP(1) = 3
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あ", 3);
            Assert.AreEqual(1, w2ph[0], "[CLS]=1");
            Assert.AreEqual(1, w2ph[1], "あ should map to 1 phoneme");
            Assert.AreEqual(1, w2ph[2], "[SEP]=1");
        }

        [Test]
        public void ComputeWord2Ph_HiraganaN_Maps1Phoneme()
        {
            // "ん" → N → 1 phoneme; total = CLS(1) + ん(1) + SEP(1) = 3
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("ん", 3);
            Assert.AreEqual(1, w2ph[1], "ん should map to 1 phoneme (N)");
        }

        [Test]
        public void ComputeWord2Ph_HiraganaShi_Maps2Phonemes()
        {
            // "し" → sh,i → 2 phonemes; total = CLS(1) + し(2) + SEP(1) = 4
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("し", 4);
            Assert.AreEqual(2, w2ph[1], "し should map to 2 phonemes (sh,i)");
        }

        [Test]
        public void ComputeWord2Ph_SmallTsu_Maps1Phoneme()
        {
            // "っ" → q → 1 phoneme; total = CLS(1) + っ(1) + SEP(1) = 3
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("っ", 3);
            Assert.AreEqual(1, w2ph[1], "っ should map to 1 phoneme (q)");
        }

        [Test]
        public void ComputeWord2Ph_LongVowelMark_Maps1Phoneme()
        {
            // "ー" → 1 phoneme; total = CLS(1) + ー(1) + SEP(1) = 3
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("ー", 3);
            Assert.AreEqual(1, w2ph[1], "ー should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_MultipleHiragana_EachGetsCorrectCount()
        {
            // "あか" → a(1) + k,a(2) = 3 for text chars; total = CLS(1) + 3 + SEP(1) = 5
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あか", 5);
            Assert.AreEqual(1, w2ph[0], "[CLS]=1");
            Assert.AreEqual(1, w2ph[1], "あ=1");
            Assert.AreEqual(2, w2ph[2], "か=2");
            Assert.AreEqual(1, w2ph[3], "[SEP]=1");
        }

        [Test]
        public void ComputeWord2Ph_VowelSeries_AllMap1()
        {
            // "あいうえお" → each 1 phoneme = 5; total = CLS(1)+5+SEP(1)=7
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("あいうえお", 7);
            for (int i = 1; i <= 5; i++)
            {
                Assert.AreEqual(1, w2ph[i],
                    $"Vowel at index {i} should map to 1 phoneme");
            }
        }

        [Test]
        public void ComputeWord2Ph_KaRow_AllMap2()
        {
            // "かきくけこ" → each 2 phonemes = 10; total = CLS(1)+10+SEP(1)=12
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("かきくけこ", 12);
            for (int i = 1; i <= 5; i++)
            {
                Assert.AreEqual(2, w2ph[i],
                    $"Ka-row char at index {i} should map to 2 phonemes");
            }
        }

        // --- Katakana → hiragana conversion ---

        [Test]
        public void ComputeWord2Ph_KatakanaKa_Maps2Phonemes()
        {
            // "カ" → katakana → hiragana "か" → 2 phonemes
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("カ", 4);
            Assert.AreEqual(2, w2ph[1], "カ (katakana) should map to 2 phonemes like か");
        }

        [Test]
        public void ComputeWord2Ph_KatakanaA_Maps1Phoneme()
        {
            // "ア" → katakana → hiragana "あ" → 1 phoneme
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("ア", 3);
            Assert.AreEqual(1, w2ph[1], "ア (katakana) should map to 1 phoneme like あ");
        }

        [Test]
        public void ComputeWord2Ph_KatakanaN_Maps1Phoneme()
        {
            // "ン" → katakana → hiragana "ん" → 1 phoneme
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("ン", 3);
            Assert.AreEqual(1, w2ph[1], "ン (katakana) should map to 1 phoneme like ん");
        }

        [Test]
        public void ComputeWord2Ph_MixedKataHira_SameResult()
        {
            // "カか" → both map to 2 phonemes; total = CLS(1)+2+2+SEP(1)=6
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("カか", 6);
            Assert.AreEqual(2, w2ph[1], "カ should map to 2");
            Assert.AreEqual(2, w2ph[2], "か should map to 2");
        }

        // --- Kanji/unknown characters get proportional distribution ---

        [Test]
        public void ComputeWord2Ph_SingleKanji_GetsRemainingPhonemes()
        {
            // "日" → unknown; total = CLS(1) + unknown + SEP(1)
            int phoneSeqLen = 5;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("日", phoneSeqLen);
            int expected = phoneSeqLen - 2; // subtract CLS and SEP
            Assert.AreEqual(expected, w2ph[1],
                "Single kanji should get all remaining phonemes");
        }

        [Test]
        public void ComputeWord2Ph_TwoKanji_ProportionalDistribution()
        {
            // "日本" → 2 unknown chars; phoneSeqLen=8 → remaining = 8-2 = 6, 6/2 = 3 each
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("日本", 8);
            Assert.AreEqual(3, w2ph[1], "First kanji should get 3");
            Assert.AreEqual(3, w2ph[2], "Second kanji should get 3");
        }

        [Test]
        public void ComputeWord2Ph_KanjiWithOddRemainder_ExtraGoesToFirst()
        {
            // "日本" → 2 unknown chars; phoneSeqLen=9 → remaining = 9-2 = 7, 7/2 = 3 remainder 1
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("日本", 9);
            Assert.AreEqual(4, w2ph[1], "First kanji gets extra from remainder");
            Assert.AreEqual(3, w2ph[2], "Second kanji gets base allocation");
        }

        [Test]
        public void ComputeWord2Ph_MixedKanjiAndHiragana()
        {
            // "日の" → 日(unknown) + の(2); total = CLS(1) + ? + 2 + SEP(1) = phoneSeqLen
            int phoneSeqLen = 8;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("日の", phoneSeqLen);
            Assert.AreEqual(1, w2ph[0], "[CLS]=1");
            Assert.AreEqual(1, w2ph[w2ph.Length - 1], "[SEP]=1");
            Assert.AreEqual(2, w2ph[2], "の should map to 2 phonemes");
            // 日 gets remaining: 8 - 1 - 2 - 1 = 4
            Assert.AreEqual(4, w2ph[1], "日 should get remaining phonemes");
        }

        // --- Punctuation characters get 1 each ---

        [Test]
        public void ComputeWord2Ph_Comma_Maps1()
        {
            // "、" → 1 phoneme; total = CLS(1)+1+SEP(1)=3
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("、", 3);
            Assert.AreEqual(1, w2ph[1], "、 should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_Period_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("。", 3);
            Assert.AreEqual(1, w2ph[1], "。 should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_ExclamationMark_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("！", 3);
            Assert.AreEqual(1, w2ph[1], "！ should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_QuestionMark_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("？", 3);
            Assert.AreEqual(1, w2ph[1], "？ should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_Ellipsis_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("\u2026", 3);
            Assert.AreEqual(1, w2ph[1], "… should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_Nakaguro_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("・", 3);
            Assert.AreEqual(1, w2ph[1], "・ should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_ASCIIComma_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(",", 3);
            Assert.AreEqual(1, w2ph[1], "ASCII , should map to 1 phoneme");
        }

        [Test]
        public void ComputeWord2Ph_ASCIIPeriod_Maps1()
        {
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(".", 3);
            Assert.AreEqual(1, w2ph[1], "ASCII . should map to 1 phoneme");
        }

        // --- Edge cases ---

        [Test]
        public void ComputeWord2Ph_EmptyString_CLSGetsAll()
        {
            int phoneSeqLen = 4;
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("", phoneSeqLen);
            Assert.AreEqual(2, w2ph.Length, "Empty text → [CLS] + [SEP] = 2");
            Assert.AreEqual(phoneSeqLen, w2ph[0],
                "Empty text: [CLS] should get all phoneSeqLen");
        }

        [Test]
        public void ComputeWord2Ph_VeryLongText_SumStillMatches()
        {
            string text = new string('あ', 200);
            int phoneSeqLen = text.Length + 2; // 1 phoneme per あ + CLS + SEP
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, w2ph.Sum());
            Assert.AreEqual(text.Length + 2, w2ph.Length);
        }

        [Test]
        public void ComputeWord2Ph_AllPunctuationText_AllGet1()
        {
            string text = "、。！？";
            // 4 punct (each 1) + CLS(1) + SEP(1) = 6
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, 6);
            Assert.AreEqual(1, w2ph[0], "[CLS]=1");
            for (int i = 1; i <= 4; i++)
                Assert.AreEqual(1, w2ph[i], $"Punctuation at index {i} should be 1");
            Assert.AreEqual(1, w2ph[5], "[SEP]=1");
        }

        [Test]
        public void ComputeWord2Ph_SmallKana_Map1Each()
        {
            // "ぁぃぅぇぉ" → small kana, each 1 phoneme; total = CLS(1)+5+SEP(1)=7
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("ぁぃぅぇぉ", 7);
            for (int i = 1; i <= 5; i++)
                Assert.AreEqual(1, w2ph[i], $"Small kana at index {i} should map to 1");
        }

        [Test]
        public void ComputeWord2Ph_YouonSmallKana_Map1Each()
        {
            // "ゃゅょ" → small kana for youon, each 1; total = CLS(1)+3+SEP(1)=5
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("ゃゅょ", 5);
            for (int i = 1; i <= 3; i++)
                Assert.AreEqual(1, w2ph[i], $"Youon kana at index {i} should map to 1");
        }

        [Test]
        public void ComputeWord2Ph_Wo_Maps1Phoneme()
        {
            // "を" is special: maps to just "o" → 1 phoneme
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("を", 3);
            Assert.AreEqual(1, w2ph[1], "を should map to 1 phoneme (o)");
        }

        [Test]
        public void ComputeWord2Ph_CorrectionApplied_WhenEstimateExceedsPhoneSeqLen()
        {
            // Force a case where known phoneme estimates exceed phoneSeqLen
            // "かきくけこ" known = CLS(1)+10+SEP(1) = 12, but phoneSeqLen = 10
            int[] w2ph = PhonemeCharacterAligner.ComputeWord2Ph("かきくけこ", 10);
            Assert.AreEqual(10, w2ph.Sum(),
                "Correction should bring total to phoneSeqLen even when estimates exceed it");
        }

        #endregion

        #region Section 2: DotNetG2P punctuation tests

        private DotNetG2PJapaneseG2P _g2p;
        private bool _g2pAvailable;
        private SBV2PhonemeMapper _mapper;

        [OneTimeSetUp]
        public void SetupDotNetG2P()
        {
            _mapper = new SBV2PhonemeMapper();

            string dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "uStyleBertVITS2/OpenJTalkDic");

            try
            {
                _g2p = new DotNetG2PJapaneseG2P(dictPath, _mapper);
                _g2pAvailable = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"Word2PhAndPunctuationTests DotNetG2P tests skipped: {e.Message}");
                _g2pAvailable = false;
            }
        }

        private void AssertG2PAvailable()
        {
            if (!_g2pAvailable)
                Assert.Ignore("dot-net-g2p dictionary not available.");
        }

        // --- Punctuation symbols appear in output phoneme IDs ---

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_CommaInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京、大阪");
            int commaId = _mapper.GetId(",");
            Assert.IsTrue(result.PhonemeIds.Contains(commaId),
                "Output should contain comma (,) phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_PeriodInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京です。");
            int periodId = _mapper.GetId(".");
            Assert.IsTrue(result.PhonemeIds.Contains(periodId),
                "Output should contain period (.) phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_ExclamationInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("すごい！");
            int excId = _mapper.GetId("!");
            Assert.IsTrue(result.PhonemeIds.Contains(excId),
                "Output should contain exclamation (!) phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_QuestionMarkInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("本当？");
            int qId = _mapper.GetId("?");
            Assert.IsTrue(result.PhonemeIds.Contains(qId),
                "Output should contain question mark (?) phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_EllipsisInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("あ\u2026");
            int ellipsisId = _mapper.GetId("\u2026");
            Assert.IsTrue(result.PhonemeIds.Contains(ellipsisId),
                "Output should contain ellipsis phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_NakaguroMapsToComma()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京・大阪");
            int commaId = _mapper.GetId(",");
            Assert.IsTrue(result.PhonemeIds.Contains(commaId),
                "・ should map to comma (,) phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_FullwidthExclamation_MapsToExclamation()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("すごい！");
            int excId = _mapper.GetId("!");
            Assert.IsTrue(result.PhonemeIds.Contains(excId),
                "！ (fullwidth) should produce ! phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_FullwidthQuestion_MapsToQuestion()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("本当？");
            int qId = _mapper.GetId("?");
            Assert.IsTrue(result.PhonemeIds.Contains(qId),
                "？ (fullwidth) should produce ? phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_FullwidthComma_MapsToComma()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京，大阪");
            int commaId = _mapper.GetId(",");
            Assert.IsTrue(result.PhonemeIds.Contains(commaId),
                "， (fullwidth) should produce , phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_FullwidthPeriod_MapsToPeriod()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京です．");
            int periodId = _mapper.GetId(".");
            Assert.IsTrue(result.PhonemeIds.Contains(periodId),
                "． (fullwidth) should produce . phoneme ID");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_MultiplePunctuation_AllPresent()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京、大阪。すごい！本当？");
            int commaId = _mapper.GetId(",");
            int periodId = _mapper.GetId(".");
            int excId = _mapper.GetId("!");
            int qId = _mapper.GetId("?");

            Assert.IsTrue(result.PhonemeIds.Contains(commaId), "Should contain comma");
            Assert.IsTrue(result.PhonemeIds.Contains(periodId), "Should contain period");
            Assert.IsTrue(result.PhonemeIds.Contains(excId), "Should contain exclamation");
            Assert.IsTrue(result.PhonemeIds.Contains(qId), "Should contain question");
        }

        // --- Section 4: sil/pau skip logic and output structure ---

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_FirstPauSkipped()
        {
            AssertG2PAvailable();
            // Process text that would produce pau at the beginning
            var result = _g2p.Process("こんにちは");
            int spId = _mapper.SpId;

            // SP (which pau maps to) should not appear in output
            // because first pau is skipped and sil/silB/silE are always skipped
            Assert.IsFalse(result.PhonemeIds.Contains(spId),
                "SP should not appear in output (first pau skipped, sil always skipped)");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_SilNeverInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("こんにちは、世界");
            int spId = _mapper.SpId;

            // sil/silB/silE all map to SP in SBV2PhonemeMapper, but should be skipped
            // Only pau after the first can produce punctuation symbols
            // SP itself should never appear because sil variants are always skipped
            // and first pau is skipped
            for (int i = 0; i < result.PhonemeIds.Length; i++)
            {
                Assert.AreNotEqual(spId, result.PhonemeIds[i],
                    $"SP (id={spId}) should not appear at index {i} — sil/silB/silE are always skipped");
            }
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_OutputStartsAndEndsWithPAD()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("テスト");
            int padId = _mapper.PadId;

            Assert.AreEqual(padId, result.PhonemeIds[0],
                "First phoneme should be PAD (_)");
            Assert.AreEqual(padId, result.PhonemeIds[result.PhonemeIds.Length - 1],
                "Last phoneme should be PAD (_)");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_OutputStartsAndEndsWithPAD_WithPunctuation()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("すごい！");
            int padId = _mapper.PadId;

            Assert.AreEqual(padId, result.PhonemeIds[0],
                "First phoneme should be PAD");
            Assert.AreEqual(padId, result.PhonemeIds[result.PhonemeIds.Length - 1],
                "Last phoneme should be PAD");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_NoUNKInOutput()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("こんにちは");
            int unkId = _mapper.UnkId;

            for (int i = 0; i < result.PhonemeIds.Length; i++)
            {
                Assert.AreNotEqual(unkId, result.PhonemeIds[i],
                    $"UNK should not appear at index {i}");
            }
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_Word2PhSumMatchesPhonemeIds()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京タワーは高い。");
            int sum = result.Word2Ph.Sum();
            Assert.AreEqual(result.PhonemeIds.Length, sum,
                "word2ph sum must equal PhonemeIds.Length");
        }

        [Test]
        [Category("DotNetG2P")]
        public void DotNetG2P_Word2PhAllPositive()
        {
            AssertG2PAvailable();
            var result = _g2p.Process("東京タワーは高い。");
            foreach (int w in result.Word2Ph)
            {
                Assert.IsTrue(w > 0,
                    $"All word2ph values must be positive, got {w}");
            }
        }

        [OneTimeTearDown]
        public void TeardownDotNetG2P()
        {
            _g2p?.Dispose();
        }

        #endregion

        #region Section 3: TextNormalizer non-interference tests (pure C#)

        [Test]
        public void TextNormalizer_FullwidthToHalfwidth_Converts()
        {
            // "！" (U+FF01) → "!" (U+0021)
            string result = TextNormalizer.Normalize("！");
            Assert.AreEqual("!", result,
                "TextNormalizer should convert fullwidth ! to halfwidth");
        }

        [Test]
        public void TextNormalizer_FullwidthQuestion_Converts()
        {
            // "？" (U+FF1F) → "?" (U+003F)
            string result = TextNormalizer.Normalize("？");
            Assert.AreEqual("?", result);
        }

        [Test]
        public void TextNormalizer_JapanesePunctuation_NotConverted()
        {
            // "、" and "。" are NOT fullwidth ASCII (they're native Japanese)
            // so they should pass through unchanged
            string result = TextNormalizer.Normalize("、。");
            Assert.AreEqual("、。", result,
                "Japanese punctuation 、。 should not be converted");
        }

        [Test]
        public void TextNormalizer_DoubleNormalize_Idempotent()
        {
            string text = "こんにちは！世界？";
            string once = TextNormalizer.Normalize(text);
            string twice = TextNormalizer.Normalize(once);
            Assert.AreEqual(once, twice,
                "Double normalization should produce the same result (idempotent)");
        }

        [Test]
        public void TextNormalizer_PureHiragana_Unchanged()
        {
            string text = "こんにちは";
            string result = TextNormalizer.Normalize(text);
            Assert.AreEqual(text, result,
                "Pure hiragana should not be changed");
        }

        [Test]
        public void TextNormalizer_FullwidthAlphanumeric_Converts()
        {
            // "Ａ" (U+FF21) → "A" (U+0041)
            string result = TextNormalizer.Normalize("\uFF21\uFF22\uFF23");
            Assert.AreEqual("ABC", result);
        }

        [Test]
        public void TextNormalizer_FullwidthSpace_BecomesHalfwidth()
        {
            string result = TextNormalizer.Normalize("あ\u3000い");
            Assert.AreEqual("あ い", result,
                "Fullwidth space should become halfwidth space");
        }

        [Test]
        public void TextNormalizer_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, TextNormalizer.Normalize(""));
            Assert.AreEqual(string.Empty, TextNormalizer.Normalize(null));
        }

        [Test]
        public void TextNormalizer_ConsecutiveSpaces_Collapsed()
        {
            string result = TextNormalizer.Normalize("あ   い");
            Assert.AreEqual("あ い", result,
                "Consecutive spaces should be collapsed to one");
        }

        [Test]
        public void TextNormalizer_LeadingTrailingSpaces_Trimmed()
        {
            string result = TextNormalizer.Normalize("  あ  ");
            Assert.AreEqual("あ", result,
                "Leading and trailing spaces should be removed");
        }

        #endregion
    }
}
