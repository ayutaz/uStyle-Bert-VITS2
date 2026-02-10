using UnityEditor;
using UnityEngine;

namespace uStyleBertVITS2.Editor
{
    /// <summary>
    /// ONNXファイルのインポート時に自動検証を行うエディタ拡張。
    /// </summary>
    public class ModelImportValidator : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (!assetPath.EndsWith(".onnx")) return;
            if (!assetPath.Contains("uStyleBertVITS2")) return;

            Debug.Log($"[uStyleBertVITS2] Importing ONNX model: {assetPath}");
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.EndsWith(".onnx")) continue;
                if (!path.Contains("uStyleBertVITS2") && !path.Contains("Models")) continue;

                Debug.Log($"[uStyleBertVITS2] ONNX model imported: {path}");

                // ModelAssetとしてロード可能か検証
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset == null)
                {
                    Debug.LogWarning(
                        $"[uStyleBertVITS2] Failed to load ONNX as asset: {path}. " +
                        "Ensure com.unity.ai.inference is installed.");
                }
            }
        }
    }
}
