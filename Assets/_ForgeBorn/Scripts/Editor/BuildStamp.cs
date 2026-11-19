using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ForgeBorn.EditorTools
{
    /// <summary>
    /// Writes Assets/_ForgeBorn/Resources/buildinfo.txt before every player build, so the menu can show which build it is
    /// ("2026-11-19 11:12 | abc1234+"). The file is git-ignored: it changes on every build.
    /// </summary>
    public sealed class BuildStamp : IPreprocessBuildWithReport
    {
        private const string AssetPath = "Assets/_ForgeBorn/Resources/buildinfo.txt";

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " | " + GitStamp();
            string full = Path.GetFullPath(AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, line + "\n");
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
            UnityEngine.Debug.Log("[BuildStamp] " + line);
        }

        private static string GitStamp()
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
                string head = Run(root, "rev-parse --short HEAD").Trim();
                if (head.Length == 0) return "no-git";
                // Tracked files only: untracked scratch folders and the stamp file itself do not count as "dirty".
                string dirty = Run(root, "status --porcelain --untracked-files=no").Trim();
                return head + (dirty.Length > 0 ? "+" : "");
            }
            catch (Exception) { return "no-git"; }
        }

        private static string Run(string dir, string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return p.ExitCode == 0 ? o : "";
            }
        }
    }
}
