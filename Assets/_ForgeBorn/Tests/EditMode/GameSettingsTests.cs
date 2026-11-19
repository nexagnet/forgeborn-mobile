using ForgeBorn.UnityLayer;
using NUnit.Framework;
using UnityEngine;

namespace ForgeBorn.Tests
{
    public class GameSettingsTests
    {
        [Test]
        public void RoundTrips()
        {
            var s = new GameSettings { ResolutionIndex = 2, Fullscreen = true, Tier = 0, Volume = 0.4f };
            GameSettings r = GameSettings.Parse(s.Serialize());
            Assert.AreEqual(2, r.ResolutionIndex);
            Assert.IsTrue(r.Fullscreen);
            Assert.AreEqual(0, r.Tier);
            Assert.AreEqual(0.4f, r.Volume, 0.001f);
        }

        [Test]
        public void AssistLevel_RoundTrips_DefaultsToLight_AndOldSettingsKeepIt()
        {
            Assert.AreEqual(1, new GameSettings().Assist);
            Assert.AreEqual(2, GameSettings.Parse(new GameSettings { Assist = 2 }.Serialize()).Assist);
            Assert.AreEqual(0, GameSettings.Parse(new GameSettings { Assist = 0 }.Serialize()).Assist);
            Assert.AreEqual(1, GameSettings.Parse("v1;res=1;fs=0;tier=-1;vol=1.00;lang=1").Assist); // saved before the setting existed
            Assert.AreEqual(2, GameSettings.Parse("v1;assist=9").Assist);
            Assert.AreEqual(0, GameSettings.Parse("v1;assist=-4").Assist);
        }

        [Test]
        public void GarbageAndOutOfRangeFallBackSafely()
        {
            GameSettings d = GameSettings.Parse(null);
            Assert.AreEqual(0, d.ResolutionIndex);
            Assert.AreEqual(-1, d.Tier);
            Assert.AreEqual(1f, d.Volume);

            GameSettings g = GameSettings.Parse("not settings at all");
            Assert.AreEqual(-1, g.Tier);

            GameSettings o = GameSettings.Parse("v1;res=99;fs=1;tier=7;vol=5.0;res=abc");
            Assert.AreEqual(GameSettings.Resolutions.Length - 1, o.ResolutionIndex);
            Assert.AreEqual(2, o.Tier);
            Assert.AreEqual(1f, o.Volume);
        }

        [Test]
        public void SandboxBuildsAndMainMenuPausesAndResumes()
        {
            // Smoke test for the whole HUD + menu build (a null settings object once broke this and no other test saw it).
            var before = new System.Collections.Generic.HashSet<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
            var go = new GameObject("ForgeSandboxTest");
            try
            {
                var sb = go.AddComponent<ForgeSandbox>();
                sb.Initialize();
                sb.ShowMainMenu();
                Assert.IsTrue(sb.Paused);
                Assert.AreEqual(0f, Time.timeScale);
                sb.SetPaused(false);
                Assert.IsFalse(sb.Paused);
                Assert.AreEqual(1f, Time.timeScale);
                sb.SetPaused(true);
                Assert.IsTrue(sb.Paused);
            }
            finally
            {
                Time.timeScale = 1f;
                // Remove only what the sandbox created; never touch objects that were already in the scene.
                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (!before.Contains(root)) Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WithTierChangesQualityNotPlatform()
        {
            PlatformProfile win = PlatformProfile.ForPlatform(RuntimePlatform.WindowsPlayer, 16000);
            PlatformProfile low = PlatformProfile.WithTier(win, QualityTier.Low);
            Assert.AreEqual("windows", low.Name);
            Assert.AreEqual(QualityTier.Low, low.Tier);
            Assert.Less(low.MaxSparkParticles, win.MaxSparkParticles);
            Assert.IsFalse(low.BrushedSteel);
            Assert.IsFalse(low.Simulated);
            Assert.AreEqual(win.TargetFps, low.TargetFps);
        }
    }
}
