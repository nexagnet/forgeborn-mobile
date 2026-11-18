using System;

namespace ForgeBorn.UnityLayer
{
    /// <summary>What the tutorial needs to know about the game right now (plain numbers, no Domain types, easy to test).</summary>
    public struct TutorialSnapshot
    {
        public double MaxTempK;
        public double Hrc;
        public double TemperP;     // same units as BladeVisuals oxide table (about 9.8 .. 15)
        public double RemovedG;
        public int HammerHits;
        public ToolMode Tool;
        public ForgeEnv Env;
        public double TimeScale;
        public bool ReportOpen;
        public bool Cracked;
    }

    public enum TutorialTarget { None, EnvFurnace, EnvWater, EnvOven, Speed, ToolButton, Report, Next }

    public sealed class TutorialStep
    {
        public string Id, Title, Why, Action;
        public TutorialTarget Target;
        public Func<TutorialSnapshot, bool> Done;
        /// <summary>Optional warning shown when the player is doing it wrong (null = none).</summary>
        public Func<TutorialSnapshot, string> Warn;
        public bool NeedsAck; // finished by tapping Next, not by playing
    }

    /// <summary>
    /// Guided first forge: why each station exists and in what order. Advances one step per Tick when the current step's goal is met.
    /// Skippable at any time. Pure logic: the sandbox feeds snapshots and draws the panel.
    /// </summary>
    public sealed class TutorialFlow
    {
        public const double ForgeTempK = 1100.0;   // bright orange: above the 1050 K minimum forging temperature
        public const double QuenchHrc = 45.0;
        public const double TemperDone = 10.5;     // straw oxide colour
        public const int HammerHitsNeeded = 3;
        public const double GrindNeededG = 3.0;

        private readonly TutorialStep[] _steps;
        private bool _acked;

        public int Index { get; private set; }
        public bool Skipped { get; private set; }
        public int Count => _steps.Length;
        public System.Collections.Generic.IReadOnlyList<TutorialStep> Steps => _steps;
        public bool Finished => Skipped || Index >= _steps.Length;
        public TutorialStep Current => Finished ? null : _steps[Index];

        public TutorialFlow()
        {
            _steps = new[]
            {
                new TutorialStep { Id = "welcome", Title = "Welcome to the forge",
                    Why = "You will turn a plain steel bar into a blade. Every step changes the real metal: heat, shape, hardness.",
                    Action = "Click Next. You can Skip this guide at any time.", Target = TutorialTarget.Next, NeedsAck = true, Done = s => false },
                new TutorialStep { Id = "furnace", Title = "1. Light the furnace",
                    Why = "Cold steel is hard to shape and cracks when hammered. Hot steel flows like soft clay.",
                    Action = "Click Furnace to put the blade in the fire.", Target = TutorialTarget.EnvFurnace, Done = s => s.Env == ForgeEnv.Furnace },
                new TutorialStep { Id = "speed", Title = "2. Speed up time",
                    Why = "Heating takes minutes in real life. Time scale lets you wait without waiting.",
                    Action = "Click the x1 button until it reads x60.", Target = TutorialTarget.Speed,
                    Done = s => s.TimeScale >= 60.0 || s.MaxTempK >= ForgeTempK },
                new TutorialStep { Id = "heat", Title = "3. Heat to bright orange",
                    Why = "Above about 830 C (1100 K) steel is soft enough to forge. Colour tells you the temperature: dull red, orange, yellow.",
                    Action = "Wait until the blade glows orange. Do not overheat: past 1500 K steel is ruined.", Target = TutorialTarget.None,
                    Done = s => s.MaxTempK >= ForgeTempK },
                new TutorialStep { Id = "to_hammer", Title = "4. Pick up the hammer",
                    Why = "The hammer pushes metal sideways: the blade gets wider where you strike, without losing any mass.",
                    Action = "Click the tool button (top left) until it reads HAMMER.", Target = TutorialTarget.ToolButton,
                    Done = s => s.Tool == ToolMode.Hammer },
                new TutorialStep { Id = "strike", Title = "5. Strike while it is hot",
                    Why = "Hammer only while the steel glows. Below 777 C (1050 K) it is too cold: it dents and cracks.",
                    Action = "Click the blade 3 times. If it goes dark, return to the Furnace first.", Target = TutorialTarget.None,
                    Done = s => s.HammerHits >= HammerHitsNeeded,
                    Warn = s => s.MaxTempK < 1050.0 ? "Too cold to forge! Put it back in the Furnace." : null },
                new TutorialStep { Id = "to_grind", Title = "6. Switch to the grinder",
                    Why = "Grinding removes metal for good. It is how you cut the edge bevel and the final shape.",
                    Action = "Click the tool button until it reads GRIND.", Target = TutorialTarget.ToolButton,
                    Done = s => s.Tool == ToolMode.Grind },
                new TutorialStep { Id = "grind", Title = "7. Grind an edge",
                    Why = "Mass leaves with every pass; more pressure cuts faster but heats and roughens the edge. Watch the removed grams.",
                    Action = "Hold the left mouse button and drag along the blade edge to remove at least 3 g.", Target = TutorialTarget.None,
                    Done = s => s.RemovedG >= GrindNeededG },
                new TutorialStep { Id = "reheat", Title = "8. Heat again for hardening",
                    Why = "To harden steel you first heat it bright orange, so its structure changes (austenite). Hot steel that is cooled fast becomes hard.",
                    Action = "Click Furnace and wait until the blade glows orange again.", Target = TutorialTarget.EnvFurnace,
                    Done = s => s.MaxTempK >= ForgeTempK && s.Env == ForgeEnv.Furnace },
                new TutorialStep { Id = "quench", Title = "9. Quench in water",
                    Why = "Fast cooling locks the steel into hard martensite (HRC goes up to about 60). Too fast on a thick blade and it cracks.",
                    Action = "Click Water and let the blade cool until HRC reads 45 or more.", Target = TutorialTarget.EnvWater,
                    Done = s => s.Hrc >= QuenchHrc,
                    Warn = s => s.Cracked ? "The blade cracked. Press Order for a new blade, or try Oil next time." : null },
                new TutorialStep { Id = "temper", Title = "10. Temper in the oven",
                    Why = "Fresh martensite is brittle like glass. Gentle reheating trades a little hardness for toughness; the surface colour shows how far you went.",
                    Action = "Click Oven and raise the time scale to x600 until the blade turns straw yellow.", Target = TutorialTarget.EnvOven,
                    Done = s => s.TemperP >= TemperDone },
                new TutorialStep { Id = "report", Title = "11. Read the report",
                    Why = "The report scores edge angle, straightness, twist, hardness, toughness and balance, and tells you why.",
                    Action = "Click Report.", Target = TutorialTarget.Report, Done = s => s.ReportOpen },
                new TutorialStep { Id = "done", Title = "You made a blade",
                    Why = "That is the whole loop: heat, hammer, grind, harden, temper, judge.",
                    Action = "Click Next to play freely, or press Order to get a new blade.", Target = TutorialTarget.Next, NeedsAck = true, Done = s => false },
            };
        }

        public void Acknowledge() { _acked = true; }

        public void Skip() { Skipped = true; }

        public void Restart() { Index = 0; Skipped = false; _acked = false; }

        /// <summary>Advance when the current goal is met. Returns true when the step changed.</summary>
        public bool Tick(TutorialSnapshot s)
        {
            if (Finished) return false;
            TutorialStep st = _steps[Index];
            bool done = st.NeedsAck ? _acked : st.Done(s);
            if (!done) return false;
            Index++;
            _acked = false;
            return true;
        }

        public string Warning(TutorialSnapshot s)
        {
            if (Finished) return null;
            Func<TutorialSnapshot, string> w = _steps[Index].Warn;
            return w == null ? null : w(s);
        }
    }
}
