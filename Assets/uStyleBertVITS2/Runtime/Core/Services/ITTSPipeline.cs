using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        /// <summary>
        /// テキストから音声を非同期合成する。
        /// CPU処理はスレッドプールで、Sentis推論とAudioClip生成はメインスレッドで実行する。
        /// </summary>
        UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken ct = default);
    }
}
