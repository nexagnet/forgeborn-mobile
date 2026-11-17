using System;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;

namespace ForgeBorn.Domain.Commands
{
    /// <summary>
    /// Forging blow. Each affected station is indented (cos^2 footprint) and then re-spread across the blow direction so its
    /// cross-section area is unchanged: volume is conserved. Elongation along X is not modelled (width spread only).
    /// Hot metal flows easily; cold metal barely moves and accumulates crack damage.
    /// </summary>
    public static class HammerSolver
    {
        public static HammerResult Apply(BladeProfile p, HammerStrikeCommand cmd, MaterialData m, HammerSettings st)
        {
            string err = Validate(cmd, st);
            if (err != null) return HammerResult.Rejected(err);

            var result = new HammerResult { Accepted = true, VolumeBeforeM3 = p.VolumeM3() };
            result.VolumeAfterM3 = result.VolumeBeforeM3;

            BladeHit hit;
            if (!BladeRaycaster.Raycast(p, cmd.RayOrigin, cmd.RayDirection, st.RayLengthM, out hit)) return result;
            result.Hit = true;

            HammerFace face = cmd.Face;
            double lx = face.HalfLengthM, rc = face.HalfWidthM;
            double contactArea = 4.0 * lx * rc;
            result.HitTempK = TempAt(p, hit.X);
            result.FlowStressPa = m.FlowStressPa(result.HitTempK);
            result.Cold = result.HitTempK < m.ForgeMinTempK;

            int pc = p.PerimeterCount;
            int first = int.MaxValue, last = -1;
            double maxDepth = 0.0, damageAdded = 0.0;
            Span<double> scratch = stackalloc double[pc * 2]; // one buffer per blow, reused by every station

            for (int s = 0; s < p.StationCount; s++)
            {
                double dx = Math.Abs(p.X[s] - hit.X);
                if (dx >= lx || p.Severed[s]) continue;

                double wx = Math.Cos(Math.PI * 0.5 * dx / lx);
                wx *= wx;
                if (wx <= 0.0) continue;

                Vec2d n = BladeProfile.Rotate(hit.NormalYZ, -p.Twist[s]);
                Vec2d c = p.ToLocal(s, hit.PointYZ);
                Vec2d tdir = new Vec2d(-n.Y, n.X);

                double ts = p.TempK[s];
                double sigma = m.FlowStressPa(ts);
                double depth = face.Efficiency * cmd.EnergyJ * wx / (sigma * contactArea);

                double minS = double.MaxValue, maxS = double.MinValue;
                for (int j = 0; j < pc; j++)
                {
                    double sj = Vec2d.Dot(p.Point(s, j) - c, n);
                    if (sj < minS) minS = sj;
                    if (sj > maxS) maxS = sj;
                }
                double extent = maxS - minS;
                double cap = Math.Min(st.MaxDepthFraction * extent, extent - st.MinThicknessM);
                if (depth > cap) depth = cap;
                if (depth <= 1e-9) continue;

                if (ApplyStation(p, s, n, c, tdir, depth, rc, pc, scratch))
                {
                    if (s < first) first = s;
                    if (s > last) last = s;
                    result.StationsTouched++;
                    if (depth > maxDepth) maxDepth = depth;

                    if (ts < m.ForgeMinTempK)
                    {
                        double coldFrac = (m.ForgeMinTempK - ts) / (m.ForgeMinTempK - BladeProfile.AmbientK);
                        if (coldFrac > 1.0) coldFrac = 1.0;
                        double dmg = st.ColdDamageCoeff * (depth / extent) * coldFrac;
                        p.Damage[s] += dmg;
                        damageAdded += dmg;
                    }
                }
            }

            result.FirstStation = last < 0 ? -1 : first;
            result.LastStation = last;
            result.DepthM = maxDepth;
            result.DamageAdded = damageAdded;
            result.VolumeAfterM3 = p.VolumeM3();
            return result;
        }

        private static string Validate(HammerStrikeCommand cmd, HammerSettings st)
        {
            if (cmd == null || cmd.Face == null) return "null command";
            if (!cmd.RayOrigin.IsFinite || !cmd.RayDirection.IsFinite) return "non-finite ray";
            if (cmd.RayDirection.Length < 1e-9) return "zero ray direction";
            if (double.IsNaN(cmd.EnergyJ) || !(cmd.EnergyJ > 0.0) || cmd.EnergyJ > st.MaxEnergyJ) return "invalid energy";
            HammerFace f = cmd.Face;
            if (!(f.HalfLengthM > 0.0) || !(f.HalfWidthM > 0.0) || !(f.Efficiency > 0.0) || f.Efficiency > 1.0) return "invalid hammer face";
            return null;
        }

        private static double TempAt(BladeProfile p, double x)
        {
            int n = p.StationCount;
            if (x <= p.X[0]) return p.TempK[0];
            if (x >= p.X[n - 1]) return p.TempK[n - 1];
            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (p.X[mid] <= x) lo = mid; else hi = mid;
            }
            double u = (x - p.X[lo]) / (p.X[lo + 1] - p.X[lo]);
            return p.TempK[lo] + (p.TempK[lo + 1] - p.TempK[lo]) * u;
        }

        /// <summary>Indents one station and restores its area by spreading across the blow direction. False = station left untouched.</summary>
        private static bool ApplyStation(BladeProfile p, int s, Vec2d n, Vec2d c, Vec2d tdir, double depth, double rc, int pc,
            Span<double> orig)
        {
            double aOld = p.StationArea(s);
            if (!(aOld > 1e-12)) return false;

            for (int j = 0; j < pc; j++)
            {
                Vec2d pt = p.Point(s, j);
                orig[j * 2] = pt.X;
                orig[j * 2 + 1] = pt.Y;
            }

            double scale = 1.0;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double planeS = -depth * scale;
                bool any = false;
                for (int j = 0; j < pc; j++)
                {
                    Vec2d pt = new Vec2d(orig[j * 2], orig[j * 2 + 1]);
                    Vec2d d = pt - c;
                    double sj = Vec2d.Dot(d, n);
                    double tj = Math.Abs(Vec2d.Dot(d, tdir));
                    double shift = 0.0;
                    if (sj > planeS && tj < rc)
                    {
                        double wr = Math.Cos(Math.PI * 0.5 * tj / rc);
                        wr *= wr;
                        shift = wr * (sj - planeS);
                    }
                    if (shift > 0.0) any = true;
                    p.SetPoint(s, j, pt - n * shift);
                }
                if (!any) { Restore(p, s, orig, pc); return false; }

                double aNew = p.StationArea(s);
                if (aNew > 1e-12)
                {
                    // Spread across the blow direction about the section centroid to restore the area.
                    double k = aOld / aNew;
                    Vec2d cen = p.CentroidLocal(s);
                    for (int j = 0; j < pc; j++)
                    {
                        Vec2d pt = p.Point(s, j);
                        double along = Vec2d.Dot(pt - cen, tdir);
                        p.SetPoint(s, j, pt + tdir * (along * (k - 1.0)));
                    }
                    if (!p.StationSelfIntersects(s) && p.StationArea(s) > 1e-12) return true;
                }
                scale *= 0.5;
            }

            Restore(p, s, orig, pc);
            return false;
        }

        private static void Restore(BladeProfile p, int s, Span<double> orig, int pc)
        {
            for (int j = 0; j < pc; j++) p.SetPoint(s, j, new Vec2d(orig[j * 2], orig[j * 2 + 1]));
        }
    }
}
