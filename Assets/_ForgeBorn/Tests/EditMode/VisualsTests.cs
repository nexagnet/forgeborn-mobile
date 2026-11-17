using System.Collections.Generic;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Session;
using ForgeBorn.UnityLayer;
using NUnit.Framework;
using UnityEngine;

namespace ForgeBorn.Tests
{
    public class VisualsTests
    {
        [Test]
        public void Emission_IsZeroWhenCold_AndBrighterWhenHotter()
        {
            Assert.AreEqual(Vector3.zero, BladeVisuals.Emission(293f));
            Assert.AreEqual(Vector3.zero, BladeVisuals.Emission(700f));
            float prev = 0f;
            for (float t = 750f; t <= 1700f; t += 50f)
            {
                Vector3 e = BladeVisuals.Emission(t);
                float lum = e.x + e.y + e.z;
                Assert.Greater(lum, prev, "glow must increase with temperature at " + t);
                prev = lum;
            }
            Assert.AreEqual(BladeVisuals.Emission(1700f), BladeVisuals.Emission(2500f));
        }

        [Test]
        public void Emission_StartsRed_EndsNearWhite()
        {
            Vector3 dull = BladeVisuals.Emission(850f);
            Assert.Greater(dull.x, dull.y * 3f, "dull red glow is mostly red");
            Vector3 hot = BladeVisuals.Emission(1700f);
            Assert.Greater(hot.z, 1.0f, "very hot steel is near white");
        }

        [Test]
        public void OxideTint_IsWhiteUntempered_StrawAtLowTemper_BlueAtHigher()
        {
            Assert.AreEqual(Vector3.one, BladeVisuals.OxideTint(0f));
            Vector3 straw = BladeVisuals.OxideTint(10500f);
            Assert.Greater(straw.x, straw.z, "straw is yellowish (red > blue)");
            Vector3 blue = BladeVisuals.OxideTint(13500f);
            Assert.Greater(blue.z, blue.x, "blue oxide has blue > red");
        }

        [Test]
        public void Tint_CrackedIsDarkerWithFullAlpha()
        {
            Color32 ok = BladeVisuals.Tint(0f, 0f);
            Color32 cracked = BladeVisuals.Tint(0f, 1.2f);
            Assert.AreEqual(255, ok.r);
            Assert.AreEqual(0, ok.a);
            Assert.Less(cracked.r, ok.r);
            Assert.AreEqual(255, cracked.a);
        }

        [Test]
        public void PlatformProfile_IosAndAndroidAreSeparateAndEditorIsMarkedSimulated()
        {
            PlatformProfile ios = PlatformProfile.ForPlatform(RuntimePlatform.IPhonePlayer, 6000);
            Assert.AreEqual("ios", ios.Name);
            Assert.AreEqual(QualityTier.High, ios.Tier);
            Assert.AreEqual(60, ios.TargetFps);
            Assert.IsFalse(ios.Simulated);

            Assert.AreEqual(QualityTier.Low, PlatformProfile.ForPlatform(RuntimePlatform.Android, 2000).Tier);
            Assert.AreEqual(QualityTier.Medium, PlatformProfile.ForPlatform(RuntimePlatform.Android, 4000).Tier);
            Assert.AreEqual(QualityTier.High, PlatformProfile.ForPlatform(RuntimePlatform.Android, 8000).Tier);
            Assert.Less(PlatformProfile.ForPlatform(RuntimePlatform.Android, 2000).MaxSparkParticles,
                PlatformProfile.ForPlatform(RuntimePlatform.Android, 8000).MaxSparkParticles);

            PlatformProfile ed = PlatformProfile.ForPlatform(RuntimePlatform.WindowsEditor, 16000);
            Assert.IsTrue(ed.Simulated, "Editor / Remote profile must be flagged as simulated, never as device evidence");
        }
    }

    public class BentMeshTests
    {
        private static BladeProfile Bent()
        {
            BladeProfile p = BladeFactory.CreateTapered(0.8, x => 0.025 - 0.010 * x / 0.8, x => 0.004 - 0.002 * x / 0.8, 192, 48);
            BladeFactory.ApplyFrame(p,
                x => 0.02 * System.Math.Sin(System.Math.PI * x / 0.8),
                x => 0.01 * System.Math.Sin(2.0 * System.Math.PI * x / 0.8),
                x => 0.5 * x / 0.8);
            return p;
        }

        private static double MeshVolume(Mesh m)
        {
            Vector3[] v = m.vertices;
            int[] t = m.triangles;
            double vol = 0.0;
            for (int i = 0; i < t.Length; i += 3)
                vol += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]]));
            return vol / 6.0;
        }

        [Test]
        public void BentTwistedBlade_VerticesMatchDomainWorldPoints()
        {
            BladeProfile p = Bent();
            var b = new BladeMeshBuilder(p);
            Vector3[] v = b.Mesh.vertices;
            foreach (int s in new[] { 0, 60, 120, 191 })
            {
                for (int j = 0; j < p.PerimeterCount; j += 7)
                {
                    Vec2d w = p.WorldPoint(s, j);
                    Vector3 got = v[s * p.PerimeterCount + j];
                    Assert.AreEqual((float)w.X, got.y, 1e-5f);
                    Assert.AreEqual((float)w.Y, got.z, 1e-5f);
                    Assert.AreEqual((float)p.X[s], got.x, 1e-6f);
                }
            }
        }

        [Test]
        public void BentTwistedBlade_MeshVolumeStaysWithinTwoPercentOfDomain()
        {
            BladeProfile p = Bent();
            var b = new BladeMeshBuilder(p);
            double mv = MeshVolume(b.Mesh);
            Assert.Greater(mv, 0.0, "winding must stay outward");
            Assert.AreEqual(p.VolumeM3(), mv, p.VolumeM3() * 0.02);
        }

        [Test]
        public void RefreshState_ShowsGlowForHotStationsAndNothingForColdOnes()
        {
            var session = new ForgeSession(BladeFactory.CreateTapered(0.8, x => 0.02, x => 0.004, 64, 16), MaterialData.SteelBasicV1);
            for (int s = 0; s < 32; s++) session.Profile.TempK[s] = 1300.0;
            var b = new BladeMeshBuilder(session.Profile);
            b.RefreshState();

            var glow = new List<Vector3>();
            b.Mesh.GetUVs(1, glow);
            int pc = session.Profile.PerimeterCount;
            Assert.Greater(glow[5 * pc].x, 1.0f, "hot station glows");
            Assert.AreEqual(0f, glow[50 * pc].x, "cold station does not glow");
        }

        [Test]
        public void RefreshState_MarksCrackedStationsInVertexAlpha()
        {
            var session = new ForgeSession(BladeFactory.CreateTapered(0.8, x => 0.02, x => 0.004, 64, 16), MaterialData.SteelBasicV1);
            session.Profile.Damage[10] = 1.5;
            var b = new BladeMeshBuilder(session.Profile);
            Color32[] c = b.Mesh.colors32;
            int pc = session.Profile.PerimeterCount;
            Assert.AreEqual(255, c[10 * pc].a);
            Assert.AreEqual(0, c[20 * pc].a);
        }

        [Test]
        public void GrindingStillUpdatesMeshOnABentBlade()
        {
            BladeProfile p = Bent();
            var session = new ForgeSession(p, MaterialData.SteelBasicV1);
            var b = new BladeMeshBuilder(session.Profile);
            double before = MeshVolume(b.Mesh);

            BladeHit hit;
            Assert.IsTrue(BladeRaycaster.Raycast(p, new Vec3d(0.4, 0.0, 1.0), new Vec3d(0, 0, -1), 5.0, out hit));
            var samples = new List<GrindSample>();
            for (int i = 0; i < 40; i++)
                samples.Add(new GrindSample(new Vec3d(0.4, hit.PointYZ.X, 1.0), new Vec3d(0, 0, -1), 80.0, 0.01));
            GrindResult r = session.ApplyGrind(new GrindStrokeCommand(1, 0.0, AbrasiveWheel.Medium, 3000.0, samples));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.Greater(r.RemovedVolumeM3, 0.0);

            b.Rebuild(r.FirstStation, r.LastStation);
            Assert.Less(MeshVolume(b.Mesh), before);
        }
    }
}
