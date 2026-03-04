# G2P移行計画: OpenJTalk P/Invoke → dot-net-g2p

## 概要

現行の G2P (Grapheme-to-Phoneme) 実装を OpenJTalk P/Invoke (`openjtalk_wrapper.dll`) から
[dot-net-g2p](https://github.com/ayutaz/dot-net-g2p) (Pure C#) に置き換える移行計画。

**目的**: ネイティブ DLL 依存を排除し、クロスプラットフォーム対応を実現する。

**ブランチ**: `feature/dotnet-g2p`

---

## 1. 現行アーキテクチャ

```
テキスト
  → TextNormalizer.Normalize()
  → OpenJTalk P/Invoke (openjtalk_phonemize_with_prosody)
  → 生音素列 + プロソディ (A1/A2/A3)
  → SBV2PhonemeMapper (音素 → SBV2トークンID)
  → ComputeTonesFromProsody (A1/A2/A3 → 0/1 トーン → +6 オフセット)
  → PhonemeCharacterAligner (word2ph 計算)
  → G2PResult { PhonemeIds[], Tones[], LanguageIds[], Word2Ph[] }
```

### 関連ファイル

| ファイル | 役割 |
|---|---|
| `IG2P.cs` | G2P インターフェース (`Process(string) → G2PResult`) |
| `JapaneseG2P.cs` | 現行実装 (OpenJTalk P/Invoke) |
| `SBV2PhonemeMapper.cs` | 音素 → SBV2 トークンID (112シンボル) |
| `PhonemeCharacterAligner.cs` | word2ph 計算 (かなテーブル + 比例配分) |
| `OpenJTalkNative.cs` | P/Invoke 定義 |
| `OpenJTalkHandle.cs` | SafeHandle |
| `OpenJTalkConstants.cs` | 辞書パス定数 |
| `G2PResult.cs` | 出力 readonly struct |
| `TextNormalizer.cs` | テキスト正規化 |

---

## 2. 移行先: dot-net-g2p

| 項目 | 内容 |
|---|---|
| リポジトリ | https://github.com/ayutaz/dot-net-g2p |
| 実装 | Pure C# (ネイティブ DLL 不要) |
| 内部エンジン | MeCab (DotNetG2P.MeCab) |
| 辞書 | naist-jdic (OpenJTalk と同一辞書) |
| ターゲット | .NET Standard 2.1+ / Unity 2021.2+ |
| ライセンス | Apache-2.0 |

### 主要 API

```csharp
using var tokenizer = new MeCabTokenizer("/path/to/naist-jdic");
using var engine = new G2PEngine(tokenizer);

string phonemes       = engine.ToPhonemes(text);        // スペース区切り音素
string prosody        = engine.ToProsody(text);         // ESPnet 韻律記号付き
var accentPhrases     = engine.ToAccentPhrases(text);   // VOICEVOX 互換
var fullContextLabels = engine.ToFullContextLabels(text); // HTS ラベル
var njdNodes          = engine.Analyze(text);            // NJD ノード
```

### UPM インストール

```
https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.Core
https://github.com/ayutaz/dot-net-g2p.git?path=src/DotNetG2P.MeCab
```

---

## 3. 検証フェーズ

### Phase 0: 前提条件の整備

**目的**: 移行作業に着手可能な環境を構築する。

| # | タスク | 完了条件 |
|---|---|---|
| 0-1 | dot-net-g2p を UPM で Unity プロジェクトに導入 | コンパイルエラーなし |
| 0-2 | UPM Git URL を tag または commit hash で固定 | manifest.json にバージョン明記 |
| 0-3 | naist-jdic 辞書ファイルの配置確認 | dot-net-g2p の MeCabTokenizer で辞書ロード成功 |
| 0-4 | IL2CPP ビルドテスト | link.xml 設定込みで Development Build 成功 |
| 0-5 | asmdef 参照設定 | Runtime.asmdef から dot-net-g2p アセンブリ参照可能 |

**懸念点**:
- 🟠 **辞書バイナリ形式の互換性**: 現行 OpenJTalkDic の辞書ファイルと dot-net-g2p の MeCab 辞書がバイナリ互換でない可能性がある。dot-net-g2p が要求する辞書ファイルセット (sys.dic, unk.dic, char.bin, matrix.bin) と現行8ファイルの差異を確認すること
- 🟠 **IL2CPP ストリッピング**: dot-net-g2p 内部の private/internal クラスが削除される可能性。`link.xml` で `<assembly fullname="DotNetG2P.Core" preserve="all"/>` 等の設定が必要
- 🟡 **UPM Git URL の安定性**: main ブランチ直接参照は破壊的変更リスクあり。リリースタグが存在しない場合は commit hash でロックする

**Phase 0 ブロッカー**: この Phase で致命的問題が見つかった場合、移行計画全体を見直す。

---

### Phase 1: 音素出力の互換性検証

**目的**: dot-net-g2p と OpenJTalk の出力が SBV2 パイプラインにおいて等価であることを確認する。

| # | タスク | 完了条件 |
|---|---|---|
| 1-1 | 基本音素出力の比較テスト作成 | 50+ テストケースで音素シーケンスが一致 |
| 1-2 | HTS フルコンテキストラベルから A1/A2/A3 抽出確認 | `ToFullContextLabels()` から A1/A2/A3 をパース可能 |
| 1-3 | SBV2PhonemeMapper 互換性確認 | dot-net-g2p 音素 → `GetId()` で全音素が正しくマッピング |
| 1-4 | 無声化母音の出力形式確認 | 大文字 (A/I/U/E/O) vs 小文字の扱いが一致 |
| 1-5 | sil/silB/silE/pau の出力有無確認 | dot-net-g2p が先頭/末尾に sil を出力するか特定 |
| 1-6 | エッジケーステスト | 長音、促音連続、記号混在、英数字、空文字列、句読点のみ |

**テストケースカテゴリ**:

```
基本:       「こんにちは」「東京タワー」「お元気ですか」
漢字混在:   「今日は良い天気です」「複雑な漢字交じり文」
句読点:     「東京タワー、スカイツリー。」「え？本当！」
記号:       「あ…」「100円」「Hello世界」
長文:       50文字以上の自然文 x10
特殊:       空文字、スペースのみ、句読点のみ、数字のみ
```

**懸念点**:
- 🔴 **形態素解析の差異**: dot-net-g2p の Pure C# MeCab と OpenJTalk の内部 MeCab で漢字の読みやアクセント句分割が異なる可能性がある。同一辞書でも実装の微妙な差でラティス探索結果が変わりうる
- 🔴 **音素シーケンス構造の差異**: 先頭/末尾の sil/pau の有無、句読点の音素列内での表現方法が異なる場合、`ConvertNativeResult()` 相当のロジック全体を再設計する必要がある
- 🟠 **未検証の前提**: 「互換性あり」という結論は README レベルの情報に基づいており、実データでの検証が一切行われていない

**Phase 1 判定基準**:
- 全テストケースで音素 ID 列が完全一致 → Phase 2 へ
- 95%以上一致 + 差異が体系的 → マッピングレイヤー追加で対応可能か検討
- 大幅な差異 → 移行計画を見直し

---

### Phase 2: トーン計算の互換性検証

**目的**: dot-net-g2p から既存のトーン計算アルゴリズムと同等の結果を得られることを確認する。

| # | タスク | 完了条件 | ステータス |
|---|---|---|---|
| 2-1 | HTS ラベルからの A1/A2/A3 パーサー実装 | `ToFullContextLabels()` → A1/A2/A3 int[] への変換 | ✅ HtsLabelParser 実装済み |
| 2-2 | 既存 ComputeTonesFromProsody 12テスト通過 | 全テストケースで現行と同一のトーン値 | ✅ 既存12テスト + 拡張7テスト通過 (ProsodyToneCalculator に抽出) |
| 2-3 | 追加テーン検証 (50+ ケース) | 平板/頭高/中高/尾高 + 複合語 + 疑問文 | ✅ 50+ 追加トーン検証テスト作成 (ToneCalculationExtendedTests.cs) |
| 2-4 | FixPhoneTone 正規化の一致確認 | {-1,0}→{0,1} シフトが正しく発生 | ✅ FixPhoneTone 正規化の一致確認 |

**トーン計算アプローチ: C案を採用**

当初 B案 (AccentPhrase.Accent → トーン直接算出) を検討したが、批判的レビューにより却下。

| 案 | 方法 | 判定 |
|---|---|---|
| A | `ToProsody()` ESPnet 記法パース | △ 文字列パースが煩雑 |
| **B** | **`ToAccentPhrases()` → Accent 核位置からトーン** | **✗ FixPhoneTone の負値シフトを再現不能** |
| **C** | **`ToFullContextLabels()` → A1/A2/A3 → ComputeTonesFromProsody()** | **◎ 既存アルゴリズム完全再利用** |

**C案の根拠**:
- HTS フルコンテキストラベルには A1/A2/A3 が含まれる (OpenJTalk と同一形式)
- 既存の `ComputeTonesFromProsody()` をそのまま呼び出せる
- 12個のテストケースを変更なしで通過できる見込み

**懸念点**:
- 🔴 **FixPhoneTone の再現性**: 現行は A1/A2/A3 → 累積トーン → {-1,0}→{0,1} 正規化という複雑なステートマシン。dot-net-g2p の A1/A2/A3 値が OpenJTalk と 1 でもずれると、トーン計算結果が完全に変わる
- 🔴 **複合句境界処理**: `a3==1 && a2[i+1]==1` での句境界検出は A1/A2/A3 の連続性に強く依存。dot-net-g2p の HTS ラベルが同一パターンを出力するか未検証
- 🟠 **HTS ラベルのフォーマット差異**: dot-net-g2p の `ToFullContextLabels()` 出力が OpenJTalk の HTS ラベル形式と完全一致するか確認が必要。フィールド順序やデリミタの差異がパースエラーを起こす可能性

**Phase 2 判定基準**:
- 12テスト全通過 + 追加50ケース一致 → Phase 3 へ
- A1/A2/A3 が体系的にずれる → C案のパーサー調整で対応可能か検討
- FixPhoneTone 結果が不一致 → 移行計画を見直し

---

### Phase 3: word2ph・句読点処理の互換性検証

**目的**: BERT アライメントに必要な word2ph 計算と句読点処理が正しく動作することを確認する。

| # | タスク | 完了条件 |
|---|---|---|
| 3-1 | 句読点の音素列内表現を特定 | dot-net-g2p での句読点 → pau/記号 の変換規則を文書化 |
| 3-2 | sil/pau スキップロジックの互換性確認 | 先頭/末尾 sil、最初の pau スキップが同等に動作 |
| 3-3 | word2ph 計算結果の比較テスト | 30+ テストケースで `Sum(word2ph) == phonemeIds.Length` |
| 3-4 | テキスト正規化の二重実行テスト | Unity 側 TextNormalizer + dot-net-g2p 内部正規化の干渉がないことを確認 |

**懸念点**:
- 🔴 **句読点キューの設計**: 現行は `openjtalk_phonemize_with_prosody()` が出力する pau の位置と、テキスト中の句読点を FIFO キューで照合する。dot-net-g2p では pau の出力タイミング・個数が異なる可能性があり、キューロジックの再設計が必要になりうる
- 🟠 **テキスト正規化の二重実行**: 現行は `TextNormalizer.Normalize()` を事前実行。dot-net-g2p が `G2POptions.NormalizeText=true` で内部正規化すると、二重正規化により word2ph のテキスト文字数がずれる
- 🟠 **word2ph の未知文字比例配分**: `PhonemeCharacterAligner` は漢字を「未知文字」として音素数を均等配分する。形態素解析結果が変わると音素総数が変わり、比例配分の結果も変化する（既存実装の構造的弱点）

---

### Phase 4: パフォーマンス検証

**目的**: dot-net-g2p が Unity リアルタイム環境で許容範囲のパフォーマンスを発揮することを確認する。

| # | タスク | 完了条件 |
|---|---|---|
| 4-1 | 辞書ロード時間・メモリ計測 | 初期化時のメモリスパイクと所要時間を記録 |
| 4-2 | G2P 処理時間の計測 | 10/20/50 文字の入力で処理時間を記録 |
| 4-3 | GC アロケーション計測 | Unity Profiler で 1回の Process() 呼び出しあたりのアロケーション量 |
| 4-4 | スレッド安全性テスト | UniTask ThreadPool での並行呼び出しテスト |
| 4-5 | 連続呼び出しテスト | 100回連続で Process() を呼び、メモリリーク・性能劣化がないことを確認 |

**パフォーマンス基準** (暫定):

| 指標 | 許容範囲 | 備考 |
|---|---|---|
| 辞書ロード時間 | < 3秒 | 起動時に1回のみ |
| 辞書メモリ使用量 | < 300MB | sys.dic + matrix.bin のヒープ常駐 |
| G2P 処理時間 (20文字) | < 50ms | パイプライン全体 ~621ms の 10% 以下 |
| GC アロケーション/呼出 | < 100KB | フレームドロップ回避 |

**懸念点**:
- 🔴 **メモリ全読み込み**: dot-net-g2p は sys.dic (~103MB) + matrix.bin を全てヒープにロードする。メモリマップファイル非使用のため、モバイル環境では 200-500MB の常駐メモリが問題になる可能性
- 🔴 **GC アロケーション**: 毎回の呼び出しで `List<IToken>`, `List<NjdNode>`, `List<Mora>`, `string[]` 等を新規生成。推定 50-200KB/呼出の一時アロケーション → GC.Collect 誘発
- 🟠 **スレッド安全性**: `LatticeBuilder` の内部バッファがインスタンスフィールド。同一インスタンスの並行呼び出しでレース条件が発生する可能性。per-call インスタンス化またはロック機構が必要
- 🟠 **初期化スパイク**: Lazy 初期化の場合、初回 `Process()` 呼び出し時に辞書全読み込みが発生し UI フリーズのリスク

**Phase 4 判定基準**:
- 全指標が許容範囲内 → Phase 5 へ
- メモリ超過のみ → 非同期ロード + ストリーミング対応を検討
- G2P 処理時間超過 → パイプライン全体への影響を再評価
- GC 問題 → ObjectPool パターン導入を検討

---

## 4. 実装マイルストーン

### Milestone 1: dot-net-g2p アダプター実装

**前提**: Phase 0-4 の検証が全て通過済み。

| # | タスク | 変更ファイル |
|---|---|---|
| M1-1 | `DotNetG2PJapaneseG2P` クラス新規作成 | `TextProcessing/DotNetG2PJapaneseG2P.cs` (新規) |
| M1-2 | HTS ラベル → A1/A2/A3 パーサー実装 | `TextProcessing/HtsLabelParser.cs` (新規) |
| M1-3 | `IG2P` インターフェース実装 | 上記クラスに `Process(string) → G2PResult` |

**設計方針**:

```csharp
public sealed class DotNetG2PJapaneseG2P : IG2P
{
    private readonly G2PEngine _engine;
    private readonly SBV2PhonemeMapper _mapper;

    // IG2P.Process の実装
    // 1. TextNormalizer.Normalize(text)
    // 2. _engine.ToPhonemes(text) → 音素列
    // 3. _engine.ToFullContextLabels(text) → HTS ラベル → A1/A2/A3
    // 4. SBV2PhonemeMapper で音素 → ID 変換
    // 5. ComputeTonesFromProsody() でトーン計算
    // 6. PhonemeCharacterAligner.ComputeWord2Ph() で word2ph
    // 7. return new G2PResult(phonemeIds, tones, languageIds, word2ph)
}
```

**維持するコンポーネント** (変更なし):
- `IG2P.cs` — インターフェース不変
- `G2PResult.cs` — 出力構造不変
- `SBV2PhonemeMapper.cs` — 音素 → ID マッピング
- `PhonemeCharacterAligner.cs` — word2ph 計算
- `PhonemeUtils.cs` — add_blank (Intersperse)
- `BertAligner.cs` — BERT→音素アライメント
- `TextNormalizer.cs` — テキスト正規化
- `SBV2Tokenizer.cs` — DeBERTa トークナイザ

---

### Milestone 2: パイプライン統合

| # | タスク | 変更ファイル | ステータス |
|---|---|---|---|
| M2-1 | TTSPipelineBuilder のデフォルト G2P 差し替え | `Services/TTSPipelineBuilder.cs` | ✅ TTSPipelineBuilder 更新 |
| M2-2 | manifest.json に UPM Git URL 追加 | `Packages/manifest.json` | ✅ (Phase 0 で完了済み) |
| M2-3 | Runtime.asmdef にアセンブリ参照追加 | `uStyleBertVITS2.Runtime.asmdef` | ✅ (Phase 0 で完了済み) |
| M2-4 | link.xml 追加 (IL2CPP 対応) | `Runtime/link.xml` (新規) | ✅ (Phase 0 で完了済み) |
| M2-5 | TTSSettings に G2P バックエンド選択追加 (任意) | `Configuration/TTSSettings.cs` | ✅ TTSSettings.G2PEngine 追加 |

---

### Milestone 3: OpenJTalk 依存の除去

| # | タスク | 変更ファイル |
|---|---|---|
| M3-1 | `JapaneseG2P.cs` 削除 | 削除 |
| M3-2 | `OpenJTalkNative.cs` 削除 | 削除 |
| M3-3 | `OpenJTalkHandle.cs` 削除 | 削除 |
| M3-4 | `OpenJTalkConstants.cs` 削除 | 削除 |
| M3-5 | `openjtalk_wrapper.dll` 削除 | 削除 |
| M3-6 | CLAUDE.md の G2P セクション更新 | `CLAUDE.md` |
| M3-7 | 02_g2p_implementation.md の更新 | `docs/02_g2p_implementation.md` |

**注意**: Milestone 3 は Milestone 1-2 が完了し、全テストが通過した後に実施する。削除前にバックアップブランチを作成すること。

---

### Milestone 4: テスト更新

| # | タスク | 変更ファイル |
|---|---|---|
| M4-1 | G2PTests.cs を dot-net-g2p 対応に書き換え | `Tests/Runtime/G2PTests.cs` |
| M4-2 | G2PDiagnosticTests.cs を書き換え | `Tests/Runtime/G2PDiagnosticTests.cs` |
| M4-3 | HTS ラベルパーサーのユニットテスト追加 | `Tests/Runtime/HtsLabelParserTests.cs` (新規) |
| M4-4 | dot-net-g2p ↔ 期待値の回帰テスト追加 | `Tests/Runtime/DotNetG2PCompatibilityTests.cs` (新規) |
| M4-5 | 既存テストの通過確認 | 変更なし (PhonemeMapper, Aligner, Tokenizer 等) |

**変更不要なテスト**:
- `PhonemeMapperTests.cs` — SBV2PhonemeMapper のテスト
- `PhonemeCharacterAlignerTests.cs` — word2ph のテスト
- `TokenizerTests.cs` — DeBERTa トークナイザのテスト
- `BertAlignerTests.cs` — BERT アライメントのテスト
- `ComputeTonesFromProsodyTests.cs` — トーン計算のテスト (C案なら変更不要)
- `TextNormalizerTests.cs` — テキスト正規化のテスト
- `ToneAndLanguageConsistencyTests.cs` — Python 定義整合性テスト
- `SymbolConsistencyTests.cs` — 112シンボル検証テスト

---

## 5. リスクマトリクス

### 深刻度: 極度に高い

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R1 | トーン計算の FixPhoneTone 再現不能 | 音声のイントネーション破綻 | C案採用 (HTS → A1/A2/A3 → 既存アルゴリズム) |
| R2 | 形態素解析結果の差異 | 音素列・word2ph 不整合 → BERT アライメント崩壊 | Phase 1 で 100+ ケースの出力比較テスト |
| R3 | メモリ全読み込み 200-500MB | モバイル環境で OOM | Phase 4 で計測。超過時は移行範囲をデスクトップに限定 |

### 深刻度: 高い

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R4 | 辞書バイナリ形式の非互換 | MeCabTokenizer 初期化失敗 | Phase 0 で辞書ロードテスト |
| R5 | GC アロケーション連鎖 | フレームドロップ | Phase 4 で計測。超過時は ObjectPool 導入 |
| R6 | 句読点キューと pau 不一致 | 句読点シンボルの位置ずれ | Phase 3 で句読点入りテスト |
| R7 | テキスト正規化の二重実行 | word2ph ずれ | G2POptions.NormalizeText=false に設定 |

### 深刻度: 中程度

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R8 | IL2CPP ストリッピング | 実行時例外 | link.xml で preserve="all" |
| R9 | UPM Git URL バージョン未固定 | 再現不能ビルド | tag/hash でロック |
| R10 | スレッド安全性 | レース条件 | per-call インスタンス or ロック |
| R11 | 無声化母音の出力形式差異 | 音素 ID マッピング失敗 | Phase 1 で確認 |
| R12 | HTS ラベルフォーマット差異 | A1/A2/A3 パース失敗 | Phase 2 で確認 |

---

## 6. 中止基準

以下のいずれかに該当した場合、移行を中止し代替案を検討する。

| 条件 | Phase | 代替案 |
|---|---|---|
| 辞書ロード不可 | 0 | dot-net-g2p 用辞書を別途入手、または移行中止 |
| IL2CPP ビルド失敗 (link.xml でも解消不能) | 0 | NuGet DLL を Plugins/ に直接配置 |
| 音素出力の一致率 < 90% | 1 | 差異パターンの変換レイヤー追加、または移行中止 |
| A1/A2/A3 の体系的ずれ | 2 | B案への切り替え検討、または移行中止 |
| 辞書メモリ > 500MB (デスクトップ) | 4 | メモリマップ対応 PR を dot-net-g2p に提案 |
| G2P 処理時間 > 200ms (20文字) | 4 | パフォーマンスチューニング、または移行中止 |

---

## 7. 変更ファイルサマリ

### 新規作成

| ファイル | 内容 |
|---|---|
| `TextProcessing/DotNetG2PJapaneseG2P.cs` | 新 G2P 実装 |
| `TextProcessing/HtsLabelParser.cs` | HTS ラベル → A1/A2/A3 パーサー |
| `TextProcessing/ProsodyToneCalculator.cs` | ComputeTonesFromProsody 共有ユーティリティ (Phase 2) |
| `Runtime/link.xml` | IL2CPP ストリッピング防止 |
| `Tests/Runtime/HtsLabelParserTests.cs` | パーサーテスト |
| `Tests/Runtime/ToneCalculationExtendedTests.cs` | 50+ 追加トーン検証テスト (Phase 2) |
| `Tests/Runtime/DotNetG2PCompatibilityTests.cs` | 互換性回帰テスト |

### 変更

| ファイル | 変更内容 |
|---|---|
| `Packages/manifest.json` | UPM Git URL 追加 |
| `uStyleBertVITS2.Runtime.asmdef` | アセンブリ参照追加 |
| `Services/TTSPipelineBuilder.cs` | デフォルト G2P 差し替え |
| `Tests/Runtime/G2PTests.cs` | NativeDLL → dot-net-g2p |
| `Tests/Runtime/G2PDiagnosticTests.cs` | NativeDLL → dot-net-g2p |
| `CLAUDE.md` | G2P セクション更新 |
| `docs/02_g2p_implementation.md` | 実装ガイド更新 |

### 削除

| ファイル | 理由 |
|---|---|
| `TextProcessing/JapaneseG2P.cs` | 旧 OpenJTalk 実装 |
| `Native/OpenJTalkNative.cs` | P/Invoke 定義 |
| `Native/OpenJTalkHandle.cs` | SafeHandle |
| `Native/OpenJTalkConstants.cs` | 辞書パス定数 |
| `Plugins/Windows/x86_64/openjtalk_wrapper.dll` | ネイティブ DLL |

### 変更なし (維持)

| ファイル | 理由 |
|---|---|
| `IG2P.cs`, `G2PResult.cs` | インターフェース・出力構造不変 |
| `SBV2PhonemeMapper.cs` | 音素 → ID マッピング不変 |
| `PhonemeCharacterAligner.cs` | word2ph 計算不変 |
| `PhonemeUtils.cs` | add_blank 不変 |
| `BertAligner.cs`, `BertAlignmentJob.cs` | BERT アライメント不変 |
| `TextNormalizer.cs` | テキスト正規化不変 |
| `SBV2Tokenizer.cs` | DeBERTa トークナイザ不変 |
| `TTSPipeline.cs` | パイプライン本体不変 |
| 全 BERT/TTS 推論コード | 影響なし |
