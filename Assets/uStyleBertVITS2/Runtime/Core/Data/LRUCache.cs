using System.Collections.Generic;

namespace uStyleBertVITS2.Data
{
    /// <summary>
    /// LRU (Least Recently Used) キャッシュ。
    /// 主にBERT推論結果のキャッシュに使用。
    /// メインスレッドからの単一スレッドアクセスを前提。
    /// </summary>
    public class LRUCache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _map;
        private readonly LinkedList<(TKey Key, TValue Value)> _list;

        public LRUCache(int capacity)
        {
            _capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>(capacity);
            _list = new LinkedList<(TKey, TValue)>();
        }

        /// <summary>
        /// キャッシュからエントリを取得する。ヒットした場合はアクセス順を更新。
        /// </summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                // アクセス順を更新 (先頭に移動)
                _list.Remove(node);
                _list.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// キャッシュにエントリを追加する。容量超過時はLRUエントリを削除。
        /// </summary>
        public void Put(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                // 既存エントリの更新
                _list.Remove(existing);
                _map.Remove(key);
            }
            else if (_map.Count >= _capacity)
            {
                // 容量超過: 末尾 (LRU) を削除
                var last = _list.Last;
                _list.RemoveLast();
                _map.Remove(last.Value.Key);
            }

            var node = _list.AddFirst((key, value));
            _map[key] = node;
        }

        /// <summary>
        /// キャッシュ内のエントリ数。
        /// </summary>
        public int Count => _map.Count;

        /// <summary>
        /// キャッシュをクリアする。
        /// </summary>
        public void Clear()
        {
            _map.Clear();
            _list.Clear();
        }
    }
}
