using UnityEngine;

namespace uStyleBertVITS2.Audio
{
    /// <summary>
    /// float32 PCM配列からAudioClipを生成するユーティリティ。
    /// SBV2出力は44100Hz monoのfloat32 PCM。
    /// </summary>
    public static class TTSAudioUtility
    {
        /// <summary>
        /// float32 PCM配列からAudioClipを生成する。
        /// </summary>
        /// <param name="samples">PCMサンプル配列 (float32)</param>
        /// <param name="sampleRate">サンプルレート (default: 44100Hz)</param>
        /// <param name="name">AudioClipの名前</param>
        /// <param name="normalize">正規化を行うかどうか</param>
        /// <param name="targetPeak">正規化のターゲットピーク (0.0-1.0)</param>
        public static AudioClip CreateClip(
            float[] samples,
            int sampleRate = 44100,
            string name = "TTS",
            bool normalize = true,
            float targetPeak = 0.95f)
        {
            if (samples == null || samples.Length == 0)
                return null;

            // 正規化
            if (normalize)
                NormalizeSamples(samples, targetPeak);

            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 音声サンプルをピーク正規化する。
        /// 全サンプルが [-targetPeak, targetPeak] の範囲に収まるようスケーリング。
        /// </summary>
        public static void NormalizeSamples(float[] samples, float targetPeak = 0.95f)
        {
            if (samples == null || samples.Length == 0) return;

            float maxAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = samples[i] < 0f ? -samples[i] : samples[i];
                if (abs > maxAbs) maxAbs = abs;
            }

            if (maxAbs <= 0f) return;

            float scale = targetPeak / maxAbs;
            for (int i = 0; i < samples.Length; i++)
                samples[i] *= scale;
        }

        /// <summary>
        /// 全サンプルが [-1, 1] の範囲内かどうかを検証する。
        /// </summary>
        public static bool ValidateSampleRange(float[] samples)
        {
            if (samples == null) return false;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] < -1f || samples[i] > 1f)
                    return false;
            }
            return true;
        }
    }
}
