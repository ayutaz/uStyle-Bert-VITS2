using System;
using System.Runtime.InteropServices;

namespace uStyleBertVITS2.Native
{
    /// <summary>
    /// OpenJTalk ネイティブライブラリ P/Invoke。
    /// uPiper の OpenJTalkNative.cs をベースに namespace 変更。
    /// openjtalk_wrapper.dll を使用。
    /// </summary>
    public static class OpenJTalkNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string DllName = "__Internal";
#else
        private const string DllName = "openjtalk_wrapper";
#endif

        /// <summary>
        /// 音素結果構造体（ネイティブ側から返される）。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativePhonemeResult
        {
            public IntPtr phonemes;   // char** — null終端の音素文字列配列
            public int phonemeCount;
        }

        /// <summary>
        /// プロソディ付き音素結果構造体。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeProsodyPhonemeResult
        {
            public IntPtr phonemes;        // char** — null終端の音素文字列配列
            public IntPtr prosodyValues;   // int* — プロソディ値配列
            public int phonemeCount;
        }

        /// <summary>
        /// OpenJTalkインスタンスを作成する。
        /// </summary>
        /// <param name="dictPath">辞書ディレクトリへのパス (UTF-8)</param>
        /// <returns>OpenJTalkハンドル (IntPtr.Zero なら失敗)</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_create(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string dictPath);

        /// <summary>
        /// OpenJTalkインスタンスを破棄する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_destroy(IntPtr handle);

        /// <summary>
        /// テキストを音素に変換する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int openjtalk_phonemize(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            out NativePhonemeResult result);

        /// <summary>
        /// テキストを音素に変換する（プロソディ情報付き）。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int openjtalk_phonemize_with_prosody(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            out NativeProsodyPhonemeResult result);

        /// <summary>
        /// 音素結果のメモリを解放する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_phoneme_result(ref NativePhonemeResult result);

        /// <summary>
        /// プロソディ付き音素結果のメモリを解放する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_prosody_result(ref NativeProsodyPhonemeResult result);
    }
}
