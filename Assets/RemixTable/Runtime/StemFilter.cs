using UnityEngine;
namespace RemixTable
{
    // Maps the bipolar Effect parameter (-1..1) to a DJ-style filter sweep.
    // Negative = low-pass (muffled), 0 = neutral (both filters transparent), positive = high-pass (thin).
    public static class StemFilter
    {
        public const float MaxCutoff = 22000f, MinLowpassCutoff = 250f;
        public const float MinHighpassCutoff = 10f, MaxHighpassCutoff = 4000f;
        public const float BaseResonance = 1f, PeakResonance = 1.6f;
        public static float SafeEffect(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, -1, 1);
        // Exponential sweeps so equal hand movements sound like equal musical steps.
        public static float LowpassCutoff(float effect)
        {
            float amount = Mathf.Max(0, -SafeEffect(effect));
            return MaxCutoff * Mathf.Pow(MinLowpassCutoff / MaxCutoff, amount);
        }
        // A plain exponential high-pass stays below ~170 Hz (inaudible on most stems) until
        // past half travel, so that direction felt dead. The sqrt shaping moves
        // the cutoff into the audible range early, matching how quickly the low-pass bites.
        public static float HighpassCutoff(float effect)
        {
            float amount = Mathf.Sqrt(Mathf.Max(0, SafeEffect(effect)));
            return MinHighpassCutoff * Mathf.Pow(MaxHighpassCutoff / MinHighpassCutoff, amount);
        }
        public static float Resonance(float effect) => Mathf.Lerp(BaseResonance, PeakResonance, Mathf.Abs(SafeEffect(effect)));
    }
}
