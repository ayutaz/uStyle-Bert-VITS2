using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace uStyleBertVITS2.Audio
{
    /// <summary>
    /// Burst対応の音声正規化ジョブ。
    /// スケーリング処理を並列化する（最大値探索はシングルスレッドで十分高速）。
    /// </summary>
    [BurstCompile]
    public struct NormalizeAudioJob : IJobParallelFor
    {
        public NativeArray<float> Samples;
        [ReadOnly] public float Scale;

        public void Execute(int i)
        {
            Samples[i] *= Scale;
        }
    }
}
