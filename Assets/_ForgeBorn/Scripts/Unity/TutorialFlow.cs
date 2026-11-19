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
        public bool HasOrder;      // an order has been taken (not free play)
        public bool Delivered;     // the current order has been handed to the client
        public int FlipCount;      // how many times the player flipped the blade
    }

    public enum TutorialTarget { None, EnvFurnace, EnvWater, EnvOven, Speed, ToolButton, Report, Next, Order, Deliver, Flip }

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
    /// Guided first order, from taking the order to delivering the blade: why each station exists and in what order.
    /// Advances one step per Tick when the current step's goal is met. The first time a player starts it is Mandatory
    /// (cannot be skipped) until it has been completed once; afterwards the Guide button restarts an optional, skippable copy.
    /// Pure logic: the sandbox feeds snapshots, draws the panel and stores the "done" flag.
    /// </summary>
    public sealed class TutorialFlow
    {
        public const double ForgeTempK = 1100.0;   // bright orange: above the 1050 K minimum forging temperature
        public const double QuenchHrc = 45.0;
        public const double TemperDone = 10.5;     // straw oxide colour
        public const int HammerHitsNeeded = 3;
        public const double GrindNeededG = 3.0;
        public const string DonePrefsKey = "fb.tutorial.done";

        private readonly TutorialStep[] _steps;
        private bool _acked;

        public int Index { get; private set; }
        public bool Skipped { get; private set; }
        /// <summary>True until the player has finished the guide once: Skip is refused while this is set.</summary>
        public bool Mandatory { get; set; }
        public int Count => _steps.Length;
        public System.Collections.Generic.IReadOnlyList<TutorialStep> Steps => _steps;
        public bool Finished => Skipped || Index >= _steps.Length;
        public TutorialStep Current => Finished ? null : _steps[Index];

        public TutorialFlow()
        {
            _steps = new[]
            {
                new TutorialStep { Id = "welcome", Title = "Welcome to the forge",
                    Why = "Your job: turn a steel bar into a blade a client will pay for. This short guide is required the first time and walks you through one complete order.",
                    Action = "Click Next to begin.", Target = TutorialTarget.Next, NeedsAck = true, Done = s => false },
                new TutorialStep { Id = "order", Title = "1. Take an order",
                    Why = "A client asks for a blade and tells you the pay. The goals stay on screen while you work, so you always know what is wanted.",
                    Action = "Click Order (bottom bar) once to take the first order.", Target = TutorialTarget.Order, Done = s => s.HasOrder },
                new TutorialStep { Id = "furnace", Title = "2. Light the furnace",
                    Why = "Cold steel is hard to shape and cracks when hammered. Hot steel flows like soft clay.",
                    Action = "Click Furnace to put the blade in the fire.", Target = TutorialTarget.EnvFurnace, Done = s => s.Env == ForgeEnv.Furnace },
                new TutorialStep { Id = "speed", Title = "3. Speed up time",
                    Why = "Heating takes minutes in real life. Time scale lets you wait without waiting.",
                    Action = "Click the x1 button until it reads x60.", Target = TutorialTarget.Speed,
                    Done = s => s.TimeScale >= 60.0 || s.MaxTempK >= ForgeTempK },
                new TutorialStep { Id = "heat", Title = "4. Heat to bright orange",
                    Why = "Above about 830 C (1100 K) steel is soft enough to forge. Colour tells you the temperature: dull red, orange, yellow.",
                    Action = "Wait until the blade glows orange. Do not overheat: past 1500 K steel is ruined.", Target = TutorialTarget.None,
                    Done = s => s.MaxTempK >= ForgeTempK },
                new TutorialStep { Id = "to_hammer", Title = "5. Pick up the hammer",
                    Why = "The hammer pushes metal sideways: the blade gets wider where you strike, without losing any mass.",
                    Action = "Click the tool button (top left) until it reads HAMMER, or press 2.", Target = TutorialTarget.ToolButton,
                    Done = s => s.Tool == ToolMode.Hammer },
                new TutorialStep { Id = "strike", Title = "6. Strike while it is hot",
                    Why = "Hammer only while the steel glows. Below 777 C (1050 K) it is too cold: it dents and cracks.",
                    Action = "Click the blade 3 times. If it goes dark, return to the Furnace first.", Target = TutorialTarget.None,
                    Done = s => s.HammerHits >= HammerHitsNeeded,
                    Warn = s => s.MaxTempK < 1050.0 ? "Too cold to forge! Put it back in the Furnace." : null },
                new TutorialStep { Id = "to_grind", Title = "7. Switch to the grinder",
                    Why = "Grinding removes metal for good. It is how you cut the edge bevel and the final shape.",
                    Action = "Click the tool button until it reads GRIND, or press 3.", Target = TutorialTarget.ToolButton,
                    Done = s => s.Tool == ToolMode.Grind },
                new TutorialStep { Id = "grind", Title = "8. Grind an edge",
                    Why = "Mass leaves with every pass; more pressure cuts faster but heats and roughens the edge. Watch the removed grams.",
                    Action = "Hold the left mouse button and drag along the blade edge to remove at least 3 g.", Target = TutorialTarget.None,
                    Done = s => s.RemovedG >= GrindNeededG },
                new TutorialStep { Id = "flip", Title = "9. Flip the blade",
                    Why = "A blade has two faces. Grind one, flip it, then grind the other, or the edge stays lopsided and too thick.",
                    Action = "Click Flip (bottom bar) or press F. Q and E change the grind angle.", Target = TutorialTarget.Flip,
                    Done = s => s.FlipCount >= 1 },
                new TutorialStep { Id = "reheat", Title = "10. Heat again for hardening",
                    Why = "To harden steel you first heat it bright orange, so its structure changes (austenite). Hot steel that is cooled fast becomes hard.",
                    Action = "Click Furnace and wait until the blade glows orange again.", Target = TutorialTarget.EnvFurnace,
                    Done = s => s.MaxTempK >= ForgeTempK && s.Env == ForgeEnv.Furnace },
                new TutorialStep { Id = "quench", Title = "11. Quench in water",
                    Why = "Fast cooling locks the steel into hard martensite (HRC goes up to about 60). Too fast on a thick blade and it cracks.",
                    Action = "Click Water and let the blade cool until HRC reads 45 or more.", Target = TutorialTarget.EnvWater,
                    Done = s => s.Hrc >= QuenchHrc,
                    Warn = s => s.Cracked ? "The blade cracked. Press Order for a new blade and quench gentler next time." : null },
                new TutorialStep { Id = "temper", Title = "12. Temper in the oven",
                    Why = "Fresh martensite is brittle like glass. Gentle reheating trades a little hardness for toughness; the surface colour shows how far you went.",
                    Action = "Click Oven and raise the time scale to x600 until the blade turns straw yellow.", Target = TutorialTarget.EnvOven,
                    Done = s => s.TemperP >= TemperDone },
                new TutorialStep { Id = "report", Title = "13. Read the report",
                    Why = "The report scores edge angle, straightness, twist, hardness, toughness and balance, and tells you why.",
                    Action = "Click Report.", Target = TutorialTarget.Report, Done = s => s.ReportOpen },
                new TutorialStep { Id = "deliver", Title = "14. Deliver the blade",
                    Why = "The client scores your blade against the order and pays you. If it is rejected, the report says exactly why.",
                    Action = "Click Deliver (bottom bar).", Target = TutorialTarget.Deliver, Done = s => s.Delivered },
                new TutorialStep { Id = "done", Title = "You delivered your first blade",
                    Why = "That is the whole loop: order, heat, hammer, grind, harden, temper, deliver. Next orders pay more and ask for more.",
                    Action = "Click Next to keep playing.", Target = TutorialTarget.Next, NeedsAck = true, Done = s => false },
            };
        }

        public void Acknowledge() { _acked = true; }

        /// <summary>Ends the guide. Ignored while it is Mandatory: a first-time player cannot skip it.</summary>
        public void Skip() { if (!Mandatory) Skipped = true; }

        public void Restart() { Index = 0; Skipped = false; _acked = false; }

        /// <summary>Goes back to the named step (used when the blade is replaced mid-guide, so no step is left unreachable).</summary>
        public void RewindTo(string id)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i].Id != id) continue;
                if (i < Index) { Index = i; _acked = false; }
                return;
            }
        }

        /// <summary>Shows the named step regardless of progress. Only for screenshot capture and tests.</summary>
        public void JumpTo(string id)
        {
            int i = IndexOf(id);
            if (i < 0) return;
            Index = i; Skipped = false; _acked = false;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < _steps.Length; i++) if (_steps[i].Id == id) return i;
            return -1;
        }

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
