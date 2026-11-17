using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ForgeBorn.EditorTools
{
    /// <summary>
    /// Idempotent project setup and build entry points (also callable with -executeMethod in batchmode).
    /// </summary>
    public static class ForgeBornSetup
    {
        private const string SettingsDir = "Assets/_ForgeBorn/Settings";
        private const string ResourcesDir = "Assets/_ForgeBorn/Resources";
        private const string ScenePath = "Assets/_ForgeBorn/Scenes/Sandbox.unity";

        [MenuItem("ForgeBorn/Setup Project")]
        public static void Run()
        {
            EnsureDir(SettingsDir);
            EnsureDir(ResourcesDir);
            EnsureDir("Assets/_ForgeBorn/Scenes");

            SetupUrp();
            SetupBladeMaterial();
            SetupPlayerSettings();
            SetupScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ForgeBorn] Setup complete.");
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static void SetupUrp()
        {
            string rendererPath = SettingsDir + "/ForgeBorn_Renderer.asset";
            string assetPath = SettingsDir + "/ForgeBorn_URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
        }

        private static void SetupBladeMaterial()
        {
            string path = ResourcesDir + "/BladeMat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh);
            mat.SetColor("_BaseColor", new Color(0.66f, 0.68f, 0.72f));
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Smoothness", 0.55f);
            AssetDatabase.CreateAsset(mat, path);
        }

        private static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "nexagnet";
            PlayerSettings.productName = "Forgeborn Mobile";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nexagnet.forgeborn");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Android 10 (design target; verify on device)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            // Unity Remote 5 dev loop (iPhone over USB). Performance seen through Remote is NOT device performance.
            EditorSettings.unityRemoteDevice = "Any iOS Device";
            EditorSettings.unityRemoteCompression = "JPEG";
            EditorSettings.unityRemoteResolution = "Normal";
            EditorSettings.unityRemoteJoystickSource = "Remote";
        }

        private static void SetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>Forces the Unity Remote device to iOS even if the Project Settings dropdown does not list it.</summary>
        [MenuItem("ForgeBorn/Set Remote iPhone")]
        public static void SetRemoteIphone()
        {
            EditorSettings.unityRemoteDevice = "Any iOS Device";
            Debug.Log("[ForgeBorn] unityRemoteDevice = '" + EditorSettings.unityRemoteDevice + "'");
        }

        /// <summary>Opens the sandbox scene and enters Play mode (use with Unity Remote 5 connected).</summary>
        [MenuItem("ForgeBorn/Play With Remote")]
        public static void PlayWithRemote()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void BuildAndroidDebug()
        {
            Run();
            EnsureDir("Builds");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/forgeborn-debug.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[ForgeBorn] Android build result: " + report.summary.result + ", size " + report.summary.totalSize);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
