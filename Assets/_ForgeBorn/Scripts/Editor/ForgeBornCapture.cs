using System.IO;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
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
        // Landscape 16:9. Override with: -fb-size 1280x720
        private static int W = 1920, H = 1080;

        private static void ReadSizeArg()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-fb-size") continue;
                string[] p = args[i + 1].ToLowerInvariant().Split('x');
                int w, h;
                if (p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w >= 320 && h >= 240) { W = w; H = h; }
            }
        }

        public static void Run()
        {
            ReadSizeArg();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../build/out/shots"));
            Directory.CreateDirectory(dir);

            var go = new GameObject("ForgeSandbox");
            var sb = go.AddComponent<ForgeSandbox>();
            Loc.Current = Language.Vietnamese; // the game is Vietnamese-first; Initialize only sets it while playing
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
                if (scaler != null)
                {
                    // A render texture is not the Screen, so ScaleWithScreenSize would use the window size: scale to the reference by hand.
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = Mathf.Min(W / 1920f, H / 1080f) * 0.5f + Mathf.Max(W / 1920f, H / 1080f) * 0.5f;
                }
            }

            Shot(sb, cam, dir, "01_cold_blank");

            sb.CaptureThermal(ForgeEnv.Furnace, 20.0);
            Shot(sb, cam, dir, "02_furnace_20s_dull_red");

            sb.CaptureThermal(ForgeEnv.Furnace, 70.0);
            Shot(sb, cam, dir, "03_furnace_90s_hot");
            sb.CaptureShowReport(false, ToolMode.Hammer);
            sb.CaptureRefreshAdvice();
            Debug.Log("[Capture] advice (hammer, hot): " + sb.CaptureAdviceText().Replace("\n", " | "));
            Shot(sb, cam, dir, "10_advice_hammer_hot");
            sb.CaptureShowReport(false, ToolMode.Grind);

            HammerResult r = sb.CaptureHammer(0.4, 0.0, 120.0);
            Debug.Log("[Capture] hammer hit=" + r.Hit + " stations=" + r.StationsTouched + " depthMm=" + (r.DepthM * 1000.0) + " tempC=" + (r.HitTempK - 273.15));
            r = sb.CaptureHammer(0.4, 0.0, 120.0);
            Shot(sb, cam, dir, "04_after_two_hot_blows");

            // First seconds in the bath: the blade is still hot, so it steams. Then the rest of the quench without vapour.
            sb.CaptureThermal(ForgeEnv.Water, 0.4);
            sb.CaptureSimulateSteam(1.0f);
            sb.CaptureRefreshAdvice();
            Debug.Log("[Capture] advice (quench): " + sb.CaptureAdviceText().Replace("\n", " | "));
            Shot(sb, cam, dir, "04b_quench_steam");
            sb.CaptureClearSteam();

            sb.CaptureThermal(ForgeEnv.Water, 1.6);
            sb.CaptureRefreshAdvice();
            Debug.Log("[Capture] advice (quench, 2 s in): " + sb.CaptureAdviceText().Replace("\n", " | "));
            Shot(sb, cam, dir, "11_advice_quench_rate");

            sb.CaptureThermal(ForgeEnv.Water, 28.0);
            sb.CaptureRefreshAdvice();
            Debug.Log("[Capture] advice (quenched): " + sb.CaptureAdviceText().Replace("\n", " | "));
            Shot(sb, cam, dir, "05_quenched");

            sb.CaptureThermal(ForgeEnv.Oven, 3600.0);
            sb.CaptureShowReport(true, ToolMode.Grind);
            Shot(sb, cam, dir, "06_tempered_with_report");

            // Order flow: deliver the (ungrounded, quenched, tempered) blade to the journeyman client. Expected: rejected, with reasons.
            sb.CaptureSetOrder(1);
            sb.CaptureRefreshGoals();
            Shot(sb, cam, dir, "09_order_goals_checklist");
            sb.CaptureDeliver();
            Shot(sb, cam, dir, "08_order_rejected_with_reasons");
            sb.CaptureShowReport(false, ToolMode.Grind);

            // Input-path verification: synthetic screen points through the real camera-ray -> blade space -> Domain pipeline.
            // Screen coordinates are pixels of the render texture (bottom-left origin); the blade lies along the horizontal centre line.
            var rayRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rayRt;
            sb.CaptureShowReport(false, ToolMode.Grind);
            double removedKg = sb.CaptureGrindDrag(new Vector2(W * 0.40f, H * 0.5f), new Vector2(W * 0.60f, H * 0.5f), 40, 0.016f);
            Debug.Log("[Capture] grind drag removed " + (removedKg * 1000.0) + " g");
            sb.CaptureSimulateSparks(0.12f);
            Shot(sb, cam, dir, "07_after_grind_drag_with_sparks");

            cam.targetTexture = rayRt;
            sb.CaptureSetTool(ToolMode.Hammer);
            sb.CaptureHammerScreen(new Vector2(W * 0.5f, H * 0.5f));
            HammerResult screenHit = sb.LastHammer;
            Debug.Log("[Capture] screen hammer hit=" + (screenHit != null && screenHit.Hit));
            Object.DestroyImmediate(rayRt);

            // Angle-held grinding through the real pointer path: a fresh blank, coarse wheel at 150 N, angle 20, both faces, edge at 2 mm.
            sb.CaptureNewWeapon();
            sb.CaptureSetupGrind(0, 150f);
            sb.CaptureSetGrindAngle(20.0);
            sb.CaptureShowReport(false, ToolMode.Grind);
            var facetRt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = facetRt;
            for (int round = 1; round <= 6; round++)
            {
                for (int face = 0; face < 2; face++)
                {
                    sb.CaptureGrindDrag(sb.CaptureEdgeScreen(0.1, 0.002), sb.CaptureEdgeScreen(0.7, 0.002), 150, 0.016f);
                    sb.CaptureFlip();
                }
                BladeReport fr = sb.Session.Evaluate(null);
                Debug.Log("[Capture] facet round " + round + ": angle " + fr.EdgeAngleDeg.ToString("F1") + " asymmetry " + fr.BevelAsymmetry.ToString("F2") + " mass " + (sb.Session.MassKg * 1000.0).ToString("F0") + " g");
            }
            BladeReport facet = sb.Session.Evaluate(null);
            Object.DestroyImmediate(facetRt);
            cam.targetTexture = null;
            Debug.Log("[Capture] FACET angle " + facet.EdgeAngleDeg.ToString("F1") + " (set 20)");
            Shot(sb, cam, dir, "12_facet_bevel_both_faces");

            // Mandatory first-order guide (74-27): one screenshot per step, in the state a first-time player sees it.
            string tdir = Path.Combine(dir, "tutorial");
            Directory.CreateDirectory(tdir);
            sb.CaptureNewWeapon();
            sb.CaptureShowReport(false, ToolMode.Grind);
            var tut = new TutorialFlow();
            int heatAt = tut.IndexOf("heat");
            bool heated = false;
            for (int i = 0; i < tut.Count; i++)
            {
                string id = tut.Steps[i].Id;
                if (id == "furnace") { sb.CaptureSetOrder(0); sb.CaptureRefreshGoals(); }
                if (!heated && i > heatAt) { sb.CaptureThermal(ForgeEnv.Furnace, 90.0); heated = true; }
                bool hammerTool = id == "strike" || id == "to_grind"; // the tool the step expects the player to be holding when it appears
                sb.CaptureShowReport(id == "report" || id == "deliver" || id == "done", hammerTool ? ToolMode.Hammer : ToolMode.Grind);
                sb.CaptureTutorialStep(id, true);
                Shot(sb, cam, tdir, (i + 1).ToString("D2") + "_" + id);
            }

            sb.CaptureTutorialStep("welcome", true);
            sb.ShowMainMenu();
            Shot(sb, cam, dir, "13_main_menu_build_stamp");

            bool ok = removedKg > 0.001 && screenHit != null && screenHit.Hit && facet.HasBevel && System.Math.Abs(facet.EdgeAngleDeg - 20.0) < 4.0;
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
