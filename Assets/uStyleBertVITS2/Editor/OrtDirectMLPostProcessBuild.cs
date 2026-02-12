#if USBV2_ORT_AVAILABLE
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace uStyleBertVITS2.Editor
{
    /// <summary>
    /// Windows x64 ビルド後に DirectML.dll をビルドルートにコピーする。
    /// onnxruntime.dll は LoadLibrary("DirectML.dll") で動的ロードするため、
    /// exe と同じディレクトリ (DLL 検索パス) に DirectML.dll が必要。
    /// Unity は Plugins/x86_64/ 配下の DLL を {BuildName}_Data/Plugins/x86_64/ に配置するが、
    /// exe のあるビルドルートには自動コピーしないため、ポストプロセスで補完する。
    /// </summary>
    public class OrtDirectMLPostProcessBuild : IPostprocessBuildWithReport
    {
        private const string DirectMLFileName = "DirectML.dll";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64)
                return;

            var buildDir = Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(buildDir))
                return;

            var destPath = Path.Combine(buildDir, DirectMLFileName);
            if (File.Exists(destPath))
                return;

            // Plugins/x86_64 内の DirectML.dll を探す
            var dataFolder = Path.Combine(buildDir,
                Path.GetFileNameWithoutExtension(report.summary.outputPath) + "_Data");
            var pluginSource = Path.Combine(dataFolder, "Plugins", "x86_64", DirectMLFileName);

            if (File.Exists(pluginSource))
            {
                File.Copy(pluginSource, destPath);
                Debug.Log($"[OrtDirectMLPostProcessBuild] Copied {DirectMLFileName} to build root: {destPath}");
            }
            else
            {
                Debug.LogWarning(
                    $"[OrtDirectMLPostProcessBuild] {DirectMLFileName} not found at {pluginSource}. " +
                    "DirectML acceleration will not be available.");
            }
        }
    }
}
#endif
