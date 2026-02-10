using System;
using NUnit.Framework;
using uStyleBertVITS2.Data;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class NpyReaderTests
    {
        /// <summary>
        /// 有効なnpyファイルバイト列（float32, shape [2, 3]）を生成するヘルパー。
        /// </summary>
        private static byte[] CreateValidNpyBytes(float[] data, int[] shape)
        {
            // ヘッダー文字列構築
            string shapeStr = shape.Length == 1
                ? $"({shape[0]},)"
                : $"({string.Join(", ", shape)})";
            string header = $"{{'descr': '<f4', 'fortran_order': False, 'shape': {shapeStr}, }}";

            // 64バイト境界にパディング
            int headerLen = header.Length;
            int totalHeaderSize = 10 + headerLen; // magic(6) + version(2) + headerLen(2) + header
            int padding = 64 - (totalHeaderSize % 64);
            if (padding == 64) padding = 0;
            header = header.PadRight(header.Length + padding - 1) + "\n";
            headerLen = header.Length;

            byte[] result = new byte[10 + headerLen + data.Length * 4];

            // Magic: \x93NUMPY
            result[0] = 0x93;
            result[1] = 0x4E; // N
            result[2] = 0x55; // U
            result[3] = 0x4D; // M
            result[4] = 0x50; // P
            result[5] = 0x59; // Y

            // Version 1.0
            result[6] = 1;
            result[7] = 0;

            // Header length (little-endian uint16)
            result[8] = (byte)(headerLen & 0xFF);
            result[9] = (byte)((headerLen >> 8) & 0xFF);

            // Header string
            var headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
            Array.Copy(headerBytes, 0, result, 10, headerLen);

            // Data
            Buffer.BlockCopy(data, 0, result, 10 + headerLen, data.Length * 4);

            return result;
        }

        [Test]
        public void ParsesValidNpyFile()
        {
            float[] data = { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
            int[] shape = { 2, 3 };
            byte[] bytes = CreateValidNpyBytes(data, shape);

            var (resultData, resultShape) = NpyReader.ParseFloat32(bytes);

            Assert.AreEqual(2, resultShape.Length);
            Assert.AreEqual(2, resultShape[0]);
            Assert.AreEqual(3, resultShape[1]);
        }

        [Test]
        public void ParsesFloat32Array()
        {
            float[] expected = { 1.5f, 2.5f, 3.5f, 4.5f };
            int[] shape = { 4 };
            byte[] bytes = CreateValidNpyBytes(expected, shape);

            var (resultData, resultShape) = NpyReader.ParseFloat32(bytes);

            Assert.AreEqual(expected.Length, resultData.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], resultData[i], 1e-6f);
        }

        [Test]
        public void ThrowsOnInvalidFormat()
        {
            byte[] garbage = { 0x00, 0x01, 0x02, 0x03 };
            Assert.Throws<FormatException>(() => NpyReader.ParseFloat32(garbage));
        }
    }
}
