#if USBV2_DOTNET_G2P_AVAILABLE
using System;
using System.Collections.Generic;
using DotNetG2P;
using DotNetG2P.MeCab;
using uStyleBertVITS2.Diagnostics;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// dot-net-g2p ベースの日本語 G2P 実装。
    /// Pure C# で OpenJTalk 互換の音素変換を行い、ネイティブ DLL 依存を排除する。
    ///
    /// パイプライン:
    /// 1. TextNormalizer.Normalize() でテキスト正規化
    /// 2. G2PEngine.ToFullContextLabels() で HTS ラベル取得
    /// 3. HtsLabelParser で音素名・A1/A2/A3 抽出
    /// 4. ProsodyToneCalculator.ComputeTonesFromProsody() でトーン計算
    /// 5. SBV2PhonemeMapper で音素ID変換
    /// 6. PhonemeCharacterAligner で word2ph 計算
    /// </summary>
    public sealed class DotNetG2PJapaneseG2P : IG2P
    {
        private const int JapaneseLanguageId = 1;
        private const int JapaneseToneOffset = 6;

        private readonly MeCabTokenizer _tokenizer;
        private readonly G2PEngine _engine;
        private readonly SBV2PhonemeMapper _mapper;
        private bool _disposed;

        public DotNetG2PJapaneseG2P(string dictPath)
        {
            _tokenizer = new MeCabTokenizer(dictPath);
            _engine = new G2PEngine(
                _tokenizer,
                new G2POptions(enableTextNormalization: false));
            _mapper = new SBV2PhonemeMapper();
        }

        public DotNetG2PJapaneseG2P(string dictPath, SBV2PhonemeMapper mapper)
        {
            _tokenizer = new MeCabTokenizer(dictPath);
            _engine = new G2PEngine(
                _tokenizer,
                new G2POptions(enableTextNormalization: false));
            _mapper = mapper;
        }

        public G2PResult Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DotNetG2PJapaneseG2P));

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text must not be null or empty.", nameof(text));

            // テキスト正規化 (dot-net-g2p 側は無効化済み)
            text = TextNormalizer.Normalize(text);

            // HTS ラベル取得
            IReadOnlyList<string> labels = _engine.ToFullContextLabels(text);

            if (labels.Count == 0)
                throw new InvalidOperationException(
                    $"dot-net-g2p returned no labels for: \"{text}\"");

            // ラベルから音素名・A1/A2/A3 を抽出
            HtsLabelParser.ParseAll(labels, out string[] rawPhonemes,
                out int[] a1Values, out int[] a2Values, out int[] a3Values);

            int phonemeCount = rawPhonemes.Length;

            // テキストから句読点キューを構築
            var punctQueue = new Queue<int>();
            foreach (char c in text)
            {
                int pid = MapPunctuationToSBV2(c);
                if (pid >= 0) punctQueue.Enqueue(pid);
            }

            // プロソディからトーンを計算 (共有ユーティリティを使用)
            int[] precomputedTones = ProsodyToneCalculator.ComputeTonesFromProsody(
                rawPhonemes, a1Values, a2Values, a3Values, phonemeCount);

            // SBV2 形式に変換
            var phonemeIds = new List<int>(phonemeCount + 2);
            var tones = new List<int>(phonemeCount + 2);
            var languageIds = new List<int>(phonemeCount + 2);

            // 先頭 PAD
            phonemeIds.Add(_mapper.PadId);
            tones.Add(JapaneseToneOffset);
            languageIds.Add(JapaneseLanguageId);

            bool firstPauSkipped = false;

            for (int i = 0; i < phonemeCount; i++)
            {
                string phoneme = rawPhonemes[i];
                if (string.IsNullOrEmpty(phoneme) || phoneme == "xx") continue;

                // sil はスキップ (先頭/末尾 PAD で代替)
                if (phoneme == "sil" || phoneme == "silB" || phoneme == "silE")
                    continue;

                if (phoneme == "pau")
                {
                    if (!firstPauSkipped)
                    {
                        firstPauSkipped = true;
                        continue;
                    }

                    if (punctQueue.Count > 0)
                    {
                        phonemeIds.Add(punctQueue.Dequeue());
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    firstPauSkipped = true;
                    int id = _mapper.GetId(phoneme);
                    phonemeIds.Add(id);
                }

                int tone = precomputedTones[i] + JapaneseToneOffset;
                tones.Add(tone);
                languageIds.Add(JapaneseLanguageId);
            }

            // 末尾 PAD
            phonemeIds.Add(_mapper.PadId);
            tones.Add(JapaneseToneOffset);
            languageIds.Add(JapaneseLanguageId);

            // word2ph 計算
            int[] word2ph = PhonemeCharacterAligner.ComputeWord2Ph(text, phonemeIds.Count);

            if (TTSDebugLog.Enabled)
            {
                var preToneSlice = new int[phonemeCount];
                Array.Copy(precomputedTones, preToneSlice, phonemeCount);

                TTSDebugLog.Log("TTS.G2P.DotNet",
                    $"\"{text}\" → {phonemeIds.Count} phonemes (dot-net-g2p)\n" +
                    $"  Labels: {labels.Count}\n" +
                    $"  Raw: [{string.Join(", ", rawPhonemes)}]\n" +
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
        /// </summary>
        private int MapPunctuationToSBV2(char c)
        {
            return c switch
            {
                '、' or ',' or '，' => _mapper.GetId(","),
                '。' or '.' or '．' => _mapper.GetId("."),
                '!' or '！' => _mapper.GetId("!"),
                '?' or '？' => _mapper.GetId("?"),
                '\u2026' => _mapper.GetId("\u2026"),     // …
                '・' => _mapper.GetId(","),               // 中黒→コンマ
                '\'' => _mapper.GetId("'"),
                '-' => _mapper.GetId("-"),
                _ => -1
            };
        }

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
                _engine?.Dispose();
                (_tokenizer as IDisposable)?.Dispose();
                _disposed = true;
            }
        }
    }
}
#endif
