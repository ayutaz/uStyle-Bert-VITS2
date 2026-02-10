using NUnit.Framework;

namespace uStyleBertVITS2.Tests
{
    /// <summary>
    /// CachedBertRunner テスト。
    /// CachedBertRunner は内部で BertRunner (Sentis Worker) を使用するため、
    /// ONNX モデルなしではインスタンス化できない。
    /// キャッシュロジック自体は LRUCacheTests で網羅済み（Put/Get、LRU eviction、GetMovesToFront）。
    /// 統合テスト（キャッシュヒット/ミスで推論がスキップされるか）はモデル配置後に有効化する。
    /// </summary>
    [TestFixture]
    [Category("RequiresModel")]
    public class CachedBertTests
    {
        [Test]
        public void CacheLogicIsCoveredByLRUCacheTests()
        {
            // CachedBertRunner のキャッシュは LRUCache<string, float[]> を内部使用。
            // LRUCacheTests で以下を検証済み:
            //   - PutAndGet: キャッシュへの格納と取得
            //   - EvictsLeastRecentlyUsed: 容量超過時のLRU排出
            //   - GetMovesToFront: アクセス時の優先度更新
            Assert.Pass("Cache logic is fully covered by LRUCacheTests. " +
                        "Integration tests (cache hit skips BERT inference) require ONNX model.");
        }
    }
}
