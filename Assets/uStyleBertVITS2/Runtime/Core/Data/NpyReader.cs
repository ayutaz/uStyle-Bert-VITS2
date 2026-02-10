using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace uStyleBertVITS2.Data
{
    /// <summary>
    /// NumPy .npy バイナリファイルパーサー。
    /// float32/float64の1D/2D配列をサポート。
    /// </summary>
    public static class NpyReader
    {
        private static readonly byte[] MagicBytes = { 0x93, 0x4E, 0x55, 0x4D, 0x50, 0x59 }; // \x93NUMPY

        /// <summary>
        /// .npyファイルを読み込み、float配列とshape情報を返す。
        /// </summary>
        public static (float[] Data, int[] Shape) LoadFloat32(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            return ParseFloat32(bytes);
        }

        /// <summary>
        /// .npyファイルを読み込み、float配列とshape情報を返す。
        /// </summary>
        public static (float[] Data, int[] Shape) LoadFloat32(byte[] bytes)
        {
            return ParseFloat32(bytes);
        }

        /// <summary>
        /// バイト配列からnpyファイルをパースする。
        /// </summary>
        public static (float[] Data, int[] Shape) ParseFloat32(byte[] bytes)
        {
            ValidateMagic(bytes);

            int majorVersion = bytes[6];
            int minorVersion = bytes[7];

            int headerLength;
            int dataOffset;

            if (majorVersion == 1)
            {
                headerLength = BitConverter.ToUInt16(bytes, 8);
                dataOffset = 10 + headerLength;
            }
            else if (majorVersion == 2)
            {
                headerLength = (int)BitConverter.ToUInt32(bytes, 8);
                dataOffset = 12 + headerLength;
            }
            else
            {
                throw new FormatException($"Unsupported npy version: {majorVersion}.{minorVersion}");
            }

            string header = Encoding.ASCII.GetString(bytes, majorVersion == 1 ? 10 : 12, headerLength);
            var (descr, fortranOrder, shape) = ParseHeader(header);

            if (fortranOrder)
                throw new NotSupportedException("Fortran-order arrays are not supported.");

            int totalElements = 1;
            for (int i = 0; i < shape.Length; i++)
                totalElements *= shape[i];

            float[] data;

            if (descr == "<f4" || descr == "f4")
            {
                // float32
                data = new float[totalElements];
                Buffer.BlockCopy(bytes, dataOffset, data, 0, totalElements * 4);
            }
            else if (descr == "<f8" || descr == "f8")
            {
                // float64 → float32変換
                data = new float[totalElements];
                for (int i = 0; i < totalElements; i++)
                {
                    double val = BitConverter.ToDouble(bytes, dataOffset + i * 8);
                    data[i] = (float)val;
                }
            }
            else
            {
                throw new NotSupportedException($"Unsupported dtype: '{descr}'. Only float32 (<f4) and float64 (<f8) are supported.");
            }

            return (data, shape);
        }

        private static void ValidateMagic(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 10)
                throw new FormatException("Invalid npy file: too short.");

            for (int i = 0; i < MagicBytes.Length; i++)
            {
                if (bytes[i] != MagicBytes[i])
                    throw new FormatException("Invalid npy file: magic number mismatch.");
            }
        }

        private static (string Descr, bool FortranOrder, int[] Shape) ParseHeader(string header)
        {
            // ヘッダー例: {'descr': '<f4', 'fortran_order': False, 'shape': (10, 256), }
            string descr = ExtractStringValue(header, "descr");
            bool fortranOrder = header.Contains("'fortran_order': True");

            var shapeMatch = Regex.Match(header, @"'shape'\s*:\s*\(([^)]*)\)");
            if (!shapeMatch.Success)
                throw new FormatException("Invalid npy header: missing shape.");

            string shapeStr = shapeMatch.Groups[1].Value.Trim();
            int[] shape;

            if (string.IsNullOrEmpty(shapeStr))
            {
                // scalar
                shape = Array.Empty<int>();
            }
            else
            {
                string[] parts = shapeStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                shape = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    shape[i] = int.Parse(parts[i].Trim());
            }

            return (descr, fortranOrder, shape);
        }

        private static string ExtractStringValue(string header, string key)
        {
            var match = Regex.Match(header, $"'{key}'\\s*:\\s*'([^']*)'");
            if (!match.Success)
                throw new FormatException($"Invalid npy header: missing '{key}'.");
            return match.Groups[1].Value;
        }
    }
}
