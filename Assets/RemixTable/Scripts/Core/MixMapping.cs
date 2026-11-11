using UnityEngine;

namespace RemixTable
{
    /// <summary>Pure gesture-to-sound mapping. Every curve and dead zone lives here; the constants live in
    /// <see cref="MixTuning"/>. No state, no Unity objects besides the tuning asset.</summary>
    public static class MixMapping
    {
        /// <summary>Silence for the AudioReverbFilter room level, in millibels.</summary>
        public const float SilentMillibels = -10000f;

        /// <summary>Horizontal distance from the listener → gain. Full gain up close, smooth (dB-linear smoothstep)
        /// fall-off to the quiet floor.</summary>
        public static float DistanceToGain(float distanceMetres, MixTuning t)
        {
            if (float.IsNaN(distanceMetres)) return t.quietGain;
            float x = InverseLerpClamped(t.fullGainDistance, t.quietDistance, distanceMetres);
            if (x <= 0f) return 1f;
            if (x >= 1f) return t.quietGain;
            float eased = x * x * (3f - 2f * x);
            float floorDb = GainToDb(Mathf.Max(t.quietGain, 1e-4f));
            return DbToGain(eased * floorDb);
        }

        /// <summary>Height of the orb's bottom above the desk → reverb 0..1.</summary>
        public static float HeightToReverb(float heightMetres, MixTuning t)
        {
            if (float.IsNaN(heightMetres)) return 0f;
            return InverseLerpClamped(t.reverbStartHeight, t.reverbFullHeight, heightMetres);
        }

        /// <summary>Wrist twist in degrees → filter -1..1. Negative twist = low-pass, positive = high-pass,
        /// with a dead zone around zero. Clamped at ±<see cref="MixTuning.twistFullDegrees"/>.</summary>
        public static float TwistToFilter(float degrees, MixTuning t)
        {
            if (float.IsNaN(degrees)) return 0f;
            float magnitude = Mathf.Abs(degrees);
            if (magnitude <= t.twistDeadZoneDegrees) return 0f;
            float range = Mathf.Max(1e-3f, t.twistFullDegrees - t.twistDeadZoneDegrees);
            float amount = Mathf.Clamp01((magnitude - t.twistDeadZoneDegrees) / range);
            return degrees < 0f ? -amount : amount;
        }

        /// <summary>Inverse of <see cref="TwistToFilter"/>: the twist angle that gives this filter value.</summary>
        public static float FilterToTwist(float filter, MixTuning t)
        {
            filter = Mathf.Clamp(filter, -1f, 1f);
            if (filter == 0f) return 0f;
            float magnitude = t.twistDeadZoneDegrees + Mathf.Abs(filter) * (t.twistFullDegrees - t.twistDeadZoneDegrees);
            return filter < 0f ? -magnitude : magnitude;
        }

        /// <summary>Low-pass cutoff: fully open for filter ≥ 0, sweeping exponentially to closed at -1.</summary>
        public static float LowPassCutoffHz(float filter, MixTuning t)
        {
            if (!(filter < 0f)) return t.lowPassOpenHz;
            return ExpLerp(t.lowPassOpenHz, t.lowPassClosedHz, Mathf.Clamp01(-filter));
        }

        /// <summary>High-pass cutoff: fully open for filter ≤ 0, sweeping exponentially to closed at +1.</summary>
        public static float HighPassCutoffHz(float filter, MixTuning t)
        {
            if (!(filter > 0f)) return t.highPassOpenHz;
            return ExpLerp(t.highPassOpenHz, t.highPassClosedHz, Mathf.Clamp01(filter));
        }

        /// <summary>The gain a stem should actually play at, after mute and solo rules (see <see cref="MixState"/>).</summary>
        public static float EffectiveGain(in StemMix stem, bool anySoloed, MixTuning t)
        {
            if (stem.soloed) return stem.gain01;
            if (stem.muted) return 0f;
            return anySoloed ? stem.gain01 * t.soloDuckGain : stem.gain01;
        }

        /// <summary>Reverb 0..1 → AudioReverbFilter room (wet) level in millibels.</summary>
        public static float ReverbToRoomMillibels(float reverb01, MixTuning t)
        {
            float amplitude = Mathf.Clamp01(reverb01) * t.reverbMaxWet;
            return AmplitudeToMillibels(amplitude);
        }

        /// <summary>Reverb 0..1 → AudioReverbFilter dry level in millibels (the dry signal dips a little at full reverb).</summary>
        public static float ReverbToDryMillibels(float reverb01, MixTuning t)
        {
            float amplitude = Mathf.Lerp(1f, t.reverbDryAtFull, Mathf.Clamp01(reverb01));
            return AmplitudeToMillibels(amplitude);
        }

        public static float AmplitudeToMillibels(float amplitude) =>
            amplitude <= 1e-5f ? SilentMillibels : Mathf.Clamp(2000f * Mathf.Log10(amplitude), SilentMillibels, 0f);

        public static float GainToDb(float gain) => 20f * Mathf.Log10(Mathf.Max(gain, 1e-6f));
        public static float DbToGain(float db) => Mathf.Pow(10f, db / 20f);

        /// <summary>Interpolates in log space: equal steps of t are equal musical intervals.</summary>
        public static float ExpLerp(float from, float to, float t) => from * Mathf.Pow(to / from, Mathf.Clamp01(t));

        static float InverseLerpClamped(float a, float b, float value)
        {
            if (b <= a) return value <= a ? 0f : 1f;
            return Mathf.Clamp01((value - a) / (b - a));
        }
    }
}
