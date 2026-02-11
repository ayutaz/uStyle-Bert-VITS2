using System;
using System.Text;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;
using uStyleBertVITS2.Native;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// G2P パイプライン診断テスト。
    /// 音素ID, トーン, word2ph の正しさを検証する。
    /// </summary>
    [TestFixture]
    [Category("RequiresNativeDLL")]
    public class G2PDiagnosticTests
    {
        private JapaneseG2P _g2p;
        private SBV2PhonemeMapper _mapper;
        private bool _available;

        [OneTimeSetUp]
        public void Setup()
        {
            _mapper = new SBV2PhonemeMapper();
            string dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                OpenJTalkConstants.DefaultDictionaryRelativePath);

            try
            {
                _g2p = new JapaneseG2P(dictPath, _mapper);
                _available = true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning(
                    $"G2PDiagnostic tests skipped: {e.Message}");
                _available = false;
            }
        }

        private void AssertAvailable()
        {
            if (!_available)
                Assert.Ignore("OpenJTalk native DLL or dictionary not available.");
        }

        private void DumpG2PResult(string text, G2PResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== G2P Diagnostic: \"{text}\" ===");
            sb.AppendLine($"PhonemeIds.Length = {result.PhonemeIds.Length}");

            // 音素名への逆引き
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

            UnityEngine.Debug.Log(sb.ToString());
        }

        [Test]
        public void Konnichiwa_BasicDiagnostic()
        {
            AssertAvailable();
            var result = _g2p.Process("こんにちは");
            DumpG2PResult("こんにちは", result);

            // 基本検証
            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length);
            Assert.AreEqual(result.PhonemeIds.Length, result.LanguageIds.Length);

            // word2ph 合計一致
            int sum = 0;
            foreach (int w in result.Word2Ph) sum += w;
            Assert.AreEqual(result.PhonemeIds.Length, sum,
                "word2ph合計がPhonemeIds.Lengthと一致すること");

            // トーンが 6/7 のみ（日本語トーンオフセット+6適用済み）
            foreach (int t in result.Tones)
                Assert.IsTrue(t == 6 || t == 7, $"Tone should be 6 or 7 (JP offset), got {t}");

            // "ん" = 1音素 (N) であることを word2ph で検証
            // "こんにちは" の word2ph で "ん" (index 2, word2ph[2+1]) は 1 であるべき
            Assert.AreEqual(1, result.Word2Ph[2],
                "\"ん\" should map to 1 phoneme (N)");
        }

        [Test]
        public void TokyoTower_MixedScript()
        {
            AssertAvailable();
            var result = _g2p.Process("東京タワー");
            DumpG2PResult("東京タワー", result);

            Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length);

            int sum = 0;
            foreach (int w in result.Word2Ph) sum += w;
            Assert.AreEqual(result.PhonemeIds.Length, sum);

            foreach (int t in result.Tones)
                Assert.IsTrue(t == 6 || t == 7, $"Tone should be 6 or 7 (JP offset), got {t}");
        }

        [Test]
        public void SingleChar_Minimal()
        {
            AssertAvailable();
            var result = _g2p.Process("あ");
            DumpG2PResult("あ", result);

            // 最小ケース: SP + a + SP = 3音素
            Assert.IsTrue(result.PhonemeIds.Length >= 3,
                $"Single char should produce at least 3 phonemes, got {result.PhonemeIds.Length}");

            int sum = 0;
            foreach (int w in result.Word2Ph) sum += w;
            Assert.AreEqual(result.PhonemeIds.Length, sum);
        }

        [Test]
        public void LongText_NoTruncation()
        {
            AssertAvailable();
            string longText = "これは長い文章のテストです";
            var result = _g2p.Process(longText);
            DumpG2PResult(longText, result);

            // 20音素超を検証（旧maxSeqLen=20 による切り詰めがないこと）
            Assert.IsTrue(result.PhonemeIds.Length > 20,
                $"Long text should produce >20 phonemes, got {result.PhonemeIds.Length}");

            int sum = 0;
            foreach (int w in result.Word2Ph) sum += w;
            Assert.AreEqual(result.PhonemeIds.Length, sum);

            foreach (int t in result.Tones)
                Assert.IsTrue(t == 6 || t == 7, $"Tone should be 6 or 7 (JP offset), got {t}");
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            _g2p?.Dispose();
        }
    }

    /// <summary>
    /// ComputeTonesFromProsody 単体テスト（ネイティブDLL不要）。
    /// OpenJTalk の実出力データを使用してアルゴリズムの正確性を検証する。
    /// </summary>
    [TestFixture]
    public class ComputeTonesFromProsodyTests
    {
        [Test]
        public void Heiban_FirstMoraLow_RestHigh()
        {
            // 平板型5モーラ「こんにちは」(実データ): 第1モーラ=LOW, 以降=HIGH
            string[] ph = { "pau", "k", "o", "N", "n", "i", "ch", "i", "w", "a", "pau" };
            int[] a1 = { 0, -4, -4, -3, -2, -2, -1, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 0 };
            int[] a3 = { 0, 5, 5, 4, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "k should be LOW");
            Assert.AreEqual(0, tones[2], "o should be LOW");
            Assert.AreEqual(1, tones[3], "N should be HIGH");
            Assert.AreEqual(1, tones[9], "a should be HIGH");
        }

        [Test]
        public void Atamadaka_FirstHigh_RestLow()
        {
            // 頭高型 (手動構築データ): 第1モーラ=HIGH, 以降=LOW
            string[] ph = { "pau", "a", "m", "e", "pau" };
            int[] a1 = { 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 2, 2, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "a should be HIGH");
            Assert.AreEqual(0, tones[2], "m should be LOW");
            Assert.AreEqual(0, tones[3], "e should be LOW");
        }

        [Test]
        public void Atamadaka_WithNegativeShift()
        {
            // 頭高型「いのち」(実データ): FixPhoneTone {-1,0}→{0,1} シフト
            string[] ph = { "pau", "i", "n", "o", "ch", "i", "pau" };
            int[] a1 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "i should be HIGH (shifted from 0)");
            Assert.AreEqual(0, tones[2], "n should be LOW (shifted from -1)");
            Assert.AreEqual(0, tones[5], "i should be LOW");
        }

        [Test]
        public void Nakadaka_MiddleHigh()
        {
            // 中高型「おにいさん」(実データ): 第1LOW, 2nd HIGH, 3rd以降LOW
            string[] ph = { "pau", "o", "n", "i", "i", "s", "a", "N", "pau" };
            int[] a1 = { 0, -1, 0, 0, 1, 2, 2, 3, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 5, 0 };
            int[] a3 = { 0, 5, 4, 4, 3, 2, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "o should be LOW (1st mora)");
            Assert.AreEqual(1, tones[2], "n should be HIGH (2nd mora)");
            Assert.AreEqual(1, tones[3], "i should be HIGH (2nd mora)");
            Assert.AreEqual(0, tones[4], "i should be LOW (3rd mora, after drop)");
            Assert.AreEqual(0, tones[7], "N should be LOW (5th mora)");
        }

        [Test]
        public void Nakadaka_Imouto()
        {
            // 中高型「いもうと」(実データ): 第1LOW, 2-3rd HIGH, 4th LOW
            string[] ph = { "pau", "i", "m", "o", "o", "t", "o", "pau" };
            int[] a1 = { 0, -2, -1, -1, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "i should be LOW");
            Assert.AreEqual(1, tones[2], "m should be HIGH");
            Assert.AreEqual(1, tones[4], "o should be HIGH");
            Assert.AreEqual(0, tones[5], "t should be LOW (after drop)");
        }

        [Test]
        public void PhraseBoundary_Hash()
        {
            // 句境界(#)パス「なまえ」(実データ): a3==1 && a2Next==1 && IsVowelOrNOrCl
            string[] ph = { "pau", "n", "a", "m", "a", "e", "pau" };
            int[] a1 = { 0, 0, 0, 1, 1, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 1, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // 句1: n=1,a=1(shift), m=0,a=0(shift) → 句2: e=0
            Assert.AreEqual(1, tones[1], "n should be HIGH (phrase 1)");
            Assert.AreEqual(1, tones[2], "a should be HIGH (phrase 1)");
            Assert.AreEqual(0, tones[3], "m should be LOW (phrase 1)");
            Assert.AreEqual(0, tones[4], "a should be LOW (phrase 1, triggers #)");
            Assert.AreEqual(0, tones[5], "e should be LOW (phrase 2)");
        }

        [Test]
        public void SingleMora()
        {
            // 1モーラ「え」(実データ)
            string[] ph = { "pau", "e", "pau" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(0, tones[1], "e should be LOW (single mora)");
            Assert.AreEqual(0, tones[2], "pau should be 0");
        }

        [Test]
        public void SilAndPau_AlwaysZero()
        {
            string[] ph = { "sil", "a", "pau", "i", "sil" };
            int[] a1 = { 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 0, 1, 0 };
            int[] a3 = { 0, 1, 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "sil should be 0");
            Assert.AreEqual(0, tones[2], "pau should be 0");
            Assert.AreEqual(0, tones[4], "sil should be 0");
        }

        [Test]
        public void ConsecutiveSilences_NoError()
        {
            // 連続 sil/pau でクラッシュしないこと
            string[] ph = { "sil", "pau", "pau", "sil" };
            int[] a1 = { 0, 0, 0, 0 };
            int[] a2 = { 0, 0, 0, 0 };
            int[] a3 = { 0, 0, 0, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(4, tones.Length);
            foreach (int t in tones)
                Assert.AreEqual(0, t, "All silences should be 0");
        }

        [Test]
        public void VoicelessVowel_UpperCase()
        {
            // 無声化母音(大文字)「ひこうき」(実データ): I を含む
            string[] ph = { "pau", "h", "I", "k", "o", "o", "k", "i", "pau" };
            int[] a1 = { 0, -3, -3, -2, -2, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 4, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // 全て 0/1 に収まること + 平板型パターン
            Assert.AreEqual(0, tones[1], "h should be LOW (1st mora)");
            Assert.AreEqual(0, tones[2], "I should be LOW (1st mora, voiceless)");
            Assert.AreEqual(1, tones[3], "k should be HIGH (2nd mora)");
            Assert.AreEqual(1, tones[7], "i should be HIGH (4th mora)");
        }

        [Test]
        public void MultiPhrase_PauBoundary()
        {
            // 「こんにちは、さくらです。」(実データ): 2句の独立トーン計算
            string[] ph = { "pau", "k", "o", "N", "n", "i", "ch", "i", "w", "a",
                            "pau", "s", "a", "k", "u", "r", "a", "d", "e", "s", "U", "pau" };
            int[] a1 = { 0, -4, -4, -3, -2, -2, -1, -1, 0, 0,
                         0, -3, -3, -2, -2, -1, -1, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 5,
                         0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0 };
            int[] a3 = { 0, 5, 5, 4, 3, 3, 2, 2, 1, 1,
                         0, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);

            // 句1: 平板型
            Assert.AreEqual(0, tones[1], "k LOW (phrase 1)");
            Assert.AreEqual(1, tones[3], "N HIGH (phrase 1)");
            Assert.AreEqual(1, tones[9], "a HIGH (phrase 1)");
            // 句2: 中高型（4番目モーラで下降）
            Assert.AreEqual(0, tones[11], "s LOW (phrase 2, 1st mora)");
            Assert.AreEqual(1, tones[13], "k HIGH (phrase 2)");
            Assert.AreEqual(0, tones[19], "s LOW (phrase 2, after drop)");
        }

        [Test]
        public void ResultAlwaysBinary()
        {
            // FixPhoneTone シフトを含むデータで全て 0/1 に収まることを確認
            string[] ph = { "pau", "i", "n", "o", "ch", "i", "pau" };
            int[] a1 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            foreach (int t in tones)
                Assert.IsTrue(t == 0 || t == 1, $"Tone {t} out of range");
        }
    }

    /// <summary>
    /// PhonemeCharacterAligner 単体テスト（ネイティブDLL不要）。
    /// </summary>
    [TestFixture]
    public class PhonemeCharacterAlignerTests
    {
        [Test]
        public void Konnichiwa_NIsOneMora()
        {
            // "こんにちは": SP + k,o,N,n,i,ch,i,w,a + SP = 11 phonemes
            // word2ph = [CLS=1, こ=2, ん=1, に=2, ち=2, は=2, SEP=1]
            int phoneSeqLen = 11;
            int[] word2ph = PhonemeCharacterAligner.ComputeWord2Ph("こんにちは", phoneSeqLen);

            // [CLS] = 1
            Assert.AreEqual(1, word2ph[0], "[CLS] should be 1");
            // こ = 2 (k, o)
            Assert.AreEqual(2, word2ph[1], "こ should be 2 phonemes");
            // ん = 1 (N)
            Assert.AreEqual(1, word2ph[2], "ん should be 1 phoneme");
            // に = 2 (n, i)
            Assert.AreEqual(2, word2ph[3], "に should be 2 phonemes");
            // ち = 2 (ch, i)
            Assert.AreEqual(2, word2ph[4], "ち should be 2 phonemes");
            // は = 2 (w, a or h, a)
            Assert.AreEqual(2, word2ph[5], "は should be 2 phonemes");
            // [SEP] = 1
            Assert.AreEqual(1, word2ph[6], "[SEP] should be 1");

            // 合計
            int sum = 0;
            foreach (int w in word2ph) sum += w;
            Assert.AreEqual(phoneSeqLen, sum, "word2ph sum must match phoneSeqLen");
        }

        [Test]
        public void EmptyText_AllToCLS()
        {
            int phoneSeqLen = 2;
            int[] word2ph = PhonemeCharacterAligner.ComputeWord2Ph("", phoneSeqLen);
            Assert.AreEqual(phoneSeqLen, word2ph[0]);
        }

        [Test]
        public void SumAlwaysMatchesPhoneSeqLen()
        {
            string[] texts = { "あ", "テスト", "東京タワー", "こんにちは、世界！" };
            int[] seqLens = { 3, 8, 15, 20 };

            for (int t = 0; t < texts.Length; t++)
            {
                int[] word2ph = PhonemeCharacterAligner.ComputeWord2Ph(texts[t], seqLens[t]);
                int sum = 0;
                foreach (int w in word2ph) sum += w;
                Assert.AreEqual(seqLens[t], sum,
                    $"word2ph sum for \"{texts[t]}\" must match {seqLens[t]}");
            }
        }
    }
}
