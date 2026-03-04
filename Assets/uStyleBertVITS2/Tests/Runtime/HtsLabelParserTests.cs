using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class HtsLabelParserTests
    {
        // =====================================================================
        // ParsePhoneme テスト
        // =====================================================================

        [Test]
        public void ParsePhoneme_StandardConsonant_ReturnsB()
        {
            string label = "xx^sil-b+o=N/A:-3+1+4/B:xx-xx_xx";
            Assert.AreEqual("b", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Vowel_ReturnsO()
        {
            string label = "sil^b-o+N=s/A:-3+2+3/B:xx-xx_xx";
            Assert.AreEqual("o", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Nasal_ReturnsN()
        {
            string label = "b^o-N+s=a/A:-2+3+2/B:xx-xx_xx";
            Assert.AreEqual("N", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Affricate_ReturnsCh()
        {
            string label = "a^i-ch+i=m/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("ch", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Fricative_ReturnsSh()
        {
            string label = "o^k-sh+i=t/A:2+1+3/B:xx-xx_xx";
            Assert.AreEqual("sh", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Affricate_ReturnsTs()
        {
            string label = "a^k-ts+u=k/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("ts", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_GlottalStop_ReturnsCl()
        {
            string label = "i^k-cl+k=a/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("cl", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Glide_ReturnsW()
        {
            string label = "a^k-w+a=t/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("w", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Glide_ReturnsY()
        {
            string label = "a^k-y+u=k/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("y", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Liquid_ReturnsR()
        {
            string label = "o^k-r+a=k/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("r", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VowelA()
        {
            string label = "k^o-a+k=i/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("a", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VowelI()
        {
            string label = "k^a-i+s=u/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("i", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VowelU()
        {
            string label = "k^a-u+k=i/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("u", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VowelE()
        {
            string label = "k^a-e+k=i/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("e", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Silence_ReturnsSil()
        {
            string label = "xx^xx-sil+b=o/A:xx+xx+xx/B:xx-xx_xx";
            Assert.AreEqual("sil", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Pause_ReturnsPau()
        {
            string label = "a^N-pau+k=o/A:xx+xx+xx/B:xx-xx_xx";
            Assert.AreEqual("pau", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Unknown_ReturnsXx()
        {
            string label = "a^sil-xx+xx=xx/A:xx+xx+xx/B:xx-xx_xx";
            Assert.AreEqual("xx", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_HeadLabel_ReturnsSil()
        {
            // 先頭ラベル: p2=xx, p1=xx
            string label = "xx^xx-sil+k=o/A:xx+xx+xx/B:xx-xx_xx";
            Assert.AreEqual("sil", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_TailLabel_ReturnsSil()
        {
            // 末尾ラベル: n1=xx, n2=xx
            string label = "a^N-sil+xx=xx/A:xx+xx+xx/B:xx-xx_xx";
            Assert.AreEqual("sil", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_SingleConsonant_ReturnsK()
        {
            string label = "sil^b-k+a=N/A:1+1+4/B:xx-xx_xx";
            Assert.AreEqual("k", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_NoDashReturnsXx()
        {
            // '-' がないラベル → "xx" にフォールバック
            string label = "nodelimiter/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("xx", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_NoPlusReturnsXx()
        {
            // '+' がないラベル（'-' の後に '+' なし）
            string label = "xx^xx-abc/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("xx", HtsLabelParser.ParsePhoneme(label));
        }

        // =====================================================================
        // ParseA テスト
        // =====================================================================

        [Test]
        public void ParseA_PositiveValues()
        {
            string label = "xx^sil-b+o=N/A:1+2+3/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(1, a1);
            Assert.AreEqual(2, a2);
            Assert.AreEqual(3, a3);
        }

        [Test]
        public void ParseA_NegativeA1()
        {
            string label = "xx^sil-b+o=N/A:-3+1+4/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(-3, a1);
            Assert.AreEqual(1, a2);
            Assert.AreEqual(4, a3);
        }

        [Test]
        public void ParseA_LargeNegativeA1()
        {
            string label = "xx^sil-b+o=N/A:-10+1+11/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(-10, a1);
            Assert.AreEqual(1, a2);
            Assert.AreEqual(11, a3);
        }

        [Test]
        public void ParseA_XxValues_ReturnZero()
        {
            string label = "xx^xx-sil+b=o/A:xx+xx+xx/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(0, a1);
            Assert.AreEqual(0, a2);
            Assert.AreEqual(0, a3);
        }

        [Test]
        public void ParseA_ZeroA1()
        {
            string label = "xx^sil-b+o=N/A:0+1+1/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(0, a1);
            Assert.AreEqual(1, a2);
            Assert.AreEqual(1, a3);
        }

        [Test]
        public void ParseA_LargePositiveValues()
        {
            string label = "xx^sil-b+o=N/A:5+10+15/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(5, a1);
            Assert.AreEqual(10, a2);
            Assert.AreEqual(15, a3);
        }

        [Test]
        public void ParseA_NoAField_ReturnZero()
        {
            string label = "xx^sil-b+o=N/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(0, a1);
            Assert.AreEqual(0, a2);
            Assert.AreEqual(0, a3);
        }

        [Test]
        public void ParseA_AFieldAtEnd()
        {
            // /A: が最後のフィールドで後続の '/' なし
            string label = "xx^sil-b+o=N/A:2+3+4";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(2, a1);
            Assert.AreEqual(3, a2);
            Assert.AreEqual(4, a3);
        }

        [Test]
        public void ParseA_NegativeOne()
        {
            string label = "xx^sil-b+o=N/A:-1+1+2/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(-1, a1);
            Assert.AreEqual(1, a2);
            Assert.AreEqual(2, a3);
        }

        [Test]
        public void ParseA_AllOnes()
        {
            string label = "xx^sil-b+o=N/A:1+1+1/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(1, a1);
            Assert.AreEqual(1, a2);
            Assert.AreEqual(1, a3);
        }

        // =====================================================================
        // ParseAll テスト
        // =====================================================================

        [Test]
        public void ParseAll_EmptyList_ReturnsEmptyArrays()
        {
            var labels = new string[0];
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            Assert.AreEqual(0, phonemes.Length);
            Assert.AreEqual(0, a1.Length);
            Assert.AreEqual(0, a2.Length);
            Assert.AreEqual(0, a3.Length);
        }

        [Test]
        public void ParseAll_SingleLabel()
        {
            var labels = new[] { "xx^sil-b+o=N/A:-3+1+4/B:xx-xx_xx" };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            Assert.AreEqual(1, phonemes.Length);
            Assert.AreEqual("b", phonemes[0]);
            Assert.AreEqual(-3, a1[0]);
            Assert.AreEqual(1, a2[0]);
            Assert.AreEqual(4, a3[0]);
        }

        [Test]
        public void ParseAll_MultipleLabels()
        {
            var labels = new[]
            {
                "xx^xx-sil+b=o/A:xx+xx+xx/B:xx-xx_xx",
                "xx^sil-b+o=N/A:-3+1+4/B:xx-xx_xx",
                "sil^b-o+N=s/A:-3+2+3/B:xx-xx_xx",
            };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            Assert.AreEqual(3, phonemes.Length);
            Assert.AreEqual("sil", phonemes[0]);
            Assert.AreEqual("b", phonemes[1]);
            Assert.AreEqual("o", phonemes[2]);

            // sil → xx → 0
            Assert.AreEqual(0, a1[0]);
            Assert.AreEqual(0, a2[0]);
            Assert.AreEqual(0, a3[0]);

            // b → -3, 1, 4
            Assert.AreEqual(-3, a1[1]);
            Assert.AreEqual(1, a2[1]);
            Assert.AreEqual(4, a3[1]);

            // o → -3, 2, 3
            Assert.AreEqual(-3, a1[2]);
            Assert.AreEqual(2, a2[2]);
            Assert.AreEqual(3, a3[2]);
        }

        [Test]
        public void ParseAll_ArrayLengthsMatch()
        {
            var labels = new[]
            {
                "xx^xx-sil+k=o/A:xx+xx+xx/B:xx-xx_xx",
                "xx^sil-k+o=r/A:-2+1+3/B:xx-xx_xx",
                "sil^k-o+r=e/A:-2+2+2/B:xx-xx_xx",
                "k^o-r+e=w/A:-1+3+1/B:xx-xx_xx",
                "o^r-e+w=a/A:xx+xx+xx/B:xx-xx_xx",
            };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            Assert.AreEqual(5, phonemes.Length);
            Assert.AreEqual(5, a1.Length);
            Assert.AreEqual(5, a2.Length);
            Assert.AreEqual(5, a3.Length);
        }

        // =====================================================================
        // 統合テスト: 「盆栽」(bonsai) 風の HTS ラベル列
        // =====================================================================

        [Test]
        public void Integration_Bonsai_PhonemesExtractedCorrectly()
        {
            // 「盆栽」: b o N s a i
            var labels = new[]
            {
                "xx^xx-sil+b=o/A:xx+xx+xx/B:xx-xx_xx",
                "xx^sil-b+o=N/A:-3+1+4/B:xx-xx_xx",
                "sil^b-o+N=s/A:-3+1+4/B:xx-xx_xx",
                "b^o-N+s=a/A:-2+2+3/B:xx-xx_xx",
                "o^N-s+a=i/A:-1+3+2/B:xx-xx_xx",
                "N^s-a+i=sil/A:-1+3+2/B:xx-xx_xx",
                "s^a-i+sil=xx/A:0+4+1/B:xx-xx_xx",
                "a^i-sil+xx=xx/A:xx+xx+xx/B:xx-xx_xx",
            };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            Assert.AreEqual(new[] { "sil", "b", "o", "N", "s", "a", "i", "sil" }, phonemes);
        }

        [Test]
        public void Integration_Bonsai_AValuesCorrect()
        {
            var labels = new[]
            {
                "xx^xx-sil+b=o/A:xx+xx+xx/B:xx-xx_xx",
                "xx^sil-b+o=N/A:-3+1+4/B:xx-xx_xx",
                "sil^b-o+N=s/A:-3+1+4/B:xx-xx_xx",
                "b^o-N+s=a/A:-2+2+3/B:xx-xx_xx",
                "o^N-s+a=i/A:-1+3+2/B:xx-xx_xx",
                "N^s-a+i=sil/A:-1+3+2/B:xx-xx_xx",
                "s^a-i+sil=xx/A:0+4+1/B:xx-xx_xx",
                "a^i-sil+xx=xx/A:xx+xx+xx/B:xx-xx_xx",
            };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            // sil: xx→0
            Assert.AreEqual(0, a1[0]);
            Assert.AreEqual(0, a2[0]);
            Assert.AreEqual(0, a3[0]);

            // b: -3, 1, 4
            Assert.AreEqual(-3, a1[1]);
            Assert.AreEqual(1, a2[1]);
            Assert.AreEqual(4, a3[1]);

            // o: -3, 1, 4 (same mora as b)
            Assert.AreEqual(-3, a1[2]);
            Assert.AreEqual(1, a2[2]);
            Assert.AreEqual(4, a3[2]);

            // N: -2, 2, 3
            Assert.AreEqual(-2, a1[3]);
            Assert.AreEqual(2, a2[3]);
            Assert.AreEqual(3, a3[3]);

            // i (last phoneme): 0, 4, 1
            Assert.AreEqual(0, a1[6]);
            Assert.AreEqual(4, a2[6]);
            Assert.AreEqual(1, a3[6]);

            // trailing sil: xx→0
            Assert.AreEqual(0, a1[7]);
            Assert.AreEqual(0, a2[7]);
            Assert.AreEqual(0, a3[7]);
        }

        [Test]
        public void Integration_Korewa_PhonemesExtractedCorrectly()
        {
            // 「これは」: k o r e w a
            var labels = new[]
            {
                "xx^xx-sil+k=o/A:xx+xx+xx/B:xx-xx_xx",
                "xx^sil-k+o=r/A:-2+1+3/B:xx-xx_xx",
                "sil^k-o+r=e/A:-2+1+3/B:xx-xx_xx",
                "k^o-r+e=w/A:-1+2+2/B:xx-xx_xx",
                "o^r-e+w=a/A:-1+2+2/B:xx-xx_xx",
                "r^e-w+a=sil/A:0+3+1/B:xx-xx_xx",
                "e^w-a+sil=xx/A:0+3+1/B:xx-xx_xx",
                "w^a-sil+xx=xx/A:xx+xx+xx/B:xx-xx_xx",
            };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            Assert.AreEqual(new[] { "sil", "k", "o", "r", "e", "w", "a", "sil" }, phonemes);
        }

        [Test]
        public void Integration_Korewa_AValuesCorrect()
        {
            var labels = new[]
            {
                "xx^xx-sil+k=o/A:xx+xx+xx/B:xx-xx_xx",
                "xx^sil-k+o=r/A:-2+1+3/B:xx-xx_xx",
                "sil^k-o+r=e/A:-2+1+3/B:xx-xx_xx",
                "k^o-r+e=w/A:-1+2+2/B:xx-xx_xx",
                "o^r-e+w=a/A:-1+2+2/B:xx-xx_xx",
                "r^e-w+a=sil/A:0+3+1/B:xx-xx_xx",
                "e^w-a+sil=xx/A:0+3+1/B:xx-xx_xx",
                "w^a-sil+xx=xx/A:xx+xx+xx/B:xx-xx_xx",
            };
            HtsLabelParser.ParseAll(labels, out string[] phonemes, out int[] a1, out int[] a2, out int[] a3);

            // k: -2, 1, 3
            Assert.AreEqual(-2, a1[1]);
            Assert.AreEqual(1, a2[1]);
            Assert.AreEqual(3, a3[1]);

            // r: -1, 2, 2
            Assert.AreEqual(-1, a1[3]);
            Assert.AreEqual(2, a2[3]);
            Assert.AreEqual(2, a3[3]);

            // w: 0, 3, 1
            Assert.AreEqual(0, a1[5]);
            Assert.AreEqual(3, a2[5]);
            Assert.AreEqual(1, a3[5]);
        }

        // =====================================================================
        // エッジケース
        // =====================================================================

        [Test]
        public void ParsePhoneme_MinimalLabel_NoBField()
        {
            string label = "xx^xx-a+b=c/A:1+2+3";
            Assert.AreEqual("a", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_LabelWithoutSlash()
        {
            // '/' なしの場合でも '-' と '+' でパース可能
            string label = "xx^xx-k+a=i";
            Assert.AreEqual("k", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParseA_EmptyAField()
        {
            // /A: の直後が / の場合
            string label = "xx^sil-b+o=N/A:/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            Assert.AreEqual(0, a1);
            Assert.AreEqual(0, a2);
            Assert.AreEqual(0, a3);
        }

        [Test]
        public void ParseA_SinglePlusOnly()
        {
            // /A: に '+' が1つだけの場合（不正形式）→ 0 返却
            string label = "xx^sil-b+o=N/A:1+2/B:xx-xx_xx";
            HtsLabelParser.ParseA(label, out int a1, out int a2, out int a3);
            // secondPlus が見つからない → a1=0, a2=0, a3=0
            Assert.AreEqual(0, a1);
            Assert.AreEqual(0, a2);
            Assert.AreEqual(0, a3);
        }

        [Test]
        public void ParsePhoneme_ConsonantM()
        {
            string label = "a^k-m+a=t/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("m", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_ConsonantT()
        {
            string label = "a^k-t+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("t", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_ConsonantS()
        {
            string label = "a^k-s+u=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("s", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_ConsonantH()
        {
            string label = "a^k-h+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("h", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VoicedZ()
        {
            string label = "a^k-z+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("z", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VoicedD()
        {
            string label = "a^k-d+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("d", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_VoicedG()
        {
            string label = "a^k-g+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("g", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Hy()
        {
            string label = "a^k-hy+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("hy", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Ny()
        {
            string label = "a^k-ny+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("ny", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Ky()
        {
            string label = "a^k-ky+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("ky", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Gy()
        {
            string label = "a^k-gy+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("gy", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_By()
        {
            string label = "a^k-by+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("by", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Py()
        {
            string label = "a^k-py+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("py", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_My()
        {
            string label = "a^k-my+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("my", HtsLabelParser.ParsePhoneme(label));
        }

        [Test]
        public void ParsePhoneme_Ry()
        {
            string label = "a^k-ry+a=r/A:1+2+3/B:xx-xx_xx";
            Assert.AreEqual("ry", HtsLabelParser.ParsePhoneme(label));
        }
    }
}
