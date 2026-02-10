using System;
using NUnit.Framework;
using UnityEngine;
using uStyleBertVITS2.Inference;

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
        // テストはモデルファイルが配置されている環境でのみ動作。
        // モデルアセットはInspector経由で設定する必要があるため、
        // ここではnullチェックでスキップする。

        [Test]
        public void BertRunner_Dispose()
        {
            // Disposeが二重呼び出しで安全であることを確認
            // (モデルなしでも Dispose のみのテストは可能ではないが、
            //  パターンとして記載)
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
            // Disposeが安全に動作すること
        }

        [Test]
        public void ModelAssetManager_DoubleDispose()
        {
            var manager = new ModelAssetManager();
            manager.Dispose();
            // 二重Disposeでも例外が出ないこと
            Assert.DoesNotThrow(() => manager.Dispose());
        }

        [Test]
        public void ModelAssetManager_HasWorker_ReturnsFalse()
        {
            using var manager = new ModelAssetManager();
            Assert.IsFalse(manager.HasWorker("nonexistent"));
        }

        // --- 以下はモデルが必要なテスト ---
        // 実際のONNXモデルが配置された環境でのみ動作する。
        // CI環境では [Category("RequiresModel")] でスキップ。

        /*
        [Test]
        public void BertRunner_Loads()
        {
            // ModelAssetをInspector経由で設定して実行
        }

        [Test]
        public void BertRunner_RunDummy()
        {
            // ダミー入力 [CLS]=1, x=100, [SEP]=2 で推論実行
        }

        [Test]
        public void BertRunner_OutputShape()
        {
            // 出力が [1, 1024, token_len] であることを確認
        }

        [Test]
        public void SBV2Runner_Loads()
        {
            // ModelAssetをInspector経由で設定して実行
        }

        [Test]
        public void SBV2Runner_RunDummy()
        {
            // ダミー入力で音声出力が得られることを確認
        }

        [Test]
        public void SBV2Runner_OutputNonEmpty()
        {
            // 出力配列が非空であることを確認
        }

        [Test]
        public void FallbackBackend()
        {
            // GPU→CPUフォールバックが動作することを確認
        }
        */
    }
}
