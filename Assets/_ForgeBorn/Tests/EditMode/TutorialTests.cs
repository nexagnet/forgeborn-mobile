using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Session;
using ForgeBorn.UnityLayer;
using NUnit.Framework;

namespace ForgeBorn.Tests
{
    public class TutorialTests
    {
        private static TutorialSnapshot Snap(double temp = 293, double hrc = 22, double temperP = 0, double removed = 0, int hits = 0,
            ToolMode tool = ToolMode.Grind, ForgeEnv env = ForgeEnv.Air, double scale = 1, bool report = false,
            bool order = false, bool delivered = false, int flips = 0)
        {
            return new TutorialSnapshot
            {
                MaxTempK = temp, Hrc = hrc, TemperP = temperP, RemovedG = removed, HammerHits = hits, Tool = tool, Env = env,
                TimeScale = scale, ReportOpen = report, HasOrder = order, Delivered = delivered, FlipCount = flips
            };
        }

        /// <summary>Advances a fresh guide past the welcome card and the "take an order" step, to the furnace step.</summary>
        private static TutorialFlow AtFurnaceStep()
        {
            var t = new TutorialFlow();
            t.Acknowledge(); t.Tick(Snap());
            Assert.AreEqual("order", t.Current.Id);
            t.Tick(Snap(order: true));
            Assert.AreEqual("furnace", t.Current.Id);
            return t;
        }

        [Test]
        public void StartsAtWelcome_AndOnlyAcknowledgementAdvancesIt()
        {
            var t = new TutorialFlow();
            Assert.AreEqual("welcome", t.Current.Id);
            Assert.IsFalse(t.Tick(Snap(env: ForgeEnv.Furnace, temp: 1400)), "playing does not skip the welcome card");
            t.Acknowledge();
            Assert.IsTrue(t.Tick(Snap()));
            Assert.AreEqual("order", t.Current.Id);
        }

        [Test]
        public void FullPlaythrough_VisitsEveryStepInOrder_FromOrderToDelivery_AndFinishes()
        {
            var t = new TutorialFlow();
            var order = new System.Collections.Generic.List<string>();
            order.Add(t.Current.Id);
            TutorialSnapshot[] script =
            {
                Snap(),                                                                                  // welcome (acknowledged below)
                Snap(order: true),                                                                       // take an order
                Snap(order: true, env: ForgeEnv.Furnace),                                                // furnace
                Snap(order: true, env: ForgeEnv.Furnace, scale: 60),                                     // speed
                Snap(order: true, env: ForgeEnv.Furnace, scale: 60, temp: 1150),                         // heat
                Snap(order: true, env: ForgeEnv.Furnace, temp: 1150, tool: ToolMode.Hammer),             // to_hammer
                Snap(order: true, temp: 1150, tool: ToolMode.Hammer, hits: 3),                           // strike
                Snap(order: true, temp: 1150, tool: ToolMode.Grind, hits: 3),                            // to_grind
                Snap(order: true, temp: 1150, tool: ToolMode.Grind, hits: 3, removed: 4),                // grind
                Snap(order: true, temp: 1150, removed: 4, flips: 1),                                     // flip
                Snap(order: true, temp: 1150, env: ForgeEnv.Furnace, removed: 4, flips: 1),              // reheat
                Snap(order: true, temp: 300, hrc: 60, env: ForgeEnv.Water, removed: 4, flips: 1),        // quench
                Snap(order: true, temp: 400, hrc: 55, temperP: 11, env: ForgeEnv.Oven, removed: 4),      // temper
                Snap(order: true, report: true, temperP: 11, hrc: 55),                                   // report
                Snap(order: true, report: true, temperP: 11, hrc: 55, delivered: true),                  // deliver
            };
            t.Acknowledge();
            foreach (TutorialSnapshot s in script)
            {
                if (t.Tick(s) && !t.Finished) order.Add(t.Current.Id);
            }
            Assert.AreEqual(new[] { "welcome", "order", "furnace", "speed", "heat", "to_hammer", "strike", "to_grind", "grind", "flip", "reheat", "quench", "temper", "report", "deliver", "done" }, order.ToArray());
            Assert.AreEqual("done", t.Current.Id);
            t.Acknowledge();
            Assert.IsTrue(t.Tick(Snap()));
            Assert.IsTrue(t.Finished);
        }

        [Test]
        public void StrikeStep_WarnsWhenTheBladeIsCold_ButNotWhenHot()
        {
            TutorialFlow t = AtFurnaceStep();
            t.Tick(Snap(env: ForgeEnv.Furnace));
            t.Tick(Snap(scale: 60));
            t.Tick(Snap(temp: 1200));
            t.Tick(Snap(temp: 1200, tool: ToolMode.Hammer));
            Assert.AreEqual("strike", t.Current.Id);
            Assert.IsNotNull(t.Warning(Snap(temp: 900, tool: ToolMode.Hammer)));
            Assert.IsNull(t.Warning(Snap(temp: 1200, tool: ToolMode.Hammer)));
        }

        [Test]
        public void SkipEndsTheGuide_WhenOptional_AndRestartBringsItBack()
        {
            var t = new TutorialFlow();
            t.Skip();
            Assert.IsTrue(t.Finished);
            Assert.IsNull(t.Current);
            Assert.IsFalse(t.Tick(Snap()));
            t.Restart();
            Assert.IsFalse(t.Finished);
            Assert.AreEqual("welcome", t.Current.Id);
        }

        [Test]
        public void MandatoryGuide_CannotBeSkipped_UntilItIsCompleted()
        {
            var t = new TutorialFlow { Mandatory = true };
            t.Skip();
            Assert.IsFalse(t.Skipped, "a first-time player must not be able to skip");
            Assert.IsFalse(t.Finished);
            Assert.AreEqual("welcome", t.Current.Id);

            t.Mandatory = false; // the sandbox clears this once the last step is done
            t.Skip();
            Assert.IsTrue(t.Finished);
        }

        [Test]
        public void RewindTo_GoesBackOnly_AndLeavesLaterStepsAlone()
        {
            TutorialFlow t = AtFurnaceStep();
            t.Tick(Snap(order: true, env: ForgeEnv.Furnace));
            t.Tick(Snap(order: true, scale: 60));
            Assert.AreEqual("heat", t.Current.Id);

            t.RewindTo("heat");              // not behind the current step: ignored
            Assert.AreEqual("heat", t.Current.Id);
            t.RewindTo("deliver");           // ahead of the current step: ignored
            Assert.AreEqual("heat", t.Current.Id);
            t.RewindTo("order");
            Assert.AreEqual("order", t.Current.Id);
            t.RewindTo("no_such_step");
            Assert.AreEqual("order", t.Current.Id);
            Assert.AreEqual(1, t.IndexOf("order"));
            Assert.AreEqual(-1, t.IndexOf("no_such_step"));
        }

        [Test]
        public void DeliverStep_WaitsForDelivery_NotJustTheReport()
        {
            var t = new TutorialFlow();
            while (t.Current.Id != "deliver")
            {
                t.Acknowledge();
                t.Tick(Snap(temp: 1200, hrc: 60, temperP: 12, removed: 10, hits: 5, tool: t.Current.Id == "to_hammer" ? ToolMode.Hammer : ToolMode.Grind,
                    env: ForgeEnv.Furnace, scale: 600, report: true, order: true, flips: 1));
            }
            Assert.IsFalse(t.Tick(Snap(order: true, report: true)), "report open is not enough");
            Assert.IsTrue(t.Tick(Snap(order: true, report: true, delivered: true)));
            Assert.AreEqual("done", t.Current.Id);
        }

        [Test]
        public void EveryStepExplainsWhy_AndHasAnAction()
        {
            var t = new TutorialFlow();
            int guard = 0;
            while (!t.Finished && guard++ < 50)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Current.Title));
                Assert.Greater(t.Current.Why.Length, 20, t.Current.Id);
                Assert.Greater(t.Current.Action.Length, 5, t.Current.Id);
                t.Acknowledge();
                ToolMode tool = t.Current.Id == "to_grind" || t.Current.Id == "grind" ? ToolMode.Grind : ToolMode.Hammer;
                t.Tick(Snap(temp: 1200, hrc: 60, temperP: 12, removed: 10, hits: 5, tool: tool, env: ForgeEnv.Furnace, scale: 600, report: true, order: true, delivered: true, flips: 1));
            }
            Assert.Less(guard, 50);
        }

        /// <summary>The thresholds in the guide must be reachable with the real metallurgy, not just in a script.</summary>
        [Test]
        public void GuideThresholds_AreReachableWithTheRealThermalModel()
        {
            BladeProfile blade = BladeFactory.CreateTapered(0.8, x => 0.025 - 0.010 * x / 0.8, x => 0.004 - 0.002 * x / 0.8, 257, 48);
            var session = new ForgeSession(blade, MaterialData.SteelBasicV1);
            long id = 1;
            Assert.IsTrue(session.ApplyThermal(new ThermalCommand(id++, SandboxRules.EnvFor(ForgeEnv.Furnace), 90)).Accepted);
            Assert.GreaterOrEqual(SandboxRules.MaxTempK(session.Profile), TutorialFlow.ForgeTempK, "90 s in the furnace reaches forging heat");

            session.Thermal.StepsPerCommand = 2000;
            Assert.IsTrue(session.ApplyThermal(new ThermalCommand(id++, SandboxRules.EnvFor(ForgeEnv.Water), 30)).Accepted);
            int mid = session.Profile.StationCount / 2;
            double hrc = session.Material.HardnessHrc(session.Profile.Martensite[mid], session.Profile.TemperP[mid]);
            Assert.GreaterOrEqual(hrc, TutorialFlow.QuenchHrc, "water quench hardens");

            session.Thermal.StepsPerCommand = 500;
            for (int i = 0; i < 20; i++)
                Assert.IsTrue(session.ApplyThermal(new ThermalCommand(id++, SandboxRules.EnvFor(ForgeEnv.Oven), 300)).Accepted);
            Assert.GreaterOrEqual(session.Profile.TemperP[mid], TutorialFlow.TemperDone, "oven tempering reaches straw colour");
        }
    }
}
