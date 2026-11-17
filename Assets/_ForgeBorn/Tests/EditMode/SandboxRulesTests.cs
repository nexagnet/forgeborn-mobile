using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Session;
using ForgeBorn.UnityLayer;
using NUnit.Framework;

namespace ForgeBorn.Tests
{
    public class SandboxRulesTests
    {
        private static ForgeSession Session()
        {
            return new ForgeSession(BladeFactory.CreateBeveled(0.8, 0.05, 0.006, 0.0125, 0.001, 96, 48), MaterialData.SteelBasicV1);
        }

        [Test]
        public void Pressure_IsQuantisedTo5N_AndHammerEnergyIsClamped()
        {
            Assert.AreEqual(80.0, SandboxRules.QuantizePressure(82.0));
            Assert.AreEqual(85.0, SandboxRules.QuantizePressure(83.0));
            Assert.AreEqual(20.0, SandboxRules.HammerEnergyJ(5.0));
            Assert.AreEqual(150.0, SandboxRules.HammerEnergyJ(900.0));
            Assert.AreEqual(100.0, SandboxRules.HammerEnergyJ(101.0));
        }

        [Test]
        public void EnvironmentsHaveExpectedTemperatures()
        {
            Assert.AreEqual(SandboxRules.FurnaceK, SandboxRules.EnvFor(ForgeEnv.Furnace).TempK);
            Assert.AreEqual(SandboxRules.OvenK, SandboxRules.EnvFor(ForgeEnv.Oven).TempK);
            Assert.AreEqual(293.15, SandboxRules.EnvFor(ForgeEnv.Air).TempK, 1e-9);
            Assert.AreEqual("water", SandboxRules.EnvFor(ForgeEnv.Water).Id);
            Assert.AreEqual("oil", SandboxRules.EnvFor(ForgeEnv.Oil).Id);
        }

        [Test]
        public void NeedsTick_SkipsEquilibrium_ButNotHotBladesOrTheOven()
        {
            ForgeSession s = Session();
            Assert.IsFalse(SandboxRules.NeedsTick(s.Profile, SandboxRules.EnvFor(ForgeEnv.Air), false));
            Assert.IsTrue(SandboxRules.NeedsTick(s.Profile, SandboxRules.EnvFor(ForgeEnv.Furnace), false));
            Assert.IsTrue(SandboxRules.NeedsTick(s.Profile, SandboxRules.EnvFor(ForgeEnv.Oven), true));

            for (int i = 0; i < s.Profile.StationCount; i++) s.Profile.TempK[i] = 900.0;
            Assert.IsTrue(SandboxRules.NeedsTick(s.Profile, SandboxRules.EnvFor(ForgeEnv.Air), false));

            for (int i = 0; i < s.Profile.StationCount; i++) s.Profile.TempK[i] = SandboxRules.OvenK;
            Assert.IsTrue(SandboxRules.NeedsTick(s.Profile, SandboxRules.EnvFor(ForgeEnv.Oven), true), "tempering continues at constant temperature");
            Assert.IsFalse(SandboxRules.NeedsTick(s.Profile, SandboxRules.EnvFor(ForgeEnv.Oven), false));
        }

        [Test]
        public void ThermalTick_WithRulesEnvironment_HeatsTheBlade()
        {
            ForgeSession s = Session();
            var r = s.ApplyThermal(new ThermalCommand(0, SandboxRules.EnvFor(ForgeEnv.Furnace), 150.0));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.Greater(SandboxRules.MaxTempK(s.Profile), 1000.0);
        }

        [Test]
        public void FormatReport_ContainsGradeScoreAndEdgeAngle()
        {
            ForgeSession s = Session();
            string text = SandboxRules.FormatReport(s.Evaluate(null));
            StringAssert.Contains("Grade", text);
            StringAssert.Contains("Score", text);
            StringAssert.Contains("Edge", text);
            StringAssert.Contains("HRC", text);
            StringAssert.DoesNotContain(",", text.Substring(0, 40), "numbers must use invariant culture");
        }

        [Test]
        public void FormatReport_FlagsCrackedAndBrokenBlades()
        {
            ForgeSession s = Session();
            s.Profile.Damage[10] = 2.0;
            StringAssert.Contains("CRACKED", SandboxRules.FormatReport(s.Evaluate(null)));
            Assert.IsTrue(SandboxRules.AnyCracked(s.Profile));
        }
    }
}
