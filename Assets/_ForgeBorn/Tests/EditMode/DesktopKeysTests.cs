using ForgeBorn.UnityLayer;
using NUnit.Framework;
using UnityEngine;

namespace ForgeBorn.Tests
{
    public class DesktopKeysTests
    {
        [Test]
        public void BracketsAndDigitsMap()
        {
            Assert.AreEqual(KeyAction.PressureDown, DesktopKeys.FromKey(KeyCode.LeftBracket));
            Assert.AreEqual(KeyAction.PressureUp, DesktopKeys.FromKey(KeyCode.RightBracket));
            Assert.AreEqual(KeyAction.Pause, DesktopKeys.FromKey(KeyCode.Escape));
            Assert.AreEqual(KeyAction.None, DesktopKeys.FromKey(KeyCode.Q));
            Assert.IsTrue(DesktopKeys.TryStation(DesktopKeys.FromKey(KeyCode.Alpha6), out Station s));
            Assert.AreEqual(Station.Inspect, s);
            Assert.IsFalse(DesktopKeys.TryStation(KeyAction.Pause, out _));
        }

        [Test]
        public void TabCyclesAllSixStations()
        {
            Station s = Station.Furnace;
            for (int i = 0; i < 6; i++) s = DesktopKeys.Next(s);
            Assert.AreEqual(Station.Furnace, s);
            Assert.AreEqual(Station.Anvil, DesktopKeys.Next(Station.Furnace));
        }

        [Test]
        public void StationsSelectToolOrEnvironment()
        {
            Assert.AreEqual(ToolMode.Hammer, DesktopKeys.ToolFor(Station.Anvil));
            Assert.AreEqual(ToolMode.Grind, DesktopKeys.ToolFor(Station.Grinder));
            Assert.IsNull(DesktopKeys.ToolFor(Station.Furnace));
            Assert.AreEqual(ForgeEnv.Water, DesktopKeys.EnvFor(Station.Quench));
            Assert.AreEqual(ForgeEnv.Oven, DesktopKeys.EnvFor(Station.Temper));
            Assert.IsNull(DesktopKeys.EnvFor(Station.Grinder));
        }

        [Test]
        public void StepsClamp()
        {
            Assert.AreEqual(5f, DesktopKeys.StepPressure(5f, -1, 5f, 200f));
            Assert.AreEqual(200f, DesktopKeys.StepPressure(198f, 1, 5f, 200f));
            Assert.AreEqual(85f, DesktopKeys.StepPressure(80f, 1, 5f, 200f));
            Assert.AreEqual(0, DesktopKeys.StepIndex(0, -1, 4));
            Assert.AreEqual(3, DesktopKeys.StepIndex(3, 1, 4));
        }
    }
}
