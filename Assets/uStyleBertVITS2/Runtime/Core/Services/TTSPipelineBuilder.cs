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
        private BertRunner _bertRunner;
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

        public TTSPipelineBuilder WithBertRunner(BertRunner bertRunner)
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

            _g2p ??= new JapaneseG2P(
                Path.Combine(Application.streamingAssetsPath, _settings.DictionaryPath));

            _tokenizer ??= new SBV2Tokenizer(
                Path.Combine(Application.streamingAssetsPath, _settings.VocabPath));

            _bertRunner ??= new BertRunner(_settings.BertModel, _settings.PreferredBackend);

            _ttsRunner ??= new SBV2ModelRunner(_settings.TTSModel, _settings.PreferredBackend);

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
