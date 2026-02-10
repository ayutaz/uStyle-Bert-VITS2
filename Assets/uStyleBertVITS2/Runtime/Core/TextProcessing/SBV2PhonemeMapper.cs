using System;
using System.Collections.Generic;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// OpenJTalk音素名 → SBV2トークンIDへの変換。
    /// SBV2モデルのconfig.jsonに含まれるsymbolsリストから辞書を動的構築する。
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
        /// デフォルトのSBV2 JP-Extra symbolsリストを使うコンストラクタ。
        /// </summary>
        public SBV2PhonemeMapper() : this(DefaultJPExtraSymbols) { }

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
                _ => openJTalkPhoneme
            };
        }

        /// <summary>
        /// SBV2 JP-Extra モデルのデフォルトシンボルリスト。
        /// sbv2-api の norm.rs から抽出。
        /// </summary>
        public static readonly string[] DefaultJPExtraSymbols =
        {
            "_",   // 0: pad
            "!",   // 1
            "?",   // 2
            "\u2026", // 3: … (ellipsis)
            ",",   // 4
            ".",   // 5
            "'",   // 6
            "-",   // 7
            "SP",  // 8: space/pause
            "UNK", // 9: unknown
            "N",   // 10: 撥音
            "a",   // 11
            "a:",  // 12
            "b",   // 13
            "by",  // 14
            "ch",  // 15
            "d",   // 16
            "dy",  // 17
            "e",   // 18
            "e:",  // 19
            "f",   // 20
            "g",   // 21
            "gy",  // 22
            "h",   // 23
            "hy",  // 24
            "i",   // 25
            "i:",  // 26
            "j",   // 27
            "k",   // 28
            "ky",  // 29
            "m",   // 30
            "my",  // 31
            "n",   // 32
            "ny",  // 33
            "o",   // 34
            "o:",  // 35
            "p",   // 36
            "py",  // 37
            "q",   // 38: 促音 (OpenJTalkのcl)
            "r",   // 39
            "ry",  // 40
            "s",   // 41
            "sh",  // 42
            "t",   // 43
            "ts",  // 44
            "ty",  // 45
            "u",   // 46
            "u:",  // 47
            "v",   // 48
            "w",   // 49
            "y",   // 50
            "z",   // 51
        };
    }
}
