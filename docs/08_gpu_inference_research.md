# DeBERTa GPU推論 高速化 — 網羅的調査レポート

## 問題

Unity Sentis 2.5.0 で DeBERTa (FP32) を GPUCompute で実行すると **D3D12 デバイスロスト (TDR)** が発生し、CPU 実行が強制される。BERT推論がパイプライン全体の40-50%を占める最大ボトルネック。

### 現状パフォーマンス

| 構成 | レイテンシ |
|---|---|
| BERT=CPU + TTS=GPU (2回目以降) | **~621ms** |
| PyTorch GPU (参考値) | ~100-200ms |

---

## 1. GPU推論エンジン比較 (13種)

| # | アプローチ | GPU対応 | C#対応 | 性能 | 実現性 | 備考 |
|---|---|---|---|---|---|---|
| 1 | **ONNX Runtime + DirectML** (asus4) | 全DX12 GPU | C# Native | 7/10 | **VERY HIGH** | UPM導入可。CUDA比1.5-2x遅い |
| 2 | **ONNX Runtime + CUDA** | NVIDIA | C# Native | 9/10 | **HIGH** | CUDA 12.x + cuDNN 9.x 必要 |
| 3 | **ONNX Runtime + TensorRT** | NVIDIA | via ORT | 10/10 | HIGH | DeBERTa専用TRTプラグイン有 |
| 4 | **Rust ort DLL** | DirectML/CUDA | P/Invoke | 9/10 | HIGH | sbv2-api実証済。Rust環境必要 |
| 5 | TensorRT standalone | NVIDIA | P/Invoke | 10/10 | MEDIUM | セットアップ超複雑 |
| 6 | PyTorch LibTorch DLL | NVIDIA | P/Invoke | 8/10 | LOW | DLL 2-3GB |
| 7 | WinML | DirectML | WinRT | 6/10 | **LOW** | WinRTバリア、opset 12制限 |
| 8 | OpenVINO | Intel | P/Invoke | 5/10 | LOW | Intel GPUのみ |
| 9 | ManagedCUDA / ILGPU | NVIDIA/AMD | C# Native | 8/10 | LOW | カーネル自作 |
| 10 | bert.cpp / GGML | なし | P/Invoke | 4/10 | LOW | GPU非対応 |
| 11 | Ollama / Triton | 全GPU | REST API | 8/10 | MEDIUM | IPC遅延 |
| 12 | MNN (Alibaba) / MACE | Vulkan/OpenCL | P/Invoke | 7/10 | LOW | C++ wrapper必要 |
| 13 | 手動Compute Shader | 全GPU | Native | 7/10 | **VERY LOW** | 月単位の開発 |

### 推奨: ONNX Runtime + DirectML (asus4/onnxruntime-unity)

- **理由**: UPMで即導入可、C# APIネイティブ対応、全DX12 GPU対応、実績あり
- **欠点**: CUDA比1.5-2x遅い（ただしCPUよりは大幅に高速）
- **将来拡張**: CUDA EP追加でNVIDIA GPU最適化可能

---

## 2. Sentis内回避策

| アプローチ | 実現性 | 備考 |
|---|---|---|
| TDRレジストリ延長 | 開発時のみ | `TdrDelay=10-60`。ユーザーに要求不可 |
| FP16量子化 | **不可** | Sentis 2.5.0 DeBERTaで KeyNotFoundException |
| ScheduleIterable | LOW | TDR回避効果不明 |
| Vulkan backend | 不明 | Sentis Vulkan compute 対応未確認 |

**結論**: Sentis内でのDeBERTa GPU推論は現時点で実用的な回避策なし。外部推論エンジンが必要。

---

## 3. モデルレベル最適化

| # | アプローチ | BERT削減 | 実現性 | リスク |
|---|---|---|---|---|
| 1 | **不要レイヤー削除** (24層→22層) | ~8% | **HIGH** | 低 |
| 2 | **ONNX Runtime グラフ最適化** (fusion) | 15-25% | **HIGH** | 低 |
| 3 | **base モデル + projection** (768→1024) | ~40-50% | HIGH (要再学習) | 中 |
| 4 | Attention head pruning | 10-20% | MEDIUM | 中 |
| 5 | 動的シーケンス長 | 10-95% | ORT可/Sentis不可 | — |

### 重要な発見

1. **DeBERTa-v2-large = 24層** — hidden_states[-3] = layer 21出力。層22-23は不要 (8.3%削減)
2. **ku-nlp/deberta-v2-base (12層, 768dim)** — char-wwm 版あり。同一トークナイザ
3. **ONNX Runtime Transformer Optimizer** — DeBERTa用 attention fusion 組込済

---

## 4. 技術詳細

### A. asus4/onnxruntime-unity 導入

**パッケージ**: v0.4.4 (ORT 1.23.2)

```json
// manifest.json
{
  "scopedRegistries": [{
    "name": "NPM",
    "url": "https://registry.npmjs.com",
    "scopes": ["com.github.asus4"]
  }],
  "dependencies": {
    "com.github.asus4.onnxruntime": "0.4.4",
    "com.github.asus4.onnxruntime.win-x64-gpu": "0.4.4"
  }
}
```

**C# API (BERT推論例)**:

```csharp
var opts = new SessionOptions();
opts.AppendExecutionProvider_DML(0);  // DirectML GPU
opts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED;
using var session = new InferenceSession("deberta.onnx", opts);

// 入力テンソル作成
using var ids = OrtValue.CreateTensorValueFromMemory(tokenIds, new long[]{1, seqLen});
using var mask = OrtValue.CreateTensorValueFromMemory(attentionMask, new long[]{1, seqLen});

var inputs = new Dictionary<string, OrtValue> {
    {"input_ids", ids}, {"attention_mask", mask}
};

// 推論実行
using var results = session.Run(new RunOptions(), inputs, session.OutputNames);
ReadOnlySpan<float> output = results[0].GetTensorDataAsSpan<float>();
```

**スレッド安全性**: セッション単体は sequential のみ。背景スレッドから呼べるが、同時呼出は `lock` or セッション分離が必要。

**注意**: DirectML.dll は Editor 実行可能ファイルと同じフォルダに配置が必要。

### B. Rust ort + P/Invoke (代替案)

**Cargo.toml**:

```toml
[lib]
crate-type = ["cdylib"]

[dependencies]
ort = { version = "2.0", features = ["directml"] }
ndarray = "0.15"
```

**FFI インターフェース**:

```rust
#[no_mangle] pub extern "C" fn sbv2_bert_create_session(path: *const u8, len: usize) -> *mut Handle;
#[no_mangle] pub extern "C" fn sbv2_bert_infer(handle: *mut Handle, ids: *const i32, ...) -> i32;
#[no_mangle] pub extern "C" fn sbv2_bert_destroy(handle: *mut Handle);
```

**C# P/Invoke**:

```csharp
[DllImport("sbv2_rust_bridge")] static extern IntPtr sbv2_bert_create_session(byte[] path, ulong len);
[DllImport("sbv2_rust_bridge")] static extern int sbv2_bert_infer(IntPtr h, int[] ids, ...);
```

### C. モデルレイヤー削除 (PyTorch level)

```python
# convert_bert_for_sentis.py に統合
model = AutoModel.from_pretrained("ku-nlp/deberta-v2-large-japanese-char-wwm")
# 24層 → 22層 (hidden_states[-3] = layer 21出力。層22-23を除去)
model.encoder.layer = nn.ModuleList(list(model.encoder.layer)[:22])
# トランケート後: hidden_states[-1] = layer 21出力 = 元のhidden_states[-3]
```

### D. ONNX Runtime グラフ最適化

```python
from onnxruntime.transformers import optimizer
opt_model = optimizer.optimize_model(
    "deberta.onnx", model_type="bert",
    num_heads=16, hidden_size=1024, opt_level=2
)
opt_model.save_model_to_file("deberta_optimized.onnx")
```

---

## 5. 実装ロードマップ

### Phase 1: IBertRunner 抽象化 + ONNX Runtime 導入 ✅ 完了

1. ✅ `IBertRunner` インターフェース抽出
2. ✅ ONNX Runtime パッケージ導入 (asus4 UPM v0.4.4)
3. ✅ `OnnxRuntimeBertRunner : IBertRunner` 実装
4. ✅ TTSSettings に `BertEngineType` 設定追加 (Sentis / OnnxRuntime)
5. ✅ テスト + ベンチマーク (`BertInferenceBenchmarkTests`)

### Phase 2: モデル最適化

1. レイヤー削除 (24→22層) — convert_bert_for_sentis.py に統合
2. ONNX Runtime グラフ最適化
3. (オプション) base モデル + projection

### Phase 3 (オプション): CUDA EP 対応

1. CUDA EP パッケージ追加
2. TTSSettings に GPU プロバイダー選択追加
3. ベンチマーク比較

---

## 6. ベンチマーク結果 (実測)

### BERT 推論バックエンド別レイテンシ

環境: NVIDIA GeForce RTX 4070 Ti SUPER / Warmup 3回 / 計測 10回平均

| 入力サイズ | Sentis CPU | ORT CPU | ORT CPU スピードアップ |
|---|---|---|---|
| 5 tokens | ~1000 ms | ~410 ms | **1.9x** |
| 20 tokens | ~860-1090 ms | ~410-554 ms | **2.0x** |
| 40 tokens | ~860-1060 ms | ~460-530 ms | **2.0x** |

> ORT DirectML は Editor PlayMode テスト環境では DML ネイティブ DLL がロードされないためスキップ。ビルド済みアプリでの計測が必要。

### パイプライン全体の改善見込み

| 項目 | 現状 (Sentis CPU) | Phase 1 (ORT CPU) | Phase 2 (モデル最適化) |
|---|---|---|---|
| BERT 推論 | ~1000 ms | **~410-530 ms** | **~300-400ms** (予測) |
| パイプライン全体 | ~621 ms | **改善中** | **~400ms** (予測) |
| メインスレッド拘束 | BERT+TTS | **TTSのみ** | TTSのみ |

---

## 7. リスクと対策

| リスク | 対策 |
|---|---|
| DirectML TDR | ORT独自dispatch。発生時CPU fallback |
| DLLサイズ (~50-100MB) | DirectML.dllはWindows標準搭載 |
| Editor DLL配置 | DirectML.dllをEditorフォルダに要配置 |
| NVIDIA GPU でDirectML遅い | 将来CUDA EP追加で対応可 |
| レイヤー削除で品質劣化 | hidden_states[-3]が最終出力なので理論上影響なし |

---

## 8. 検証方法

1. OnnxRuntimeBertRunner で DirectML GPU 推論が TDR なしで完了するか
2. Sentis CPU 版との出力比較 (許容誤差 1e-5)
3. Unity Profiler `TTS.BERT.Inference` マーカーでレイテンシ計測
4. PlayMode テスト end-to-end
5. レイヤー削除後のモデルで音声品質聴取テスト

---

## 9. 参照リソース

- [asus4/onnxruntime-unity](https://github.com/asus4/onnxruntime-unity) v0.4.4
- [asus4/onnxruntime-unity-examples](https://github.com/asus4/onnxruntime-unity-examples)
- [ONNX Runtime C# API](https://onnxruntime.ai/docs/get-started/with-csharp.html)
- [ONNX Runtime DirectML EP](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html)
- [ONNX Runtime CUDA EP](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html)
- [ONNX Runtime Transformers Optimizer](https://onnxruntime.ai/docs/performance/transformers-optimization.html)
- [Christian Mills Unity ONNX Tutorial](https://christianjmills.com/posts/unity-onnxruntime-cv-plugin-tutorial/)
- [sbv2-api (Rust SBV2)](https://github.com/neodyland/sbv2-api)
- [pykeio/ort crate](https://github.com/pykeio/ort) v2.0
- [Cysharp/csbindgen](https://github.com/Cysharp/csbindgen) — Rust↔C# 自動FFI生成
- [ku-nlp/deberta-v2-base-japanese-char-wwm](https://huggingface.co/ku-nlp/deberta-v2-base-japanese-char-wwm)
- [DeBERTa TensorRT Plugin](https://github.com/symphonylyh/deberta-tensorrt)
- [ONNX-GraphSurgeon](https://github.com/NVIDIA/TensorRT/tree/master/tools/onnx-graphsurgeon)
