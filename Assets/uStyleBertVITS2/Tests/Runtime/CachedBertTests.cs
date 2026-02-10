using NUnit.Framework;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// CachedBertRunner テスト。
    /// 実際のBERT推論はモデルが必要なため、キャッシュのロジック検証に限定。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    public class CachedBertTests
    {
        [Test]
        public void Placeholder_CachedBertTestsRequireModel()
        {
            // キャッシュヒット/ミスのテストはモデル配置後に有効化
            Assert.Pass("CachedBertRunner tests require BERT ONNX model.");
        }

        /*
        [Test]
        public void CacheHitSkipsInference()
        {
            // 同一テキストの2回目はキャッシュから返される
        }

        [Test]
        public void CacheMissRunsInference()
        {
            // 未キャッシュテキストで推論が実行される
        }
        */
    }
}
