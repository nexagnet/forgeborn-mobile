using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>
    /// Pure rules for quench vapour: how hard the blade steams for a given temperature and bath. Unit tested; no scene needed.
    /// Visual only: it never feeds back into the thermal model (that stays in ForgeBorn.Domain).
    /// </summary>
    public static class SteamModel
    {
        /// <summary>Below this the liquid film on the steel stops boiling off in visible quantity (K).</summary>
        public const double ColdK = 400.0;
        /// <summary>At and above this the blade steams at the full rate (K).</summary>
        public const double FullK = 1100.0;
        public const float WaterPuffsPerSecond = 70f;
        public const float OilPuffsPerSecond = 28f;

        /// <summary>Puffs per second for the hottest station temperature in the given bath. Zero outside liquid baths.</summary>
        public static float PuffsPerSecond(double stationTempK, ForgeEnv env)
        {
            float max;
            switch (env)
            {
                case ForgeEnv.Water: max = WaterPuffsPerSecond; break;
                case ForgeEnv.Oil: max = OilPuffsPerSecond; break;
                default: return 0f;
            }
            if (stationTempK <= ColdK) return 0f;
            double t = (stationTempK - ColdK) / (FullK - ColdK);
            if (t > 1.0) t = 1.0;
            return max * (float)t;
        }

        /// <summary>Vapour colour: white-grey for water, dark brown smoke for oil.</summary>
        public static Color Tint(ForgeEnv env)
        {
            return env == ForgeEnv.Oil ? new Color(0.30f, 0.26f, 0.22f) : new Color(0.88f, 0.9f, 0.93f);
        }
    }

    /// <summary>World-space steam puffs rising off a quenched blade. Particle cap comes from the platform profile.</summary>
    public sealed class SteamEffect : MonoBehaviour
    {
        private ParticleSystem _ps;
        private float _carry;

        public void Setup(Material material, int maxParticles)
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.gravityModifier = -0.05f; // slight buoyancy on top of the initial velocity
            main.startLifetime = 1.4f;
            main.startSize = 0.06f;
            var emission = _ps.emission;
            emission.enabled = false;
            var shape = _ps.shape;
            shape.enabled = false;

            var size = _ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.35f), new Keyframe(0.35f, 0.8f), new Keyframe(1f, 1.6f)));

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.8f;

            var rend = _ps.GetComponent<ParticleSystemRenderer>();
            if (material != null) rend.sharedMaterial = material;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            _ps.Play();
        }

        /// <summary>Advances the particle simulation by hand (headless capture runs outside Play mode).</summary>
        public void SimulateForCapture(float seconds)
        {
            if (_ps != null) _ps.Simulate(seconds, true, false, true);
        }

        /// <summary>Removes all live puffs at once.</summary>
        public void Clear()
        {
            if (_ps != null) _ps.Clear(true);
        }

        /// <summary>
        /// Accumulate puffs for dt seconds at the given rate and emit them at random ones of the given world points.
        /// Fractions carry over between frames so low rates still emit.
        /// </summary>
        public void Emit(Vector3[] points, int pointCount, float puffsPerSecond, float dt, Color tint)
        {
            if (_ps == null || pointCount <= 0 || puffsPerSecond <= 0f) return;
            _carry += puffsPerSecond * dt;
            int n = Mathf.FloorToInt(_carry);
            if (n <= 0) return;
            _carry -= n;
            var p = new ParticleSystem.EmitParams { startColor = tint };
            for (int i = 0; i < n; i++)
            {
                Vector3 at = points[Random.Range(0, pointCount)];
                p.position = at + new Vector3(Random.Range(-0.02f, 0.02f), 0f, Random.Range(-0.05f, 0f));
                p.velocity = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0.12f, 0.3f), 0f);
                p.startLifetime = Random.Range(0.9f, 1.7f);
                p.startSize = Random.Range(0.05f, 0.1f);
                _ps.Emit(p, 1);
            }
        }
    }
}
