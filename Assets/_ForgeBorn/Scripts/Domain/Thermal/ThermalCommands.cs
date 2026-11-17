using System;

namespace ForgeBorn.Domain.Commands
{
    /// <summary>
    /// Surroundings of a heat exchange: temperature, convective coefficient by temperature band (vapor film / nucleate boiling /
    /// convection for liquids), and whether the part radiates to the surroundings. Gameplay-calibrated, not CFD.
    /// </summary>
    public sealed class ThermalEnvironment
    {
        public string Id { get; }
        public double TempK { get; }
        /// <summary>W/(m^2 K) when the part is at or above MidHiK.</summary>
        public double HHigh { get; }
        /// <summary>W/(m^2 K) when MidLoK &lt;= part &lt; MidHiK (nucleate boiling for liquids).</summary>
        public double HMid { get; }
        /// <summary>W/(m^2 K) below MidLoK.</summary>
        public double HLow { get; }
        public double MidLoK { get; }
        public double MidHiK { get; }
        public bool Radiates { get; }

        public ThermalEnvironment(string id, double tempK, double hHigh, double hMid, double hLow, double midLoK, double midHiK, bool radiates)
        {
            Id = id;
            TempK = tempK;
            HHigh = hHigh;
            HMid = hMid;
            HLow = hLow;
            MidLoK = midLoK;
            MidHiK = midHiK;
            Radiates = radiates;
        }

        /// <summary>Constant convection coefficient, optional radiation. Used by tests and simple environments.</summary>
        public static ThermalEnvironment Constant(string id, double tempK, double h, bool radiates)
        {
            return new ThermalEnvironment(id, tempK, h, h, h, 0.0, 0.0, radiates);
        }

        public static ThermalEnvironment Air() { return Constant("air", 293.15, 15.0, true); }
        public static ThermalEnvironment Furnace(double tempK) { return Constant("furnace", tempK, 60.0, true); }
        public static ThermalEnvironment Oven(double tempK) { return Constant("oven", tempK, 25.0, true); }
        public static ThermalEnvironment Oil() { return new ThermalEnvironment("oil", 323.15, 350.0, 1500.0, 250.0, 573.0, 873.0, false); }
        public static ThermalEnvironment Water() { return new ThermalEnvironment("water", 293.15, 1500.0, 9000.0, 3500.0, 573.0, 873.0, false); }
        public static ThermalEnvironment Brine() { return new ThermalEnvironment("brine", 288.15, 2500.0, 14000.0, 5500.0, 573.0, 873.0, false); }

        /// <summary>Convective + radiative coefficient (W/(m^2 K)) for a surface at tK. eps = material emissivity.</summary>
        public double H(double tK, double eps)
        {
            double h = tK >= MidHiK ? HHigh : (tK >= MidLoK ? HMid : HLow);
            if (Radiates && eps > 0.0)
            {
                const double sigma = 5.670374419e-8;
                h += eps * sigma * (tK * tK + TempK * TempK) * (tK + TempK);
            }
            return h;
        }
    }

    /// <summary>Exposes the whole blade to an environment for DurationS (heat, air cool, quench, temper all use this one command).</summary>
    public sealed class ThermalCommand
    {
        public long Id { get; }
        public ThermalEnvironment Environment { get; }
        public double DurationS { get; }

        public ThermalCommand(long id, ThermalEnvironment environment, double durationS)
        {
            Id = id;
            Environment = environment;
            DurationS = durationS;
        }
    }

    public sealed class ThermalSettings
    {
        public double MaxDurationS = 14400.0;
        public double MinDtS = 0.005;
        public double MaxDtS = 0.25;
        public int MaxSubsteps = 200000;
        /// <summary>Tempering accumulation interval (s); the Hollomon-Jaffe update is lumped to keep it cheap.</summary>
        public double TemperIntervalS = 0.5;
    }

    public sealed class ThermalResult
    {
        public bool Accepted { get; set; }
        public string Reason { get; set; }
        public int Substeps { get; set; }
        public double DtS { get; set; }
        public double MaxTempK { get; set; }
        public double MinTempK { get; set; }
        /// <summary>Change of total thermal energy sum(rho c A dx T) over the command (J).</summary>
        public double InternalEnergyChangeJ { get; set; }
        public int NewlyCrackedStations { get; set; }

        public static ThermalResult Rejected(string reason)
        {
            return new ThermalResult { Accepted = false, Reason = reason };
        }
    }
}
