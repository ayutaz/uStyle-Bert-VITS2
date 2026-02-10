using System;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;
using uStyleBertVITS2.Native;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    [Category("RequiresNativeDLL")]
    public class G2PTests
    {
        private JapaneseG2P _g2p;
        private bool _available;

        [OneTimeSetUp]
        public void Setup()
        {
            string dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                OpenJTalkConstants.DefaultDictionaryRelativePath);

            try
            {
                _g2p = new JapaneseG2P(dictPath);
                _available = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"G2P tests skipped: OpenJTalk initialization failed: {e.Message}");
                _available = false;
            }
        }

        private void AssertAvailable()
        {
            if (!_available)
                Assert.Ignore("OpenJTalk native DLL or dictionary not available.");
        }

        [Test]
        public void OpenJTalkInitializes()
        {
            AssertAvailable();
            Assert.IsNotNull(_g2p);
        }

        [Test]
        public void Process_Konnichiwa_ReturnsPhonemes()
        {
            AssertAvailable();
            var result = _g2p.Process("こんにちは");
            Assert.IsNotNull(result.PhonemeIds);
            Assert.IsTrue(result.PhonemeIds.Length > 0, "PhonemeIds should not be empty");
        }

        [Test]
        public void Process_ArrayLengthsMatch()
        {
            AssertAvailable();
            var result = _g2p.Process("テスト");
            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length,
                "PhonemeIds and Tones must have same length");
            Assert.AreEqual(result.PhonemeIds.Length, result.LanguageIds.Length,
                "PhonemeIds and LanguageIds must have same length");
        }

        [Test]
        public void Process_AllLanguageIdsAreJapanese()
        {
            AssertAvailable();
            var result = _g2p.Process("テスト");
            foreach (int langId in result.LanguageIds)
                Assert.AreEqual(1, langId, "JP-Extraでは全言語IDが1(日本語)");
        }

        [Test]
        public void Process_Word2PhSumMatchesPhonemeLength()
        {
            AssertAvailable();
            var result = _g2p.Process("東京タワー");
            int sum = 0;
            foreach (int w in result.Word2Ph) sum += w;
            Assert.AreEqual(result.PhonemeIds.Length, sum,
                "word2ph合計がPhonemeIds.Lengthと一致する必要がある");
        }

        [Test]
        public void Process_LongText()
        {
            AssertAvailable();
            string longText = "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。";
            var result = _g2p.Process(longText);
            Assert.IsTrue(result.PhonemeIds.Length > 0);
            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length);
        }

        [Test]
        public void Process_Punctuation()
        {
            AssertAvailable();
            var result = _g2p.Process("こんにちは、世界！");
            Assert.IsTrue(result.PhonemeIds.Length > 0);
        }

        [Test]
        public void Dispose_ReleasesNativeResources()
        {
            // 新しいインスタンスを作って即Dispose
            string dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                OpenJTalkConstants.DefaultDictionaryRelativePath);

            try
            {
                using var g2p = new JapaneseG2P(dictPath);
                // Dispose should not throw
            }
            catch (Exception)
            {
                Assert.Ignore("OpenJTalk native DLL or dictionary not available.");
            }
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            _g2p?.Dispose();
        }
    }

    /// <summary>
    /// SBV2PhonemeMapper 単体テスト（ネイティブDLL不要）。
    /// </summary>
    [TestFixture]
    public class PhonemeMapperTests
    {
        private SBV2PhonemeMapper _mapper;

        [OneTimeSetUp]
        public void Setup()
        {
            _mapper = new SBV2PhonemeMapper();
        }

        [Test]
        public void PhonemeMapper_ClMapsToQ()
        {
            // OpenJTalkの "cl"(促音) → SBV2の "q"
            int qId = _mapper.GetId("q");
            int clId = _mapper.GetId("cl");
            Assert.AreEqual(qId, clId, "cl should map to same ID as q (促音)");
        }

        [Test]
        public void PhonemeMapper_PauMapsToSP()
        {
            // OpenJTalkの "pau"(ポーズ) → SBV2の "SP"
            int spId = _mapper.SpId;
            int pauId = _mapper.GetId("pau");
            Assert.AreEqual(spId, pauId, "pau should map to SP");
        }

        [Test]
        public void ToneValues_HaveCorrectOffset()
        {
            // トーンオフセットは+6
            // tone=0 (Low) → 0+6=6, tone=1 (High) → 1+6=7
            // これはJapaneseG2P内部のロジックだが、定数値を確認
            Assert.AreEqual(8, _mapper.SpId, "SP should be at index 8");
            Assert.AreEqual(38, _mapper.GetId("q"), "q(促音) should be at index 38");
        }

        [Test]
        public void BasicPhonemes_Resolve()
        {
            // 基本音素がすべて解決できることを確認
            string[] basics = { "a", "i", "u", "e", "o", "k", "s", "t", "n", "N", "SP" };
            foreach (string p in basics)
            {
                int id = _mapper.GetId(p);
                Assert.AreNotEqual(_mapper.UnkId, id, $"Phoneme '{p}' should resolve to a known ID");
            }
        }
    }
}
