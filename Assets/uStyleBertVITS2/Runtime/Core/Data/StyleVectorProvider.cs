using System;
using System.IO;

namespace uStyleBertVITS2.Data
{
    /// <summary>
    /// style_vectors.npy からスタイルベクトルを読み込み、
    /// sbv2-api方式の補間 (mean + (style - mean) * weight) で取得する。
    /// </summary>
    public class StyleVectorProvider
    {
        public const int VectorDimension = 256;

        private float[] _flatData;
        private int _numStyles;
        private bool _loaded;

        /// <summary>
        /// 読み込み済みのスタイル数。
        /// </summary>
        public int NumStyles => _numStyles;

        /// <summary>
        /// データが読み込み済みかどうか。
        /// </summary>
        public bool IsLoaded => _loaded;

        /// <summary>
        /// .npyファイルからスタイルベクトルを読み込む。
        /// shape: [num_styles, 256]
        /// </summary>
        public void Load(string npyPath)
        {
            var (data, shape) = NpyReader.LoadFloat32(npyPath);

            if (shape.Length != 2 || shape[1] != VectorDimension)
                throw new FormatException(
                    $"Expected shape [N, {VectorDimension}], got [{string.Join(", ", shape)}].");

            _flatData = data;
            _numStyles = shape[0];
            _loaded = true;
        }

        /// <summary>
        /// バイト配列からスタイルベクトルを読み込む。
        /// </summary>
        public void Load(byte[] npyBytes)
        {
            var (data, shape) = NpyReader.ParseFloat32(npyBytes);

            if (shape.Length != 2 || shape[1] != VectorDimension)
                throw new FormatException(
                    $"Expected shape [N, {VectorDimension}], got [{string.Join(", ", shape)}].");

            _flatData = data;
            _numStyles = shape[0];
            _loaded = true;
        }

        /// <summary>
        /// スタイルベクトルを取得する。
        /// sbv2-api方式: mean + (style - mean) * weight
        /// weight=0 → ニュートラル(mean), weight=1 → 完全なスタイル
        /// </summary>
        /// <param name="styleId">スタイルインデックス (0=ニュートラル基準)</param>
        /// <param name="weight">スタイルの強度 (0.0-1.0)</param>
        public float[] GetVector(int styleId, float weight = 1.0f)
        {
            if (!_loaded)
                throw new InvalidOperationException("Style vectors not loaded. Call Load() first.");

            if (styleId < 0 || styleId >= _numStyles)
                throw new ArgumentOutOfRangeException(nameof(styleId),
                    $"styleId must be 0-{_numStyles - 1}, got {styleId}.");

            float[] result = new float[VectorDimension];
            int meanOffset = 0; // index 0 = ニュートラル基準 (mean)
            int styleOffset = styleId * VectorDimension;

            for (int i = 0; i < VectorDimension; i++)
            {
                float mean = _flatData[meanOffset + i];
                float style = _flatData[styleOffset + i];
                result[i] = mean + (style - mean) * weight;
            }

            return result;
        }

        /// <summary>
        /// 指定インデックスの生のスタイルベクトルを取得する（補間なし）。
        /// </summary>
        public float[] GetRawVector(int index)
        {
            if (!_loaded)
                throw new InvalidOperationException("Style vectors not loaded. Call Load() first.");

            if (index < 0 || index >= _numStyles)
                throw new ArgumentOutOfRangeException(nameof(index));

            float[] result = new float[VectorDimension];
            Array.Copy(_flatData, index * VectorDimension, result, 0, VectorDimension);
            return result;
        }
    }
}
