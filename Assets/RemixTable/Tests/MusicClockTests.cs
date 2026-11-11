using NUnit.Framework;

namespace RemixTable.Tests
{
    sealed class ManualTimeSource : ITimeSource
    {
        public double Now { get; set; }
    }

    public class MusicClockTests
    {
        static readonly SongTiming Timing = SongTiming.FromBpm(120, 4, 8); // 0.5 s beats, 2 s bars, 16 s loop

        [Test]
        public void Timing_DerivedValues()
        {
            Assert.AreEqual(0.5, Timing.SecondsPerBeat, 1e-12);
            Assert.AreEqual(2.0, Timing.SecondsPerBar, 1e-12);
            Assert.AreEqual(16.0, Timing.LoopSeconds, 1e-12);
            Assert.AreEqual(32, Timing.BeatsPerLoop);
            Assert.AreEqual(120.0, Timing.Bpm, 1e-9);
        }

        [Test]
        public void FromLoopLength_MatchesFromBpmForExactLoops()
        {
            SongTiming fromLength = SongTiming.FromLoopLength(16.0, 4, 8);
            Assert.AreEqual(Timing.SecondsPerBeat, fromLength.SecondsPerBeat, 1e-12);
        }

        [TestCase(0.0, 0, 0)]
        [TestCase(0.49, 0, 0)]
        [TestCase(0.5, 0, 1)]
        [TestCase(1.99, 0, 3)]
        [TestCase(2.0, 1, 0)]
        [TestCase(15.99, 7, 3)]
        [TestCase(16.0, 0, 0)]   // loop wrap
        [TestCase(17.0, 0, 2)]
        [TestCase(33.0, 0, 2)]   // third loop
        [TestCase(46.5, 7, 1)]
        public void BarAndBeat_FromSongTime(double songTime, int bar, int beat)
        {
            Assert.AreEqual(bar, Timing.BarInLoop(songTime), "bar");
            Assert.AreEqual(beat, Timing.BeatInBar(songTime), "beat");
        }

        [Test]
        public void LoopIndex_CountsLoops()
        {
            Assert.AreEqual(0, Timing.LoopIndex(15.9));
            Assert.AreEqual(1, Timing.LoopIndex(16.0));
            Assert.AreEqual(2, Timing.LoopIndex(33.0));
        }

        [Test]
        public void BeforeTheStart_IsTheLastBeatOfThePreviousLoop()
        {
            Assert.AreEqual(-1, Timing.BeatIndex(-0.1));
            Assert.AreEqual(7, Timing.BarInLoop(-0.1));
            Assert.AreEqual(3, Timing.BeatInBar(-0.1));
        }

        [TestCase(0.0, 0.0)]
        [TestCase(0.25, 0.25)]
        [TestCase(16.0, 0.0)]
        [TestCase(16.25, 0.25)]
        [TestCase(47.5, 15.5)]
        [TestCase(-0.25, 15.75)]
        public void LoopTime_WrapsAround(double songTime, double expected)
        {
            Assert.AreEqual(expected, Timing.LoopTime(songTime), 1e-9);
        }

        [Test]
        public void BeatIndex_IsRobustToFloatingPointAtBoundaries()
        {
            SongTiming odd = SongTiming.FromBpm(140, 4, 8);
            for (int beat = 0; beat < 200; beat++)
                Assert.AreEqual(beat, odd.BeatIndex(beat * (60.0 / 140.0)), $"beat {beat}");
        }

        [TestCase(0.0, false, 0.0)]
        [TestCase(0.1, false, 2.0)]
        [TestCase(2.0, false, 2.0)]
        [TestCase(2.0, true, 4.0)]
        [TestCase(1.9999999999, false, 2.0)]
        [TestCase(15.5, false, 16.0)]    // next loop's first downbeat (unwrapped)
        [TestCase(-1.0, false, 0.0)]
        public void NextDownbeat(double songTime, bool strictlyAfter, double expected)
        {
            Assert.AreEqual(expected, Timing.NextDownbeat(songTime, strictlyAfter), 1e-9);
        }

        [TestCase(1.1, 1.0, false, 1.5)]
        [TestCase(1.5, 1.0, false, 1.5)]
        [TestCase(1.5, 1.0, true, 2.0)]
        [TestCase(1.1, 0.5, false, 1.25)]
        public void Quantize(double songTime, double gridBeats, bool strictlyAfter, double expected)
        {
            Assert.AreEqual(expected, Timing.Quantize(songTime, gridBeats, strictlyAfter), 1e-9);
        }

        [TestCase(33.0, 1.0, 33.0)]      // same loop
        [TestCase(31.99, 0.01, 32.01)]   // audio already wrapped into the next loop
        [TestCase(32.01, 15.99, 31.99)]  // clock wrapped, audio hadn't yet
        [TestCase(-0.1, 0.0, 0.0)]       // paused before the start
        public void ResumeSongTime_KeepsTheLoopCount(double pausedSongTime, double audioLoopTime, double expected)
        {
            Assert.AreEqual(expected, Timing.ResumeSongTime(pausedSongTime, audioLoopTime), 1e-9);
        }

        [Test]
        public void Clock_NotStarted_IsAtZero()
        {
            var clock = new MusicClock(Timing, new ManualTimeSource { Now = 50 });
            Assert.IsFalse(clock.IsStarted);
            Assert.IsFalse(clock.HasReachedStart);
            Assert.AreEqual(0.0, clock.SongTime);
        }

        [Test]
        public void Clock_FollowsTheTimeSource()
        {
            var time = new ManualTimeSource { Now = 99.7 };
            var clock = new MusicClock(Timing, time);
            clock.StartAt(100.0);

            Assert.IsFalse(clock.HasReachedStart, "scheduled in the future");
            Assert.AreEqual(-0.3, clock.SongTime, 1e-9);

            time.Now = 102.5;
            Assert.AreEqual(2.5, clock.SongTime, 1e-9);
            Assert.AreEqual(1, clock.BarInLoop);
            Assert.AreEqual(1, clock.BeatInBar);
            Assert.AreEqual(104.0, clock.NextDownbeatSourceTime(), 1e-9);
            Assert.AreEqual(2.5, clock.QuantizeSongTime(1.0), 1e-9, "already on a beat");
            Assert.AreEqual(3.0, clock.QuantizeSongTime(1.0, true), 1e-9);

            time.Now = 100.0 + 16.0 + 0.25;
            Assert.AreEqual(0.25, clock.LoopTime, 1e-9);
            Assert.AreEqual(0, clock.BarInLoop);
        }

        [Test]
        public void Clock_PauseFreezesAndStartAtResumes()
        {
            var time = new ManualTimeSource { Now = 100.0 };
            var clock = new MusicClock(Timing, time);
            clock.StartAt(100.0);

            time.Now = 103.0;
            clock.Pause();
            time.Now = 110.0;
            Assert.IsTrue(clock.IsPaused);
            Assert.AreEqual(3.0, clock.SongTime, 1e-9);

            clock.StartAt(200.0, 3.0);
            time.Now = 200.0;
            Assert.IsFalse(clock.IsPaused);
            Assert.AreEqual(3.0, clock.SongTime, 1e-9);
            Assert.AreEqual(200.0 + 1.0, clock.SongTimeToSourceTime(4.0), 1e-9);
        }

        [Test]
        public void Clock_AdjustStartTime_MovesSongTimeBack()
        {
            var time = new ManualTimeSource { Now = 10.0 };
            var clock = new MusicClock(Timing, time);
            clock.StartAt(0.0);
            clock.AdjustStartTime(0.25);
            Assert.AreEqual(9.75, clock.SongTime, 1e-9);
        }
    }
}
