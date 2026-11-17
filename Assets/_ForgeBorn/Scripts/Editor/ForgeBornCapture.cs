using System.IO;
using ForgeBorn.Domain.Commands;
using ForgeBorn.UnityLayer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ForgeBorn.EditorTools
{
    /// <summary>
    /// Headless screenshots: builds the sandbox without Play mode, drives it through scripted states and renders each state to a PNG.
    /// Run: Unity.exe -batchmode -projectPath . -executeMethod ForgeBorn.EditorTools.ForgeBornCapture.Run -logFile build/out/capture.log
    /// (do NOT pass -nographics: rendering needs a GPU device). Output: build/out/shots/*.png. PC GPU render, not a device image.
    /// </summary>
    public static class ForgeBornCapture
    {
        private const int W = 540, H = 1170; // portrait, iPhone-like aspect

        public static void Run()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../build/out/shots"));
            Directory.CreateDirectory(dir);

            var go = new GameObject("ForgeSandbox");
            var sb = go.AddComponent<ForgeSandbox>();
            sb.Initialize();

            Camera cam = Camera.main;
            // Overlay canvases are not drawn into a render texture: bind the HUD to the camera for the capture.
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.scaleFactor = W / 390f; // emulate a 390 pt wide iPhone: UI sized in dp like on the device
            }

            Shot(sb, cam, dir, "01_cold_blank");

            sb.CaptureThermal(ForgeEnv.Furnace, 20.0);
            Shot(sb, cam, dir, "02_furnace_20s_dull_red");

            sb.CaptureThermal(ForgeEnv.Furnace, 70.0);
            Shot(sb, cam, dir, "03_furnace_90s_hot");

            HammerResult r = sb.CaptureHammer(0.4, 0.0, 120.0);
            Debug.Log("[Capture] hammer hit=" + r.Hit + " stations=" + r.StationsTouched + " depthMm=" + (r.DepthM * 1000.0) + " tempC=" + (r.HitTempK - 273.15));
            r = sb.CaptureHammer(0.4, 0.0, 120.0);
            Shot(sb, cam, dir, "04_after_two_hot_blows");

            sb.CaptureThermal(ForgeEnv.Water, 30.0);
            Shot(sb, cam, dir, "05_quenched");

            sb.CaptureThermal(ForgeEnv.Oven, 3600.0);
            sb.CaptureShowReport(true, ToolMode.Grind);
            Shot(sb, cam, dir, "06_tempered_with_report");

            Debug.Log("[Capture] done: " + dir);
            EditorApplication.Exit(0);
        }

        private static void Shot(ForgeSandbox sb, Camera cam, string dir, string name)
        {
            sb.CaptureRefreshHud();
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("[Capture] wrote " + name);
        }
    }
}
