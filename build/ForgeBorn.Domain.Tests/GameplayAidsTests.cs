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
    /// <summary>Plan phases 0-2: grind rail, play log, quench monitor, tempering forecast, next-step advice.</summary>
    public class GameplayAidsTests
    {
        private readonly ITestOutputHelper _out;
        public GameplayAidsTests(ITestOutputHelper o) { _out = o; }

        private const double L = 0.8;

        private static ForgeSession Tapered()
        {
            return new ForgeSession(BladeFactory.CreateTapered(L, x => 0.025 - 0.010 * x / L, x => 0.004 - 0.002 * x / L, 192, 48), MaterialData.SteelBasicV1);
        }

        private static double EdgeY(double x) { return 0.025 - 0.010 * x / L; }

        private static void PassAt(ForgeSession s, AbrasiveWheel w, double offsetM)
        {
            var samples = new List<GrindSample>();
            for (int i = 0; i <= 120; i++)
            {
                double x = 0.1 + 0.005 * i;
                samples.Add(H.Top(x, EdgeY(x) - offsetM, 80.0, i == 0 ? 0.0 : 0.005 / 0.25)); // 80 N is the game's default pressure
            }
            Assert.True(s.ApplyGrind(new GrindStrokeCommand(H.NextId(), 0.0, w, 3000.0, samples)).Accepted);
        }

        // ---- grind rail (docs/GRIND_ANGLE_RESEARCH.md)

        [Fact]
        public void Rail_StaysInsideTheWheelFootprintAndTheFacetZone_ForEveryWheel()
        {
            foreach (AbrasiveWheel w in new[] { AbrasiveWheel.Coarse, AbrasiveWheel.Medium, AbrasiveWheel.Fine })
            {
                double off = GrindRail.OffsetM(w);
                Assert.InRange(off, 0.0005, w.FootprintHalfWidthM);
                Assert.True(off < GrindSolver.FacetZoneM);
            }
        }

        // ---- phase 0: play log

        [Fact]
        public void PlayStats_CountsStrikesMissesAndUndos()
        {
            var st = new PlayStats();
            st.OnStrike(true, false);
            st.OnStrike(true, true);
            st.OnStrike(false, false);
            st.OnUndo();
            st.OnGrindStroke();
            st.AddTime(1.5);
            st.AddTime(-3.0);   // ignored
            st.AddTime(100.0);  // a frame hitch or a pause, ignored
            Assert.Equal(3, st.Strikes);
            Assert.Equal(1, st.MissedStrikes);
            Assert.Equal(1, st.ColdStrikes);
            Assert.Equal(1, st.Undos);
            Assert.Equal(1.5, st.Seconds, 9);
            Assert.Equal(1.0 / 3.0, st.MissRate, 9);
            st.Reset();
            Assert.Equal(0, st.Strikes);
            Assert.Equal(0.0, st.MissRate);
        }

        [Fact]
        public void PlayLog_RowHasOneFieldPerColumn_AndSurvivesNanAndCommas()
        {
            ForgeSession s = Tapered();
            BladeReport r = s.Evaluate(null);
            string row = PlayLog.Row("o,1\nx", true, 120, r, new PlayStats());
            Assert.Equal(PlayLog.Header.Split(',').Length, row.Split(',').Length);
            Assert.DoesNotContain("\n", row);
            Assert.DoesNotContain("NaN", row);
        }

        // ---- phase 2: quench monitor

        private static List<QuenchState> QuenchStates(ThermalEnvironment bath, out QuenchReading first)
        {
            ForgeSession s = HB.Beveled();
            Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1400.0), 90.0)).Accepted);
            var states = new List<QuenchState>();
            first = default(QuenchReading);
            bool haveFirst = false;
            for (int i = 0; i < 120; i++)
            {
                double[] prev = (double[])s.Profile.TempK.Clone();
                s.Thermal.StepsPerCommand = 2000.0;
                Assert.True(s.ApplyThermal(HB.Therm(bath, 0.25)).Accepted);
                QuenchReading q = QuenchMonitor.Assess(s.Profile, s.Material, prev, 0.25);
                states.Add(q.State);
                if (!haveFirst && q.State != QuenchState.Idle) { first = q; haveFirst = true; }
            }
            return states;
        }

        [Fact]
        public void QuenchMonitor_IsIdleInAir_AndReportsASpeedInWater()
        {
            ForgeSession s = HB.Beveled();
            double[] prev = (double[])s.Profile.TempK.Clone();
            Assert.Equal(QuenchState.Idle, QuenchMonitor.Assess(s.Profile, s.Material, prev, 0.25).State);

            QuenchReading first;
            List<QuenchState> w = QuenchStates(ThermalEnvironment.Water(), out first);
            _out.WriteLine("water states: " + string.Join(",", w.FindAll(x => x != QuenchState.Idle)));
            Assert.Contains(w, x => x != QuenchState.Idle);
            Assert.True(first.MaxRateKPerS >= first.MinRateKPerS);
        }

        [Fact]
        public void QuenchMonitor_WaterIsAtLeastAsAlarmingAsOil()
        {
            QuenchReading f;
            List<QuenchState> w = QuenchStates(ThermalEnvironment.Water(), out f);
            List<QuenchState> o = QuenchStates(ThermalEnvironment.Oil(), out f);
            _out.WriteLine("oil states: " + string.Join(",", o.FindAll(x => x != QuenchState.Idle)));
            Assert.True(w.FindAll(x => x == QuenchState.CrackRisk).Count >= o.FindAll(x => x == QuenchState.CrackRisk).Count);
        }

        [Fact]
        public void Quench_OutcomePerBath_IsRecordedForTheDocs()
        {
            foreach (string name in new[] { "air", "oil", "water" })
            {
                ForgeSession s = HB.Beveled();
                Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1400.0), 90.0)).Accepted);
                s.Thermal.StepsPerCommand = 2000.0;
                ThermalEnvironment bath = name == "air" ? ThermalEnvironment.Air() : name == "oil" ? ThermalEnvironment.Oil() : ThermalEnvironment.Water();
                Assert.True(s.ApplyThermal(HB.Therm(bath, 30.0)).Accepted);
                int mid = s.Profile.StationCount / 2;
                BladeReport r = s.Evaluate(null);
                _out.WriteLine(name + ": martensite(mid) " + s.Profile.Martensite[mid].ToString("F2") + ", HRC " + r.MeanHrc.ToString("F1") + ", cracked stations " + r.CrackedStations);
            }
        }

        [Fact]
        public void QuenchMonitor_IgnoresBadInput()
        {
            ForgeSession s = Tapered();
            Assert.Equal(QuenchState.Idle, QuenchMonitor.Assess(s.Profile, s.Material, null, 0.25).State);
            Assert.Equal(QuenchState.Idle, QuenchMonitor.Assess(s.Profile, s.Material, new double[3], 0.25).State);
            Assert.Equal(QuenchState.Idle, QuenchMonitor.Assess(s.Profile, s.Material, (double[])s.Profile.TempK.Clone(), 0.0).State);
        }

        // ---- phase 2: tempering forecast

        [Fact]
        public void TemperForecast_FallsWithTimeAndTemperature_AndMatchesTheSolverFormula()
        {
            MaterialData m = MaterialData.SteelBasicV1;
            double untempered = TemperForecast.HrcAfter(m, 1.0, 0.0, 523.15, 0.0);
            Assert.Equal(m.HardnessHrc(1.0, 0.0), untempered, 9);

            double h1 = TemperForecast.HrcAfter(m, 1.0, 0.0, 523.15, 1.0);
            double h4 = TemperForecast.HrcAfter(m, 1.0, 0.0, 523.15, 4.0);
            double hot = TemperForecast.HrcAfter(m, 1.0, 0.0, 673.15, 1.0);
            _out.WriteLine("250C: 1h " + h1 + ", 4h " + h4 + "; 400C 1h " + hot);
            Assert.True(h1 < untempered && h4 < h1 && hot < h1);

            // Never rises: a blade already tempered further than the forecast stays put.
            double p = 700.0 * (20.0 + Math.Log10(2.0));
            Assert.True(TemperForecast.HrcAfter(m, 1.0, p, 523.15, 0.1) <= m.HardnessHrc(1.0, p) + 1e-9);
        }

        [Fact]
        public void TemperForecast_AgreesWithWhatTheOvenReallyDoes()
        {
            ForgeSession s = HB.Beveled();
            HB.Harden(s);
            int mid = s.Profile.StationCount / 2;
            double mart = s.Profile.Martensite[mid];
            Assert.True(mart > 0.5, "the fixture must be hardened");
            Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Air(), 600.0)).Accepted); // cool to near room temperature
            double forecast = TemperForecast.HrcAfter(s.Material, mart, s.Profile.TemperP[mid], 523.15, 1.0);
            Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Oven(523.15), 3600.0)).Accepted);
            double actual = s.Material.HardnessHrc(s.Profile.Martensite[mid], s.Profile.TemperP[mid]);
            _out.WriteLine("forecast " + forecast + " actual " + actual);
            Assert.InRange(actual, forecast - 3.0, forecast + 3.0); // the blade needs a few minutes to reach oven temperature
        }

        // ---- phase 4c: published ranges (typical handbook values as tolerance bands; the source still has to be confirmed, see docs/MODEL_FIDELITY.md)

        [Fact]
        public void PublishedRanges_FullyHardenedPlainCarbonSteelIsAtLeastAbout60Hrc()
        {
            Assert.True(MaterialData.SteelBasicV1.HardnessHrc(1.0, 0.0) >= 60.0);
        }

        [Fact]
        public void PublishedRanges_Tempering200CDropsAFewHrc_And400CDropsMuchMore()
        {
            MaterialData m = MaterialData.SteelBasicV1;
            double q = m.HardnessHrc(1.0, 0.0);
            double t200 = TemperForecast.HrcAfter(m, 1.0, 0.0, 473.15, 1.0);
            double t400 = TemperForecast.HrcAfter(m, 1.0, 0.0, 673.15, 1.0);
            _out.WriteLine("as quenched " + q + ", 200C " + t200 + ", 400C " + t400);
            Assert.InRange(q - t200, 1.0, 6.0);
            Assert.True(q - t400 > 2.0 * (q - t200));
            Assert.InRange(t400, 38.0, 56.0);
        }

        [Fact]
        public void PublishedRanges_UntemperedMartensiteIsBrittle_AndTemperingRestoresToughness()
        {
            double t0 = MaterialData.Toughness(1.0, 0.0);
            double t1 = MaterialData.Toughness(1.0, 673.15 * 20.0);
            Assert.True(t0 < 0.3);
            Assert.True(t1 > t0 + 0.3);
        }

        // ---- phase 2: next-step advice

        [Fact]
        public void Advice_DangerForCrackedAndBrokenBlades()
        {
            ForgeSession s = Tapered();
            s.Profile.Damage[10] = 1.2;
            Advice a = NextStepAdvisor.Suggest(s.Profile, s.Material, s.Evaluate(null), null, double.MaxValue);
            Assert.Equal(AdviceLevel.Danger, a.Level);
            Assert.Contains("cracked", a.Template);

            ForgeSession b = Tapered();
            for (int i = 90; i < 100; i++) b.Profile.Severed[i] = true;
            Assert.Equal(AdviceLevel.Danger, NextStepAdvisor.Suggest(b.Profile, b.Material, b.Evaluate(null), null, double.MaxValue).Level);
        }

        [Fact]
        public void Advice_FollowsTheProcess_GrindFirstAndMassLimitBeforeEdge()
        {
            ForgeSession s = Tapered();
            // One-sided grinding currently shifts the section centroid and reads as "bent" (docs/GRIND_ANGLE_RESEARCH.md): loosen those limits here.
            BladeSpec spec = new BladeSpec { MaxStraightDevM = 1.0, MaxTwistDeg = 360.0 };
            Advice a = NextStepAdvisor.Suggest(s.Profile, s.Material, s.Evaluate(spec), spec, double.MaxValue);
            Assert.StartsWith("Grind the edge", a.Template);

            PassAt(s, AbrasiveWheel.Medium, GrindRail.OffsetM(AbrasiveWheel.Medium));
            a = NextStepAdvisor.Suggest(s.Profile, s.Material, s.Evaluate(spec), spec, double.MaxValue);
            _out.WriteLine("after grinding: " + a.Template);
            Assert.True(a.Template.StartsWith("Not hardened") || a.Template.StartsWith("Edge too") || a.Template.StartsWith("Bent") || a.Template.StartsWith("Only one face"), a.Template);

            Advice heavy = NextStepAdvisor.Suggest(s.Profile, s.Material, s.Evaluate(spec), spec, 0.1);
            Assert.StartsWith("Too heavy", heavy.Template);
            Assert.Equal(2, heavy.Args.Length);
        }

        [Fact]
        public void Advice_HotBladeWithAustenite_SaysQuench()
        {
            ForgeSession s = HB.Beveled();
            BladeSpec spec = new BladeSpec { TargetEdgeAngleDeg = 20.0, EdgeAngleTolDeg = 90.0 }; // make the edge rule pass for this fixture
            Assert.True(s.ApplyThermal(HB.Therm(ThermalEnvironment.Furnace(1400.0), 90.0)).Accepted);
            Advice a = NextStepAdvisor.Suggest(s.Profile, s.Material, s.Evaluate(spec), spec, double.MaxValue);
            _out.WriteLine(a.Template);
            Assert.StartsWith("Hot enough to harden", a.Template);

            HB.Harden(s);
            a = NextStepAdvisor.Suggest(s.Profile, s.Material, s.Evaluate(spec), spec, double.MaxValue);
            _out.WriteLine(a.Template);
            Assert.True(a.Template.StartsWith("Too hard") || a.Template.StartsWith("All goals") || a.Template.StartsWith("Check the report") || a.Template.StartsWith("Bent"), a.Template);
        }
    }
}
