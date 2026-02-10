using System;
using UnityEngine;

namespace uStyleBertVITS2.Services
{
    /// <summary>
    /// TTS推論パイプラインの抽象化。
    /// 同期/非同期の実装を切り替え可能にする。
    /// </summary>
    public interface ITTSPipeline : IDisposable
    {
        /// <summary>
        /// テキストから音声を同期合成する。
        /// </summary>
        AudioClip Synthesize(TTSRequest request);
    }
}
