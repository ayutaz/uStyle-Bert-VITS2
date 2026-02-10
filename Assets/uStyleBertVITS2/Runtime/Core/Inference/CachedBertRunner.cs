using System;
using uStyleBertVITS2.Data;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// LRUキャッシュ付きBERT推論ラッパー。
    /// 同一テキストの重複推論を回避する。
    /// </summary>
    public class CachedBertRunner : IDisposable
    {
        private readonly BertRunner _runner;
        private readonly LRUCache<string, float[]> _cache;
        private bool _disposed;

        public CachedBertRunner(BertRunner runner, int capacity = 64)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _cache = new LRUCache<string, float[]>(capacity);
        }

        /// <summary>
        /// BERT推論を実行する。キャッシュヒット時は推論をスキップ。
        /// </summary>
        /// <param name="text">キャッシュキーとなるテキスト</param>
        /// <param name="tokenIds">トークンID配列</param>
        /// <param name="attentionMask">アテンションマスク</param>
        public float[] Run(string text, int[] tokenIds, int[] attentionMask)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CachedBertRunner));

            if (_cache.TryGet(text, out var cached))
                return cached;

            float[] result = _runner.Run(tokenIds, attentionMask);
            _cache.Put(text, result);
            return result;
        }

        /// <summary>
        /// キャッシュ内のエントリ数。
        /// </summary>
        public int CacheCount => _cache.Count;

        /// <summary>
        /// キャッシュをクリアする。
        /// </summary>
        public void ClearCache() => _cache.Clear();

        public void Dispose()
        {
            if (!_disposed)
            {
                _runner?.Dispose();
                _disposed = true;
            }
        }
    }
}
