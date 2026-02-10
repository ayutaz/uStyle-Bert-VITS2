using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// DeBERTa (ku-nlp/deberta-v2-large-japanese-char-wwm) 用の文字レベルトークナイザ。
    /// vocab.json から char→int の辞書を構築し、[CLS]...[SEP] で囲む。
    /// </summary>
    public class SBV2Tokenizer
    {
        public const int PadId = 0;
        public const int ClsId = 1;
        public const int SepId = 2;
        public const int UnkId = 3;

        private readonly Dictionary<char, int> _charToId;
        private readonly int _clsId;
        private readonly int _sepId;
        private readonly int _unkId;

        /// <summary>
        /// vocab.json のパスからトークナイザを初期化する。
        /// vocab.json は {"token": id, ...} 形式の JSON ファイル。
        /// </summary>
        public SBV2Tokenizer(string vocabJsonPath)
        {
            string json = File.ReadAllText(vocabJsonPath);
            var vocab = ParseVocabJson(json);

            _charToId = new Dictionary<char, int>(vocab.Count);
            foreach (var kvp in vocab)
            {
                if (kvp.Key.Length == 1)
                    _charToId[kvp.Key[0]] = kvp.Value;
            }

            _clsId = vocab.TryGetValue("[CLS]", out int cls) ? cls : ClsId;
            _sepId = vocab.TryGetValue("[SEP]", out int sep) ? sep : SepId;
            _unkId = vocab.TryGetValue("[UNK]", out int unk) ? unk : UnkId;
        }

        /// <summary>
        /// テスト用: 辞書を直接渡すコンストラクタ。
        /// </summary>
        public SBV2Tokenizer(Dictionary<string, int> vocab)
        {
            _charToId = new Dictionary<char, int>(vocab.Count);
            foreach (var kvp in vocab)
            {
                if (kvp.Key.Length == 1)
                    _charToId[kvp.Key[0]] = kvp.Value;
            }

            _clsId = vocab.TryGetValue("[CLS]", out int cls) ? cls : ClsId;
            _sepId = vocab.TryGetValue("[SEP]", out int sep) ? sep : SepId;
            _unkId = vocab.TryGetValue("[UNK]", out int unk) ? unk : UnkId;
        }

        /// <summary>
        /// テキストをトークナイズする。
        /// 戻り値: (tokenIds, attentionMask)
        /// tokenIds: [CLS] + 文字ごとのID + [SEP]
        /// attentionMask: 全要素1
        /// </summary>
        public (int[] TokenIds, int[] AttentionMask) Encode(string text)
        {
            if (text == null)
                text = string.Empty;

            int len = text.Length + 2; // [CLS] + text + [SEP]
            int[] tokenIds = new int[len];
            int[] mask = new int[len];

            tokenIds[0] = _clsId;
            mask[0] = 1;

            for (int i = 0; i < text.Length; i++)
            {
                tokenIds[i + 1] = _charToId.TryGetValue(text[i], out int id) ? id : _unkId;
                mask[i + 1] = 1;
            }

            tokenIds[len - 1] = _sepId;
            mask[len - 1] = 1;

            return (tokenIds, mask);
        }

        /// <summary>
        /// token_type_ids を生成する（DeBERTaでは全0）。
        /// </summary>
        public int[] CreateTokenTypeIds(int tokenLength)
        {
            return new int[tokenLength];
        }

        /// <summary>
        /// 語彙サイズを返す。
        /// </summary>
        public int VocabSize => _charToId.Count;

        /// <summary>
        /// 簡易JSONパーサー: {"key": value, ...} 形式のフラットなオブジェクトをパース。
        /// Unity内蔵のJsonUtilityは Dictionary をサポートしないため独自実装。
        /// </summary>
        private static Dictionary<string, int> ParseVocabJson(string json)
        {
            var dict = new Dictionary<string, int>();

            // 簡易パーサー: "key": value のペアを順に抽出
            int i = 0;
            while (i < json.Length)
            {
                // キー文字列の開始を探す
                int keyStart = json.IndexOf('"', i);
                if (keyStart < 0) break;

                int keyEnd = FindClosingQuote(json, keyStart + 1);
                if (keyEnd < 0) break;

                string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);
                // Unicodeエスケープをデコード
                key = DecodeUnicodeEscapes(key);

                // コロンの後の数値を探す
                int colonIdx = json.IndexOf(':', keyEnd);
                if (colonIdx < 0) break;

                int valueStart = colonIdx + 1;
                while (valueStart < json.Length && (json[valueStart] == ' ' || json[valueStart] == '\t'))
                    valueStart++;

                int valueEnd = valueStart;
                while (valueEnd < json.Length && json[valueEnd] != ',' && json[valueEnd] != '}' && json[valueEnd] != '\n')
                    valueEnd++;

                string valueStr = json.Substring(valueStart, valueEnd - valueStart).Trim();
                if (int.TryParse(valueStr, out int value))
                    dict[key] = value;

                i = valueEnd + 1;
            }

            return dict;
        }

        private static int FindClosingQuote(string s, int start)
        {
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] == '"' && (i == 0 || s[i - 1] != '\\'))
                    return i;
            }
            return -1;
        }

        private static string DecodeUnicodeEscapes(string s)
        {
            if (!s.Contains("\\u"))
                return s;

            var sb = new System.Text.StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                if (i + 5 < s.Length && s[i] == '\\' && s[i + 1] == 'u')
                {
                    string hex = s.Substring(i + 2, 4);
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                    {
                        sb.Append((char)code);
                        i += 6;
                        continue;
                    }
                }
                sb.Append(s[i]);
                i++;
            }
            return sb.ToString();
        }
    }
}
