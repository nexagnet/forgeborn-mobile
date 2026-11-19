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
            string frames = ArgValue(args, "-fb-frames");
            if (shot == null && bench == null && frames == null) return;

            var go = new GameObject("ForgeBornAutomation");
            DontDestroyOnLoad(go);
            var pa = go.AddComponent<PlayerAutomation>();
            if (shot != null)
                pa.StartCoroutine(pa.RunScreenshot(shot, ParseFloat(ArgValue(args, "-fb-wait"), 3f)));
            else if (frames != null)
                pa.StartCoroutine(pa.RunFrames(frames, ParseFloat(ArgValue(args, "-fb-duration"), 8f), ParseFloat(ArgValue(args, "-fb-fps"), 4f), ArgValue(args, "-fb-scenario")));
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

        /// <summary>
        /// Saves frames while the real Update loop runs, so motion, glow, sparks and steam can be inspected afterwards
        /// (ForgeBornCapture runs in batchmode and never executes Update). Same drive as the benchmark: 3 s idle, then grinding.
        /// ForgeBorn.exe -fb-frames dir [-fb-duration 8] [-fb-fps 4]  ->  dir/f_0001.png ... and dir/frames.csv (index,seconds,phase).
        /// Frames are read back from the player's own render target, so nothing on the user's desktop is captured.
        /// </summary>
        private IEnumerator RunFrames(string dir, float loadSeconds, float fps, string scenario)
        {
            // scenario "forge": 3 s idle in air, then half the duration in the furnace, then the rest in water (time x60):
            // shows the furnace mouth glow, the heating colour and the quench steam. Default (null): grind as before.
            bool forge = string.Equals(scenario, "forge", StringComparison.OrdinalIgnoreCase);
            int stage = -1;
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.persistentDataPath, "frames");
            Directory.CreateDirectory(Path.GetFullPath(dir));

            ForgeSandbox sb = null;
            for (int i = 0; i < 120 && sb == null; i++)
            {
                sb = FindFirstObjectByType<ForgeSandbox>();
                if (sb == null) yield return null;
            }
            if (sb == null)
            {
                Debug.LogError("[Automation] frames: no ForgeSandbox in the scene");
                Application.Quit(1);
                yield break;
            }
            for (int i = 0; i < WarmupFrames; i++) yield return null;

            var csv = new System.Text.StringBuilder("index,seconds,phase,max_temp_c,unity_time_scale\n");
            float interval = 1f / Mathf.Max(0.5f, fps);
            float start = Time.realtimeSinceStartup;
            float total = IdleSeconds + loadSeconds;
            float nextShot = 0f;
            float w = Screen.width, h = Screen.height;
            bool up = true;
            int index = 0, code = 0;

            while (true)
            {
                float t = Time.realtimeSinceStartup - start;
                if (t >= total) break;
                bool grinding = t >= IdleSeconds && !forge;
                if (forge && t >= IdleSeconds)
                {
                    int want = t < IdleSeconds + loadSeconds * 0.5f ? 1 : 2;
                    if (want != stage)
                    {
                        stage = want;
                        sb.AutomationSetEnvAndSpeed(want == 1 ? ForgeEnv.Furnace : ForgeEnv.Water, want == 1 ? 60.0 : 1.0); // quench at x1: at x60 it ends inside one frame
                    }
                }
                if (grinding)
                {
                    float x0 = w * (up ? 0.40f : 0.60f), x1 = w * (up ? 0.60f : 0.40f);
                    sb.CaptureGrindDrag(new Vector2(x0, h * 0.5f), new Vector2(x1, h * 0.5f), 2, Mathf.Max(0.001f, Time.unscaledDeltaTime));
                    up = !up;
                }
                if (t >= nextShot)
                {
                    yield return new WaitForEndOfFrame();
                    Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        index++;
                        File.WriteAllBytes(Path.Combine(dir, "f_" + index.ToString("D4", CultureInfo.InvariantCulture) + ".png"), tex.EncodeToPNG());
                        csv.Append(index).Append(',').Append(F(t, 2)).Append(',').Append(forge ? (stage == 1 ? "furnace" : stage == 2 ? "water" : "idle") : grinding ? "grind" : "idle")
                           .Append(',').Append(F(SandboxRules.MaxTempK(sb.Session.Profile) - 273.15, 0)).Append(',').Append(F(Time.timeScale, 1)).Append('\n');
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[Automation] frame write failed: " + e.Message);
                        code = 1;
                    }
                    Destroy(tex);
                    if (code != 0) break;
                    nextShot += interval;
                }
                yield return null;
            }

            try { File.WriteAllText(Path.Combine(dir, "frames.csv"), csv.ToString()); }
            catch (Exception e) { Debug.LogError("[Automation] frames.csv failed: " + e.Message); code = 1; }
            Debug.Log("[Automation] frames: " + index + " -> " + Path.GetFullPath(dir));
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

            // ForgeSandbox.Initialize sets Application.targetFrameRate from its platform profile; undo that after it has run.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

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
                // Along the blade (it lies horizontally), same stroke as ForgeBornCapture; the old vertical stroke crossed the blade.
                float x0 = w * (up ? 0.40f : 0.60f), x1 = w * (up ? 0.60f : 0.40f);
                sb.CaptureGrindDrag(new Vector2(x0, h * 0.5f), new Vector2(x1, h * 0.5f), 2, Mathf.Max(0.001f, Time.unscaledDeltaTime));
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
