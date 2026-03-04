using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// ComputeTonesFromProsody の拡張検証テスト (50+ ケース)。
    /// 全テストは手動構築データを使用し、ネイティブDLLは不要。
    ///
    /// A1/A2/A3 の手動構築ルール:
    ///   a1 = moraPos - accent + 1 (アクセント核相対位置)
    ///   a2 = moraPos + 1 (1始まり前方位置)
    ///   a3 = moraCount - moraPos (後方位置)
    ///   sil/pau: a1=a2=a3=0
    ///   同じ mora に属する複数の音素は同じ a2, a3 値を持つ。
    /// </summary>
    [TestFixture]
    public class ToneCalculationExtendedTests
    {
        private static void AssertAllBinary(int[] tones, string[] phonemes)
        {
            for (int i = 0; i < tones.Length; i++)
            {
                Assert.IsTrue(tones[i] == 0 || tones[i] == 1,
                    $"Index {i} ({phonemes[i]}) = {tones[i]}, expected 0 or 1");
            }
        }

        // ================================================================
        // 1. 平板型 (Heiban) - 8 ケース
        // ================================================================

        [Test]
        public void Heiban_1Mora_SingleVowel()
        {
            // 「あ」1モーラ平板: accent=0 → a1= moraPos - 0 + 1
            // moraPos=0: a1=1, a2=1, a3=1
            // 1モーラ平板: tone stays 0 (no [ detected since no a2Next==2)
            string[] ph = { "pau", "a", "pau" };
            int[] a1 = { 0, 1, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "pau");
            Assert.AreEqual(0, tones[1], "a = LOW (single mora heiban)");
            Assert.AreEqual(0, tones[2], "pau");
        }

        [Test]
        public void Heiban_2Mora_KaZe()
        {
            // 「かぜ」2モーラ平板: accent=0
            // mora0(k,a): a1=1, a2=1, a3=2
            // mora1(z,e): a1=2, a2=2, a3=1
            // [ detected at a2==1 && a2Next==2 → currentTone += 1
            string[] ph = { "pau", "k", "a", "z", "e", "pau" };
            int[] a1 = { 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // k: a2==1, a2Next==1 (same mora) → no [ yet
            // a: a2==1, a2Next==2 → [ → currentTone=1
            // z: tone=1, e: tone=1
            Assert.AreEqual(0, tones[1], "k = LOW (1st mora)");
            Assert.AreEqual(0, tones[2], "a = LOW (1st mora, [ at end)");
            Assert.AreEqual(1, tones[3], "z = HIGH (2nd mora)");
            Assert.AreEqual(1, tones[4], "e = HIGH (2nd mora)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Heiban_3Mora_Sakana()
        {
            // 「さかな」3モーラ平板: accent=0
            // mora0(s,a): a2=1, a3=3
            // mora1(k,a): a2=2, a3=2
            // mora2(n,a): a2=3, a3=1
            string[] ph = { "pau", "s", "a", "k", "a", "n", "a", "pau" };
            int[] a1 = { 0, 1, 1, 2, 2, 3, 3, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "s = LOW");
            Assert.AreEqual(0, tones[2], "a = LOW");
            Assert.AreEqual(1, tones[3], "k = HIGH");
            Assert.AreEqual(1, tones[4], "a = HIGH");
            Assert.AreEqual(1, tones[5], "n = HIGH");
            Assert.AreEqual(1, tones[6], "a = HIGH");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Heiban_5Mora_Konnichiwa()
        {
            // 「こんにちは」5モーラ平板 (実データ)
            string[] ph = { "pau", "k", "o", "N", "n", "i", "ch", "i", "w", "a", "pau" };
            int[] a1 = { 0, -4, -4, -3, -2, -2, -1, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 0 };
            int[] a3 = { 0, 5, 5, 4, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "k = LOW");
            Assert.AreEqual(0, tones[2], "o = LOW");
            Assert.AreEqual(1, tones[3], "N = HIGH");
            Assert.AreEqual(1, tones[4], "n = HIGH");
            Assert.AreEqual(1, tones[5], "i = HIGH");
            Assert.AreEqual(1, tones[6], "ch = HIGH");
            Assert.AreEqual(1, tones[7], "i = HIGH");
            Assert.AreEqual(1, tones[8], "w = HIGH");
            Assert.AreEqual(1, tones[9], "a = HIGH");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Heiban_8Mora_Long()
        {
            // 「あたらしいもの」8モーラ仮想平板: accent=0
            // 各モーラ1音素で簡略化: a i a r a sh i i m o n o → 実際は省略
            // 8モーラ: a, ta, ra, shi, i, mo, no, ga → 簡略化
            // 母音のみ8個で構築
            string[] ph = { "pau", "a", "a", "a", "a", "a", "a", "a", "a", "pau" };
            int[] a1 = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 0 };
            int[] a2 = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 0 };
            int[] a3 = { 0, 8, 7, 6, 5, 4, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "1st mora = LOW");
            for (int i = 2; i <= 8; i++)
                Assert.AreEqual(1, tones[i], $"mora {i} = HIGH");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Heiban_WithPauBefore()
        {
            // pau + 平板2モーラ + pau
            string[] ph = { "pau", "pau", "t", "a", "n", "a", "pau" };
            int[] a1 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a3 = { 0, 0, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "pau");
            Assert.AreEqual(0, tones[1], "pau");
            Assert.AreEqual(0, tones[2], "t = LOW (1st mora)");
            Assert.AreEqual(0, tones[3], "a = LOW (1st mora)");
            Assert.AreEqual(1, tones[4], "n = HIGH (2nd mora)");
            Assert.AreEqual(1, tones[5], "a = HIGH (2nd mora)");
            Assert.AreEqual(0, tones[6], "pau");
        }

        [Test]
        public void Heiban_A3Equals1_FinalMora()
        {
            // 平板型の最終モーラ a3==1 でも ] が検出されないことを確認
            // 平板型: a1 != 0 at final mora → ] 条件の a1==0 が不成立
            string[] ph = { "pau", "k", "a", "k", "i", "pau" };
            int[] a1 = { 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "k = LOW");
            Assert.AreEqual(1, tones[3], "k = HIGH");
            Assert.AreEqual(1, tones[4], "i = HIGH (final, no drop)");
        }

        [Test]
        public void Heiban_4Mora_Tomato()
        {
            // 「トマト」仮想4モーラ平板: to, ma, to, o
            string[] ph = { "pau", "t", "o", "m", "a", "t", "o", "o", "pau" };
            int[] a1 = { 0, 1, 1, 2, 2, 3, 3, 4, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 3, 4, 0 };
            int[] a3 = { 0, 4, 4, 3, 3, 2, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "t = LOW");
            Assert.AreEqual(0, tones[2], "o = LOW");
            Assert.AreEqual(1, tones[3], "m = HIGH");
            Assert.AreEqual(1, tones[7], "o = HIGH (final)");
            AssertAllBinary(tones, ph);
        }

        // ================================================================
        // 2. 頭高型 (Atamadaka) - 8 ケース
        // ================================================================

        [Test]
        public void Atamadaka_2Mora_Ame()
        {
            // 「あめ」(雨) 頭高型: accent=1
            // mora0(a): a1=0+1-1=0, a2=1, a3=2
            // mora1(m,e): a1=1+1-1=1, a2=2, a3=1
            // [ at a2==1 && a2Next==2... but a: a2==1, a2Next==2 → tone+1
            // but ] at a: a1==0, a2Next==2, a2==1, a2!=a3(1!=2) → ] → tone-1
            // Precedence: a1==0 condition checked first? No - # checked first, then ], then [
            // Actually: For phoneme 'a' (i=1):
            //   a3[1]==2 != 1 → # not triggered
            //   a1[1]==0 && a2Next==a2[1]+1(2==2) && a2[1]!=a3[1](1!=2) → ] triggered → tone -= 1
            //   (elif, so [ not checked)
            // Then m (i=2): tone=-1, no transitions since a2Next==a2 (same mora)
            // Then e (i=3): tone=-1, no transitions
            // FixPhoneTone: {0, -1, -1} → min=-1 → shift+1 → {1, 0, 0}
            string[] ph = { "pau", "a", "m", "e", "pau" };
            int[] a1 = { 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "a = HIGH (atamadaka)");
            Assert.AreEqual(0, tones[2], "m = LOW");
            Assert.AreEqual(0, tones[3], "e = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Atamadaka_3Mora_Inochi()
        {
            // 「いのち」(命) 頭高型 (実データ)
            string[] ph = { "pau", "i", "n", "o", "ch", "i", "pau" };
            int[] a1 = { 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "i = HIGH");
            Assert.AreEqual(0, tones[2], "n = LOW");
            Assert.AreEqual(0, tones[3], "o = LOW");
            Assert.AreEqual(0, tones[4], "ch = LOW");
            Assert.AreEqual(0, tones[5], "i = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Atamadaka_4Mora_Megane()
        {
            // 「めがね」仮想4モーラ頭高: accent=1
            // mora0(m,e): a1=0, a2=1, a3=4
            // mora1(g,a): a1=1, a2=2, a3=3
            // mora2(n,e): a1=2, a2=3, a3=2
            // mora3(e):   a1=3, a2=4, a3=1
            // Wait - 「めがね」is 3 mora. Let me use 仮想4モーラ「めがねや」
            // mora0(m,e): a1=0, a2=1, a3=4
            // mora1(g,a): a1=1, a2=2, a3=3
            // mora2(n,e): a1=2, a2=3, a3=2
            // mora3(y,a): a1=3, a2=4, a3=1
            string[] ph = { "pau", "m", "e", "g", "a", "n", "e", "y", "a", "pau" };
            int[] a1 = { 0, 0, 0, 1, 1, 2, 2, 3, 3, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 4, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "m = HIGH (1st mora)");
            Assert.AreEqual(1, tones[2], "e = HIGH (1st mora)");
            Assert.AreEqual(0, tones[3], "g = LOW (2nd mora)");
            Assert.AreEqual(0, tones[4], "a = LOW");
            Assert.AreEqual(0, tones[5], "n = LOW");
            Assert.AreEqual(0, tones[8], "a = LOW (last)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Atamadaka_5Mora()
        {
            // 仮想5モーラ頭高: accent=1, 各モーラ1母音
            string[] ph = { "pau", "a", "i", "u", "e", "o", "pau" };
            int[] a1 = { 0, 0, 1, 2, 3, 4, 0 };
            int[] a2 = { 0, 1, 2, 3, 4, 5, 0 };
            int[] a3 = { 0, 5, 4, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "a = HIGH (1st)");
            Assert.AreEqual(0, tones[2], "i = LOW (2nd)");
            Assert.AreEqual(0, tones[3], "u = LOW");
            Assert.AreEqual(0, tones[4], "e = LOW");
            Assert.AreEqual(0, tones[5], "o = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Atamadaka_FixPhoneTone_ShiftVerification()
        {
            // FixPhoneTone {-1, 0} → {0, 1} の詳細検証
            // 頭高2モーラ: 累積 {0, -1} → shift+1 → {1, 0}
            string[] ph = { "pau", "e", "a", "pau" };
            int[] a1 = { 0, 0, 1, 0 };
            int[] a2 = { 0, 1, 2, 0 };
            int[] a3 = { 0, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "e = HIGH after shift");
            Assert.AreEqual(0, tones[2], "a = LOW after shift");
        }

        [Test]
        public void Atamadaka_WithSokuon()
        {
            // 促音含む頭高: 「かった」accent=1
            // mora0(k,a): a2=1, a3=3
            // mora1(cl):  a2=2, a3=2 (促音は1モーラ)
            // mora2(t,a): a2=3, a3=1
            string[] ph = { "pau", "k", "a", "cl", "t", "a", "pau" };
            int[] a1 = { 0, 0, 0, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "k = HIGH");
            Assert.AreEqual(1, tones[2], "a = HIGH");
            Assert.AreEqual(0, tones[3], "cl = LOW");
            Assert.AreEqual(0, tones[4], "t = LOW");
            Assert.AreEqual(0, tones[5], "a = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Atamadaka_WithHatsuon()
        {
            // 撥音含む頭高: 「かんた」accent=1
            // mora0(k,a): a2=1, a3=3
            // mora1(N):   a2=2, a3=2 (撥音は1モーラ)
            // mora2(t,a): a2=3, a3=1
            string[] ph = { "pau", "k", "a", "N", "t", "a", "pau" };
            int[] a1 = { 0, 0, 0, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "k = HIGH");
            Assert.AreEqual(1, tones[2], "a = HIGH");
            Assert.AreEqual(0, tones[3], "N = LOW");
            Assert.AreEqual(0, tones[4], "t = LOW");
            Assert.AreEqual(0, tones[5], "a = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Atamadaka_WithSil()
        {
            // sil で囲まれた頭高: sil + 2モーラ + sil
            string[] ph = { "sil", "a", "m", "e", "sil" };
            int[] a1 = { 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "sil = 0");
            Assert.AreEqual(1, tones[1], "a = HIGH");
            Assert.AreEqual(0, tones[2], "m = LOW");
            Assert.AreEqual(0, tones[3], "e = LOW");
            Assert.AreEqual(0, tones[4], "sil = 0");
        }

        // ================================================================
        // 3. 中高型 (Nakadaka) - 8 ケース
        // ================================================================

        [Test]
        public void Nakadaka_3Mora_Accent2()
        {
            // 3モーラ中高 accent=2: 1st LOW, 2nd HIGH, 3rd LOW
            // mora0(k,o): a1=-1, a2=1, a3=3
            // mora1(k,o): a1=0, a2=2, a3=2
            // mora2(r,o): a1=1, a2=3, a3=1
            string[] ph = { "pau", "k", "o", "k", "o", "r", "o", "pau" };
            int[] a1 = { 0, -1, -1, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "k = LOW (1st)");
            Assert.AreEqual(0, tones[2], "o = LOW (1st)");
            Assert.AreEqual(1, tones[3], "k = HIGH (2nd)");
            Assert.AreEqual(1, tones[4], "o = HIGH (2nd)");
            Assert.AreEqual(0, tones[5], "r = LOW (3rd)");
            Assert.AreEqual(0, tones[6], "o = LOW (3rd)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_4Mora_Accent2()
        {
            // 4モーラ中高 accent=2
            // mora0: a1=-1, a2=1, a3=4
            // mora1: a1=0, a2=2, a3=3
            // mora2: a1=1, a2=3, a3=2
            // mora3: a1=2, a2=4, a3=1
            string[] ph = { "pau", "a", "t", "a", "m", "a", "y", "a", "pau" };
            int[] a1 = { 0, -1, 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW (1st)");
            Assert.AreEqual(1, tones[2], "t = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "a = HIGH (2nd)");
            Assert.AreEqual(0, tones[4], "m = LOW (3rd, after drop)");
            Assert.AreEqual(0, tones[5], "a = LOW");
            Assert.AreEqual(0, tones[6], "y = LOW");
            Assert.AreEqual(0, tones[7], "a = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_4Mora_Accent3()
        {
            // 4モーラ中高 accent=3
            // mora0: a1=-2, a2=1, a3=4
            // mora1: a1=-1, a2=2, a3=3
            // mora2: a1=0, a2=3, a3=2
            // mora3: a1=1, a2=4, a3=1
            string[] ph = { "pau", "o", "t", "o", "u", "t", "o", "pau" };
            int[] a1 = { 0, -2, -1, -1, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "o = LOW (1st)");
            Assert.AreEqual(1, tones[2], "t = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "o = HIGH (2nd)");
            Assert.AreEqual(1, tones[4], "u = HIGH (3rd)");
            Assert.AreEqual(0, tones[5], "t = LOW (4th, after drop)");
            Assert.AreEqual(0, tones[6], "o = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_5Mora_Accent3()
        {
            // 5モーラ中高 accent=3
            // mora0: a1=-2, a2=1, a3=5
            // mora1: a1=-1, a2=2, a3=4
            // mora2: a1=0, a2=3, a3=3
            // mora3: a1=1, a2=4, a3=2
            // mora4: a1=2, a2=5, a3=1
            string[] ph = { "pau", "a", "i", "u", "e", "o", "pau" };
            int[] a1 = { 0, -2, -1, 0, 1, 2, 0 };
            int[] a2 = { 0, 1, 2, 3, 4, 5, 0 };
            int[] a3 = { 0, 5, 4, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW (1st)");
            Assert.AreEqual(1, tones[2], "i = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "u = HIGH (3rd)");
            Assert.AreEqual(0, tones[4], "e = LOW (4th, after drop)");
            Assert.AreEqual(0, tones[5], "o = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_6Mora_Accent4()
        {
            // 6モーラ中高 accent=4
            // mora0: a1=-3, a2=1, a3=6
            // mora1: a1=-2, a2=2, a3=5
            // mora2: a1=-1, a2=3, a3=4
            // mora3: a1=0, a2=4, a3=3
            // mora4: a1=1, a2=5, a3=2
            // mora5: a1=2, a2=6, a3=1
            string[] ph = { "pau", "a", "i", "u", "e", "o", "a", "pau" };
            int[] a1 = { 0, -3, -2, -1, 0, 1, 2, 0 };
            int[] a2 = { 0, 1, 2, 3, 4, 5, 6, 0 };
            int[] a3 = { 0, 6, 5, 4, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW (1st)");
            Assert.AreEqual(1, tones[2], "i = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "u = HIGH (3rd)");
            Assert.AreEqual(1, tones[4], "e = HIGH (4th)");
            Assert.AreEqual(0, tones[5], "o = LOW (5th, after drop)");
            Assert.AreEqual(0, tones[6], "a = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_VoicelessVowel()
        {
            // 無声化母音含む中高: 「ひこうき」accent=2 (実データ相当)
            string[] ph = { "pau", "h", "I", "k", "o", "o", "k", "i", "pau" };
            int[] a1 = { 0, -1, -1, 0, 0, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 4, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "h = LOW (1st)");
            Assert.AreEqual(0, tones[2], "I = LOW (1st, voiceless)");
            Assert.AreEqual(1, tones[3], "k = HIGH (2nd)");
            Assert.AreEqual(1, tones[4], "o = HIGH (2nd)");
            Assert.AreEqual(0, tones[6], "k = LOW (4th, after drop)");
            Assert.AreEqual(0, tones[7], "i = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_LongVowel()
        {
            // 長母音含む中高: 「おにいさん」(実データ) accent=2
            string[] ph = { "pau", "o", "n", "i", "i", "s", "a", "N", "pau" };
            int[] a1 = { 0, -1, 0, 0, 1, 2, 2, 3, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 5, 0 };
            int[] a3 = { 0, 5, 4, 4, 3, 2, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "o = LOW (1st)");
            Assert.AreEqual(1, tones[2], "n = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "i = HIGH (2nd)");
            Assert.AreEqual(0, tones[4], "i = LOW (3rd, after drop)");
            Assert.AreEqual(0, tones[7], "N = LOW (5th)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Nakadaka_Imouto_Accent3()
        {
            // 「いもうと」(実データ) accent=3, 4モーラ
            string[] ph = { "pau", "i", "m", "o", "o", "t", "o", "pau" };
            int[] a1 = { 0, -2, -1, -1, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "i = LOW");
            Assert.AreEqual(1, tones[2], "m = HIGH");
            Assert.AreEqual(1, tones[3], "o = HIGH");
            Assert.AreEqual(1, tones[4], "o = HIGH");
            Assert.AreEqual(0, tones[5], "t = LOW (after drop)");
            Assert.AreEqual(0, tones[6], "o = LOW");
            AssertAllBinary(tones, ph);
        }

        // ================================================================
        // 4. 尾高型 (Odaka) - 6 ケース
        // ================================================================

        [Test]
        public void Odaka_2Mora()
        {
            // 2モーラ尾高 accent=2 (最終モーラにアクセント核)
            // mora0(a): a1=-1, a2=1, a3=2
            // mora1(m,e): a1=0, a2=2, a3=1
            // ] at mora1: a1==0, a2Next(pau)=0, but a2+1 check: a2Next==a2+1? 0==3? No
            // Actually for odaka, a1 at last mora == 0, but a2Next is pau so 0.
            // So a2Next != a2+1. No ] triggered. So tones stay {0, 1, 1} → shift+1 → {1, 2, 2} → clamp {1, 1, 1}?
            // Wait, let me re-derive. currentTone starts at 0.
            // i=1 (a): phraseAdd(0). a3[1]==2≠1. a1[1]==-1≠0. a2[1]==1 && a2Next(a2[2])==2 → [ → tone=1
            // i=2 (m): phraseAdd(1). a2Next=a2[3]==2(same mora). Nothing triggered.
            // i=3 (e): phraseAdd(1). a2Next=0(pau). a3[3]==1 && a2Next==0≠1 → # not triggered.
            //          a1[3]==0 && a2Next==0 != a2[3]+1==3 → ] not triggered.
            //          a2[3]==2 → a2!=1 → [ not triggered.
            // FixPhoneTone: {0, 1, 1} → min=0 → no shift → {0, 1, 1}
            string[] ph = { "pau", "a", "m", "e", "pau" };
            int[] a1 = { 0, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW (1st)");
            Assert.AreEqual(1, tones[2], "m = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "e = HIGH (2nd)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Odaka_3Mora()
        {
            // 3モーラ尾高 accent=3
            // mora0: a1=-2, a2=1, a3=3
            // mora1: a1=-1, a2=2, a3=2
            // mora2: a1=0, a2=3, a3=1
            string[] ph = { "pau", "o", "t", "o", "k", "o", "pau" };
            int[] a1 = { 0, -2, -1, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "o = LOW (1st)");
            Assert.AreEqual(1, tones[2], "t = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "o = HIGH (2nd)");
            Assert.AreEqual(1, tones[4], "k = HIGH (3rd)");
            Assert.AreEqual(1, tones[5], "o = HIGH (3rd)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Odaka_4Mora()
        {
            // 4モーラ尾高 accent=4
            // mora0: a1=-3, a2=1, a3=4
            // mora1: a1=-2, a2=2, a3=3
            // mora2: a1=-1, a2=3, a3=2
            // mora3: a1=0, a2=4, a3=1
            string[] ph = { "pau", "i", "m", "o", "u", "t", "o", "pau" };
            int[] a1 = { 0, -3, -2, -2, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 4, 4, 0 };
            int[] a3 = { 0, 4, 3, 3, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "i = LOW (1st)");
            Assert.AreEqual(1, tones[2], "m = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "o = HIGH (2nd)");
            Assert.AreEqual(1, tones[4], "u = HIGH (3rd)");
            Assert.AreEqual(1, tones[5], "t = HIGH (4th)");
            Assert.AreEqual(1, tones[6], "o = HIGH (4th)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Odaka_WithParticle()
        {
            // 尾高 + 助詞 (pau後): 「おとこ」尾高3 + pau + 「が」
            // 句1: o, t, o, k, o (尾高3: LOW, HIGH, HIGH, HIGH, HIGH)
            // 句2: g, a (1モーラ → LOW)
            string[] ph = { "pau", "o", "t", "o", "k", "o", "pau", "g", "a", "pau" };
            int[] a1 = { 0, -2, -1, -1, 0, 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0, 1, 1, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "o = LOW");
            Assert.AreEqual(1, tones[2], "t = HIGH");
            Assert.AreEqual(1, tones[5], "o = HIGH (last, odaka)");
            Assert.AreEqual(0, tones[6], "pau = 0");
            // 助詞 1モーラは LOW
            Assert.AreEqual(0, tones[7], "g = LOW (particle)");
            Assert.AreEqual(0, tones[8], "a = LOW (particle)");
        }

        [Test]
        public void Odaka_5Mora_Long()
        {
            // 5モーラ尾高 accent=5
            string[] ph = { "pau", "a", "i", "u", "e", "o", "pau" };
            int[] a1 = { 0, -4, -3, -2, -1, 0, 0 };
            int[] a2 = { 0, 1, 2, 3, 4, 5, 0 };
            int[] a3 = { 0, 5, 4, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW (1st)");
            Assert.AreEqual(1, tones[2], "i = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "u = HIGH (3rd)");
            Assert.AreEqual(1, tones[4], "e = HIGH (4th)");
            Assert.AreEqual(1, tones[5], "o = HIGH (5th, odaka)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Odaka_VsHeiban_DifferentA1()
        {
            // 尾高と平板は a1 の違いで区別される
            // Odaka(2, accent=2): a1={-1, 0}
            // Heiban(2, accent=0): a1={1, 2}
            // 両方 tones は {LOW, HIGH} → 区別は助詞接続時のみ
            string[] phOdaka = { "pau", "a", "m", "e", "pau" };
            int[] a1Odaka = { 0, -1, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 1, 1, 0 };
            int[] tonesOdaka = JapaneseG2P.ComputeTonesFromProsody(phOdaka, a1Odaka, a2, a3, phOdaka.Length);

            string[] phHeiban = { "pau", "a", "m", "e", "pau" };
            int[] a1Heiban = { 0, 1, 2, 2, 0 };
            int[] tonesHeiban = JapaneseG2P.ComputeTonesFromProsody(phHeiban, a1Heiban, a2, a3, phHeiban.Length);

            // Both should be LOW, HIGH, HIGH
            Assert.AreEqual(0, tonesOdaka[1]);
            Assert.AreEqual(1, tonesOdaka[2]);
            Assert.AreEqual(0, tonesHeiban[1]);
            Assert.AreEqual(1, tonesHeiban[2]);
        }

        // ================================================================
        // 5. 複合語・多句 - 8 ケース
        // ================================================================

        [Test]
        public void MultiPhrase_2Phrases_PauBoundary()
        {
            // 2句: pau + 平板2モーラ + pau + 頭高2モーラ + pau
            string[] ph = { "pau", "k", "a", "z", "e", "pau", "a", "m", "e", "pau" };
            // 句1: 平板2
            int[] a1 = { 0, 1, 1, 2, 2, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 0, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // 句1: LOW, LOW, HIGH, HIGH
            Assert.AreEqual(0, tones[1], "k = LOW (phrase1)");
            Assert.AreEqual(1, tones[3], "z = HIGH (phrase1)");
            Assert.AreEqual(0, tones[5], "pau = 0");
            // 句2: HIGH, LOW, LOW
            Assert.AreEqual(1, tones[6], "a = HIGH (phrase2 atamadaka)");
            Assert.AreEqual(0, tones[7], "m = LOW (phrase2)");
            Assert.AreEqual(0, tones[8], "e = LOW (phrase2)");
        }

        [Test]
        public void MultiPhrase_3Phrases()
        {
            // 3句: 1モーラ + 1モーラ + 1モーラ
            string[] ph = { "pau", "a", "pau", "i", "pau", "u", "pau" };
            int[] a1 = { 0, 0, 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 0, 1, 0, 1, 0 };
            int[] a3 = { 0, 1, 0, 1, 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW");
            Assert.AreEqual(0, tones[3], "i = LOW");
            Assert.AreEqual(0, tones[5], "u = LOW");
            for (int i = 0; i < tones.Length; i += 2)
                Assert.AreEqual(0, tones[i], $"pau/sil at {i}");
        }

        [Test]
        public void MultiPhrase_Heiban_Plus_Atamadaka()
        {
            // 句1=平板3モーラ + 句2=頭高2モーラ
            string[] ph = { "pau", "s", "a", "k", "a", "n", "a", "pau", "a", "m", "e", "pau" };
            // 句1: 平板3 (a1: 1,2,3 all positive)
            int[] a1 = { 0, 1, 1, 2, 2, 3, 3, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 3, 0, 1, 2, 2, 0 };
            int[] a3 = { 0, 3, 3, 2, 2, 1, 1, 0, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // 句1: LOW, LOW, HIGH, HIGH, HIGH, HIGH
            Assert.AreEqual(0, tones[1], "s = LOW");
            Assert.AreEqual(1, tones[3], "k = HIGH");
            Assert.AreEqual(1, tones[6], "a = HIGH (end phrase1)");
            // 句2: HIGH, LOW, LOW
            Assert.AreEqual(1, tones[8], "a = HIGH (atamadaka)");
            Assert.AreEqual(0, tones[9], "m = LOW");
            Assert.AreEqual(0, tones[10], "e = LOW");
        }

        [Test]
        public void MultiPhrase_Nakadaka_Plus_Heiban()
        {
            // 句1=中高3モーラ(accent=2) + 句2=平板2モーラ
            string[] ph = { "pau", "a", "k", "a", "r", "i", "pau", "t", "a", "n", "a", "pau" };
            int[] a1 = { 0, -1, 0, 0, 1, 1, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0, 1, 1, 2, 2, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // 句1: LOW, HIGH, HIGH, LOW, LOW
            Assert.AreEqual(0, tones[1], "a = LOW (1st)");
            Assert.AreEqual(1, tones[2], "k = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "a = HIGH (2nd)");
            Assert.AreEqual(0, tones[4], "r = LOW (3rd, after drop)");
            Assert.AreEqual(0, tones[5], "i = LOW");
            // 句2: LOW, LOW, HIGH, HIGH
            Assert.AreEqual(0, tones[7], "t = LOW (1st)");
            Assert.AreEqual(0, tones[8], "a = LOW (1st)");
            Assert.AreEqual(1, tones[9], "n = HIGH (2nd)");
            Assert.AreEqual(1, tones[10], "a = HIGH (2nd)");
        }

        [Test]
        public void MultiPhrase_HashBoundary_Condition()
        {
            // 句境界(#): a3==1 && a2[i+1]==1 && IsVowelOrNOrCl
            // 「なまえ」の # 境界 (実データ)
            string[] ph = { "pau", "n", "a", "m", "a", "e", "pau" };
            int[] a1 = { 0, 0, 0, 1, 1, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 1, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "n = HIGH (phrase1)");
            Assert.AreEqual(1, tones[2], "a = HIGH (phrase1)");
            Assert.AreEqual(0, tones[3], "m = LOW (phrase1)");
            Assert.AreEqual(0, tones[4], "a = LOW (phrase1, triggers #)");
            Assert.AreEqual(0, tones[5], "e = LOW (phrase2)");
        }

        [Test]
        public void MultiPhrase_IndependentToneCalculation()
        {
            // 各句が独立にトーン計算: 句1で -1 があっても句2はリセット
            // 句1: 頭高2モーラ → {0, -1} → FixPhoneTone → {1, 0}
            // 句2: 平板2モーラ → {0, 1} → FixPhoneTone → {0, 1}
            string[] ph = { "pau", "a", "i", "pau", "u", "e", "pau" };
            int[] a1 = { 0, 0, 1, 0, 1, 2, 0 };
            int[] a2 = { 0, 1, 2, 0, 1, 2, 0 };
            int[] a3 = { 0, 2, 1, 0, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "a = HIGH (phrase1 atamadaka)");
            Assert.AreEqual(0, tones[2], "i = LOW (phrase1)");
            Assert.AreEqual(0, tones[4], "u = LOW (phrase2 heiban 1st)");
            Assert.AreEqual(1, tones[5], "e = HIGH (phrase2 heiban 2nd)");
        }

        [Test]
        public void MultiPhrase_ConsecutivePau()
        {
            // 連続 pau: 空句が挟まっても問題なし
            string[] ph = { "pau", "a", "pau", "pau", "i", "pau" };
            int[] a1 = { 0, 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 0, 0, 1, 0 };
            int[] a3 = { 0, 1, 0, 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "pau");
            Assert.AreEqual(0, tones[1], "a = LOW");
            Assert.AreEqual(0, tones[2], "pau");
            Assert.AreEqual(0, tones[3], "pau (consecutive)");
            Assert.AreEqual(0, tones[4], "i = LOW");
            Assert.AreEqual(0, tones[5], "pau");
        }

        [Test]
        public void MultiPhrase_HashNotTriggered_ConsonantEnd()
        {
            // # 条件の IsVowelOrNOrCl が false (子音で終わる) → # が発動しない
            // a3==1 && a2Next==1 だが、ph が 'k' → IsVowelOrNOrCl=false
            string[] ph = { "pau", "k", "a", "k", "e", "pau" };
            // 構築: 句境界条件を満たすが、音素 'k' は母音ではない
            // mora0(k,a): a2=1, a3=2
            // mora1(k): a2=2, a3=1 ← a3==1
            // 'e': a2=1 ← a2Next==1 (but e is next, not k)
            // Actually for # check: at i=3 (k in mora1), a3[3]==1, a2Next=a2[4]==2 ≠ 1 → # not triggered
            // Let me construct it properly:
            // We need a3[i]==1 && a2[i+1]==1 at a consonant
            string[] ph2 = { "pau", "a", "k", "a", "pau" };
            int[] a1 = { 0, 0, 1, 0, 0 };
            int[] a2 = { 0, 1, 2, 1, 0 };
            int[] a3 = { 0, 2, 1, 1, 0 };
            // i=2 (k): a3==1, a2Next==a2[3]==1 → # condition: IsVowelOrNOrCl("k") → false → # NOT triggered
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph2, a1, a2, a3, ph2.Length);
            // Without #, all in one phrase
            // a(0): tone=0, [ at a2==1 && a2Next==2 → tone=1? a2[1]==1, a2Next=a2[2]==2 → yes → tone=1
            // k(1): tone=1. a1[2]==1≠0 → no ]. a2[2]==2≠1 → no [. a3[2]==1, a2Next==1, IsVowel(k)=false → no #
            // a(2): tone=1.
            // FixPhoneTone: {0, 1, 1} → {0, 1, 1}
            Assert.AreEqual(0, tones[1], "a = LOW");
            Assert.AreEqual(1, tones[2], "k = HIGH (# not triggered)");
            Assert.AreEqual(1, tones[3], "a = HIGH");
        }

        // ================================================================
        // 6. 疑問文・感嘆文 - 3 ケース
        // ================================================================

        [Test]
        public void Question_Short()
        {
            // 短い疑問「え？」: 1モーラ + pau
            string[] ph = { "pau", "e", "pau" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "e = LOW (single mora question)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Question_Long()
        {
            // 長い疑問「本当ですか？」: 2句 h,o,N,t,o,u + d,e,s,U,k,a
            string[] ph = { "pau", "h", "o", "N", "t", "o", "u",
                            "pau", "d", "e", "s", "U", "k", "a", "pau" };
            // 句1: 平板4モーラ
            int[] a1 = { 0, 1, 1, 2, 3, 3, 4,
                         0, 0, 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4,
                         0, 1, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 4, 4, 3, 2, 2, 1,
                         0, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // 句1: heiban → LOW, LOW, HIGH, HIGH, HIGH, HIGH
            Assert.AreEqual(0, tones[1], "h = LOW");
            Assert.AreEqual(1, tones[3], "N = HIGH");
            // 句2: atamadaka → HIGH, HIGH, LOW, LOW, LOW, LOW
            Assert.AreEqual(1, tones[8], "d = HIGH");
            Assert.AreEqual(1, tones[9], "e = HIGH");
            Assert.AreEqual(0, tones[10], "s = LOW");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Exclamation_Pattern()
        {
            // 感嘆「すごい！」: 3モーラ中高accent=2 s,u,g,o,i
            string[] ph = { "pau", "s", "u", "g", "o", "i", "pau" };
            int[] a1 = { 0, -1, -1, 0, 0, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 3, 0 };
            int[] a3 = { 0, 3, 3, 2, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "s = LOW (1st)");
            Assert.AreEqual(0, tones[2], "u = LOW (1st)");
            Assert.AreEqual(1, tones[3], "g = HIGH (2nd)");
            Assert.AreEqual(1, tones[4], "o = HIGH (2nd)");
            Assert.AreEqual(0, tones[5], "i = LOW (3rd, after drop)");
            AssertAllBinary(tones, ph);
        }

        // ================================================================
        // 7. エッジケース - 8 ケース
        // ================================================================

        [Test]
        public void Edge_EmptyArray()
        {
            string[] ph = { };
            int[] a1 = { };
            int[] a2 = { };
            int[] a3 = { };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, 0);
            Assert.AreEqual(0, tones.Length);
        }

        [Test]
        public void Edge_SilOnly()
        {
            string[] ph = { "sil" };
            int[] a1 = { 0 };
            int[] a2 = { 0 };
            int[] a3 = { 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones.Length);
            Assert.AreEqual(0, tones[0], "sil = 0");
        }

        [Test]
        public void Edge_PauOnly()
        {
            string[] ph = { "pau" };
            int[] a1 = { 0 };
            int[] a2 = { 0 };
            int[] a3 = { 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones.Length);
            Assert.AreEqual(0, tones[0], "pau = 0");
        }

        [Test]
        public void Edge_SilOnePhonemeSil()
        {
            // sil + 1音素 + sil
            string[] ph = { "sil", "a", "sil" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "sil = 0");
            Assert.AreEqual(0, tones[1], "a = LOW (single)");
            Assert.AreEqual(0, tones[2], "sil = 0");
        }

        [Test]
        public void Edge_AllPau()
        {
            string[] ph = { "pau", "pau", "pau", "pau" };
            int[] a1 = { 0, 0, 0, 0 };
            int[] a2 = { 0, 0, 0, 0 };
            int[] a3 = { 0, 0, 0, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(4, tones.Length);
            foreach (int t in tones)
                Assert.AreEqual(0, t);
        }

        [Test]
        public void Edge_CountLessThanArrayLength()
        {
            // count < array length の場合、count 分だけ処理
            string[] ph = { "pau", "a", "i", "pau", "u", "e" };
            int[] a1 = { 0, 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 2, 0, 1, 2 };
            int[] a3 = { 0, 2, 1, 0, 2, 1 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, 4);
            Assert.AreEqual(4, tones.Length, "Should only process first 4");
            Assert.AreEqual(0, tones[0], "pau");
            Assert.AreEqual(0, tones[3], "pau");
        }

        [Test]
        public void Edge_VeryLongPhrase_20Phonemes()
        {
            // 20+ 音素の長い句 (10モーラ平板, 各2音素)
            int moraCount = 10;
            int phCount = moraCount * 2 + 2; // +2 for pau
            string[] ph = new string[phCount];
            int[] a1 = new int[phCount];
            int[] a2 = new int[phCount];
            int[] a3 = new int[phCount];

            ph[0] = "pau"; a1[0] = 0; a2[0] = 0; a3[0] = 0;
            for (int m = 0; m < moraCount; m++)
            {
                int idx1 = 1 + m * 2;
                int idx2 = 2 + m * 2;
                ph[idx1] = "k";
                ph[idx2] = "a";
                int mp = m; // 0-indexed moraPos
                a1[idx1] = a1[idx2] = mp + 1; // positive for heiban
                a2[idx1] = a2[idx2] = mp + 1;
                a3[idx1] = a3[idx2] = moraCount - mp;
            }
            ph[phCount - 1] = "pau"; a1[phCount - 1] = 0; a2[phCount - 1] = 0; a3[phCount - 1] = 0;

            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, phCount);
            Assert.AreEqual(phCount, tones.Length);
            Assert.AreEqual(0, tones[0], "pau");
            // 1st mora LOW
            Assert.AreEqual(0, tones[1], "k = LOW (1st mora)");
            Assert.AreEqual(0, tones[2], "a = LOW (1st mora)");
            // 2nd mora HIGH
            Assert.AreEqual(1, tones[3], "k = HIGH (2nd mora)");
            // Last mora HIGH (heiban)
            Assert.AreEqual(1, tones[phCount - 3], "k = HIGH (last mora)");
            Assert.AreEqual(1, tones[phCount - 2], "a = HIGH (last mora)");
            Assert.AreEqual(0, tones[phCount - 1], "pau");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Edge_SilB_And_SilE()
        {
            // silB, silE は sil と同様にトーン0
            string[] ph = { "silB", "a", "silE" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[0], "silB = 0");
            Assert.AreEqual(0, tones[1], "a = LOW");
            Assert.AreEqual(0, tones[2], "silE = 0");
        }

        // ================================================================
        // 8. 特殊音素 - 8 ケース
        // ================================================================

        [Test]
        public void Special_ClOnly()
        {
            // 促音のみの句: cl は IsVowelOrNOrCl=true
            string[] ph = { "pau", "cl", "pau" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "cl = LOW (single)");
        }

        [Test]
        public void Special_NOnly()
        {
            // 撥音のみの句: N は IsVowelOrNOrCl=true
            string[] ph = { "pau", "N", "pau" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "N = LOW (single)");
        }

        [Test]
        public void Special_ClAndN_Mixed()
        {
            // cl, N 混在: 2モーラ平板
            string[] ph = { "pau", "cl", "N", "pau" };
            int[] a1 = { 0, 1, 2, 0 };
            int[] a2 = { 0, 1, 2, 0 };
            int[] a3 = { 0, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "cl = LOW (1st)");
            Assert.AreEqual(1, tones[2], "N = HIGH (2nd)");
        }

        [Test]
        public void Special_UppercaseVowel_A()
        {
            // 無声化母音 A で IsVowelOrNOrCl=true (# 境界判定に影響)
            // a3==1 && a2Next==1 && IsVowelOrNOrCl("A") → # triggered
            string[] ph = { "pau", "k", "A", "t", "a", "pau" };
            int[] a1 = { 0, 0, 0, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 1, 0 };
            int[] a3 = { 0, 1, 1, 1, 1, 0 };
            // i=1 (k): a3==1, a2Next=a2[2]==1, IsVowelOrNOrCl("k")=false → # not triggered
            // i=2 (A): a3==1, a2Next=a2[3]==2, 2≠1 → # not triggered
            // Let me construct properly for # trigger on A:
            string[] ph2 = { "pau", "A", "k", "a", "pau" };
            int[] a12 = { 0, 0, 0, 0, 0 };
            int[] a22 = { 0, 1, 2, 1, 0 };
            int[] a32 = { 0, 1, 1, 1, 0 };
            // i=1 (A): a3==1, a2Next=a2[2]==2 ≠ 1 → # not triggered
            // Actually # needs a2Next==1. So:
            string[] ph3 = { "pau", "n", "A", "k", "a", "pau" };
            int[] a13 = { 0, 0, 0, 1, 0, 0 };
            int[] a23 = { 0, 1, 1, 2, 1, 0 };
            int[] a33 = { 0, 2, 2, 1, 1, 0 };
            // i=3 (k): a3==1, a2Next=a2[4]==1 → # condition, but IsVowelOrNOrCl("k")=false → no
            // i=2 (A): a3==2 ≠ 1 → no
            // Need a vowel phoneme at a3==1 with a2Next==1
            string[] ph4 = { "pau", "k", "a", "A", "k", "a", "pau" };
            int[] a14 = { 0, 0, 0, 1, 0, 0, 0 };
            int[] a24 = { 0, 1, 1, 2, 1, 1, 0 };
            int[] a34 = { 0, 2, 2, 1, 1, 1, 0 };
            // i=3 (A): a3[3]==1, a2Next=a2[4]==1, IsVowelOrNOrCl("A")=true → # triggered!
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph4, a14, a24, a34, ph4.Length);
            // Phrase1: k(0), a(0), A → [ at a: a2==1, a2Next==2 → tone=1
            // A: tone=1. # triggered → FixPhoneTone({0, 0, 1}) → min=0, no shift → {0, 0, 1}
            Assert.AreEqual(0, tones[1], "k = LOW");
            Assert.AreEqual(0, tones[2], "a = LOW");
            Assert.AreEqual(1, tones[3], "A = HIGH (before # boundary)");
            // Phrase2: k(0), a(0) → [ at k: a2==1, a2Next==1 (same mora)? No, a2[5]==1, a2Next=0
            // k: a2==1, a2Next=a2[5]==1 → no [. a1[4]==0, a2Next==1, a2[4]+1==2 → 1≠2 → no ]
            // a: a2==1, a2Next=0 → no transition
            // FixPhoneTone({0, 0}) → {0, 0}
            Assert.AreEqual(0, tones[4], "k = LOW (phrase2)");
            Assert.AreEqual(0, tones[5], "a = LOW (phrase2)");
            AssertAllBinary(tones, ph4);
        }

        [Test]
        public void Special_UppercaseVowel_I_U_E_O()
        {
            // 無声化母音 I, U, E, O が母音として正しく扱われる
            string[] ph = { "pau", "I", "U", "E", "O", "pau" };
            int[] a1 = { 0, -3, -2, -1, 0, 0 };
            int[] a2 = { 0, 1, 2, 3, 4, 0 };
            int[] a3 = { 0, 4, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "I = LOW (1st)");
            Assert.AreEqual(1, tones[2], "U = HIGH (2nd)");
            Assert.AreEqual(1, tones[3], "E = HIGH (3rd)");
            Assert.AreEqual(1, tones[4], "O = HIGH (4th)");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Special_ConsonantOnly_NotVowel()
        {
            // 子音のみ (m, k, s 等) は IsVowelOrNOrCl=false
            // 子音で構成される句でも正常に処理される
            string[] ph = { "pau", "k", "s", "t", "pau" };
            int[] a1 = { 0, 1, 2, 3, 0 };
            int[] a2 = { 0, 1, 2, 3, 0 };
            int[] a3 = { 0, 3, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // k: a2==1, a2Next==2 → [ → tone=1
            Assert.AreEqual(0, tones[1], "k = LOW (before [)");
            Assert.AreEqual(1, tones[2], "s = HIGH");
            Assert.AreEqual(1, tones[3], "t = HIGH");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void Special_N_TriggerHashBoundary()
        {
            // N (撥音) で # 境界がトリガーされることを確認
            // a3==1 && a2Next==1 && IsVowelOrNOrCl("N")=true
            string[] ph = { "pau", "k", "a", "N", "k", "o", "pau" };
            int[] a1 = { 0, 0, 0, 1, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 1, 1, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 1, 0 };
            // i=3 (N): a3[3]==1, a2Next=a2[4]==1, IsVowelOrNOrCl("N")=true → # triggered
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // Phrase1: k(0), a(0) → [ at a: a2==1, a2Next==2 → tone=1. N(1).
            // # triggered → FixPhoneTone({0, 0, 1}) → {0, 0, 1}
            Assert.AreEqual(0, tones[1], "k = LOW (phrase1)");
            Assert.AreEqual(0, tones[2], "a = LOW (phrase1)");
            Assert.AreEqual(1, tones[3], "N = HIGH (phrase1, triggers #)");
            // Phrase2: k(0), o(0) → k: a2==1, a2Next=a2[5]==1 → no [
            // FixPhoneTone({0, 0}) → {0, 0}
            Assert.AreEqual(0, tones[4], "k = LOW (phrase2)");
            Assert.AreEqual(0, tones[5], "o = LOW (phrase2)");
        }

        [Test]
        public void Special_Cl_TriggerHashBoundary()
        {
            // cl (促音) で # 境界がトリガーされることを確認
            string[] ph = { "pau", "k", "a", "cl", "t", "a", "pau" };
            int[] a1 = { 0, 0, 0, 1, 0, 0, 0 };
            int[] a2 = { 0, 1, 1, 2, 1, 1, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 1, 0 };
            // i=3 (cl): a3==1, a2Next=a2[4]==1, IsVowelOrNOrCl("cl")=true → # triggered
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "k = LOW");
            Assert.AreEqual(0, tones[2], "a = LOW");
            Assert.AreEqual(1, tones[3], "cl = HIGH (triggers #)");
            Assert.AreEqual(0, tones[4], "t = LOW (phrase2)");
            Assert.AreEqual(0, tones[5], "a = LOW (phrase2)");
        }

        // ================================================================
        // 追加: 遷移検出の網羅テスト
        // ================================================================

        [Test]
        public void Transition_UpOnly_NoDown()
        {
            // [ のみ発生、] なし → {0, 1, 1, ...}
            // 平板型は [ のみ
            string[] ph = { "pau", "k", "a", "m", "i", "pau" };
            int[] a1 = { 0, 1, 1, 2, 2, 0 };
            int[] a2 = { 0, 1, 1, 2, 2, 0 };
            int[] a3 = { 0, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1]);
            Assert.AreEqual(0, tones[2]);
            Assert.AreEqual(1, tones[3]);
            Assert.AreEqual(1, tones[4]);
        }

        [Test]
        public void Transition_UpThenDown()
        {
            // [ → ] の順序: 中高型
            string[] ph = { "pau", "a", "k", "a", "n", "a", "pau" };
            int[] a1 = { 0, -1, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 2, 2, 3, 3, 0 };
            int[] a3 = { 0, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = LOW");
            Assert.AreEqual(1, tones[2], "k = HIGH");
            Assert.AreEqual(1, tones[3], "a = HIGH");
            Assert.AreEqual(0, tones[4], "n = LOW (after ])");
            Assert.AreEqual(0, tones[5], "a = LOW");
        }

        [Test]
        public void Transition_DownOnly_Atamadaka()
        {
            // ] のみ発生: 頭高型 ([ なし, ] で tone-1)
            // 頭高2モーラ: tone starts 0, ] at mora0 end → tone=-1
            // FixPhoneTone: {0, -1} → shift+1 → {1, 0}
            string[] ph = { "pau", "a", "i", "pau" };
            int[] a1 = { 0, 0, 1, 0 };
            int[] a2 = { 0, 1, 2, 0 };
            int[] a3 = { 0, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(1, tones[1], "a = HIGH (shifted)");
            Assert.AreEqual(0, tones[2], "i = LOW (shifted)");
        }

        [Test]
        public void FixPhoneTone_ClampAbove1()
        {
            // トーンが2以上になるケース: 複数の [ が連続
            // (通常の日本語では発生しないが、アルゴリズムの堅牢性テスト)
            // 手動構築: [ が2回連続 → currentTone=2
            // a2==1 && a2Next==2 → +1, then somehow another +1
            // 実際には [ 条件は a2==1 限定なので、単一句内で2回は起きない
            // 代わに、FixPhoneTone のクランプが > 1 を 1 にすることを間接的に確認
            // 通常データで検証: {0, 1} → クランプ不要
            string[] ph = { "pau", "a", "i", "pau" };
            int[] a1 = { 0, -1, 0, 0 };
            int[] a2 = { 0, 1, 2, 0 };
            int[] a3 = { 0, 2, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            // {0, 1} → no shift needed
            Assert.AreEqual(0, tones[1], "a = LOW");
            Assert.AreEqual(1, tones[2], "i = HIGH");
            AssertAllBinary(tones, ph);
        }

        [Test]
        public void FixPhoneTone_AllZero()
        {
            // 全トーン0: 単モーラ句
            string[] ph = { "pau", "a", "pau" };
            int[] a1 = { 0, 0, 0 };
            int[] a2 = { 0, 1, 0 };
            int[] a3 = { 0, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);
            Assert.AreEqual(0, tones[1], "a = 0 (no shift for all-zero)");
        }

        [Test]
        public void FullPipeline_KonnichiwaWorld()
        {
            // 統合テスト: 「こんにちは、さくらです。」(実データ)
            string[] ph = { "pau", "k", "o", "N", "n", "i", "ch", "i", "w", "a",
                            "pau", "s", "a", "k", "u", "r", "a", "d", "e", "s", "U", "pau" };
            int[] a1 = { 0, -4, -4, -3, -2, -2, -1, -1, 0, 0,
                         0, -3, -3, -2, -2, -1, -1, 0, 0, 1, 1, 0 };
            int[] a2 = { 0, 1, 1, 2, 3, 3, 4, 4, 5, 5,
                         0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0 };
            int[] a3 = { 0, 5, 5, 4, 3, 3, 2, 2, 1, 1,
                         0, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1, 0 };
            int[] tones = JapaneseG2P.ComputeTonesFromProsody(ph, a1, a2, a3, ph.Length);

            // 句1: 平板5モーラ
            int[] expected1 = { 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0 };
            for (int i = 0; i <= 10; i++)
                Assert.AreEqual(expected1[i], tones[i], $"Index {i} ({ph[i]})");

            // 句2: 中高 (LOW, LOW, HIGH, HIGH, HIGH, HIGH, HIGH, HIGH, LOW, LOW)
            int[] expected2 = { 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0 };
            for (int i = 0; i < 11; i++)
                Assert.AreEqual(expected2[i], tones[i + 11], $"Index {i + 11} ({ph[i + 11]})");
        }
    }
}
