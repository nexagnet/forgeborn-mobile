using System.Globalization;
using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Player-facing settings (GAME_DESIGN §13 "Cài đặt"): resolution, fullscreen, quality tier, volume. Stored as one
    /// PlayerPrefs string ("v1;res=1;fs=0;tier=-1;vol=1.00"). Parse/Serialize are pure so they can be tested without a device.
    /// Quality tier and resolution take effect on the next launch for tier (the profile is cached at boot) and immediately for the rest.
    /// </summary>
    public sealed class GameSettings
    {
        public const string PrefsKey = "fb.settings";

        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
        };

        public int ResolutionIndex;
        public bool Fullscreen;
        /// <summary>-1 = automatic (platform default), otherwise a QualityTier value.</summary>
        public int Tier = -1;
        public float Volume = 1f;

        public static string TierName(int tier) { return tier < 0 ? "Auto" : ((QualityTier)tier).ToString(); }

        public void Clamp()
        {
            ResolutionIndex = Mathf.Clamp(ResolutionIndex, 0, Resolutions.Length - 1);
            Tier = Mathf.Clamp(Tier, -1, 2);
            Volume = Mathf.Clamp01(Volume);
        }

        public string Serialize()
        {
            return "v1;res=" + ResolutionIndex + ";fs=" + (Fullscreen ? 1 : 0) + ";tier=" + Tier
                + ";vol=" + Volume.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Never throws: anything unreadable falls back to the default for that field.</summary>
        public static GameSettings Parse(string text)
        {
            var s = new GameSettings();
            if (!string.IsNullOrEmpty(text) && text.StartsWith("v1;", System.StringComparison.Ordinal))
            {
                foreach (string part in text.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = part.Substring(0, eq), val = part.Substring(eq + 1);
                    int i;
                    float f;
                    switch (key)
                    {
                        case "res": if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) s.ResolutionIndex = i; break;
                        case "fs": s.Fullscreen = val == "1"; break;
                        case "tier": if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) s.Tier = i; break;
                        case "vol": if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) s.Volume = f; break;
                    }
                }
            }
            s.Clamp();
            return s;
        }

        public static GameSettings Load() { return Parse(PlayerPrefs.GetString(PrefsKey, "")); }

        public void Save()
        {
            Clamp();
            PlayerPrefs.SetString(PrefsKey, Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>Applies resolution, fullscreen and volume now. (Tier is read when the platform profile is first built.)</summary>
        public void Apply()
        {
            Clamp();
            Vector2Int r = Resolutions[ResolutionIndex];
            Screen.SetResolution(r.x, r.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            AudioListener.volume = Volume;
        }
    }
}
