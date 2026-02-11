using System;
using System.Collections.Generic;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// OpenJTalk音素名 → SBV2トークンIDへの変換。
    /// SBV2モデルの全言語統合シンボルリスト(n_vocab=112)から辞書を動的構築する。
    /// </summary>
    public class SBV2PhonemeMapper
    {
        private readonly Dictionary<string, int> _phonemeToId;
        private readonly int _unkId;
        private readonly int _spId;
        private readonly int _padId;

        /// <summary>
        /// symbolsリスト (config.json由来) から辞書を構築する。
        /// symbols例: ["_", "!", "?", "…", ",", ".", "'", "-", "SP", "UNK", "N", "a", ...]
        /// </summary>
        public SBV2PhonemeMapper(IList<string> symbols)
        {
            _phonemeToId = new Dictionary<string, int>(symbols.Count, StringComparer.Ordinal);
            for (int i = 0; i < symbols.Count; i++)
                _phonemeToId[symbols[i]] = i;

            _padId = _phonemeToId.GetValueOrDefault("_", 0);
            _spId = _phonemeToId.GetValueOrDefault("SP", 0);
            _unkId = _phonemeToId.GetValueOrDefault("UNK", 0);
        }

        /// <summary>
        /// デフォルトのSBV2シンボルリスト（全言語統合112要素）を使うコンストラクタ。
        /// </summary>
        public SBV2PhonemeMapper() : this(DefaultSymbols) { }

        /// <summary>
        /// OpenJTalk音素名をSBV2トークンIDに変換する。
        /// </summary>
        public int GetId(string phoneme)
        {
            // OpenJTalkとSBV2の差異を吸収
            string mapped = MapOpenJTalkToSBV2(phoneme);
            return _phonemeToId.TryGetValue(mapped, out int id) ? id : _unkId;
        }

        /// <summary>
        /// 音素がマッピング可能かどうかを返す。
        /// </summary>
        public bool Contains(string phoneme)
        {
            string mapped = MapOpenJTalkToSBV2(phoneme);
            return _phonemeToId.ContainsKey(mapped);
        }

        /// <summary>
        /// SP (無音/ポーズ) のトークンID。
        /// </summary>
        public int SpId => _spId;

        /// <summary>
        /// PAD (_) のトークンID。
        /// </summary>
        public int PadId => _padId;

        /// <summary>
        /// UNK のトークンID。
        /// </summary>
        public int UnkId => _unkId;

        /// <summary>
        /// OpenJTalk音素名をSBV2シンボル名にマッピングする。
        /// </summary>
        private static string MapOpenJTalkToSBV2(string openJTalkPhoneme)
        {
            return openJTalkPhoneme switch
            {
                "cl" => "q",       // 促音
                "pau" => "SP",     // ポーズ
                "sil" => "SP",     // 文頭/文末無音
                "silB" => "SP",    // 文頭無音
                "silE" => "SP",    // 文末無音
                // 無声母音: OpenJTalk は無声化を大文字で表現 → SBV2 は小文字のみ
                "A" => "a",
                "I" => "i",
                "U" => "u",
                "E" => "e",
                "O" => "o",
                _ => openJTalkPhoneme
            };
        }

        /// <summary>
        /// SBV2 モデルの全言語統合シンボルリスト (n_vocab=112)。
        /// Python: SYMBOLS = [PAD] + sorted(set(ZH_SYMBOLS + JP_SYMBOLS + EN_SYMBOLS)) + PUNCTUATION_SYMBOLS
        /// </summary>
        public static readonly string[] DefaultSymbols =
        {
            "_",    // 0: PAD
            "AA",   // 1
            "E",    // 2
            "EE",   // 3
            "En",   // 4
            "N",    // 5: 撥音
            "OO",   // 6
            "V",    // 7
            "a",    // 8
            "a:",   // 9
            "aa",   // 10
            "ae",   // 11
            "ah",   // 12
            "ai",   // 13
            "an",   // 14
            "ang",  // 15
            "ao",   // 16
            "aw",   // 17
            "ay",   // 18
            "b",    // 19
            "by",   // 20
            "c",    // 21
            "ch",   // 22
            "d",    // 23
            "dh",   // 24
            "dy",   // 25
            "e",    // 26
            "e:",   // 27
            "eh",   // 28
            "ei",   // 29
            "en",   // 30
            "eng",  // 31
            "er",   // 32
            "ey",   // 33
            "f",    // 34
            "g",    // 35
            "gy",   // 36
            "h",    // 37
            "hh",   // 38
            "hy",   // 39
            "i",    // 40
            "i0",   // 41
            "i:",   // 42
            "ia",   // 43
            "ian",  // 44
            "iang", // 45
            "iao",  // 46
            "ie",   // 47
            "ih",   // 48
            "in",   // 49
            "ing",  // 50
            "iong", // 51
            "ir",   // 52
            "iu",   // 53
            "iy",   // 54
            "j",    // 55
            "jh",   // 56
            "k",    // 57
            "ky",   // 58
            "l",    // 59
            "m",    // 60
            "my",   // 61
            "n",    // 62
            "ng",   // 63
            "ny",   // 64
            "o",    // 65
            "o:",   // 66
            "ong",  // 67
            "ou",   // 68
            "ow",   // 69
            "oy",   // 70
            "p",    // 71
            "py",   // 72
            "q",    // 73: 促音 (OpenJTalkのcl)
            "r",    // 74
            "ry",   // 75
            "s",    // 76
            "sh",   // 77
            "t",    // 78
            "th",   // 79
            "ts",   // 80
            "ty",   // 81
            "u",    // 82
            "u:",   // 83
            "ua",   // 84
            "uai",  // 85
            "uan",  // 86
            "uang", // 87
            "uh",   // 88
            "ui",   // 89
            "un",   // 90
            "uo",   // 91
            "uw",   // 92
            "v",    // 93
            "van",  // 94
            "ve",   // 95
            "vn",   // 96
            "w",    // 97
            "x",    // 98
            "y",    // 99
            "z",    // 100
            "zh",   // 101
            "zy",   // 102
            "!",    // 103
            "?",    // 104
            "\u2026", // 105: … (ellipsis)
            ",",    // 106
            ".",    // 107
            "'",    // 108
            "-",    // 109
            "SP",   // 110: space/pause
            "UNK",  // 111: unknown
        };
    }
}
