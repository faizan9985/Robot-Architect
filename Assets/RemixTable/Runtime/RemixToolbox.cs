using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // The TOOLS row (SNAP / LOOP / SPATIAL / REC / ROOM / MIC / RESET ALL) and the single place every command
    // runs through, so buttons and voice commands behave identically.
    public sealed class RemixToolbox : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixModeSelector modeSelector;
        public RemixPlaybackModeSelector playbackSelector;
        public RemixTransport transport;
        public SpatialStemField spatialField;
        public RemixRecorder recorder;                  // Found or added on the AudioListener when first used.
        public RemixVibes vibes;
        public RemixOnboarding onboarding;
        [Tooltip("An IRemixToggle for passthrough (see Assets/Scripts/RemixPassthroughToggle).")]
        public MonoBehaviour passthrough;
        [Tooltip("An IRemixToggle for voice commands (see RemixTable.Voice).")]
        public MonoBehaviour voice;
        public Button snapButton, loopButton, spatialButton, recordButton, passthroughButton, voiceButton, resetButton;
        public TMP_Text recordLabel, voiceLabel, status;
        public Color idleColor = new Color(.07f, .11f, .15f, 1), selectedColor = new Color(.13f, .42f, .62f, 1), recordingColor = new Color(.75f, .12f, .12f, 1);
        // Short-lived feedback line ("MUTED DRUMS", "LOOP: PINCH A REGION FIRST", "SAVED remix-....wav").
        string message; float messageUntil;
        public string Message => Time.unscaledTime < messageUntil ? message : "";
        IRemixToggle Passthrough => passthrough as IRemixToggle;
        IRemixToggle Voice => voice as IRemixToggle;

        public void Say(string text, float seconds = 2.5f) { message = text; messageUntil = Time.unscaledTime + seconds; }

        // ---- Buttons --------------------------------------------------------------------------------------
        public void ToggleSnap() => Run(new RemixCommand(manager.snapToBeat ? RemixCommandKind.SnapOff : RemixCommandKind.SnapOn));
        public void ToggleLoop() => Run(new RemixCommand(manager.audioManager != null && manager.audioManager.LoopActive ? RemixCommandKind.LoopOff : RemixCommandKind.LoopOn));
        public void ToggleSpatial() => Run(new RemixCommand(manager.mix.spatial ? RemixCommandKind.SpatialOff : RemixCommandKind.SpatialOn));
        public void ToggleRecord() => Run(new RemixCommand(recorder != null && recorder.Recording ? RemixCommandKind.RecordStop : RemixCommandKind.RecordStart));
        public void TogglePassthrough() => Run(new RemixCommand(Passthrough != null && Passthrough.IsOn ? RemixCommandKind.PassthroughOff : RemixCommandKind.PassthroughOn));
        public void ToggleVoice() { var v = Voice; if (v == null || !v.Available) { Say("VOICE NEEDS A WIT.AI APP (SEE README)"); return; } v.SetOn(!v.IsOn); }
        public void ResetAll() => Run(new RemixCommand(RemixCommandKind.ResetAll));

        // Voice entry point: returns what was understood, or null.
        public string RunPhrase(string phrase)
        {
            var command = RemixCommands.Parse(phrase);
            if (command.kind == RemixCommandKind.None) { Say($"DIDN'T CATCH \"{phrase.Trim().ToUpperInvariant()}\""); return null; }
            Run(command);
            return command.ToString();
        }

        public bool Run(RemixCommand c)
        {
            if (manager == null) return false;
            var audio = manager.audioManager; var mix = manager.mix; string stem = RemixCommands.StemName(c.stem);
            switch (c.kind)
            {
                case RemixCommandKind.Mute: mix.SetMuted(c.stem, true); Say("MUTED " + stem); return true;
                case RemixCommandKind.Unmute: mix.SetMuted(c.stem, false); Say("UNMUTED " + stem); return true;
                case RemixCommandKind.Solo: mix.SetSoloed(c.stem, true); Say("SOLO " + stem); return true;
                case RemixCommandKind.Unsolo: mix.SetSoloed(c.stem, false); Say("UNSOLO " + stem); return true;
                case RemixCommandKind.UnmuteAll: mix.ClearMuteSolo(); Say("ALL STEMS ON"); return true;
                case RemixCommandKind.ClearStem: manager.ResetStem(c.stem); Say("CLEARED " + stem); return true;
                case RemixCommandKind.ResetAll:
                    manager.ResetSong(); if (audio != null) audio.ClearLoop();
                    if (manager.interactions != null) manager.interactions.ForgetLastRegion();
                    Say("REMIX RESET  (UNDO TO RESTORE)"); return true;
                case RemixCommandKind.Undo: if (manager.interactions != null) manager.interactions.Cancel(); return manager.history.Undo(manager.automation);
                case RemixCommandKind.Redo: if (manager.interactions != null) manager.interactions.Cancel(); return manager.history.Redo(manager.automation);
                case RemixCommandKind.Play: if (audio == null || !audio.Ready) return false; audio.Resume(); return true;
                case RemixCommandKind.Pause: if (audio == null || !audio.Ready) return false; audio.Pause(); return true;
                case RemixCommandKind.NextSong: if (transport != null) transport.NextSong(); else manager.library.Select(manager.library.selectedIndex + 1); return true;
                case RemixCommandKind.PreviousSong: if (transport != null) transport.PreviousSong(); else manager.library.Select(manager.library.selectedIndex - 1); return true;
                case RemixCommandKind.VolumeMode: return SetMode(RibbonInteractionManager.ControlMode.Volume);
                case RemixCommandKind.FilterMode: return SetMode(RibbonInteractionManager.ControlMode.Filter);
                case RemixCommandKind.PitchMode: return SetMode(RibbonInteractionManager.ControlMode.Pitch);
                case RemixCommandKind.EditMode: if (audio == null) return false; audio.LiveMode = false; return true;
                case RemixCommandKind.LiveMode: if (audio == null) return false; audio.LiveMode = true; return true;
                case RemixCommandKind.SnapOn:
                    if (!BeatGrid.Valid(manager.library.Current.bpm)) { Say("THIS SONG HAS NO TEMPO SET"); return false; }
                    manager.snapToBeat = true; Say($"SNAP TO BEAT  ({Mathf.RoundToInt(manager.library.Current.bpm)} BPM)"); return true;
                case RemixCommandKind.SnapOff: manager.snapToBeat = false; return true;
                case RemixCommandKind.LoopOn: return StartLoop();
                case RemixCommandKind.LoopOff: if (audio != null) audio.ClearLoop(); return true;
                case RemixCommandKind.SpatialOn: mix.SetSpatial(true); Say("GRAB THE ORBS TO MOVE STEMS AROUND YOU"); return true;
                case RemixCommandKind.SpatialOff: mix.SetSpatial(false); return true;
                case RemixCommandKind.RecordStart:
                    if (!EnsureRecorder()) { Say("NO AUDIO LISTENER TO RECORD"); return false; }
                    if (!recorder.StartRecording()) { Say("RECORDING COULD NOT START"); return false; }
                    return true;
                case RemixCommandKind.RecordStop:
                    if (recorder == null || !recorder.Recording) return false;
                    var file = recorder.StopRecording(); Say("SAVED " + System.IO.Path.GetFileName(file), 5); return true;
                case RemixCommandKind.Tutorial: if (onboarding == null) return false; onboarding.Restart(); return true;
                case RemixCommandKind.VibeChill: return Vibe(RemixVibe.Chill);
                case RemixCommandKind.VibeHype: return Vibe(RemixVibe.Hype);
                case RemixCommandKind.VibeKaraoke: return Vibe(RemixVibe.Karaoke);
                case RemixCommandKind.VibeBreakdown: return Vibe(RemixVibe.Breakdown);
                case RemixCommandKind.VibeAcapella: return Vibe(RemixVibe.Acapella);
                case RemixCommandKind.VibeNormal: return Vibe(RemixVibe.Normal);
                case RemixCommandKind.PassthroughOn:
                case RemixCommandKind.PassthroughOff:
                    var p = Passthrough;
                    if (p == null || !p.Available) { Say("PASSTHROUGH NEEDS A QUEST HEADSET"); return false; }
                    p.SetOn(c.kind == RemixCommandKind.PassthroughOn); return true;
            }
            return false;
        }
        bool Vibe(RemixVibe vibe)
        {
            if (vibes == null) vibes = GetComponent<RemixVibes>();
            if (vibes == null) { vibes = gameObject.AddComponent<RemixVibes>(); vibes.manager = manager; vibes.toolbox = this; }
            return vibes.Apply(vibe);
        }
        bool SetMode(RibbonInteractionManager.ControlMode mode)
        {
            if (modeSelector != null) modeSelector.Select(mode);
            else if (manager.interactions != null) manager.interactions.Mode = mode;
            else return false;
            return true;
        }
        // LOOP plays the last region you edited. Without one it loops the bar (4 beats) or 4 s around the playhead.
        bool StartLoop()
        {
            var audio = manager.audioManager;
            if (audio == null || !audio.Ready) { Say("NO AUDIO TO LOOP"); return false; }
            var gestures = manager.interactions; var song = manager.library.Current;
            float start, end;
            if (gestures != null && gestures.HasLastRegion) { start = gestures.LastRegionStart; end = gestures.LastRegionEnd; }
            else
            {
                float t = manager.clock.Time;
                float length = BeatGrid.Valid(song.bpm) ? BeatGrid.BeatLength(song.bpm) * BeatGrid.BeatsPerBar : 4;
                start = BeatGrid.Valid(song.bpm) ? BeatGrid.Floor(t, song.bpm, song.beatOffset, manager.clock.duration) : t;
                end = start + length;
            }
            // A one-hand region is under a second; stretch short loops to a full bar so they are usable.
            float bar = BeatGrid.Valid(song.bpm) ? BeatGrid.BeatLength(song.bpm) * BeatGrid.BeatsPerBar : 2;
            if (AutomationStore.RegionLength(start, Mathf.Repeat(end, manager.clock.duration), manager.clock.duration) < 1) end = start + bar;
            if (!audio.SetLoop(start, end)) { Say("COULD NOT LOOP THIS REGION"); return false; }
            Say("LOOPING  (PRESS LOOP AGAIN TO STOP)");
            return true;
        }
        public bool EnsureRecorder()
        {
            if (recorder != null) return true;
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null) return false;
            recorder = listener.GetComponent<RemixRecorder>();
            if (recorder == null) recorder = listener.gameObject.AddComponent<RemixRecorder>();
            return true;
        }

        // ---- Painting -------------------------------------------------------------------------------------
        void Start() => EnsureRecorder(); // The recorder learns the output channel count before REC is pressed.
        void Update() => Paint();
        public void Paint()
        {
            if (manager == null) return;
            var audio = manager.audioManager;
            Paint(snapButton, manager.snapToBeat);
            Paint(loopButton, audio != null && audio.LoopActive);
            Paint(spatialButton, manager.mix.spatial);
            bool recording = recorder != null && recorder.Recording;
            if (recordButton != null && recordButton.targetGraphic != null) recordButton.targetGraphic.color = recording ? recordingColor : idleColor;
            if (recordLabel != null)
            {
                int s = recording ? Mathf.FloorToInt(recorder.Seconds) : 0;
                string text = recording ? $"STOP {s / 60}:{s % 60:00}" : "REC";
                if (recordLabel.text != text) recordLabel.text = text;
            }
            var p = Passthrough; Paint(passthroughButton, p != null && p.IsOn);
            var v = Voice; Paint(voiceButton, v != null && v.IsOn);
            if (voiceLabel != null) { string text = v != null && v.IsOn ? "LISTENING" : "MIC"; if (voiceLabel.text != text) voiceLabel.text = text; }
            if (status != null) { string text = Message; if (status.text != text) status.text = text; }
        }
        void Paint(Button button, bool on)
        {
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = on ? selectedColor : idleColor;
        }
    }
}
