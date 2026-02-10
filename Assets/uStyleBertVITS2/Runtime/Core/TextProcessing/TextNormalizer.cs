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

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // 全角英数字→半角
                if (c >= '\uFF01' && c <= '\uFF5E')
                {
                    sb.Append((char)(c - 0xFEE0));
                    continue;
                }

                // 全角スペース→半角
                if (c == '\u3000')
                {
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c);
            }

            // 連続スペースを1つに
            string result = sb.ToString();
            while (result.Contains("  "))
                result = result.Replace("  ", " ");

            return result.Trim();
        }
    }
}
