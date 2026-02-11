using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using uStyleBertVITS2.Native;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// OpenJTalk ベースの日本語 G2P 実装。
    /// テキストから SBV2 入力テンソル用の音素ID・トーン・言語ID・word2ph を生成する。
    /// </summary>
    public class JapaneseG2P : IG2P
    {
        private const int JapaneseLanguageId = 1;
        private const int ToneOffset = 6;

        private readonly OpenJTalkHandle _handle;
        private readonly SBV2PhonemeMapper _mapper;
        private bool _disposed;

        public JapaneseG2P(string dictPath)
        {
            IntPtr rawHandle = OpenJTalkNative.openjtalk_create(dictPath);
            if (rawHandle == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to initialize OpenJTalk with dictionary: {dictPath}");

            _handle = new OpenJTalkHandle(rawHandle);
            _mapper = new SBV2PhonemeMapper();
        }

        public JapaneseG2P(string dictPath, SBV2PhonemeMapper mapper)
        {
            IntPtr rawHandle = OpenJTalkNative.openjtalk_create(dictPath);
            if (rawHandle == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Failed to initialize OpenJTalk with dictionary: {dictPath}");

            _handle = new OpenJTalkHandle(rawHandle);
            _mapper = mapper;
        }

        public G2PResult Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(JapaneseG2P));

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be null or empty.", nameof(text));

            // テキスト正規化
            text = TextNormalizer.Normalize(text);

            // OpenJTalkでプロソディ付き音素変換
            IntPtr resultPtr = OpenJTalkNative.openjtalk_phonemize_with_prosody(
                _handle.DangerousGetHandle(), text);

            if (resultPtr == IntPtr.Zero)
            {
                int err = OpenJTalkNative.openjtalk_get_last_error(_handle.DangerousGetHandle());
                throw new InvalidOperationException(
                    $"OpenJTalk phonemize failed with error code {err}");
            }

            try
            {
                var nativeResult = Marshal.PtrToStructure<OpenJTalkNative.NativeProsodyPhonemeResult>(resultPtr);
                return ConvertNativeResult(nativeResult, text);
            }
            finally
            {
                OpenJTalkNative.openjtalk_free_prosody_result(resultPtr);
            }
        }

        private G2PResult ConvertNativeResult(
            OpenJTalkNative.NativeProsodyPhonemeResult nativeResult, string originalText)
        {
            int count = nativeResult.phonemeCount;
            if (count == 0)
                throw new InvalidOperationException("OpenJTalk returned no phonemes.");

            // ネイティブ結果からマネージド配列にコピー
            // phonemes はスペース区切り文字列
            string phonemeStr = Marshal.PtrToStringUTF8(nativeResult.phonemes) ?? string.Empty;
            string[] rawPhonemes = phonemeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // prosodyA1 配列を読み取り（トーン計算に使用）
            int[] prosodyA1Values = new int[count];
            for (int i = 0; i < count; i++)
            {
                prosodyA1Values[i] = Marshal.ReadInt32(nativeResult.prosodyA1, i * sizeof(int));
            }

            // SBV2形式に変換
            var phonemeIds = new List<int>(count + 2);
            var tones = new List<int>(count + 2);
            var languageIds = new List<int>(count + 2);

            // 先頭SP
            phonemeIds.Add(_mapper.SpId);
            tones.Add(0);
            languageIds.Add(JapaneseLanguageId);

            int phonemeCount = Math.Min(rawPhonemes.Length, count);
            for (int i = 0; i < phonemeCount; i++)
            {
                string phoneme = rawPhonemes[i];
                if (string.IsNullOrEmpty(phoneme)) continue;

                // sil (文頭/文末無音) はスキップ（先頭/末尾SPで代替）
                if (phoneme == "sil" || phoneme == "silB" || phoneme == "silE")
                    continue;

                int id = _mapper.GetId(phoneme);
                phonemeIds.Add(id);

                // トーン計算: A1 プロソディ値にオフセットを加算
                int tone = (prosodyA1Values[i] > 0) ? prosodyA1Values[i] + ToneOffset : ToneOffset;
                tones.Add(tone);

                languageIds.Add(JapaneseLanguageId);
            }

            // 末尾SP
            phonemeIds.Add(_mapper.SpId);
            tones.Add(0);
            languageIds.Add(JapaneseLanguageId);

            // word2ph: テキストの各文字に対応する音素数を計算
            // 簡略化: [CLS]→1, 各文字→均等分割, [SEP]→0
            int[] word2ph = ComputeWord2Ph(originalText, phonemeIds.Count);

            return new G2PResult(
                phonemeIds.ToArray(),
                tones.ToArray(),
                languageIds.ToArray(),
                word2ph);
        }

        /// <summary>
        /// word2ph を計算する。
        /// BERTトークン列 [CLS] + 文字列 + [SEP] の各トークンに対応する音素数。
        /// 合計が phoneSeqLen と一致する必要がある。
        /// </summary>
        private static int[] ComputeWord2Ph(string text, int phoneSeqLen)
        {
            int tokenLen = text.Length + 2; // [CLS] + text + [SEP]
            int[] word2ph = new int[tokenLen];

            if (text.Length == 0)
            {
                // 空テキスト: [CLS]にすべて割り当て
                word2ph[0] = phoneSeqLen;
                return word2ph;
            }

            // [CLS] に先頭SPを割り当て
            word2ph[0] = 1;

            // 残りの音素数を文字数で均等分割
            int remaining = phoneSeqLen - 1; // 先頭SP分を引く
            int perChar = remaining / text.Length;
            int extra = remaining % text.Length;

            for (int i = 0; i < text.Length; i++)
            {
                word2ph[i + 1] = perChar + (i < extra ? 1 : 0);
            }

            // [SEP] は0（音素なし）
            word2ph[tokenLen - 1] = 0;

            return word2ph;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _handle?.Dispose();
                _disposed = true;
            }
        }
    }
}
