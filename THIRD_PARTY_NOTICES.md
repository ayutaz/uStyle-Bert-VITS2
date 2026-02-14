# サードパーティーに関するお知らせ

このリポジトリのソースコードは Apache License 2.0 で提供されています。  
ただし、実行時アセットやモデル関連資材には、別途ライセンス・利用規約が適用される場合があります。

## 1. Style-Bert-VITS2（参照実装）

- 上流リポジトリ: https://github.com/litagin02/Style-Bert-VITS2
- 上流ライセンス: AGPL-3.0
- 本リポジトリでの扱い: アーキテクチャおよび ONNX 変換手順の参照

## 2. つくよみちゃん関連（コーパス・キャラクター規約）

- 本プロジェクト文脈で利用する上流モデル:
  - https://huggingface.co/ayousanz/tsukuyomi-chan-style-bert-vits2-model
- Hugging Face のモデルカード上のライセンス表記:
  - `other`（標準 SPDX ではない独自規約系）
- 公式規約ページ:
  - つくよみちゃんコーパス: https://tyc.rei-yumesaki.net/material/corpus/
  - つくよみちゃんキャラクターライセンス: https://tyc.rei-yumesaki.net/about/terms/
  - クレジット表記ガイド: https://tyc.rei-yumesaki.net/about/terms/credit/

### 実運用上の要点（要約）

以下は公式ページの要点を抜粋した実務向けメモです。最終判断は必ず公式規約本文で行ってください。

1. つくよみちゃんコーパスの声質を第三者が利用可能な形で公開する場合、クレジット表記が必須です。
2. 有料で公開する場合、必要なクレジット情報は「支払い前に確認できる場所」にも表示が必要です。
3. コーパス本体の再配布は原則禁止です（例外は公式規約に従う）。
4. コーパス声質で合成した音声を「素材として再配布・販売」する行為は原則禁止です。
5. コーパス自体を音楽・動画素材として利用することは不可です（詳細はコーパス規約参照）。
6. キャラクター利用時は、政治・宗教・思想への賛否呼びかけ、実在対象への批判・攻撃など、キャラクターライセンス上の禁止事項を遵守してください。
7. キャラクターデザイン（立ち絵・イラスト等）を併用する場合は、キャラクターライセンスとクレジット規定もあわせて遵守してください。
8. 規約は更新される可能性があるため、公開・配布前に公式ページの最新内容を再確認してください。

### クレジット記載について

クレジットの具体的な文面は、公式の「クレジット表記について」に用途別の例文があります。  
本プロジェクトで配布するアプリ/API/モデルの公開時は、必ず上記公式ページの最新例文に合わせてください。

本項の要約は法的助言ではありません。公式規約本文が常に優先されます。

## 3. 日本語 DeBERTa 元モデル

- 上流モデル: https://huggingface.co/ku-nlp/deberta-v2-large-japanese-char-wwm
- ライセンス: CC BY-SA 4.0

## 4. OpenJTalk / NAIST 辞書

- 上流: https://github.com/r9y9/open_jtalk
- 辞書パス: `src/mecab-naist-jdic`
- ライセンス本文: https://raw.githubusercontent.com/r9y9/open_jtalk/master/src/mecab-naist-jdic/COPYING

## 5. ONNX Runtime / DirectML バイナリ

- ONNX Runtime: https://github.com/microsoft/onnxruntime
- DirectML: https://github.com/microsoft/DirectML
- 再配布時は各上流プロジェクトの再配布条件に従ってください。

## 注意

- 複数のライセンス・規約が重なる場合は、適用される条件をすべて満たす必要があります。
- 本ファイルは技術的なライセンス整理のための要約であり、法的助言ではありません。
