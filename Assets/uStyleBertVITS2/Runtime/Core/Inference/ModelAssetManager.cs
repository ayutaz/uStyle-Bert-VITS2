using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// モデルのロード・キャッシュ・Dispose管理を一元化する。
    /// 同じモデルの重複ロードを防ぎ、ライフサイクルを統一管理する。
    /// </summary>
    public class ModelAssetManager : IDisposable
    {
        private readonly Dictionary<string, Worker> _workers = new();
        private bool _disposed;

        /// <summary>
        /// 指定キーの Worker を取得する。未作成なら新規作成してキャッシュ。
        /// </summary>
        public Worker GetOrCreateWorker(ModelAsset asset, BackendType backendType, string key)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModelAssetManager));

            if (_workers.TryGetValue(key, out var existing))
                return existing;

            var model = ModelLoader.Load(asset);
            var worker = new Worker(model, backendType);
            _workers[key] = worker;
            return worker;
        }

        /// <summary>
        /// GPU→CPUフォールバック付きでWorkerを作成する。
        /// </summary>
        public Worker GetOrCreateWorkerWithFallback(
            ModelAsset asset, BackendType preferred, BackendType fallback, string key)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModelAssetManager));

            if (_workers.TryGetValue(key, out var existing))
                return existing;

            var model = ModelLoader.Load(asset);
            Worker worker;

            try
            {
                worker = new Worker(model, preferred);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"Backend {preferred} failed for '{key}': {e.Message}. Falling back to {fallback}.");
                worker = new Worker(model, fallback);
            }

            _workers[key] = worker;
            return worker;
        }

        /// <summary>
        /// 指定キーの Worker が存在するか確認する。
        /// </summary>
        public bool HasWorker(string key)
        {
            return _workers.ContainsKey(key);
        }

        /// <summary>
        /// 管理中の Worker 数。
        /// </summary>
        public int WorkerCount => _workers.Count;

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var worker in _workers.Values)
                worker?.Dispose();

            _workers.Clear();
            _disposed = true;
        }
    }
}
