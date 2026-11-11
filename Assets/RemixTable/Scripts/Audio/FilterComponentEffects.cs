using UnityEngine;

namespace RemixTable
{
    /// <summary>
    /// <see cref="IStemEffects"/> using Unity's built-in filter components on the stem's GameObject.
    /// DSP order follows component order: AudioSource → low-pass → high-pass → reverb.
    /// Neutral settings (22 kHz low-pass, 10 Hz high-pass, reverb room at -10000 mB) are effectively transparent.
    /// </summary>
    public sealed class FilterComponentEffects : IStemEffects
    {
        const float RelativeCutoffEpsilon = 0.002f;
        const float MillibelEpsilon = 1f;

        readonly MixTuning tuning;
        readonly AudioLowPassFilter lowPass;
        readonly AudioHighPassFilter highPass;
        readonly AudioReverbFilter reverb;

        float appliedLowHz = -1f;
        float appliedHighHz = -1f;
        float appliedRoomMb = float.NaN;
        float appliedDryMb = float.NaN;

        public FilterComponentEffects(GameObject host, MixTuning tuning)
        {
            this.tuning = tuning;

            lowPass = GetOrAdd<AudioLowPassFilter>(host);
            lowPass.lowpassResonanceQ = 1f;

            highPass = GetOrAdd<AudioHighPassFilter>(host);
            highPass.highpassResonanceQ = 1f;

            reverb = GetOrAdd<AudioReverbFilter>(host);
            reverb.reverbPreset = AudioReverbPreset.User;
            reverb.decayTime = tuning.reverbDecaySeconds;
            reverb.decayHFRatio = 0.6f;
            reverb.roomHF = -300f;
            reverb.roomLF = 0f;
            reverb.reflectionsLevel = -1200f;
            reverb.reflectionsDelay = 0.012f;
            reverb.reverbLevel = 0f;
            reverb.reverbDelay = 0.025f;
            reverb.hfReference = 5000f;
            reverb.lfReference = 250f;
            reverb.diffusion = 100f;
            reverb.density = 100f;

            SetFilter(0f);
            SetReverb(0f);
        }

        public void SetFilter(float filter)
        {
            float low = MixMapping.LowPassCutoffHz(filter, tuning);
            if (CutoffChanged(appliedLowHz, low))
            {
                lowPass.cutoffFrequency = low;
                appliedLowHz = low;
            }

            float high = MixMapping.HighPassCutoffHz(filter, tuning);
            if (CutoffChanged(appliedHighHz, high))
            {
                highPass.cutoffFrequency = high;
                appliedHighHz = high;
            }
        }

        public void SetReverb(float reverb01)
        {
            float room = MixMapping.ReverbToRoomMillibels(reverb01, tuning);
            if (float.IsNaN(appliedRoomMb) || Mathf.Abs(room - appliedRoomMb) > MillibelEpsilon)
            {
                reverb.room = room;
                appliedRoomMb = room;
            }

            float dry = MixMapping.ReverbToDryMillibels(reverb01, tuning);
            if (float.IsNaN(appliedDryMb) || Mathf.Abs(dry - appliedDryMb) > MillibelEpsilon)
            {
                reverb.dryLevel = dry;
                appliedDryMb = dry;
            }
        }

        public void Dispose()
        {
            // The components live on the stem's GameObject, which StemPlayer destroys.
        }

        static bool CutoffChanged(float applied, float next) =>
            applied < 0f || Mathf.Abs(next - applied) > applied * RelativeCutoffEpsilon;

        static T GetOrAdd<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            return existing != null ? existing : host.AddComponent<T>();
        }
    }
}
