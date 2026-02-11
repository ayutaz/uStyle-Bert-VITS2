using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace uStyleBertVITS2.TextProcessing
{
    /// <summary>
    /// Burst対応のBERT埋め込み展開ジョブ。
    /// BERT出力 [1024, tokenLen] を word2ph に基づき [1024, phoneSeqLen] に展開する。
    /// 各音素のインデックスを並列処理する。
    /// </summary>
    [BurstCompile]
    public struct BertAlignmentJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> BertFlat;
        [ReadOnly] public NativeArray<int> PhoneToToken;
        [NativeDisableParallelForRestriction] public NativeArray<float> AlignedBert;

        public int TokenLen;
        public int PhoneSeqLen;
        public int EmbDim; // 1024

        public void Execute(int phoneIdx)
        {
            int tokenIdx = PhoneToToken[phoneIdx];
            for (int d = 0; d < EmbDim; d++)
            {
                AlignedBert[d * PhoneSeqLen + phoneIdx] = BertFlat[d * TokenLen + tokenIdx];
            }
        }
    }
}
