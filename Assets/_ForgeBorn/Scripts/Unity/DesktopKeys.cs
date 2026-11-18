using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>Workshop stations in the order of GAME_DESIGN §13 (keys 1-6).</summary>
    public enum Station { Furnace, Anvil, Grinder, Quench, Temper, Inspect }

    public enum KeyAction { None, PressureDown, PressureUp, SpeedDown, SpeedUp, NextStation, Station1, Station2, Station3, Station4, Station5, Station6, Pause }

    /// <summary>Pure key/station mapping so it can be tested without a window or a Unity input device.</summary>
    public static class DesktopKeys
    {
        public const float PressureStepN = 5f;

        public static KeyAction FromKey(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftBracket: return KeyAction.PressureDown;
                case KeyCode.RightBracket: return KeyAction.PressureUp;
                case KeyCode.Minus: case KeyCode.KeypadMinus: return KeyAction.SpeedDown;
                case KeyCode.Equals: case KeyCode.KeypadPlus: return KeyAction.SpeedUp;
                case KeyCode.Tab: return KeyAction.NextStation;
                case KeyCode.Alpha1: return KeyAction.Station1;
                case KeyCode.Alpha2: return KeyAction.Station2;
                case KeyCode.Alpha3: return KeyAction.Station3;
                case KeyCode.Alpha4: return KeyAction.Station4;
                case KeyCode.Alpha5: return KeyAction.Station5;
                case KeyCode.Alpha6: return KeyAction.Station6;
                case KeyCode.Escape: return KeyAction.Pause;
                default: return KeyAction.None;
            }
        }

        public static bool TryStation(KeyAction a, out Station s)
        {
            s = Station.Furnace;
            if (a < KeyAction.Station1 || a > KeyAction.Station6) return false;
            s = (Station)(a - KeyAction.Station1);
            return true;
        }

        public static Station Next(Station s) { return (Station)(((int)s + 1) % 6); }

        /// <summary>The tool a station uses; stations that only change the environment keep the current tool.</summary>
        public static ToolMode? ToolFor(Station s)
        {
            switch (s)
            {
                case Station.Anvil: return ToolMode.Hammer;
                case Station.Grinder: return ToolMode.Grind;
                case Station.Inspect: return ToolMode.Inspect;
                default: return null;
            }
        }

        /// <summary>The environment a station selects, or null when it does not change it.</summary>
        public static ForgeEnv? EnvFor(Station s)
        {
            switch (s)
            {
                case Station.Furnace: return ForgeEnv.Furnace;
                case Station.Quench: return ForgeEnv.Water;
                case Station.Temper: return ForgeEnv.Oven;
                default: return null;
            }
        }

        public static float StepPressure(float current, int direction, float min, float max)
        {
            return Mathf.Clamp(current + direction * PressureStepN, min, max);
        }

        public static int StepIndex(int index, int direction, int count)
        {
            return Mathf.Clamp(index + direction, 0, count - 1);
        }
    }
}
