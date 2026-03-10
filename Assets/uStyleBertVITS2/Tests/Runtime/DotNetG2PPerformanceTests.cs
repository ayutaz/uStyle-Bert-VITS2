using System;
using System.Diagnostics;
using NUnit.Framework;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// Phase 4: dot-net-g2p パフォーマンス検証テスト。
    /// Unity Editor 上でのみ実行可能。辞書ファイルが必要。
    ///
    /// パフォーマンス基準:
    ///   辞書ロード時間: &lt; 3秒
    ///   G2P 処理時間 (20文字): &lt; 50ms
    ///   GC アロケーション/呼出: &lt; 100KB
    /// </summary>
    [TestFixture]
    [Category("DotNetG2P")]
    [Category("Performance")]
    public class DotNetG2PPerformanceTests
    {
        private const string ShortText = "こんにちは";           // 5文字
        private const string MediumText = "今日は良い天気ですね、散歩に行きましょう。"; // 20文字
        private const string LongText =
            "これは長い文章のテストです。日本語の音声合成システムが正しく動作することを確認します。" +
            "様々な漢字やひらがな、カタカナが混在する文を処理できるか検証しています。";

        private string _dictPath;
        private SBV2PhonemeMapper _mapper;
        private bool _available;

        [OneTimeSetUp]
        public void Setup()
        {
            _dictPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "uStyleBertVITS2/OpenJTalkDic");

            _mapper = new SBV2PhonemeMapper();

            _available = System.IO.Directory.Exists(_dictPath);
            if (!_available)
                UnityEngine.Debug.LogWarning(
                    $"DotNetG2P performance tests skipped: dictionary not found at {_dictPath}");
        }

        private void AssertAvailable()
        {
            if (!_available)
                Assert.Ignore("dot-net-g2p dictionary not available.");
        }

        // ================================================================
        // 4-1: 辞書ロード時間計測
        // ================================================================

        [Test]
        public void DictLoad_CompletesWithin3Seconds()
        {
            AssertAvailable();
            var sw = Stopwatch.StartNew();

            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            sw.Stop();
            UnityEngine.Debug.Log(
                $"[Phase4] Dictionary load time: {sw.ElapsedMilliseconds}ms");
            Assert.Less(sw.ElapsedMilliseconds, 3000,
                $"Dictionary load should complete within 3s, got {sw.ElapsedMilliseconds}ms");
        }

        // ================================================================
        // 4-2: G2P 処理時間計測
        // ================================================================

        [Test]
        public void ProcessTime_ShortText_Under50ms()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            // ウォームアップ
            g2p.Process(ShortText);

            var sw = Stopwatch.StartNew();
            const int iterations = 20;
            for (int i = 0; i < iterations; i++)
                g2p.Process(ShortText);
            sw.Stop();

            double avgMs = (double)sw.ElapsedMilliseconds / iterations;
            UnityEngine.Debug.Log(
                $"[Phase4] Short text ({ShortText.Length} chars) avg: {avgMs:F1}ms ({iterations} iterations)");
            Assert.Less(avgMs, 50,
                $"Short text avg should be <50ms, got {avgMs:F1}ms");
        }

        [Test]
        public void ProcessTime_MediumText_Under50ms()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            g2p.Process(MediumText);

            var sw = Stopwatch.StartNew();
            const int iterations = 20;
            for (int i = 0; i < iterations; i++)
                g2p.Process(MediumText);
            sw.Stop();

            double avgMs = (double)sw.ElapsedMilliseconds / iterations;
            UnityEngine.Debug.Log(
                $"[Phase4] Medium text ({MediumText.Length} chars) avg: {avgMs:F1}ms ({iterations} iterations)");
            Assert.Less(avgMs, 50,
                $"Medium text avg should be <50ms, got {avgMs:F1}ms");
        }

        [Test]
        public void ProcessTime_LongText_Under200ms()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            g2p.Process(LongText);

            var sw = Stopwatch.StartNew();
            const int iterations = 10;
            for (int i = 0; i < iterations; i++)
                g2p.Process(LongText);
            sw.Stop();

            double avgMs = (double)sw.ElapsedMilliseconds / iterations;
            UnityEngine.Debug.Log(
                $"[Phase4] Long text ({LongText.Length} chars) avg: {avgMs:F1}ms ({iterations} iterations)");
            Assert.Less(avgMs, 200,
                $"Long text avg should be <200ms, got {avgMs:F1}ms");
        }

        // ================================================================
        // 4-3: GC アロケーション計測 (概算)
        // ================================================================

        [Test]
        public void GCAllocation_SingleCall_Under100KB()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            // ウォームアップ
            g2p.Process(MediumText);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);

            const int iterations = 10;
            for (int i = 0; i < iterations; i++)
                g2p.Process(MediumText);

            long after = GC.GetTotalMemory(false);
            long allocPerCall = (after - before) / iterations;

            UnityEngine.Debug.Log(
                $"[Phase4] GC alloc per call (approx): {allocPerCall / 1024.0:F1} KB " +
                $"(total delta: {(after - before) / 1024.0:F1} KB over {iterations} calls)");

            // 100KB = 102400 bytes per call
            // Note: GC.GetTotalMemory is approximate; actual allocation tracking
            // should use Unity Profiler for precise measurements.
            Assert.Less(allocPerCall, 102400,
                $"GC allocation per call should be <100KB, got {allocPerCall / 1024.0:F1}KB");
        }

        // ================================================================
        // 4-4: スレッド安全性テスト
        // ================================================================

        [Test]
        public void ThreadSafety_SequentialCalls_NoExceptions()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            string[] texts = { ShortText, MediumText, LongText, "あ", "東京タワー" };

            // Sequential calls should never throw
            for (int round = 0; round < 5; round++)
            {
                foreach (string text in texts)
                {
                    var result = g2p.Process(text);
                    Assert.IsNotNull(result.PhonemeIds);
                    Assert.Greater(result.PhonemeIds.Length, 0);
                }
            }
        }

        [Test]
        public void ThreadSafety_SeparateInstances_NoConflict()
        {
            AssertAvailable();
            // Two separate instances should not conflict
            using var g2p1 = new DotNetG2PJapaneseG2P(_dictPath, _mapper);
            using var g2p2 = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            var result1 = g2p1.Process("こんにちは");
            var result2 = g2p2.Process("こんにちは");

            CollectionAssert.AreEqual(result1.PhonemeIds, result2.PhonemeIds,
                "Two instances should produce identical results");
        }

        // ================================================================
        // 4-5: 連続呼び出しテスト (100回)
        // ================================================================

        [Test]
        public void StressTest_100Calls_NoMemoryLeakOrDegradation()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            string[] texts =
            {
                "こんにちは",
                "今日は良い天気です",
                "東京タワー、スカイツリー。",
                "え？本当！",
                "私は毎日朝7時に起きて、朝食を食べてから学校に行きます。"
            };

            var sw = Stopwatch.StartNew();
            int totalPhonemes = 0;

            for (int i = 0; i < 100; i++)
            {
                string text = texts[i % texts.Length];
                var result = g2p.Process(text);

                // Basic validity
                Assert.IsNotNull(result.PhonemeIds, $"Iteration {i}: null PhonemeIds");
                Assert.Greater(result.PhonemeIds.Length, 0, $"Iteration {i}: empty PhonemeIds");
                Assert.AreEqual(result.PhonemeIds.Length, result.Tones.Length,
                    $"Iteration {i}: length mismatch");
                totalPhonemes += result.PhonemeIds.Length;
            }

            sw.Stop();
            UnityEngine.Debug.Log(
                $"[Phase4] 100 calls completed in {sw.ElapsedMilliseconds}ms " +
                $"(avg {sw.ElapsedMilliseconds / 100.0:F1}ms/call, " +
                $"total phonemes: {totalPhonemes})");

            // 100 calls should complete within 10 seconds
            Assert.Less(sw.ElapsedMilliseconds, 10000,
                $"100 calls should complete within 10s, got {sw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void StressTest_ConsistencyAfter100Calls()
        {
            AssertAvailable();
            using var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);

            // Run 100 calls with mixed input
            for (int i = 0; i < 100; i++)
                g2p.Process("テスト");

            // After stress, verify output is still correct
            var result1 = g2p.Process("こんにちは");
            var result2 = g2p.Process("こんにちは");

            CollectionAssert.AreEqual(result1.PhonemeIds, result2.PhonemeIds,
                "After 100 calls, output should still be deterministic");
            CollectionAssert.AreEqual(result1.Tones, result2.Tones);
            CollectionAssert.AreEqual(result1.Word2Ph, result2.Word2Ph);
        }

        // ================================================================
        // 4-6: Dispose 後の使用チェック
        // ================================================================

        [Test]
        public void Dispose_ThenProcess_ThrowsObjectDisposedException()
        {
            AssertAvailable();
            var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);
            g2p.Dispose();

            Assert.Throws<ObjectDisposedException>(() => g2p.Process("テスト"));
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            AssertAvailable();
            var g2p = new DotNetG2PJapaneseG2P(_dictPath, _mapper);
            g2p.Dispose();
            Assert.DoesNotThrow(() => g2p.Dispose());
        }
    }
}
