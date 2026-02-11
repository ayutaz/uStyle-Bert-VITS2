using System.IO;
using UnityEngine;
using Unity.InferenceEngine;

namespace uStyleBertVITS2.Configuration
{
    /// <summary>
    /// モデルパス・BackendType等のランタイム設定。
    /// TTSSettingsから構築されるimmutableな設定オブジェクト。
    /// </summary>
    public class ModelConfiguration
    {
        public ModelAsset BertModelAsset { get; }
        public ModelAsset TTSModelAsset { get; }
        public BackendType BertBackend { get; }
        public BackendType TTSBackend { get; }
        public string DictionaryFullPath { get; }
        public string VocabFullPath { get; }
        public string StyleVectorFullPath { get; }

        public ModelConfiguration(TTSSettings settings)
        {
            BertModelAsset = settings.BertModel;
            TTSModelAsset = settings.TTSModel;
            BertBackend = settings.BertBackend;
            TTSBackend = settings.TTSBackend;
            DictionaryFullPath = Path.Combine(Application.streamingAssetsPath, settings.DictionaryPath);
            VocabFullPath = Path.Combine(Application.streamingAssetsPath, settings.VocabPath);
            StyleVectorFullPath = Path.Combine(Application.streamingAssetsPath, settings.StyleVectorPath);
        }

        /// <summary>
        /// プラットフォームに応じた推奨バックエンドを返す。
        /// </summary>
        public static BackendType GetRecommendedBackend()
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.LinuxPlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
                return BackendType.GPUCompute;

            if (Application.platform == RuntimePlatform.Android ||
                Application.platform == RuntimePlatform.IPhonePlayer)
                return BackendType.CPU;

            if (Application.isEditor)
                return BackendType.GPUCompute;

            return BackendType.CPU;
        }
    }
}
