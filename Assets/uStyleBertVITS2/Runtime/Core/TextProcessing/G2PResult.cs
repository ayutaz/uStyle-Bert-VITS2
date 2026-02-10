namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// G2P処理の結果。全配列は同じ長さ (seq_len)。
    /// word2ph は BERT トークンと音素のアライメント情報で、合計が seq_len と一致する。
    /// </summary>
    public readonly struct G2PResult
    {
        /// <summary>SBV2音素トークンID配列 [seq_len]</summary>
        public readonly int[] PhonemeIds;

        /// <summary>トーン/アクセント配列 [seq_len] (+6オフセット済み)</summary>
        public readonly int[] Tones;

        /// <summary>言語ID配列 [seq_len] (JP-Extraでは全て1)</summary>
        public readonly int[] LanguageIds;

        /// <summary>
        /// BERTトークンごとの音素数 [token_len]。
        /// 合計が PhonemeIds.Length と一致する。
        /// </summary>
        public readonly int[] Word2Ph;

        public G2PResult(int[] phonemeIds, int[] tones, int[] languageIds, int[] word2ph)
        {
            PhonemeIds = phonemeIds;
            Tones = tones;
            LanguageIds = languageIds;
            Word2Ph = word2ph;
        }
    }
}
