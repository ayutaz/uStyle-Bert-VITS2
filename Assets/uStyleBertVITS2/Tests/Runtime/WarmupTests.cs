using NUnit.Framework;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// ウォームアップ推論テスト。モデルが必要。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    public class WarmupTests
    {
        [Test]
        public void Placeholder_WarmupTestsRequireModel()
        {
            Assert.Pass("Warmup tests require ONNX models.");
        }

        /*
        [Test]
        public void WarmupCompletes()
        {
            // ダミー推論でウォームアップが正常完了
        }
        */
    }
}
