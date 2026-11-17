using ForgeBorn.UnityLayer;
using NUnit.Framework;
using UnityEngine;

namespace ForgeBorn.Tests
{
    /// <summary>Audio is procedural, so the sample buffers can be checked without an audio device.</summary>
    public class AudioTests
    {
        private GameObject _go;
        private SandboxAudio _audio;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("audio_test");
            _audio = _go.AddComponent<SandboxAudio>(); // no Setup(): no AudioSource / device needed
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        private static double Rms(float[] d, int from, int to)
        {
            double sum = 0.0;
            for (int i = from; i < to; i++) sum += d[i] * d[i];
            return System.Math.Sqrt(sum / (to - from));
        }

        private float[] Block(int frames = 4096, int channels = 2)
        {
            var buf = new float[frames * channels];
            _audio.Render(buf, channels);
            return buf;
        }

        [Test]
        public void Silent_WhenNothingIsHappening()
        {
            float[] b = Block();
            Assert.Less(Rms(b, 0, b.Length), 1e-6);
        }

        [Test]
        public void Grinding_IsAudible_FiniteAndBounded_AndLouderWithMoreGain()
        {
            _audio.SetGrind(0.3f, 0.5f);
            Block(); // let the gain smoothing settle
            Block();
            float[] quiet = Block();
            _audio.SetGrind(1f, 0.5f);
            for (int i = 0; i < 6; i++) Block();
            float[] loud = Block();

            double rq = Rms(quiet, 0, quiet.Length), rl = Rms(loud, 0, loud.Length);
            Assert.Greater(rq, 0.001);
            Assert.Greater(rl, rq * 1.5, "more pressure must be louder");
            foreach (float s in loud) { Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s)); Assert.LessOrEqual(System.Math.Abs(s), 1f); }
        }

        [Test]
        public void Brighter_GrindingHasMoreHighFrequencyContent()
        {
            _audio.SetGrind(1f, 0f);
            for (int i = 0; i < 6; i++) Block();
            float[] dull = Block();
            _audio.SetGrind(1f, 1f);
            for (int i = 0; i < 6; i++) Block();
            float[] bright = Block();

            Assert.Greater(Diff(bright), Diff(dull) * 1.5, "higher tone setting passes more high frequencies");
        }

        private static double Diff(float[] d)
        {
            double sum = 0.0;
            for (int i = 2; i < d.Length; i += 2) sum += System.Math.Abs(d[i] - d[i - 2]);
            return sum / (d.Length / 2);
        }

        [Test]
        public void HammerImpact_IsAudible_ThenDecays()
        {
            _audio.Impact(1f, 0f);
            float[] first = Block(2048);
            Block(48000 / 2);
            float[] later = Block(2048);

            Assert.Greater(Rms(first, 0, first.Length), 0.05);
            Assert.Less(Rms(later, 0, later.Length), Rms(first, 0, first.Length) * 0.5, "the clank dies away");
            foreach (float s in first) Assert.LessOrEqual(System.Math.Abs(s), 1f);
        }

        [Test]
        public void StereoChannelsCarryTheSameSignal()
        {
            _audio.SetGrind(0.8f, 0.5f);
            Block();
            float[] b = Block(256, 2);
            for (int i = 0; i < b.Length; i += 2) Assert.AreEqual(b[i], b[i + 1]);
        }
    }
}
