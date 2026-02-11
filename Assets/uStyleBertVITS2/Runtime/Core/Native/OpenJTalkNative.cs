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
        /// openjtalk_phonemize() はこのポインタを返す。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativePhonemeResult
        {
            public IntPtr phonemes;       // char* — スペース区切り音素文字列
            public IntPtr phonemeIds;     // int* — 音素ID配列
            public int phonemeCount;
            public IntPtr durations;      // float* — 各音素の持続時間
            public float totalDuration;
        }

        /// <summary>
        /// プロソディ付き音素結果構造体。
        /// openjtalk_phonemize_with_prosody() はこのポインタを返す。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeProsodyPhonemeResult
        {
            public IntPtr phonemes;        // char* — スペース区切り音素文字列
            public IntPtr prosodyA1;       // int* — A1: アクセント核からの相対位置
            public IntPtr prosodyA2;       // int* — A2: アクセント句内位置 (1-based)
            public IntPtr prosodyA3;       // int* — A3: アクセント句のモーラ数
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
        /// 戻り値は PhonemeResult* ポインタ。失敗時は IntPtr.Zero。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_phonemize(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        /// <summary>
        /// テキストを音素に変換する（プロソディ情報付き）。
        /// 戻り値は ProsodyPhonemeResult* ポインタ。失敗時は IntPtr.Zero。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_phonemize_with_prosody(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        /// <summary>
        /// 音素結果のメモリを解放する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_result(IntPtr result);

        /// <summary>
        /// プロソディ付き音素結果のメモリを解放する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_prosody_result(IntPtr result);

        /// <summary>
        /// 最後のエラーコードを取得する。
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int openjtalk_get_last_error(IntPtr handle);
    }
}
