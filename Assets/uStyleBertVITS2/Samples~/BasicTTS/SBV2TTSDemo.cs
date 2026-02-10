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
        private bool _isReady;

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
            if (!_isReady)
            {
                SetStatus("Not ready yet...");
                return;
            }

            string text = _inputField != null ? _inputField.text : "こんにちは";
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("Please enter text.");
                return;
            }

            SetStatus("Synthesizing...");

            try
            {
                var request = new TTSRequest(
                    text: text,
                    speakerId: _speakerId,
                    styleId: _styleId,
                    sdpRatio: _sdpRatio,
                    lengthScale: _speed);

                var clip = _pipeline.Synthesize(request);

                if (clip != null && _audioSource != null)
                {
                    _audioSource.clip = clip;
                    _audioSource.Play();
                    SetStatus($"Playing ({clip.length:F1}s)");
                }
                else
                {
                    SetStatus("No audio generated.");
                }
            }
            catch (System.Exception e)
            {
                SetStatus($"Error: {e.Message}");
                Debug.LogError($"TTS synthesis failed: {e}");
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
        }

        private void OnDestroy()
        {
            _pipeline?.Dispose();
        }
    }
}
