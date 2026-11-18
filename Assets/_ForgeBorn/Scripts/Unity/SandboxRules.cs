using System;
using System.Globalization;
using System.Text;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;

namespace ForgeBorn.UnityLayer
{
    public enum ForgeEnv { Air, Furnace, Water, Oil, Oven }

    /// <summary>
    /// Pure gameplay rules for the sandbox (no UnityEngine): kept separate so they can be unit-tested in EditMode
    /// and so every device sends the same quantised commands.
    /// </summary>
    public static class SandboxRules
    {
        /// <summary>Simulated seconds per real second. Long heat treatments (tempering) need the faster steps.</summary>
        public static readonly double[] TimeScales = { 1.0, 10.0, 60.0, 600.0 };

        public const double FurnaceK = 1500.0;
        public const double OvenK = 523.15; // 250 C
        public const double MinPressureN = 20.0, MaxPressureN = 150.0;

        /// <summary>Quantise to 5 N so the same gesture is the same command on every device.</summary>
        public static double QuantizePressure(double pressureN)
        {
            return Math.Round(pressureN / 5.0) * 5.0;
        }

        /// <summary>On-screen pressure doubles as blow energy: 1 J per N, clamped to the supported range.</summary>
        public static double HammerEnergyJ(double pressureN)
        {
            double e = QuantizePressure(pressureN);
            return Math.Min(MaxPressureN, Math.Max(MinPressureN, e));
        }

        public static ThermalEnvironment EnvFor(ForgeEnv env) { return EnvFor(env, false); }

        public static ThermalEnvironment EnvFor(ForgeEnv env, bool fastFurnace)
        {
            switch (env)
            {
                case ForgeEnv.Furnace: return fastFurnace ? ThermalEnvironment.FastFurnace(FurnaceK) : ThermalEnvironment.Furnace(FurnaceK);
                case ForgeEnv.Water: return ThermalEnvironment.Water();
                case ForgeEnv.Oil: return ThermalEnvironment.Oil();
                case ForgeEnv.Oven: return ThermalEnvironment.Oven(OvenK);
                default: return ThermalEnvironment.Air();
            }
        }

        /// <summary>
        /// Skip thermal commands when nothing would change: blade already in equilibrium with the environment.
        /// The oven always ticks while any station is in the tempering range, because tempering progresses at constant temperature.
        /// </summary>
        public static bool NeedsTick(BladeProfile p, ThermalEnvironment env, bool isOven)
        {
            for (int s = 0; s < p.StationCount; s++)
            {
                if (p.Severed[s]) continue;
                double t = p.TempK[s];
                if (Math.Abs(t - env.TempK) > 0.3) return true;
                if (isOven && t >= 373.0 && t < 1000.0) return true;
            }
            return false;
        }

        public static double MaxTempK(BladeProfile p)
        {
            double m = 0.0;
            for (int s = 0; s < p.StationCount; s++)
                if (!p.Severed[s] && p.TempK[s] > m) m = p.TempK[s];
            return m;
        }

        public static bool AnyCracked(BladeProfile p)
        {
            for (int s = 0; s < p.StationCount; s++) if (p.Damage[s] >= 1.0) return true;
            return false;
        }

        public static string F(double v, int decimals)
        {
            return v.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        /// <summary>Multi-line text of a blade report for the HUD panel.</summary>
        public static string FormatReport(BladeReport r)
        {
            var sb = new StringBuilder();
            sb.Append(Loc.F("Grade {0}   Score {1}   {2}", r.Grade, F(r.Overall, 1), Loc.T(r.Pass ? "PASS" : "not passed"))).Append('\n');
            if (r.Broken) sb.Append(Loc.T("BROKEN: blade is cut through")).Append('\n');
            if (r.CrackedStations > 0) sb.Append(Loc.F("CRACKED at {0} stations", r.CrackedStations)).Append('\n');
            sb.Append(Loc.F("Mass {0} kg   Length {1} mm", F(r.MassKg, 3), F(r.LengthM * 1000.0, 0))).Append('\n');
            sb.Append(Loc.F("Balance {0}% from tang  (score {1})", F(r.BalanceFrac * 100.0, 0), F(r.BalanceScore * 100.0, 0))).Append('\n');
            sb.Append(Loc.F("Straight {0} mm dev  ({1})", F(r.StraightnessDevM * 1000.0, 2), F(r.StraightnessScore * 100.0, 0))).Append('\n');
            sb.Append(Loc.F("Twist {0} deg  ({1})", F(r.TwistRangeDeg, 2), F(r.TwistScore * 100.0, 0))).Append('\n');
            sb.Append(Loc.F("Symmetry {0}", F(r.SymmetryScore * 100.0, 0))).Append('\n');
            if (r.HasBevel) sb.Append(Loc.F("Edge {0} deg, {1} mm at 0.5 mm  ({2})", F(r.EdgeAngleDeg, 1), F(r.EdgeThickness05M * 1000.0, 2), F(r.EdgeScore * 100.0, 0))).Append('\n');
            else sb.Append(Loc.T("Edge: no bevel ground yet")).Append('\n');
            sb.Append(Loc.F("Hardness {0} HRC  ({1})", F(r.MeanHrc, 0), F(r.HardnessScore * 100.0, 0))).Append('\n');
            sb.Append(Loc.F("Toughness {0}", F(r.ToughnessScore * 100.0, 0)));
            return sb.ToString();
        }
    }
}
