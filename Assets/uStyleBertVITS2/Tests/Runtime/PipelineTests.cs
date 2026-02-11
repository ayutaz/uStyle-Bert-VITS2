using System;
using System.IO;
using NUnit.Framework;
using Unity.InferenceEngine;
using UnityEngine;
using uStyleBertVITS2.Data;
using uStyleBertVITS2.Inference;
using uStyleBertVITS2.Services;
using uStyleBertVITS2.TextProcessing;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// TTSPipeline E2Eテスト。
    /// 全コンポーネント(G2P + BERT + TTS + StyleVector)が必要。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    [Category("RequiresNativeDLL")]
    public class PipelineTests
    {
        private const string DeBERTaAssetPath = "Assets/uStyleBertVITS2/Models/deberta_fp32.onnx";
        private const string SBV2AssetPath = "Assets/uStyleBertVITS2/Models/sbv2_model_fp32.onnx";

        private static readonly string StreamingBase = Path.Combine(
            Application.streamingAssetsPath, "uStyleBertVITS2");

        private TTSPipeline _pipeline;

        private static ModelAsset LoadModelAsset(string assetPath)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(assetPath);
            if (asset == null)
                Assert.Ignore($"ModelAsset not found at: {assetPath}");
            return asset;
#else
            Assert.Ignore("Model loading only supported in Editor.");
            return null;
#endif
        }

        [SetUp]
        public void SetUp()
        {
            string dictPath = Path.Combine(StreamingBase, "OpenJTalkDic");
            string vocabPath = Path.Combine(StreamingBase, "Tokenizer", "vocab.json");
            string stylePath = Path.Combine(StreamingBase, "Models", "style_vectors.npy");

            if (!Directory.Exists(dictPath))
                Assert.Ignore($"OpenJTalk dictionary not found: {dictPath}");
            if (!File.Exists(vocabPath))
                Assert.Ignore($"Vocab file not found: {vocabPath}");
            if (!File.Exists(stylePath))
                Assert.Ignore($"Style vectors not found: {stylePath}");

            var g2p = new JapaneseG2P(dictPath);
            var tokenizer = new SBV2Tokenizer(vocabPath);

            var bertAsset = LoadModelAsset(DeBERTaAssetPath);
            var bert = new BertRunner(bertAsset, BackendType.CPU);

            var ttsAsset = LoadModelAsset(SBV2AssetPath);
            var tts = new SBV2ModelRunner(ttsAsset, BackendType.CPU);

            var styleProvider = new StyleVectorProvider();
            styleProvider.Load(stylePath);

            _pipeline = new TTSPipeline(g2p, tokenizer, bert, tts, styleProvider);
        }

        [TearDown]
        public void TearDown()
        {
            _pipeline?.Dispose();
            _pipeline = null;
        }

        [Test]
        public void SynthesizeReturnsAudioClip()
        {
            var request = new TTSRequest("こんにちは");
            var clip = _pipeline.Synthesize(request);
            Assert.IsNotNull(clip);
        }

        [Test]
        public void AudioClipSampleRate44100()
        {
            var clip = _pipeline.Synthesize(new TTSRequest("テスト"));
            Assert.AreEqual(44100, clip.frequency);
        }

        [Test]
        public void AudioClipHasSamples()
        {
            var clip = _pipeline.Synthesize(new TTSRequest("テスト"));
            Assert.IsTrue(clip.samples > 0);
        }

        [Test]
        public void DisposeCleansUp()
        {
            _pipeline.Dispose();
            Assert.DoesNotThrow(() => _pipeline.Dispose());
            _pipeline = null;
        }
    }
}
