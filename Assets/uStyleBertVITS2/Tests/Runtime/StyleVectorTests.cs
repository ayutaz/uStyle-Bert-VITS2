using System;
using NUnit.Framework;
using uStyleBertVITS2.Data;

namespace uStyleBertVITS2.Tests
{
    [TestFixture]
    public class StyleVectorTests
    {
        private StyleVectorProvider _provider;

        /// <summary>
        /// テスト用npyバイト列を作成するヘルパー。
        /// shape: [numStyles, 256]
        /// </summary>
        private static byte[] CreateStyleVectorNpyBytes(int numStyles)
        {
            float[] data = new float[numStyles * StyleVectorProvider.VectorDimension];
            for (int s = 0; s < numStyles; s++)
            {
                for (int d = 0; d < StyleVectorProvider.VectorDimension; d++)
                {
                    // 各スタイルに異なる値を設定
                    data[s * StyleVectorProvider.VectorDimension + d] = s * 0.1f + d * 0.001f;
                }
            }

            return CreateNpyBytes(data, new[] { numStyles, StyleVectorProvider.VectorDimension });
        }

        private static byte[] CreateNpyBytes(float[] data, int[] shape)
        {
            string shapeStr = $"({string.Join(", ", shape)})";
            string header = $"{{'descr': '<f4', 'fortran_order': False, 'shape': {shapeStr}, }}";
            int headerLen = header.Length;
            int totalHeaderSize = 10 + headerLen;
            int padding = 64 - (totalHeaderSize % 64);
            if (padding == 64) padding = 0;
            header = header.PadRight(header.Length + padding - 1) + "\n";
            headerLen = header.Length;

            byte[] result = new byte[10 + headerLen + data.Length * 4];
            result[0] = 0x93;
            result[1] = 0x4E;
            result[2] = 0x55;
            result[3] = 0x4D;
            result[4] = 0x50;
            result[5] = 0x59;
            result[6] = 1;
            result[7] = 0;
            result[8] = (byte)(headerLen & 0xFF);
            result[9] = (byte)((headerLen >> 8) & 0xFF);
            var headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
            Array.Copy(headerBytes, 0, result, 10, headerLen);
            Buffer.BlockCopy(data, 0, result, 10 + headerLen, data.Length * 4);
            return result;
        }

        [SetUp]
        public void Setup()
        {
            _provider = new StyleVectorProvider();
            byte[] npyBytes = CreateStyleVectorNpyBytes(3); // 3 styles
            _provider.Load(npyBytes);
        }

        [Test]
        public void LoadAndGetVector()
        {
            float[] vec = _provider.GetVector(0);
            Assert.AreEqual(StyleVectorProvider.VectorDimension, vec.Length);
        }

        [Test]
        public void NeutralVectorIsIndex0()
        {
            // weight=0 → ニュートラル(mean) = index 0のベクトルそのもの
            float[] neutral = _provider.GetVector(0, weight: 0f);
            float[] raw0 = _provider.GetRawVector(0);

            for (int i = 0; i < StyleVectorProvider.VectorDimension; i++)
                Assert.AreEqual(raw0[i], neutral[i], 1e-6f);
        }

        [Test]
        public void GetVector_DestOverload_MatchesAlloc()
        {
            float[] allocResult = _provider.GetVector(1, 0.5f);
            float[] dest = new float[StyleVectorProvider.VectorDimension];
            _provider.GetVector(1, 0.5f, dest);

            for (int i = 0; i < StyleVectorProvider.VectorDimension; i++)
                Assert.AreEqual(allocResult[i], dest[i], 1e-6f, $"dim {i} mismatch");
        }

        [Test]
        public void GetVector_DestOverload_ThrowsOnSmallBuffer()
        {
            float[] tooSmall = new float[128];
            Assert.Throws<ArgumentException>(() =>
                _provider.GetVector(0, 1f, tooSmall));
        }

        [Test]
        public void WeightedInterpolation()
        {
            // weight=0 → mean(index0)のまま, weight=1 → style(index1)そのもの
            float[] raw0 = _provider.GetRawVector(0); // mean
            float[] raw1 = _provider.GetRawVector(1); // style

            // weight=0 → mean
            float[] w0 = _provider.GetVector(1, weight: 0f);
            for (int i = 0; i < StyleVectorProvider.VectorDimension; i++)
                Assert.AreEqual(raw0[i], w0[i], 1e-6f, $"weight=0 should equal mean at dim {i}");

            // weight=1 → style
            float[] w1 = _provider.GetVector(1, weight: 1f);
            for (int i = 0; i < StyleVectorProvider.VectorDimension; i++)
                Assert.AreEqual(raw1[i], w1[i], 1e-6f, $"weight=1 should equal style at dim {i}");

            // weight=0.5 → midpoint
            float[] w05 = _provider.GetVector(1, weight: 0.5f);
            for (int i = 0; i < StyleVectorProvider.VectorDimension; i++)
            {
                float expected = raw0[i] + (raw1[i] - raw0[i]) * 0.5f;
                Assert.AreEqual(expected, w05[i], 1e-5f, $"weight=0.5 midpoint at dim {i}");
            }
        }
    }
}
