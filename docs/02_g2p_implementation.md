# G2P実装ガイド (uPiperベース + SBV2拡張)

## 概要

日本語テキストから Style-Bert-VITS2 が必要とする入力テンソルを生成するパイプライン。
2つの G2P バックエンドを提供する:

1. **OpenJTalk (JapaneseG2P)** — uPiperのOpenJTalk P/Invoke基盤を流用。ネイティブDLL (`openjtalk_wrapper.dll`) が必要
2. **dot-net-g2p (DotNetG2PJapaneseG2P)** — Pure C# 実装。ネイティブ DLL 不要で、条件付きコンパイルで有効化

いずれも SBV2 固有の音素マッピング・DeBERTa トークナイザと組み合わせて使用する。`TTSSettings.G2PEngine` で実行時選択が可能。

---

## uPiperからの流用コンポーネント (OpenJTalk バックエンド)

> **Note**: OpenJTalk バックエンドは `#if !USBV2_DOTNET_G2P_AVAILABLE` の条件付きコンパイルでオプショナル。dot-net-g2p バックエンド使用時はネイティブ DLL 不要（→ [dot-net-g2p バックエンド](#dot-net-g2p-バックエンド) セクション参照）。

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

SBV2モデルの全言語統合シンボルリスト（112シンボル, n_vocab=112）:

```
日本語音素(42): "N", "a", "a:", "b", "by", "ch", "d", "dy", "e", "f",
  "g", "gy", "h", "hy", "i", "j", "k", "ky", "m", "my",
  "n", "ny", "o", "p", "py", "r", "ry", "s", "sh", "t",
  "ts", "ty", "u", "v", "w", "y", "z", ...
句読点(9): "!", "?", "…", ",", ".", "'", "-", "SP", "UNK"
パディング: "_"
```

**実装方針**:
- ハードコードされた DefaultSymbols テーブル（112要素）から音素→ID辞書を構築。config.json は不要。
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

**実装**: トーン計算ロジックは `ProsodyToneCalculator` クラスに抽出され、`JapaneseG2P` (OpenJTalk) と `DotNetG2PJapaneseG2P` (dot-net-g2p) の両方から共有される（→ [ProsodyToneCalculator](#prosodytonecalculator--プロソディトーン計算) セクション参照）。`JapaneseG2P.ComputeTonesFromProsody()` は後方互換のため維持されているが、内部では `ProsodyToneCalculator` に委譲する。

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

### 5. PhonemeCharacterAligner — word2ph の計算

`PhonemeCharacterAligner.ComputeWord2Ph(text, phoneSeqLen)` で word2ph を計算する。

- 仮名文字ごとの音素数テーブル（ひらがな/カタカナ対応）を内蔵
- [CLS] → 先頭SP(1音素)、[SEP] → 末尾SP(1音素)
- 漢字等の未知文字は残りの音素数を比例配分
- 推定合計が phoneSeqLen と一致しない場合は最後の文字で補正

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
  │      │                    │
  │      │                    └──→ [PhonemeUtils.Intersperse]
  │      │                           │  add_blank: 各音素間に blank(0) を挿入
  │      │                           │  [a, b, c] → [0, a, 0, b, 0, c, 0]
  │      │                           │
  │      │                           ├── phoneme_ids[]  (int32, 2N+1)
  │      │                           ├── tones[]        (int32, 2N+1)
  │      │                           └── language_ids[]  (int32, 2N+1)
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
  │      └── bert [1, 1024, seq_len]  ← JP-Extraモデルの入力名
  │
  └──→ [SBV2ModelRunner] メインTTS推論
         │
         └── audio [1, 1, audio_samples]  (44100Hz float32)
```

---

## ファイル配置

```
Assets/uStyleBertVITS2/
  Runtime/Core/
    Native/
      OpenJTalkNative.cs            (uPiper流用, namespace変更)
      OpenJTalkConstants.cs         (uPiper流用, namespace変更)
    TextProcessing/
      TextNormalizer.cs             (uPiper流用, namespace変更)
      SBV2PhonemeMapper.cs          (OpenJTalk音素→SBV2トークンID)
      JapaneseG2P.cs                (パイプライン統合)
      SBV2Tokenizer.cs              (DeBERTa用文字レベルトークナイザ)
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

- **OpenJTalk辞書サイズ**: sys.dic が 103MB あるため、ビルドサイズに注意。圧縮配布も検討（dot-net-g2p バックエンドの MeCab 辞書も同様）
- **word2phのアライメント精度**: DeBERTaのトークナイズ結果とOpenJTalkの単語境界が一致しない可能性がある。SBV2のPython実装 (`get_bert_feature` 関数) のアライメント方法を忠実に移植すること
- **tone値のオフセット**: sbv2-apiでは tone+6 を行っている。モデル学習時の仕様に合わせること

---

## dot-net-g2p バックエンド

### 概要

dot-net-g2p は Pure C# で OpenJTalk 互換の形態素解析・音素変換を行うライブラリ。ネイティブ DLL (`openjtalk_wrapper.dll`) への依存を排除し、クロスプラットフォーム対応を容易にする。

内部では `MeCabTokenizer` (C# MeCab 実装) と `G2PEngine` で形態素解析から HTS full context labels の生成まで行い、その後は既存の SBV2 パイプライン（SBV2PhonemeMapper, PhonemeCharacterAligner 等）を共有する。

### DotNetG2PJapaneseG2P の処理パイプライン

`DotNetG2PJapaneseG2P` クラス (`IG2P` を実装) の `Process(text)` メソッドは以下の6ステップで動作する:

```
1. TextNormalizer.Normalize()
   テキスト正規化（全角→半角変換等）。
   G2PEngine 側のテキスト正規化は G2POptions(enableTextNormalization: false) で無効化し、
   二重正規化を防止。

2. G2PEngine.ToFullContextLabels(text)
   MeCabTokenizer による形態素解析 → HTS full context labels を生成。
   戻り値: IReadOnlyList<string> (各ラベルが1音素に対応)

3. HtsLabelParser.ParseAll(labels, ...)
   各 HTS ラベルから音素名と A1/A2/A3 プロソディ値を抽出。
   出力: string[] rawPhonemes, int[] a1, int[] a2, int[] a3

4. ProsodyToneCalculator.ComputeTonesFromProsody()
   A1/A2/A3 からプロソディ遷移を検出し、各音素のトーン (0/1) を計算。
   JapaneseG2P と共有される共通ユーティリティ。

5. SBV2PhonemeMapper
   音素名を SBV2 トークン ID に変換。句読点は MapPunctuationToSBV2() で
   テキスト中の文字→句読点キューを構築し、pau 位置でデキューして挿入。

6. PhonemeCharacterAligner.ComputeWord2Ph()
   テキストと音素列長から word2ph を計算。
```

### 句読点処理 (MapPunctuationToSBV2)

dot-net-g2p の HTS ラベルでは句読点が直接音素として出力されない（`pau` として出現する）。
`DotNetG2PJapaneseG2P` はテキストを先にスキャンして句読点の SBV2 ID をキューに蓄え、
音素列中の `pau` に遭遇するたびにキューから対応する句読点 ID をデキューして挿入する:

```csharp
// テキストから句読点キューを構築
var punctQueue = new Queue<int>();
foreach (char c in text)
{
    int pid = MapPunctuationToSBV2(c);
    if (pid >= 0) punctQueue.Enqueue(pid);
}

// pau 位置でデキューして句読点を復元
if (phoneme == "pau" && punctQueue.Count > 0)
    phonemeIds.Add(punctQueue.Dequeue());
```

句読点マッピング:

| 入力文字 | SBV2 シンボル |
|---|---|
| `、`, `,`, `，` | `,` |
| `。`, `.`, `．` | `.` |
| `!`, `！` | `!` |
| `?`, `？` | `?` |
| `…` | `…` |
| `・` | `,` (中黒はコンマにフォールバック) |

### コンストラクタ

```csharp
// 基本コンストラクタ: MeCab辞書パスを指定
var g2p = new DotNetG2PJapaneseG2P(dictPath);

// SBV2PhonemeMapper 共有版
var g2p = new DotNetG2PJapaneseG2P(dictPath, sharedMapper);
```

- `dictPath`: MeCab 辞書ディレクトリ (NAIST JDIC 等) のパス
- `G2POptions(enableTextNormalization: false)` で dot-net-g2p 側のテキスト正規化を無効化。`TextNormalizer.Normalize()` による正規化のみ使用

---

## HtsLabelParser

### 概要

`HtsLabelParser` は HTS full context labels をパースして音素名と A1/A2/A3 プロソディ値を抽出する static ユーティリティクラス。`DotNetG2PJapaneseG2P` が `G2PEngine.ToFullContextLabels()` の出力をパースする際に使用する。

### HTS full context label 形式

```
p2^p1-c+n1=n2/A:a1+a2+a3/B:b1-b2_b3/C:c1_c2+c3/...
```

- **音素コンテキスト部** (最初の `/` 以前): `p2^p1-c+n1=n2`
  - `p2`: 2つ前の音素
  - `p1`: 1つ前の音素
  - `c`: 現在の音素
  - `n1`: 1つ後の音素
  - `n2`: 2つ後の音素
- **プロソディ部**: `/A:a1+a2+a3/B:.../C:...` 等のセクション

### ParsePhoneme

`-` と `+` の間が現在の音素名:

```
例: "xx^sil-k+o=N/A:-3+1+7/B:..."
     → ParsePhoneme() = "k"
```

### ParseA

`/A:` セクションから `a1+a2+a3` を `+` 区切りで抽出:

```
例: "/A:-3+1+7/" → a1=-3, a2=1, a3=7
    "/A:xx+xx+xx/" → a1=0, a2=0, a3=0 (sil/pau)
```

**A フィールドの意味:**

| フィールド | 意味 | 計算式 |
|---|---|---|
| a1 | アクセント核相対位置 | `moraPos - accent + 1` |
| a2 | 句内前方位置 (1始まり) | `moraPos + 1` |
| a3 | 句内後方位置 | `moraCount - moraPos` |

- `sil`/`pau` では `"xx+xx+xx"` → すべて 0 として返す
- a1 は負値になり得る（例: `-3+1+4`）

### ParseAll

一括パースメソッド。HTS ラベル列から音素名配列と A1/A2/A3 配列をまとめて抽出:

```csharp
HtsLabelParser.ParseAll(labels, out string[] phonemes,
    out int[] a1, out int[] a2, out int[] a3);
```

---

## ProsodyToneCalculator — プロソディトーン計算

### 概要

`ProsodyToneCalculator` は OpenJTalk プロソディ情報 (A1/A2/A3) から SBV2 トーン (0/1) を計算する共有ユーティリティ。`JapaneseG2P` (OpenJTalk バックエンド) と `DotNetG2PJapaneseG2P` (dot-net-g2p バックエンド) の両方から使用される。

Python の `__pyopenjtalk_g2p_prosody()` + `__g2phone_tone_wo_punct()` を C# に移植した実装。

### アルゴリズム

`ComputeTonesFromProsody(rawPhonemes, a1, a2, a3, count)` は以下のロジックで動作:

1. **句境界検出**: `sil`/`pau` に遭遇したらアクセント句バッファを確定し、tone=0 をセット
2. **プロソディ記号検出** (隣接する A2/A3 の変化から):
   - **`#` (句境界)**: `a3[i]==1 && a2[i+1]==1 && IsVowelOrNOrCl(ph)` → バッファ確定・リセット
   - **`]` (下降)**: `a1[i]==0 && a2[i+1]==a2[i]+1 && a2[i]!=a3[i]` → `currentTone -= 1`
   - **`[` (上昇)**: `a2[i]==1 && a2[i+1]==2` → `currentTone += 1`
3. **currentTone** 状態変数を管理し、各音素に累積トーンを割り当て

### FixPhoneTone — 正規化

アクセント句内のトーン集合を {0, 1} に正規化する:

| 入力トーン集合 | 処理 |
|---|---|
| {0, 1} | そのまま |
| {-1, 0} | 全体を +1 シフト → {0, 1} |
| {0} のみ | そのまま (全て 0) |

### IsVowelOrNOrCl

句境界判定 (`#`) に使用。Python の `p3 in "aeiouAEIOUNcl"` を移植:

- 母音: `a`, `i`, `u`, `e`, `o` (大文字=無声化含む)
- 撥音: `N`
- 促音: `cl`

---

## 条件付きコンパイルと実行時選択

### コンパイルシンボル

| シンボル | 効果 |
|---|---|
| `USBV2_DOTNET_G2P_AVAILABLE` | 定義時: `DotNetG2PJapaneseG2P` クラスが有効化される |
| (未定義時) | OpenJTalk の `JapaneseG2P` のみ使用可能 |

dot-net-g2p パッケージがプロジェクトに追加されている場合にのみ `USBV2_DOTNET_G2P_AVAILABLE` を定義する。

### TTSSettings による実行時選択

`TTSSettings` ScriptableObject の `G2PEngine` フィールドで実行時に G2P バックエンドを選択:

```csharp
public enum G2PEngineType
{
    OpenJTalk = 0,   // ネイティブDLL P/Invoke
    DotNetG2P = 1,   // Pure C# (dot-net-g2p)
}

// TTSSettings
public G2PEngineType G2PEngine = G2PEngineType.OpenJTalk;
```

- `G2PEngineType.DotNetG2P` を選択しても `USBV2_DOTNET_G2P_AVAILABLE` が未定義の場合は OpenJTalk にフォールバックする
- Inspector の Tooltip: `"G2Pエンジン (OpenJTalk=ネイティブDLL, DotNetG2P=Pure C#)"`

### ファイル配置 (dot-net-g2p 関連)

```
Assets/uStyleBertVITS2/
  Runtime/Core/
    TextProcessing/
      DotNetG2PJapaneseG2P.cs       (#if USBV2_DOTNET_G2P_AVAILABLE で囲まれている)
      HtsLabelParser.cs             (HTS ラベルパーサー、常時コンパイル)
      ProsodyToneCalculator.cs      (トーン計算共有ユーティリティ、常時コンパイル)
      JapaneseG2P.cs                (OpenJTalk バックエンド、ComputeTonesFromProsody は委譲)
```
