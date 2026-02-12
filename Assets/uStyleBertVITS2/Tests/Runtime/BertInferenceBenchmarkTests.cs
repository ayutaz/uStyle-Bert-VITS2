using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;
using uStyleBertVITS2.Inference;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// BERT推論バックエンド別ベンチマーク。
    /// Sentis CPU / ORT DirectML / ORT CPU の速度を計測する。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    [Category("Benchmark")]
    public class BertInferenceBenchmarkTests
    {
        private const string DeBERTaAssetPath = "Assets/uStyleBertVITS2/Models/deberta_fp32.onnx";
        private const int Warmup = 3;
        private const int Iterations = 10;

        // 3 input sizes: short / medium / long
        private static readonly (string label, int[] tokenIds)[] InputSizes = new[]
        {
            ("5 tokens", MakeTokenIds(5)),
            ("20 tokens", MakeTokenIds(20)),
            ("40 tokens", MakeTokenIds(40)),
        };

        private static int[] MakeTokenIds(int count)
        {
            // [CLS]=1, body tokens 100..., [SEP]=2
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

#if UNITY_EDITOR
        private static ModelAsset LoadModelAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(DeBERTaAssetPath);
            if (asset == null)
                Assert.Ignore($"ModelAsset not found at: {DeBERTaAssetPath}");
            return asset;
        }
#endif

#if USBV2_ORT_AVAILABLE
        private static string GetOrtModelPath()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "uStyleBertVITS2/Models/deberta_for_ort.onnx");
            if (!File.Exists(path))
                Assert.Ignore($"ORT BERT model not found: {path}");
            return path;
        }
#endif

        /// <summary>
        /// Warmup + 計測を実行し、平均レイテンシ(ms)を返す。
        /// </summary>
        private static double MeasureInference(IBertRunner runner, int[] tokenIds, int[] mask, float[] dest)
        {
            // Warmup
            for (int i = 0; i < Warmup; i++)
                runner.Run(tokenIds, mask, dest);

            // Measure
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < Iterations; i++)
                runner.Run(tokenIds, mask, dest);
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds / Iterations;
        }

        private static string FormatResult(string label, double avgMs)
        {
            double infPerSec = avgMs > 0 ? 1000.0 / avgMs : 0;
            return $"  {label,-12} {avgMs,8:F2} ms  ({infPerSec,6:F1} inf/sec)";
        }

        private static double[] RunAllSizes(IBertRunner runner)
        {
            var results = new double[InputSizes.Length];
            // Pre-allocate dest buffer for largest size
            int maxTokens = 0;
            foreach (var (_, ids) in InputSizes)
                if (ids.Length > maxTokens) maxTokens = ids.Length;
            var dest = new float[1024 * maxTokens];

            for (int i = 0; i < InputSizes.Length; i++)
            {
                var (label, tokenIds) = InputSizes[i];
                var mask = MakeMask(tokenIds.Length);
                results[i] = MeasureInference(runner, tokenIds, mask, dest);
            }
            return results;
        }

        private static void LogResults(string backendName, double[] results)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{backendName}]");
            for (int i = 0; i < InputSizes.Length; i++)
                sb.AppendLine(FormatResult(InputSizes[i].label, results[i]));
            UnityEngine.Debug.Log(sb.ToString());
        }

        [Test]
        public void Benchmark_SentisCPU()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Sentis model loading only supported in Editor.");
#else
            var asset = LoadModelAsset();
            using var runner = new BertRunner(asset, BackendType.CPU);

            var results = RunAllSizes(runner);

            var sb = new StringBuilder();
            sb.AppendLine("=== BERT Inference Benchmark: Sentis CPU ===");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"Warmup: {Warmup}, Iterations: {Iterations}");
            sb.AppendLine();
            for (int i = 0; i < InputSizes.Length; i++)
                sb.AppendLine(FormatResult(InputSizes[i].label, results[i]));
            UnityEngine.Debug.Log(sb.ToString());
            Assert.Pass();
#endif
        }

#if USBV2_ORT_AVAILABLE
        private static OnnxRuntimeBertRunner TryCreateDirectML(string path)
        {
            try
            {
                return new OnnxRuntimeBertRunner(path, useDirectML: true);
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }
        }

        [Test]
        public void Benchmark_OrtDirectML()
        {
            var path = GetOrtModelPath();
            using var runner = TryCreateDirectML(path);
            if (runner == null)
                Assert.Ignore("DirectML native library not available in this environment.");

            var results = RunAllSizes(runner);

            var sb = new StringBuilder();
            sb.AppendLine("=== BERT Inference Benchmark: ORT DirectML ===");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"Warmup: {Warmup}, Iterations: {Iterations}");
            sb.AppendLine();
            for (int i = 0; i < InputSizes.Length; i++)
                sb.AppendLine(FormatResult(InputSizes[i].label, results[i]));
            UnityEngine.Debug.Log(sb.ToString());
            Assert.Pass();
        }

        [Test]
        public void Benchmark_OrtCPU()
        {
            var path = GetOrtModelPath();
            using var runner = new OnnxRuntimeBertRunner(path, useDirectML: false);

            var results = RunAllSizes(runner);

            var sb = new StringBuilder();
            sb.AppendLine("=== BERT Inference Benchmark: ORT CPU ===");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"Warmup: {Warmup}, Iterations: {Iterations}");
            sb.AppendLine();
            for (int i = 0; i < InputSizes.Length; i++)
                sb.AppendLine(FormatResult(InputSizes[i].label, results[i]));
            UnityEngine.Debug.Log(sb.ToString());
            Assert.Pass();
        }
#endif

        [Test]
        public void Benchmark_Comparison()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Sentis model loading only supported in Editor.");
#else
            var asset = LoadModelAsset();

            // Sentis CPU
            double[] sentisCpu;
            using (var runner = new BertRunner(asset, BackendType.CPU))
                sentisCpu = RunAllSizes(runner);

            var sb = new StringBuilder();
            sb.AppendLine("=== BERT Inference Benchmark: Comparison ===");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"Warmup: {Warmup}, Iterations: {Iterations}");
            sb.AppendLine();

            sb.AppendLine("[Sentis CPU]");
            for (int i = 0; i < InputSizes.Length; i++)
                sb.AppendLine(FormatResult(InputSizes[i].label, sentisCpu[i]));

#if USBV2_ORT_AVAILABLE
            var ortPath = Path.Combine(Application.streamingAssetsPath, "uStyleBertVITS2/Models/deberta_for_ort.onnx");
            bool ortAvailable = File.Exists(ortPath);

            double[] ortDirectML = null;
            double[] ortCpu = null;

            if (ortAvailable)
            {
                // DirectML — may not be available in Editor
                sb.AppendLine();
                using (var dmlRunner = TryCreateDirectML(ortPath))
                {
                    if (dmlRunner != null)
                    {
                        ortDirectML = RunAllSizes(dmlRunner);
                        sb.AppendLine("[ORT DirectML]");
                        for (int i = 0; i < InputSizes.Length; i++)
                            sb.AppendLine(FormatResult(InputSizes[i].label, ortDirectML[i]));
                    }
                    else
                    {
                        sb.AppendLine("[ORT DirectML] Skipped — DirectML native library not available");
                    }
                }

                sb.AppendLine();
                using (var runner = new OnnxRuntimeBertRunner(ortPath, useDirectML: false))
                    ortCpu = RunAllSizes(runner);
                sb.AppendLine("[ORT CPU]");
                for (int i = 0; i < InputSizes.Length; i++)
                    sb.AppendLine(FormatResult(InputSizes[i].label, ortCpu[i]));

                // Speedup ratios
                sb.AppendLine();
                sb.AppendLine("[Speedup vs Sentis CPU]");
                if (ortDirectML != null)
                {
                    sb.Append("  DirectML:  ");
                    for (int i = 0; i < InputSizes.Length; i++)
                    {
                        double speedup = sentisCpu[i] / ortDirectML[i];
                        sb.Append($"{speedup:F1}x ({InputSizes[i].label})");
                        if (i < InputSizes.Length - 1) sb.Append(" / ");
                    }
                    sb.AppendLine();
                }

                sb.Append("  ORT CPU:   ");
                for (int i = 0; i < InputSizes.Length; i++)
                {
                    double speedup = sentisCpu[i] / ortCpu[i];
                    sb.Append($"{speedup:F1}x ({InputSizes[i].label})");
                    if (i < InputSizes.Length - 1) sb.Append(" / ");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("[ORT] Skipped — model not found");
            }
#endif

            UnityEngine.Debug.Log(sb.ToString());
            Assert.Pass();
#endif
        }
    }
}
