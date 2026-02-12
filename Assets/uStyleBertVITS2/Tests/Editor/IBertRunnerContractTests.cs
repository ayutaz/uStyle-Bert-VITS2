using System;
using NUnit.Framework;
using uStyleBertVITS2.Inference;

namespace uStyleBertVITS2.Tests.Editor
{
    /// <summary>
    /// IBertRunner インターフェース契約テスト。
    /// 各実装が IBertRunner を正しく実装していることを検証する。
    /// </summary>
    public class IBertRunnerContractTests
    {
        [Test]
        public void BertRunner_Implements_IBertRunner()
        {
            Assert.That(typeof(IBertRunner).IsAssignableFrom(typeof(BertRunner)),
                "BertRunner must implement IBertRunner");
        }

        [Test]
        public void CachedBertRunner_Implements_IBertRunner()
        {
            Assert.That(typeof(IBertRunner).IsAssignableFrom(typeof(CachedBertRunner)),
                "CachedBertRunner must implement IBertRunner");
        }

        [Test]
        public void IBertRunner_Has_HiddenSize_Property()
        {
            var prop = typeof(IBertRunner).GetProperty(nameof(IBertRunner.HiddenSize));
            Assert.That(prop, Is.Not.Null, "IBertRunner must have HiddenSize property");
            Assert.That(prop.PropertyType, Is.EqualTo(typeof(int)));
        }

        [Test]
        public void IBertRunner_Has_Run_DestOverload()
        {
            var method = typeof(IBertRunner).GetMethod("Run",
                new[] { typeof(int[]), typeof(int[]), typeof(float[]) });
            Assert.That(method, Is.Not.Null, "IBertRunner must have Run(int[], int[], float[]) method");
        }

        [Test]
        public void IBertRunner_Has_Run_AllocOverload()
        {
            var method = typeof(IBertRunner).GetMethod("Run",
                new[] { typeof(int[]), typeof(int[]) });
            Assert.That(method, Is.Not.Null, "IBertRunner must have Run(int[], int[]) method");
        }

        [Test]
        public void IBertRunner_Extends_IDisposable()
        {
            Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(IBertRunner)),
                "IBertRunner must extend IDisposable");
        }

#if USBV2_ORT_AVAILABLE
        [Test]
        public void OnnxRuntimeBertRunner_Implements_IBertRunner()
        {
            Assert.That(typeof(IBertRunner).IsAssignableFrom(typeof(OnnxRuntimeBertRunner)),
                "OnnxRuntimeBertRunner must implement IBertRunner");
        }
#endif
    }
}
