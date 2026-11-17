using System;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;

namespace ForgeBorn.Domain.Commands
{
    public sealed class GrindSolverSettings
    {
        /// <summary>Max contact travel per substep (m) so fast swipes cannot skip or tunnel.</summary>
        public double MaxStepM = 0.0005;
        /// <summary>Max time per substep (s).</summary>
        public double MaxDtS = 0.01;
        /// <summary>A grind never thins material below this thickness unless it cuts completely through.</summary>
        public double MinThicknessM = 0.0003;
        public double MaxPressureN = 500.0;
        public int MaxSamples = 10000;
        public double MaxTotalSeconds = 10.0;
        public int MaxSubsteps = 20000;
        public double RayLengthM = 5.0;
    }

    /// <summary>
    /// Removes metal. Removal is monotonic: vertices only move into the solid, so volume never increases.
    /// The solver mutates the profile in place; ForgeSession wraps it in a transaction with rollback.
    /// </summary>
    public static class GrindSolver
    {
        public static GrindResult Apply(BladeProfile profile, GrindStrokeCommand cmd, MaterialData material, GrindSolverSettings st)
        {
            string err = ValidateCommand(cmd, st);
            if (err != null) return GrindResult.Rejected(err);

            var result = new GrindResult { Accepted = true, VolumeBeforeM3 = profile.VolumeM3() };
            int first = int.MaxValue, last = -1;
            int total = 0, contact = 0;
            double rpmFactor = cmd.WheelRpm / cmd.Wheel.RatedRpm;
            // Grinding only removes metal, so the bounds at stroke start stay a valid superset for every substep.
            BladeBounds bounds = BladeRaycaster.ComputeBounds(profile);

            var samples = cmd.Samples;
            // Dwell at the first sample.
            {
                GrindSample a = samples[0];
                int n = Math.Max(1, (int)Math.Ceiling(a.DeltaSeconds / st.MaxDtS));
                double dt = a.DeltaSeconds / n;
                for (int k = 0; k < n && a.DeltaSeconds > 0.0; k++)
                {
                    if (++total > st.MaxSubsteps) return GrindResult.Rejected("too many substeps");
                    Substep(profile, bounds, a.RayOrigin, a.RayDirection, a.PressureN, dt, cmd.Wheel, material, st, rpmFactor,
                        ref contact, ref first, ref last);
                }
            }

            for (int i = 1; i < samples.Count; i++)
            {
                GrindSample a = samples[i - 1], b = samples[i];
                double dist = (b.RayOrigin - a.RayOrigin).Length;
                int n = Math.Max(1, Math.Max((int)Math.Ceiling(dist / st.MaxStepM), (int)Math.Ceiling(b.DeltaSeconds / st.MaxDtS)));
                double dt = b.DeltaSeconds / n;
                for (int k = 1; k <= n; k++)
                {
                    if (++total > st.MaxSubsteps) return GrindResult.Rejected("too many substeps");
                    double u = (double)k / n;
                    Vec3d o = Vec3d.Lerp(a.RayOrigin, b.RayOrigin, u);
                    Vec3d d = Vec3d.Lerp(a.RayDirection, b.RayDirection, u);
                    double pr = a.PressureN + (b.PressureN - a.PressureN) * u;
                    Substep(profile, bounds, o, d, pr, dt, cmd.Wheel, material, st, rpmFactor, ref contact, ref first, ref last);
                }
            }

            result.TotalSubsteps = total;
            result.ContactSubsteps = contact;
            result.FirstStation = last < 0 ? -1 : first;
            result.LastStation = last;
            result.VolumeAfterM3 = profile.VolumeM3();
            double removed = result.VolumeBeforeM3 - result.VolumeAfterM3;
            result.RemovedVolumeM3 = removed > 0.0 ? removed : 0.0;
            result.RemovedMassKg = result.RemovedVolumeM3 * material.DensityKgPerM3;
            return result;
        }

        private static string ValidateCommand(GrindStrokeCommand cmd, GrindSolverSettings st)
        {
            if (cmd == null || cmd.Wheel == null || cmd.Samples == null) return "null command";
            if (cmd.Samples.Count == 0) return "no samples";
            if (cmd.Samples.Count > st.MaxSamples) return "too many samples";
            if (!(cmd.WheelRpm > 0.0) || double.IsInfinity(cmd.WheelRpm)) return "invalid wheel rpm";
            if (!(cmd.Wheel.RatedRpm > 0.0) || !(cmd.Wheel.FootprintHalfLengthM > 0.0) || !(cmd.Wheel.FootprintHalfWidthM > 0.0))
                return "invalid wheel";
            double totalDt = 0.0;
            for (int i = 0; i < cmd.Samples.Count; i++)
            {
                GrindSample s = cmd.Samples[i];
                if (!s.RayOrigin.IsFinite || !s.RayDirection.IsFinite) return "non-finite sample";
                if (s.RayDirection.Length < 1e-9) return "zero ray direction";
                if (double.IsNaN(s.PressureN) || s.PressureN < 0.0 || s.PressureN > st.MaxPressureN) return "invalid pressure";
                if (double.IsNaN(s.DeltaSeconds) || s.DeltaSeconds < 0.0 || double.IsInfinity(s.DeltaSeconds)) return "invalid dt";
                totalDt += s.DeltaSeconds;
            }
            if (totalDt > st.MaxTotalSeconds) return "stroke too long";
            return null;
        }

        private static void Substep(BladeProfile p, BladeBounds bounds, Vec3d origin, Vec3d dir, double pressure, double dt, AbrasiveWheel w,
            MaterialData m, GrindSolverSettings st, double rpmFactor, ref int contact, ref int first, ref int last)
        {
            if (pressure <= 0.0 || dt <= 0.0) return; // tool lifted / not pressing: nothing happens
            BladeHit hit;
            if (!BladeRaycaster.Raycast(p, origin, dir, st.RayLengthM, bounds, out hit)) return; // no contact: nothing happens
            contact++;
            double depth = w.RemovalCoeffMPerNs * pressure * dt * rpmFactor / m.GrindResistance;
            if (depth <= 0.0) return;
            ApplyKernel(p, hit, w, depth, st.MinThicknessM, ref first, ref last);
        }

        private static void ApplyKernel(BladeProfile p, BladeHit hit, AbrasiveWheel w, double depth, double minThickness,
            ref int first, ref int last)
        {
            double lx = w.FootprintHalfLengthM, rc = w.FootprintHalfWidthM;
            int pc = p.PerimeterCount;

            // Scratch buffers allocated once per call (not per station): stack use stays O(P) regardless of footprint size.
            Span<double> shifts = stackalloc double[pc];
            Span<double> orig = stackalloc double[pc * 2];
            Span<bool> moved = stackalloc bool[pc];

            for (int s = 0; s < p.StationCount; s++)
            {
                double dx = Math.Abs(p.X[s] - hit.X);
                if (dx >= lx || p.Severed[s]) continue;

                // Contact point and normal expressed in this station's local section frame.
                Vec2d n = BladeProfile.Rotate(hit.NormalYZ, -p.Twist[s]);
                Vec2d c = p.ToLocal(s, hit.PointYZ);
                Vec2d tdir = new Vec2d(-n.Y, n.X);

                double wx = Math.Cos(Math.PI * 0.5 * dx / lx);
                wx *= wx;
                double planeDepth = depth * wx;
                if (planeDepth <= 0.0) continue;

                double minS = double.MaxValue, maxT = 0.0;
                for (int j = 0; j < pc; j++)
                {
                    Vec2d d = p.Point(s, j) - c;
                    double sj = Vec2d.Dot(d, n);
                    double tj = Math.Abs(Vec2d.Dot(d, tdir));
                    if (sj < minS) minS = sj;
                    if (tj > maxT) maxT = tj;
                }

                double floorS = minS + minThickness;
                double planeS = -planeDepth;
                if (planeS <= floorS)
                {
                    if (maxT <= rc)
                    {
                        Sever(p, s);
                        Touch(s, ref first, ref last);
                        continue;
                    }
                    planeS = floorS;
                }

                // Per-vertex inward shifts; originals kept so the move can be scaled back or undone.
                bool any = false;
                moved.Clear();
                for (int j = 0; j < pc; j++)
                {
                    Vec2d pt = p.Point(s, j);
                    orig[j * 2] = pt.X;
                    orig[j * 2 + 1] = pt.Y;
                    shifts[j] = 0.0;
                    Vec2d d = pt - c;
                    double sj = Vec2d.Dot(d, n);
                    if (sj <= planeS) continue;
                    double tj = Math.Abs(Vec2d.Dot(d, tdir));
                    if (tj >= rc) continue;
                    double wr = Math.Cos(Math.PI * 0.5 * tj / rc);
                    wr *= wr;
                    double shift = wr * (sj - planeS);
                    if (shift <= 0.0) continue;
                    shifts[j] = shift;
                    moved[j] = true;
                    any = true;
                }
                if (!any) continue;

                // Oblique groove walls can make vertices cross their neighbours. Halve the move until the ring is simple;
                // shrinking a move can only remove less metal, so removal stays monotonic.
                double scale = 1.0;
                bool applied = false;
                for (int attempt = 0; attempt < 6 && !applied; attempt++)
                {
                    for (int j = 0; j < pc; j++)
                        p.SetPoint(s, j, new Vec2d(orig[j * 2], orig[j * 2 + 1]) - n * (shifts[j] * scale));
                    if (!p.StationSelfIntersectsTouching(s, moved)) applied = true;
                    else scale *= 0.5;
                }
                if (applied) Touch(s, ref first, ref last);
                else
                {
                    for (int j = 0; j < pc; j++) p.SetPoint(s, j, new Vec2d(orig[j * 2], orig[j * 2 + 1]));
                }
            }
        }

        private static void Touch(int s, ref int first, ref int last)
        {
            if (s < first) first = s;
            if (s > last) last = s;
        }

        /// <summary>Whole cross-section cut through: ring collapses to its centroid and the station carries no area.</summary>
        private static void Sever(BladeProfile p, int s)
        {
            double cy = 0.0, cz = 0.0;
            int pc = p.PerimeterCount;
            for (int j = 0; j < pc; j++)
            {
                Vec2d v = p.Point(s, j);
                cy += v.X;
                cz += v.Y;
            }
            Vec2d centroid = new Vec2d(cy / pc, cz / pc);
            for (int j = 0; j < pc; j++) p.SetPoint(s, j, centroid);
            p.Severed[s] = true;
        }
    }
}
