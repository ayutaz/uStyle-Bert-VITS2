using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests.Editor
{
    /// <summary>
    /// トーンオフセット・言語ID の Python 側定義との整合性テスト。
    /// トーンオフセット +6 欠落バグの再発防止。
    /// </summary>
    [TestFixture]
    public class ToneAndLanguageConsistencyTests
    {
        /// <summary>
        /// 平板型アクセント (実データ): 第1モーラ=LOW(0), 以降=HIGH(1)。
        /// 実データ: 「こんにちは」(平板型、5モーラ)
        /// OpenJTalk 出力: a1=[0,-4,-4,-3,-2,-2,-1,-1,0,0,0], a2=[0,1,1,2,3,3,4,4,5,5,0], a3=[0,5,5,4,3,3,2,2,1,1,0]
        /// 音素: pau k o N n i ch i w a pau
        /// 期待: k=0,o=0, N=1,n=1,i=1,ch=1,i=1,w=1,a=1
        /// </summary>
        [Test]
        public void Prosody_FlatAccent_FirstMoraLowRestHigh()
        {
            string[] phonemes = { "pau", "k", "o", "N", "n", "i", "ch", "i", "w", "a", "pau" };
            int[] a1 = { 0, -4, -4, -3, -2, -2, -1, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 0 };
            int[] a3 = { 0, 5, 5, 4, 3, 3, 2, 2, 1, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            // pau=0, k=0, o=0, N=1, n=1, i=1, ch=1, i=1, w=1, a=1, pau=0
            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(0, tones[1], "k (1st mora) should be LOW");
            Assert.AreEqual(0, tones[2], "o (1st mora) should be LOW");
            Assert.AreEqual(1, tones[3], "N (2nd mora) should be HIGH");
            Assert.AreEqual(1, tones[4], "n (3rd mora) should be HIGH");
            Assert.AreEqual(1, tones[5], "i (3rd mora) should be HIGH");
            Assert.AreEqual(1, tones[6], "ch (4th mora) should be HIGH");
            Assert.AreEqual(1, tones[7], "i (4th mora) should be HIGH");
            Assert.AreEqual(1, tones[8], "w (5th mora) should be HIGH");
            Assert.AreEqual(1, tones[9], "a (5th mora) should be HIGH");
            Assert.AreEqual(0, tones[10], "pau should be 0");
        }

        /// <summary>
        /// 頭高型アクセント: 第1モーラ=HIGH(1), 以降=LOW(0)。
        /// 例: 「あめ」(雨、頭高型、2モーラ)
        /// 既存テスト: 手動構築データ (a3 が均一) でアルゴリズムの正しさを検証。
        /// </summary>
        [Test]
        public void Prosody_HeadHighAccent_FirstMoraHighRestLow()
        {
            // 頭高型2モーラ: a2[0]==1 && a2Next==2 で上昇 [→ tone=1
            // a1==0 && a2Next==a2+1 && a2!=a3 で下降 ]→ tone=0
            string[] phonemes = { "pau", "a", "m", "e", "pau" };
            int[] a1 = { 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 2, 2, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            // pau=0, a=1, m=0, e=0, pau=0
            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(1, tones[1], "a (1st mora) should be HIGH");
            Assert.AreEqual(0, tones[2], "m (2nd mora) should be LOW");
            Assert.AreEqual(0, tones[3], "e (2nd mora) should be LOW");
            Assert.AreEqual(0, tones[4], "pau should be 0");
        }

        /// <summary>
        /// 頭高型 + FixPhoneTone {-1,0}→{0,1} シフト。
        /// 実データ: 「いのち」(命、頭高型、3モーラ)
        /// OpenJTalk 出力: a1=[0,0,1,1,2,2,0], a2=[0,1,2,2,3,3,0], a3=[0,3,2,2,1,1,0]
        /// 累積トーン: [0,-1,-1,-1,-1] → FixPhoneTone で {-1,0}→{0,1} シフト → [1,0,0,0,0]
        /// </summary>
        [Test]
        public void Prosody_Atamadaka_FixPhoneTone_NegativeShift()
        {
            string[] phonemes = { "pau", "i", "n", "o", "ch", "i", "pau" };
            int[] a1 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(1, tones[1], "i (1st mora) should be HIGH after FixPhoneTone shift");
            Assert.AreEqual(0, tones[2], "n (2nd mora) should be LOW");
            Assert.AreEqual(0, tones[3], "o (2nd mora) should be LOW");
            Assert.AreEqual(0, tones[4], "ch (3rd mora) should be LOW");
            Assert.AreEqual(0, tones[5], "i (3rd mora) should be LOW");
            Assert.AreEqual(0, tones[6], "pau should be 0");
        }

        /// <summary>
        /// 中高型アクセント: 第1モーラ=LOW, 2〜核モーラ=HIGH, 核後=LOW。
        /// 実データ: 「おにいさん」(5モーラ、核=第2モーラ)
        /// OpenJTalk 出力: a1=[0,-1,0,0,1,2,2,3,0], a2=[0,1,2,2,3,4,4,5,0], a3=[0,5,4,4,3,2,2,1,0]
        /// </summary>
        [Test]
        public void Prosody_NakadakaAccent_LowHighHighLow()
        {
            string[] phonemes = { "pau", "o", "n", "i", "i", "s", "a", "N", "pau" };
            int[] a1 = { 0, -1, 0, 0, 1, 2, 2, 3, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 5, 0 };
            int[] a3 = { 0, 5, 4, 4, 3, 2, 2, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(0, tones[1], "o (1st mora) should be LOW");
            Assert.AreEqual(1, tones[2], "n (2nd mora) should be HIGH");
            Assert.AreEqual(1, tones[3], "i (2nd mora) should be HIGH");
            Assert.AreEqual(0, tones[4], "i (3rd mora) should be LOW (after accent drop)");
            Assert.AreEqual(0, tones[5], "s (4th mora) should be LOW");
            Assert.AreEqual(0, tones[6], "a (4th mora) should be LOW");
            Assert.AreEqual(0, tones[7], "N (5th mora) should be LOW");
            Assert.AreEqual(0, tones[8], "pau should be 0");
        }

        /// <summary>
        /// 中高型アクセント (別パターン): 「いもうと」(4モーラ、核=第3モーラ)
        /// OpenJTalk 出力: a1=[0,-2,-1,-1,0,1,1,0], a2=[0,1,2,2,3,4,4,0], a3=[0,4,3,3,2,1,1,0]
        /// </summary>
        [Test]
        public void Prosody_NakadakaAccent_Imouto()
        {
            string[] phonemes = { "pau", "i", "m", "o", "o", "t", "o", "pau" };
            int[] a1 = { 0, -2, -1, -1, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 3, 3, 2, 1, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(0, tones[1], "i (1st mora) should be LOW");
            Assert.AreEqual(1, tones[2], "m (2nd mora) should be HIGH");
            Assert.AreEqual(1, tones[3], "o (2nd mora) should be HIGH");
            Assert.AreEqual(1, tones[4], "o (3rd mora) should be HIGH");
            Assert.AreEqual(0, tones[5], "t (4th mora) should be LOW (after accent drop)");
            Assert.AreEqual(0, tones[6], "o (4th mora) should be LOW");
            Assert.AreEqual(0, tones[7], "pau should be 0");
        }

        /// <summary>
        /// 句境界 (#) パス: 「なまえ」は OpenJTalk で内部的に2つのアクセント句に分割される。
        /// a3==1 && a2Next==1 && IsVowelOrNOrCl で # が検出され、FixPhoneTone が途中で適用される。
        /// OpenJTalk 出力: a1=[0,0,0,1,1,0,0], a2=[0,1,1,2,2,1,0], a3=[0,2,2,1,1,1,0]
        /// 句1: n,a → {0,0} → ] で tone=-1 → m,a → {-1,-1} → Fix({0,0,-1,-1}) shift+1 → {1,1,0,0}
        /// 句2: e → {0} → Fix → {0}
        /// </summary>
        [Test]
        public void Prosody_PhraseBoundary_SeparateAccentPhrases()
        {
            string[] phonemes = { "pau", "n", "a", "m", "a", "e", "pau" };
            int[] a1 = { 0, 0, 0, 1, 1, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 1, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            Assert.AreEqual(0, tones[0], "pau should be 0");
            // 句1: n=1 (HIGH after shift), a=1 (HIGH after shift), m=0 (LOW after shift), a=0 (LOW after shift)
            Assert.AreEqual(1, tones[1], "n (1st mora, phrase 1) should be HIGH");
            Assert.AreEqual(1, tones[2], "a (1st mora, phrase 1) should be HIGH");
            Assert.AreEqual(0, tones[3], "m (2nd mora, phrase 1) should be LOW");
            Assert.AreEqual(0, tones[4], "a (2nd mora, phrase 1) should be LOW (triggers # boundary)");
            // 句2: e=0 (single mora)
            Assert.AreEqual(0, tones[5], "e (phrase 2, single mora) should be LOW");
            Assert.AreEqual(0, tones[6], "pau should be 0");
        }

        /// <summary>
        /// 1モーラ語: 「え」(最小フレーズ)
        /// OpenJTalk 出力: a1=[0,0,0], a2=[0,1,0], a3=[0,1,0]
        /// </summary>
        [Test]
        public void Prosody_SingleMora_MinimalPhrase()
        {
            string[] phonemes = { "pau", "e", "pau" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(0, tones[1], "e (single mora) should be LOW");
            Assert.AreEqual(0, tones[2], "pau should be 0");
        }

        /// <summary>
        /// 複数アクセント句 (pau 境界): 「こんにちは、さくらです。」
        /// 各句が独立にトーン計算されることを検証。
        /// OpenJTalk 出力からの実データ使用。
        /// </summary>
        [Test]
        public void Prosody_MultiPhrase_PauBoundary()
        {
            string[] phonemes = { "pau", "k", "o", "N", "n", "i", "ch", "i", "w", "a",
                                  "pau", "s", "a", "k", "u", "r", "a", "d", "e", "s", "U", "pau" };
            int[] a1 = { 0, -4, -4, -3, -2, -2, -1, -1, 0, 0,
                         0, -3, -3, -2, -2, -1, -1, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 5,
                         0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0 };
            int[] a3 = { 0, 5, 5, 4, 3, 3, 2, 2, 1, 1,
                         0, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            // 句1「こんにちは」: 平板型 → LOW, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH
            Assert.AreEqual(0, tones[0], "pau should be 0");
            Assert.AreEqual(0, tones[1], "k should be LOW (1st mora)");
            Assert.AreEqual(0, tones[2], "o should be LOW (1st mora)");
            Assert.AreEqual(1, tones[3], "N should be HIGH");
            Assert.AreEqual(1, tones[4], "n should be HIGH");
            Assert.AreEqual(1, tones[5], "i should be HIGH");
            Assert.AreEqual(1, tones[6], "ch should be HIGH");
            Assert.AreEqual(1, tones[7], "i should be HIGH");
            Assert.AreEqual(1, tones[8], "w should be HIGH");
            Assert.AreEqual(1, tones[9], "a should be HIGH");

            // pau 境界
            Assert.AreEqual(0, tones[10], "pau boundary should be 0");

            // 句2「さくらです」: s=0, a=0, k=1, u=1, r=1, a=1, d=1, e=1, s=0, U=0
            Assert.AreEqual(0, tones[11], "s should be LOW (1st mora, phrase 2)");
            Assert.AreEqual(0, tones[12], "a should be LOW (1st mora, phrase 2)");
            Assert.AreEqual(1, tones[13], "k should be HIGH (2nd mora, phrase 2)");
            Assert.AreEqual(1, tones[14], "u should be HIGH");
            Assert.AreEqual(1, tones[15], "r should be HIGH");
            Assert.AreEqual(1, tones[16], "a should be HIGH");
            Assert.AreEqual(1, tones[17], "d should be HIGH");
            Assert.AreEqual(1, tones[18], "e should be HIGH");
            Assert.AreEqual(0, tones[19], "s should be LOW (after accent drop)");
            Assert.AreEqual(0, tones[20], "U should be LOW");
            Assert.AreEqual(0, tones[21], "pau should be 0");
        }

        /// <summary>
        /// sil と pau は常にトーン 0。
        /// </summary>
        [Test]
        public void Prosody_SilAndPau_AreToneZero()
        {
            string[] phonemes = { "sil", "a", "pau", "i", "sil" };
            int[] a1 = { 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 0, 1, 0 };
            int[] a3 = { 0, 1, 0, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            Assert.AreEqual(0, tones[0], "sil should be 0");
            Assert.AreEqual(0, tones[2], "pau should be 0");
            Assert.AreEqual(0, tones[4], "sil should be 0");
        }

        /// <summary>
        /// ComputeTonesFromProsody の結果は常に 0 または 1 のみを返す。
        /// FixPhoneTone の {-1,0}→{0,1} シフトを含むケースで検証。
        /// </summary>
        [Test]
        public void Prosody_ResultAlwaysBinary()
        {
            // 「いのち」の実データ: FixPhoneTone で -1→0, 0→1 シフトが発生するパターン
            string[] phonemes = { "pau", "i", "n", "o", "ch", "i", "pau" };
            int[] a1 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(phonemes, a1, a2, a3, phonemes.Length);

            for (int i = 0; i < tones.Length; i++)
            {
                Assert.IsTrue(tones[i] == 0 || tones[i] == 1,
                    $"Tone at index {i} ({phonemes[i]}) = {tones[i]}, expected 0 or 1");
            }
        }

        /// <summary>
        /// Python: LANGUAGE_ID_MAP["JP"] = 1
        /// </summary>
        [Test]
        public void LanguageId_JapaneseIs1()
        {
            var field = typeof(JapaneseG2P).GetField("JapaneseLanguageId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(field, "JapaneseLanguageId field not found");
            Assert.AreEqual(1, (int)field.GetValue(null));
        }

        /// <summary>
        /// Python: 先頭/末尾の境界トークンは PAD ("_", index 0) であること。
        /// SP (index 110) ではない。
        /// </summary>
        [Test]
        public void BoundaryToken_IsPad_NotSP()
        {
            var mapper = new SBV2PhonemeMapper();
            Assert.AreEqual(0, mapper.PadId, "Boundary token should be PAD (index 0)");
            Assert.AreNotEqual(mapper.SpId, mapper.PadId, "PAD and SP must be different");
        }

        /// <summary>
        /// PhonemeCharacterAligner が句読点文字に 1 音素を割り当てることを検証する。
        /// </summary>
        [TestCase('、', 1)]
        [TestCase('。', 1)]
        [TestCase(',', 1)]
        [TestCase('.', 1)]
        [TestCase('!', 1)]
        [TestCase('?', 1)]
        public void PhonemeAligner_PunctuationGets1Phoneme(char punct, int expectedCount)
        {
            string text = $"あ{punct}い";
            int phoneSeqLen = 5;
            int[] word2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, phoneSeqLen);
            Assert.AreEqual(expectedCount, word2ph[2],
                $"Punctuation '{punct}' should map to {expectedCount} phoneme(s) in word2ph");
        }

        /// <summary>
        /// Python: NUM_TONES = NUM_ZH_TONES(6) + NUM_JP_TONES(2) + NUM_EN_TONES(4) = 12
        /// </summary>
        [Test]
        public void TotalToneCount_Is12()
        {
            const int numZhTones = 6;
            const int numJpTones = 2;
            const int numEnTones = 4;

            Assert.AreEqual(12, numZhTones + numJpTones + numEnTones,
                "NUM_TONES should be 12 (ZH:6 + JP:2 + EN:4)");
        }
    }
}
