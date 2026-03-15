using System.Collections.Generic;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// プロソディ情報 (A1/A2/A3) から SBV2 トーン (0/1) を計算する共有ユーティリティ。
    /// DotNetG2PJapaneseG2P から使用される。
    /// </summary>
    internal static class ProsodyToneCalculator
    {
        /// <summary>
        /// Python の __pyopenjtalk_g2p_prosody() + __g2phone_tone_wo_punct() を再現。
        /// 全音素の A1/A2/A3 からプロソディ遷移を検出し、累積トーンで各音素の 0/1 を計算する。
        ///
        /// Python のアルゴリズム:
        /// 1. 音素を順に走査し、隣接する A1/A2/A3 の変化からプロソディ記号 [, ], # を検出
        /// 2. currentTone 状態変数を管理し各音素に割り当て
        /// 3. アクセント句ごとに FixPhoneTone() で 0/1 に正規化
        /// </summary>
        public static int[] ComputeTonesFromProsody(
            string[] rawPhonemes, int[] a1, int[] a2, int[] a3, int count)
        {
            // 結果配列（各音素の最終トーン 0 or 1）
            int[] result = new int[count];

            // アクセント句バッファ: (元のインデックス, 累積トーン) のペア
            var phraseIndices = new List<int>();
            var phraseTones = new List<int>();
            int currentTone = 0;

            for (int i = 0; i < count; i++)
            {
                string ph = rawPhonemes[i];

                // sil/pau は句境界: バッファ確定 + tone=0 をセット
                if (ph == "sil" || ph == "silB" || ph == "silE" || ph == "pau")
                {
                    // 溜まったバッファを正規化して結果に書き込み
                    if (phraseIndices.Count > 0)
                    {
                        FixPhoneTone(phraseIndices, phraseTones, result);
                        phraseIndices.Clear();
                        phraseTones.Clear();
                    }
                    result[i] = 0;
                    currentTone = 0;
                    continue;
                }

                // 通常音素: 現在のトーンを記録
                phraseIndices.Add(i);
                phraseTones.Add(currentTone);

                // 次の音素の A2 を参照してプロソディ記号を検出
                int a2Next = (i + 1 < count) ? a2[i + 1] : 0;

                // Python の条件分岐（if/elif の優先順位が重要）:
                // 1. # (句境界): a3[i]==1 && a2Next==1 && IsVowelOrNOrCl(ph)
                if (a3[i] == 1 && a2Next == 1 && IsVowelOrNOrCl(ph))
                {
                    // アクセント句境界: バッファ確定してリセット
                    FixPhoneTone(phraseIndices, phraseTones, result);
                    phraseIndices.Clear();
                    phraseTones.Clear();
                    currentTone = 0;
                }
                // 2. ] (下降): a1[i]==0 && a2Next==a2[i]+1 && a2[i]!=a3[i]
                else if (a1[i] == 0 && a2Next == a2[i] + 1 && a2[i] != a3[i])
                {
                    currentTone -= 1;
                }
                // 3. [ (上昇): a2[i]==1 && a2Next==2
                else if (a2[i] == 1 && a2Next == 2)
                {
                    currentTone += 1;
                }
            }

            // 末尾のバッファを確定
            if (phraseIndices.Count > 0)
            {
                FixPhoneTone(phraseIndices, phraseTones, result);
            }

            return result;
        }

        /// <summary>
        /// Python の __fix_phone_tone() を移植。
        /// アクセント句内のトーン集合を 0/1 に正規化する。
        /// - {0, 1} → そのまま
        /// - {-1, 0} → -1→0, 0→1 にシフト
        /// - {0} のみ → そのまま
        /// </summary>
        private static void FixPhoneTone(
            List<int> indices, List<int> tones, int[] result)
        {
            // トーンの最小値・最大値を求める
            int minTone = int.MaxValue;
            int maxTone = int.MinValue;
            for (int i = 0; i < tones.Count; i++)
            {
                if (tones[i] < minTone) minTone = tones[i];
                if (tones[i] > maxTone) maxTone = tones[i];
            }

            // 正規化: 最小値が0未満なら全体を+1シフトして {0,1} にする
            int shift = (minTone < 0) ? 1 : 0;

            for (int i = 0; i < tones.Count; i++)
            {
                int t = tones[i] + shift;
                // 0/1 にクランプ
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                result[indices[i]] = t;
            }
        }

        /// <summary>
        /// Python の p3 in "aeiouAEIOUNcl" 判定。
        /// 母音(大文字=無声化含む)、撥音(N)、促音(cl) を判定する。
        /// </summary>
        private static bool IsVowelOrNOrCl(string phoneme)
            => phoneme is "a" or "i" or "u" or "e" or "o"
                or "A" or "I" or "U" or "E" or "O"
                or "N" or "cl";
    }
}
