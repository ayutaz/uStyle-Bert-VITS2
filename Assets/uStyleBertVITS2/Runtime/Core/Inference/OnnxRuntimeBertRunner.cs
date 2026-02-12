#if USBV2_ORT_AVAILABLE
using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using UnityEngine;

namespace uStyleBertVITS2.Inference
{
    /// <summary>
    /// ONNX Runtime + DirectML による DeBERTa 推論ラッパー。
    /// Sentis と異なり動的シェイプをネイティブサポートするためパディング不要。
    /// 出力は [1, 1024, tokenLen] (convert_bert_for_sentis.py で transpose 済み)。
    /// </summary>
    public class OnnxRuntimeBertRunner : IBertRunner
    {
        private const int HiddenDim = 1024;

        private InferenceSession _session;
        private bool _disposed;

        public int HiddenSize => HiddenDim;

        /// <summary>DirectML が実際に有効化されたかどうか。</summary>
        public bool IsDirectMLActive { get; private set; }

        public OnnxRuntimeBertRunner(string modelPath, bool useDirectML = true, int deviceId = 0)
        {
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

            if (useDirectML)
            {
                try
                {
                    // DirectML は EnableMemoryPattern = false が必須
                    options.EnableMemoryPattern = false;
                    options.AppendExecutionProvider_DML(deviceId);
                    IsDirectMLActive = true;
                }
                catch (EntryPointNotFoundException)
                {
                    Debug.LogWarning(
                        "[OnnxRuntimeBertRunner] DirectML entry point not found in onnxruntime.dll. " +
                        "Falling back to CPU. To enable DirectML, place the DirectML-enabled " +
                        "onnxruntime.dll in Assets/uStyleBertVITS2/Plugins/Windows/x86_64/.");
                    options.EnableMemoryPattern = true;
                }
            }

            // CPU fallback は暗黙的に追加される
            _session = new InferenceSession(modelPath, options);
        }

        public void Run(int[] tokenIds, int[] attentionMask, float[] dest)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OnnxRuntimeBertRunner));
            if (dest == null)
                throw new ArgumentNullException(nameof(dest));

            int tokenLen = tokenIds.Length;
            int requiredLen = HiddenDim * tokenLen;
            if (dest.Length < requiredLen)
                throw new ArgumentException(
                    $"dest buffer too small: {dest.Length} < {requiredLen}.", nameof(dest));

            var shape = new long[] { 1, tokenLen };

            // int[] → long[] 変換 (ORT は int64 入力を期待)
            var inputIdsLong = new long[tokenLen];
            var maskLong = new long[tokenLen];
            for (int i = 0; i < tokenLen; i++)
            {
                inputIdsLong[i] = tokenIds[i];
                maskLong[i] = attentionMask[i];
            }

            using var inputIdsTensor = OrtValue.CreateTensorValueFromMemory(inputIdsLong, shape);
            using var maskTensor = OrtValue.CreateTensorValueFromMemory(maskLong, shape);

            var inputs = new Dictionary<string, OrtValue>
            {
                { "input_ids", inputIdsTensor },
                { "attention_mask", maskTensor },
            };

            using var outputs = _session.Run(new RunOptions(), inputs, _session.OutputNames);

            // 出力: [1, 1024, tokenLen] — そのまま dest にコピー
            var outputSpan = outputs[0].GetTensorDataAsSpan<float>();
            outputSpan.Slice(0, requiredLen).CopyTo(dest.AsSpan());
        }

        public float[] Run(int[] tokenIds, int[] attentionMask)
        {
            float[] result = new float[HiddenDim * tokenIds.Length];
            Run(tokenIds, attentionMask, result);
            return result;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _session = null;
                _disposed = true;
            }
        }
    }
}
#endif
