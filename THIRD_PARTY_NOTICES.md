# Third-Party Notices

This repository's source code is licensed under Apache License 2.0.  
Some runtime assets and model-related artifacts may be governed by separate licenses and terms.

## 1. Style-Bert-VITS2 (reference implementation)

- Upstream: https://github.com/litagin02/Style-Bert-VITS2
- License (upstream repository): AGPL-3.0
- Usage here: architecture/reference implementation and ONNX conversion workflow reference

## 2. Tsukuyomi-chan corpus and derived TTS model terms

- Upstream model used in this project context:
  - https://huggingface.co/ayousanz/tsukuyomi-chan-style-bert-vits2-model
- Hugging Face model card license field: `other` (non-standard license field)
- Primary rights holder/project references:
  - Tsukuyomi-chan corpus page and terms: https://tyc.rei-yumesaki.net/material/corpus/
  - Tsukuyomi-chan character license: https://tyc.rei-yumesaki.net/about/terms/
  - Credit notation guidance: https://tyc.rei-yumesaki.net/about/terms/credit/
  - The official pages above note recent revisions (character license: 2025-09-26, corpus terms update: 2025-11-18).

### Practical compliance requirements (important)

When distributing apps, APIs, models, or services that expose Tsukuyomi-chan voice characteristics to third parties:

1. Credit/attribution is required by the corpus terms when distributing software/services that expose this voice.
2. Distribution of voice-synthesis/voice-conversion software based on this corpus is generally limited to free distribution under the corpus terms (check official exceptions and details).
3. Commercial use of AI models learned from this corpus may require prior inquiry/approval from the rights holder under the corpus terms.
4. For paid APIs/services (where allowed), required credit information must be visible before purchase/payment.
5. If character visuals/design (illustrations, avatar, 3D model, etc.) are used, comply with the character license and related credit rules in addition to corpus terms.
6. Direct use of the corpus itself as raw material for music/video assets is not allowed under the corpus terms.
7. Redistribution of the corpus itself is generally prohibited (except limited exceptional cases defined by the official terms).
8. Redistribution/sale of synthesized voice data as reusable material is generally prohibited unless separately permitted by the rights holder.
9. Content/use categories restricted by the character license (for example, certain political/religious/ideological advocacy or attack-oriented use) must be respected.
10. Terms may be revised; always verify the latest official pages before release.

### Suggested attribution block (non-normative template)

Use the official credit examples on the linked pages. A minimal project-level attribution reference is:

- Voice corpus: Tsukuyomi-chan Corpus (CV: Rei Yumesaki)
- Source: https://tyc.rei-yumesaki.net/material/corpus/
- Character/project: Tsukuyomi-chan / Rei Yumesaki

This summary is not a substitute for the official terms. The official pages above take precedence.

## 3. Japanese DeBERTa source model

- Upstream model: https://huggingface.co/ku-nlp/deberta-v2-large-japanese-char-wwm
- License: CC BY-SA 4.0

## 4. OpenJTalk / NAIST dictionary

- Upstream: https://github.com/r9y9/open_jtalk
- Dictionary path: `src/mecab-naist-jdic`
- License text: https://raw.githubusercontent.com/r9y9/open_jtalk/master/src/mecab-naist-jdic/COPYING

## 5. ONNX Runtime and DirectML binaries

- ONNX Runtime: https://github.com/microsoft/onnxruntime
- DirectML: https://github.com/microsoft/DirectML
- Distributed binaries must follow each upstream project's redistribution terms.

## Notes

- If multiple licenses/terms apply to your distribution, comply with all applicable conditions.
- This file is a technical attribution summary, not legal advice.
