using UnityEngine;

namespace uStyleBertVITS2.Audio
{
    /// <summary>
    /// Sentis Tensor 出力から AudioClip を生成する。
    /// SBV2の出力テンソル [1, 1, audio_samples] を AudioClip に変換。
    /// </summary>
    public static class AudioClipGenerator
    {
        /// <summary>
        /// SBV2出力 (flatten済みfloat[]) からAudioClipを生成する。
        /// SBV2の出力shape: [1, 1, audio_samples]
        /// </summary>
        /// <param name="flatOutput">SBV2出力テンソルを flatten した配列</param>
        /// <param name="sampleRate">サンプルレート (default: 44100)</param>
        /// <param name="normalize">正規化を行うかどうか</param>
        public static AudioClip FromModelOutput(
            float[] flatOutput,
            int sampleRate = 44100,
            bool normalize = true)
        {
            if (flatOutput == null || flatOutput.Length == 0)
                return null;

            // SBV2出力は [1, 1, audio_samples] なので flatten 結果がそのままPCMデータ
            return TTSAudioUtility.CreateClip(flatOutput, sampleRate, "TTS_Output", normalize);
        }

        /// <summary>
        /// AudioClipの再生時間（秒）を計算する。
        /// </summary>
        public static float GetDurationSeconds(int sampleCount, int sampleRate = 44100)
        {
            return (float)sampleCount / sampleRate;
        }
    }
}
