using UnityEditor;
using UnityEngine;
using uStyleBertVITS2.Configuration;

namespace uStyleBertVITS2.Editor
{
    /// <summary>
    /// TTSSettings のカスタムInspector。
    /// モデルの有無やパスの検証を表示する。
    /// </summary>
    [CustomEditor(typeof(TTSSettings))]
    public class TTSSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var settings = (TTSSettings)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

            // モデルアセット検証
            ValidateModelAsset("BERT Model", settings.BertModel);
            ValidateModelAsset("TTS Model", settings.TTSModel);

            // パス検証
            ValidatePath("Dictionary", settings.DictionaryPath);
            ValidatePath("Vocab", settings.VocabPath);
            ValidatePath("Style Vectors", settings.StyleVectorPath);
        }

        private static void ValidateModelAsset(string label, Object asset)
        {
            if (asset == null)
            {
                EditorGUILayout.HelpBox($"{label}: Not assigned", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox($"{label}: OK", MessageType.Info);
            }
        }

        private static void ValidatePath(string label, string relativePath)
        {
            string fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);
            bool exists = System.IO.File.Exists(fullPath) || System.IO.Directory.Exists(fullPath);

            if (!exists)
            {
                EditorGUILayout.HelpBox(
                    $"{label}: Not found at StreamingAssets/{relativePath}",
                    MessageType.Warning);
            }
        }
    }
}
