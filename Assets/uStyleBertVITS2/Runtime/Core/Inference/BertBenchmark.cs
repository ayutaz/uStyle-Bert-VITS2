using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.InferenceEngine;
using UnityEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// BERT推論バックエンド別ベンチマーク。
    /// ビルド済みプレイヤーでも動作するよう Runtime に配置。
    /// </summary>
    public static class BertBenchmark
    {
        public struct Result
        {
            public string Label;
            public double AvgMs;
            public double InfPerSec;
        }

        public struct BackendResult
        {
            public string BackendName;
            public Result[] Sizes;
        }

        private static readonly (string label, int count)[] InputSizes = new[]
        {
            ("5 tokens", 5),
            ("20 tokens", 20),
            ("40 tokens", 40),
        };

        private static int[] MakeTokenIds(int count)
        {
            var ids = new int[count];
            ids[0] = 1; // CLS
            ids[count - 1] = 2; // SEP
            for (int i = 1; i < count - 1; i++)
                ids[i] = 100 + i;
            return ids;
        }

        private static int[] MakeMask(int count)
        {
            var mask = new int[count];
            for (int i = 0; i < count; i++)
                mask[i] = 1;
            return mask;
        }

        /// <summary>
        /// 指定した IBertRunner で 3サイズ (5/20/40 tokens) を warmup + iterations で計測する。
        /// </summary>
        public static Result[] RunAllSizes(IBertRunner runner, int warmup = 3, int iterations = 10)
        {
            int maxTokens = 0;
            foreach (var (_, count) in InputSizes)
                if (count > maxTokens) maxTokens = count;

            var dest = new float[1024 * maxTokens];
            var results = new Result[InputSizes.Length];

            for (int i = 0; i < InputSizes.Length; i++)
            {
                var (label, count) = InputSizes[i];
                var tokenIds = MakeTokenIds(count);
                var mask = MakeMask(count);

                // Warmup
                for (int w = 0; w < warmup; w++)
                    runner.Run(tokenIds, mask, dest);

                // Measure
                var sw = new Stopwatch();
                sw.Start();
                for (int iter = 0; iter < iterations; iter++)
                    runner.Run(tokenIds, mask, dest);
                sw.Stop();

                double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
                results[i] = new Result
                {
                    Label = label,
                    AvgMs = avgMs,
                    InfPerSec = avgMs > 0 ? 1000.0 / avgMs : 0,
                };
            }

            return results;
        }

        /// <summary>
        /// ORT バックエンド (DirectML + CPU) のベンチマークを実行し、結果リストに追加する。
        /// Runtime asmdef 内なので USBV2_ORT_AVAILABLE が有効。
        /// Assembly-CSharp 側から #if なしで呼び出せる。
        /// </summary>
        /// <param name="ortModelPath">ORT 用 ONNX モデルの絶対パス</param>
        /// <param name="results">結果を追加する先のリスト</param>
        public static void RunOrtBenchmarks(string ortModelPath, List<BackendResult> results)
        {
#if USBV2_ORT_AVAILABLE
            if (!File.Exists(ortModelPath))
            {
                UnityEngine.Debug.LogWarning($"[BertBenchmark] ORT skipped — model not found: {ortModelPath}");
                return;
            }

            // DirectML
            try
            {
                using var dmlRunner = TryCreateOrt(ortModelPath, useDirectML: true);
                if (dmlRunner != null)
                {
                    var dmlResults = RunAllSizes(dmlRunner);
                    results.Add(new BackendResult { BackendName = "ORT DirectML", Sizes = dmlResults });
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[BertBenchmark] ORT DirectML skipped — DLL not available");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BertBenchmark] ORT DirectML failed: {e.Message}");
            }

            // CPU
            try
            {
                using var cpuRunner = TryCreateOrt(ortModelPath, useDirectML: false);
                if (cpuRunner != null)
                {
                    var cpuResults = RunAllSizes(cpuRunner);
                    results.Add(new BackendResult { BackendName = "ORT CPU", Sizes = cpuResults });
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[BertBenchmark] ORT CPU failed to create runner");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BertBenchmark] ORT CPU failed: {e.Message}");
            }
#else
            UnityEngine.Debug.LogWarning("[BertBenchmark] ORT not available — USBV2_ORT_AVAILABLE not defined");
#endif
        }

#if USBV2_ORT_AVAILABLE
        private static OnnxRuntimeBertRunner TryCreateOrt(string modelPath, bool useDirectML)
        {
            try
            {
                return new OnnxRuntimeBertRunner(modelPath, useDirectML);
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[BertBenchmark] Failed to create ORT runner (DirectML={useDirectML}): {e.Message}");
                return null;
            }
        }
#endif

        /// <summary>
        /// ベンチマーク結果をフォーマット済み文字列で返す。
        /// </summary>
        public static string FormatResults(List<BackendResult> backends)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BERT Inference Benchmark ===");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"Platform: {Application.platform}");
            sb.AppendLine();

            // Find Sentis CPU results for speedup calculation
            Result[] sentisCpu = null;
            foreach (var b in backends)
            {
                if (b.BackendName == "Sentis CPU")
                {
                    sentisCpu = b.Sizes;
                    break;
                }
            }

            foreach (var backend in backends)
            {
                sb.AppendLine($"[{backend.BackendName}]");
                foreach (var r in backend.Sizes)
                    sb.AppendLine($"  {r.Label,-12} {r.AvgMs,8:F2} ms  ({r.InfPerSec,6:F1} inf/sec)");
                sb.AppendLine();
            }

            // Speedup ratios vs Sentis CPU
            if (sentisCpu != null && backends.Count > 1)
            {
                sb.AppendLine("[Speedup vs Sentis CPU]");
                foreach (var backend in backends)
                {
                    if (backend.BackendName == "Sentis CPU") continue;
                    sb.Append($"  {backend.BackendName,-16} ");
                    for (int i = 0; i < backend.Sizes.Length; i++)
                    {
                        double speedup = sentisCpu[i].AvgMs / backend.Sizes[i].AvgMs;
                        sb.Append($"{speedup:F1}x ({backend.Sizes[i].Label})");
                        if (i < backend.Sizes.Length - 1) sb.Append(" / ");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
    }
}
