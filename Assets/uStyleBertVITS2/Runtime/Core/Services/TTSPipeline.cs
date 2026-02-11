using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using uStyleBertVITS2.Audio;
using uStyleBertVITS2.Data;
using uStyleBertVITS2.Diagnostics;
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
        private readonly float[] _styleVecBuffer = new float[StyleVectorProvider.VectorDimension];
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

            TTSDebugLog.Log("TTS", $"=== Synthesize: \"{request.Text}\" ===");

            // Stage 1: G2P
            G2PResult g2p;
            using (s_G2P.Auto())
            {
                g2p = _g2p.Process(request.Text);
            }

            TTSDebugLog.Log("TTS.G2P",
                $"PhonemeIds.Length={g2p.PhonemeIds.Length}, " +
                $"Tones range=[{ArrayMin(g2p.Tones)},{ArrayMax(g2p.Tones)}], " +
                $"Word2Ph.Length={g2p.Word2Ph.Length}");

            // Stage 1.5: add_blank — 学習時と同じ前処理
            int[] phonemeIds = PhonemeUtils.Intersperse(g2p.PhonemeIds, 0);
            int[] tones = PhonemeUtils.Intersperse(g2p.Tones, 0);
            int[] languageIds = PhonemeUtils.Intersperse(g2p.LanguageIds, 0);
            int[] word2ph = PhonemeUtils.AdjustWord2PhForBlanks(g2p.Word2Ph);
            int phoneSeqLen = phonemeIds.Length;

            TTSDebugLog.Log("TTS.Blank",
                $"Interleaved: {g2p.PhonemeIds.Length} → {phoneSeqLen} phonemes");

            // Stage 2: DeBERTa トークナイズ
            int[] tokenIds;
            int[] attentionMask;
            using (s_Tokenize.Auto())
            {
                (tokenIds, attentionMask) = _tokenizer.Encode(request.Text);
            }

            TTSDebugLog.Log("TTS.Tokenize", $"tokenIds.Length={tokenIds.Length}");

            // Stage 3: BERT推論
            float[] bertData;
            using (s_BertInfer.Auto())
            {
                bertData = _bert.Run(tokenIds, attentionMask);
            }

            TTSDebugLog.Log("TTS.BERT",
                $"bertData.Length={bertData.Length} ({tokenIds.Length} tokens × 1024 dim)");

            // Stage 4: word2phアライメント (Burst + ArrayPool)
            int alignedLen = BertAligner.EmbeddingDimension * phoneSeqLen;
            float[] alignedBert = System.Buffers.ArrayPool<float>.Shared.Rent(alignedLen);
            try
            {
                using (s_BertAlign.Auto())
                {
                    BertAligner.AlignBertToPhonemesBurst(
                        bertData, tokenIds.Length, word2ph, phoneSeqLen, alignedBert);
                }

                TTSDebugLog.Log("TTS.Align",
                    $"alignedBert effective={alignedLen} ({phoneSeqLen} phonemes × 1024 dim)");

                // Stage 5: スタイルベクトル取得 (バッファ再利用)
                _styleProvider.GetVector(request.StyleId, request.StyleWeight, _styleVecBuffer);

                // Stage 6: TTS推論
                float[] audioSamples;
                using (s_TTSInfer.Auto())
                {
                    audioSamples = _tts.Run(
                        phonemeIds,
                        tones,
                        languageIds,
                        request.SpeakerId,
                        alignedBert,
                        _styleVecBuffer,
                        request.SdpRatio,
                        request.NoiseScale,
                        request.NoiseScaleW,
                        request.LengthScale);
                }

                TTSDebugLog.Log("TTS.Model",
                    $"audioSamples.Length={audioSamples.Length} ({(float)audioSamples.Length / _sampleRate:F2}s @ {_sampleRate}Hz)");

                // Stage 7: AudioClip生成
                AudioClip clip;
                using (s_Audio.Auto())
                {
                    TTSAudioUtility.NormalizeSamplesBurst(audioSamples, _normalizationPeak);
                    audioSamples = TrimTrailingSilence(audioSamples);
                    clip = AudioClip.Create("TTS", audioSamples.Length, 1, _sampleRate, false);
                    clip.SetData(audioSamples, 0);
                }

                return clip;
            }
            finally
            {
                System.Buffers.ArrayPool<float>.Shared.Return(alignedBert);
            }
        }

        public async UniTask<AudioClip> SynthesizeAsync(TTSRequest request, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TTSPipeline));

            TTSDebugLog.Log("TTS", $"=== SynthesizeAsync: \"{request.Text}\" ===");

            // --- ThreadPool: CPU処理 (G2P + Tokenize) ---
            await UniTask.SwitchToThreadPool();

            G2PResult g2p;
            using (s_G2P.Auto())
            {
                g2p = _g2p.Process(request.Text);
            }

            TTSDebugLog.Log("TTS.G2P",
                $"PhonemeIds.Length={g2p.PhonemeIds.Length}, " +
                $"Tones range=[{ArrayMin(g2p.Tones)},{ArrayMax(g2p.Tones)}], " +
                $"Word2Ph.Length={g2p.Word2Ph.Length}");

            // Stage 1.5: add_blank — 学習時と同じ前処理
            int[] phonemeIds = PhonemeUtils.Intersperse(g2p.PhonemeIds, 0);
            int[] tones = PhonemeUtils.Intersperse(g2p.Tones, 0);
            int[] languageIds = PhonemeUtils.Intersperse(g2p.LanguageIds, 0);
            int[] word2ph = PhonemeUtils.AdjustWord2PhForBlanks(g2p.Word2Ph);
            int phoneSeqLen = phonemeIds.Length;

            TTSDebugLog.Log("TTS.Blank",
                $"Interleaved: {g2p.PhonemeIds.Length} → {phoneSeqLen} phonemes");

            ct.ThrowIfCancellationRequested();

            int[] tokenIds;
            int[] attentionMask;
            using (s_Tokenize.Auto())
            {
                (tokenIds, attentionMask) = _tokenizer.Encode(request.Text);
            }

            TTSDebugLog.Log("TTS.Tokenize", $"tokenIds.Length={tokenIds.Length}");

            ct.ThrowIfCancellationRequested();

            // --- MainThread: BERT推論 (Sentis) ---
            await UniTask.SwitchToMainThread(ct);

            float[] bertData;
            using (s_BertInfer.Auto())
            {
                bertData = _bert.Run(tokenIds, attentionMask);
            }

            TTSDebugLog.Log("TTS.BERT",
                $"bertData.Length={bertData.Length} ({tokenIds.Length} tokens × 1024 dim)");

            ct.ThrowIfCancellationRequested();

            // --- ThreadPool: BertAlignment + StyleVector ---
            await UniTask.SwitchToThreadPool();

            int alignedLenAsync = BertAligner.EmbeddingDimension * phoneSeqLen;
            float[] alignedBert = System.Buffers.ArrayPool<float>.Shared.Rent(alignedLenAsync);
            try
            {
                using (s_BertAlign.Auto())
                {
                    BertAligner.AlignBertToPhonemesBurst(
                        bertData, tokenIds.Length, word2ph, phoneSeqLen, alignedBert);
                }

                TTSDebugLog.Log("TTS.Align",
                    $"alignedBert effective={alignedLenAsync} ({phoneSeqLen} phonemes × 1024 dim)");

                _styleProvider.GetVector(request.StyleId, request.StyleWeight, _styleVecBuffer);

                ct.ThrowIfCancellationRequested();

                // --- MainThread: TTS推論 + AudioClip生成 ---
                await UniTask.SwitchToMainThread(ct);

                float[] audioSamples;
                using (s_TTSInfer.Auto())
                {
                    audioSamples = _tts.Run(
                        phonemeIds,
                        tones,
                        languageIds,
                        request.SpeakerId,
                        alignedBert,
                        _styleVecBuffer,
                        request.SdpRatio,
                        request.NoiseScale,
                        request.NoiseScaleW,
                        request.LengthScale);
                }

                TTSDebugLog.Log("TTS.Model",
                    $"audioSamples.Length={audioSamples.Length} ({(float)audioSamples.Length / _sampleRate:F2}s @ {_sampleRate}Hz)");

                ct.ThrowIfCancellationRequested();

                AudioClip clip;
                using (s_Audio.Auto())
                {
                    TTSAudioUtility.NormalizeSamplesBurst(audioSamples, _normalizationPeak);
                    audioSamples = TrimTrailingSilence(audioSamples);
                    clip = AudioClip.Create("TTS", audioSamples.Length, 1, _sampleRate, false);
                    clip.SetData(audioSamples, 0);
                }

                return clip;
            }
            finally
            {
                System.Buffers.ArrayPool<float>.Shared.Return(alignedBert);
            }
        }

        private static int ArrayMin(int[] arr)
        {
            int min = arr[0];
            for (int i = 1; i < arr.Length; i++)
                if (arr[i] < min) min = arr[i];
            return min;
        }

        private static int ArrayMax(int[] arr)
        {
            int max = arr[0];
            for (int i = 1; i < arr.Length; i++)
                if (arr[i] > max) max = arr[i];
            return max;
        }

        /// <summary>
        /// 末尾の無音区間を除去する。
        /// パディングやデコーダの出力サイズ切り上げで生じる末尾の完全無音を取り除く。
        /// ピーク振幅ベースでブロック単位に判定し、有音ブロックが見つかったら停止する。
        /// </summary>
        private static float[] TrimTrailingSilence(float[] samples)
        {
            const int blockSize = 512; // 1 フレーム = hop_length
            const float silenceThreshold = 0.002f; // ピーク振幅閾値

            int totalBlocks = samples.Length / blockSize;
            if (totalBlocks <= 1)
                return samples;

            // 末尾からブロック単位でピーク振幅を確認し、無音ブロックをスキップ
            int lastActiveBlock = totalBlocks - 1;
            while (lastActiveBlock > 0)
            {
                int start = lastActiveBlock * blockSize;
                float peak = 0f;
                for (int i = start; i < start + blockSize && i < samples.Length; i++)
                {
                    float abs = samples[i] < 0 ? -samples[i] : samples[i];
                    if (abs > peak) peak = abs;
                }

                if (peak > silenceThreshold)
                    break;
                lastActiveBlock--;
            }

            int trimmedLength = (lastActiveBlock + 1) * blockSize;
            if (trimmedLength >= samples.Length)
                return samples;

            var result = new float[trimmedLength];
            Array.Copy(samples, result, trimmedLength);
            return result;
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
