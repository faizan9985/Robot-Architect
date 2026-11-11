using System;

namespace RemixTable
{
    /// <summary>
    /// Where we are in the song, on the audio clock. Plain C#: <see cref="StemPlayer"/> owns one and starts it at the
    /// same dsp time it schedules the stems. "Source time" means the <see cref="ITimeSource"/>'s time (dspTime locally).
    /// </summary>
    public sealed class MusicClock
    {
        readonly ITimeSource timeSource;
        double pausedSongTime;

        public MusicClock(SongTiming timing, ITimeSource timeSource)
        {
            Timing = timing;
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
        }

        public SongTiming Timing { get; }
        public ITimeSource TimeSource => timeSource;
        public double Now => timeSource.Now;

        public bool IsStarted { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsRunning => IsStarted && !IsPaused;

        /// <summary>Source time at which song time is 0.</summary>
        public double StartTime { get; private set; }

        /// <summary>Starts (or restarts) so that song time is <paramref name="songTimeAtStart"/> at
        /// <paramref name="sourceTime"/>. Song time is negative until then.</summary>
        public void StartAt(double sourceTime, double songTimeAtStart = 0.0)
        {
            StartTime = sourceTime - songTimeAtStart;
            IsStarted = true;
            IsPaused = false;
        }

        /// <summary>Freezes song time until the next <see cref="StartAt"/>.</summary>
        public void Pause()
        {
            if (!IsStarted || IsPaused) return;
            pausedSongTime = SongTime;
            IsPaused = true;
        }

        public void Stop()
        {
            IsStarted = false;
            IsPaused = false;
        }

        /// <summary>Moves the clock back by <paramref name="seconds"/> (song time decreases). Used to re-anchor
        /// the clock on the audio's real play position.</summary>
        public void AdjustStartTime(double seconds) => StartTime += seconds;

        /// <summary>Seconds since the song started (unwrapped). 0 if not started; frozen while paused.</summary>
        public double SongTime => !IsStarted ? 0.0 : IsPaused ? pausedSongTime : timeSource.Now - StartTime;

        public bool HasReachedStart => IsStarted && SongTime >= 0.0;
        public double LoopTime => Timing.LoopTime(SongTime);
        public double BeatPosition => Timing.BeatPosition(SongTime);
        public long BeatIndex => Timing.BeatIndex(SongTime);
        public int BeatInBar => Timing.BeatInBar(SongTime);
        public int BarInLoop => Timing.BarInLoop(SongTime);

        public double SongTimeToSourceTime(double songTime) => StartTime + songTime;

        public double NextDownbeatSongTime(bool strictlyAfter = false) =>
            Timing.NextDownbeat(Math.Max(0.0, SongTime), strictlyAfter);

        public double NextDownbeatSourceTime(bool strictlyAfter = false) =>
            SongTimeToSourceTime(NextDownbeatSongTime(strictlyAfter));

        /// <summary>Next song time on a grid of <paramref name="gridBeats"/> beats (1 = next beat, 4 = next bar in 4/4).</summary>
        public double QuantizeSongTime(double gridBeats, bool strictlyAfter = false) =>
            Timing.Quantize(Math.Max(0.0, SongTime), gridBeats, strictlyAfter);
    }
}
