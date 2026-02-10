using NUnit.Framework;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// UniTask非同期パイプラインテスト。
    /// 全コンポーネントが必要。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    [Category("RequiresNativeDLL")]
    public class AsyncPipelineTests
    {
        [Test]
        public void Placeholder_AsyncTestsRequireFullSetup()
        {
            Assert.Pass("Async pipeline tests require ONNX models and OpenJTalk DLL.");
        }

        /*
        [Test]
        public async void SynthesizeAsyncReturnsClip()
        {
            // UniTask版E2Eテスト
        }

        [Test]
        public async void CancellationStopsInference()
        {
            // CancellationToken でキャンセルが動作
        }
        */
    }
}
