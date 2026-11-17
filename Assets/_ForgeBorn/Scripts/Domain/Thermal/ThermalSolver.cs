using System;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;

namespace ForgeBorn.Domain.Commands
{
    /// <summary>
    /// Lumped-capacitance surface exchange per station (exponential integrator, exact for constant coefficients) plus explicit,
    /// energy-conserving axial conduction, plus phase/temper/damage bookkeeping. Geometry is never changed.
    /// Deterministic: no randomness, fixed substep sizes derived from the command.
    /// </summary>
    public static class ThermalSolver
    {
        public static ThermalResult Apply(BladeProfile p, ThermalCommand cmd, MaterialData m, ThermalSettings st)
        {
            string err = Validate(cmd, st);
            if (err != null) return ThermalResult.Rejected(err);

            int count = p.StationCount;
            double rhoC = m.VolumetricHeatCapacity;
            var area = new double[count];
            var per = new double[count];
            var dxcv = new double[count];
            double minDx2 = double.MaxValue;
            for (int s = 0; s < count; s++)
            {
                area[s] = p.StationArea(s);
                per[s] = p.StationPerimeter(s);
                double left = s > 0 ? p.X[s] - p.X[s - 1] : 0.0;
                double right = s < count - 1 ? p.X[s + 1] - p.X[s] : 0.0;
                dxcv[s] = 0.5 * (left + right);
                if (right > 0.0 && right * right < minDx2) minDx2 = right * right;
            }

            var result = new ThermalResult { Accepted = true };
            double e0 = TotalEnergy(p, area, dxcv, rhoC);
            int crackedBefore = CountCracked(p);

            if (cmd.DurationS > 0.0)
            {
                double dtStab = 0.4 * rhoC * minDx2 / m.ConductivityWPerMK;
                double dtWant = Math.Min(st.MaxDtS, Math.Max(st.MinDtS, cmd.DurationS / st.StepsPerCommand));
                double dt = Math.Min(dtWant, dtStab);
                int n = (int)Math.Ceiling(cmd.DurationS / dt);
                if (n < 1) n = 1;
                if (n > st.MaxSubsteps) return ThermalResult.Rejected("too many substeps");
                dt = cmd.DurationS / n;
                result.Substeps = n;
                result.DtS = dt;
                Run(p, cmd.Environment, m, st, area, per, dxcv, rhoC, dt, n);
            }

            double mx = double.MinValue, mn = double.MaxValue;
            for (int s = 0; s < count; s++)
            {
                if (area[s] <= 0.0) continue;
                if (p.TempK[s] > mx) mx = p.TempK[s];
                if (p.TempK[s] < mn) mn = p.TempK[s];
            }
            result.MaxTempK = mx;
            result.MinTempK = mn;
            result.InternalEnergyChangeJ = TotalEnergy(p, area, dxcv, rhoC) - e0;
            result.NewlyCrackedStations = CountCracked(p) - crackedBefore;
            return result;
        }

        /// <summary>Total thermal energy sum(rho c A_s dx_s T_s) over non-severed stations (J), relative to 0 K.</summary>
        public static double TotalEnergy(BladeProfile p, MaterialData m)
        {
            int count = p.StationCount;
            var area = new double[count];
            var dxcv = new double[count];
            for (int s = 0; s < count; s++)
            {
                area[s] = p.StationArea(s);
                double left = s > 0 ? p.X[s] - p.X[s - 1] : 0.0;
                double right = s < count - 1 ? p.X[s + 1] - p.X[s] : 0.0;
                dxcv[s] = 0.5 * (left + right);
            }
            return TotalEnergy(p, area, dxcv, m.VolumetricHeatCapacity);
        }

        private static double TotalEnergy(BladeProfile p, double[] area, double[] dxcv, double rhoC)
        {
            double e = 0.0;
            for (int s = 0; s < p.StationCount; s++)
                if (area[s] > 0.0) e += rhoC * area[s] * dxcv[s] * p.TempK[s];
            return e;
        }

        private static int CountCracked(BladeProfile p)
        {
            int c = 0;
            for (int s = 0; s < p.StationCount; s++) if (p.Damage[s] >= 1.0) c++;
            return c;
        }

        private static string Validate(ThermalCommand cmd, ThermalSettings st)
        {
            if (cmd == null || cmd.Environment == null) return "null command";
            ThermalEnvironment e = cmd.Environment;
            if (double.IsNaN(cmd.DurationS) || cmd.DurationS < 0.0 || cmd.DurationS > st.MaxDurationS) return "invalid duration";
            if (!(e.TempK >= 200.0 && e.TempK <= 2200.0)) return "invalid environment temperature";
            if (!(e.HHigh >= 0.0) || !(e.HMid >= 0.0) || !(e.HLow >= 0.0) ||
                double.IsInfinity(e.HHigh) || double.IsInfinity(e.HMid) || double.IsInfinity(e.HLow)) return "invalid heat transfer coefficient";
            return null;
        }

        private static void Run(BladeProfile p, ThermalEnvironment env, MaterialData m, ThermalSettings st,
            double[] area, double[] per, double[] dxcv, double rhoC, double dt, int steps)
        {
            int count = p.StationCount;
            double[] t = p.TempK;
            var told = new double[count];
            var dT = new double[count];
            double eps = env.Radiates ? m.Emissivity : 0.0;
            double tenv = env.TempK;
            double temperAcc = 0.0;

            for (int step = 0; step < steps; step++)
            {
                Array.Copy(t, told, count);

                // 1) Surface exchange per station.
                for (int i = 0; i < count; i++)
                {
                    if (area[i] <= 0.0) continue;
                    double h = env.H(t[i], eps);
                    double k = h * per[i] / (rhoC * area[i]);
                    t[i] = tenv + (t[i] - tenv) * Math.Exp(-k * dt);
                }

                // 2) Axial conduction (conservative pairwise flux; severed stations are insulating).
                Array.Clear(dT, 0, count);
                for (int i = 0; i < count - 1; i++)
                {
                    if (area[i] <= 0.0 || area[i + 1] <= 0.0) continue;
                    double dx = p.X[i + 1] - p.X[i];
                    double q = m.ConductivityWPerMK * 0.5 * (area[i] + area[i + 1]) * (t[i + 1] - t[i]) / dx * dt;
                    dT[i] += q / (rhoC * area[i] * dxcv[i]);
                    dT[i + 1] -= q / (rhoC * area[i + 1] * dxcv[i + 1]);
                }
                for (int i = 0; i < count; i++) t[i] += dT[i];

                // 3) Metallurgy.
                for (int i = 0; i < count; i++)
                {
                    if (area[i] <= 0.0) continue;
                    PhaseStep(p, m, i, told[i], t[i], dt);
                }

                temperAcc += dt;
                if (temperAcc >= st.TemperIntervalS || step == steps - 1)
                {
                    for (int i = 0; i < count; i++)
                        if (area[i] > 0.0) TemperStep(p, m, i, temperAcc);
                    temperAcc = 0.0;
                }
            }
        }

        private static void PhaseStep(BladeProfile p, MaterialData m, int i, double to, double tn, double dt)
        {
            // Austenitising above A1 (also erases martensite and tempering proportionally).
            if (tn > m.A1K)
            {
                double kA = 0.2 * (tn - m.A1K) / 50.0;
                double f = 1.0 - Math.Exp(-kA * dt);
                p.Austenite[i] += (1.0 - p.Austenite[i]) * f;
                p.Martensite[i] *= 1.0 - f;
                p.TemperP[i] *= 1.0 - f;
            }

            // Grain burning when overheated.
            if (tn > m.OverheatK)
            {
                double x = (tn - m.OverheatK) / 100.0;
                p.Damage[i] += 0.01 * x * x * dt;
            }

            // Nose crossing: cooling rate decides how much austenite is kept for martensite; the rest becomes soft pearlite.
            if (p.Austenite[i] > 0.0 && to >= m.NoseK && tn < m.NoseK)
            {
                double rate = (to - tn) / dt;
                double rc = m.CriticalCoolRateKPerS;
                double frac = (rate - 0.4 * rc) / (0.6 * rc);
                frac = frac < 0.0 ? 0.0 : (frac > 1.0 ? 1.0 : frac);
                p.Austenite[i] *= frac;
            }

            // Martensite start: remaining austenite transforms; fast cooling here risks cracks.
            if (p.Austenite[i] > 0.0 && tn < m.MsK)
            {
                double dm = p.Austenite[i];
                double rate = (to - tn) / dt;
                double marOld = p.Martensite[i];
                double marNew = Math.Min(1.0, marOld + dm);
                if (marNew > 0.0) p.TemperP[i] *= marOld / marNew; // fresh martensite is untempered
                p.Martensite[i] = marNew;
                p.Austenite[i] = 0.0;
                if (rate > m.CrackCoolRateKPerS)
                    p.Damage[i] += m.QuenchCrackCoeff * dm * (rate / m.CrackCoolRateKPerS - 1.0);
            }
        }

        private static void TemperStep(BladeProfile p, MaterialData m, int i, double dtAcc)
        {
            double tn = p.TempK[i];
            if (tn < 373.0 || tn >= m.A1K) return;
            double e = p.TemperP[i] / tn - 20.0;
            if (e < -30.0) e = -30.0;
            double tPrevH = Math.Pow(10.0, e);
            double tNew = tPrevH + dtAcc / 3600.0;
            double pNew = tn * (20.0 + Math.Log10(tNew));
            if (pNew > p.TemperP[i]) p.TemperP[i] = pNew;
        }
    }
}
