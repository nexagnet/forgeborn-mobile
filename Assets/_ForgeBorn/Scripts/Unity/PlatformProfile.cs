using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    public enum QualityTier { Low, Medium, High }

    /// <summary>
    /// Per-platform defaults behind one object so the rest of the code has no platform #if. Chosen at runtime from the platform and RAM.
    /// STATUS: these are configured defaults, NOT measured on any device. iOS and Android differ only where the platforms differ
    /// (haptics, tiering, refresh target); nothing here is tuned from device measurements yet.
    /// </summary>
    public sealed class PlatformProfile
    {
        public string Name { get; }
        public QualityTier Tier { get; }
        public int TargetFps { get; }
        public int MaxSparkParticles { get; }
        public bool BrushedSteel { get; }
        public bool UseHandheldHaptics { get; }
        /// <summary>True when the profile only simulates a device (Editor / Unity Remote): behaviour yes, performance no.</summary>
        public bool Simulated { get; }

        public PlatformProfile(string name, QualityTier tier, int targetFps, int maxSpark, bool brushed, bool haptics, bool simulated)
        {
            Name = name;
            Tier = tier;
            TargetFps = targetFps;
            MaxSparkParticles = maxSpark;
            BrushedSteel = brushed;
            UseHandheldHaptics = haptics;
            Simulated = simulated;
        }

        private static PlatformProfile _current;

        public static PlatformProfile Current
        {
            get { return _current ?? (_current = ForPlatform(Application.platform, SystemInfo.systemMemorySize)); }
        }

        /// <summary>Pure function (EditMode-testable). systemMemoryMb is SystemInfo.systemMemorySize.</summary>
        public static PlatformProfile ForPlatform(RuntimePlatform platform, int systemMemoryMb)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    // 60 Hz default; 120 Hz ProMotion is an opt-in after thermal measurement on a real iPhone.
                    return new PlatformProfile("ios", systemMemoryMb >= 4000 ? QualityTier.High : QualityTier.Medium, 60,
                        systemMemoryMb >= 4000 ? 300 : 150, true, true, false);
                case RuntimePlatform.Android:
                {
                    QualityTier t = systemMemoryMb >= 6000 ? QualityTier.High : (systemMemoryMb >= 3000 ? QualityTier.Medium : QualityTier.Low);
                    int sparks = t == QualityTier.High ? 300 : (t == QualityTier.Medium ? 150 : 60);
                    return new PlatformProfile("android", t, 60, sparks, t != QualityTier.Low, true, false);
                }
                case RuntimePlatform.WindowsPlayer:
                    // The shipping PC target. High is what BENCHMARKS.md measured on an Intel UHD iGPU (1280x720); a Low tier is P4 work.
                    return new PlatformProfile("windows", QualityTier.High, 60, 300, true, false, false);
                default:
                    // Editor and Unity Remote run on the PC: simulate an iPhone-class profile, flagged as simulated.
                    return new PlatformProfile("editor-sim-ios", QualityTier.High, 60, 300, true, false, true);
            }
        }
    }
}
