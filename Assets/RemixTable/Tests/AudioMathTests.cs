using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RemixTable.Tests
{
    public class RampedValueTests
    {
        [Test]
        public void JumpTakesTheFullRampTime()
        {
            var v = new RampedValue(0f);
            v.SetTarget(1f, 0.02f);
            Assert.AreEqual(0.5f, v.Tick(0.01f), 1e-5f);
            Assert.Less(v.Tick(0.009f), 1f, "not there before 20 ms");
            Assert.AreEqual(1f, v.Tick(0.01f));
            Assert.AreEqual(1f, v.Tick(0.5f), "no overshoot");
            Assert.IsTrue(v.IsSettled);
        }

        [Test]
        public void SmallChangesAlsoTakeTheFullRampTime()
        {
            var v = new RampedValue(0.5f);
            v.SetTarget(0.6f, 0.02f);
            Assert.AreEqual(0.55f, v.Tick(0.01f), 1e-5f);
            Assert.AreEqual(0.6f, v.Tick(0.01f), 1e-5f);
        }

        [Test]
        public void RampsDownToo()
        {
            var v = new RampedValue(1f);
            v.SetTarget(0f, 0.04f);
            Assert.AreEqual(0.75f, v.Tick(0.01f), 1e-5f);
        }

        [Test]
        public void RetargetingMidRampRestartsFromTheCurrentValue()
        {
            var v = new RampedValue(0f);
            v.SetTarget(1f, 0.02f);
            v.Tick(0.01f); // 0.5
            v.SetTarget(0f, 0.02f);
            Assert.AreEqual(0.25f, v.Tick(0.01f), 1e-5f);
        }

        [Test]
        public void ZeroRampIsInstantAndSnapResets()
        {
            var v = new RampedValue(0f);
            v.SetTarget(0.8f, 0f);
            Assert.AreEqual(0.8f, v.Tick(0.001f));
            v.Snap(0.1f);
            Assert.AreEqual(0.1f, v.Current);
            Assert.AreEqual(0.1f, v.Target);
        }

        [Test]
        public void Follow_RisesWithAttackAndFallsWithRelease()
        {
            float up = Smoothing.Follow(0f, 1f, 0.01f, 0.01f, 1f);
            Assert.AreEqual(1f - Mathf.Exp(-1f), up, 1e-5f);
            float down = Smoothing.Follow(1f, 0f, 0.01f, 0.01f, 1f);
            Assert.AreEqual(Mathf.Exp(-0.01f), down, 1e-5f);
            Assert.AreEqual(0.3f, Smoothing.Follow(0.9f, 0.3f, 0.01f, 0f, 0f));
        }
    }

    public class SampleMathTests
    {
        [TestCase(100, 100, 1000, 0)]
        [TestCase(100, 356, 1000, 256)]
        [TestCase(10, 990, 1000, 20)]   // across the loop point
        [TestCase(990, 10, 1000, 20)]
        [TestCase(0, 500, 1000, 500)]
        public void CircularDistance(int a, int b, int length, int expected)
        {
            Assert.AreEqual(expected, SampleMath.CircularDistance(a, b, length));
        }

        [TestCase(0.25, 16.0, 0.25)]
        [TestCase(15.9, 16.0, -0.1)]
        [TestCase(-0.1, 16.0, -0.1)]
        [TestCase(8.0, 16.0, 8.0)]
        public void WrapSigned(double value, double length, double expected)
        {
            Assert.AreEqual(expected, SampleMath.WrapSigned(value, length), 1e-9);
        }
    }

    public class BandAnalysisTests
    {
        const int Rate = 48000;

        static float[] Sine(double hz, int samples, float amplitude = 1f)
        {
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = amplitude * (float)Math.Sin(2 * Math.PI * hz * i / Rate);
            return data;
        }

        static BandLevels Analyze(float[] data) => BandAnalysis.Analyze(data, 1,
            BandAnalysis.OnePoleCoefficient(250f, Rate), BandAnalysis.OnePoleCoefficient(4000f, Rate));

        [Test]
        public void Rms_OfAFullScaleSineIsAbout0707()
        {
            Assert.AreEqual(0.7071f, Analyze(Sine(1000, 4800)).rms, 0.01f);
        }

        [Test]
        public void Silence_GivesZeros()
        {
            BandLevels b = Analyze(new float[1024]);
            Assert.AreEqual(0f, b.rms);
            Assert.AreEqual(0f, b.low);
            Assert.AreEqual(0f, b.mid);
            Assert.AreEqual(0f, b.high);
            Assert.AreEqual(0f, BandAnalysis.Analyze(null, 1, 0.1f, 0.2f).rms);
        }

        [Test]
        public void LowTone_IsMostlyLow()
        {
            BandLevels b = Analyze(Sine(60, 4800));
            Assert.Greater(b.low, b.mid);
            Assert.Greater(b.low, b.high);
        }

        [Test]
        public void MidTone_IsMostlyMid()
        {
            BandLevels b = Analyze(Sine(1000, 4800));
            Assert.Greater(b.mid, b.low);
            Assert.Greater(b.mid, b.high);
        }

        [Test]
        public void HighTone_IsMostlyHigh()
        {
            BandLevels b = Analyze(Sine(10000, 4800));
            Assert.Greater(b.high, b.mid);
            Assert.Greater(b.high, b.low);
        }

        [Test]
        public void Stereo_IsDownmixed()
        {
            float[] mono = Sine(1000, 2048);
            var stereo = new float[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++)
            {
                stereo[2 * i] = mono[i];
                stereo[2 * i + 1] = mono[i];
            }
            BandLevels a = Analyze(mono);
            BandLevels b = BandAnalysis.Analyze(stereo, 2,
                BandAnalysis.OnePoleCoefficient(250f, Rate), BandAnalysis.OnePoleCoefficient(4000f, Rate));
            Assert.AreEqual(a.rms, b.rms, 1e-5f);
        }
    }

    public class SongDefinitionTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in created) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        AudioClip Clip(string name, int samples)
        {
            AudioClip clip = AudioClip.Create(name, samples, 1, 48000, false);
            created.Add(clip);
            return clip;
        }

        SongDefinition Song(params AudioClip[] clips)
        {
            var song = ScriptableObject.CreateInstance<SongDefinition>();
            created.Add(song);
            var stems = new List<StemDefinition>();
            foreach (AudioClip c in clips) stems.Add(new StemDefinition(c != null ? c.name : "missing", StemRole.Other, c, Color.white));
            song.EditorConfigure("Test", "Tester", "CC0", "", 120f, 4, 1, stems); // 1 bar = 2 s = 96000 samples
            return song;
        }

        [Test]
        public void MatchingStems_Validate()
        {
            SongDefinition song = Song(Clip("a", 96000), Clip("b", 96000));
            Assert.IsTrue(song.TryValidate(out string errors, out string warnings), errors);
            StringAssert.DoesNotContain("samples", warnings);
            StringAssert.DoesNotContain("BPM", warnings);
        }

        [Test]
        public void DifferentLengths_Warn()
        {
            SongDefinition song = Song(Clip("a", 96000), Clip("b", 96010));
            Assert.IsTrue(song.TryValidate(out _, out string warnings));
            StringAssert.Contains("drift", warnings);
        }

        [Test]
        public void LengthNotMatchingBpm_Warns()
        {
            SongDefinition song = Song(Clip("a", 100000));
            song.TryValidate(out _, out string warnings);
            StringAssert.Contains("BPM", warnings);
        }

        [Test]
        public void MissingClip_IsAnError()
        {
            SongDefinition song = Song(Clip("a", 96000), null);
            Assert.IsFalse(song.TryValidate(out string errors, out _));
            StringAssert.Contains("no clip", errors);
        }

        [Test]
        public void ComputeTiming_FollowsTheClipLength()
        {
            SongDefinition song = Song(Clip("a", 96000));
            Assert.AreEqual(0.5, song.ComputeTiming().SecondsPerBeat, 1e-9);
            Assert.AreEqual(2.0, song.ComputeTiming().LoopSeconds, 1e-9);
        }
    }
}
