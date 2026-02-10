using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;

namespace uStyleBertVITS2.Services
{
    /// <summary>
    /// UniTask Channel ベースの TTS リクエストキュー。
    /// 複数のリクエストを順次処理し、音声再生する。
    /// </summary>
    public class TTSRequestQueue : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;

        private Channel<TTSRequest> _channel;
        private ITTSPipeline _pipeline;
        private CancellationTokenSource _cts;

        /// <summary>
        /// パイプラインとAudioSourceを設定して処理を開始する。
        /// </summary>
        public void Initialize(ITTSPipeline pipeline, AudioSource audioSource = null)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            if (audioSource != null) _audioSource = audioSource;

            _channel = Channel.CreateSingleConsumerUnbounded<TTSRequest>();
            _cts = new CancellationTokenSource();

            ProcessQueueAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// リクエストをキューに追加する。
        /// </summary>
        public void Enqueue(TTSRequest request)
        {
            _channel?.Writer.TryWrite(request);
        }

        /// <summary>
        /// 現在のキューをクリアする。
        /// </summary>
        public void ClearQueue()
        {
            // 古いチャネルを破棄して新規作成
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _channel = Channel.CreateSingleConsumerUnbounded<TTSRequest>();
            ProcessQueueAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid ProcessQueueAsync(CancellationToken ct)
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    var clip = _pipeline.Synthesize(request);

                    if (clip != null && _audioSource != null)
                    {
                        _audioSource.clip = clip;
                        _audioSource.Play();

                        // 再生完了まで待機
                        await UniTask.WaitWhile(() => _audioSource.isPlaying,
                            cancellationToken: ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TTSRequestQueue] Synthesis failed: {e.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
