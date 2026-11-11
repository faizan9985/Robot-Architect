using System;

namespace RemixTable
{
    /// <summary>
    /// Per-stem filter and reverb. <see cref="StemPlayer"/> calls these every frame with already-smoothed values,
    /// so implementations should only push values that changed. Swap implementations via
    /// <see cref="StemEffectsBackend"/> (for example an AudioMixer-based one if Unity's filter components don't
    /// work with the Meta XR Audio spatializer on Quest).
    /// </summary>
    public interface IStemEffects : IDisposable
    {
        /// <summary>-1 = full low-pass, 0 = neutral, +1 = full high-pass.</summary>
        void SetFilter(float filter);

        /// <summary>0 = dry, 1 = full reverb.</summary>
        void SetReverb(float reverb01);
    }

    public enum StemEffectsBackend
    {
        /// <summary>AudioLowPassFilter + AudioHighPassFilter + AudioReverbFilter on each stem's AudioSource.</summary>
        FilterComponents,
        // AudioMixer: planned fallback; see INTEGRATION_M1.md.
    }
}
