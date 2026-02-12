#if USBV2_ORT_AVAILABLE
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using uStyleBertVITS2.Inference;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// OnnxRuntimeBertRunner テスト。
    /// ONNX Runtime パッケージ導入時のみコンパイルされる。
    /// </summary>
    [Category("RequiresModel")]
    public class OnnxRuntimeBertRunnerTests
    {
        private string _modelPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _modelPath = Path.Combine(Application.streamingAssetsPath, "uStyleBertVITS2/Models/deberta_for_ort.onnx");
            if (!File.Exists(_modelPath))
                Assert.Ignore($"ORT BERT model not found: {_modelPath}");
        }

        [Test]
        public void Constructor_CreatesSession()
        {
            using var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);
            Assert.That(runner.HiddenSize, Is.EqualTo(1024));
        }

        [Test]
        public void Run_OutputShape()
        {
            using var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);

            int[] tokenIds = { 1, 100, 200, 2 }; // [CLS] x y [SEP]
            int[] mask = { 1, 1, 1, 1 };
            float[] result = runner.Run(tokenIds, mask);

            Assert.That(result.Length, Is.EqualTo(1024 * tokenIds.Length));
        }

        [Test]
        public void Run_DestOverload_MatchesAlloc()
        {
            using var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);

            int[] tokenIds = { 1, 100, 2 };
            int[] mask = { 1, 1, 1 };

            float[] alloc = runner.Run(tokenIds, mask);
            float[] dest = new float[1024 * tokenIds.Length];
            runner.Run(tokenIds, mask, dest);

            Assert.That(dest, Is.EqualTo(alloc).Within(1e-6f));
        }

        [Test]
        public void Run_OutputNonZero()
        {
            using var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);

            int[] tokenIds = { 1, 100, 200, 2 };
            int[] mask = { 1, 1, 1, 1 };
            float[] result = runner.Run(tokenIds, mask);

            bool hasNonZero = false;
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != 0f)
                {
                    hasNonZero = true;
                    break;
                }
            }
            Assert.That(hasNonZero, Is.True, "BERT output should contain non-zero values.");
        }

        [Test]
        public void DoubleDispose_DoesNotThrow()
        {
            var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);
            runner.Dispose();
            Assert.DoesNotThrow(() => runner.Dispose());
        }

        [Test]
        public void Run_AfterDispose_Throws()
        {
            var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);
            runner.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => runner.Run(new[] { 1, 2 }, new[] { 1, 1 }));
        }

        [Test]
        public void Run_DestOverload_ThrowsOnNull()
        {
            using var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);
            Assert.Throws<ArgumentNullException>(
                () => runner.Run(new[] { 1, 2 }, new[] { 1, 1 }, null));
        }

        [Test]
        public void Run_DestOverload_ThrowsOnSmallBuffer()
        {
            using var runner = new OnnxRuntimeBertRunner(_modelPath, useDirectML: false);
            Assert.Throws<ArgumentException>(
                () => runner.Run(new[] { 1, 2, 3 }, new[] { 1, 1, 1 }, new float[10]));
        }
    }
}
#endif
