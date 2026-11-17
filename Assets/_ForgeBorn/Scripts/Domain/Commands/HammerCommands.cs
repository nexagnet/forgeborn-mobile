using ForgeBorn.Domain.Geometry;

namespace ForgeBorn.Domain.Commands
{
    /// <summary>Hammer face. Efficiency = fraction of blow energy that becomes plastic work. Gameplay-calibrated.</summary>
    public sealed class HammerFace
    {
        public string Id { get; }
        public double HalfLengthM { get; }
        public double HalfWidthM { get; }
        public double Efficiency { get; }

        public HammerFace(string id, double halfLengthM, double halfWidthM, double efficiency)
        {
            Id = id;
            HalfLengthM = halfLengthM;
            HalfWidthM = halfWidthM;
            Efficiency = efficiency;
        }

        public static readonly HammerFace Flat = new HammerFace("hammer_flat_v1", 0.015, 0.015, 0.5);
        public static readonly HammerFace Peen = new HammerFace("hammer_peen_v1", 0.006, 0.020, 0.5);
    }

    /// <summary>One hammer blow along a ray (blade-local world meters). Volume is conserved; metal flows sideways.</summary>
    public sealed class HammerStrikeCommand
    {
        public long Id { get; }
        public double TimestampS { get; }
        public Vec3d RayOrigin { get; }
        public Vec3d RayDirection { get; }
        public double EnergyJ { get; }
        public HammerFace Face { get; }

        public HammerStrikeCommand(long id, double timestampS, Vec3d rayOrigin, Vec3d rayDirection, double energyJ, HammerFace face)
        {
            Id = id;
            TimestampS = timestampS;
            RayOrigin = rayOrigin;
            RayDirection = rayDirection;
            EnergyJ = energyJ;
            Face = face;
        }
    }

    public sealed class HammerSettings
    {
        public double MaxEnergyJ = 500.0;
        /// <summary>A single blow never reduces a section's thickness (along the blow) by more than this fraction.</summary>
        public double MaxDepthFraction = 0.30;
        public double MinThicknessM = 0.0003;
        /// <summary>Crack damage per unit (depth/thickness) at the coldest (ambient) temperature.</summary>
        public double ColdDamageCoeff = 3.0;
        public double RayLengthM = 5.0;
        /// <summary>Largest accepted relative volume change of one blow.</summary>
        public double MaxVolumeDriftRel = 1e-6;
    }

    public sealed class HammerResult
    {
        public bool Accepted { get; set; }
        public string Reason { get; set; }
        public bool Hit { get; set; }
        public double VolumeBeforeM3 { get; set; }
        public double VolumeAfterM3 { get; set; }
        public double HitTempK { get; set; }
        public double FlowStressPa { get; set; }
        /// <summary>Deepest indentation actually applied (m).</summary>
        public double DepthM { get; set; }
        public bool Cold { get; set; }
        public double DamageAdded { get; set; }
        public int StationsTouched { get; set; }
        public int FirstStation { get; set; } = -1;
        public int LastStation { get; set; } = -1;

        public static HammerResult Rejected(string reason)
        {
            return new HammerResult { Accepted = false, Reason = reason };
        }
    }
}
