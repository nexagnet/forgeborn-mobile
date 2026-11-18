using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Command-line automation for the standalone player, so a built .exe can be checked without a person at the keyboard.
    /// Screenshot: ForgeBorn.exe -fb-screenshot out.png [-fb-wait 3] [-screen-width 1920 -screen-height 1080]
    /// Benchmark:  ForgeBorn.exe -fb-benchmark 8 [-fb-out bench.json]   (3 s idle, then 8 s of grinding load)
    /// The benchmark turns vSync and the frame cap off so the numbers show real cost. Numbers are labelled with the device
    /// they were taken on and must never be quoted for another device.
    /// </summary>
    public sealed class PlayerAutomation : MonoBehaviour
    {
        private const int WarmupFrames = 60;
        private const float IdleSeconds = 3f;

        [Serializable]
        public sealed class PhaseStats
        {
            public string name;
            public int frames;
            public double avgMs, p50Ms, p95Ms, p99Ms, maxMs, fps;
        }

        [Serializable]
        public sealed class BenchReport
        {
            public string device, api, cpu, quality;
            public int ramMb, width, height, vsync;
            public double massRemovedGrams;
            public PhaseStats[] phases;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            string shot = ArgValue(args, "-fb-screenshot");
            string bench = ArgValue(args, "-fb-benchmark");
            if (shot == null && bench == null) return;

            var go = new GameObject("ForgeBornAutomation");
            DontDestroyOnLoad(go);
            var pa = go.AddComponent<PlayerAutomation>();
            if (shot != null)
                pa.StartCoroutine(pa.RunScreenshot(shot, ParseFloat(ArgValue(args, "-fb-wait"), 3f)));
            else
                pa.StartCoroutine(pa.RunBenchmark(ParseFloat(bench, 8f), ArgValue(args, "-fb-out")));
        }

        private static string ArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal) ? args[i + 1] : "";
            return null;
        }

        private static float ParseFloat(string s, float fallback)
        {
            return !string.IsNullOrEmpty(s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v > 0f ? v : fallback;
        }

        private IEnumerator RunScreenshot(string path, float waitSeconds)
        {
            if (string.IsNullOrEmpty(path)) path = Path.Combine(Application.persistentDataPath, "screenshot.png");
            float until = Time.realtimeSinceStartup + waitSeconds;
            while (Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            int code = 0;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("[Automation] screenshot " + tex.width + "x" + tex.height + " -> " + Path.GetFullPath(path));
            }
            catch (Exception e)
            {
                Debug.LogError("[Automation] screenshot failed: " + e.Message);
                code = 1;
            }
            Destroy(tex);
            Application.Quit(code);
        }

        private IEnumerator RunBenchmark(float loadSeconds, string outPath)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            ForgeSandbox sb = null;
            for (int i = 0; i < 120 && sb == null; i++)
            {
                sb = FindFirstObjectByType<ForgeSandbox>();
                if (sb == null) yield return null;
            }
            if (sb == null)
            {
                Debug.LogError("[Automation] benchmark: no ForgeSandbox in the scene");
                Application.Quit(1);
                yield break;
            }

            for (int i = 0; i < WarmupFrames; i++) yield return null;

            double massBefore = sb.Session.MassKg;
            var idle = new List<float>();
            var load = new List<float>();

            float end = Time.realtimeSinceStartup + IdleSeconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                idle.Add(Time.unscaledDeltaTime * 1000f);
            }

            float w = Screen.width, h = Screen.height;
            end = Time.realtimeSinceStartup + loadSeconds;
            bool up = true;
            while (Time.realtimeSinceStartup < end)
            {
                float y0 = h * (up ? 0.42f : 0.62f), y1 = h * (up ? 0.62f : 0.42f);
                sb.CaptureGrindDrag(new Vector2(w * 0.5f, y0), new Vector2(w * 0.5f, y1), 2, Mathf.Max(0.001f, Time.unscaledDeltaTime));
                up = !up;
                yield return null;
                load.Add(Time.unscaledDeltaTime * 1000f);
            }

            var report = new BenchReport
            {
                device = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceType.ToString(),
                cpu = SystemInfo.processorType,
                ramMb = SystemInfo.systemMemorySize,
                width = Screen.width,
                height = Screen.height,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                vsync = QualitySettings.vSyncCount,
                massRemovedGrams = (massBefore - sb.Session.MassKg) * 1000.0,
                phases = new[] { Summarize("idle", idle), Summarize("grind", load) }
            };

            if (string.IsNullOrEmpty(outPath)) outPath = Path.Combine(Application.persistentDataPath, "benchmark.json");
            int code = 0;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
                foreach (PhaseStats p in report.phases)
                    Debug.Log("[Benchmark] " + p.name + ": avg " + F(p.avgMs, 2) + " ms, p95 " + F(p.p95Ms, 2) + " ms, max " + F(p.maxMs, 2) +
                              " ms, " + F(p.fps, 1) + " fps (" + p.frames + " frames) on " + report.device);
                Debug.Log("[Benchmark] written " + Path.GetFullPath(outPath));
            }
            catch (Exception e)
            {
                Debug.LogError("[Automation] benchmark write failed: " + e.Message);
                code = 1;
            }
            Application.Quit(code);
        }

        private static string F(double v, int digits) { return v.ToString("F" + digits, CultureInfo.InvariantCulture); }

        private static PhaseStats Summarize(string name, List<float> ms)
        {
            var s = new PhaseStats { name = name, frames = ms.Count };
            if (ms.Count == 0) return s;
            double sum = 0;
            foreach (float v in ms) sum += v;
            ms.Sort();
            s.avgMs = sum / ms.Count;
            s.p50Ms = ms[ms.Count / 2];
            s.p95Ms = ms[Mathf.Min(ms.Count - 1, (int)(ms.Count * 0.95))];
            s.p99Ms = ms[Mathf.Min(ms.Count - 1, (int)(ms.Count * 0.99))];
            s.maxMs = ms[ms.Count - 1];
            s.fps = s.avgMs > 0 ? 1000.0 / s.avgMs : 0;
            return s;
        }
    }
}
