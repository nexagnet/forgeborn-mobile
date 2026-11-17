using System;
using System.Collections.Generic;
using ForgeBorn.Domain.Commands;
using ForgeBorn.Domain.Evaluation;
using ForgeBorn.Domain.Geometry;
using ForgeBorn.Domain.Material;

namespace ForgeBorn.Domain.Session
{
    /// <summary>Immutable-by-convention capture of the weapon state (deep copy of geometry).</summary>
    public sealed class WeaponSnapshot
    {
        public BladeProfile Profile { get; }
        public string MaterialId { get; }
        public long Version { get; }
        public double CumulativeRemovedMassKg { get; }

        public WeaponSnapshot(BladeProfile profile, string materialId, long version, double cumulativeRemovedMassKg)
        {
            Profile = profile;
            MaterialId = materialId;
            Version = version;
            CumulativeRemovedMassKg = cumulativeRemovedMassKg;
        }
    }

    public sealed class AuditEntry
    {
        public long Version { get; set; }
        public long CommandId { get; set; }
        public string Kind { get; set; }
        public double RemovedVolumeM3 { get; set; }
        public int ContactSubsteps { get; set; }
    }

    /// <summary>
    /// Owns the canonical weapon state. Commands are validated and applied as a transaction:
    /// on any failure the geometry is restored exactly and the version/audit log are untouched.
    /// UI handlers must never mutate the profile directly.
    /// </summary>
    public sealed class ForgeSession
    {
        private readonly BladeProfile _backup;
        private readonly HashSet<long> _seenCommandIds = new HashSet<long>();
        private readonly List<AuditEntry> _audit = new List<AuditEntry>();

        public BladeProfile Profile { get; }
        public MaterialData Material { get; }
        public long Version { get; private set; }
        public double CumulativeRemovedMassKg { get; private set; }
        public GrindSolverSettings Settings { get; } = new GrindSolverSettings();
        public HammerSettings Hammer { get; } = new HammerSettings();
        public ThermalSettings Thermal { get; } = new ThermalSettings();
        public IReadOnlyList<AuditEntry> AuditLog => _audit;

        public double VolumeM3 => Profile.VolumeM3();
        public double MassKg => Profile.VolumeM3() * Material.DensityKgPerM3;

        public ForgeSession(BladeProfile profile, MaterialData material)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (material == null) throw new ArgumentNullException(nameof(material));
            string reason;
            if (!profile.Validate(out reason)) throw new ArgumentException("invalid initial geometry: " + reason);
            Profile = profile;
            Material = material;
            _backup = profile.Clone();
        }

        public static ForgeSession FromSnapshot(WeaponSnapshot snap)
        {
            MaterialData m = MaterialData.ById(snap.MaterialId);
            if (m == null) throw new ArgumentException("unknown material " + snap.MaterialId);
            var s = new ForgeSession(snap.Profile.Clone(), m);
            s.Version = snap.Version;
            s.CumulativeRemovedMassKg = snap.CumulativeRemovedMassKg;
            return s;
        }

        public WeaponSnapshot Capture()
        {
            return new WeaponSnapshot(Profile.Clone(), Material.Id, Version, CumulativeRemovedMassKg);
        }

        public GrindResult ApplyGrind(GrindStrokeCommand cmd)
        {
            if (cmd != null && cmd.Id != 0 && _seenCommandIds.Contains(cmd.Id))
                return GrindResult.Rejected("duplicate command id");

            _backup.CopyFrom(Profile);
            GrindResult r;
            try
            {
                r = GrindSolver.Apply(Profile, cmd, Material, Settings);
            }
            catch (Exception)
            {
                Profile.CopyFrom(_backup);
                throw;
            }

            if (!r.Accepted)
            {
                Profile.CopyFrom(_backup);
                return r;
            }

            string reason;
            if (r.LastStation >= 0 && !Profile.Validate(r.FirstStation - 1, r.LastStation + 1, out reason))
            {
                Profile.CopyFrom(_backup);
                return GrindResult.Rejected("geometry invalid after grind: " + reason);
            }
            if (r.VolumeAfterM3 > r.VolumeBeforeM3 + 1e-15)
            {
                Profile.CopyFrom(_backup);
                return GrindResult.Rejected("volume increased; grinding can never add metal");
            }

            if (cmd.Id != 0) _seenCommandIds.Add(cmd.Id);
            if (r.RemovedVolumeM3 > 0.0)
            {
                Version++;
                CumulativeRemovedMassKg += r.RemovedMassKg;
                _audit.Add(new AuditEntry
                {
                    Version = Version,
                    CommandId = cmd.Id,
                    Kind = "grind",
                    RemovedVolumeM3 = r.RemovedVolumeM3,
                    ContactSubsteps = r.ContactSubsteps
                });
            }
            return r;
        }

        /// <summary>One forging blow. Transactional: rolls back on any failure or if volume is not conserved.</summary>
        public HammerResult ApplyHammer(HammerStrikeCommand cmd)
        {
            if (cmd != null && cmd.Id != 0 && _seenCommandIds.Contains(cmd.Id))
                return HammerResult.Rejected("duplicate command id");

            _backup.CopyFrom(Profile);
            HammerResult r;
            try
            {
                r = HammerSolver.Apply(Profile, cmd, Material, Hammer);
            }
            catch (Exception)
            {
                Profile.CopyFrom(_backup);
                throw;
            }

            if (!r.Accepted)
            {
                Profile.CopyFrom(_backup);
                return r;
            }

            if (r.StationsTouched > 0)
            {
                string reason;
                if (!Profile.Validate(r.FirstStation - 1, r.LastStation + 1, out reason))
                {
                    Profile.CopyFrom(_backup);
                    return HammerResult.Rejected("geometry invalid after hammer: " + reason);
                }
                double drift = Math.Abs(r.VolumeAfterM3 - r.VolumeBeforeM3) / Math.Max(r.VolumeBeforeM3, 1e-300);
                if (drift > Hammer.MaxVolumeDriftRel)
                {
                    Profile.CopyFrom(_backup);
                    return HammerResult.Rejected("volume not conserved by hammer blow");
                }
            }

            if (cmd.Id != 0) _seenCommandIds.Add(cmd.Id);
            if (r.StationsTouched > 0)
            {
                Version++;
                _audit.Add(new AuditEntry { Version = Version, CommandId = cmd.Id, Kind = "hammer" });
            }
            return r;
        }

        /// <summary>Heat, cool, quench or temper the whole blade. Transactional; geometry is untouched.</summary>
        public ThermalResult ApplyThermal(ThermalCommand cmd)
        {
            if (cmd != null && cmd.Id != 0 && _seenCommandIds.Contains(cmd.Id))
                return ThermalResult.Rejected("duplicate command id");

            _backup.CopyFrom(Profile);
            ThermalResult r;
            try
            {
                r = ThermalSolver.Apply(Profile, cmd, Material, Thermal);
            }
            catch (Exception)
            {
                Profile.CopyFrom(_backup);
                throw;
            }

            if (!r.Accepted)
            {
                Profile.CopyFrom(_backup);
                return r;
            }

            string reason;
            if (!Profile.Validate(out reason))
            {
                Profile.CopyFrom(_backup);
                return ThermalResult.Rejected("state invalid after thermal step: " + reason);
            }

            if (cmd.Id != 0) _seenCommandIds.Add(cmd.Id);
            if (cmd.DurationS > 0.0)
            {
                Version++;
                _audit.Add(new AuditEntry { Version = Version, CommandId = cmd.Id, Kind = "thermal:" + cmd.Environment.Id });
            }
            return r;
        }

        public BladeReport Evaluate(BladeSpec spec)
        {
            return BladeEvaluator.Evaluate(Profile, Material, spec);
        }
    }
}
