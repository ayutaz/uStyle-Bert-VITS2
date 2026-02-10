# G2P実装ガイド (uPiperベース + SBV2拡張)

## 概要

日本語テキストから Style-Bert-VITS2 が必要とする入力テンソルを生成するパイプライン。
uPiperのOpenJTalk P/Invoke基盤を流用し、SBV2固有の音素マッピング・DeBERTaトークナイザを新規実装する。

---

## uPiperからの流用コンポーネント

| コンポーネント | uPiperパス | 流用方法 |
|---|---|---|
| OpenJTalkNative | `Runtime/Core/Phonemizers/Native/OpenJTalkNative.cs` | namespace変更して流用 |
| OpenJTalkConstants | `Runtime/Core/Phonemizers/OpenJTalkConstants.cs` | namespace変更して流用 |
| TextNormalizer | `Runtime/Core/Phonemizers/Text/TextNormalizer.cs` | namespace変更して流用 |
| CustomDictionary | `Runtime/Core/Phonemizers/CustomDictionary.cs` | namespace変更して流用 |
| openjtalk_wrapper.dll | `Plugins/Windows/x86_64/` | バイナリコピー |
| NAIST JDIC辞書 | `Samples~/OpenJTalk Dictionary Data/naist_jdic/` | StreamingAssetsにコピー |

### 流用時の変更点

**namespace変更**: `uPiper.Core.Phonemizers.*` → `uStyleBertVits2.G2P.*`

**依存の置き換え**:
- `uPiper.Core.Logging.PiperLogger` → `UnityEngine.Debug.Log` に置き換え or 独自Logger作成
- `ITextNormalizer` インターフェース → TextNormalizerを直接使用で可
- `BasePhonemizer` → 不要（SBV2用の独自テキストプロセッサを作成）

### OpenJTalkNative.cs の構造（参考）

```
OpenJTalkNative (static class)
├── NativePhonemeResult (struct)     - 音素結果
├── NativeProsodyPhonemeResult       - プロソディ付き音素結果
├── openjtalk_create(dictPath)       - 初期化
├── openjtalk_destroy(handle)        - 破棄
├── openjtalk_phonemize(handle, text, out result) - 音素変換
├── openjtalk_phonemize_with_prosody(handle, text, out result) - プロソディ付き
└── プラットフォーム別DLL名定義
    ├── iOS: "__Internal"
    └── その他: "openjtalk_wrapper"
```

プロソディ機能(`openjtalk_phonemize_with_prosody`)が重要。SBV2のトーン（アクセント）情報抽出に使う。

### 辞書ファイル一覧 (OpenJTalkConstants.cs)

必須8ファイル（NAIST JDIC）:
- `sys.dic` (103MB) — システム辞書
- `unk.dic` — 未知語辞書
- `char.bin` — 文字定義
- `matrix.bin` (3.7MB) — 接続コスト行列
- `left-id.def`, `right-id.def` — ID定義
- `pos-id.def` — 品詞タグ定義
- `rewrite.def` — 書き換えルール

---

## SBV2固有の新規実装コンポーネント

### 1. SBV2PhonemeMapper — 音素→SBV2トークンID変換

sbv2-apiの `crates/sbv2_core/src/norm.rs` から確認した音素インベントリ（127シンボル）:

```
日本語音素(42): "N", "a", "a:", "b", "by", "ch", "d", "dy", "e", "f",
  "g", "gy", "h", "hy", "i", "j", "k", "ky", "m", "my",
  "n", "ny", "o", "p", "py", "r", "ry", "s", "sh", "t",
  "ts", "ty", "u", "v", "w", "y", "z", ...
句読点(9): "!", "?", "…", ",", ".", "'", "-", "SP", "UNK"
パディング: "_"
```

**実装方針**:
- SBV2モデルの `config.json` に含まれる `symbols` リストから音素→ID辞書を動的構築
- OpenJTalkの出力音素をSBV2シンボル名にマッピング
- Piperと異なりPUA変換は不要（SBV2はOpenJTalk音素をほぼそのまま使用）

#### マッピングの注意点

OpenJTalk出力とSBV2シンボルの差異:
- OpenJTalkの `cl`（促音） → SBV2の `q`（促音記号）
- OpenJTalkの `pau`（ポーズ） → SBV2の `SP`（無音記号）
- OpenJTalkの `sil`（文頭/文末無音） → SBV2の `SP` or 省略
- 長音記号の扱い: OpenJTalkは母音の後に `:` を付ける場合あり

### 2. トーン（アクセント）配列生成

sbv2-apiの `crates/sbv2_core/src/jtalk.rs` の `g2p_prosody()` を参考:

- OpenJTalkのプロソディ情報（A1/A2/A3）からトーン値を計算
- 基本ルール: アクセント核以前=1(High)、以後=0(Low)
- SBV2ではトーン値に+6のオフセットを加える（`tones` テンソルの値域は0〜）
- 句読点・無音には tone=0

```
例: "こんにちは" (アクセント型: LHHHH → 平板型)
音素:  k o N n i ch i w a
トーン: 0 0 1  1 1 1  1 1 1  (アクセント情報に基づく)
→ +6オフセット: 6 6 7 7 7 7 7 7 7
```

### 3. 言語ID配列生成

JP-Extra版では全音素に language_id=1（日本語）を設定:

```
language_ids: [1, 1, 1, ..., 1]  // seq_len個、全て1
```

### 4. word2ph — BERTトークンと音素のアライメント

sbv2-apiの `tts.rs` で確認したアライメント処理:

1. G2P時にword2phベクトルを生成（各単語が何個の音素に対応するか）
2. DeBERTaのトークン列は原文の文字に対応
3. word2phを使ってBERT埋め込みを音素列長に展開

```
テキスト: "こんにちは"
BERT tokens: [CLS] こ ん に ち は [SEP]  → token_len=7
音素: k o N n i ch i w a               → seq_len=9
word2ph: [1, 2, 1, 2, 1, 2, 0]          → 各トークンの音素数
→ BERT出力[1, 1024, 7] → 展開 → [1, 1024, 9]
```

展開処理:
```
bert_output = [b0, b1, b2, b3, b4, b5, b6]  (各1024次元)
word2ph =     [1,  2,  1,  2,  1,  2,  0 ]
展開後 =      [b0, b1, b1, b2, b3, b3, b4, b5, b5]
```

---

## DeBERTaトークナイザ (SBV2Tokenizer)

### モデル情報
- **モデル名**: `ku-nlp/deberta-v2-large-japanese-char-wwm`
- **タイプ**: 文字レベルSentencePiece
- **特徴**: 日本語文字を1文字ずつトークン化（サブワードではなくキャラクターレベル）

### 実装方針
- `vocab.json` (or `spm.model`) をStreamingAssetsに配置
- uCosyVoiceの `Qwen2Tokenizer` パターンを参考にC#実装
- 特殊トークン: `[CLS]`=1, `[SEP]`=2, `[UNK]`=3, `[PAD]`=0

### トークナイズフロー
```
入力: "こんにちは"
→ 文字分割: ["こ", "ん", "に", "ち", "は"]
→ vocab.jsonでID変換: [234, 567, 345, 432, 298]  (例)
→ 特殊トークン付加: [1, 234, 567, 345, 432, 298, 2]  ([CLS]...[SEP])
→ attention_mask: [1, 1, 1, 1, 1, 1, 1]  (全1)
→ token_type_ids: [0, 0, 0, 0, 0, 0, 0]  (全0)
```

---

## 全体パイプライン

```
日本語テキスト
  │
  ├──→ [TextNormalizer] 全角→半角等
  │      │
  │      ├──→ [CustomDictionary] ユーザー辞書適用
  │      │      │
  │      │      └──→ [OpenJTalk P/Invoke] 形態素解析+音素変換
  │      │             │
  │      │             └──→ [SBV2PhonemeMapper]
  │      │                    │
  │      │                    ├── phoneme_ids[]  (int32)
  │      │                    ├── tones[]        (int32)
  │      │                    ├── language_ids[]  (int32)
  │      │                    └── word2ph[]       (int[])
  │      │
  │      └──→ [SBV2Tokenizer] DeBERTa用文字レベルトークナイズ
  │             │
  │             ├── token_ids[]      (int32)
  │             └── attention_mask[] (int32)
  │
  ├──→ [BertRunner] DeBERTa ONNX推論
  │      │
  │      └── bert_output [1, 1024, token_len]
  │
  ├──→ [word2phアライメント]
  │      │
  │      └── ja_bert [1, 1024, seq_len]  ← BERTを音素列に展開
  │
  └──→ [SBV2ModelRunner] メインTTS推論
         │
         └── audio [1, 1, audio_samples]  (44100Hz float32)
```

---

## ファイル配置

```
Assets/
  Scripts/
    G2P/
      Native/
        OpenJTalkNative.cs          (uPiper流用, namespace変更)
      Text/
        TextNormalizer.cs           (uPiper流用, namespace変更)
      OpenJTalkConstants.cs         (uPiper流用, namespace変更)
      CustomDictionary.cs           (uPiper流用, namespace変更)
      SBV2PhonemeMapper.cs          (新規: OpenJTalk音素→SBV2トークンID)
      SBV2TextProcessor.cs          (新規: パイプライン統合)
    Tokenizer/
      SBV2Tokenizer.cs              (新規: DeBERTa用文字レベルトークナイザ)
  Plugins/
    Windows/x86_64/
      openjtalk_wrapper.dll         (uPiperからコピー)
  StreamingAssets/
    OpenJTalkDic/                   (NAIST JDIC辞書 8ファイル)
    Tokenizer/
      vocab.json                    (DeBERTaトークナイザ語彙)
```

---

## 参考: sbv2-apiのG2Pフロー (Rust)

sbv2-apiでは `jpreprocess` (OpenJTalkのRust実装) を使用:

```
テキスト入力
  → jpreprocess.run_frontend()  // 形態素解析
  → 言語学ラベル抽出
  → g2p_prosody()               // アクセント句・モーラ解析
  → カタカナ→音素変換 (mora_list.json)
  → phoneme_ids, tones, word2ph 生成
```

C#での実装は OpenJTalk ネイティブP/Invoke で同等機能を実現。
`openjtalk_phonemize_with_prosody()` がプロソディ(A1/A2/A3)を返すため、
sbv2-apiの `g2p_prosody()` と同等のトーン計算が可能。

---

## 注意事項

- **OpenJTalk辞書サイズ**: sys.dic が 103MB あるため、ビルドサイズに注意。圧縮配布も検討
- **word2phのアライメント精度**: DeBERTaのトークナイズ結果とOpenJTalkの単語境界が一致しない可能性がある。SBV2のPython実装 (`get_bert_feature` 関数) のアライメント方法を忠実に移植すること
- **tone値のオフセット**: sbv2-apiでは tone+6 を行っている。モデル学習時の仕様に合わせること
