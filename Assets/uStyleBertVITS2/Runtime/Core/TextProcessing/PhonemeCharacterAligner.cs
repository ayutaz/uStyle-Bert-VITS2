using System.Collections.Generic;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// BERTトークン（文字）と音素列のアライメントを計算する。
    /// 仮名文字から正確な音素数を推定し、word2ph 配列を生成する。
    ///
    /// word2ph[i] = BERTトークン i に対応する音素数
    /// word2ph の合計 = 音素列長 (phoneSeqLen)
    /// BERTトークン列: [CLS] + 文字列 + [SEP]
    /// </summary>
    public static class PhonemeCharacterAligner
    {
        /// <summary>
        /// 仮名文字ごとの音素数テーブル。
        /// OpenJTalk → SBV2 音素変換後の音素数を定義。
        /// 例: "か" → k, a → 2音素、"ん" → N → 1音素
        /// </summary>
        private static readonly Dictionary<char, int> s_kanaPhonemeCount = BuildKanaTable();

        /// <summary>
        /// word2ph を計算する。
        /// [CLS] に先頭SP(1音素)、[SEP] に末尾SP(1音素)、
        /// 各文字は仮名テーブルまたはフォールバックで音素数を推定。
        /// </summary>
        /// <param name="text">入力テキスト（正規化済み）</param>
        /// <param name="phoneSeqLen">音素列の総長（先頭SP・末尾SP含む）</param>
        /// <returns>word2ph 配列 [tokenLen] (tokenLen = text.Length + 2)</returns>
        public static int[] ComputeWord2Ph(string text, int phoneSeqLen)
        {
            int tokenLen = text.Length + 2; // [CLS] + text + [SEP]
            int[] word2ph = new int[tokenLen];

            if (text.Length == 0)
            {
                word2ph[0] = phoneSeqLen;
                return word2ph;
            }

            // [CLS] → 先頭SP (1音素)
            word2ph[0] = 1;
            // [SEP] → 末尾SP (1音素)
            word2ph[tokenLen - 1] = 1;

            // 仮名文字の音素数を推定
            int knownSum = 2; // CLS(1) + SEP(1)
            int unknownCount = 0;
            int[] charPhonemes = new int[text.Length];

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (s_kanaPhonemeCount.TryGetValue(c, out int count))
                {
                    charPhonemes[i] = count;
                    knownSum += count;
                }
                else if (IsKatakana(c))
                {
                    // カタカナをひらがなに変換して再検索
                    char hiragana = (char)(c - 0x60);
                    if (s_kanaPhonemeCount.TryGetValue(hiragana, out int hCount))
                    {
                        charPhonemes[i] = hCount;
                        knownSum += hCount;
                    }
                    else
                    {
                        charPhonemes[i] = -1; // 未知
                        unknownCount++;
                    }
                }
                else
                {
                    charPhonemes[i] = -1; // 漢字・句読点等 = 未知
                    unknownCount++;
                }
            }

            // 未知の文字に残りの音素数を比例配分
            int unknownTotal = phoneSeqLen - knownSum;
            if (unknownCount > 0 && unknownTotal > 0)
            {
                int perUnknown = unknownTotal / unknownCount;
                int extraUnknown = unknownTotal % unknownCount;
                int extraIdx = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    if (charPhonemes[i] == -1)
                    {
                        charPhonemes[i] = perUnknown + (extraIdx < extraUnknown ? 1 : 0);
                        extraIdx++;
                    }
                }
            }
            else if (unknownCount > 0)
            {
                // 残りが 0 以下の場合（推定が過大）、未知文字に 0 を割り当て
                for (int i = 0; i < text.Length; i++)
                {
                    if (charPhonemes[i] == -1)
                        charPhonemes[i] = 0;
                }
            }

            // 推定合計が phoneSeqLen と一致しない場合の補正
            int totalEstimate = 2; // CLS + SEP
            for (int i = 0; i < text.Length; i++)
                totalEstimate += charPhonemes[i];

            int diff = phoneSeqLen - totalEstimate;
            if (diff != 0)
            {
                // 差分を最後の文字に加減
                charPhonemes[text.Length - 1] += diff;
                if (charPhonemes[text.Length - 1] < 0)
                    charPhonemes[text.Length - 1] = 0;
            }

            // word2ph に反映
            for (int i = 0; i < text.Length; i++)
            {
                word2ph[i + 1] = charPhonemes[i];
            }

            return word2ph;
        }

        private static bool IsKatakana(char c)
        {
            return c >= '\u30A0' && c <= '\u30FF';
        }

        private static Dictionary<char, int> BuildKanaTable()
        {
            var table = new Dictionary<char, int>();

            // 母音 (1音素)
            table['あ'] = 1; // a
            table['い'] = 1; // i
            table['う'] = 1; // u
            table['え'] = 1; // e
            table['お'] = 1; // o

            // か行 (2音素: 子音+母音)
            table['か'] = 2; // k,a
            table['き'] = 2; // k,i
            table['く'] = 2; // k,u
            table['け'] = 2; // k,e
            table['こ'] = 2; // k,o

            // さ行
            table['さ'] = 2; // s,a
            table['し'] = 2; // sh,i
            table['す'] = 2; // s,u
            table['せ'] = 2; // s,e
            table['そ'] = 2; // s,o

            // た行
            table['た'] = 2; // t,a
            table['ち'] = 2; // ch,i
            table['つ'] = 2; // ts,u
            table['て'] = 2; // t,e
            table['と'] = 2; // t,o

            // な行
            table['な'] = 2; // n,a
            table['に'] = 2; // n,i
            table['ぬ'] = 2; // n,u
            table['ね'] = 2; // n,e
            table['の'] = 2; // n,o

            // は行
            table['は'] = 2; // h,a
            table['ひ'] = 2; // h,i
            table['ふ'] = 2; // f,u (or h,u)
            table['へ'] = 2; // h,e
            table['ほ'] = 2; // h,o

            // ま行
            table['ま'] = 2; // m,a
            table['み'] = 2; // m,i
            table['む'] = 2; // m,u
            table['め'] = 2; // m,e
            table['も'] = 2; // m,o

            // や行
            table['や'] = 2; // y,a
            table['ゆ'] = 2; // y,u
            table['よ'] = 2; // y,o

            // ら行
            table['ら'] = 2; // r,a
            table['り'] = 2; // r,i
            table['る'] = 2; // r,u
            table['れ'] = 2; // r,e
            table['ろ'] = 2; // r,o

            // わ行
            table['わ'] = 2; // w,a
            table['を'] = 1; // o (助詞の場合)
            table['ん'] = 1; // N (撥音)

            // 濁音
            table['が'] = 2; // g,a
            table['ぎ'] = 2; // g,i
            table['ぐ'] = 2; // g,u
            table['げ'] = 2; // g,e
            table['ご'] = 2; // g,o
            table['ざ'] = 2; // z,a
            table['じ'] = 2; // j,i (or z,i)
            table['ず'] = 2; // z,u
            table['ぜ'] = 2; // z,e
            table['ぞ'] = 2; // z,o
            table['だ'] = 2; // d,a
            table['ぢ'] = 2; // j,i
            table['づ'] = 2; // z,u
            table['で'] = 2; // d,e
            table['ど'] = 2; // d,o
            table['ば'] = 2; // b,a
            table['び'] = 2; // b,i
            table['ぶ'] = 2; // b,u
            table['べ'] = 2; // b,e
            table['ぼ'] = 2; // b,o

            // 半濁音
            table['ぱ'] = 2; // p,a
            table['ぴ'] = 2; // p,i
            table['ぷ'] = 2; // p,u
            table['ぺ'] = 2; // p,e
            table['ぽ'] = 2; // p,o

            // 小文字/特殊
            table['っ'] = 1; // q (促音)
            table['ー'] = 1; // 長音 (前の母音の長音素)

            // 小文字仮名（拗音の一部）
            table['ぁ'] = 1; // a
            table['ぃ'] = 1; // i
            table['ぅ'] = 1; // u
            table['ぇ'] = 1; // e
            table['ぉ'] = 1; // o
            table['ゃ'] = 1; // (拗音の一部、前の子音と合わせて使用)
            table['ゅ'] = 1; // (拗音の一部)
            table['ょ'] = 1; // (拗音の一部)

            // 句読点・記号 (各1音素 — OpenJTalk が pau に変換 → SBV2 句読点シンボルにマッピング)
            // 日本語句読点
            table['、'] = 1;
            table['。'] = 1;
            table['！'] = 1;
            table['？'] = 1;
            table['，'] = 1;
            table['．'] = 1;
            table['…'] = 1;
            table['・'] = 1;
            // ASCII 句読点 (TextNormalizer の全角→半角変換後に出現しうる)
            table[','] = 1;
            table['.'] = 1;
            table['!'] = 1;
            table['?'] = 1;
            table['-'] = 1;
            table['\''] = 1;

            return table;
        }
    }
}
