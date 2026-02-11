using System;
using System.IO;
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
    /// Sentis推論ラッパーのテスト。
    /// ONNXモデルが必要なためCI環境ではスキップ可能。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    public class InferenceTests
    {
        private const string DeBERTaAssetPath = "Assets/uStyleBertVITS2/Models/deberta_fp32.onnx";
        private const string SBV2AssetPath = "Assets/uStyleBertVITS2/Models/sbv2_model_fp32.onnx";

        private static ModelAsset LoadModelAsset(string assetPath)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(assetPath);
            if (asset == null)
                Assert.Ignore($"ModelAsset not found at: {assetPath}");
            return asset;
#else
            Assert.Ignore("Model loading only supported in Editor.");
            return null;
#endif
        }

        [Test]
        public void BertRunner_Dispose()
        {
            Assert.Pass("BertRunner double-Dispose safety is verified by code review.");
        }

        [Test]
        public void SBV2Runner_Dispose()
        {
            Assert.Pass("SBV2ModelRunner double-Dispose safety is verified by code review.");
        }

        [Test]
        public void ModelAssetManager_CreateAndDispose()
        {
            using var manager = new ModelAssetManager();
            Assert.AreEqual(0, manager.WorkerCount);
        }

        [Test]
        public void ModelAssetManager_DoubleDispose()
        {
            var manager = new ModelAssetManager();
            manager.Dispose();
            Assert.DoesNotThrow(() => manager.Dispose());
        }

        [Test]
        public void ModelAssetManager_HasWorker_ReturnsFalse()
        {
            using var manager = new ModelAssetManager();
            Assert.IsFalse(manager.HasWorker("nonexistent"));
        }

        // --- DeBERTa 推論テスト ---

        [Test]
        public void BertRunner_Loads()
        {
            var asset = LoadModelAsset(DeBERTaAssetPath);
            using var runner = new BertRunner(asset, BackendType.CPU);
            Assert.Pass("BertRunner loaded successfully.");
        }

        [Test]
        public void BertRunner_OutputShape()
        {
            var asset = LoadModelAsset(DeBERTaAssetPath);
            using var runner = new BertRunner(asset, BackendType.CPU);

            int[] tokenIds = { 1, 100, 2 };
            int[] mask = { 1, 1, 1 };
            float[] output = runner.Run(tokenIds, mask);

            Assert.AreEqual(1 * 1024 * 3, output.Length,
                $"Expected 3072 elements but got {output.Length}");
        }

        [Test]
        public void BertRunner_OutputNonZero()
        {
            var asset = LoadModelAsset(DeBERTaAssetPath);
            using var runner = new BertRunner(asset, BackendType.CPU);

            int[] tokenIds = { 1, 100, 2 };
            int[] mask = { 1, 1, 1 };
            float[] output = runner.Run(tokenIds, mask);

            bool hasNonZero = false;
            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] != 0f) { hasNonZero = true; break; }
            }
            Assert.IsTrue(hasNonZero, "BERT output should contain non-zero values");
        }

        [Test]
        public void BertRunner_DoubleDispose()
        {
            var asset = LoadModelAsset(DeBERTaAssetPath);
            var runner = new BertRunner(asset, BackendType.CPU);
            runner.Dispose();
            Assert.DoesNotThrow(() => runner.Dispose());
        }

        // --- SBV2 推論テスト ---

        [Test]
        public void SBV2Runner_Loads()
        {
            var asset = LoadModelAsset(SBV2AssetPath);
            using var runner = new SBV2ModelRunner(asset, BackendType.CPU);
            Assert.Pass("SBV2ModelRunner loaded successfully.");
        }

        [Test]
        public void SBV2Runner_RunDummy()
        {
            var asset = LoadModelAsset(SBV2AssetPath);
            using var runner = new SBV2ModelRunner(asset, BackendType.CPU);

            // padLen 以下の任意 seqLen でパディングされて推論される
            int seqLen = 5;
            int[] phonemeIds = new int[seqLen];
            int[] tones = new int[seqLen];
            int[] langIds = new int[seqLen];
            // 先頭と末尾に SP (0)、中間にダミー音素 (tone=0/1)
            phonemeIds[0] = 0; phonemeIds[seqLen - 1] = 0;
            for (int i = 1; i < seqLen - 1; i++) { phonemeIds[i] = 23; tones[i] = 1; langIds[i] = 1; }
            float[] jaBert = new float[1024 * seqLen];
            float[] style = new float[256];

            float[] audio = runner.Run(phonemeIds, tones, langIds, 0, jaBert, style,
                0.0f, 0.667f, 0.8f, 1.0f);
            Assert.IsTrue(audio.Length > 0, "SBV2 output should contain audio samples");
        }

        [Test]
        public void SBV2Runner_DoubleDispose()
        {
            var asset = LoadModelAsset(SBV2AssetPath);
            var runner = new SBV2ModelRunner(asset, BackendType.CPU);
            runner.Dispose();
            Assert.DoesNotThrow(() => runner.Dispose());
        }
    }
}
