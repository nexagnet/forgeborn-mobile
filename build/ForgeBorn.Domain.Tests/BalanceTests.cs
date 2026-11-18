using System;
using System.Collections.Generic;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;
using ForgeBorn.Domain.Orders;
using ForgeBorn.Domain.Session;
using Xunit;
using Xunit.Abstractions;

namespace ForgeBorn.Domain.Tests
{
    /// <summary>Economy checks: can the game's own blanks meet the mass limits of the orders by grinding?</summary>
    public class BalanceTests
    {
        private readonly ITestOutputHelper _out;
        public BalanceTests(ITestOutputHelper o) { _out = o; }

        private static ForgeSession Tapered()
        {
            const double L = 0.8;
            return new ForgeSession(BladeFactory.CreateTapered(L, x => 0.025 - 0.010 * x / L, x => 0.004 - 0.002 * x / L, 192, 48), MaterialData.SteelBasicV1);
        }

        private static ForgeSession Beveled()
        {
            return new ForgeSession(BladeFactory.CreateBeveled(0.8, 0.05, 0.006, 0.0125, 0.001, 192, 48), MaterialData.SteelBasicV1);
        }

        private static void GrindFace(ForgeSession s)
        {
            var samples = new List<GrindSample>();
            for (int i = 0; i <= 140; i++) samples.Add(H.Top(0.05 + 0.005 * i, 0.0, 200.0, i == 0 ? 0.0 : 0.02));
            Assert.True(s.ApplyGrind(H.Cmd(samples.ToArray())).Accepted);
        }

        [Fact]
        public void MassLimits_AreBindingOnBothBlanks_SoTheyMatter()
        {
            Order o2 = OrderBook.ById("o2"), o3 = OrderBook.ById("o3");
            foreach (ForgeSession s in new[] { Tapered(), Beveled() })
            {
                Assert.True(s.MassKg > o2.MaxMassKg, "a fresh blank must be too heavy for order 2");
                Assert.True(s.MassKg > o3.MaxMassKg, "a fresh blank must be too heavy for order 3");
            }
        }

        [Fact]
        public void MassLimits_AreReachableWithOneGrindingPass_OnBothBlanks()
        {
            Order o2 = OrderBook.ById("o2"), o3 = OrderBook.ById("o3");
            foreach (ForgeSession s in new[] { Tapered(), Beveled() })
            {
                GrindFace(s);
                Assert.True(s.MassKg <= o3.MaxMassKg, "after one pass: " + s.MassKg);
                Assert.True(s.MassKg <= o2.MaxMassKg);
            }
        }

        [Fact]
        public void MassLimits_DoNotRequireRuiningTheBlade()
        {
            // A single pass must leave most of the steel: the limit asks for trimming, not for a sliver.
            foreach (ForgeSession s in new[] { Tapered(), Beveled() })
            {
                double before = s.MassKg;
                GrindFace(s);
                Assert.True(s.MassKg > 0.4 * before);
            }
        }
    }
}
