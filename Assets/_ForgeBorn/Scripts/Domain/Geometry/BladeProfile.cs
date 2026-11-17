using System;

namespace ForgeBorn.Domain.Geometry
{
    /// <summary>
    /// Canonical blade state: axial stations, each with a closed ring of P perimeter samples (CCW in the section's local Y/Z plane),
    /// a section frame (centerline offset + twist about X) and per-station metallurgical state.
    /// Blade axes: +X length, Y width, Z thickness. SI meters, radians, kelvin. This is the single source of truth; meshes are projections.
    /// World (Y,Z) of a section point = Rotate(local, Twist[s]) + (CenterY[s], CenterZ[s]).
    /// Cross-sections stay perpendicular to X, so bending and twisting never change volume (a deliberate model simplification).
    /// </summary>
    public sealed class BladeProfile
    {
        public const double AmbientK = 293.15;

        public int StationCount { get; }
        public int PerimeterCount { get; }

        /// <summary>Station positions along X, strictly increasing. Length S.</summary>
        public double[] X { get; }

        /// <summary>Flat ring storage (section-local): index ((s * P) + j) * 2 -> y, +1 -> z. Length S*P*2.</summary>
        public double[] Ring { get; }

        /// <summary>Station fully cut through (area 0). Length S.</summary>
        public bool[] Severed { get; }

        /// <summary>Centerline offset of each section in world Y (m). Length S.</summary>
        public double[] CenterY { get; }
        /// <summary>Centerline offset of each section in world Z (m). Length S.</summary>
        public double[] CenterZ { get; }
        /// <summary>Rotation of each section about X (rad, CCW in Y/Z). Length S.</summary>
        public double[] Twist { get; }

        /// <summary>Temperature per station (K).</summary>
        public double[] TempK { get; }
        /// <summary>Austenite fraction 0..1 (transient, above A1 or waiting for martensite start).</summary>
        public double[] Austenite { get; }
        /// <summary>Martensite fraction 0..1 produced by quenching.</summary>
        public double[] Martensite { get; }
        /// <summary>Hollomon-Jaffe tempering parameter T*(20+log10 t_hours), K. 0 = never tempered.</summary>
        public double[] TemperP { get; }
        /// <summary>Accumulated crack damage 0..; &gt;= 1 means cracked.</summary>
        public double[] Damage { get; }

        public BladeProfile(double[] x, double[] ring, bool[] severed, int perimeterCount)
            : this(x, ring, severed, perimeterCount, null, null, null, null, null, null, null, null)
        {
        }

        public BladeProfile(double[] x, double[] ring, bool[] severed, int perimeterCount,
            double[] centerY, double[] centerZ, double[] twist,
            double[] tempK, double[] austenite, double[] martensite, double[] temperP, double[] damage)
        {
            if (x == null || ring == null || severed == null) throw new ArgumentNullException();
            if (perimeterCount < 4) throw new ArgumentException("perimeterCount must be >= 4");
            if (x.Length < 2) throw new ArgumentException("need >= 2 stations");
            if (ring.Length != x.Length * perimeterCount * 2) throw new ArgumentException("ring length mismatch");
            if (severed.Length != x.Length) throw new ArgumentException("severed length mismatch");
            int s = x.Length;
            X = x;
            Ring = ring;
            Severed = severed;
            StationCount = s;
            PerimeterCount = perimeterCount;
            CenterY = Take(centerY, s, 0.0);
            CenterZ = Take(centerZ, s, 0.0);
            Twist = Take(twist, s, 0.0);
            TempK = Take(tempK, s, AmbientK);
            Austenite = Take(austenite, s, 0.0);
            Martensite = Take(martensite, s, 0.0);
            TemperP = Take(temperP, s, 0.0);
            Damage = Take(damage, s, 0.0);
        }

        private static double[] Take(double[] a, int n, double fill)
        {
            if (a != null)
            {
                if (a.Length != n) throw new ArgumentException("per-station array length mismatch");
                return a;
            }
            var r = new double[n];
            if (fill != 0.0) for (int i = 0; i < n; i++) r[i] = fill;
            return r;
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

        public static Vec2d Rotate(Vec2d v, double angle)
        {
            if (angle == 0.0) return v;
            double c = Math.Cos(angle), s = Math.Sin(angle);
            return new Vec2d(c * v.X - s * v.Y, s * v.X + c * v.Y);
        }

        /// <summary>Section-local point of station s to world (Y,Z).</summary>
        public Vec2d ToWorld(int s, Vec2d local)
        {
            Vec2d r = Rotate(local, Twist[s]);
            return new Vec2d(r.X + CenterY[s], r.Y + CenterZ[s]);
        }

        /// <summary>World (Y,Z) to the section-local frame of station s.</summary>
        public Vec2d ToLocal(int s, Vec2d world)
        {
            return Rotate(new Vec2d(world.X - CenterY[s], world.Y - CenterZ[s]), -Twist[s]);
        }

        /// <summary>World position of ring vertex j of station s.</summary>
        public Vec2d WorldPoint(int s, int j)
        {
            return ToWorld(s, Point(s, j));
        }

        /// <summary>Linearly interpolated section frame between stations k and k+1.</summary>
        public void FrameAt(int k, double u, out double cy, out double cz, out double twist)
        {
            cy = CenterY[k] + (CenterY[k + 1] - CenterY[k]) * u;
            cz = CenterZ[k] + (CenterZ[k + 1] - CenterZ[k]) * u;
            twist = Twist[k] + (Twist[k + 1] - Twist[k]) * u;
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

        /// <summary>Ring perimeter length (m) of station s; 0 if severed.</summary>
        public double StationPerimeter(int s)
        {
            if (Severed[s]) return 0.0;
            int p = PerimeterCount;
            double len = 0.0;
            for (int j = 0; j < p; j++) len += (Point(s, (j + 1) % p) - Point(s, j)).Length;
            return len;
        }

        /// <summary>Area centroid of station s in the section-local frame.</summary>
        public Vec2d CentroidLocal(int s)
        {
            int p = PerimeterCount;
            double a2 = 0.0, cx = 0.0, cy = 0.0;
            for (int j = 0; j < p; j++)
            {
                Vec2d a = Point(s, j), b = Point(s, (j + 1) % p);
                double cr = a.X * b.Y - b.X * a.Y;
                a2 += cr;
                cx += (a.X + b.X) * cr;
                cy += (a.Y + b.Y) * cr;
            }
            if (Math.Abs(a2) < 1e-300)
            {
                double mx = 0.0, my = 0.0;
                for (int j = 0; j < p; j++) { Vec2d v = Point(s, j); mx += v.X; my += v.Y; }
                return new Vec2d(mx / p, my / p);
            }
            return new Vec2d(cx / (3.0 * a2), cy / (3.0 * a2));
        }

        /// <summary>
        /// Section-local z extent of station s along the line y = yLocal (min and max z of ring crossings).
        /// False if the line misses the section.
        /// </summary>
        public bool ZExtentAt(int s, double yLocal, out double zMin, out double zMax)
        {
            zMin = double.MaxValue;
            zMax = double.MinValue;
            int p = PerimeterCount;
            bool any = false;
            for (int j = 0; j < p; j++)
            {
                Vec2d a = Point(s, j), b = Point(s, (j + 1) % p);
                double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
                if (yLocal < lo || yLocal > hi) continue;
                if (hi - lo < 1e-15)
                {
                    // Edge parallel to the line: both endpoints count.
                    if (a.Y < zMin) zMin = a.Y;
                    if (a.Y > zMax) zMax = a.Y;
                    if (b.Y < zMin) zMin = b.Y;
                    if (b.Y > zMax) zMax = b.Y;
                    any = true;
                    continue;
                }
                double t = (yLocal - a.X) / (b.X - a.X);
                double z = a.Y + (b.Y - a.Y) * t;
                if (z < zMin) zMin = z;
                if (z > zMax) zMax = z;
                any = true;
            }
            return any;
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
            return new BladeProfile((double[])X.Clone(), (double[])Ring.Clone(), (bool[])Severed.Clone(), PerimeterCount,
                (double[])CenterY.Clone(), (double[])CenterZ.Clone(), (double[])Twist.Clone(),
                (double[])TempK.Clone(), (double[])Austenite.Clone(), (double[])Martensite.Clone(),
                (double[])TemperP.Clone(), (double[])Damage.Clone());
        }

        /// <summary>Copies all state from another profile of identical dimensions (no allocation). Used for rollback.</summary>
        public void CopyFrom(BladeProfile other)
        {
            if (other.StationCount != StationCount || other.PerimeterCount != PerimeterCount)
                throw new ArgumentException("dimension mismatch");
            Array.Copy(other.X, X, X.Length);
            Array.Copy(other.Ring, Ring, Ring.Length);
            Array.Copy(other.Severed, Severed, Severed.Length);
            Array.Copy(other.CenterY, CenterY, CenterY.Length);
            Array.Copy(other.CenterZ, CenterZ, CenterZ.Length);
            Array.Copy(other.Twist, Twist, Twist.Length);
            Array.Copy(other.TempK, TempK, TempK.Length);
            Array.Copy(other.Austenite, Austenite, Austenite.Length);
            Array.Copy(other.Martensite, Martensite, Martensite.Length);
            Array.Copy(other.TemperP, TemperP, TemperP.Length);
            Array.Copy(other.Damage, Damage, Damage.Length);
        }

        public bool Validate(out string reason)
        {
            return Validate(0, StationCount - 1, out reason);
        }

        /// <summary>Validates stations in [s0, s1]: finite, increasing X, positive area, simple (non self-intersecting) rings, sane state.</summary>
        public bool Validate(int s0, int s1, out string reason)
        {
            if (s0 < 0) s0 = 0;
            if (s1 > StationCount - 1) s1 = StationCount - 1;
            for (int s = s0; s <= s1; s++)
            {
                if (double.IsNaN(X[s]) || double.IsInfinity(X[s])) { reason = "X not finite at station " + s; return false; }
                if (s > 0 && !(X[s] > X[s - 1])) { reason = "X not strictly increasing at station " + s; return false; }
                if (!IsFinite(CenterY[s]) || !IsFinite(CenterZ[s]) || !IsFinite(Twist[s])) { reason = "frame not finite at station " + s; return false; }
                if (!IsFinite(TempK[s]) || TempK[s] <= 0.0 || TempK[s] > 4000.0) { reason = "temperature out of range at station " + s; return false; }
                if (!InUnit(Austenite[s]) || !InUnit(Martensite[s])) { reason = "phase fraction out of range at station " + s; return false; }
                if (!IsFinite(TemperP[s]) || TemperP[s] < 0.0 || !IsFinite(Damage[s]) || Damage[s] < 0.0) { reason = "state out of range at station " + s; return false; }
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

        private static bool IsFinite(double v) { return !(double.IsNaN(v) || double.IsInfinity(v)); }
        private static bool InUnit(double v) { return v >= -1e-9 && v <= 1.0 + 1e-9; }

        /// <summary>True if the ring of station s crosses itself (non-adjacent edges intersect).</summary>
        public bool StationSelfIntersects(int s)
        {
            return HasSelfIntersection(s);
        }

        /// <summary>
        /// Like <see cref="StationSelfIntersects"/> but only tests edges that touch a moved vertex against all others.
        /// Exact when the ring was simple before the vertices in <paramref name="moved"/> were moved.
        /// </summary>
        public bool StationSelfIntersectsTouching(int s, ReadOnlySpan<bool> moved)
        {
            int p = PerimeterCount;
            for (int a = 0; a < p; a++)
            {
                int a1 = (a + 1) % p;
                bool touchA = moved[a] || moved[a1];
                Vec2d a0 = Point(s, a), a1p = Point(s, a1);
                for (int b = a + 2; b < p; b++)
                {
                    if (a == 0 && b == p - 1) continue;
                    int b1 = (b + 1) % p;
                    if (!touchA && !moved[b] && !moved[b1]) continue;
                    if (SegmentsCross(a0, a1p, Point(s, b), Point(s, b1))) return true;
                }
            }
            return false;
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
