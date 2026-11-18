using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ForgeBorn.EditorTools
{
    /// <summary>
    /// Builds the standalone Windows player to build/out/win/ForgeBorn.exe and writes build/out/win_build.txt (result, time, size, errors).
    /// Menu: ForgeBorn > Build Windows. Batch: Unity.exe -batchmode -projectPath . -executeMethod ForgeBorn.EditorTools.ForgeBornBuild.BuildWindows
    /// The Editor build uses the Mono backend (no IL2CPP module for Windows is installed on the dev PC).
    /// </summary>
    public static class ForgeBornBuild
    {
        [MenuItem("ForgeBorn/Build Windows")]
        public static void BuildWindows()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(root, "build", "out", "win");
            string reportPath = Path.Combine(root, "build", "out", "win_build.txt");
            Directory.CreateDirectory(outDir);

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(outDir, "ForgeBorn.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var sb = new StringBuilder();
            int code;
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary s = report.summary;
                sb.AppendLine("result: " + s.result);
                sb.AppendLine("time: " + s.totalTime.TotalSeconds.ToString("F1") + " s");
                sb.AppendLine("size_mb: " + (s.totalSize / 1048576.0).ToString("F1"));
                sb.AppendLine("errors: " + s.totalErrors + " warnings: " + s.totalWarnings);
                sb.AppendLine("output: " + s.outputPath);
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception)
                            sb.AppendLine("ERROR: " + m.content);
                code = s.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                sb.AppendLine("result: Exception");
                sb.AppendLine(e.ToString());
                code = 1;
            }

            File.WriteAllText(reportPath, sb.ToString());
            Debug.Log("[Build] " + sb.ToString().Replace("\n", " | "));
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
