using System;

namespace ForgeBorn.Domain.Geometry
{
    public struct BladeHit
    {
        public double T;          // distance along ray
        public double X;          // blade-local X of the hit
        public Vec2d PointYZ;     // hit point in world Y/Z
        public Vec2d NormalYZ;    // outward unit normal in world Y/Z
    }

    public readonly struct BladeBounds
    {
        public readonly double MinY, MaxY, MinZ, MaxZ;

        public BladeBounds(double minY, double maxY, double minZ, double maxZ)
        {
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
        }
    }

    /// <summary>Analytic ray vs. interpolated-ring solid (section frames honoured). No MeshCollider, no allocation per call.</summary>
    public static class BladeRaycaster
    {
        private const double StepM = 0.00025;

        public static bool Raycast(BladeProfile p, Vec3d origin, Vec3d direction, double maxDistance, out BladeHit hit)
        {
            return Raycast(p, origin, direction, maxDistance, ComputeBounds(p), out hit);
        }

        /// <summary>
        /// World Y/Z bounds of all section rings. Valid for any later state of the same blade in which metal is only removed
        /// (grinding), so a stroke can compute it once instead of once per substep.
        /// </summary>
        public static BladeBounds ComputeBounds(BladeProfile p)
        {
            double minY = double.MaxValue, maxY = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            int pc = p.PerimeterCount;
            for (int s = 0; s < p.StationCount; s++)
            {
                if (p.Severed[s]) continue;
                int b = s * pc * 2;
                double cy = p.CenterY[s], cz = p.CenterZ[s], tw = p.Twist[s];
                if (tw == 0.0)
                {
                    for (int j = 0; j < pc; j++)
                    {
                        double y = p.Ring[b + j * 2] + cy, z = p.Ring[b + j * 2 + 1] + cz;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z;
                        if (z > maxZ) maxZ = z;
                    }
                }
                else
                {
                    double c = Math.Cos(tw), sn = Math.Sin(tw);
                    for (int j = 0; j < pc; j++)
                    {
                        double ly = p.Ring[b + j * 2], lz = p.Ring[b + j * 2 + 1];
                        double y = c * ly - sn * lz + cy, z = sn * ly + c * lz + cz;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z;
                        if (z > maxZ) maxZ = z;
                    }
                }
            }
            return new BladeBounds(minY, maxY, minZ, maxZ);
        }

        public static bool Raycast(BladeProfile p, Vec3d origin, Vec3d direction, double maxDistance, BladeBounds bounds, out BladeHit hit)
        {
            hit = default(BladeHit);
            if (!origin.IsFinite || !direction.IsFinite) return false;
            Vec3d d = direction.Normalized();
            if (d.Length < 0.5) return false;
            if (bounds.MinY > bounds.MaxY) return false; // everything severed
            double minY = bounds.MinY, maxY = bounds.MaxY, minZ = bounds.MinZ, maxZ = bounds.MaxZ;

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
            double cy, cz, tw;
            p.FrameAt(k, u, out cy, out cz, out tw);
            Vec2d ql = BladeProfile.Rotate(new Vec2d(q.Y - cy, q.Z - cz), -tw);
            int n = p.PerimeterCount;
            bool inside = false;
            Vec2d prev = Interp(p, k, u, n - 1);
            for (int j = 0; j < n; j++)
            {
                Vec2d cur = Interp(p, k, u, j);
                if ((cur.Y > ql.Y) != (prev.Y > ql.Y))
                {
                    double xi = (prev.X - cur.X) * (ql.Y - cur.Y) / (prev.Y - cur.Y) + cur.X;
                    if (ql.X < xi) inside = !inside;
                }
                prev = cur;
            }
            return inside;
        }

        private static BladeHit MakeHit(BladeProfile p, Vec3d q, double t)
        {
            double u;
            int k = FindInterval(p, Math.Min(Math.Max(q.X, p.X[0]), p.X[p.StationCount - 1]), out u);
            double cy, cz, tw;
            p.FrameAt(k, u, out cy, out cz, out tw);
            int n = p.PerimeterCount;
            Vec2d pt = BladeProfile.Rotate(new Vec2d(q.Y - cy, q.Z - cz), -tw);
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
            return new BladeHit { T = t, X = q.X, PointYZ = new Vec2d(q.Y, q.Z), NormalYZ = BladeProfile.Rotate(bestN, tw) };
        }
    }
}
