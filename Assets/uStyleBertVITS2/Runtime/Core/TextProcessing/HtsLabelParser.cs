using System;
using System.Collections.Generic;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// HTS full context labels をパースして音素名と A1/A2/A3 プロソディ値を抽出する。
    /// ラベル形式: p2^p1-c+n1=n2/A:a1+a2+a3/B:b1-b2_b3/...
    ///
    /// A フィールド:
    ///   a1 = moraPos - accent + 1 (アクセント核相対位置)
    ///   a2 = moraPos + 1          (句内前方位置, 1始まり)
    ///   a3 = moraCount - moraPos  (句内後方位置)
    ///   sil/pau では "xx+xx+xx" → 0 として返す
    /// </summary>
    internal static class HtsLabelParser
    {
        /// <summary>
        /// HTSラベル列を一括パースして音素名配列と A1/A2/A3 配列を返す。
        /// </summary>
        public static void ParseAll(
            IReadOnlyList<string> labels,
            out string[] phonemes,
            out int[] a1, out int[] a2, out int[] a3)
        {
            int count = labels.Count;
            phonemes = new string[count];
            a1 = new int[count];
            a2 = new int[count];
            a3 = new int[count];

            for (int i = 0; i < count; i++)
            {
                string label = labels[i];
                phonemes[i] = ParsePhoneme(label);
                ParseA(label, out a1[i], out a2[i], out a3[i]);
            }
        }

        /// <summary>
        /// 単一のHTSラベルから現在の音素名を抽出する。
        /// 音素コンテキスト部 (最初の '/' 以前): p2^p1-c+n1=n2
        /// </summary>
        public static string ParsePhoneme(string label)
        {
            // 音素コンテキスト部は最初の '/' まで
            int slashIdx = label.IndexOf('/');
            if (slashIdx < 0) slashIdx = label.Length;

            // '-' と '+' の間が現在の音素
            int dashIdx = label.IndexOf('-');
            if (dashIdx < 0 || dashIdx >= slashIdx) return "xx";

            int plusIdx = label.IndexOf('+', dashIdx + 1);
            if (plusIdx < 0 || plusIdx >= slashIdx) return "xx";

            return label.Substring(dashIdx + 1, plusIdx - dashIdx - 1);
        }

        /// <summary>
        /// 単一のHTSラベルから A1, A2, A3 プロソディ値を抽出する。
        /// /A:a1+a2+a3/ 形式。"xx" の場合は 0 を返す。
        /// </summary>
        public static void ParseA(string label, out int a1, out int a2, out int a3)
        {
            a1 = 0;
            a2 = 0;
            a3 = 0;

            // "/A:" を検索
            int aStart = label.IndexOf("/A:", StringComparison.Ordinal);
            if (aStart < 0) return;

            aStart += 3; // "/A:" の直後

            // 次の "/" までが A フィールド
            int aEnd = label.IndexOf('/', aStart);
            if (aEnd < 0) aEnd = label.Length;

            // "a1+a2+a3" を '+' で分割 (a1 は負値の可能性あり: e.g. "-3+1+4")
            int len = aEnd - aStart;
            if (len <= 0) return;

            // 最初の '+' を探す (a1 が負の場合 '-' で始まるため IndexOf('+') で探す)
            int firstPlus = label.IndexOf('+', aStart);
            if (firstPlus < 0 || firstPlus >= aEnd) return;

            int secondPlus = label.IndexOf('+', firstPlus + 1);
            if (secondPlus < 0 || secondPlus >= aEnd) return;

            a1 = ParseIntOrZero(label, aStart, firstPlus - aStart);
            a2 = ParseIntOrZero(label, firstPlus + 1, secondPlus - firstPlus - 1);
            a3 = ParseIntOrZero(label, secondPlus + 1, aEnd - secondPlus - 1);
        }

        private static int ParseIntOrZero(string s, int start, int length)
        {
            if (length <= 0) return 0;
            if (s[start] == 'x') return 0; // "xx"

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            if (int.TryParse(s.AsSpan(start, length), out int result))
                return result;
#else
            if (int.TryParse(s.Substring(start, length), out int result))
                return result;
#endif
            return 0;
        }
    }
}
