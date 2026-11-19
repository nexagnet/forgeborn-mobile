using System;
using System.Collections.Generic;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Session;
using Xunit;
using Xunit.Abstractions;

namespace ForgeBorn.Domain.Tests
{
    /// <summary>
    /// Angle-held grinding: with a set angle the bevel comes out at that angle whatever the pointer offset or pressure
    /// (the old free groove did not; see docs/GRIND_ANGLE_RESEARCH.md).
    /// </summary>
    public class GrindFacetTests
    {
        private readonly ITestOutputHelper _out;
        public GrindFacetTests(ITestOutputHelper o) { _out = o; }

        private const double L = 0.8;

        /// <summary>The game's tapered blank: 8 mm thick at the tang, 4 mm at the tip. A bevel here means removing about 2.7 mm per face.</summary>
        private static ForgeSession Thick()
        {
            return new ForgeSession(BladeFactory.CreateTapered(L, x => 0.025 - 0.010 * x / L, x => 0.004 - 0.002 * x / L, 192, 48), MaterialData.SteelBasicV1);
        }

        /// <summary>A thin bar (3 mm) so a bevel needs only a little metal off and the angle tests stay quick.</summary>
        private static ForgeSession Tapered()
        {
            return new ForgeSession(BladeFactory.CreateTapered(L, x => 0.025 - 0.010 * x / L, x => 0.0015 - 0.0004 * x / L, 192, 48), MaterialData.SteelBasicV1);
        }

        private static double EdgeY(double x) { return 0.025 - 0.010 * x / L; }

        /// <summary>One pass along the +Y edge at a distance below it. fromBelow grinds the other face (the blade turned over).</summary>
        private static void Pass(ForgeSession s, double angleDeg, double offsetM, double pressureN, bool fromBelow, AbrasiveWheel w = null, double yAbs = double.NaN)
        {
            var samples = new List<GrindSample>();
            for (int i = 0; i <= 120; i++)
            {
                double x = 0.1 + 0.005 * i;
                double y = double.IsNaN(yAbs) ? EdgeY(x) - offsetM : yAbs;
                Vec3d o = fromBelow ? new Vec3d(x, y, -0.1) : new Vec3d(x, y, 0.1);
                Vec3d d = fromBelow ? new Vec3d(0, 0, 1) : new Vec3d(0, 0, -1);
                samples.Add(new GrindSample(o, d, pressureN, i == 0 ? 0.0 : 0.005 / 0.25));
            }
            GrindResult r = s.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0.0, w ?? AbrasiveWheel.Medium, 3000.0, samples, angleDeg));
            Assert.True(r.Accepted, r.Reason);
        }

        /// <summary>Grinds both faces round after round until the measured angle stops moving (or 25 rounds). Returns the rounds used.</summary>
        private int Converge(ForgeSession s, double angle, double offsetM, double pressureN, AbrasiveWheel w)
        {
            double last = double.NaN;
            int steady = 0;
            for (int round = 1; round <= 25; round++)
            {
                Pass(s, angle, offsetM, pressureN, false, w);
                Pass(s, angle, offsetM, pressureN, true, w);
                double now = s.Evaluate(null).EdgeAngleDeg;
                steady = !double.IsNaN(now) && !double.IsNaN(last) && Math.Abs(now - last) < 0.25 ? steady + 1 : 0;
                last = now;
                if (steady >= 2) return round;
            }
            return 25;
        }

        [Theory]
        [InlineData(10.0)]
        [InlineData(20.0)]
        [InlineData(30.0)]
        [InlineData(40.0)]
        public void Angle_ComesOutAsSet_WhenBothFacesAreGround(double angle)
        {
            ForgeSession s = Tapered();
            int rounds = Converge(s, angle, 0.002, 150.0, AbrasiveWheel.Coarse);
            BladeReport r = s.Evaluate(null);
            _out.WriteLine("set " + angle + " -> measured " + r.EdgeAngleDeg.ToString("F1") + " after " + rounds + " rounds, t05 " + (r.EdgeThickness05M * 1000.0).ToString("F2") + " mm");
            Assert.True(r.HasBevel);
            Assert.True(r.BevelAsymmetry < 0.1, "both faces ground alike: " + r.BevelAsymmetry);
            Assert.InRange(r.EdgeAngleDeg, angle - (angle >= 35.0 ? 3.5 : 2.0), angle + 2.0); // steep facets are shorter than the measuring span, so they read a little low
            string why;
            Assert.True(s.Profile.Validate(out why), why);
        }

        [Fact]
        public void Angle_DoesNotDependOnWherePointerRuns()
        {
            foreach (double off in new[] { 0.0005, 0.002, 0.004, 0.006 })
            {
                ForgeSession s = Tapered();
                int rounds = Converge(s, 20.0, off, 150.0, AbrasiveWheel.Coarse);
                BladeReport r = s.Evaluate(null);
                _out.WriteLine("offset " + off * 1000.0 + " mm -> " + r.EdgeAngleDeg.ToString("F1") + " (" + rounds + " rounds)");
                Assert.True(r.HasBevel, "offset " + off);
                Assert.InRange(r.EdgeAngleDeg, 18.0, 22.0);
            }
        }

        [Theory]
        [InlineData(40.0)]
        [InlineData(150.0)]
        public void Angle_DoesNotDependOnPressure(double pressure)
        {
            ForgeSession s = Tapered();
            int rounds = Converge(s, 20.0, 0.003, pressure, AbrasiveWheel.Coarse);
            BladeReport r = s.Evaluate(null);
            _out.WriteLine(pressure + " N -> " + r.EdgeAngleDeg.ToString("F1") + " (" + rounds + " rounds)");
            Assert.InRange(r.EdgeAngleDeg, 18.0, 22.0);
        }

        [Fact]
        public void TheWheelDoesNotMatter_FineAndCoarseEndAtTheSameAngle()
        {
            ForgeSession s = Tapered();
            Converge(s, 20.0, 0.002, 150.0, AbrasiveWheel.Coarse);
            double coarse = s.Evaluate(null).EdgeAngleDeg;
            Converge(s, 20.0, 0.002, 150.0, AbrasiveWheel.Fine); // finishing pass changes nothing once on the plane
            double fine = s.Evaluate(null).EdgeAngleDeg;
            Assert.InRange(Math.Abs(fine - coarse), 0.0, 1.0);
        }

        [Fact]
        public void GrindingOneFaceOnly_GivesAboutHalfTheAngle_AndIsNotCalledBent()
        {
            ForgeSession s = Tapered();
            for (int i = 0; i < 8; i++) Pass(s, 20.0, 0.002, 150.0, false, AbrasiveWheel.Coarse);
            BladeReport r = s.Evaluate(null);
            _out.WriteLine("one face: " + r.EdgeAngleDeg.ToString("F1") + " symmetry " + r.SymmetryScore.ToString("F2") + " straight " + r.StraightnessScore.ToString("F2"));
            Assert.InRange(r.EdgeAngleDeg, 7.0, 13.0);
            Assert.True(r.StraightnessScore > 0.8, "a one-sided bevel is not a bent blade");
            Assert.True(r.BevelAsymmetry > 0.15, "one face ground reads as one-sided: " + r.BevelAsymmetry);
        }

        [Fact]
        public void MetalIsOnlyRemoved_AndTheBladeStaysValid()
        {
            ForgeSession s = Tapered();
            double v = s.VolumeM3;
            for (int i = 0; i < 4; i++)
            {
                Pass(s, 25.0, 0.003, 150.0, i % 2 == 0);
                Assert.True(s.VolumeM3 <= v + 1e-12);
                v = s.VolumeM3;
            }
            string why;
            Assert.True(s.Profile.Validate(out why), why);
        }

        [Fact]
        public void FarFromTheEdge_TheFaceIsStillThinned_ForMassLimits()
        {
            ForgeSession s = Tapered();
            double before = s.MassKg;
            Pass(s, 20.0, 0.0, 120.0, false, null, 0.0); // pointer along the middle of the face
            Assert.True(s.MassKg < before - 0.004, "the face away from the edge still loses metal");
        }

        [Fact]
        public void AngleZeroIsTheOldBehaviour_AndBadAnglesAreRejected()
        {
            ForgeSession a = Tapered(), b = Tapered();
            Pass(a, 0.0, 0.005, 80.0, false);
            var samples = new List<GrindSample>();
            for (int i = 0; i <= 120; i++)
            {
                double x = 0.1 + 0.005 * i;
                samples.Add(new GrindSample(new Vec3d(x, EdgeY(x) - 0.005, 0.1), new Vec3d(0, 0, -1), 80.0, i == 0 ? 0.0 : 0.02));
            }
            Assert.True(b.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0.0, AbrasiveWheel.Medium, 3000.0, samples)).Accepted);
            Assert.Equal(a.MassKg, b.MassKg, 12);

            foreach (double bad in new[] { 3.0, 75.0, double.NaN, -10.0 })
            {
                var one = new List<GrindSample> { samples[1] };
                Assert.False(Tapered().ApplyGrind(new GrindStrokeCommand(H.NextId(), 0.0, AbrasiveWheel.Medium, 3000.0, one, bad)).Accepted, "angle " + bad);
            }
        }
    }
}
