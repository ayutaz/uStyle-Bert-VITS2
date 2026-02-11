using System.Text;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// テキスト正規化。uPiper の TextNormalizer.cs をベースに namespace 変更。
    /// 全角→半角変換、スペース正規化等を行う。
    /// </summary>
    public static class TextNormalizer
    {
        /// <summary>
        /// テキストを正規化する。
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            bool prevWasSpace = true; // 先頭スペースをスキップするために true で開始

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // 全角英数字→半角
                if (c >= '\uFF01' && c <= '\uFF5E')
                {
                    sb.Append((char)(c - 0xFEE0));
                    prevWasSpace = false;
                    continue;
                }

                // 全角スペース→半角スペースとして扱う
                if (c == '\u3000')
                    c = ' ';

                // 連続スペース圧縮 + 先頭スペーススキップ
                if (c == ' ')
                {
                    if (!prevWasSpace)
                    {
                        sb.Append(' ');
                        prevWasSpace = true;
                    }
                    continue;
                }

                sb.Append(c);
                prevWasSpace = false;
            }

            // 末尾スペース除去
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--;

            return sb.ToString();
        }
    }
}
