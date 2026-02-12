using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using uStyleBertVITS2.Configuration;
using uStyleBertVITS2.Diagnostics;
using uStyleBertVITS2.Inference;
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
        [SerializeField] private Button _benchmarkButton;
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
        private bool _isBenchmarking;

        private void Awake()
        {
            if (_synthesizeButton != null)
                _synthesizeButton.onClick.AddListener(OnSynthesizeClicked);
            if (_benchmarkButton != null)
                _benchmarkButton.onClick.AddListener(OnBenchmarkClicked);
        }

        [Header("Benchmark")]
        [SerializeField] private bool _autoRunBenchmark;

        private void Start()
        {
            TTSDebugLog.Enabled = true;
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

            // --benchmark コマンドライン引数 or Inspector フラグで自動実行
            if (_autoRunBenchmark || Array.Exists(Environment.GetCommandLineArgs(), a => a == "--benchmark"))
            {
                RunBenchmarkAndQuitAsync().Forget();
            }
        }

        private async UniTaskVoid RunBenchmarkAndQuitAsync()
        {
            // 初期化完了を1フレーム待つ
            await UniTask.Yield();
            await RunBenchmarkAsync();

            // ログフラッシュを待ってから終了
            #if !UNITY_EDITOR
            Debug.Log("[Benchmark] Quitting application.");
            await UniTask.DelayFrame(5);
            Application.Quit();
            #endif
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

        private void OnBenchmarkClicked()
        {
            if (_isBenchmarking) return;
            RunBenchmarkAsync().Forget();
        }


        private async UniTask RunBenchmarkAsync()
        {
            _isBenchmarking = true;
            if (_benchmarkButton != null) _benchmarkButton.interactable = false;
            if (_synthesizeButton != null) _synthesizeButton.interactable = false;
            SetStatus("Benchmark running...");

            try
            {
                var backends = new List<BertBenchmark.BackendResult>();

                // --- Sentis CPU ---
                SetStatus("Benchmark: Sentis CPU...");
                await UniTask.Yield(); // UI更新のためフレーム待ち

                if (_settings.BertModel != null)
                {
                    try
                    {
                        using var sentisRunner = new BertRunner(_settings.BertModel, BackendType.CPU);
                        var sentisResults = BertBenchmark.RunAllSizes(sentisRunner);
                        backends.Add(new BertBenchmark.BackendResult
                        {
                            BackendName = "Sentis CPU",
                            Sizes = sentisResults,
                        });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Benchmark] Sentis CPU failed: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning("[Benchmark] Sentis skipped — BertModel not assigned");
                }

                // --- ORT DirectML + CPU ---
                SetStatus("Benchmark: ORT...");
                await UniTask.Yield();

                var ortPath = System.IO.Path.Combine(Application.streamingAssetsPath, _settings.OrtBertModelPath);
                BertBenchmark.RunOrtBenchmarks(ortPath, backends);

                string report = BertBenchmark.FormatResults(backends);
                Debug.Log(report);
                SetStatus(backends.Count > 0 ? "Benchmark done — see console" : "No backends available");
            }
            catch (Exception e)
            {
                SetStatus($"Benchmark error: {e.Message}");
                Debug.LogError($"[Benchmark] {e}");
            }
            finally
            {
                _isBenchmarking = false;
                if (_benchmarkButton != null) _benchmarkButton.interactable = true;
                if (_synthesizeButton != null && _isReady) _synthesizeButton.interactable = true;
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
