using ForgeBorn.UnityLayer;
using NUnit.Framework;

namespace ForgeBorn.Tests
{
    public class SteamTests
    {
        [Test]
        public void NoSteamOutsideLiquidBaths()
        {
            foreach (ForgeEnv e in new[] { ForgeEnv.Air, ForgeEnv.Furnace, ForgeEnv.Oven })
                Assert.AreEqual(0f, SteamModel.PuffsPerSecond(1300.0, e), e.ToString());
        }

        [Test]
        public void ColdBlade_DoesNotSteam()
        {
            Assert.AreEqual(0f, SteamModel.PuffsPerSecond(293.0, ForgeEnv.Water));
            Assert.AreEqual(0f, SteamModel.PuffsPerSecond(SteamModel.ColdK, ForgeEnv.Water));
        }

        [Test]
        public void RateGrowsWithTemperature_AndSaturates()
        {
            float a = SteamModel.PuffsPerSecond(600.0, ForgeEnv.Water);
            float b = SteamModel.PuffsPerSecond(900.0, ForgeEnv.Water);
            float c = SteamModel.PuffsPerSecond(SteamModel.FullK, ForgeEnv.Water);
            float d = SteamModel.PuffsPerSecond(1800.0, ForgeEnv.Water);
            Assert.Less(a, b);
            Assert.Less(b, c);
            Assert.AreEqual(SteamModel.WaterPuffsPerSecond, c, 1e-4f);
            Assert.AreEqual(c, d, 1e-4f, "capped above the full-rate temperature");
        }

        [Test]
        public void OilSteamsLessThanWater_AndIsDarker()
        {
            Assert.Less(SteamModel.PuffsPerSecond(1100.0, ForgeEnv.Oil), SteamModel.PuffsPerSecond(1100.0, ForgeEnv.Water));
            Assert.Less(SteamModel.Tint(ForgeEnv.Oil).r, SteamModel.Tint(ForgeEnv.Water).r);
        }
    }
}
