using System;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// 音素列の前処理ユーティリティ。
    /// SBV2 モデルは add_blank=true で学習されているため、
    /// 推論時に blank トークンを挟む必要がある。
    /// </summary>
    public static class PhonemeUtils
    {
        /// <summary>
        /// Python commons.intersperse(lst, item) と同等。
        /// [a, b, c] → [filler, a, filler, b, filler, c, filler]  (長さ 2N+1)
        /// </summary>
        public static int[] Intersperse(int[] src, int filler)
        {
            int[] result = new int[src.Length * 2 + 1];
            Array.Fill(result, filler);
            for (int i = 0; i < src.Length; i++)
                result[i * 2 + 1] = src[i];
            return result;
        }

        /// <summary>
        /// word2ph を blank 挿入に合わせて調整する。
        /// 各値を×2し、先頭に+1する（先頭 blank トークン分）。
        /// 調整後の合計 = 2×元合計+1 = interleaved 音素長と一致。
        /// </summary>
        public static int[] AdjustWord2PhForBlanks(int[] word2ph)
        {
            int[] result = new int[word2ph.Length];
            for (int i = 0; i < word2ph.Length; i++)
                result[i] = word2ph[i] * 2;
            result[0] += 1;
            return result;
        }
    }
}
