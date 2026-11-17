using System;

namespace ForgeBorn.Domain.Geometry
{
    /// <summary>
    /// Canonical blade geometry (Phase A): axial stations, each with a closed ring of P perimeter samples (CCW in the Y/Z plane).
    /// Blade-local axes: +X length, Y width, Z thickness. SI meters. This is the single source of truth; meshes are projections.
    /// </summary>
    public sealed class BladeProfile
    {
        public int StationCount { get; }
        public int PerimeterCount { get; }

        /// <summary>Station positions along X, strictly increasing. Length S.</summary>
        public double[] X { get; }

        /// <summary>Flat ring storage: index ((s * P) + j) * 2 -> y, +1 -> z. Length S*P*2.</summary>
        public double[] Ring { get; }

        /// <summary>Station fully cut through (area 0). Length S.</summary>
        public bool[] Severed { get; }

        public BladeProfile(double[] x, double[] ring, bool[] severed, int perimeterCount)
        {
            if (x == null || ring == null || severed == null) throw new ArgumentNullException();
            if (perimeterCount < 4) throw new ArgumentException("perimeterCount must be >= 4");
            if (x.Length < 2) throw new ArgumentException("need >= 2 stations");
            if (ring.Length != x.Length * perimeterCount * 2) throw new ArgumentException("ring length mismatch");
            if (severed.Length != x.Length) throw new ArgumentException("severed length mismatch");
            X = x;
            Ring = ring;
            Severed = severed;
            StationCount = x.Length;
            PerimeterCount = perimeterCount;
        }

        public Vec2d Point(int s, int j)
        {
            int k = (s * PerimeterCount + j) * 2;
            return new Vec2d(Ring[k], Ring[k + 1]);
        }

        public void SetPoint(int s, int j, Vec2d v)
        {
            int k = (s * PerimeterCount + j) * 2;
            Ring[k] = v.X;
            Ring[k + 1] = v.Y;
        }

        /// <summary>Cross-section area (m^2) of station s; 0 if severed. Shoelace over the ring.</summary>
        public double StationArea(int s)
        {
            if (Severed[s]) return 0.0;
            int p = PerimeterCount;
            int b = s * p * 2;
            double sum = 0.0;
            for (int j = 0; j < p; j++)
            {
                int k0 = b + j * 2;
                int k1 = b + ((j + 1) % p) * 2;
                sum += Ring[k0] * Ring[k1 + 1] - Ring[k1] * Ring[k0 + 1];
            }
            return 0.5 * sum;
        }

        /// <summary>Volume (m^3) by trapezoidal integration of station areas along X.</summary>
        public double VolumeM3()
        {
            double v = 0.0;
            double aPrev = StationArea(0);
            for (int s = 0; s < StationCount - 1; s++)
            {
                double aNext = StationArea(s + 1);
                v += 0.5 * (aPrev + aNext) * (X[s + 1] - X[s]);
                aPrev = aNext;
            }
            return v;
        }

        /// <summary>Center of mass X (m) assuming uniform density; NaN if no volume.</summary>
        public double CenterOfMassX()
        {
            double vol = 0.0, mom = 0.0;
            double aPrev = StationArea(0);
            for (int s = 0; s < StationCount - 1; s++)
            {
                double aNext = StationArea(s + 1);
                double dx = X[s + 1] - X[s];
                double sv = 0.5 * (aPrev + aNext) * dx;
                double sum = aPrev + aNext;
                double cx = sum > 1e-300 ? X[s] + dx * (aPrev + 2.0 * aNext) / (3.0 * sum) : X[s] + 0.5 * dx;
                vol += sv;
                mom += sv * cx;
                aPrev = aNext;
            }
            return vol > 0.0 ? mom / vol : double.NaN;
        }

        public BladeProfile Clone()
        {
            return new BladeProfile((double[])X.Clone(), (double[])Ring.Clone(), (bool[])Severed.Clone(), PerimeterCount);
        }

        /// <summary>Copies all state from another profile of identical dimensions (no allocation). Used for rollback.</summary>
        public void CopyFrom(BladeProfile other)
        {
            if (other.StationCount != StationCount || other.PerimeterCount != PerimeterCount)
                throw new ArgumentException("dimension mismatch");
            Array.Copy(other.X, X, X.Length);
            Array.Copy(other.Ring, Ring, Ring.Length);
            Array.Copy(other.Severed, Severed, Severed.Length);
        }

        public bool Validate(out string reason)
        {
            return Validate(0, StationCount - 1, out reason);
        }

        /// <summary>Validates stations in [s0, s1]: finite, increasing X, positive area, simple (non self-intersecting) rings.</summary>
        public bool Validate(int s0, int s1, out string reason)
        {
            if (s0 < 0) s0 = 0;
            if (s1 > StationCount - 1) s1 = StationCount - 1;
            for (int s = s0; s <= s1; s++)
            {
                if (double.IsNaN(X[s]) || double.IsInfinity(X[s])) { reason = "X not finite at station " + s; return false; }
                if (s > 0 && !(X[s] > X[s - 1])) { reason = "X not strictly increasing at station " + s; return false; }
                for (int j = 0; j < PerimeterCount; j++)
                {
                    if (!Point(s, j).IsFinite) { reason = "non-finite point at station " + s + " vertex " + j; return false; }
                }
                if (Severed[s]) continue;
                double area = StationArea(s);
                if (!(area > 1e-12)) { reason = "non-positive area at station " + s; return false; }
                if (HasSelfIntersection(s)) { reason = "self-intersecting ring at station " + s; return false; }
            }
            reason = null;
            return true;
        }

        /// <summary>True if the ring of station s crosses itself (non-adjacent edges intersect).</summary>
        public bool StationSelfIntersects(int s)
        {
            return HasSelfIntersection(s);
        }

        private bool HasSelfIntersection(int s)
        {
            int p = PerimeterCount;
            for (int a = 0; a < p; a++)
            {
                Vec2d a0 = Point(s, a);
                Vec2d a1 = Point(s, (a + 1) % p);
                for (int b = a + 2; b < p; b++)
                {
                    if (a == 0 && b == p - 1) continue; // adjacent through wrap-around
                    Vec2d b0 = Point(s, b);
                    Vec2d b1 = Point(s, (b + 1) % p);
                    if (SegmentsCross(a0, a1, b0, b1)) return true;
                }
            }
            return false;
        }

        private static bool SegmentsCross(Vec2d a, Vec2d b, Vec2d c, Vec2d d)
        {
            double o1 = Vec2d.Cross(b - a, c - a);
            double o2 = Vec2d.Cross(b - a, d - a);
            double o3 = Vec2d.Cross(d - c, a - c);
            double o4 = Vec2d.Cross(d - c, b - c);
            return o1 * o2 < 0.0 && o3 * o4 < 0.0;
        }
    }
}
