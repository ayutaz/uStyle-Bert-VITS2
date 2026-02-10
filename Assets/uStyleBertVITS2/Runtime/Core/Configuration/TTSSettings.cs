using UnityEngine;
using Unity.InferenceEngine;

namespace uStyleBertVITS2.Configuration
{
    /// <summary>
    /// Style-Bert-VITS2 TTS設定。ScriptableObjectとしてInspectorから設定可能。
    /// </summary>
    [CreateAssetMenu(fileName = "TTSSettings", menuName = "uStyleBertVITS2/TTS Settings")]
    public class TTSSettings : ScriptableObject
    {
        [Header("Models")]
        [Tooltip("DeBERTa ONNX model asset")]
        public ModelAsset BertModel;

        [Tooltip("SynthesizerTrn ONNX model asset")]
        public ModelAsset TTSModel;

        [Header("Backend")]
        [Tooltip("推論バックエンド (GPUCompute推奨)")]
        public BackendType PreferredBackend = BackendType.GPUCompute;

        [Tooltip("フォールバック用バックエンド")]
        public BackendType FallbackBackend = BackendType.CPU;

        [Header("Default Parameters")]
        [Range(0f, 1f)]
        [Tooltip("SDP/DP混合比 (0=DP only, 1=SDP only)")]
        public float DefaultSdpRatio = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("生成ノイズスケール")]
        public float DefaultNoiseScale = 0.6f;

        [Range(0f, 1f)]
        [Tooltip("継続長ノイズスケール")]
        public float DefaultNoiseScaleW = 0.8f;

        [Range(0.5f, 2f)]
        [Tooltip("話速倍率 (1.0=通常)")]
        public float DefaultLengthScale = 1.0f;

        [Header("Paths (relative to StreamingAssets)")]
        [Tooltip("OpenJTalk辞書ディレクトリのパス")]
        public string DictionaryPath = "uStyleBertVITS2/OpenJTalkDic";

        [Tooltip("DeBERTaトークナイザ語彙ファイルのパス")]
        public string VocabPath = "uStyleBertVITS2/Tokenizer/vocab.json";

        [Tooltip("スタイルベクトル .npy ファイルのパス")]
        public string StyleVectorPath = "uStyleBertVITS2/StyleVectors/style_vectors.npy";

        [Header("Performance")]
        [Tooltip("起動時にウォームアップ推論を実行する")]
        public bool EnableWarmup = true;

        [Tooltip("BERT推論結果のキャッシュを有効にする")]
        public bool EnableBertCache = true;

        [Range(16, 256)]
        [Tooltip("BERTキャッシュの最大エントリ数")]
        public int BertCacheCapacity = 64;

        [Header("Audio")]
        [Tooltip("出力サンプルレート (Hz)")]
        public int SampleRate = 44100;

        [Range(0.1f, 1.0f)]
        [Tooltip("音声正規化のピークレベル")]
        public float NormalizationPeak = 0.95f;
    }
}
