using NUnit.Framework;

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
        // E2Eテストは全依存が揃った環境でのみ動作する。
        // CI環境ではカテゴリでスキップ。

        [Test]
        public void Placeholder_PipelineTestsRequireFullSetup()
        {
            // 以下のテストはモデル+DLL配置後に有効化
            Assert.Pass("Pipeline E2E tests require ONNX models and OpenJTalk DLL.");
        }

        /*
        [Test]
        public void SynthesizeReturnsAudioClip()
        {
            var pipeline = CreatePipeline();
            var request = new TTSRequest("こんにちは");
            var clip = pipeline.Synthesize(request);
            Assert.IsNotNull(clip);
        }

        [Test]
        public void AudioClipSampleRate44100()
        {
            var pipeline = CreatePipeline();
            var clip = pipeline.Synthesize(new TTSRequest("テスト"));
            Assert.AreEqual(44100, clip.frequency);
        }

        [Test]
        public void AudioClipHasSamples()
        {
            var pipeline = CreatePipeline();
            var clip = pipeline.Synthesize(new TTSRequest("テスト"));
            Assert.IsTrue(clip.samples > 0);
        }

        [Test]
        public void DisposeCleansUp()
        {
            var pipeline = CreatePipeline();
            pipeline.Dispose();
            // 二重Disposeで例外が出ないこと
            Assert.DoesNotThrow(() => pipeline.Dispose());
        }
        */
    }
}
