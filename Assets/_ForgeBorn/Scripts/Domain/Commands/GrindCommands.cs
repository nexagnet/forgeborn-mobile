using System.Collections.Generic;
using ForgeBorn.Domain.Geometry;

namespace ForgeBorn.Domain.Commands
{
    /// <summary>Abrasive wheel data. Coefficients are calibrated gameplay values, not an industrial grinding equation.</summary>
    public sealed class AbrasiveWheel
    {
        public string Id { get; }

        /// <summary>Half length of the contact patch along the blade axis (m).</summary>
        public double FootprintHalfLengthM { get; }

        /// <summary>Half width of the contact patch across the surface (m).</summary>
        public double FootprintHalfWidthM { get; }

        /// <summary>Removal depth per (newton * second) at rated rpm, for baseline material (m / (N*s)).</summary>
        public double RemovalCoeffMPerNs { get; }

        public double RatedRpm { get; }

        public AbrasiveWheel(string id, double halfLengthM, double halfWidthM, double removalCoeffMPerNs, double ratedRpm)
        {
            Id = id;
            FootprintHalfLengthM = halfLengthM;
            FootprintHalfWidthM = halfWidthM;
            RemovalCoeffMPerNs = removalCoeffMPerNs;
            RatedRpm = ratedRpm;
        }

        public static readonly AbrasiveWheel Coarse = new AbrasiveWheel("wheel_coarse_v1", 0.010, 0.010, 2e-4, 3000.0);
        public static readonly AbrasiveWheel Medium = new AbrasiveWheel("wheel_medium_v1", 0.008, 0.008, 8e-5, 3000.0);
        public static readonly AbrasiveWheel Fine = new AbrasiveWheel("wheel_fine_v1", 0.006, 0.006, 2e-5, 3000.0);
    }

    /// <summary>
    /// One touch sample already converted to blade-local world space (meters), never screen pixels.
    /// DeltaSeconds is the time since the previous sample (for the first sample: dwell time at that spot).
    /// </summary>
    public readonly struct GrindSample
    {
        public readonly Vec3d RayOrigin;
        public readonly Vec3d RayDirection;
        public readonly double PressureN;
        public readonly double DeltaSeconds;

        public GrindSample(Vec3d rayOrigin, Vec3d rayDirection, double pressureN, double deltaSeconds)
        {
            RayOrigin = rayOrigin;
            RayDirection = rayDirection;
            PressureN = pressureN;
            DeltaSeconds = deltaSeconds;
        }
    }

    /// <summary>A finger stroke from touch-down to touch-up. When the stroke ends, no further grinding happens.</summary>
    public sealed class GrindStrokeCommand
    {
        /// <summary>Unique id (non-zero) for duplicate detection; 0 disables the check.</summary>
        public long Id { get; }
        public double TimestampS { get; }
        public AbrasiveWheel Wheel { get; }
        public double WheelRpm { get; }
        public IReadOnlyList<GrindSample> Samples { get; }

        public GrindStrokeCommand(long id, double timestampS, AbrasiveWheel wheel, double wheelRpm, IReadOnlyList<GrindSample> samples)
        {
            Id = id;
            TimestampS = timestampS;
            Wheel = wheel;
            WheelRpm = wheelRpm;
            Samples = samples;
        }
    }

    public sealed class GrindResult
    {
        public bool Accepted { get; set; }
        public string Reason { get; set; }
        public int TotalSubsteps { get; set; }
        public int ContactSubsteps { get; set; }
        public double VolumeBeforeM3 { get; set; }
        public double VolumeAfterM3 { get; set; }
        public double RemovedVolumeM3 { get; set; }
        public double RemovedMassKg { get; set; }
        public int FirstStation { get; set; } = -1;
        public int LastStation { get; set; } = -1;

        public static GrindResult Rejected(string reason)
        {
            return new GrindResult { Accepted = false, Reason = reason };
        }
    }
}
