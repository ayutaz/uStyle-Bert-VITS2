using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using uStyleBertVITS2.Configuration;
using uStyleBertVITS2.Services;

namespace uStyleBertVITS2.Samples
{
    /// <summary>
    /// Style-Bert-VITS2 TTS 最小デモ。
    /// InputFieldにテキストを入力し、ボタンで音声合成・再生する。
    /// </summary>
    public class SBV2TTSDemo : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private TTSSettings _settings;

        [Header("UI")]
        [SerializeField] private InputField _inputField;
        [SerializeField] private Button _synthesizeButton;
        [SerializeField] private Text _statusText;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Parameters")]
        [SerializeField] private int _speakerId;
        [SerializeField] private int _styleId;
        [SerializeField, Range(0.5f, 2f)] private float _speed = 1.0f;
        [SerializeField, Range(0f, 1f)] private float _sdpRatio = 0.2f;

        private ITTSPipeline _pipeline;
        private CancellationTokenSource _cts;
        private bool _isReady;
        private bool _isSynthesizing;

        private void Awake()
        {
            if (_synthesizeButton != null)
                _synthesizeButton.onClick.AddListener(OnSynthesizeClicked);
        }

        private void Start()
        {
            SetStatus("Initializing...");

            try
            {
                _pipeline = new TTSPipelineBuilder()
                    .WithSettings(_settings)
                    .Build();
                _isReady = true;
                SetStatus("Ready");
            }
            catch (System.Exception e)
            {
                SetStatus($"Error: {e.Message}");
                Debug.LogError($"TTS initialization failed: {e}");
            }
        }

        private void OnSynthesizeClicked()
        {
            if (!_isReady || _isSynthesizing)
            {
                if (!_isReady) SetStatus("Not ready yet...");
                return;
            }

            SynthesizeAsync().Forget();
        }

        private async UniTaskVoid SynthesizeAsync()
        {
            string text = _inputField != null ? _inputField.text : "こんにちは";
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("Please enter text.");
                return;
            }

            _isSynthesizing = true;
            if (_synthesizeButton != null) _synthesizeButton.interactable = false;
            SetStatus("合成中...");

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                var request = new TTSRequest(
                    text: text,
                    speakerId: _speakerId,
                    styleId: _styleId,
                    sdpRatio: _sdpRatio,
                    lengthScale: _speed);

                var clip = await _pipeline.SynthesizeAsync(request, _cts.Token);

                if (clip != null && _audioSource != null)
                {
                    _audioSource.clip = clip;
                    _audioSource.Play();
                    SetStatus($"再生中 ({clip.length:F1}s)");
                }
                else
                {
                    SetStatus("音声が生成されませんでした。");
                }
            }
            catch (System.OperationCanceledException)
            {
                SetStatus("キャンセルされました。");
            }
            catch (System.Exception e)
            {
                SetStatus($"Error: {e.Message}");
                Debug.LogError($"TTS synthesis failed: {e}");
            }
            finally
            {
                _isSynthesizing = false;
                if (_synthesizeButton != null) _synthesizeButton.interactable = true;
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _pipeline?.Dispose();
        }
    }
}
