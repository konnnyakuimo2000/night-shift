using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// リリース版のビルド（Windows 64bit）。メニュー「NightShift > リリース版をビルド」、または
    /// コマンドライン: Unity.exe -batchmode -quit -projectPath ... -executeMethod NightShift.EditorTools.ReleaseBuild.BuildWindows
    /// 出力先: プロジェクト直下の Builds/NIGHT_SHIFT_v{版}/NIGHT_SHIFT.exe（Builds は git の管理外）
    /// </summary>
    public static class ReleaseBuild
    {
        public const string Version = "1.1.0";

        [MenuItem("NightShift/リリース版をビルド（Windows）")]
        public static void BuildWindows()
        {
            PlayerSettings.bundleVersion = Version;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "NIGHT_SHIFT_v" + Version));
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(root, "NIGHT_SHIFT.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[ReleaseBuild] {summary.result} / {summary.totalSize / (1024 * 1024)} MB / {summary.totalErrors} errors / {root}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
