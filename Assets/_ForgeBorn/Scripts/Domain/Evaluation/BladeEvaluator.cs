using System;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;

namespace ForgeBorn.Domain.Evaluation
{
    /// <summary>Targets a finished blade is scored against. Gameplay scoring, not an engineering certification.</summary>
    public sealed class BladeSpec
    {
        public double TargetEdgeAngleDeg = 20.0;
        public double EdgeAngleTolDeg = 10.0;
        public double TargetEdgeHrc = 58.0;
        public double HrcTol = 12.0;
        /// <summary>Centerline deviation (m) at which the straightness score reaches 0.</summary>
        public double MaxStraightDevM = 0.004;
        /// <summary>Twist range (deg) at which the twist score reaches 0.</summary>
        public double MaxTwistDeg = 3.0;
        public double TargetBalanceFrac = 0.40;
        public double BalanceTol = 0.20;
        public double PassScore = 70.0;
    }

    public sealed class BladeReport
    {
        public double MassKg;
        public double LengthM;
        /// <summary>Center of mass along the blade, 0 at the tang end, 1 at the tip.</summary>
        public double BalanceFrac;

        public double StraightnessDevM;
        public double StraightnessScore;
        public double TwistRangeDeg;
        public double TwistScore;
        public double SymmetryScore;

        public bool HasBevel;
        /// <summary>Mean included edge angle (deg) of the +Y edge; NaN if no bevel.</summary>
        public double EdgeAngleDeg;
        /// <summary>Mean thickness (m) 0.5 mm behind the edge tip; NaN if not measurable.</summary>
        public double EdgeThickness05M;
        public double EdgeScore;

        public double MeanHrc;
        public double HardnessScore;
        public double ToughnessScore;
        public double BalanceScore;

        public int SeveredStations;
        public int CrackedStations;
        public bool Broken;
        public double Overall;
        public bool Pass;
        public string Grade;
    }

    public static class BladeEvaluator
    {
        public static BladeReport Evaluate(BladeProfile p, MaterialData m, BladeSpec spec)
        {
            if (spec == null) spec = new BladeSpec();
            var r = new BladeReport();
            int count = p.StationCount;

            r.LengthM = p.X[count - 1] - p.X[0];
            r.MassKg = p.VolumeM3() * m.DensityKgPerM3;
            double comX = p.CenterOfMassX();
            r.BalanceFrac = double.IsNaN(comX) ? 0.0 : (comX - p.X[0]) / r.LengthM;

            for (int s = 0; s < count; s++)
            {
                if (p.Severed[s]) r.SeveredStations++;
                if (p.Damage[s] >= 1.0) r.CrackedStations++;
            }
            r.Broken = r.SeveredStations > 0;

            int first = -1, last = -1;
            for (int s = 0; s < count; s++)
            {
                if (p.Severed[s]) continue;
                if (first < 0) first = s;
                last = s;
            }
            if (first < 0 || last - first < 2)
            {
                r.Broken = true;
                r.EdgeAngleDeg = double.NaN;
                r.EdgeThickness05M = double.NaN;
                r.Overall = 0.0;
                r.Grade = "F";
                return r;
            }

            Straightness(p, first, last, spec, r);

            double twMin = double.MaxValue, twMax = double.MinValue;
            for (int s = first; s <= last; s++)
            {
                if (p.Severed[s]) continue;
                if (p.Twist[s] < twMin) twMin = p.Twist[s];
                if (p.Twist[s] > twMax) twMax = p.Twist[s];
            }
            r.TwistRangeDeg = (twMax - twMin) * 180.0 / Math.PI;
            r.TwistScore = Clamp01(1.0 - r.TwistRangeDeg / spec.MaxTwistDeg);

            // Mid-blade stations (10 %..90 %) carry the edge and heat-treatment evaluation.
            double x0 = p.X[0] + 0.1 * r.LengthM, x1 = p.X[0] + 0.9 * r.LengthM;
            int stride = Math.Max(1, count / 48);

            double symSum = 0.0;
            int symN = 0;
            double angSum = 0.0, t05Sum = 0.0;
            int angN = 0, t05N = 0;
            double hrcSum = 0.0, toughSum = 0.0;
            int midN = 0;

            for (int s = first; s <= last; s++)
            {
                if (p.Severed[s] || p.X[s] < x0 || p.X[s] > x1) continue;
                hrcSum += m.HardnessHrc(p.Martensite[s], p.TemperP[s]);
                toughSum += MaterialData.Toughness(p.Martensite[s], p.TemperP[s]);
                midN++;

                if ((s - first) % stride != 0) continue;
                SymmetryAt(p, s, ref symSum, ref symN);
                EdgeAt(p, s, ref angSum, ref angN, ref t05Sum, ref t05N);
            }

            r.SymmetryScore = symN > 0 ? Clamp01(1.0 - 2.0 * symSum / symN) : 0.0;

            r.EdgeAngleDeg = angN > 0 ? angSum / angN : double.NaN;
            r.EdgeThickness05M = t05N > 0 ? t05Sum / t05N : double.NaN;
            r.HasBevel = angN > 0 && r.EdgeAngleDeg > 1.0;
            if (r.HasBevel)
            {
                double angleScore = Clamp01(1.0 - Math.Abs(r.EdgeAngleDeg - spec.TargetEdgeAngleDeg) / spec.EdgeAngleTolDeg);
                double sharp = double.IsNaN(r.EdgeThickness05M) ? 0.0 : Clamp01(1.0 - (r.EdgeThickness05M - 0.001) / 0.002);
                r.EdgeScore = 0.6 * angleScore + 0.4 * sharp;
            }

            r.MeanHrc = midN > 0 ? hrcSum / midN : m.BaseHrc;
            r.HardnessScore = Clamp01(1.0 - Math.Abs(r.MeanHrc - spec.TargetEdgeHrc) / spec.HrcTol);
            r.ToughnessScore = midN > 0 ? toughSum / midN : 1.0;
            r.BalanceScore = Clamp01(1.0 - Math.Abs(r.BalanceFrac - spec.TargetBalanceFrac) / spec.BalanceTol);

            double overall = 100.0 * (0.15 * r.StraightnessScore + 0.10 * r.TwistScore + 0.10 * r.SymmetryScore +
                                      0.25 * r.EdgeScore + 0.20 * r.HardnessScore + 0.10 * r.ToughnessScore +
                                      0.10 * r.BalanceScore);
            if (r.CrackedStations > 0) overall = Math.Min(overall, 15.0);
            if (r.Broken) overall = 0.0;
            overall = Math.Round(overall, 6); // stable grade boundaries despite float summation order
            r.Overall = overall;
            r.Pass = !r.Broken && r.CrackedStations == 0 && overall >= spec.PassScore;
            r.Grade = overall >= 92.0 ? "S" : overall >= 85.0 ? "A" : overall >= 75.0 ? "B" : overall >= 65.0 ? "C" : overall >= 50.0 ? "D" : "F";
            return r;
        }

        private static void Straightness(BladeProfile p, int first, int last, BladeSpec spec, BladeReport r)
        {
            Vec2d a = p.ToWorld(first, p.CentroidLocal(first));
            Vec2d b = p.ToWorld(last, p.CentroidLocal(last));
            double xa = p.X[first], xb = p.X[last];
            double dev = 0.0;
            for (int s = first; s <= last; s++)
            {
                if (p.Severed[s]) continue;
                double u = (p.X[s] - xa) / (xb - xa);
                Vec2d line = new Vec2d(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
                Vec2d c = p.ToWorld(s, p.CentroidLocal(s));
                double d = (c - line).Length;
                if (d > dev) dev = d;
            }
            r.StraightnessDevM = dev;
            r.StraightnessScore = Clamp01(1.0 - dev / spec.MaxStraightDevM);
        }

        /// <summary>Asymmetry about the section mid-plane z = 0: |zTop + zBottom| / thickness at samples across the width.</summary>
        private static void SymmetryAt(BladeProfile p, int s, ref double sum, ref int n)
        {
            double yMin = double.MaxValue, yMax = double.MinValue;
            for (int j = 0; j < p.PerimeterCount; j++)
            {
                double y = p.Point(s, j).X;
                if (y < yMin) yMin = y;
                if (y > yMax) yMax = y;
            }
            double w = yMax - yMin;
            if (!(w > 1e-6)) return;
            const int samples = 9;
            for (int i = 0; i < samples; i++)
            {
                double y = yMin + w * (0.1 + 0.8 * i / (samples - 1));
                double zMin, zMax;
                if (!p.ZExtentAt(s, y, out zMin, out zMax)) continue;
                double thick = zMax - zMin;
                if (thick < 1e-6) continue;
                sum += Math.Abs(zMax + zMin) / thick;
                n++;
            }
        }

        /// <summary>Included edge angle of the +Y edge from section thickness 2 mm and 6 mm behind the tip, plus thickness at 0.5 mm.</summary>
        private static void EdgeAt(BladeProfile p, int s, ref double angSum, ref int angN, ref double t05Sum, ref int t05N)
        {
            double yTip = double.MinValue, yMin = double.MaxValue;
            for (int j = 0; j < p.PerimeterCount; j++)
            {
                double y = p.Point(s, j).X;
                if (y > yTip) yTip = y;
                if (y < yMin) yMin = y;
            }
            if (yTip - yMin < 0.008) return;

            double zMin, zMax;
            if (p.ZExtentAt(s, yTip - 0.0005, out zMin, out zMax))
            {
                t05Sum += zMax - zMin;
                t05N++;
            }

            double zMin2, zMax2, zMin6, zMax6;
            if (!p.ZExtentAt(s, yTip - 0.002, out zMin2, out zMax2)) return;
            if (!p.ZExtentAt(s, yTip - 0.006, out zMin6, out zMax6)) return;
            double t2 = zMax2 - zMin2, t6 = zMax6 - zMin6;
            double slope = (t6 - t2) * 0.5 / 0.004;
            if (slope <= 1e-6) return; // no bevel at this station
            angSum += 2.0 * Math.Atan(slope) * 180.0 / Math.PI;
            angN++;
        }

        private static double Clamp01(double v)
        {
            return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
        }
    }
}
