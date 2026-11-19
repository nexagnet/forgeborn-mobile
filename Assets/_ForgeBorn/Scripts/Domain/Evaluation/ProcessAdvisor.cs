using System;
using System.Globalization;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;

namespace ForgeBorn.Domain.Evaluation
{
    public enum AdviceLevel { Info, Warn, Danger, Good }

    /// <summary>A hint as a template plus arguments (the UI layer translates the template, like the order reasons).</summary>
    public sealed class Advice
    {
        public readonly string Template;
        public readonly string[] Args;
        public readonly AdviceLevel Level;

        public Advice(AdviceLevel level, string template, params string[] args)
        {
            Level = level;
            Template = template;
            Args = args ?? new string[0];
        }
    }

    public enum QuenchState { Idle, TooSlow, Partial, Full, CrackRisk }

    public struct QuenchReading
    {
        public QuenchState State;
        public double MinRateKPerS;
        public double MaxRateKPerS;
    }

    /// <summary>
    /// Live reading of a quench from the temperatures before and after a thermal step, so the player sees "too slow / hardening / crack risk"
    /// before the result is locked in. Uses the same thresholds as the thermal solver (nose rate for hardening, Ms rate for cracks).
    /// Rates are averages over the step, so they under-read at high time scales. Evidence label: gameplay-calibrated.
    /// </summary>
    public static class QuenchMonitor
    {
        public static QuenchReading Assess(BladeProfile p, MaterialData m, double[] prevTempK, double dtS)
        {
            var r = new QuenchReading { State = QuenchState.Idle, MinRateKPerS = 0.0, MaxRateKPerS = 0.0 };
            if (dtS <= 0.0 || prevTempK == null || prevTempK.Length != p.StationCount) return r;

            double min = double.MaxValue, max = 0.0;
            bool nearMs = false;
            int n = 0;
            // Mid-blade only (10 %..90 %), like the grader: the thick tang cools slowly and would hide a good edge quench.
            double len = p.X[p.StationCount - 1] - p.X[0];
            double xa = p.X[0] + 0.1 * len, xb = p.X[0] + 0.9 * len;
            for (int s = 0; s < p.StationCount; s++)
            {
                if (p.Severed[s] || p.X[s] < xa || p.X[s] > xb) continue;
                double t0 = prevTempK[s], t1 = p.TempK[s];
                if (t1 >= t0 || t1 > m.A1K || t1 < m.MsK) continue; // only stations cooling through the hardening band
                if (p.Austenite[s] <= 0.0 && p.Martensite[s] > 0.0) continue; // already transformed
                double rate = (t0 - t1) / dtS;
                n++;
                if (rate < min) min = rate;
                if (rate > max) max = rate;
                if (t1 <= m.MsK + 120.0 && rate > m.CrackCoolRateKPerS) nearMs = true;
            }
            if (n == 0) return r;

            r.MinRateKPerS = min;
            r.MaxRateKPerS = max;
            if (nearMs) r.State = QuenchState.CrackRisk;
            else if (min >= m.CriticalCoolRateKPerS) r.State = QuenchState.Full;
            else if (min >= 0.4 * m.CriticalCoolRateKPerS) r.State = QuenchState.Partial;
            else r.State = QuenchState.TooSlow;
            return r;
        }
    }

    /// <summary>Hardness after tempering for a given extra time, using the same Hollomon-Jaffe update as the thermal solver.</summary>
    public static class TemperForecast
    {
        public static double HrcAfter(MaterialData m, double martensite, double currentP, double tempK, double extraHours)
        {
            double tPrevH = currentP > 0.0 ? Math.Pow(10.0, Math.Max(-30.0, currentP / tempK - 20.0)) : 0.0;
            double tNew = tPrevH + Math.Max(0.0, extraHours);
            double pNew = tNew > 0.0 ? tempK * (20.0 + Math.Log10(tNew)) : 0.0;
            return m.HardnessHrc(martensite, Math.Max(currentP, pNew));
        }
    }

    /// <summary>
    /// "What next?" from the blade's state alone: the first rule that applies wins. Order follows the real sequence
    /// (shape, grind, harden, temper, deliver) but only advises, never blocks.
    /// </summary>
    public static class NextStepAdvisor
    {
        private static string N(double v) { return v.ToString("0", CultureInfo.InvariantCulture); }

        /// <param name="r">Report for the same blade and spec.</param>
        /// <param name="maxMassKg">Mass limit of the current order, or a huge value in free play.</param>
        public static Advice Suggest(BladeProfile p, MaterialData m, BladeReport r, BladeSpec spec, double maxMassKg)
        {
            if (spec == null) spec = new BladeSpec();
            if (r.Broken) return new Advice(AdviceLevel.Danger, "Blade is cut through: start a new blank (Order button).");
            if (r.CrackedStations > 0) return new Advice(AdviceLevel.Danger, "Blade is cracked: start a new blank (Order button).");

            double maxT = 0.0;
            for (int s = 0; s < p.StationCount; s++) if (!p.Severed[s] && p.TempK[s] > maxT) maxT = p.TempK[s];
            if (maxT > m.OverheatK) return new Advice(AdviceLevel.Danger, "Overheated ({0} C): let it cool in air, burnt steel stays weak.", N(maxT - 273.15));

            double mart = 0.0, aust = 0.0;
            int cnt = 0;
            double x0 = p.X[0] + 0.1 * r.LengthM, x1 = p.X[0] + 0.9 * r.LengthM;
            for (int s = 0; s < p.StationCount; s++)
            {
                if (p.Severed[s] || p.X[s] < x0 || p.X[s] > x1) continue;
                mart += p.Martensite[s];
                aust += p.Austenite[s];
                cnt++;
            }
            if (cnt > 0) { mart /= cnt; aust /= cnt; }
            bool hardened = mart >= 0.5;
            bool hot = maxT >= m.ForgeMinTempK;

            if (!hardened && (r.StraightnessScore < 0.6 || r.TwistScore < 0.6))
                return hot ? new Advice(AdviceLevel.Warn, "Bent or twisted and hot: hammer the high side now, it goes cold below {0} C.", N(m.ForgeMinTempK - 273.15))
                           : new Advice(AdviceLevel.Warn, "Bent or twisted: heat it in the furnace to {0} C or more, then hammer.", N(m.ForgeMinTempK - 273.15));

            if (r.MassKg > maxMassKg)
                return new Advice(AdviceLevel.Warn, "Too heavy ({0} g, max {1} g): grind the faces to remove metal.", N(r.MassKg * 1000.0), N(maxMassKg * 1000.0));

            if (!r.HasBevel)
                return new Advice(AdviceLevel.Info, "Grind the edge: set the angle to {0} deg, then drag the wheel along the edge, one face and then the other.", N(spec.TargetEdgeAngleDeg));

            if (r.BevelAsymmetry > 0.15)
                return new Advice(AdviceLevel.Info, "Only one face is ground: turn the blade over (Flip button) and grind the other face.");

            if (r.EdgeScore < 0.6)
            {
                return r.EdgeAngleDeg > spec.TargetEdgeAngleDeg
                    ? new Advice(AdviceLevel.Info, "Edge too blunt ({0} deg, aim {1}): grind a little more along the edge.", N(r.EdgeAngleDeg), N(spec.TargetEdgeAngleDeg))
                    : new Advice(AdviceLevel.Warn, "Edge too thin ({0} deg, aim {1}): metal cannot be put back. Press Ctrl+Z or start a new blank.", N(r.EdgeAngleDeg), N(spec.TargetEdgeAngleDeg));
            }

            if (!hardened)
            {
                return aust >= 0.6
                    ? new Advice(AdviceLevel.Info, "Hot enough to harden: quench now in water. Oil is too slow for this steel.")
                    : new Advice(AdviceLevel.Info, "Not hardened yet: heat to {0} C or more in the furnace until it glows evenly, then quench.", N(m.A1K - 273.15 + 100.0));
            }

            if (r.MeanHrc > spec.TargetEdgeHrc + spec.HrcTol)
                return new Advice(AdviceLevel.Info, "Too hard and brittle ({0} HRC, aim {1}): temper it in the oven.", N(r.MeanHrc), N(spec.TargetEdgeHrc));
            if (r.MeanHrc < spec.TargetEdgeHrc - spec.HrcTol)
                return new Advice(AdviceLevel.Warn, "Too soft ({0} HRC, aim {1}): heat and quench again.", N(r.MeanHrc), N(spec.TargetEdgeHrc));

            bool allOk = r.StraightnessScore >= 0.6 && r.TwistScore >= 0.6 && r.HardnessScore >= 0.6 && r.EdgeScore >= 0.6;
            return allOk ? new Advice(AdviceLevel.Good, "All goals are met: press Deliver.")
                         : new Advice(AdviceLevel.Info, "Check the report for the weakest score.");
        }
    }
}
