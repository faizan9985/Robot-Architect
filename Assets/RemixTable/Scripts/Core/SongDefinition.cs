using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RemixTable
{
    public enum StemRole
    {
        Drums,
        Bass,
        Keys,
        Lead,
        Vocals,
        FX,
        Other,
    }

    /// <summary>One stem of a song: a mono, looping clip with a role and a display colour.</summary>
    [Serializable]
    public sealed class StemDefinition
    {
        [SerializeField] string name = "Stem";
        [SerializeField] StemRole role = StemRole.Other;
        [Tooltip("Mono, Decompress On Load. Every stem in a song must have exactly the same sample count.")]
        [SerializeField] AudioClip clip;
        [SerializeField] Color color = Color.white;

        public StemDefinition() { }

        public StemDefinition(string name, StemRole role, AudioClip clip, Color color)
        {
            this.name = name;
            this.role = role;
            this.clip = clip;
            this.color = color;
        }

        public string Name => name;
        public StemRole Role => role;
        public AudioClip Clip => clip;
        public Color Color => color;
    }

    /// <summary>A playable song: metadata, credits, musical timing and its stems.</summary>
    [CreateAssetMenu(menuName = "Remix Table/Song Definition", fileName = "NewSong")]
    public sealed class SongDefinition : ScriptableObject
    {
        /// <summary>Clip length may differ from the BPM maths by this much before we warn.</summary>
        public const double LengthToleranceSeconds = 0.005;

        [Header("Credits")]
        [SerializeField] string title = "Untitled";
        [SerializeField] string artist = "Unknown";
        [Tooltip("CC0 or CC BY only (no NC, no ND).")]
        [SerializeField] string licence = "CC BY 4.0";
        [SerializeField] string attributionUrl = "";

        [Header("Timing")]
        [SerializeField, Min(1f)] float bpm = 120f;
        [SerializeField, Min(1)] int beatsPerBar = 4;
        [Tooltip("The stems' loop length. Must be a whole number of bars.")]
        [SerializeField, Min(1)] int loopLengthBars = 8;

        [Header("Stems")]
        [SerializeField] List<StemDefinition> stems = new List<StemDefinition>();

        public string Title => title;
        public string Artist => artist;
        public string Licence => licence;
        public string AttributionUrl => attributionUrl;
        public float Bpm => bpm;
        public int BeatsPerBar => beatsPerBar;
        public int LoopLengthBars => loopLengthBars;
        public IReadOnlyList<StemDefinition> Stems => stems;
        public int StemCount => stems.Count;
        public StemDefinition GetStem(int index) => stems[index];

        /// <summary>Credit line for the in-app credits screen and Devpost.</summary>
        public string Credit => string.IsNullOrEmpty(attributionUrl)
            ? $"\"{title}\" by {artist} ({licence})"
            : $"\"{title}\" by {artist} ({licence}) {attributionUrl}";

        /// <summary>Timing derived from the real clip length, so the beat clock can never drift from the audio
        /// even if the declared BPM is slightly off. Falls back to the declared BPM without clips.</summary>
        public SongTiming ComputeTiming()
        {
            AudioClip first = FirstClip();
            if (first != null && first.frequency > 0 && first.samples > 0)
                return SongTiming.FromLoopLength(first.samples / (double)first.frequency, beatsPerBar, loopLengthBars);
            return SongTiming.FromBpm(bpm, beatsPerBar, loopLengthBars);
        }

        /// <summary>Checks the audio rules from the spec. Returns false only when the song cannot play at all.
        /// <paramref name="warnings"/> lists problems that will hurt sync or spatial audio.</summary>
        public bool TryValidate(out string errors, out string warnings)
        {
            var err = new StringBuilder();
            var warn = new StringBuilder();
            if (stems.Count == 0) err.AppendLine("Song has no stems.");

            AudioClip reference = null;
            string referenceName = null;
            for (int i = 0; i < stems.Count; i++)
            {
                StemDefinition stem = stems[i];
                if (stem == null || stem.Clip == null)
                {
                    err.AppendLine($"Stem {i} has no clip.");
                    continue;
                }

                AudioClip clip = stem.Clip;
                if (clip.channels != 1)
                    warn.AppendLine($"Stem '{stem.Name}' has {clip.channels} channels. Import it with Force To Mono (spatial audio needs mono).");
                if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
                    warn.AppendLine($"Stem '{stem.Name}' is {clip.loadType}. Use Decompress On Load (sample-exact sync and StemAnalyzer need it).");

                if (reference == null)
                {
                    reference = clip;
                    referenceName = stem.Name;
                    continue;
                }

                if (clip.samples != reference.samples)
                    warn.AppendLine($"Stem '{stem.Name}' has {clip.samples} samples but '{referenceName}' has {reference.samples}. The stems will drift apart every loop.");
                if (clip.frequency != reference.frequency)
                    warn.AppendLine($"Stem '{stem.Name}' is {clip.frequency} Hz but '{referenceName}' is {reference.frequency} Hz.");
            }

            if (reference != null && reference.frequency > 0)
            {
                double clipSeconds = reference.samples / (double)reference.frequency;
                double expected = loopLengthBars * beatsPerBar * 60.0 / bpm;
                if (Math.Abs(clipSeconds - expected) > LengthToleranceSeconds)
                {
                    double effectiveBpm = loopLengthBars * beatsPerBar * 60.0 / clipSeconds;
                    warn.AppendLine($"Clips are {clipSeconds:F3} s but {loopLengthBars} bars at {bpm} BPM is {expected:F3} s. " +
                                    $"The beat clock will follow the clips ({effectiveBpm:F2} BPM). Check BPM and loop length.");
                }
            }

            errors = err.ToString();
            warnings = warn.ToString();
            return err.Length == 0;
        }

        AudioClip FirstClip()
        {
            for (int i = 0; i < stems.Count; i++)
                if (stems[i] != null && stems[i].Clip != null) return stems[i].Clip;
            return null;
        }

        /// <summary>Used by editor tooling (test-song generator) and tests.</summary>
        internal void EditorConfigure(string title, string artist, string licence, string attributionUrl,
            float bpm, int beatsPerBar, int loopLengthBars, List<StemDefinition> stems)
        {
            this.title = title;
            this.artist = artist;
            this.licence = licence;
            this.attributionUrl = attributionUrl;
            this.bpm = bpm;
            this.beatsPerBar = beatsPerBar;
            this.loopLengthBars = loopLengthBars;
            this.stems = stems ?? new List<StemDefinition>();
        }
    }
}
