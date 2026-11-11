using UnityEngine;

namespace RemixTable
{
    /// <summary>Every curve constant for the gesture-to-sound mapping, smoothing and the drop, in one asset.
    /// Create one with Assets > Create > Remix Table > Mix Tuning. Components without one use these defaults.</summary>
    [CreateAssetMenu(menuName = "Remix Table/Mix Tuning", fileName = "MixTuning")]
    public sealed class MixTuning : ScriptableObject
    {
        /// <summary>The spec's floor: no gain change may ramp faster than this.</summary>
        public const float MinGainRampSeconds = 0.02f;

        [Header("Distance to listener → gain (horizontal, metres)")]
        [Tooltip("At or closer than this the stem is at full gain.")]
        public float fullGainDistance = 0.25f;
        [Tooltip("At or further than this the stem sits at the quiet floor.")]
        public float quietDistance = 0.85f;
        [Range(0f, 1f)] public float quietGain = 0.05f;

        [Header("Height above desk → reverb (metres, measured from the orb's bottom)")]
        public float reverbStartHeight = 0.03f;
        public float reverbFullHeight = 0.30f;

        [Header("Wrist twist → filter (degrees)")]
        public float twistDeadZoneDegrees = 8f;
        public float twistFullDegrees = 90f;

        [Header("Filter cutoffs (Hz, exponential sweep)")]
        public float lowPassOpenHz = 22000f;
        public float lowPassClosedHz = 350f;
        public float highPassOpenHz = 10f;
        public float highPassClosedHz = 2500f;

        [Header("Solo")]
        [Tooltip("Gain multiplier for the other stems while something is soloed.")]
        [Range(0f, 1f)] public float soloDuckGain = 0.15f;

        [Header("Reverb")]
        [Range(0f, 1f)] public float reverbMaxWet = 0.9f;
        [Tooltip("Dry level multiplier at full reverb.")]
        [Range(0f, 1f)] public float reverbDryAtFull = 0.6f;
        [Range(0.1f, 20f)] public float reverbDecaySeconds = 2.2f;

        [Header("Smoothing (seconds for a change to complete)")]
        [Min(MinGainRampSeconds)] public float gainRampSeconds = 0.03f;
        [Min(0f)] public float filterRampSeconds = 0.06f;
        [Min(0f)] public float reverbRampSeconds = 0.12f;
        [Min(MinGainRampSeconds)] public float transportFadeSeconds = 0.05f;

        [Header("Output")]
        [Tooltip("Master scale so the drop's gain bump has room above full gain.")]
        [Range(0f, 1f)] public float masterHeadroom = 0.8f;

        [Header("Drop")]
        [Min(0)] public int dropBuildBars = 2;
        [Tooltip("Filter value every stem sweeps towards during the build (-1 = fully closed low-pass).")]
        [Range(-1f, 0f)] public float dropBuildFilterTarget = -0.9f;
        [Tooltip("Sweep shape: 1 = linear, >1 = slow start then a fast close near the hit.")]
        [Min(0.1f)] public float dropBuildCurve = 2f;
        [Tooltip("Extra gain at the hit (0.25 = +25%), fading out over the hit and release.")]
        [Range(0f, 1f)] public float dropHitBump = 0.25f;
        [Min(0f)] public float dropHitBeats = 1f;
        [Min(0f)] public float dropReleaseBeats = 3f;
        [Tooltip("How fast the filters snap open at the hit.")]
        [Min(0f)] public float dropHitFilterRampSeconds = 0.02f;

        void OnValidate()
        {
            quietDistance = Mathf.Max(quietDistance, fullGainDistance);
            reverbFullHeight = Mathf.Max(reverbFullHeight, reverbStartHeight);
            twistFullDegrees = Mathf.Max(twistFullDegrees, twistDeadZoneDegrees + 1f);
            lowPassOpenHz = Mathf.Max(lowPassOpenHz, 1f);
            lowPassClosedHz = Mathf.Clamp(lowPassClosedHz, 1f, lowPassOpenHz);
            highPassOpenHz = Mathf.Max(highPassOpenHz, 1f);
            highPassClosedHz = Mathf.Max(highPassClosedHz, highPassOpenHz);
            gainRampSeconds = Mathf.Max(gainRampSeconds, MinGainRampSeconds);
            transportFadeSeconds = Mathf.Max(transportFadeSeconds, MinGainRampSeconds);
        }

        /// <summary>A defaults-only instance for components that have no tuning asset assigned.</summary>
        public static MixTuning CreateRuntimeDefault()
        {
            var tuning = CreateInstance<MixTuning>();
            tuning.name = "MixTuning (defaults)";
            tuning.hideFlags = HideFlags.DontSave;
            return tuning;
        }
    }
}
