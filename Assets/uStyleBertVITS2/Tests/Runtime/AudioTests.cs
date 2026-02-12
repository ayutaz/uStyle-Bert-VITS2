using NUnit.Framework;
using UnityEngine;
using uStyleBertVITS2.Audio;
using uStyleBertVITS2.Services;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class AudioTests
    {
        [Test]
        public void CreateClipFromSamples()
        {
            float[] samples = new float[4410]; // 0.1秒分
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / 44100f) * 0.5f;

            var clip = TTSAudioUtility.CreateClip(samples, 44100);
            Assert.IsNotNull(clip);
            Assert.AreEqual(4410, clip.samples);

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void ClipSampleRateCorrect()
        {
            float[] samples = new float[1000];
            var clip = TTSAudioUtility.CreateClip(samples, 44100);

            Assert.AreEqual(44100, clip.frequency);

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void NormalizationPreventClipping()
        {
            // 大きな値を含むサンプル
            float[] samples = { -2.0f, 0.5f, 3.0f, -1.5f, 0.0f };
            TTSAudioUtility.NormalizeSamples(samples, 0.95f);

            // 全サンプルが [-1, 1] 範囲に収まること
            Assert.IsTrue(TTSAudioUtility.ValidateSampleRange(samples),
                "正規化後は全サンプルが[-1,1]範囲内");

            // 最大値がtargetPeak付近であること
            float maxAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = samples[i] < 0f ? -samples[i] : samples[i];
                if (abs > maxAbs) maxAbs = abs;
            }
            Assert.AreEqual(0.95f, maxAbs, 1e-5f);
        }

        [Test]
        public void EmptySamplesHandled()
        {
            var clip = TTSAudioUtility.CreateClip(new float[0]);
            Assert.IsNull(clip, "空配列はnullを返すべき");

            clip = TTSAudioUtility.CreateClip(null);
            Assert.IsNull(clip, "nullはnullを返すべき");
        }

        [Test]
        public void NormalizeSamplesBurst_MatchesScalar()
        {
            // Short array (scalar fallback path)
            float[] shortSamples = { -2.0f, 0.5f, 3.0f, -1.5f, 0.0f };
            float[] shortCopy = (float[])shortSamples.Clone();
            TTSAudioUtility.NormalizeSamples(shortSamples, 0.95f);
            TTSAudioUtility.NormalizeSamplesBurst(shortCopy, 0.95f);

            Assert.AreEqual(shortSamples.Length, shortCopy.Length);
            for (int i = 0; i < shortSamples.Length; i++)
                Assert.AreEqual(shortSamples[i], shortCopy[i], 1e-6f, $"short[{i}] mismatch");

            // Long array (Burst path, >= 4096)
            int longLen = 8192;
            float[] longSamples = new float[longLen];
            for (int i = 0; i < longLen; i++)
                longSamples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / 44100f) * 2.5f;
            float[] longCopy = (float[])longSamples.Clone();
            TTSAudioUtility.NormalizeSamples(longSamples, 0.95f);
            TTSAudioUtility.NormalizeSamplesBurst(longCopy, 0.95f);

            for (int i = 0; i < longLen; i++)
                Assert.AreEqual(longSamples[i], longCopy[i], 1e-5f, $"long[{i}] mismatch");
        }

        [Test]
        public void NormalizeSamplesBurst_EmptyAndNull_NoThrow()
        {
            Assert.DoesNotThrow(() => TTSAudioUtility.NormalizeSamplesBurst(null));
            Assert.DoesNotThrow(() => TTSAudioUtility.NormalizeSamplesBurst(new float[0]));
        }

        [Test]
        public void LargeSamplesHandled()
        {
            // 10秒分 (441000サンプル)
            int sampleCount = 44100 * 10;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / 44100f) * 0.8f;

            var clip = TTSAudioUtility.CreateClip(samples, 44100);
            Assert.IsNotNull(clip);
            Assert.AreEqual(sampleCount, clip.samples);
            Assert.AreEqual(1, clip.channels);

            Object.DestroyImmediate(clip);
        }

        // --- GetTrimmedLength テスト ---

        [Test]
        public void GetTrimmedLength_ShortArray_ReturnsFullLength()
        {
            float[] samples = new float[256]; // < blockSize(512)
            Assert.AreEqual(256, TTSPipeline.GetTrimmedLength(samples));
        }

        [Test]
        public void GetTrimmedLength_NoSilence_ReturnsFullLength()
        {
            float[] samples = new float[1024]; // 2 blocks
            samples[800] = 0.1f; // 最終ブロックに信号
            Assert.AreEqual(1024, TTSPipeline.GetTrimmedLength(samples));
        }

        [Test]
        public void GetTrimmedLength_TrailingSilence_Trimmed()
        {
            float[] samples = new float[2048]; // 4 blocks
            samples[600] = 0.5f; // block 1 に信号 (blocks 2,3 は無音)
            Assert.AreEqual(1024, TTSPipeline.GetTrimmedLength(samples)); // blocks 0-1 保持
        }

        [Test]
        public void GetTrimmedLength_AllSilence_ReturnsOneBlock()
        {
            float[] samples = new float[2048]; // 4 blocks, 全てゼロ
            Assert.AreEqual(512, TTSPipeline.GetTrimmedLength(samples)); // 最低1ブロック
        }
    }
}
