using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Pure mapping from metallurgical state to appearance. Heat glow is an HDR emission (black-body-like ramp, gameplay-tuned,
    /// not radiometric), tempering shows oxide colours (straw to blue), damage darkens/cracks. No Domain types: plain numbers in.
    /// </summary>
    public static class BladeVisuals
    {
        private static readonly float[] GlowT = { 700f, 900f, 1100f, 1300f, 1500f, 1700f };
        private static readonly Vector3[] GlowRgb =
        {
            new Vector3(0.05f, 0.00f, 0.00f),
            new Vector3(0.60f, 0.05f, 0.00f),
            new Vector3(1.40f, 0.35f, 0.02f),
            new Vector3(2.00f, 0.90f, 0.15f),
            new Vector3(2.60f, 1.70f, 0.60f),
            new Vector3(3.00f, 2.50f, 1.40f)
        };

        // Hollomon-Jaffe P/1000 -> tint (white = bare steel).
        private static readonly float[] OxideP = { 9.8f, 10.5f, 11.5f, 12.5f, 13.5f, 15.0f };
        private static readonly Vector3[] OxideRgb =
        {
            new Vector3(1.00f, 1.00f, 1.00f),
            new Vector3(1.00f, 0.92f, 0.62f), // light straw
            new Vector3(0.85f, 0.60f, 0.35f), // brown
            new Vector3(0.75f, 0.50f, 0.80f), // purple
            new Vector3(0.45f, 0.55f, 0.95f), // blue
            new Vector3(0.80f, 0.85f, 0.90f)  // grey blue
        };

        /// <summary>HDR emission for a surface at tK kelvin. Zero below ~700 K (cold steel does not glow).</summary>
        public static Vector3 Emission(float tK)
        {
            if (!(tK > GlowT[0])) return Vector3.zero;
            if (tK >= GlowT[GlowT.Length - 1]) return GlowRgb[GlowRgb.Length - 1];
            int i = 0;
            while (tK > GlowT[i + 1]) i++;
            float u = (tK - GlowT[i]) / (GlowT[i + 1] - GlowT[i]);
            return Vector3.Lerp(GlowRgb[i], GlowRgb[i + 1], u);
        }

        /// <summary>Oxide tint multiplier (RGB 0..1) from the tempering parameter P (K); white when never tempered.</summary>
        public static Vector3 OxideTint(float temperP)
        {
            float p = temperP / 1000f;
            if (!(p > OxideP[0])) return OxideRgb[0];
            if (p >= OxideP[OxideP.Length - 1]) return OxideRgb[OxideRgb.Length - 1];
            int i = 0;
            while (p > OxideP[i + 1]) i++;
            float u = (p - OxideP[i]) / (OxideP[i + 1] - OxideP[i]);
            return Vector3.Lerp(OxideRgb[i], OxideRgb[i + 1], u);
        }

        /// <summary>Vertex colour: RGB = oxide tint (darkened when cracked), A = crack damage 0..255.</summary>
        public static Color32 Tint(float temperP, float damage)
        {
            Vector3 t = OxideTint(temperP);
            float dark = damage >= 1f ? 0.45f : 1f - 0.4f * Mathf.Clamp01(damage);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(t.x * dark) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(t.y * dark) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(t.z * dark) * 255f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(damage) * 255f));
        }
    }
}
