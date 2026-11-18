using UnityEngine;

namespace ForgeBorn.UnityLayer
{
    /// <summary>Haptics behind one interface so the game code never touches platform APIs.</summary>
    public interface IHapticsService
    {
        /// <summary>strength01 in 0..1.</summary>
        void Impact(float strength01);
    }

    public sealed class NullHaptics : IHapticsService
    {
        public void Impact(float strength01) { }
    }

    /// <summary>
    /// Basic mobile vibration via Handheld.Vibrate (fixed strength, rate-limited). This is NOT CoreHaptics/Android amplitude control;
    /// it is a placeholder that only does something on a real device build, so it is unverified until then.
    /// </summary>
    public sealed class HandheldHaptics : IHapticsService
    {
        private float _last = -10f;

        public void Impact(float strength01)
        {
            if (strength01 < 0.3f) return;
            if (Time.unscaledTime - _last < 0.25f) return;
            _last = Time.unscaledTime;
            Handheld.Vibrate();
        }
    }

    /// <summary>
    /// Procedural grinding noise and hammer impacts (no audio assets). Runs on the audio thread via OnAudioFilterRead.
    /// Under Unity Remote the sound plays on the PC, not on the phone.
    /// </summary>
    public sealed class SandboxAudio : MonoBehaviour
    {
        private volatile float _grindGain;
        private volatile float _grindTone = 0.2f;
        private volatile float _impactEnv;
        private volatile float _impactFreq = 220f;
        private float _gainSmooth, _lp;
        private double _impactPhase;
        private uint _rng = 2463534242u;
        private double _sampleRate = 48000.0;

        private AudioSource _oneShot;
        private AudioClip _hammerHit;

        public void Setup()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            // Real recording (CC0, see docs/CREDITS.md) layered over the synthesized clank; absent file falls back to synthesis only.
            _hammerHit = Resources.Load<AudioClip>("Audio/hammer_hit");
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.spatialBlend = 0f;
            _oneShot.playOnAwake = false;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = AudioClip.Create("fb_silence", 4800, 1, 48000, false);
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
        }

        /// <summary>gain01: 0 = silent; tone01: 0 = dull, 1 = bright (grinding harder and faster is brighter).</summary>
        public void SetGrind(float gain01, float tone01)
        {
            _grindGain = Mathf.Clamp01(gain01);
            _grindTone = Mathf.Lerp(0.05f, 0.55f, Mathf.Clamp01(tone01));
        }

        /// <summary>Hammer clank. hot01 lowers the pitch a little (soft metal).</summary>
        public void Impact(float energy01, float hot01)
        {
            _impactFreq = Mathf.Lerp(420f, 180f, Mathf.Clamp01(hot01)) * Mathf.Lerp(1.15f, 0.9f, Mathf.Clamp01(energy01));
            _impactEnv = Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(energy01));
            if (_hammerHit != null && _oneShot != null)
            {
                // Hot, soft metal rings lower; harder blows are louder.
                _oneShot.pitch = Mathf.Lerp(1.08f, 0.86f, Mathf.Clamp01(hot01)) * Random.Range(0.97f, 1.03f);
                _oneShot.PlayOneShot(_hammerHit, Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(energy01)));
            }
        }

        private float Noise()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / 8388608f - 1f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Render(data, channels);
        }

        /// <summary>Fills an interleaved buffer (also called directly by tests, no audio device needed).</summary>
        public void Render(float[] data, int channels)
        {
            float g = _grindGain, tone = _grindTone;
            float env = _impactEnv, freq = _impactFreq;
            double step = 2.0 * System.Math.PI * freq / _sampleRate;
            int frames = data.Length / channels;
            for (int i = 0; i < frames; i++)
            {
                _gainSmooth += (g - _gainSmooth) * 0.002f;
                float n = Noise();
                _lp += (n - _lp) * tone;
                float s = _lp * _gainSmooth * 0.45f;
                if (env > 1e-4f)
                {
                    _impactPhase += step;
                    s += (float)System.Math.Sin(_impactPhase) * env * 0.5f + n * env * 0.15f;
                    env *= 0.9993f;
                }
                for (int c = 0; c < channels; c++) data[i * channels + c] = s;
            }
            _impactEnv = env;
        }
    }

    /// <summary>World-space spark particles. Particle cap comes from the platform profile.</summary>
    public sealed class SparkEffect : MonoBehaviour
    {
        private ParticleSystem _ps;

        public void Setup(Material material, int maxParticles)
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.gravityModifier = 1.2f;
            main.startLifetime = 0.45f;
            main.startSize = 0.008f;
            var emission = _ps.emission;
            emission.enabled = false;
            var shape = _ps.shape;
            shape.enabled = false;
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
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

        /// <summary>Emit count sparks at a world position flying roughly along dir.</summary>
        public void Emit(Vector3 worldPos, Vector3 dir, int count, Color color)
        {
            if (_ps == null || count <= 0) return;
            var p = new ParticleSystem.EmitParams { position = worldPos, startColor = color };
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (dir.normalized + Random.insideUnitSphere * 0.7f) * Random.Range(0.25f, 0.9f);
                v.z = Random.Range(-0.1f, 0.1f);
                p.velocity = v;
                p.startLifetime = Random.Range(0.25f, 0.55f);
                p.startSize = Random.Range(0.005f, 0.012f);
                _ps.Emit(p, 1);
            }
        }
    }
}
