using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uStyleBertVITS2.Services;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// SynthesizeAsync の構造テスト（モック使用、モデル不要）。
    /// </summary>
    [TestFixture]
    public class AsyncPipelineTests
    {
        /// <summary>
        /// テスト用モック ITTSPipeline。
        /// 実際のモデルは使わず、ダミーの AudioClip を返す。
        /// </summary>
        private class MockTTSPipeline : ITTSPipeline
        {
            public int SynthesizeCallCount { get; private set; }
            public int SynthesizeAsyncCallCount { get; private set; }

            public AudioClip Synthesize(TTSRequest request)
            {
                SynthesizeCallCount++;
                return AudioClip.Create("MockSync", 4410, 1, 44100, false);
            }

            public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken ct = default)
            {
                SynthesizeAsyncCallCount++;
                await UniTask.Yield(ct);
                ct.ThrowIfCancellationRequested();
                return AudioClip.Create("MockAsync", 4410, 1, 44100, false);
            }

            public void Dispose() { }
        }

        /// <summary>
        /// 遅延を入れるモック（キャンセルテスト用）。
        /// </summary>
        private class SlowMockTTSPipeline : ITTSPipeline
        {
            public AudioClip Synthesize(TTSRequest request)
            {
                return AudioClip.Create("MockSync", 4410, 1, 44100, false);
            }

            public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken ct = default)
            {
                // 長い遅延 — キャンセルされることを期待
                await UniTask.Delay(5000, cancellationToken: ct);
                return AudioClip.Create("MockAsync", 4410, 1, 44100, false);
            }

            public void Dispose() { }
        }

        [UnityTest]
        public System.Collections.IEnumerator SynthesizeAsync_ReturnsAudioClip() => UniTask.ToCoroutine(async () =>
        {
            var pipeline = new MockTTSPipeline();
            var request = new TTSRequest("テスト");

            var clip = await pipeline.SynthesizeAsync(request);

            Assert.IsNotNull(clip);
            Assert.AreEqual("MockAsync", clip.name);
            Assert.AreEqual(1, pipeline.SynthesizeAsyncCallCount);
            Assert.AreEqual(0, pipeline.SynthesizeCallCount);

            pipeline.Dispose();
        });

        [UnityTest]
        public System.Collections.IEnumerator SynthesizeAsync_CancellationThrows() => UniTask.ToCoroutine(async () =>
        {
            var pipeline = new SlowMockTTSPipeline();
            var request = new TTSRequest("テスト");
            var cts = new CancellationTokenSource();

            // すぐキャンセル
            cts.CancelAfter(50);

            bool cancelled = false;
            try
            {
                await pipeline.SynthesizeAsync(request, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled, "SynthesizeAsync should throw OperationCanceledException on cancellation");

            cts.Dispose();
            pipeline.Dispose();
        });

        [UnityTest]
        public System.Collections.IEnumerator SynthesizeAsync_MultipleCallsSucceed() => UniTask.ToCoroutine(async () =>
        {
            var pipeline = new MockTTSPipeline();

            var clip1 = await pipeline.SynthesizeAsync(new TTSRequest("テスト1"));
            var clip2 = await pipeline.SynthesizeAsync(new TTSRequest("テスト2"));

            Assert.IsNotNull(clip1);
            Assert.IsNotNull(clip2);
            Assert.AreEqual(2, pipeline.SynthesizeAsyncCallCount);

            pipeline.Dispose();
        });
    }
}
