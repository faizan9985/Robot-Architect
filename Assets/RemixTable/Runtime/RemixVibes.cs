using System;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    public enum RemixVibe { Normal, Chill, Hype, Karaoke, Breakdown, Acapella }

    // VIBES: one tap (or one phrase: "make it chill", "karaoke", "back to normal") reshapes the whole mix.
    // A vibe is a set of whole-stem edits tagged "vibe-" plus mute / solo, applied as one undoable step.
    // Vibes replace each other rather than stacking; NORMAL removes them and gives back the user's own mix.
    public sealed class RemixVibes : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixToolbox toolbox;
        public Button chillButton, hypeButton, karaokeButton, breakdownButton, normalButton;
        public Color idleColor = new Color(.07f, .11f, .15f, 1), selectedColor = new Color(.13f, .42f, .62f, 1);
        public const string IdPrefix = "vibe-";
        public RemixVibe Current { get; private set; }
        public event Action<RemixVibe> Applied;
        RemixVibe? shown;

        public void Chill() => Apply(RemixVibe.Chill);
        public void Hype() => Apply(RemixVibe.Hype);
        public void Karaoke() => Apply(RemixVibe.Karaoke);
        public void Breakdown() => Apply(RemixVibe.Breakdown);
        public void Acapella() => Apply(RemixVibe.Acapella);
        public void Normal() => Apply(RemixVibe.Normal);

        public static string Label(RemixVibe vibe) => vibe switch
        {
            RemixVibe.Chill => "VIBE: CHILL  (DRUMS BACK, MELODY MUFFLED)",
            RemixVibe.Hype => "VIBE: HYPE  (DRUMS + BASS UP)",
            RemixVibe.Karaoke => "VIBE: KARAOKE  (VOCALS OUT, SING ALONG)",
            RemixVibe.Breakdown => "VIBE: BREAKDOWN  (DRUMS + BASS OUT)",
            RemixVibe.Acapella => "VIBE: A CAPPELLA  (JUST THE VOCALS)",
            _ => "BACK TO YOUR MIX",
        };

        public bool Apply(RemixVibe vibe)
        {
            if (manager == null || manager.library == null || manager.library.songs == null || manager.library.songs.Length == 0) return false;
            string song = manager.library.Current.id;
            if (manager.interactions != null) manager.interactions.Cancel();
            manager.history.BeginGesture(manager.automation, true);
            manager.automation.edits.RemoveAll(e => e.songId == song && e.id != null && e.id.StartsWith(IdPrefix, StringComparison.Ordinal));
            switch (vibe)
            {
                case RemixVibe.Chill:
                    Edit(song, StemId.Drums, ParameterType.Gain, .55f);
                    Edit(song, StemId.Bass, ParameterType.Gain, .85f);
                    Edit(song, StemId.Melody, ParameterType.Effect, -.55f); // low-pass: soft, lo-fi
                    Edit(song, StemId.Drums, ParameterType.Effect, -.35f);
                    break;
                case RemixVibe.Hype:
                    Edit(song, StemId.Drums, ParameterType.Gain, 1.5f);
                    Edit(song, StemId.Bass, ParameterType.Gain, 1.35f);
                    Edit(song, StemId.Melody, ParameterType.Gain, 1.1f);
                    break;
            }
            // Vibes own the mute / solo buttons while active.
            var mix = manager.mix;
            mix.ClearMuteSolo();
            if (vibe == RemixVibe.Karaoke) mix.SetMuted(StemId.Vocals, true);
            if (vibe == RemixVibe.Breakdown) { mix.SetMuted(StemId.Drums, true); mix.SetMuted(StemId.Bass, true); }
            if (vibe == RemixVibe.Acapella) mix.SetSoloed(StemId.Vocals, true);
            manager.history.EndGesture(manager.automation);
            Current = vibe;
            if (toolbox != null) toolbox.Say(Label(vibe), 3);
            Applied?.Invoke(vibe);
            Paint();
            return true;
        }

        void Edit(string song, StemId stem, ParameterType parameter, float value) =>
            manager.automation.Upsert(new AutomationEdit { id = $"{IdPrefix}{song}-{stem}-{parameter}", songId = song, stem = stem, parameter = parameter, wholeStem = true, value = value });

        void OnEnable() { if (manager != null) manager.SongReady += SongReady; shown = null; Paint(); }
        void OnDisable() { if (manager != null) manager.SongReady -= SongReady; }
        void SongReady(SongMetadata _) { Current = RemixVibe.Normal; Paint(); }
        void Update() { if (shown != Current) Paint(); }
        public void Paint()
        {
            shown = Current;
            Tint(chillButton, Current == RemixVibe.Chill); Tint(hypeButton, Current == RemixVibe.Hype);
            Tint(karaokeButton, Current == RemixVibe.Karaoke); Tint(breakdownButton, Current == RemixVibe.Breakdown);
            Tint(normalButton, false);
        }
        void Tint(Button b, bool on) { if (b != null && b.targetGraphic != null) b.targetGraphic.color = on ? selectedColor : idleColor; }
    }
}
