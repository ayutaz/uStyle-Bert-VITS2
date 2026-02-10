using NUnit.Framework;
using uStyleBertVITS2.Data;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class LRUCacheTests
    {
        [Test]
        public void PutAndGet()
        {
            var cache = new LRUCache<string, int>(3);
            cache.Put("a", 1);
            cache.Put("b", 2);

            Assert.IsTrue(cache.TryGet("a", out int val));
            Assert.AreEqual(1, val);

            Assert.IsTrue(cache.TryGet("b", out val));
            Assert.AreEqual(2, val);

            Assert.IsFalse(cache.TryGet("c", out _));
        }

        [Test]
        public void EvictsLeastRecentlyUsed()
        {
            var cache = new LRUCache<string, int>(2);
            cache.Put("a", 1);
            cache.Put("b", 2);
            // キャパシティ2、"a"がLRU
            cache.Put("c", 3); // "a"が削除されるべき

            Assert.IsFalse(cache.TryGet("a", out _), "aはevictされるべき");
            Assert.IsTrue(cache.TryGet("b", out int val));
            Assert.AreEqual(2, val);
            Assert.IsTrue(cache.TryGet("c", out val));
            Assert.AreEqual(3, val);
        }

        [Test]
        public void GetMovesToFront()
        {
            var cache = new LRUCache<string, int>(2);
            cache.Put("a", 1);
            cache.Put("b", 2);

            // "a"をアクセスして先頭に移動
            cache.TryGet("a", out _);

            // "c"追加で "b"がevictされるべき（"a"は最近アクセスしたので残る）
            cache.Put("c", 3);

            Assert.IsTrue(cache.TryGet("a", out _), "aは最近アクセスしたので残る");
            Assert.IsFalse(cache.TryGet("b", out _), "bがevictされるべき");
            Assert.IsTrue(cache.TryGet("c", out _));
        }
    }
}
