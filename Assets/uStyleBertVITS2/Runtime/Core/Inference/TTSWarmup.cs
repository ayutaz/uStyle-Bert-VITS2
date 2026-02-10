using System.Collections;
using UnityEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// ウォームアップ推論。
    /// 初回のシェーダコンパイルをユーザー操作前に完了させて、
    /// 初回発話のレイテンシを削減する。
    /// </summary>
    public static class TTSWarmup
    {
        /// <summary>
        /// ダミーデータで推論パイプラインを事前ウォームアップする。
        /// ロード画面やスプラッシュ画面中に実行すること。
        /// </summary>
        public static IEnumerator WarmupAll(BertRunner bert, SBV2ModelRunner tts)
        {
            // BERT ウォームアップ（最小入力: [CLS] x [SEP]）
            int[] dummyTokens = { 1, 100, 2 };
            int[] dummyMask = { 1, 1, 1 };

            try
            {
                bert.Run(dummyTokens, dummyMask);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"BERT warmup failed: {e.Message}");
            }

            yield return null;

            // TTS ウォームアップ（最小入力）
            int dummySeqLen = 3;
            int[] dummyPhonemes = { 0, 1, 2 };
            int[] dummyTones = { 0, 0, 0 };
            int[] dummyLangs = { 1, 1, 1 };
            float[] dummyBert = new float[1024 * dummySeqLen];
            float[] dummyStyle = new float[256];

            try
            {
                tts.Run(dummyPhonemes, dummyTones, dummyLangs,
                    0, dummyBert, dummyStyle,
                    0.2f, 0.6f, 0.8f, 1.0f);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"TTS warmup failed: {e.Message}");
            }

            yield return null;

            Debug.Log("[uStyleBertVITS2] Warmup complete.");
        }
    }
}
