using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Persistence;
using ForgeBorn.Domain.Session;
using Xunit;

namespace ForgeBorn.Domain.Tests
{
    internal static class HB
    {
        public const double L = 0.7;

        public static void FillTemp(BladeProfile p, double tK)
        {
            for (int i = 0; i < p.TempK.Length; i++) p.TempK[i] = tK;
        }

        public static HammerStrikeCommand Strike(double x, double y, double energyJ)
        {
            return new HammerStrikeCommand(H.NextId(), 0.0, new Vec3d(x, y, 0.1), new Vec3d(0, 0, -1), energyJ, HammerFace.Flat);
        }

        public static ThermalCommand Therm(ThermalEnvironment env, double seconds)
        {
            return new ThermalCommand(H.NextId(), env, seconds);
        }

        /// <summary>Beveled blade: 0.7 x 0.05 x 0.006 m, 12.5 mm bevel to 1 mm edge (kink on a ring vertex), 257 stations, P=48.</summary>
        public static ForgeSession Beveled()
        {
            return new ForgeSession(BladeFactory.CreateBeveled(L, 0.05, 0.006, 0.0125, 0.001, 257, 48), MaterialData.SteelBasicV1);
        }

        /// <summary>Furnace 90 s at 1400 K, then water 30 s: a full hardening cycle.</summary>
        public static void Harden(ForgeSession s)
        {
            Assert.True(s.ApplyThermal(Therm(ThermalEnvironment.Furnace(1400.0), 90.0)).Accepted);
            Assert.True(s.ApplyThermal(Therm(ThermalEnvironment.Water(), 30.0)).Accepted);
        }

        public static double MidIndexValue(BladeProfile p, double[] arr)
        {
            return arr[p.StationCount / 2];
        }
    }

    public class FrameTests
    {
        [Fact]
        public void BendAndTwist_DoNotChangeVolumeOrMass()
        {
            var p = H.Blank();
            double v0 = p.VolumeM3();
            BladeFactory.ApplyFrame(p,
                x => 0.01 * 4.0 * x * (HB.L - x) / (HB.L * HB.L),
                x => 0.02 * Math.Sin(Math.PI * x / HB.L),
                x => 0.5 * x / HB.L);
            Assert.InRange(Math.Abs(p.VolumeM3() - v0), 0.0, 1e-15);
            string reason;
            Assert.True(p.Validate(out reason), reason);
        }

        [Fact]
        public void Raycast_FollowsCenterlineOffset()
        {
            var p = H.Blank();
            BladeFactory.ApplyFrame(p, null, x => 0.02 * Math.Sin(Math.PI * x / HB.L), null);
            BladeHit hit;
            Assert.True(BladeRaycaster.Raycast(p, new Vec3d(0.35, 0.0, 0.2), new Vec3d(0, 0, -1), 5.0, out hit));
            Assert.InRange(Math.Abs(hit.PointYZ.Y - (0.02 + 0.003)), 0.0, 1e-5);
            Assert.InRange(Math.Abs(hit.NormalYZ.Y - 1.0), 0.0, 1e-6);
        }

        [Fact]
        public void Raycast_HonoursTwist_NormalRotates()
        {
            var p = H.Blank();
            BladeFactory.ApplyFrame(p, null, null, x => Math.PI * 0.5); // section turned on edge: now 6 mm wide, 50 mm tall
            BladeHit hit;
            Assert.True(BladeRaycaster.Raycast(p, new Vec3d(0.35, 0.0, 0.2), new Vec3d(0, 0, -1), 5.0, out hit));
            Assert.InRange(Math.Abs(hit.PointYZ.Y - 0.025), 0.0, 1e-5);
            Assert.InRange(Math.Abs(hit.NormalYZ.Y - 1.0), 0.0, 1e-6);
        }

        [Fact]
        public void Grind_OnBentTwistedBlade_RemovesMetalAndStaysValid()
        {
            var s = H.Session();
            BladeFactory.ApplyFrame(s.Profile,
                x => 0.004 * Math.Sin(Math.PI * x / HB.L),
                x => 0.01 * Math.Sin(Math.PI * x / HB.L),
                x => 0.3 * x / HB.L);
            double before = s.MassKg;
            BladeHit hit;
            Assert.True(BladeRaycaster.Raycast(s.Profile, new Vec3d(0.35, 0.004, 0.2), new Vec3d(0, 0, -1), 5.0, out hit));
            var r = s.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0, AbrasiveWheel.Coarse, 3000.0,
                new[] { new GrindSample(new Vec3d(0.35, 0.004, 0.2), new Vec3d(0, 0, -1), 120.0, 0.1) }));
            Assert.True(r.Accepted, r.Reason);
            Assert.True(r.RemovedVolumeM3 > 0.0);
            Assert.True(s.MassKg < before);
            string reason;
            Assert.True(s.Profile.Validate(out reason), reason);
        }

        [Fact]
        public void Raycast_WithPrecomputedBounds_EqualsPlainRaycast_EvenAfterGrinding()
        {
            var s = H.Session();
            BladeBounds bounds = BladeRaycaster.ComputeBounds(s.Profile);
            s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 200, 0.2)));
            var rng = new Random(3);
            for (int i = 0; i < 50; i++)
            {
                var o = new Vec3d(0.1 + 0.5 * rng.NextDouble(), -0.03 + 0.06 * rng.NextDouble(), 0.1);
                BladeHit a, b;
                bool ra = BladeRaycaster.Raycast(s.Profile, o, new Vec3d(0, 0, -1), 5.0, out a);
                bool rb = BladeRaycaster.Raycast(s.Profile, o, new Vec3d(0, 0, -1), 5.0, bounds, out b);
                Assert.Equal(ra, rb);
                if (ra) Assert.Equal(a.T, b.T);
            }
        }

        [Fact]
        public void WorldAndLocal_AreInverse()
        {
            var p = H.Blank();
            BladeFactory.ApplyFrame(p, x => 0.01 * x, x => -0.02 * x, x => 0.7 * x);
            var local = new Vec2d(0.013, -0.002);
            Vec2d back = p.ToLocal(100, p.ToWorld(100, local));
            Assert.InRange(Math.Abs(back.X - local.X), 0.0, 1e-12);
            Assert.InRange(Math.Abs(back.Y - local.Y), 0.0, 1e-12);
        }
    }

    public class SelfIntersectionTests
    {
        [Fact]
        public void TouchingCheck_AgreesWithFullCheck_OnRandomVertexMoves()
        {
            var p = H.Blank();
            var rng = new Random(5);
            int crossings = 0;
            for (int trial = 0; trial < 400; trial++)
            {
                var q = p.Clone();
                var moved = new bool[q.PerimeterCount];
                int count = 1 + rng.Next(6);
                int start = rng.Next(q.PerimeterCount);
                for (int k = 0; k < count; k++)
                {
                    int j = (start + k) % q.PerimeterCount;
                    Vec2d v = q.Point(100, j);
                    double mag = rng.NextDouble() * 0.02;
                    q.SetPoint(100, j, new Vec2d(v.X + (rng.NextDouble() - 0.5) * mag * 2, v.Y + (rng.NextDouble() - 0.5) * mag * 2));
                    moved[j] = true;
                }
                bool full = q.StationSelfIntersects(100);
                bool local = q.StationSelfIntersectsTouching(100, moved);
                Assert.Equal(full, local);
                if (full) crossings++;
            }
            Assert.True(crossings > 20, "the trial must exercise real crossings, got " + crossings);
        }
    }

    public class HammerTests
    {
        [Fact]
        public void HotStrike_ConservesVolume_ThinsAndWidens()
        {
            var s = H.Session();
            HB.FillTemp(s.Profile, 1300.0);
            double v0 = s.VolumeM3;
            double widthBefore = 0.025;

            HammerResult r = s.ApplyHammer(HB.Strike(0.35, 0.0, 100.0));

            Assert.True(r.Accepted, r.Reason);
            Assert.True(r.Hit);
            Assert.True(r.StationsTouched > 0);
            Assert.False(r.Cold);
            Assert.InRange(Math.Abs(s.VolumeM3 - v0) / v0, 0.0, 1e-9);
            Assert.Equal(0.0, r.DamageAdded);
            int mid = 128;
            Assert.True(s.Profile.Point(mid, H.TopCenterVertex).Y < 0.003 - 5e-4, "top surface should be pressed down");
            double maxY = 0.0;
            for (int j = 0; j < s.Profile.PerimeterCount; j++) maxY = Math.Max(maxY, Math.Abs(s.Profile.Point(mid, j).X));
            Assert.True(maxY > widthBefore + 1e-5, "metal should spread sideways");
            Assert.Equal(1L, s.Version);
        }

        [Fact]
        public void ColdStrike_MovesLittle_AndAccumulatesDamage()
        {
            var hot = H.Session();
            HB.FillTemp(hot.Profile, 1300.0);
            var cold = H.Session();

            HammerResult rh = hot.ApplyHammer(HB.Strike(0.35, 0.0, 100.0));
            HammerResult rc = cold.ApplyHammer(HB.Strike(0.35, 0.0, 100.0));

            Assert.True(rc.Accepted, rc.Reason);
            Assert.True(rc.Cold);
            Assert.True(rc.DepthM < 0.2 * rh.DepthM, "cold metal should barely deform");
            Assert.True(rc.DamageAdded > 0.0);
            Assert.Equal(0.0, rh.DamageAdded);
        }

        [Fact]
        public void RepeatedColdStrikes_EventuallyCrack()
        {
            var s = H.Session();
            double maxDamage = 0.0;
            for (int i = 0; i < 80 && maxDamage < 1.0; i++)
            {
                Assert.True(s.ApplyHammer(HB.Strike(0.35, 0.0, 150.0)).Accepted);
                for (int st = 0; st < s.Profile.StationCount; st++) maxDamage = Math.Max(maxDamage, s.Profile.Damage[st]);
            }
            Assert.True(maxDamage >= 1.0, "hammering cold steel must eventually crack it");
            Assert.True(s.Evaluate(null).CrackedStations > 0);
        }

        [Fact]
        public void HundredRandomHotBlows_KeepVolumeWithinOnePercent_AndStayValid()
        {
            var s = H.Session();
            HB.FillTemp(s.Profile, 1350.0);
            double v0 = s.VolumeM3;
            var rng = new Random(7);
            for (int i = 0; i < 100; i++)
            {
                double x = 0.05 + 0.6 * rng.NextDouble();
                double y = -0.02 + 0.04 * rng.NextDouble();
                double e = 20.0 + 130.0 * rng.NextDouble();
                HammerResult r = s.ApplyHammer(HB.Strike(x, y, e));
                Assert.True(r.Accepted, "blow " + i + ": " + r.Reason);
            }
            Assert.InRange(Math.Abs(s.VolumeM3 - v0) / v0, 0.0, 0.01);
            Assert.InRange(Math.Abs(s.VolumeM3 - v0) / v0, 0.0, 1e-6); // the model is in fact much tighter
            string reason;
            Assert.True(s.Profile.Validate(out reason), reason);
            Assert.True(s.Version > 50L);
        }

        [Fact]
        public void Replay_IdenticalBlows_ProduceIdenticalGeometry()
        {
            var a = H.Session();
            var b = H.Session();
            HB.FillTemp(a.Profile, 1300.0);
            HB.FillTemp(b.Profile, 1300.0);
            var rng = new Random(11);
            for (int i = 0; i < 20; i++)
            {
                double x = 0.1 + 0.5 * rng.NextDouble();
                double e = 30.0 + 100.0 * rng.NextDouble();
                a.ApplyHammer(HB.Strike(x, 0.0, e));
                b.ApplyHammer(HB.Strike(x, 0.0, e));
            }
            Assert.Equal(a.Profile.Ring, b.Profile.Ring);
        }

        [Fact]
        public void Miss_ChangesNothing_AndInvalidInputIsRejected()
        {
            var s = H.Session();
            double[] ring = H.Copy(s.Profile.Ring);
            HammerResult miss = s.ApplyHammer(HB.Strike(0.35, 0.5, 100.0));
            Assert.True(miss.Accepted);
            Assert.False(miss.Hit);
            Assert.Equal(ring, s.Profile.Ring);
            Assert.Equal(0L, s.Version);

            Assert.False(s.ApplyHammer(HB.Strike(0.35, 0.0, double.NaN)).Accepted);
            Assert.False(s.ApplyHammer(HB.Strike(0.35, 0.0, -5.0)).Accepted);
            Assert.False(s.ApplyHammer(HB.Strike(0.35, 0.0, 1e9)).Accepted);
            Assert.Equal(ring, s.Profile.Ring);
        }

        [Fact]
        public void DuplicateId_IsRejected()
        {
            var s = H.Session();
            HB.FillTemp(s.Profile, 1300.0);
            var cmd = HB.Strike(0.35, 0.0, 50.0);
            Assert.True(s.ApplyHammer(cmd).Accepted);
            Assert.False(s.ApplyHammer(cmd).Accepted);
        }
    }

    public class ThermalTests
    {
        [Fact]
        public void UniformCooling_MatchesAnalyticLumpedSolution()
        {
            var s = H.Session();
            HB.FillTemp(s.Profile, 700.0);
            var env = ThermalEnvironment.Constant("test_h100", 300.0, 100.0, false);
            double t = 40.0;

            var r = s.ApplyThermal(HB.Therm(env, t));

            Assert.True(r.Accepted, r.Reason);
            MaterialData m = MaterialData.SteelBasicV1;
            double area = 0.05 * 0.006, per = 2.0 * (0.05 + 0.006);
            double k = 100.0 * per / (m.VolumetricHeatCapacity * area);
            double expected = 300.0 + (700.0 - 300.0) * Math.Exp(-k * t);
            for (int i = 0; i < s.Profile.StationCount; i += 16)
                Assert.InRange(Math.Abs(s.Profile.TempK[i] - expected), 0.0, 1e-6);
        }

        [Fact]
        public void Conduction_ConservesEnergy_AndSmoothsGradients()
        {
            var s = H.Session();
            for (int i = 0; i < s.Profile.StationCount; i++) s.Profile.TempK[i] = i < 32 ? 1000.0 : 300.0;
            double e0 = ThermalSolver.TotalEnergy(s.Profile, s.Material);
            double spread0 = 700.0;

            var env = ThermalEnvironment.Constant("adiabatic", 300.0, 0.0, false);
            var r = s.ApplyThermal(HB.Therm(env, 600.0));

            Assert.True(r.Accepted, r.Reason);
            double e1 = ThermalSolver.TotalEnergy(s.Profile, s.Material);
            Assert.InRange(Math.Abs(e1 - e0) / e0, 0.0, 1e-9);
            Assert.InRange(Math.Abs(r.InternalEnergyChangeJ) / e0, 0.0, 1e-9);
            double mx = double.MinValue, mn = double.MaxValue;
            foreach (double v in s.Profile.TempK) { mx = Math.Max(mx, v); mn = Math.Min(mn, v); }
            Assert.True(mx - mn < spread0, "gradient must shrink");
            Assert.True(mx <= 1000.0 + 1e-9 && mn >= 300.0 - 1e-9, "no over/undershoot");
        }

        [Fact]
        public void Heating_NeverOvershootsEnvironment_CoolingNeverUndershoots()
        {
            var s = H.Session();
            Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1200.0), 600.0)).Accepted);
            foreach (double t in s.Profile.TempK) Assert.True(t <= 1200.0 + 1e-9);
            Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Air(), 3600.0)).Accepted);
            foreach (double t in s.Profile.TempK) Assert.True(t >= 293.15 - 1e-9);
        }

        [Fact]
        public void Thermal_IsDeterministic_AndLeavesGeometryUntouched()
        {
            var a = H.Session();
            var b = H.Session();
            double[] ring = H.Copy(a.Profile.Ring);
            foreach (var s in new[] { a, b })
            {
                s.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1400.0), 90.0));
                s.ApplyThermal(HB.Therm(ThermalEnvironment.Water(), 30.0));
            }
            Assert.Equal(a.Profile.TempK, b.Profile.TempK);
            Assert.Equal(a.Profile.Martensite, b.Profile.Martensite);
            Assert.Equal(ring, a.Profile.Ring);
        }

        [Fact]
        public void WaterQuench_Hardens_AirCool_DoesNot_OilIsInBetween()
        {
            var water = H.Session();
            HB.Harden(water);

            var air = H.Session();
            air.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1400.0), 90.0));
            air.ApplyThermal(HB.Therm(ThermalEnvironment.Air(), 900.0));

            var oil = H.Session();
            oil.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1400.0), 90.0));
            oil.ApplyThermal(HB.Therm(ThermalEnvironment.Oil(), 60.0));

            double mw = HB.MidIndexValue(water.Profile, water.Profile.Martensite);
            double ma = HB.MidIndexValue(air.Profile, air.Profile.Martensite);
            double mo = HB.MidIndexValue(oil.Profile, oil.Profile.Martensite);
            Assert.True(mw > 0.95, "water quench should be (nearly) fully martensitic, was " + mw);
            Assert.True(ma < 0.05, "slow air cool must stay soft, was " + ma);
            Assert.True(mo < mw, "oil is gentler than water for this steel");

            MaterialData m = MaterialData.SteelBasicV1;
            Assert.True(m.HardnessHrc(mw, 0.0) > 58.0);
            Assert.InRange(m.HardnessHrc(ma, 0.0), 21.0, 24.0);
            Assert.Equal(0, water.Evaluate(null).CrackedStations);
        }

        [Fact]
        public void Tempering_SoftensMartensite_MoreWithHigherTemperature()
        {
            var low = H.Session();
            HB.Harden(low);
            var high = H.Session();
            HB.Harden(high);
            double hardened = low.Material.HardnessHrc(HB.MidIndexValue(low.Profile, low.Profile.Martensite),
                HB.MidIndexValue(low.Profile, low.Profile.TemperP));

            Assert.True(low.ApplyThermal(HB.Therm(ThermalEnvironment.Oven(473.15), 3600.0)).Accepted);
            Assert.True(high.ApplyThermal(HB.Therm(ThermalEnvironment.Oven(673.15), 3600.0)).Accepted);

            double hLow = low.Material.HardnessHrc(HB.MidIndexValue(low.Profile, low.Profile.Martensite),
                HB.MidIndexValue(low.Profile, low.Profile.TemperP));
            double hHigh = high.Material.HardnessHrc(HB.MidIndexValue(high.Profile, high.Profile.Martensite),
                HB.MidIndexValue(high.Profile, high.Profile.TemperP));

            Assert.True(hLow < hardened, "200 C temper softens a little");
            Assert.True(hHigh < hLow - 8.0, "400 C temper softens much more");
            Assert.InRange(hHigh, 35.0, 50.0);
        }

        [Fact]
        public void Overheating_AccumulatesDamage()
        {
            var s = H.Session();
            var r = s.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1900.0), 120.0));
            Assert.True(r.Accepted, r.Reason);
            Assert.True(r.MaxTempK > 1500.0);
            Assert.True(HB.MidIndexValue(s.Profile, s.Profile.Damage) > 0.0);
        }

        [Fact]
        public void InvalidCommands_AreRejected_AndStateUntouched()
        {
            var s = H.Session();
            double[] t = H.Copy(s.Profile.TempK);
            Assert.False(s.ApplyThermal(HB.Therm(ThermalEnvironment.Air(), -1.0)).Accepted);
            Assert.False(s.ApplyThermal(HB.Therm(ThermalEnvironment.Air(), double.NaN)).Accepted);
            Assert.False(s.ApplyThermal(HB.Therm(ThermalEnvironment.Air(), 1e9)).Accepted);
            Assert.False(s.ApplyThermal(HB.Therm(ThermalEnvironment.Constant("hot", 1e6, 10.0, false), 10.0)).Accepted);
            Assert.False(s.ApplyThermal(null).Accepted);
            Assert.Equal(t, s.Profile.TempK);
            Assert.Equal(0L, s.Version);
        }
    }

    public class EvaluationTests
    {
        [Fact]
        public void Golden_RectBlank()
        {
            var s = H.Session();
            BladeReport r = s.Evaluate(null);

            Assert.InRange(Math.Abs(r.MassKg - 0.7 * 0.05 * 0.006 * 7850.0), 0.0, 1e-9);
            Assert.InRange(Math.Abs(r.LengthM - 0.7), 0.0, 1e-12);
            Assert.InRange(Math.Abs(r.BalanceFrac - 0.5), 0.0, 1e-9);
            Assert.Equal(0.0, r.StraightnessDevM, 12);
            Assert.Equal(1.0, r.StraightnessScore, 9);
            Assert.Equal(0.0, r.TwistRangeDeg, 12);
            Assert.Equal(1.0, r.SymmetryScore, 9);
            Assert.False(r.HasBevel);
            Assert.True(double.IsNaN(r.EdgeAngleDeg));
            Assert.Equal(0.0, r.EdgeScore);
            Assert.Equal(22.0, r.MeanHrc, 9);
            Assert.Equal(0.0, r.HardnessScore);
            Assert.Equal(1.0, r.ToughnessScore, 9);
            Assert.Equal(0.5, r.BalanceScore, 6);
            Assert.Equal(50.0, r.Overall, 6);
            Assert.Equal("D", r.Grade);
            Assert.False(r.Broken);
            Assert.False(r.Pass);
        }

        [Fact]
        public void Golden_BeveledBlade_EdgeAngleMatchesAnalytic()
        {
            var s = HB.Beveled();
            BladeReport r = s.Evaluate(null);

            double analyticDeg = 2.0 * Math.Atan((0.003 - 0.0005) / 0.0125) * 180.0 / Math.PI;
            Assert.True(r.HasBevel);
            Assert.InRange(Math.Abs(r.EdgeAngleDeg - analyticDeg), 0.0, 0.01);
            // thickness 0.5 mm behind the tip: 1 mm + 2 * (2.5 mm / 12.5 mm) * 0.5 mm
            Assert.InRange(Math.Abs(r.EdgeThickness05M - 0.0012), 0.0, 1e-7);
            Assert.Equal(1.0, r.SymmetryScore, 6);
            Assert.True(r.EdgeScore > 0.7);
        }

        [Fact]
        public void Golden_CurvedBlade_StraightnessDeviationEqualsAmplitude()
        {
            var s = H.Session();
            BladeFactory.ApplyFrame(s.Profile, x => 0.01 * 4.0 * x * (HB.L - x) / (HB.L * HB.L), null, null);
            BladeReport r = s.Evaluate(null);
            Assert.InRange(Math.Abs(r.StraightnessDevM - 0.01), 0.0, 1e-9);
            Assert.Equal(0.0, r.StraightnessScore, 9);
        }

        [Fact]
        public void Golden_TwistedBlade_TwistScore()
        {
            var s = H.Session();
            BladeFactory.ApplyFrame(s.Profile, null, null, x => (2.0 * Math.PI / 180.0) * x / HB.L);
            BladeReport r = s.Evaluate(null);
            Assert.InRange(Math.Abs(r.TwistRangeDeg - 2.0), 0.0, 1e-9);
            Assert.InRange(Math.Abs(r.TwistScore - (1.0 - 2.0 / 3.0)), 0.0, 1e-9);
        }

        [Fact]
        public void OneSidedGrind_ReducesSymmetry()
        {
            var s = H.Session();
            double before = s.Evaluate(null).SymmetryScore;
            var samples = new List<GrindSample>();
            for (int i = 0; i <= 120; i++) samples.Add(H.Top(0.1 + 0.005 * i, 0.0, 200.0, i == 0 ? 0.0 : 0.02));
            Assert.True(s.ApplyGrind(H.Cmd(samples.ToArray())).Accepted);
            Assert.True(s.Evaluate(null).SymmetryScore < before);
        }

        [Fact]
        public void HeatTreatedBlade_ScoresHigherThanUntreated()
        {
            var raw = HB.Beveled();
            var treated = HB.Beveled();
            HB.Harden(treated);
            Assert.True(treated.ApplyThermal(HB.Therm(ThermalEnvironment.Oven(523.15), 3600.0)).Accepted);

            BladeReport rr = raw.Evaluate(null);
            BladeReport rt = treated.Evaluate(null);

            Assert.InRange(rt.MeanHrc, 50.0, 60.0);
            Assert.True(rt.Overall > rr.Overall + 5.0, "treated " + rt.Overall + " vs raw " + rr.Overall);
            Assert.True(rt.ToughnessScore < rr.ToughnessScore, "hardening costs toughness");
            Assert.True(rt.Pass, "a correctly hardened and tempered bevel blade should pass, score " + rt.Overall);
        }

        [Fact]
        public void CrackedOrSeveredBlade_FailsWhateverElse()
        {
            var cracked = HB.Beveled();
            cracked.Profile.Damage[128] = 1.5;
            BladeReport rc = cracked.Evaluate(null);
            Assert.Equal(1, rc.CrackedStations);
            Assert.True(rc.Overall <= 15.0);
            Assert.False(rc.Pass);

            var p = BladeFactory.CreateRectBlank(0.7, 0.01, 0.006, 65, 16);
            var s = new ForgeSession(p, MaterialData.SteelBasicV1);
            s.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0, AbrasiveWheel.Coarse, 3000,
                new[] { new GrindSample(new Vec3d(0.35, 0.0, 0.1), new Vec3d(0, 0, -1), 500, 0.1) }));
            BladeReport rs = s.Evaluate(null);
            Assert.True(rs.Broken);
            Assert.Equal(0.0, rs.Overall);
            Assert.False(rs.Pass);
        }
    }

    public class PersistenceV2Tests
    {
        [Fact]
        public void FrameAndMetallurgy_RoundTrip()
        {
            var s = HB.Beveled();
            BladeFactory.ApplyFrame(s.Profile, x => 0.004 * x, x => 0.002 * x, x => 0.2 * x);
            HB.Harden(s);
            s.ApplyThermal(HB.Therm(ThermalEnvironment.Oven(523.15), 600.0));

            byte[] bytes = WeaponSerializer.ToBytes(s.Capture());
            WeaponSnapshot snap;
            string err;
            Assert.True(WeaponSerializer.TryFromBytes(bytes, out snap, out err), err);
            var loaded = ForgeSession.FromSnapshot(snap);

            Assert.Equal(s.Profile.Ring, loaded.Profile.Ring);
            Assert.Equal(s.Profile.CenterY, loaded.Profile.CenterY);
            Assert.Equal(s.Profile.CenterZ, loaded.Profile.CenterZ);
            Assert.Equal(s.Profile.Twist, loaded.Profile.Twist);
            Assert.Equal(s.Profile.TempK, loaded.Profile.TempK);
            Assert.Equal(s.Profile.Austenite, loaded.Profile.Austenite);
            Assert.Equal(s.Profile.Martensite, loaded.Profile.Martensite);
            Assert.Equal(s.Profile.TemperP, loaded.Profile.TemperP);
            Assert.Equal(s.Profile.Damage, loaded.Profile.Damage);
        }

        [Fact]
        public void Schema1File_StillLoads_WithDefaultFrameAndAnnealedState()
        {
            var s = H.Session();
            s.ApplyGrind(H.Cmd(H.Top(0.35, 0.0, 100, 0.1)));
            byte[] legacy = LegacyV1(s.Capture());

            WeaponSnapshot snap;
            string err;
            Assert.True(WeaponSerializer.TryFromBytes(legacy, out snap, out err), err);
            var loaded = ForgeSession.FromSnapshot(snap);

            Assert.Equal(s.Profile.Ring, loaded.Profile.Ring);
            Assert.Equal(s.MassKg, loaded.MassKg);
            Assert.All(loaded.Profile.CenterY, v => Assert.Equal(0.0, v));
            Assert.All(loaded.Profile.Twist, v => Assert.Equal(0.0, v));
            Assert.All(loaded.Profile.TempK, v => Assert.Equal(BladeProfile.AmbientK, v));
            Assert.All(loaded.Profile.Martensite, v => Assert.Equal(0.0, v));
        }

        [Fact]
        public void UnknownFutureSchema_IsRejected()
        {
            byte[] legacy = LegacyV1(H.Session().Capture());
            // bump the schema field (bytes 4..7) and re-sign so only the schema check can reject it
            BitConverter.GetBytes(99).CopyTo(legacy, 4);
            Resign(legacy);
            WeaponSnapshot snap;
            string err;
            Assert.False(WeaponSerializer.TryFromBytes(legacy, out snap, out err));
            Assert.Contains("unsupported schema", err);
        }

        [Fact]
        public void InvalidMetallurgicalState_IsRejectedOnLoad()
        {
            var s = H.Session();
            s.Profile.Martensite[10] = 2.0; // not a fraction
            byte[] bytes = WeaponSerializer.ToBytes(s.Capture());
            WeaponSnapshot snap;
            string err;
            Assert.False(WeaponSerializer.TryFromBytes(bytes, out snap, out err));
            Assert.Contains("invalid geometry", err);
        }

        private static void Resign(byte[] data)
        {
            int bodyLen = data.Length - 32;
            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(data, 0, bodyLen);
            Buffer.BlockCopy(hash, 0, data, bodyLen, 32);
        }

        /// <summary>Writes a snapshot exactly as the original schema-1 writer did (geometry only).</summary>
        private static byte[] LegacyV1(WeaponSnapshot snap)
        {
            BladeProfile p = snap.Profile;
            byte[] id = Encoding.UTF8.GetBytes(snap.MaterialId);
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    w.Write(new byte[] { (byte)'F', (byte)'B', (byte)'W', (byte)'S' });
                    w.Write(1);
                    w.Write(id.Length);
                    w.Write(id);
                    w.Write(p.StationCount);
                    w.Write(p.PerimeterCount);
                    w.Write(snap.Version);
                    w.Write(snap.CumulativeRemovedMassKg);
                    foreach (double v in p.X) w.Write(v);
                    foreach (double v in p.Ring) w.Write(v);
                    foreach (bool b in p.Severed) w.Write((byte)(b ? 1 : 0));
                }
                byte[] body = ms.ToArray();
                byte[] hash;
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(body);
                byte[] all = new byte[body.Length + 32];
                Buffer.BlockCopy(body, 0, all, 0, body.Length);
                Buffer.BlockCopy(hash, 0, all, body.Length, 32);
                return all;
            }
        }
    }
}
