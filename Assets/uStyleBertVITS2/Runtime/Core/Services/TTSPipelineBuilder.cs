using System;
using System.IO;
using UnityEngine;
using uStyleBertVITS2.Configuration;
using uStyleBertVITS2.Data;
using uStyleBertVITS2.Inference;
using uStyleBertVITS2.TextProcessing;

namespace uStyleBertVITS2.Services
{
    /// <summary>
    /// TTSPipeline の Builder パターン。
    /// TTSSettings から各コンポーネントを構築して ITTSPipeline を返す。
    /// </summary>
    public class TTSPipelineBuilder
    {
        private IG2P _g2p;
        private SBV2Tokenizer _tokenizer;
        private IBertRunner _bertRunner;
        private SBV2ModelRunner _ttsRunner;
        private StyleVectorProvider _styleProvider;
        private TTSSettings _settings;

        public TTSPipelineBuilder WithSettings(TTSSettings settings)
        {
            _settings = settings;
            return this;
        }

        public TTSPipelineBuilder WithG2P(IG2P g2p)
        {
            _g2p = g2p;
            return this;
        }

        public TTSPipelineBuilder WithTokenizer(SBV2Tokenizer tokenizer)
        {
            _tokenizer = tokenizer;
            return this;
        }

        public TTSPipelineBuilder WithBertRunner(IBertRunner bertRunner)
        {
            _bertRunner = bertRunner;
            return this;
        }

        public TTSPipelineBuilder WithTTSRunner(SBV2ModelRunner ttsRunner)
        {
            _ttsRunner = ttsRunner;
            return this;
        }

        public TTSPipelineBuilder WithStyleProvider(StyleVectorProvider styleProvider)
        {
            _styleProvider = styleProvider;
            return this;
        }

        /// <summary>
        /// TTSPipeline を構築する。
        /// 未設定のコンポーネントは TTSSettings から自動構築される。
        /// </summary>
        public ITTSPipeline Build()
        {
            if (_settings == null)
                throw new System.InvalidOperationException(
                    "TTSSettings must be provided via WithSettings().");

            if (_g2p == null)
            {
                string dictPath = Path.Combine(Application.streamingAssetsPath, _settings.DictionaryPath);
                _g2p = _settings.G2PEngine switch
                {
#if USBV2_DOTNET_G2P_AVAILABLE
                    G2PEngineType.DotNetG2P => new DotNetG2PJapaneseG2P(dictPath),
#endif
                    _ => new JapaneseG2P(dictPath)
                };
            }

            _tokenizer ??= new SBV2Tokenizer(
                Path.Combine(Application.streamingAssetsPath, _settings.VocabPath));

            if (_bertRunner == null)
            {
                switch (_settings.BertEngineType)
                {
                    case BertEngine.Sentis:
                        _bertRunner = new BertRunner(_settings.BertModel, _settings.BertBackend);
                        break;
                    case BertEngine.OnnxRuntime:
#if USBV2_ORT_AVAILABLE
                        var ortPath = Path.Combine(Application.streamingAssetsPath, _settings.OrtBertModelPath);
                        _bertRunner = new OnnxRuntimeBertRunner(ortPath, _settings.UseDirectML, _settings.DirectMLDeviceId);
                        break;
#else
                        throw new InvalidOperationException(
                            "BertEngine.OnnxRuntime is selected but ONNX Runtime is not available. " +
                            "Install com.github.asus4.onnxruntime package.");
#endif
                }
            }

            _ttsRunner ??= new SBV2ModelRunner(_settings.TTSModel, _settings.TTSBackend);

            if (_styleProvider == null)
            {
                _styleProvider = new StyleVectorProvider();
                _styleProvider.Load(
                    Path.Combine(Application.streamingAssetsPath, _settings.StyleVectorPath));
            }

            return new TTSPipeline(
                _g2p, _tokenizer, _bertRunner, _ttsRunner, _styleProvider,
                _settings.SampleRate, _settings.NormalizationPeak);
        }
    }
}
