using System;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// G2P (Grapheme-to-Phoneme) バックエンドの抽象化。
    /// OpenJTalk以外のバックエンド (リモートAPI等) への切替を可能にする。
    /// </summary>
    public interface IG2P : IDisposable
    {
        /// <summary>
        /// テキストを音素列に変換する。
        /// </summary>
        G2PResult Process(string text);
    }
}
