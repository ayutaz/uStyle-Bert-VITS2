using UnityEngine;

namespace uStyleBertVITS2.Diagnostics
{
    /// <summary>
    /// TTSパイプラインのデバッグログ制御。
    /// Enabled を true にすると各ステージの詳細をコンソールに出力する。
    /// </summary>
    public static class TTSDebugLog
    {
        /// <summary>true にするとパイプライン各段の詳細をコンソールに出力する。</summary>
        public static bool Enabled { get; set; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Log(string tag, string message)
        {
            if (Enabled) Debug.Log($"[{tag}] {message}");
        }
    }
}
