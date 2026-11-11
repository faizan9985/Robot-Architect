using NUnit.Framework;
using UnityEngine;

namespace RemixTable.Tests
{
    public class DropMathTests
    {
        static readonly SongTiming Timing = SongTiming.FromBpm(120, 4, 8); // 2 s bars
        MixTuning t;

        [SetUp]
        public void SetUp() => t = ScriptableObject.CreateInstance<MixTuning>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(t);

        DropState BuildAt(double now) => new DropState
        {
            phase = DropPhase.Build,
            buildStartSongTime = now,
            hitSongTime = DropMath.ComputeHitSongTime(now, Timing, t.dropBuildBars),
        };

        [TestCase(0.0, 4.0)]     // on a downbeat: exactly 2 bars
        [TestCase(0.1, 6.0)]
        [TestCase(3.9, 8.0)]
        [TestCase(4.0, 8.0)]
        [TestCase(15.0, 20.0)]   // across the loop point (unwrapped song time)
        public void Hit_LandsOnTheNextDownbeatAtLeastTwoBarsAway(double now, double expectedHit)
        {
            double hit = DropMath.ComputeHitSongTime(now, Timing, 2);
            Assert.AreEqual(expectedHit, hit, 1e-9);
            Assert.GreaterOrEqual(hit, now + 2 * Timing.SecondsPerBar - 1e-9);
            Assert.AreEqual(0, Timing.BeatInBar(hit));
        }

        [Test]
        public void Phases_RunBuildHitReleaseIdle()
        {
            DropState drop = BuildAt(1.0); // hit at 6.0; hit lasts 1 beat, release 3 beats
            Assert.AreEqual(DropPhase.Build, DropMath.PhaseAt(drop, 1.0, Timing, t));
            Assert.AreEqual(DropPhase.Build, DropMath.PhaseAt(drop, 5.99, Timing, t));
            Assert.AreEqual(DropPhase.Hit, DropMath.PhaseAt(drop, 6.0, Timing, t));
            Assert.AreEqual(DropPhase.Hit, DropMath.PhaseAt(drop, 6.49, Timing, t));
            Assert.AreEqual(DropPhase.Release, DropMath.PhaseAt(drop, 6.5, Timing, t));
            Assert.AreEqual(DropPhase.Release, DropMath.PhaseAt(drop, 7.99, Timing, t));
            Assert.AreEqual(DropPhase.Idle, DropMath.PhaseAt(drop, 8.0, Timing, t));
            Assert.AreEqual(DropPhase.Idle, DropMath.PhaseAt(DropState.Idle, 6.0, Timing, t));
        }

        [Test]
        public void Next_StepsThroughThePhases()
        {
            Assert.AreEqual(DropPhase.Build, DropMath.Next(DropPhase.Idle));
            Assert.AreEqual(DropPhase.Hit, DropMath.Next(DropPhase.Build));
            Assert.AreEqual(DropPhase.Release, DropMath.Next(DropPhase.Hit));
            Assert.AreEqual(DropPhase.Idle, DropMath.Next(DropPhase.Release));
        }

        [Test]
        public void Build_SweepsTheFilterFromZeroToFull()
        {
            DropState drop = BuildAt(0.0); // hit at 4.0
            Assert.AreEqual(0f, DropMath.Evaluate(drop, 0.0, Timing, t).filterAmount, 1e-6f);

            float previous = 0f;
            for (double s = 0.1; s < 4.0; s += 0.1)
            {
                DropEffect e = DropMath.Evaluate(drop, s, Timing, t);
                Assert.GreaterOrEqual(e.filterAmount, previous);
                Assert.AreEqual(0f, e.gainBump);
                previous = e.filterAmount;
            }
            Assert.Greater(DropMath.Evaluate(drop, 3.999, Timing, t).filterAmount, 0.99f);
        }

        [Test]
        public void Hit_RestoresTheFilterAndBumpsTheGainThenDecays()
        {
            DropState drop = BuildAt(0.0).WithPhase(DropPhase.Hit); // hit at 4.0, bump over 4 beats = 2 s
            DropEffect atHit = DropMath.Evaluate(drop, 4.0, Timing, t);
            Assert.AreEqual(0f, atHit.filterAmount);
            Assert.AreEqual(t.dropHitBump, atHit.gainBump, 1e-6f);
            Assert.IsTrue(atHit.fastFilter);

            DropEffect midway = DropMath.Evaluate(drop, 5.0, Timing, t);
            Assert.AreEqual(t.dropHitBump * 0.5f, midway.gainBump, 1e-5f);
            Assert.IsFalse(midway.fastFilter);

            Assert.AreEqual(0f, DropMath.Evaluate(drop, 6.0, Timing, t).gainBump, 1e-6f);
            Assert.AreEqual(0f, DropMath.Evaluate(drop, 9.0, Timing, t).gainBump);
        }

        [Test]
        public void Idle_HasNoEffect()
        {
            DropEffect e = DropMath.Evaluate(DropState.Idle, 3.0, Timing, t);
            Assert.AreEqual(0f, e.filterAmount);
            Assert.AreEqual(0f, e.gainBump);
            Assert.IsFalse(e.fastFilter);
        }

        [Test]
        public void BuildProgress_ClampsToZeroAndOne()
        {
            DropState drop = BuildAt(0.0);
            Assert.AreEqual(0f, DropMath.BuildProgress(drop, -1.0));
            Assert.AreEqual(0.5f, DropMath.BuildProgress(drop, 2.0), 1e-6f);
            Assert.AreEqual(1f, DropMath.BuildProgress(drop, 10.0));
        }
    }
}
