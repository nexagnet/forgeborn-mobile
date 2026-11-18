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

            // First seconds in the bath: the blade is still hot, so it steams. Then the rest of the quench without vapour.
            sb.CaptureThermal(ForgeEnv.Water, 0.4);
            sb.CaptureSimulateSteam(1.0f);
            Shot(sb, cam, dir, "04b_quench_steam");
            sb.CaptureClearSteam();

            sb.CaptureThermal(ForgeEnv.Water, 29.6);
            Shot(sb, cam, dir, "05_quenched");

            sb.CaptureThermal(ForgeEnv.Oven, 3600.0);
            sb.CaptureShowReport(true, ToolMode.Grind);
            Shot(sb, cam, dir, "06_tempered_with_report");

            // Order flow: deliver the (ungrounded, quenched, tempered) blade to the journeyman client. Expected: rejected, with reasons.
            sb.CaptureSetOrder(1);
            sb.CaptureDeliver();
            Shot(sb, cam, dir, "08_order_rejected_with_reasons");
            sb.CaptureShowReport(false, ToolMode.Grind);

            // Input-path verification: synthetic screen points through the real camera-ray -> blade space -> Domain pipeline.
            // Screen coordinates are pixels of the render texture (bottom-left origin); the blade is centred at x = W/2.
            var rayRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rayRt;
            sb.CaptureShowReport(false, ToolMode.Grind);
            double removedKg = sb.CaptureGrindDrag(new Vector2(W * 0.5f, 520f), new Vector2(W * 0.5f, 700f), 40, 0.016f);
            Debug.Log("[Capture] grind drag removed " + (removedKg * 1000.0) + " g");
            sb.CaptureSimulateSparks(0.12f);
            Shot(sb, cam, dir, "07_after_grind_drag_with_sparks");

            cam.targetTexture = rayRt;
            sb.CaptureSetTool(ToolMode.Hammer);
            sb.CaptureHammerScreen(new Vector2(W * 0.5f, 800f));
            HammerResult screenHit = sb.LastHammer;
            Debug.Log("[Capture] screen hammer hit=" + (screenHit != null && screenHit.Hit));
            Object.DestroyImmediate(rayRt);

            bool ok = removedKg > 0.001 && screenHit != null && screenHit.Hit;
            Debug.Log(ok ? "[Capture] INPUT PATH OK" : "[Capture] INPUT PATH FAILED");
            Debug.Log("[Capture] done: " + dir);
            EditorApplication.Exit(ok ? 0 : 1);
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
