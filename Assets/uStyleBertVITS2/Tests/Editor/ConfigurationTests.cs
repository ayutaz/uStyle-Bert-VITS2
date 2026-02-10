using NUnit.Framework;
using UnityEngine;
using Unity.InferenceEngine;
using uStyleBertVITS2.Configuration;

namespace uStyleBertVITS2.Tests.Editor
{
    [TestFixture]
    public class ConfigurationTests
    {
        [Test]
        public void TTSSettingsDefaults()
        {
            var settings = ScriptableObject.CreateInstance<TTSSettings>();

            Assert.AreEqual(BackendType.GPUCompute, settings.PreferredBackend);
            Assert.AreEqual(BackendType.CPU, settings.FallbackBackend);
            Assert.AreEqual(0.2f, settings.DefaultSdpRatio, 1e-6f);
            Assert.AreEqual(0.6f, settings.DefaultNoiseScale, 1e-6f);
            Assert.AreEqual(0.8f, settings.DefaultNoiseScaleW, 1e-6f);
            Assert.AreEqual(1.0f, settings.DefaultLengthScale, 1e-6f);
            Assert.AreEqual("uStyleBertVITS2/OpenJTalkDic", settings.DictionaryPath);
            Assert.AreEqual("uStyleBertVITS2/Tokenizer/vocab.json", settings.VocabPath);
            Assert.AreEqual("uStyleBertVITS2/StyleVectors/style_vectors.npy", settings.StyleVectorPath);
            Assert.IsTrue(settings.EnableWarmup);
            Assert.IsTrue(settings.EnableBertCache);
            Assert.AreEqual(64, settings.BertCacheCapacity);
            Assert.AreEqual(44100, settings.SampleRate);
            Assert.AreEqual(0.95f, settings.NormalizationPeak, 1e-6f);

            Object.DestroyImmediate(settings);
        }

        [Test]
        public void TTSSettingsSerializes()
        {
            var settings = ScriptableObject.CreateInstance<TTSSettings>();
            settings.DefaultSdpRatio = 0.5f;
            settings.DefaultLengthScale = 1.5f;
            settings.EnableWarmup = false;

            // ScriptableObjectのシリアライズ/デシリアライズをJSON経由でテスト
            string json = JsonUtility.ToJson(settings);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("0.5"));

            var restored = ScriptableObject.CreateInstance<TTSSettings>();
            JsonUtility.FromJsonOverwrite(json, restored);

            Assert.AreEqual(0.5f, restored.DefaultSdpRatio, 1e-6f);
            Assert.AreEqual(1.5f, restored.DefaultLengthScale, 1e-6f);
            Assert.IsFalse(restored.EnableWarmup);

            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(restored);
        }
    }
}
