using System;
using System.Collections.Generic;
using System.IO;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Persistence;
using ForgeBorn.Domain.Session;
using Xunit;

namespace ForgeBorn.Domain.Tests
{
    internal static class H
    {
        private static long _id = 1000;
        public static long NextId() { return ++_id; }

        public static BladeProfile Blank() { return BladeFactory.CreateRectBlank(0.7, 0.05, 0.006, 257, 48); }
        public static ForgeSession Session() { return new ForgeSession(Blank(), MaterialData.SteelBasicV1); }

        /// <summary>Ray from above (+Z) pointing down onto the top face at (x, y).</summary>
        public static GrindSample Top(double x, double y, double pressureN, double dt)
        {
            return new GrindSample(new Vec3d(x, y, 0.1), new Vec3d(0, 0, -1), pressureN, dt);
        }

        public static GrindStrokeCommand Cmd(params GrindSample[] samples)
        {
            return new GrindStrokeCommand(NextId(), 0.0, AbrasiveWheel.Coarse, 3000.0, samples);
        }

        public static double[] Copy(double[] a) { return (double[])a.Clone(); }

        /// <summary>Index of the top-face vertex at y = 0 for P=48 rings (side 3, i = 6).</summary>
        public const int TopCenterVertex = 30;
    }

    public class VolumeAndMassTests
    {
        [Fact]
        public void Volume_RectBlank_MatchesAnalytic()
        {
            double v = H.Blank().VolumeM3();
            Assert.InRange(Math.Abs(v - 0.7 * 0.05 * 0.006), 0.0, 1e-12);
        }

        [Fact]
        public void Volume_TaperedBlank_MatchesAnalytic()
        {
            double L = 0.7, w0 = 0.05, w1 = 0.02, t0 = 0.006, t1 = 0.003;
            var p = BladeFactory.CreateTapered(L, x => 0.5 * (w0 + (w1 - w0) * x / L), x => 0.5 * (t0 + (t1 - t0) * x / L), 257, 48);
            double a = (w1 - w0) / L, b = (t1 - t0) / L;
            double analytic = w0 * t0 * L + (w0 * b + a * t0) * L * L / 2.0 + a * b * L * L * L / 3.0;
            Assert.InRange(Math.Abs(p.VolumeM3() - analytic) / analytic, 0.0, 1e-4);
        }

        [Fact]
        public void Mass_IsVolumeTimesDensity()
        {
            var s = H.Session();
            Assert.InRange(Math.Abs(s.MassKg - 0.7 * 0.05 * 0.006 * 7850.0), 0.0, 1e-9);
        }

        [Fact]
        public void CenterOfMass_UniformBlank_IsAtMidLength()
        {
            Assert.InRange(Math.Abs(H.Blank().CenterOfMassX() - 0.35), 0.0, 1e-9);
        }
    }

    public class RaycastTests
    {
        [Fact]
        public void Ray_HitsTopFace_WithUpwardNormal()
        {
            BladeHit hit;
            bool ok = BladeRaycaster.Raycast(H.Blank(), new Vec3d(0.35, 0.0, 0.1), new Vec3d(0, 0, -1), 5.0, out hit);
            Assert.True(ok);
            Assert.InRange(Math.Abs(hit.PointYZ.Y - 0.003), 0.0, 1e-6);
            Assert.InRange(Math.Abs(hit.NormalYZ.Y - 1.0), 0.0, 1e-9);
        }

        [Fact]
        public void Ray_MissesBeside()
        {
            BladeHit hit;
            Assert.False(BladeRaycaster.Raycast(H.Blank(), new Vec3d(0.35, 0.5, 0.1), new Vec3d(0, 0, -1), 5.0, out hit));
        }
    }

    public class GrindTests
    {
        [Fact]
        public void Grind_NoContact_DoesNotChangeMeshOrMass()
        {
            var s = H.Session();
            double[] ringBefore = H.Copy(s.Profile.Ring);
            double massBefore = s.MassKg;

            GrindResult r = s.ApplyGrind(H.Cmd(H.Top(0.3, 0.5, 100, 0.0), H.Top(0.4, 0.5, 100, 0.2)));

            Assert.True(r.Accepted);
            Assert.Equal(0, r.ContactSubsteps);
            Assert.Equal(0.0, r.RemovedVolumeM3);
            Assert.Equal(massBefore, s.MassKg);
            Assert.Equal(ringBefore, s.Profile.Ring);
            Assert.Equal(0L, s.Version);
        }

        [Fact]
        public void Grind_ConstantContact_MassStrictlyDecreases()
        {
            var s = H.Session();
            double massBefore = s.MassKg;

            GrindResult r = s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 100, 0.1)));

            Assert.True(r.Accepted, r.Reason);
            Assert.True(r.ContactSubsteps > 0);
            Assert.True(r.RemovedVolumeM3 > 0.0);
            Assert.True(s.MassKg < massBefore);
            // Removed mass equals the geometric mass difference.
            Assert.InRange(Math.Abs((massBefore - s.MassKg) - r.RemovedMassKg), 0.0, 1e-12);
            Assert.InRange(Math.Abs(s.CumulativeRemovedMassKg - r.RemovedMassKg), 0.0, 1e-12);
            Assert.Equal(1L, s.Version);
            // Real geometry moved: top surface at the contact point dropped.
            Assert.True(s.Profile.Point(128, H.TopCenterVertex).Y < 0.003 - 1e-3);
        }

        [Fact]
        public void Grind_ToolLift_StopsRemoval()
        {
            var s = H.Session();
            s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 100, 0.05)));
            double volAfterStroke = s.VolumeM3;
            double[] ring = H.Copy(s.Profile.Ring);

            // Pressure 0 = tool lifted: nothing may change, however long we "wait".
            GrindResult r = s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 0.0, 1.0)));

            Assert.True(r.Accepted);
            Assert.Equal(0.0, r.RemovedVolumeM3);
            Assert.Equal(volAfterStroke, s.VolumeM3);
            Assert.Equal(ring, s.Profile.Ring);
        }

        [Fact]
        public void Grind_MultipleSwipes_NoNegativeThickness_NoSelfIntersection_VolumeNeverIncreases()
        {
            var s = H.Session();
            var rng = new Random(42);
            double prev = s.VolumeM3;
            for (int i = 0; i < 40; i++)
            {
                double x = 0.05 + 0.6 * rng.NextDouble();
                double y = -0.02 + 0.04 * rng.NextDouble();
                double pr = 50 + 150 * rng.NextDouble();
                GrindResult r = s.ApplyGrind(H.Cmd(H.Top(x, y, pr, 0.02), H.Top(x + 0.03, y, pr, 0.05)));
                Assert.True(r.Accepted, "stroke " + i + ": " + r.Reason);
                Assert.True(s.VolumeM3 <= prev + 1e-15);
                prev = s.VolumeM3;
            }

            string reason;
            Assert.True(s.Profile.Validate(out reason), reason);
            for (int st = 0; st < s.Profile.StationCount; st++)
            {
                if (s.Profile.Severed[st]) continue;
                double zMin = double.MaxValue, zMax = double.MinValue;
                for (int j = 0; j < s.Profile.PerimeterCount; j++)
                {
                    double z = s.Profile.Point(st, j).Y;
                    if (z < zMin) zMin = z;
                    if (z > zMax) zMax = z;
                }
                Assert.True(zMax - zMin > 1e-5, "station " + st + " collapsed");
            }
        }

        [Fact]
        public void Grind_FastSwipe_InterpolatesContinuousPath()
        {
            // One huge jump 0.2 m in a single sample pair must equal a dense stroke over the same path/time.
            var sparse = H.Session();
            GrindResult rs = sparse.ApplyGrind(H.Cmd(H.Top(0.2, 0.0, 100, 0.0), H.Top(0.4, 0.0, 100, 0.2)));

            var denseSamples = new List<GrindSample> { H.Top(0.2, 0.0, 100, 0.0) };
            for (int i = 1; i <= 200; i++) denseSamples.Add(H.Top(0.2 + 0.001 * i, 0.0, 100, 0.001));
            var dense = H.Session();
            GrindResult rd = dense.ApplyGrind(H.Cmd(denseSamples.ToArray()));

            Assert.True(rs.Accepted && rd.Accepted);
            Assert.True(rs.RemovedVolumeM3 > 0.0);
            Assert.InRange(Math.Abs(rs.RemovedVolumeM3 - rd.RemovedVolumeM3) / rd.RemovedVolumeM3, 0.0, 0.05);
            // Mid-path station was actually cut (no tunnelling over the gap).
            Assert.True(sparse.Profile.Point(110, H.TopCenterVertex).Y < 0.003 - 5e-5);
        }

        [Fact]
        public void Grind_CanCutThroughBlade_AndStaysValid()
        {
            var p = BladeFactory.CreateRectBlank(0.7, 0.01, 0.006, 65, 16);
            var s = new ForgeSession(p, MaterialData.SteelBasicV1);
            double before = s.VolumeM3;

            var ray = new GrindSample(new Vec3d(0.35, 0.0, 0.1), new Vec3d(0, 0, -1), 500, 0.1);
            GrindResult r = s.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0, AbrasiveWheel.Coarse, 3000, new[] { ray }));

            Assert.True(r.Accepted, r.Reason);
            Assert.True(s.Profile.Severed[32]);
            Assert.True(s.VolumeM3 < before);
            string reason;
            Assert.True(s.Profile.Validate(out reason), reason);
        }

        [Fact]
        public void Grind_InvalidCommand_IsRejected_AndStateUntouched()
        {
            var s = H.Session();
            double[] ring = H.Copy(s.Profile.Ring);
            GrindResult r = s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, double.NaN, 0.1)));
            Assert.False(r.Accepted);
            Assert.Equal(ring, s.Profile.Ring);
            Assert.Equal(0L, s.Version);
        }

        [Fact]
        public void Grind_DuplicateCommandId_IsRejected()
        {
            var s = H.Session();
            var cmd = H.Cmd(H.Top(0.35, 0.0, 100, 0.05));
            Assert.True(s.ApplyGrind(cmd).Accepted);
            double vol = s.VolumeM3;
            GrindResult again = s.ApplyGrind(cmd);
            Assert.False(again.Accepted);
            Assert.Equal(vol, s.VolumeM3);
        }

        [Fact]
        public void Replay_SameCommands_ProduceIdenticalGeometry()
        {
            var a = H.Session();
            var b = H.Session();
            var cmds = new[]
            {
                H.Cmd(H.Top(0.3, 0.0, 120, 0.05), H.Top(0.4, 0.0, 120, 0.1)),
                H.Cmd(H.Top(0.5, 0.01, 80, 0.05))
            };
            foreach (var c in cmds) a.ApplyGrind(c);
            foreach (var c in cmds)
                b.ApplyGrind(new GrindStrokeCommand(H.NextId(), c.TimestampS, c.Wheel, c.WheelRpm, c.Samples));
            Assert.Equal(a.Profile.Ring, b.Profile.Ring);
            Assert.Equal(a.MassKg, b.MassKg);
        }

        [Fact]
        public void Finer_Wheel_RemovesLess_ThanCoarse_ForSameStroke()
        {
            var a = H.Session();
            var b = H.Session();
            var samples = new[] { H.Top(0.35, 0.0, 100, 0.05) };
            double coarse = a.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0, AbrasiveWheel.Coarse, 3000, samples)).RemovedVolumeM3;
            double fine = b.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0, AbrasiveWheel.Fine, 3000, samples)).RemovedVolumeM3;
            Assert.True(fine > 0.0 && fine < coarse);
        }
    }

    public class PersistenceTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "forgeborn_tests_" + Guid.NewGuid().ToString("N"));

        public PersistenceTests() { Directory.CreateDirectory(_dir); }
        public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

        [Fact]
        public void Mesh_SavedReloaded_GeometryEquivalent()
        {
            var s = H.Session();
            s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 100, 0.1)));
            string path = Path.Combine(_dir, "w.fbw");
            WeaponFile.SaveAtomic(path, s.Capture());

            WeaponSnapshot snap;
            string err;
            Assert.True(WeaponFile.TryLoadWithRecovery(path, out snap, out err), err);
            var loaded = ForgeSession.FromSnapshot(snap);

            Assert.Equal(s.Profile.X, loaded.Profile.X);
            Assert.Equal(s.Profile.Ring, loaded.Profile.Ring);
            Assert.Equal(s.Profile.Severed, loaded.Profile.Severed);
            Assert.Equal(s.Version, loaded.Version);
            Assert.Equal(s.MassKg, loaded.MassKg);
            Assert.Equal(s.CumulativeRemovedMassKg, loaded.CumulativeRemovedMassKg);
        }

        [Fact]
        public void Save_CorruptedByte_IsDetected()
        {
            byte[] bytes = WeaponSerializer.ToBytes(H.Session().Capture());
            bytes[bytes.Length / 2] ^= 0x5A;
            WeaponSnapshot snap;
            string err;
            Assert.False(WeaponSerializer.TryFromBytes(bytes, out snap, out err));
            Assert.Equal("checksum mismatch", err);
        }

        [Fact]
        public void Save_Oversized_OrGarbage_IsRejected()
        {
            WeaponSnapshot snap;
            string err;
            Assert.False(WeaponSerializer.TryFromBytes(new byte[10], out snap, out err));
            Assert.False(WeaponSerializer.TryFromBytes(new byte[1000], out snap, out err));
            Assert.False(WeaponSerializer.TryFromBytes(null, out snap, out err));
        }

        [Fact]
        public void Save_Atomic_WriteInterrupted_RecoversPreviousVersion()
        {
            var s = H.Session();
            string path = Path.Combine(_dir, "w.fbw");

            s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 100, 0.05)));
            WeaponFile.SaveAtomic(path, s.Capture());           // version 1 on disk
            long v1 = s.Version;

            s.ApplyGrind(H.Cmd(H.Top(0.45, 0.0, 100, 0.05)));
            WeaponFile.SaveAtomic(path, s.Capture());           // version 2 on disk, version 1 in .bak
            Assert.True(s.Version > v1);

            // Simulate a crash that left a half-written temp file and a truncated main file.
            File.WriteAllBytes(path + ".tmp", new byte[] { 1, 2, 3 });
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write)) fs.SetLength(10);

            WeaponSnapshot snap;
            string err;
            Assert.True(WeaponFile.TryLoadWithRecovery(path, out snap, out err), err);
            Assert.Equal(v1, snap.Version);
        }

        [Fact]
        public void Save_FirstWriteInterrupted_NoValidFile_ReportsFailure()
        {
            string path = Path.Combine(_dir, "none.fbw");
            File.WriteAllBytes(path + ".tmp", new byte[] { 9, 9 });
            WeaponSnapshot snap;
            string err;
            Assert.False(WeaponFile.TryLoadWithRecovery(path, out snap, out err));
            Assert.NotNull(err);
        }
    }
}
