using UnityEngine;
namespace RemixTable
{
    // Beat math for SNAP: a song's beats fall every 60/bpm seconds starting at `offset` (the first downbeat).
    public static class BeatGrid
    {
        public const int BeatsPerBar = 4;
        public static bool Valid(float bpm) => bpm >= 30 && bpm <= 300 && !float.IsNaN(bpm);
        public static float BeatLength(float bpm) => 60f / Mathf.Clamp(bpm, 30, 300);
        // Nearest beat to `time`, kept inside [0, duration].
        public static float Nearest(float time, float bpm, float offset, float duration)
        {
            if (!Valid(bpm) || duration <= 0) return time;
            float beat = BeatLength(bpm);
            float snapped = offset + Mathf.Round((time - offset) / beat) * beat;
            return Wrap(snapped, duration);
        }
        // The beat at or before `time`.
        public static float Floor(float time, float bpm, float offset, float duration)
        {
            if (!Valid(bpm) || duration <= 0) return time;
            float beat = BeatLength(bpm);
            // A small epsilon keeps a time that is already on a beat on that beat despite float error.
            float snapped = offset + Mathf.Floor((time - offset) / beat + 1e-4f) * beat;
            return Wrap(snapped, duration);
        }
        // A snapped region [start, end] covering both input times, at least one beat long.
        public static void SnapRegion(ref float start, ref float end, float bpm, float offset, float duration)
        {
            if (!Valid(bpm) || duration <= 0) return;
            float beat = BeatLength(bpm);
            float length = AutomationStore.RegionLength(start, end, duration);
            float s = Nearest(start, bpm, offset, duration);
            float snappedLength = Mathf.Max(beat, Mathf.Round(length / beat) * beat);
            if (snappedLength >= duration) snappedLength = duration - .001f;
            start = s; end = Wrap(s + snappedLength, duration);
        }
        // The one-beat region containing `time` (used by a one-hand pinch while SNAP is on).
        public static void BeatAround(float time, float bpm, float offset, float duration, out float start, out float end)
        {
            float beat = BeatLength(bpm);
            start = Floor(time, bpm, offset, duration);
            end = Wrap(start + beat, duration);
        }
        // True when `time` lies within `tolerance` seconds of a beat; `bar` reports whether it is a downbeat.
        public static bool OnBeat(float time, float bpm, float offset, float tolerance, out bool bar)
        {
            bar = false;
            if (!Valid(bpm)) return false;
            float beat = BeatLength(bpm);
            float position = (time - offset) / beat;
            float nearest = Mathf.Round(position);
            if (Mathf.Abs(position - nearest) * beat > tolerance) return false;
            bar = Mathf.RoundToInt(nearest) % BeatsPerBar == 0;
            return true;
        }
        static float Wrap(float t, float duration) => Mathf.Repeat(t, duration);
    }
}
