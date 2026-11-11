using System;
using UnityEngine;

namespace RemixTable
{
    /// <summary>What the drop does to the mix at one moment.</summary>
    public struct DropEffect
    {
        /// <summary>0..1: how far every stem's filter is pulled towards <see cref="MixTuning.dropBuildFilterTarget"/>.</summary>
        public float filterAmount;
        /// <summary>Extra gain on top of the mix (0.25 = +25%).</summary>
        public float gainBump;
        /// <summary>True right after the hit, so filters snap open fast.</summary>
        public bool fastFilter;
    }

    /// <summary>Pure drop timeline maths. The drop is described entirely by <see cref="DropState"/> plus song time,
    /// so any client can reproduce it from the synced state.</summary>
    public static class DropMath
    {
        /// <summary>The hit lands on the first downbeat at least <paramref name="buildBars"/> bars from now.</summary>
        public static double ComputeHitSongTime(double songTimeNow, in SongTiming timing, int buildBars)
        {
            double earliest = Math.Max(0.0, songTimeNow) + Math.Max(0, buildBars) * timing.SecondsPerBar;
            return timing.NextDownbeat(earliest);
        }

        /// <summary>The phase the drop should be in at <paramref name="songTime"/>.</summary>
        public static DropPhase PhaseAt(in DropState drop, double songTime, in SongTiming timing, MixTuning t)
        {
            if (drop.phase == DropPhase.Idle) return DropPhase.Idle;
            if (songTime < drop.hitSongTime) return DropPhase.Build;
            double since = songTime - drop.hitSongTime;
            double spb = timing.SecondsPerBeat;
            if (since < t.dropHitBeats * spb) return DropPhase.Hit;
            if (since < (t.dropHitBeats + t.dropReleaseBeats) * spb) return DropPhase.Release;
            return DropPhase.Idle;
        }

        /// <summary>Phases in order; Idle follows Release.</summary>
        public static DropPhase Next(DropPhase phase) => phase switch
        {
            DropPhase.Idle => DropPhase.Build,
            DropPhase.Build => DropPhase.Hit,
            DropPhase.Hit => DropPhase.Release,
            _ => DropPhase.Idle,
        };

        /// <summary>Build progress 0..1 (1 at the hit).</summary>
        public static float BuildProgress(in DropState drop, double songTime)
        {
            if (drop.phase == DropPhase.Idle) return 0f;
            double span = drop.hitSongTime - drop.buildStartSongTime;
            if (span <= 0) return 1f;
            return Mathf.Clamp01((float)((songTime - drop.buildStartSongTime) / span));
        }

        public static DropEffect Evaluate(in DropState drop, double songTime, in SongTiming timing, MixTuning t)
        {
            var effect = default(DropEffect);
            if (drop.phase == DropPhase.Idle) return effect;

            if (songTime < drop.hitSongTime)
            {
                float progress = BuildProgress(drop, songTime);
                effect.filterAmount = Mathf.Pow(progress, Mathf.Max(0.1f, t.dropBuildCurve));
                return effect;
            }

            double since = songTime - drop.hitSongTime;
            double spb = timing.SecondsPerBeat;
            double total = (t.dropHitBeats + t.dropReleaseBeats) * spb;
            if (total > 0 && since < total) effect.gainBump = t.dropHitBump * (float)(1.0 - since / total);
            effect.fastFilter = since < t.dropHitBeats * spb;
            return effect;
        }
    }
}
