using System;

namespace ForgeBorn.Domain.Material
{
    /// <summary>
    /// Game-model material parameters (SI). Physically informed approximation calibrated for gameplay, not engineering data.
    /// Steel here behaves like a plain ~0.8 % carbon water-hardening steel.
    /// </summary>
    public sealed class MaterialData
    {
        public string Id { get; }
        public double DensityKgPerM3 { get; }

        /// <summary>Relative grinding resistance; removal depth scales with 1/GrindResistance. 1.0 = baseline.</summary>
        public double GrindResistance { get; }

        // ---- thermal ----
        public double SpecificHeatJPerKgK { get; } = 480.0;
        public double ConductivityWPerMK { get; } = 45.0;
        public double Emissivity { get; } = 0.8;

        // ---- phase transformation ----
        /// <summary>Austenite start (K). Above this the structure transforms to austenite.</summary>
        public double A1K { get; } = 1000.0;
        /// <summary>Pearlite nose (K). Cooling rate at this crossing decides how much austenite becomes martensite.</summary>
        public double NoseK { get; } = 823.0;
        /// <summary>Martensite start (K).</summary>
        public double MsK { get; } = 500.0;
        /// <summary>Cooling rate (K/s) at the nose at/above which the structure becomes fully martensitic.</summary>
        public double CriticalCoolRateKPerS { get; } = 120.0;
        /// <summary>Cooling rate (K/s) through Ms above which quench cracks start to accumulate.</summary>
        public double CrackCoolRateKPerS { get; } = 130.0;
        /// <summary>Damage per unit martensite formed per unit of (rate/CrackRate - 1).</summary>
        public double QuenchCrackCoeff { get; } = 0.5;
        /// <summary>Temperature (K) above which grain burning damage accumulates.</summary>
        public double OverheatK { get; } = 1500.0;

        // ---- hardness ----
        public double BaseHrc { get; } = 22.0;
        public double MartensiteHrcGain { get; } = 40.0;

        // ---- forging ----
        /// <summary>Below this temperature (K) hammering is cold work and accumulates crack damage.</summary>
        public double ForgeMinTempK { get; } = 1050.0;

        public MaterialData(string id, double densityKgPerM3, double grindResistance)
        {
            Id = id;
            DensityKgPerM3 = densityKgPerM3;
            GrindResistance = grindResistance;
        }

        public static readonly MaterialData SteelBasicV1 = new MaterialData("steel_basic_v1", 7850.0, 1.0);

        public static MaterialData ById(string id)
        {
            return id == SteelBasicV1.Id ? SteelBasicV1 : null;
        }

        /// <summary>Volumetric heat capacity rho*c (J/(m^3 K)).</summary>
        public double VolumetricHeatCapacity => DensityKgPerM3 * SpecificHeatJPerKgK;

        /// <summary>
        /// Hot flow stress (Pa) vs temperature, log-linear interpolation of a gameplay table
        /// (cold annealed ~350 MPa down to ~15 MPa near 1300 C).
        /// </summary>
        public double FlowStressPa(double tK)
        {
            if (tK <= FlowT[0]) return FlowS[0];
            int last = FlowT.Length - 1;
            if (tK >= FlowT[last]) return FlowS[last];
            int i = 0;
            while (tK > FlowT[i + 1]) i++;
            double u = (tK - FlowT[i]) / (FlowT[i + 1] - FlowT[i]);
            return Math.Exp(Math.Log(FlowS[i]) + (Math.Log(FlowS[i + 1]) - Math.Log(FlowS[i])) * u);
        }

        private static readonly double[] FlowT = { 293.0, 873.0, 1073.0, 1273.0, 1473.0, 1573.0 };
        private static readonly double[] FlowS = { 350e6, 150e6, 90e6, 45e6, 22e6, 15e6 };

        /// <summary>Tempering softening fraction 0..1 from the Hollomon-Jaffe parameter P = T*(20+log10 t_h) (K).</summary>
        public static double SoftFraction(double temperP)
        {
            double f = (temperP / 1000.0 - 9.0) / 9.0;
            return f < 0.0 ? 0.0 : (f > 1.0 ? 1.0 : f);
        }

        /// <summary>Rockwell C hardness of a station from its phase state.</summary>
        public double HardnessHrc(double martensite, double temperP)
        {
            double soft = SoftFraction(temperP);
            return BaseHrc + MartensiteHrcGain * martensite * (1.0 - 0.93 * soft);
        }

        /// <summary>Toughness 0..1: untempered martensite is brittle.</summary>
        public static double Toughness(double martensite, double temperP)
        {
            double soft = SoftFraction(temperP);
            return 1.0 - 0.85 * martensite * (1.0 - soft);
        }
    }
}
