using System;

namespace ForgeBorn.Domain.Geometry
{
    public struct BladeHit
    {
        public double T;          // distance along ray
        public double X;          // blade-local X of the hit
        public Vec2d PointYZ;     // hit point in the cross-section plane
        public Vec2d NormalYZ;    // outward unit normal in the cross-section plane
    }

    /// <summary>Analytic ray vs. interpolated-ring solid. No MeshCollider, no allocation per call.</summary>
    public static class BladeRaycaster
    {
        private const double StepM = 0.00025;

        public static bool Raycast(BladeProfile p, Vec3d origin, Vec3d direction, double maxDistance, out BladeHit hit)
        {
            hit = default(BladeHit);
            if (!origin.IsFinite || !direction.IsFinite) return false;
            Vec3d d = direction.Normalized();
            if (d.Length < 0.5) return false;

            // Bounds
            double minY = double.MaxValue, maxY = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            for (int i = 0; i < p.Ring.Length; i += 2)
            {
                double y = p.Ring[i], z = p.Ring[i + 1];
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
            const double pad = 1e-6;
            double tEnter = 0.0, tExit = maxDistance;
            if (!Slab(origin.X, d.X, p.X[0] - pad, p.X[p.StationCount - 1] + pad, ref tEnter, ref tExit)) return false;
            if (!Slab(origin.Y, d.Y, minY - pad, maxY + pad, ref tEnter, ref tExit)) return false;
            if (!Slab(origin.Z, d.Z, minZ - pad, maxZ + pad, ref tEnter, ref tExit)) return false;

            double t = tEnter;
            if (Inside(p, origin + d * t))
            {
                hit = MakeHit(p, origin + d * t, t);
                return true;
            }

            double prev = t;
            while (prev < tExit)
            {
                t = Math.Min(prev + StepM, tExit);
                if (Inside(p, origin + d * t))
                {
                    double lo = prev, hi = t;
                    for (int it = 0; it < 30; it++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if (Inside(p, origin + d * mid)) hi = mid; else lo = mid;
                    }
                    hit = MakeHit(p, origin + d * hi, hi);
                    return true;
                }
                prev = t;
                if (t >= tExit) break;
            }
            return false;
        }

        private static bool Slab(double o, double dir, double min, double max, ref double tEnter, ref double tExit)
        {
            if (Math.Abs(dir) < 1e-12)
                return o >= min && o <= max;
            double t1 = (min - o) / dir, t2 = (max - o) / dir;
            if (t1 > t2) { double tmp = t1; t1 = t2; t2 = tmp; }
            if (t1 > tEnter) tEnter = t1;
            if (t2 < tExit) tExit = t2;
            return tEnter <= tExit;
        }

        private static int FindInterval(BladeProfile p, double x, out double u)
        {
            int lo = 0, hi = p.StationCount - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (p.X[mid] <= x) lo = mid; else hi = mid;
            }
            u = (x - p.X[lo]) / (p.X[lo + 1] - p.X[lo]);
            return lo;
        }

        private static Vec2d Interp(BladeProfile p, int k, double u, int j)
        {
            Vec2d a = p.Point(k, j), b = p.Point(k + 1, j);
            return new Vec2d(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
        }

        private static bool Inside(BladeProfile p, Vec3d q)
        {
            if (q.X < p.X[0] || q.X > p.X[p.StationCount - 1]) return false;
            double u;
            int k = FindInterval(p, q.X, out u);
            int n = p.PerimeterCount;
            bool inside = false;
            Vec2d prev = Interp(p, k, u, n - 1);
            for (int j = 0; j < n; j++)
            {
                Vec2d cur = Interp(p, k, u, j);
                if ((cur.Y > q.Z) != (prev.Y > q.Z))
                {
                    double xi = (prev.X - cur.X) * (q.Z - cur.Y) / (prev.Y - cur.Y) + cur.X;
                    if (q.Y < xi) inside = !inside;
                }
                prev = cur;
            }
            return inside;
        }

        private static BladeHit MakeHit(BladeProfile p, Vec3d q, double t)
        {
            double u;
            int k = FindInterval(p, Math.Min(Math.Max(q.X, p.X[0]), p.X[p.StationCount - 1]), out u);
            int n = p.PerimeterCount;
            Vec2d pt = new Vec2d(q.Y, q.Z);
            double best = double.MaxValue;
            Vec2d bestN = new Vec2d(0, 1);
            Vec2d prev = Interp(p, k, u, n - 1);
            for (int j = 0; j < n; j++)
            {
                Vec2d cur = Interp(p, k, u, j);
                Vec2d e = cur - prev;
                double len2 = Vec2d.Dot(e, e);
                double s = len2 > 1e-300 ? Math.Min(1.0, Math.Max(0.0, Vec2d.Dot(pt - prev, e) / len2)) : 0.0;
                Vec2d closest = prev + e * s;
                double d2 = Vec2d.Dot(pt - closest, pt - closest);
                if (d2 < best && len2 > 1e-300)
                {
                    best = d2;
                    bestN = new Vec2d(e.Y, -e.X).Normalized(); // outward for CCW ring
                }
                prev = cur;
            }
            return new BladeHit { T = t, X = q.X, PointYZ = pt, NormalYZ = bestN };
        }
    }
}
