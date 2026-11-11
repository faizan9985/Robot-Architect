using UnityEngine;

namespace RemixTable
{
    /// <summary>
    /// Per-stem level and low/mid/high bands for visuals (orb glow, pulses). Reads the stem clip's samples at the
    /// current play position (pre-fader, so it is independent of the spatializer), using preallocated buffers:
    /// no allocations per frame. All getters return smoothed values and 0 for an invalid stem index.
    /// Requires stems imported as Decompress On Load.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [AddComponentMenu("Remix Table/Stem Analyzer")]
    public sealed class StemAnalyzer : MonoBehaviour
    {
        [SerializeField] StemPlayer player;
        [Tooltip("Samples analysed per frame (ending at the play position).")]
        [SerializeField, Range(128, 4096)] int windowSamples = 1024;
        [SerializeField] float lowBandHz = 250f;
        [SerializeField] float highBandHz = 4000f;
        [SerializeField, Min(0f)] float attackSeconds = 0.02f;
        [SerializeField, Min(0f)] float releaseSeconds = 0.18f;
        [Tooltip("Multiplier that maps RMS (a full-scale sine is 0.71) to the 0..1 getters.")]
        [SerializeField, Min(0f)] float levelScale = 3f;

        float[][] buffers;
        int[] channels;
        float[] lowCoefficients;
        float[] highCoefficients;
        bool[] readable;
        BandLevels[] levels;

        public int StemCount => levels != null ? levels.Length : 0;

        /// <summary>Raw smoothed RMS values (not scaled).</summary>
        public BandLevels GetBands(int stem) => Valid(stem) ? levels[stem] : default;
        public float GetRms(int stem) => Valid(stem) ? levels[stem].rms : 0f;
        public float GetLevel01(int stem) => Valid(stem) ? Mathf.Clamp01(levels[stem].rms * levelScale) : 0f;
        public float GetLow01(int stem) => Valid(stem) ? Mathf.Clamp01(levels[stem].low * levelScale) : 0f;
        public float GetMid01(int stem) => Valid(stem) ? Mathf.Clamp01(levels[stem].mid * levelScale) : 0f;
        public float GetHigh01(int stem) => Valid(stem) ? Mathf.Clamp01(levels[stem].high * levelScale) : 0f;

        /// <summary>Level after mute, solo, ducking and distance gain: what the player actually hears from that stem
        /// (before spatialization).</summary>
        public float GetPostFaderLevel01(int stem) =>
            Valid(stem) && player != null ? Mathf.Clamp01(levels[stem].rms * levelScale * player.GetAppliedGain(stem)) : 0f;

        void Start()
        {
            if (player == null) player = GetComponent<StemPlayer>();
            if (player == null) player = FindFirstObjectByType<StemPlayer>();
            if (player == null)
            {
                RemixLog.Warn("StemAnalyzer has no StemPlayer.", this);
                enabled = false;
                return;
            }

            player.SongLoaded += Bind;
            Bind();
        }

        void OnDestroy()
        {
            if (player != null) player.SongLoaded -= Bind;
        }

        void Bind()
        {
            int count = player.IsLoaded ? player.StemCount : 0;
            buffers = new float[count][];
            channels = new int[count];
            lowCoefficients = new float[count];
            highCoefficients = new float[count];
            readable = new bool[count];
            levels = new BandLevels[count];

            for (int i = 0; i < count; i++)
            {
                AudioClip clip = player.GetSource(i).clip;
                readable[i] = clip != null && clip.loadType == AudioClipLoadType.DecompressOnLoad && clip.samples > windowSamples;
                channels[i] = clip != null ? Mathf.Max(1, clip.channels) : 1;
                buffers[i] = new float[windowSamples * channels[i]];
                int rate = clip != null ? clip.frequency : 48000;
                lowCoefficients[i] = BandAnalysis.OnePoleCoefficient(lowBandHz, rate);
                highCoefficients[i] = BandAnalysis.OnePoleCoefficient(highBandHz, rate);
                if (clip != null && !readable[i])
                    RemixLog.Warn($"StemAnalyzer can't read stem {i} ('{clip.name}', {clip.loadType}). Import it as Decompress On Load.", this);
            }
        }

        void Update()
        {
            if (levels == null || player == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool live = player.IsPlaying && player.Clock != null && player.Clock.HasReachedStart;

            for (int i = 0; i < levels.Length; i++)
            {
                BandLevels target = default;
                if (live && readable[i])
                {
                    AudioSource source = player.GetSource(i);
                    AudioClip clip = source.clip;
                    if (source.isPlaying && clip != null)
                    {
                        int length = clip.samples;
                        int offset = source.timeSamples - windowSamples;
                        if (offset < 0) offset += length;
                        // GetData wraps around the end of the clip, which is what a loop needs.
                        if (clip.GetData(buffers[i], offset))
                            target = BandAnalysis.Analyze(buffers[i], channels[i], lowCoefficients[i], highCoefficients[i]);
                    }
                }

                BandLevels current = levels[i];
                current.rms = Smoothing.Follow(current.rms, target.rms, dt, attackSeconds, releaseSeconds);
                current.low = Smoothing.Follow(current.low, target.low, dt, attackSeconds, releaseSeconds);
                current.mid = Smoothing.Follow(current.mid, target.mid, dt, attackSeconds, releaseSeconds);
                current.high = Smoothing.Follow(current.high, target.high, dt, attackSeconds, releaseSeconds);
                levels[i] = current;
            }
        }

        bool Valid(int stem) => levels != null && (uint)stem < (uint)levels.Length;

        internal void EditorConfigure(StemPlayer stemPlayer) => player = stemPlayer;
    }
}
