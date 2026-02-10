using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using uStyleBertVITS2.Data;
using uStyleBertVITS2.Inference;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Services
{
    /// <summary>
    /// TTS推論パイプラインオーケストレータ。
    /// G2P → Tokenize → BERT推論 → word2phアライメント → StyleVector → TTS推論 → AudioClip
    /// </summary>
    public class TTSPipeline : ITTSPipeline
    {
        private static readonly ProfilerMarker s_G2P = new("TTS.G2P");
        private static readonly ProfilerMarker s_Tokenize = new("TTS.Tokenize");
        private static readonly ProfilerMarker s_BertInfer = new("TTS.BERT.Inference");
        private static readonly ProfilerMarker s_BertAlign = new("TTS.BERT.Alignment");
        private static readonly ProfilerMarker s_TTSInfer = new("TTS.SynthesizerTrn");
        private static readonly ProfilerMarker s_Audio = new("TTS.AudioClip");

        private readonly IG2P _g2p;
        private readonly SBV2Tokenizer _tokenizer;
        private readonly BertRunner _bert;
        private readonly SBV2ModelRunner _tts;
        private readonly StyleVectorProvider _styleProvider;
        private readonly int _sampleRate;
        private readonly float _normalizationPeak;
        private bool _disposed;

        public TTSPipeline(
            IG2P g2p,
            SBV2Tokenizer tokenizer,
            BertRunner bert,
            SBV2ModelRunner tts,
            StyleVectorProvider styleProvider,
            int sampleRate = 44100,
            float normalizationPeak = 0.95f)
        {
            _g2p = g2p ?? throw new ArgumentNullException(nameof(g2p));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _bert = bert ?? throw new ArgumentNullException(nameof(bert));
            _tts = tts ?? throw new ArgumentNullException(nameof(tts));
            _styleProvider = styleProvider ?? throw new ArgumentNullException(nameof(styleProvider));
            _sampleRate = sampleRate;
            _normalizationPeak = normalizationPeak;
        }

        public AudioClip Synthesize(TTSRequest request)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TTSPipeline));

            // Stage 1: G2P
            G2PResult g2p;
            using (s_G2P.Auto())
            {
                g2p = _g2p.Process(request.Text);
            }

            // Stage 2: DeBERTa トークナイズ
            int[] tokenIds;
            int[] attentionMask;
            using (s_Tokenize.Auto())
            {
                (tokenIds, attentionMask) = _tokenizer.Encode(request.Text);
            }

            // Stage 3: BERT推論
            float[] bertData;
            using (s_BertInfer.Auto())
            {
                bertData = _bert.Run(tokenIds, attentionMask);
            }

            // Stage 4: word2phアライメント
            float[] alignedBert;
            using (s_BertAlign.Auto())
            {
                alignedBert = BertAligner.AlignBertToPhonemes(
                    bertData, tokenIds.Length, g2p.Word2Ph, g2p.PhonemeIds.Length);
            }

            // Stage 5: スタイルベクトル取得
            float[] styleVec = _styleProvider.GetVector(request.StyleId, request.StyleWeight);

            // Stage 6: TTS推論
            float[] audioSamples;
            using (s_TTSInfer.Auto())
            {
                audioSamples = _tts.Run(
                    g2p.PhonemeIds,
                    g2p.Tones,
                    g2p.LanguageIds,
                    request.SpeakerId,
                    alignedBert,
                    styleVec,
                    request.SdpRatio,
                    request.NoiseScale,
                    request.NoiseScaleW,
                    request.LengthScale);
            }

            // Stage 7: AudioClip生成
            AudioClip clip;
            using (s_Audio.Auto())
            {
                NormalizeSamples(audioSamples, _normalizationPeak);
                clip = AudioClip.Create("TTS", audioSamples.Length, 1, _sampleRate, false);
                clip.SetData(audioSamples, 0);
            }

            return clip;
        }

        public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TTSPipeline));

            // --- ThreadPool: CPU処理 (G2P + Tokenize) ---
            await UniTask.SwitchToThreadPool();

            G2PResult g2p;
            using (s_G2P.Auto())
            {
                g2p = _g2p.Process(request.Text);
            }

            ct.ThrowIfCancellationRequested();

            int[] tokenIds;
            int[] attentionMask;
            using (s_Tokenize.Auto())
            {
                (tokenIds, attentionMask) = _tokenizer.Encode(request.Text);
            }

            ct.ThrowIfCancellationRequested();

            // --- MainThread: BERT推論 (Sentis) ---
            await UniTask.SwitchToMainThread(ct);

            float[] bertData;
            using (s_BertInfer.Auto())
            {
                bertData = _bert.Run(tokenIds, attentionMask);
            }

            ct.ThrowIfCancellationRequested();

            // --- ThreadPool: BertAlignment + StyleVector ---
            await UniTask.SwitchToThreadPool();

            float[] alignedBert;
            using (s_BertAlign.Auto())
            {
                alignedBert = BertAligner.AlignBertToPhonemes(
                    bertData, tokenIds.Length, g2p.Word2Ph, g2p.PhonemeIds.Length);
            }

            float[] styleVec = _styleProvider.GetVector(request.StyleId, request.StyleWeight);

            ct.ThrowIfCancellationRequested();

            // --- MainThread: TTS推論 + AudioClip生成 ---
            await UniTask.SwitchToMainThread(ct);

            float[] audioSamples;
            using (s_TTSInfer.Auto())
            {
                audioSamples = _tts.Run(
                    g2p.PhonemeIds,
                    g2p.Tones,
                    g2p.LanguageIds,
                    request.SpeakerId,
                    alignedBert,
                    styleVec,
                    request.SdpRatio,
                    request.NoiseScale,
                    request.NoiseScaleW,
                    request.LengthScale);
            }

            ct.ThrowIfCancellationRequested();

            AudioClip clip;
            using (s_Audio.Auto())
            {
                NormalizeSamples(audioSamples, _normalizationPeak);
                clip = AudioClip.Create("TTS", audioSamples.Length, 1, _sampleRate, false);
                clip.SetData(audioSamples, 0);
            }

            return clip;
        }

        /// <summary>
        /// 音声サンプルを正規化する。
        /// </summary>
        private static void NormalizeSamples(float[] samples, float targetPeak)
        {
            float maxAbs = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = samples[i] < 0 ? -samples[i] : samples[i];
                if (abs > maxAbs) maxAbs = abs;
            }

            if (maxAbs > 0f)
            {
                float scale = targetPeak / maxAbs;
                for (int i = 0; i < samples.Length; i++)
                    samples[i] *= scale;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _g2p?.Dispose();
                _bert?.Dispose();
                _tts?.Dispose();
                _disposed = true;
            }
        }
    }
}
