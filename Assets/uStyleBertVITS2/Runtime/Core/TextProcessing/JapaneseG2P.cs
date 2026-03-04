using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using uStyleBertVITS2.Diagnostics;
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

        /// <summary>
        /// 日本語トーンオフセット。Python の LANGUAGE_TONE_START_MAP["JP"] = NUM_ZH_TONES = 6。
        /// モデルは日本語トーンを 6(低)/7(高) として学習しているため、ComputeTone の 0/1 にこの値を加算する。
        /// </summary>
        private const int JapaneseToneOffset = 6;

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
            string phonemeStr = Marshal.PtrToStringUTF8(nativeResult.phonemes) ?? string.Empty;
            string[] rawPhonemes = phonemeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // A1 (アクセント核位置), A2 (句内モーラ位置), A3 (句内モーラ数) を読み取り
            int[] a1Values = new int[count];
            int[] a2Values = new int[count];
            int[] a3Values = new int[count];
            for (int i = 0; i < count; i++)
            {
                a1Values[i] = Marshal.ReadInt32(nativeResult.prosodyA1, i * sizeof(int));
                a2Values[i] = Marshal.ReadInt32(nativeResult.prosodyA2, i * sizeof(int));
                a3Values[i] = Marshal.ReadInt32(nativeResult.prosodyA3, i * sizeof(int));
            }

            // テキストから句読点キューを構築
            // Python: 句読点は音素列に保持される (","=106, "."=107 等)
            var punctQueue = new Queue<int>();
            foreach (char c in originalText)
            {
                int pid = MapPunctuationToSBV2(c);
                if (pid >= 0) punctQueue.Enqueue(pid);
            }

            // プロソディからトーンを事前計算（Python の累積トーンアルゴリズムを再現）
            int phonemeCount = Math.Min(rawPhonemes.Length, count);
            int[] precomputedTones = ComputeTonesFromProsody(
                rawPhonemes, a1Values, a2Values, a3Values, phonemeCount);

            // SBV2形式に変換
            var phonemeIds = new List<int>(count + 2);
            var tones = new List<int>(count + 2);
            var languageIds = new List<int>(count + 2);

            // 先頭PAD (Python: phone_tone_list = [("_", 0)] + ...)
            phonemeIds.Add(_mapper.PadId);
            tones.Add(JapaneseToneOffset);
            languageIds.Add(JapaneseLanguageId);

            // OpenJTalk は先頭に発話境界 pau を返す (Python の sil に相当)。
            // 先頭/末尾PAD で代替するため、最初の pau はスキップする。
            bool firstPauSkipped = false;

            for (int i = 0; i < phonemeCount; i++)
            {
                string phoneme = rawPhonemes[i];
                if (string.IsNullOrEmpty(phoneme)) continue;

                // sil (文頭/文末無音) はスキップ（先頭/末尾PADで代替）
                if (phoneme == "sil" || phoneme == "silB" || phoneme == "silE")
                    continue;

                if (phoneme == "pau")
                {
                    if (!firstPauSkipped)
                    {
                        // 発話先頭の境界 pau → スキップ (PAD で代替済み)
                        firstPauSkipped = true;
                        continue;
                    }

                    if (punctQueue.Count > 0)
                    {
                        // 句読点に対応する pau → 実際の句読点シンボルにマッピング
                        phonemeIds.Add(punctQueue.Dequeue());
                    }
                    else
                    {
                        // 末尾境界 or 余剰 pau → スキップ
                        continue;
                    }
                }
                else
                {
                    firstPauSkipped = true; // 非pau を見たので先頭境界は過ぎた
                    int id = _mapper.GetId(phoneme);
                    phonemeIds.Add(id);
                }

                // 事前計算済みトーンを使用 + 日本語オフセット(+6)を加算
                int tone = precomputedTones[i] + JapaneseToneOffset;
                tones.Add(tone);

                languageIds.Add(JapaneseLanguageId);
            }

            // 末尾PAD (Python: phone_tone_list = ... + [("_", 0)])
            phonemeIds.Add(_mapper.PadId);
            tones.Add(JapaneseToneOffset);
            languageIds.Add(JapaneseLanguageId);

            // word2ph: 仮名テーブルベースの正確なアライメント
            int[] word2ph = PhonemeCharacterAligner.ComputeWord2Ph(originalText, phonemeIds.Count);

            // デバッグログ: G2P結果のダンプ
            if (TTSDebugLog.Enabled)
            {
                // A1/A2/A3 は phonemeCount 分のみ表示
                var a1Slice = new int[phonemeCount];
                var a2Slice = new int[phonemeCount];
                var a3Slice = new int[phonemeCount];
                Array.Copy(a1Values, a1Slice, phonemeCount);
                Array.Copy(a2Values, a2Slice, phonemeCount);
                Array.Copy(a3Values, a3Slice, phonemeCount);
                var preToneSlice = new int[phonemeCount];
                Array.Copy(precomputedTones, preToneSlice, phonemeCount);

                TTSDebugLog.Log("TTS.G2P",
                    $"\"{originalText}\" → {phonemeIds.Count} phonemes\n" +
                    $"  Raw: {phonemeStr}\n" +
                    $"  A1: [{string.Join(", ", a1Slice)}]\n" +
                    $"  A2: [{string.Join(", ", a2Slice)}]\n" +
                    $"  A3: [{string.Join(", ", a3Slice)}]\n" +
                    $"  PreTones: [{string.Join(", ", preToneSlice)}]\n" +
                    $"  IDs: [{string.Join(", ", phonemeIds)}]\n" +
                    $"  Tones: [{string.Join(", ", tones)}]\n" +
                    $"  Word2Ph: [{string.Join(", ", word2ph)}] (sum={Sum(word2ph)})");
            }

            return new G2PResult(
                phonemeIds.ToArray(),
                tones.ToArray(),
                languageIds.ToArray(),
                word2ph);
        }

        /// <summary>
        /// テキスト中の文字が句読点の場合、対応する SBV2 シンボル ID を返す。
        /// 句読点でない場合は -1 を返す。
        /// Python の normalizer.py の __REPLACE_MAP + PUNCTUATIONS に対応。
        /// </summary>
        private int MapPunctuationToSBV2(char c)
        {
            return c switch
            {
                '、' or ',' or '，' => _mapper.GetId(","),    // 106
                '。' or '.' or '．' => _mapper.GetId("."),    // 107
                '!' or '！' => _mapper.GetId("!"),            // 103
                '?' or '？' => _mapper.GetId("?"),            // 104
                '\u2026' => _mapper.GetId("\u2026"),           // 105 (…)
                '・' => _mapper.GetId(","),                    // 106 (中黒→コンマ)
                '\'' => _mapper.GetId("'"),                    // 108
                '-' => _mapper.GetId("-"),                     // 109
                _ => -1
            };
        }

        /// <summary>
        /// ProsodyToneCalculator への委譲ラッパー。後方互換のため維持。
        /// </summary>
        internal static int[] ComputeTonesFromProsody(
            string[] rawPhonemes, int[] a1, int[] a2, int[] a3, int count)
            => ProsodyToneCalculator.ComputeTonesFromProsody(rawPhonemes, a1, a2, a3, count);

        private static int Sum(int[] values)
        {
            int s = 0;
            for (int i = 0; i < values.Length; i++) s += values[i];
            return s;
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
