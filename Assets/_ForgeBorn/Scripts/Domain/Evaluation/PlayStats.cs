using System.Globalization;

namespace ForgeBorn.Domain.Evaluation
{
    /// <summary>Per-order counters (plan phase 0): what the player did, so difficulty can be tuned from play instead of guesses.</summary>
    public sealed class PlayStats
    {
        public int Strikes;
        public int MissedStrikes;
        public int ColdStrikes;
        public int Undos;
        public int GrindStrokes;
        public double Seconds;

        public void Reset()
        {
            Strikes = MissedStrikes = ColdStrikes = Undos = GrindStrokes = 0;
            Seconds = 0.0;
        }

        public void OnStrike(bool hit, bool cold)
        {
            Strikes++;
            if (!hit) MissedStrikes++;
            else if (cold) ColdStrikes++;
        }

        public void OnUndo() { Undos++; }
        public void OnGrindStroke() { GrindStrokes++; }
        public void AddTime(double dt) { if (dt > 0.0 && dt < 5.0) Seconds += dt; }

        public double MissRate { get { return Strikes == 0 ? 0.0 : (double)MissedStrikes / Strikes; } }
    }

    /// <summary>One CSV row per delivery. Stays on the player's machine (the game writes it next to the career file and sends nothing).</summary>
    public static class PlayLog
    {
        public const string Header = "order,delivered,grade,score,edge_deg,hrc,bend_mm,twist_deg,mass_g,cracked,net,seconds,strikes,missed,cold,undos,grind_strokes";

        public static string Row(string orderId, bool delivered, long net, BladeReport r, PlayStats st)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            return Safe(orderId) + "," + (delivered ? 1 : 0) + "," + Safe(r.Grade) + "," + r.Overall.ToString("0.0", c) + ","
                + (double.IsNaN(r.EdgeAngleDeg) ? "" : r.EdgeAngleDeg.ToString("0.0", c)) + "," + r.MeanHrc.ToString("0.0", c) + ","
                + (r.StraightnessDevM * 1000.0).ToString("0.00", c) + "," + r.TwistRangeDeg.ToString("0.00", c) + ","
                + (r.MassKg * 1000.0).ToString("0", c) + "," + (r.CrackedStations > 0 ? 1 : 0) + "," + net.ToString(c) + ","
                + st.Seconds.ToString("0", c) + "," + st.Strikes + "," + st.MissedStrikes + "," + st.ColdStrikes + "," + st.Undos + "," + st.GrindStrokes;
        }

        private static string Safe(string s) { return string.IsNullOrEmpty(s) ? "-" : s.Replace(',', ';').Replace('\n', ' '); }
    }
}
