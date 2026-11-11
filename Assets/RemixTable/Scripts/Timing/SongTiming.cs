using System;

namespace RemixTable
{
    /// <summary>
    /// Pure musical-time maths for a looping song. "Song time" is seconds since the song started, unwrapped
    /// (it keeps growing across loops); "loop time" is song time wrapped into one loop.
    /// Beat and bar indices are 0-based.
    /// </summary>
    public readonly struct SongTiming
    {
        /// <summary>Tolerance (in grid units) so values like 3.9999999 still count as beat 4.</summary>
        const double Epsilon = 1e-6;

        public readonly double SecondsPerBeat;
        public readonly int BeatsPerBar;
        public readonly int LoopBars;

        public SongTiming(double secondsPerBeat, int beatsPerBar, int loopBars)
        {
            if (!(secondsPerBeat > 0)) throw new ArgumentOutOfRangeException(nameof(secondsPerBeat));
            if (beatsPerBar <= 0) throw new ArgumentOutOfRangeException(nameof(beatsPerBar));
            if (loopBars <= 0) throw new ArgumentOutOfRangeException(nameof(loopBars));
            SecondsPerBeat = secondsPerBeat;
            BeatsPerBar = beatsPerBar;
            LoopBars = loopBars;
        }

        public static SongTiming FromBpm(double bpm, int beatsPerBar, int loopBars) =>
            new SongTiming(60.0 / bpm, beatsPerBar, loopBars);

        /// <summary>Timing whose loop is exactly <paramref name="loopSeconds"/> long (use the real clip length).</summary>
        public static SongTiming FromLoopLength(double loopSeconds, int beatsPerBar, int loopBars) =>
            new SongTiming(loopSeconds / (beatsPerBar * (double)loopBars), beatsPerBar, loopBars);

        public double Bpm => 60.0 / SecondsPerBeat;
        public double SecondsPerBar => SecondsPerBeat * BeatsPerBar;
        public int BeatsPerLoop => BeatsPerBar * LoopBars;
        public double LoopSeconds => SecondsPerBar * LoopBars;

        /// <summary>Beats since the start, fractional and unwrapped. Negative before the start.</summary>
        public double BeatPosition(double songTime) => songTime / SecondsPerBeat;

        /// <summary>Song time wrapped into [0, LoopSeconds).</summary>
        public double LoopTime(double songTime)
        {
            double loop = LoopSeconds;
            double wrapped = songTime - Math.Floor(songTime / loop + Epsilon / BeatsPerLoop) * loop;
            return wrapped < 0 ? 0 : wrapped;
        }

        /// <summary>Whole beats since the start (unwrapped). -1 just before the start.</summary>
        public long BeatIndex(double songTime) => (long)Math.Floor(songTime / SecondsPerBeat + Epsilon);

        public long BarIndex(double songTime) => FloorDiv(BeatIndex(songTime), BeatsPerBar);

        public long LoopIndex(double songTime) => FloorDiv(BarIndex(songTime), LoopBars);

        /// <summary>Beat within the bar, 0..BeatsPerBar-1.</summary>
        public int BeatInBar(double songTime) => (int)PositiveMod(BeatIndex(songTime), BeatsPerBar);

        /// <summary>Bar within the loop, 0..LoopBars-1.</summary>
        public int BarInLoop(double songTime) => (int)PositiveMod(BarIndex(songTime), LoopBars);

        /// <summary>Song time of the first downbeat at or after <paramref name="songTime"/>
        /// (strictly after if <paramref name="strictlyAfter"/>).</summary>
        public double NextDownbeat(double songTime, bool strictlyAfter = false) =>
            Quantize(songTime, BeatsPerBar, strictlyAfter);

        /// <summary>Rounds <paramref name="songTime"/> up to the next multiple of <paramref name="gridBeats"/> beats.</summary>
        public double Quantize(double songTime, double gridBeats, bool strictlyAfter = false)
        {
            if (!(gridBeats > 0)) throw new ArgumentOutOfRangeException(nameof(gridBeats));
            double grid = gridBeats * SecondsPerBeat;
            double k = songTime / grid;
            double n = strictlyAfter ? Math.Floor(k + Epsilon) + 1.0 : Math.Ceiling(k - Epsilon);
            return n * grid;
        }

        /// <summary>
        /// After a pause, the audio resumes at <paramref name="audioLoopTime"/> (taken from the stem's play position).
        /// Returns the unwrapped song time that matches it and is closest to <paramref name="pausedSongTime"/>,
        /// so the loop count survives even if the clock and the audio were either side of the loop point.
        /// </summary>
        public double ResumeSongTime(double pausedSongTime, double audioLoopTime)
        {
            double loop = LoopSeconds;
            double loops = Math.Round((pausedSongTime - audioLoopTime) / loop);
            return Math.Max(0.0, loops) * loop + audioLoopTime;
        }

        static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);
        static long PositiveMod(long a, long b) => ((a % b) + b) % b;
    }
}
