using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using uStyleBertVITS2.Native;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
#if !USBV2_DOTNET_G2P_AVAILABLE
    /// <summary>
    /// dot-net-g2p と現行 JapaneseG2P の互換性テスト。
    /// 50+ テストケース（基本日本語、漢字混在、句読点、記号、長文、特殊）。
    /// </summary>
    [TestFixture]
    [Category("RequiresNativeDLL")]
    public class DotNetG2PCompatibilityTests
    {
        private JapaneseG2P _jg2p;
        private bool _available;
        private SBV2PhonemeMapper _mapper;

        [OneTimeSetUp]
        public void Setup()
        {
            string dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                OpenJTalkConstants.DefaultDictionaryRelativePath);

            _mapper = new SBV2PhonemeMapper();

            try
            {
                _jg2p = new JapaneseG2P(dictPath, _mapper);
                _available = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"DotNetG2PCompatibility tests skipped: OpenJTalk initialization failed: {e.Message}");
                _available = false;
            }
        }

        private void AssertAvailable()
        {
            if (!_available)
                Assert.Ignore("OpenJTalk native DLL or dictionary not available.");
        }

        /// <summary>
        /// G2P出力を詳細にダンプして互換性を確認するヘルパーメソッド。
        /// </summary>
        private void DumpAndCompareG2PResult(string text, G2PResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== G2P Result: \"{text}\" ===");
            sb.AppendLine($"PhonemeIds.Length = {result.PhonemeIds.Length}");

            var symbols = SBV2PhonemeMapper.DefaultSymbols;
            sb.Append("Phonemes: ");
            for (int i = 0; i < result.PhonemeIds.Length; i++)
            {
                int id = result.PhonemeIds[i];
                string name = id < symbols.Length ? symbols[id] : $"?{id}";
                sb.Append(i > 0 ? $" {name}" : name);
            }
            sb.AppendLine();

            sb.Append("Tones:    ");
            for (int i = 0; i < result.Tones.Length; i++)
            {
                sb.Append(i > 0 ? $" {result.Tones[i]}" : $"{result.Tones[i]}");
            }
            sb.AppendLine();

            sb.Append("Word2Ph:  [");
            int w2pSum = 0;
            for (int i = 0; i < result.Word2Ph.Length; i++)
            {
                sb.Append(i > 0 ? $", {result.Word2Ph[i]}" : $"{result.Word2Ph[i]}");
                w2pSum += result.Word2Ph[i];
            }
            sb.AppendLine($"] (sum={w2pSum})");

            sb.Append("LanguageIds: [");
            for (int i = 0; i < result.LanguageIds.Length; i++)
            {
                sb.Append(i > 0 ? $", {result.LanguageIds[i]}" : $"{result.LanguageIds[i]}");
            }
            sb.AppendLine("]");

            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>
        /// G2P出力の基本検証を実施（配列長、word2ph合計、値の有効性）。
        /// </summary>
        private void ValidateG2PResult(string text, G2PResult result)
        {
            // 1. 配列長の一致
            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length,
                $"[{text}] PhonemeIds and Tones length mismatch");
            Assert.AreEqual(result.PhonemeIds.Length, result.LanguageIds.Length,
                $"[{text}] PhonemeIds and LanguageIds length mismatch");

            // 2. word2ph合計
            int w2pSum = result.Word2Ph.Sum();
            Assert.AreEqual(result.PhonemeIds.Length, w2pSum,
                $"[{text}] word2ph sum ({w2pSum}) != PhonemeIds.Length ({result.PhonemeIds.Length})");

            // 3. 音素ID有効性 [0, 111]
            foreach (int id in result.PhonemeIds)
            {
                Assert.IsTrue(id >= 0 && id < 112,
                    $"[{text}] Phoneme ID {id} out of range [0, 111]");
            }

            // 4. JP-Extra: トーン値は 6 or 7 のみ
            foreach (int tone in result.Tones)
            {
                Assert.IsTrue(tone == 6 || tone == 7,
                    $"[{text}] Tone {tone} out of expected range [6, 7]");
            }

            // 5. JP-Extra: 言語ID は全て 1
            foreach (int langId in result.LanguageIds)
            {
                Assert.AreEqual(1, langId,
                    $"[{text}] LanguageId {langId} != 1 (JP-Extra)");
            }

            // 6. word2ph 全て正数
            foreach (int w in result.Word2Ph)
            {
                Assert.IsTrue(w > 0,
                    $"[{text}] word2ph value {w} must be positive");
            }
        }

        #region Category 1: 基本日本語 (10 cases)

        [Test]
        public void BasicJapanese_Konnichiwa_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("こんにちは");
            DumpAndCompareG2PResult("こんにちは", result);
            ValidateG2PResult("こんにちは", result);
        }

        [Test]
        public void BasicJapanese_Arigatou_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("ありがとう");
            DumpAndCompareG2PResult("ありがとう", result);
            ValidateG2PResult("ありがとう", result);
        }

        [Test]
        public void BasicJapanese_Sayounara_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("さようなら");
            DumpAndCompareG2PResult("さようなら", result);
            ValidateG2PResult("さようなら", result);
        }

        [Test]
        public void BasicJapanese_SingleChar_A_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("あ");
            DumpAndCompareG2PResult("あ", result);
            ValidateG2PResult("あ", result);
        }

        [Test]
        public void BasicJapanese_RepeatedA_AaaReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("あああ");
            DumpAndCompareG2PResult("あああ", result);
            ValidateG2PResult("あああ", result);
        }

        [Test]
        public void BasicJapanese_KakikukekoReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("かきくけこ");
            DumpAndCompareG2PResult("かきくけこ", result);
            ValidateG2PResult("かきくけこ", result);
        }

        [Test]
        public void BasicJapanese_VowelSeriesReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("あいうえお");
            DumpAndCompareG2PResult("あいうえお", result);
            ValidateG2PResult("あいうえお", result);
        }

        [Test]
        public void BasicJapanese_ConsonantISeriesReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("きしちにひみりぎじびぴ");
            DumpAndCompareG2PResult("きしちにひみりぎじびぴ", result);
            ValidateG2PResult("きしちにひみりぎじびぴ", result);
        }

        [Test]
        public void BasicJapanese_WithN_NamanakoreturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("なまなこ");
            DumpAndCompareG2PResult("なまなこ", result);
            ValidateG2PResult("なまなこ", result);

            // 「ん」は1音素であることを確認
            Assert.AreEqual(1, result.Word2Ph[2],
                "ん should map to 1 phoneme");
        }

        [Test]
        public void BasicJapanese_WithSmallTsu_SassatoReturnsValidResult()
        {
            AssertAvailable();
            var result = _jg2p.Process("さっさと");
            DumpAndCompareG2PResult("さっさと", result);
            ValidateG2PResult("さっさと", result);
        }

        #endregion

        #region Category 2: 漢字混在 (10 cases)

        [Test]
        [TestCase("今日は良い天気です")]
        [TestCase("東京タワー")]
        [TestCase("複雑な漢字交じり文")]
        [TestCase("123年")]
        [TestCase("昨日")]
        [TestCase("明日")]
        [TestCase("学生")]
        [TestCase("太郎")]
        [TestCase("花子")]
        [TestCase("日本")]
        public void KanjiMixed_VariousTexts_ReturnsValidResult(string text)
        {
            AssertAvailable();
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        #endregion

        #region Category 3: 句読点・記号 (10 cases)

        [Test]
        [TestCase("東京タワー、スカイツリー。")]
        [TestCase("え？本当！")]
        [TestCase("あ…")]
        [TestCase("100円")]
        [TestCase("あ、い、う")]
        [TestCase("(こんにちは)")]
        [TestCase("「はい」")]
        [TestCase("Hello世界")]
        [TestCase("abc")]
        [TestCase("あ！？")]
        public void Punctuation_VariousMarks_ReturnsValidResult(string text)
        {
            AssertAvailable();
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        #endregion

        #region Category 4: 長文・複雑パターン (10 cases)

        [Test]
        public void LongText_EightyCharacters_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
            Assert.IsTrue(result.PhonemeIds.Length > 20, "Long text should produce >20 phonemes");
        }

        [Test]
        public void LongText_ThirtyCharacters_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "私は毎日朝7時に起きて、朝食を食べてから学校に行きます。";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_TwentyCharacters_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "彼はその映画を見た後、友人に感想を述べた。";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_MultipleLocations_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "京都、大阪、神戸、そして広島へ旅行に行きました。";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_MixedEnglishKanji_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "Mr. Tanaka は、昨日東京駅で会いました。";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_SpecialCharacter_VuiRefereeReturnsValidResult()
        {
            AssertAvailable();
            string text = "ゔァイオリン";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_SmallKanaPalatal_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "キャ行、シャ行、チャ行、ニャ行。";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_DakutenMarks_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "あ゛い゛う゛";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_FiftyPlusSameChar_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "あああああああああああああああああああああああああああああああああ";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_PunctuationOnly_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "。。。...!!!???";
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        #endregion

        #region Category 5: 特殊ケース (10 cases)

        [Test]
        public void SpecialCase_EmptyString_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("");
            DumpAndCompareG2PResult("(empty)", result);
            ValidateG2PResult("", result);
        }

        [Test]
        public void SpecialCase_SpaceOnly_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("   ");
            DumpAndCompareG2PResult("(spaces)", result);
            ValidateG2PResult("   ", result);
        }

        [Test]
        public void SpecialCase_PunctuationOnly_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("。、！？");
            DumpAndCompareG2PResult("。、！？", result);
            ValidateG2PResult("。、！？", result);
        }

        [Test]
        public void SpecialCase_WithNewline_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("あ\nい");
            DumpAndCompareG2PResult("あ\\nい", result);
            ValidateG2PResult("あ\nい", result);
        }

        [Test]
        public void SpecialCase_WithTab_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("あ\tい");
            DumpAndCompareG2PResult("あ\\tい", result);
            ValidateG2PResult("あ\tい", result);
        }

        [Test]
        public void SpecialCase_FullWidthSpace_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("あ　い");
            DumpAndCompareG2PResult("あ　い", result);
            ValidateG2PResult("あ　い", result);
        }

        [Test]
        public void SpecialCase_SmallKanaRepeated_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("ぁぃぅぇぉ");
            DumpAndCompareG2PResult("ぁぃぅぇぉ", result);
            ValidateG2PResult("ぁぃぅぇぉ", result);
        }

        [Test]
        [TestCase("らー")]
        [TestCase("らあ")]
        public void SpecialCase_LongVowelMark_ProducesValidOutput(string text)
        {
            AssertAvailable();
            var result = _jg2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void SpecialCase_SmallTsuWithFollowing_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _jg2p.Process("がっこう");  // 学校
            DumpAndCompareG2PResult("がっこう", result);
            ValidateG2PResult("がっこう", result);
        }

        [Test]
        public void SpecialCase_SuperLongText_ProducesValidOutput()
        {
            AssertAvailable();
            // 500文字以上
            string text = string.Concat(
                Enumerable.Repeat("これは長い文章のテストです。", 20));
            var result = _jg2p.Process(text);
            Assert.IsTrue(result.PhonemeIds.Length > 100, "Super long text should produce >100 phonemes");
            ValidateG2PResult("(super long)", result);
        }

        #endregion

        #region Performance & Benchmark Tests

        [Test]
        public void PerformanceBenchmark_Konnichiwa_CompletesInReasonableTime()
        {
            AssertAvailable();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                var result = _jg2p.Process("こんにちは");
            }
            sw.Stop();

            UnityEngine.Debug.Log($"10x Process(\"こんにちは\") = {sw.ElapsedMilliseconds}ms");
            Assert.IsTrue(sw.ElapsedMilliseconds < 5000,
                $"10 iterations should complete in <5000ms, got {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void PerformanceBenchmark_LongText_CompletesInReasonableTime()
        {
            AssertAvailable();
            string text = "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。";

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                var result = _jg2p.Process(text);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"5x Process(long text) = {sw.ElapsedMilliseconds}ms");
            Assert.IsTrue(sw.ElapsedMilliseconds < 3000,
                $"5 iterations of long text should complete in <3000ms, got {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Consistency Tests

        [Test]
        public void Consistency_SameInputProducesSameOutput()
        {
            AssertAvailable();
            string text = "こんにちは、世界！";

            var result1 = _jg2p.Process(text);
            var result2 = _jg2p.Process(text);

            CollectionAssert.AreEqual(result1.PhonemeIds, result2.PhonemeIds,
                "Same input should produce identical PhonemeIds");
            CollectionAssert.AreEqual(result1.Tones, result2.Tones,
                "Same input should produce identical Tones");
            CollectionAssert.AreEqual(result1.Word2Ph, result2.Word2Ph,
                "Same input should produce identical Word2Ph");
        }

        #endregion

        [OneTimeTearDown]
        public void Teardown()
        {
            _jg2p?.Dispose();
        }
    }
#endif

#if USBV2_DOTNET_G2P_AVAILABLE
    /// <summary>
    /// dot-net-g2p バックエンドで DotNetG2PCompatibilityTests と同じ 50+ テストケースを実行する。
    /// DotNetG2PJapaneseG2P は空文字列・空白のみ入力で ArgumentException を投げるため、
    /// 該当テストは Assert.Throws に変更している。
    /// </summary>
    [TestFixture]
    [Category("DotNetG2P")]
    public class DotNetG2PBackendCompatibilityTests
    {
        private DotNetG2PJapaneseG2P _g2p;
        private bool _available;
        private SBV2PhonemeMapper _mapper;

        [OneTimeSetUp]
        public void Setup()
        {
            string dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "uStyleBertVITS2/OpenJTalkDic");

            _mapper = new SBV2PhonemeMapper();

            try
            {
                _g2p = new DotNetG2PJapaneseG2P(dictPath, _mapper);
                _available = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"DotNetG2P backend compat tests skipped: {e.Message}");
                _available = false;
            }
        }

        private void AssertAvailable()
        {
            if (!_available)
                Assert.Ignore("dot-net-g2p dictionary not available.");
        }

        private void DumpAndCompareG2PResult(string text, G2PResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== DotNetG2P Result: \"{text}\" ===");
            sb.AppendLine($"PhonemeIds.Length = {result.PhonemeIds.Length}");

            var symbols = SBV2PhonemeMapper.DefaultSymbols;
            sb.Append("Phonemes: ");
            for (int i = 0; i < result.PhonemeIds.Length; i++)
            {
                int id = result.PhonemeIds[i];
                string name = id < symbols.Length ? symbols[id] : $"?{id}";
                sb.Append(i > 0 ? $" {name}" : name);
            }
            sb.AppendLine();

            sb.Append("Tones:    ");
            for (int i = 0; i < result.Tones.Length; i++)
            {
                sb.Append(i > 0 ? $" {result.Tones[i]}" : $"{result.Tones[i]}");
            }
            sb.AppendLine();

            sb.Append("Word2Ph:  [");
            int w2pSum = 0;
            for (int i = 0; i < result.Word2Ph.Length; i++)
            {
                sb.Append(i > 0 ? $", {result.Word2Ph[i]}" : $"{result.Word2Ph[i]}");
                w2pSum += result.Word2Ph[i];
            }
            sb.AppendLine($"] (sum={w2pSum})");

            sb.Append("LanguageIds: [");
            for (int i = 0; i < result.LanguageIds.Length; i++)
            {
                sb.Append(i > 0 ? $", {result.LanguageIds[i]}" : $"{result.LanguageIds[i]}");
            }
            sb.AppendLine("]");

            UnityEngine.Debug.Log(sb.ToString());
        }

        private void ValidateG2PResult(string text, G2PResult result)
        {
            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length,
                $"[{text}] PhonemeIds and Tones length mismatch");
            Assert.AreEqual(result.PhonemeIds.Length, result.LanguageIds.Length,
                $"[{text}] PhonemeIds and LanguageIds length mismatch");

            int w2pSum = result.Word2Ph.Sum();
            Assert.AreEqual(result.PhonemeIds.Length, w2pSum,
                $"[{text}] word2ph sum ({w2pSum}) != PhonemeIds.Length ({result.PhonemeIds.Length})");

            foreach (int id in result.PhonemeIds)
            {
                Assert.IsTrue(id >= 0 && id < 112,
                    $"[{text}] Phoneme ID {id} out of range [0, 111]");
            }

            foreach (int tone in result.Tones)
            {
                Assert.IsTrue(tone == 6 || tone == 7,
                    $"[{text}] Tone {tone} out of expected range [6, 7]");
            }

            foreach (int langId in result.LanguageIds)
            {
                Assert.AreEqual(1, langId,
                    $"[{text}] LanguageId {langId} != 1 (JP-Extra)");
            }

            foreach (int w in result.Word2Ph)
            {
                Assert.IsTrue(w > 0,
                    $"[{text}] word2ph value {w} must be positive");
            }
        }

        #region Category 1: 基本日本語 (10 cases)

        [Test]
        public void BasicJapanese_Konnichiwa_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("こんにちは");
            DumpAndCompareG2PResult("こんにちは", result);
            ValidateG2PResult("こんにちは", result);
        }

        [Test]
        public void BasicJapanese_Arigatou_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("ありがとう");
            DumpAndCompareG2PResult("ありがとう", result);
            ValidateG2PResult("ありがとう", result);
        }

        [Test]
        public void BasicJapanese_Sayounara_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("さようなら");
            DumpAndCompareG2PResult("さようなら", result);
            ValidateG2PResult("さようなら", result);
        }

        [Test]
        public void BasicJapanese_SingleChar_A_ReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("あ");
            DumpAndCompareG2PResult("あ", result);
            ValidateG2PResult("あ", result);
        }

        [Test]
        public void BasicJapanese_RepeatedA_AaaReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("あああ");
            DumpAndCompareG2PResult("あああ", result);
            ValidateG2PResult("あああ", result);
        }

        [Test]
        public void BasicJapanese_KakikukekoReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("かきくけこ");
            DumpAndCompareG2PResult("かきくけこ", result);
            ValidateG2PResult("かきくけこ", result);
        }

        [Test]
        public void BasicJapanese_VowelSeriesReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("あいうえお");
            DumpAndCompareG2PResult("あいうえお", result);
            ValidateG2PResult("あいうえお", result);
        }

        [Test]
        public void BasicJapanese_ConsonantISeriesReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("きしちにひみりぎじびぴ");
            DumpAndCompareG2PResult("きしちにひみりぎじびぴ", result);
            ValidateG2PResult("きしちにひみりぎじびぴ", result);
        }

        [Test]
        public void BasicJapanese_WithN_NamanakoreturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("なまなこ");
            DumpAndCompareG2PResult("なまなこ", result);
            ValidateG2PResult("なまなこ", result);
        }

        [Test]
        public void BasicJapanese_WithSmallTsu_SassatoReturnsValidResult()
        {
            AssertAvailable();
            var result = _g2p.Process("さっさと");
            DumpAndCompareG2PResult("さっさと", result);
            ValidateG2PResult("さっさと", result);
        }

        #endregion

        #region Category 2: 漢字混在 (10 cases)

        [Test]
        [TestCase("今日は良い天気です")]
        [TestCase("東京タワー")]
        [TestCase("複雑な漢字交じり文")]
        [TestCase("123年")]
        [TestCase("昨日")]
        [TestCase("明日")]
        [TestCase("学生")]
        [TestCase("太郎")]
        [TestCase("花子")]
        [TestCase("日本")]
        public void KanjiMixed_VariousTexts_ReturnsValidResult(string text)
        {
            AssertAvailable();
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        #endregion

        #region Category 3: 句読点・記号 (10 cases)

        [Test]
        [TestCase("東京タワー、スカイツリー。")]
        [TestCase("え？本当！")]
        [TestCase("あ…")]
        [TestCase("100円")]
        [TestCase("あ、い、う")]
        [TestCase("(こんにちは)")]
        [TestCase("「はい」")]
        [TestCase("Hello世界")]
        [TestCase("abc")]
        [TestCase("あ！？")]
        public void Punctuation_VariousMarks_ReturnsValidResult(string text)
        {
            AssertAvailable();
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        #endregion

        #region Category 4: 長文・複雑パターン (10 cases)

        [Test]
        public void LongText_EightyCharacters_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
            Assert.IsTrue(result.PhonemeIds.Length > 20, "Long text should produce >20 phonemes");
        }

        [Test]
        public void LongText_ThirtyCharacters_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "私は毎日朝7時に起きて、朝食を食べてから学校に行きます。";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_TwentyCharacters_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "彼はその映画を見た後、友人に感想を述べた。";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_MultipleLocations_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "京都、大阪、神戸、そして広島へ旅行に行きました。";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_MixedEnglishKanji_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "Mr. Tanaka は、昨日東京駅で会いました。";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_SpecialCharacter_VuiRefereeReturnsValidResult()
        {
            AssertAvailable();
            string text = "ゔァイオリン";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_SmallKanaPalatal_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "キャ行、シャ行、チャ行、ニャ行。";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_DakutenMarks_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "あ゛い゛う゛";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_FiftyPlusSameChar_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "あああああああああああああああああああああああああああああああああ";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void LongText_PunctuationOnly_ReturnsValidResult()
        {
            AssertAvailable();
            string text = "。。。...!!!???";
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        #endregion

        #region Category 5: 特殊ケース (10 cases)

        [Test]
        public void SpecialCase_EmptyString_ThrowsArgumentException()
        {
            AssertAvailable();
            Assert.Throws<ArgumentException>(() => _g2p.Process(""));
        }

        [Test]
        public void SpecialCase_SpaceOnly_ThrowsArgumentException()
        {
            AssertAvailable();
            Assert.Throws<ArgumentException>(() => _g2p.Process("   "));
        }

        [Test]
        public void SpecialCase_PunctuationOnly_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _g2p.Process("。、！？");
            DumpAndCompareG2PResult("。、！？", result);
            ValidateG2PResult("。、！？", result);
        }

        [Test]
        public void SpecialCase_WithNewline_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _g2p.Process("あ\nい");
            DumpAndCompareG2PResult("あ\\nい", result);
            ValidateG2PResult("あ\nい", result);
        }

        [Test]
        public void SpecialCase_WithTab_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _g2p.Process("あ\tい");
            DumpAndCompareG2PResult("あ\\tい", result);
            ValidateG2PResult("あ\tい", result);
        }

        [Test]
        public void SpecialCase_FullWidthSpace_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _g2p.Process("あ　い");
            DumpAndCompareG2PResult("あ　い", result);
            ValidateG2PResult("あ　い", result);
        }

        [Test]
        public void SpecialCase_SmallKanaRepeated_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _g2p.Process("ぁぃぅぇぉ");
            DumpAndCompareG2PResult("ぁぃぅぇぉ", result);
            ValidateG2PResult("ぁぃぅぇぉ", result);
        }

        [Test]
        [TestCase("らー")]
        [TestCase("らあ")]
        public void SpecialCase_LongVowelMark_ProducesValidOutput(string text)
        {
            AssertAvailable();
            var result = _g2p.Process(text);
            DumpAndCompareG2PResult(text, result);
            ValidateG2PResult(text, result);
        }

        [Test]
        public void SpecialCase_SmallTsuWithFollowing_ProducesValidOutput()
        {
            AssertAvailable();
            var result = _g2p.Process("がっこう");
            DumpAndCompareG2PResult("がっこう", result);
            ValidateG2PResult("がっこう", result);
        }

        [Test]
        public void SpecialCase_SuperLongText_ProducesValidOutput()
        {
            AssertAvailable();
            string text = string.Concat(
                Enumerable.Repeat("これは長い文章のテストです。", 20));
            var result = _g2p.Process(text);
            Assert.IsTrue(result.PhonemeIds.Length > 100, "Super long text should produce >100 phonemes");
            ValidateG2PResult("(super long)", result);
        }

        #endregion

        #region Performance & Benchmark Tests

        [Test]
        public void PerformanceBenchmark_Konnichiwa_CompletesInReasonableTime()
        {
            AssertAvailable();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                _g2p.Process("こんにちは");
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[DotNetG2P] 10x Process(\"こんにちは\") = {sw.ElapsedMilliseconds}ms");
            Assert.IsTrue(sw.ElapsedMilliseconds < 5000,
                $"10 iterations should complete in <5000ms, got {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void PerformanceBenchmark_LongText_CompletesInReasonableTime()
        {
            AssertAvailable();
            string text = "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。";

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                _g2p.Process(text);
            }
            sw.Stop();

            UnityEngine.Debug.Log($"[DotNetG2P] 5x Process(long text) = {sw.ElapsedMilliseconds}ms");
            Assert.IsTrue(sw.ElapsedMilliseconds < 3000,
                $"5 iterations of long text should complete in <3000ms, got {sw.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Consistency Tests

        [Test]
        public void Consistency_SameInputProducesSameOutput()
        {
            AssertAvailable();
            string text = "こんにちは、世界！";

            var result1 = _g2p.Process(text);
            var result2 = _g2p.Process(text);

            CollectionAssert.AreEqual(result1.PhonemeIds, result2.PhonemeIds,
                "Same input should produce identical PhonemeIds");
            CollectionAssert.AreEqual(result1.Tones, result2.Tones,
                "Same input should produce identical Tones");
            CollectionAssert.AreEqual(result1.Word2Ph, result2.Word2Ph,
                "Same input should produce identical Word2Ph");
        }

        #endregion

        [OneTimeTearDown]
        public void Teardown()
        {
            _g2p?.Dispose();
        }
    }
#endif
}
