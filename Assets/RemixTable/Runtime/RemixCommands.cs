using System.Text.RegularExpressions;
namespace RemixTable
{
    public enum RemixCommandKind
    {
        None, Mute, Unmute, Solo, Unsolo, ClearStem, ResetAll, UnmuteAll,
        Undo, Redo, Play, Pause, NextSong, PreviousSong,
        VolumeMode, FilterMode, PitchMode, EditMode, LiveMode,
        SnapOn, SnapOff, LoopOn, LoopOff, SpatialOn, SpatialOff, RecordStart, RecordStop, PassthroughOn, PassthroughOff,
        VibeChill, VibeHype, VibeKaraoke, VibeBreakdown, VibeAcapella, VibeNormal
    }
    public struct RemixCommand
    {
        public RemixCommandKind kind;
        public StemId stem;
        public RemixCommand(RemixCommandKind kind, StemId stem = StemId.Drums) { this.kind = kind; this.stem = stem; }
        public bool NeedsStem => kind == RemixCommandKind.Mute || kind == RemixCommandKind.Unmute || kind == RemixCommandKind.Solo ||
                                 kind == RemixCommandKind.Unsolo || kind == RemixCommandKind.ClearStem;
        public override string ToString() => NeedsStem ? $"{kind} {RemixCommands.StemName(stem)}" : kind.ToString();
    }
    // Turns a spoken (or typed) phrase into a Remix Table command, e.g. "mute the drums", "solo vocals",
    // "pitch mode", "undo", "start recording", "turn loop off". Matching is keyword based and forgiving about
    // filler words, so it works with plain dictation (no trained intents needed).
    public static class RemixCommands
    {
        public static string StemName(StemId stem) => stem == StemId.Melody ? "OTHER" : stem.ToString().ToUpperInvariant();
        static bool Has(string text, string pattern) => Regex.IsMatch(text, @"\b(" + pattern + @")\b");
        static bool Off(string text) => Has(text, "off|stop|disable|end|exit|leave|no more|turn off|cancel");
        static bool On(string text) => Has(text, "on|start|enable|turn on|begin");
        public static bool TryStem(string text, out StemId stem)
        {
            stem = StemId.Drums;
            if (Has(text, "vocals?|voices?|vox|singers?|singing|lyrics")) { stem = StemId.Vocals; return true; }
            if (Has(text, "drums?|beats?|percussion|kick|snare|hi ?hats?")) { stem = StemId.Drums; return true; }
            if (Has(text, "bass|base|low end|bassline")) { stem = StemId.Bass; return true; }
            if (Has(text, "other|melody|melodies|instruments?|music|keys|guitars?|synths?|piano")) { stem = StemId.Melody; return true; }
            return false;
        }
        public static RemixCommand Parse(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return default;
            string text = " " + Regex.Replace(phrase.ToLowerInvariant(), @"[^a-z0-9 ]", " ") + " ";
            text = Regex.Replace(text, @"\s+", " ");
            bool stem = TryStem(text, out var id);

            if (Has(text, "unmute|un mute") && Has(text, "all|everything|every stem")) return new RemixCommand(RemixCommandKind.UnmuteAll);
            if (Has(text, "unmute|un mute") && stem) return new RemixCommand(RemixCommandKind.Unmute, id);
            if (Has(text, "unsolo|un solo") && stem) return new RemixCommand(RemixCommandKind.Unsolo, id);
            if (Has(text, "unsolo|un solo")) return new RemixCommand(RemixCommandKind.UnmuteAll);
            if (Has(text, "mute|silence|kill|quiet") && stem) return new RemixCommand(RemixCommandKind.Mute, id);
            if (Has(text, "solo|only|isolate|just") && stem) return new RemixCommand(RemixCommandKind.Solo, id);
            if (Has(text, "reset|clear|start over|wipe") && Has(text, "all|everything|song|remix|over")) return new RemixCommand(RemixCommandKind.ResetAll);
            if (Has(text, "reset|clear|wipe") && stem) return new RemixCommand(RemixCommandKind.ClearStem, id);

            // Vibes: one phrase reshapes the whole mix ("make it chill", "karaoke", "back to normal").
            if (Has(text, "back to normal|bring it back|normal|original mix|original|no vibe|clear the vibe|clear vibe")) return new RemixCommand(RemixCommandKind.VibeNormal);
            if (Has(text, "karaoke|sing along|singalong|instrumental|no vocals")) return new RemixCommand(RemixCommandKind.VibeKaraoke);
            if (Has(text, "acapella|a cappella|a capella|acappella")) return new RemixCommand(RemixCommandKind.VibeAcapella);
            if (Has(text, "breakdown|break it down|strip it down|stripped|strip it")) return new RemixCommand(RemixCommandKind.VibeBreakdown);
            if (Has(text, "chill|chilled|chiller|relax|relaxed|calm|mellow|lofi|lo fi|smooth|laid back")) return new RemixCommand(RemixCommandKind.VibeChill);
            if (Has(text, "hype|hyped|pump it up|pump|hit harder|harder|energy|energetic|banger|party|turn it up")) return new RemixCommand(RemixCommandKind.VibeHype);

            if (Has(text, "undo|take that back|go back a step")) return new RemixCommand(RemixCommandKind.Undo);
            if (Has(text, "redo")) return new RemixCommand(RemixCommandKind.Redo);

            if (Has(text, "record|recording|rec")) return new RemixCommand(Off(text) ? RemixCommandKind.RecordStop : RemixCommandKind.RecordStart);
            if (Has(text, "loop|looping|repeat")) return new RemixCommand(Off(text) ? RemixCommandKind.LoopOff : RemixCommandKind.LoopOn);
            if (Has(text, "snap|snapping|grid|quantize")) return new RemixCommand(Off(text) ? RemixCommandKind.SnapOff : RemixCommandKind.SnapOn);
            if (Has(text, "spatial|surround|3d|three d|around me")) return new RemixCommand(Off(text) ? RemixCommandKind.SpatialOff : RemixCommandKind.SpatialOn);
            if (Has(text, "passthrough|pass through|see my room|mixed reality|my room")) return new RemixCommand(Off(text) ? RemixCommandKind.PassthroughOff : RemixCommandKind.PassthroughOn);

            if (Has(text, "next|skip")) return new RemixCommand(RemixCommandKind.NextSong);
            if (Has(text, "previous|last song|go back|back")) return new RemixCommand(RemixCommandKind.PreviousSong);
            if (Has(text, "pause|stop|hold on|wait")) return new RemixCommand(RemixCommandKind.Pause);
            if (Has(text, "play|resume|continue|go")) return new RemixCommand(RemixCommandKind.Play);

            if (Has(text, "live")) return new RemixCommand(RemixCommandKind.LiveMode);
            if (Has(text, "edit|editing")) return new RemixCommand(RemixCommandKind.EditMode);
            if (Has(text, "pitch|key|tune|transpose")) return new RemixCommand(RemixCommandKind.PitchMode);
            if (Has(text, "filter|muffle|tone")) return new RemixCommand(RemixCommandKind.FilterMode);
            if (Has(text, "volume|level|loudness")) return new RemixCommand(RemixCommandKind.VolumeMode);
            return default;
        }
    }
    // A toolbar toggle implemented outside this assembly (passthrough, voice input). The toolbar only needs
    // its state and a way to flip it.
    public interface IRemixToggle
    {
        bool Available { get; }
        bool IsOn { get; }
        void SetOn(bool on);
    }
}
