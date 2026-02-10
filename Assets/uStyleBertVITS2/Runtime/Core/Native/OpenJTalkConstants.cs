namespace uStyleBertVITS2.Native
{
    /// <summary>
    /// OpenJTalk 辞書関連の定数。
    /// NAIST JDIC 辞書ファイル一覧とパス。
    /// </summary>
    public static class OpenJTalkConstants
    {
        /// <summary>
        /// NAIST JDIC 辞書の必須ファイル名一覧。
        /// </summary>
        public static readonly string[] RequiredDictionaryFiles =
        {
            "sys.dic",          // システム辞書 (~103MB)
            "unk.dic",          // 未知語辞書
            "char.bin",         // 文字定義
            "matrix.bin",       // 接続コスト行列 (~3.7MB)
            "left-id.def",      // 左文脈ID定義
            "right-id.def",     // 右文脈ID定義
            "pos-id.def",       // 品詞タグ定義
            "rewrite.def",      // 書き換えルール
        };

        /// <summary>
        /// StreamingAssets 以下のデフォルト辞書ディレクトリパス。
        /// </summary>
        public const string DefaultDictionaryRelativePath = "uStyleBertVITS2/OpenJTalkDic";
    }
}
