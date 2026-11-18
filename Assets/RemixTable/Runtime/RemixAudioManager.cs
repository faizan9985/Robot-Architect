using System;
using System.Collections.Generic;
using UnityEngine;
namespace RemixTable
{
    public class RemixAudioManager : MonoBehaviour
    {
        public RemixTableManager manager;
        public AudioSource[] sources;
        [Range(0, 1)] public float headroom = .06f;
        public double ScheduledStart { get; private set; }
        public bool Ready { get; private set; }
        double origin, offset;
        bool running, manualPaused, suspended;
        readonly HashSet<Behaviour> editingOwners = new HashSet<Behaviour>();
        public int EditingPauseCount => editingOwners.Count;
        public bool IsManuallyPaused => manualPaused;
        // Main menu is open: the song stays paused underneath it (a song picked from the menu loads paused too).
        // Independent of the PAUSE button, so closing the menu restores whatever the listener had chosen.
        bool menuHold;
        public bool MenuHold
        {
            get => menuHold;
            set { if (menuHold == value) return; menuHold = value; ApplyPlaybackState(); }
        }
        // EDIT (default): holding a ribbon pauses the song while you shape it.
        // LIVE: playback keeps running while you edit, so changes are heard as the song plays.
        [SerializeField] bool liveMode;
        public bool LiveMode
        {
            get => liveMode;
            set { if (liveMode == value) return; liveMode = value; ApplyPlaybackState(); }
        }
        float SafeHeadroom => Mathf.Clamp(AutomationStore.SafeGain(headroom),0,.06f);

        // ---- LOOP ----------------------------------------------------------------------------------------
        // A loop plays [LoopStart, LoopEnd] over and over without a gap. Each stem has a second "twin" source
        // (created the first time a loop is used): just before the loop end the idle set is scheduled to start
        // at the loop start on the exact DSP tick where the playing set is scheduled to stop, then they swap.
        public const float MinimumLoopSeconds = .5f, LoopScheduleLead = .15f;
        public bool LoopActive { get; private set; }
        public float LoopStart { get; private set; }
        public float LoopEnd { get; private set; }
        public int Version { get; private set; } // Bumped when the loop changes, for UI.
        AudioSource[] twins;
        int activeSet;                 // 0 = sources, 1 = twins.
        bool loopPending;
        double loopSwitchDsp;
        AudioSource Active(int i) => activeSet == 0 || twins == null ? sources[i] : twins[i];
        AudioSource Idle(int i) => activeSet == 0 ? twins[i] : sources[i];
        public AudioSource ActiveSource(StemId stem) => Active((int)stem);
        public bool SetLoop(float start, float end)
        {
            if (manager == null || sources == null || sources.Length != 4) return false;
            float duration = manager.clock.duration;
            if (duration <= MinimumLoopSeconds * 2 || float.IsNaN(start) || float.IsNaN(end)) return false;
            start = Mathf.Repeat(start, duration); end = Mathf.Repeat(end, duration);
            if (AutomationStore.RegionLength(start, end, duration) < MinimumLoopSeconds) end = Mathf.Repeat(start + MinimumLoopSeconds, duration);
            EnsureTwins();
            LoopStart = start; LoopEnd = end; LoopActive = true; Version++;
            CancelPendingLoop();
            if (!InLoop(CurrentTime())) Seek(LoopStart);
            return true;
        }
        public void ClearLoop()
        {
            if (!LoopActive && !loopPending) return;
            LoopActive = false; Version++;
            CancelPendingLoop();
        }
        public bool InLoop(float time) => LoopStart <= LoopEnd ? time >= LoopStart - .0005f && time <= LoopEnd + .0005f : time >= LoopStart - .0005f || time <= LoopEnd + .0005f;
        float CurrentTime() => Mathf.Repeat((float)Position(AudioSettings.dspTime), Mathf.Max(.01f, manager.clock.duration));
        // Jump the playhead. While playing this restarts playback at `time` after a short scheduling lead.
        public void Seek(float time)
        {
            if (!Ready) return;
            time = Mathf.Repeat(time, Mathf.Max(.01f, manager.clock.duration));
            bool wasRunning = running;
            if (wasRunning) PauseTransport();
            offset = time; manager.clock.elapsed = offset;
            if (wasRunning) ResumeTransport();
        }
        void EnsureTwins()
        {
            if (twins != null && twins.Length == sources.Length) return;
            twins = new AudioSource[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                var existing = source.transform.Find("LoopTwin");
                var go = existing != null ? existing.gameObject : new GameObject("LoopTwin");
                go.transform.SetParent(source.transform, false);
                var twin = go.GetComponent<AudioSource>(); if (twin == null) twin = go.AddComponent<AudioSource>();
                twin.playOnAwake = false; twin.loop = true; twin.clip = source.clip;
                twin.spatialBlend = source.spatialBlend; twin.volume = source.volume; twin.panStereo = source.panStereo;
                twin.outputAudioMixerGroup = source.outputAudioMixerGroup; twin.priority = source.priority; twin.pitch = 1;
                twins[i] = twin;
            }
            lowpass = null; EnsureFilters(); // Give the twins their own filter chain.
        }
        void CancelPendingLoop()
        {
            CommitPendingLoop(AudioSettings.dspTime); // A swap that already happened is kept, not undone.
            if (!loopPending) return;
            loopPending = false;
            // The idle set was scheduled to start and the active set to stop: undo both.
            for (int i = 0; i < sources.Length; i++)
            {
                Idle(i).Stop();
                if (running) Active(i).SetScheduledEndTime(AudioSettings.dspTime + 3600 * 24);
            }
        }
        void CommitPendingLoop(double dsp)
        {
            if (!loopPending || dsp < loopSwitchDsp) return;
            loopPending = false;
            offset = LoopStart; origin = loopSwitchDsp;
            activeSet = 1 - activeSet;
        }
        void ScheduleLoop(double dsp)
        {
            if (!LoopActive || !running || loopPending || twins == null) return;
            float duration = manager.clock.duration;
            float time = Mathf.Repeat((float)Position(dsp), duration);
            if (!InLoop(time)) { Seek(LoopStart); return; } // Missed the end (e.g. a long frame): catch up.
            double remaining = Mathf.Repeat(LoopEnd - time, duration);
            if (remaining > LoopScheduleLead) return;
            loopSwitchDsp = dsp + remaining;
            if (loopSwitchDsp < AudioSettings.dspTime + .005) return; // Too late to schedule cleanly; next frame catches up.
            int sample = (int)(LoopStart * sources[0].clip.frequency);
            for (int i = 0; i < sources.Length; i++)
            {
                var idle = Idle(i); var active = Active(i);
                idle.volume = active.volume; idle.panStereo = active.panStereo;
                idle.timeSamples = Math.Min(sample, idle.clip.samples - 1);
                idle.PlayScheduled(loopSwitchDsp);
                active.SetScheduledEndTime(loopSwitchDsp);
            }
            loopPending = true;
        }

        public void BeginEditing(Behaviour owner)
        {
            if (owner == null || !owner.isActiveAndEnabled) return;
            // Capture legacy clock pause requests before taking ownership.
            if (running && !manager.clock.playing) manualPaused = true;
            if (editingOwners.Add(owner)) ApplyPlaybackState();
        }
        public void EndEditing(Behaviour owner)
        {
            if (editingOwners.Remove(owner)) ApplyPlaybackState();
        }
        void ApplyPlaybackState()
        {
            if (manualPaused || suspended || menuHold || (!liveMode && editingOwners.Count > 0)) PauseTransport();
            else ResumeTransport();
        }
        readonly float[] samples = new float[128];
        readonly float[] levels = new float[4];
        readonly float[] effects = new float[4];
        readonly float[] pans = new float[4];
        AudioLowPassFilter[] lowpass;
        AudioHighPassFilter[] highpass;
        StemPitchShifter[] pitch;
        public float Level(StemId stem) => levels[(int)stem];
        public float Effect(StemId stem) => effects[(int)stem];
        public AudioLowPassFilter Lowpass(StemId stem) { EnsureFilters(); return lowpass[(int)stem]; }
        public AudioHighPassFilter Highpass(StemId stem) { EnsureFilters(); return highpass[(int)stem]; }
        public StemPitchShifter Pitch(StemId stem) { EnsureFilters(); return pitch[(int)stem]; }
        int ChainCount => sources.Length * (twins != null ? 2 : 1);
        AudioSource Chain(int k) => k < sources.Length ? sources[k] : twins[k - sources.Length];
        // Filters live on each stem source's GameObject so they only process that stem. Index k covers the
        // primary sources first, then the loop twins.
        void EnsureFilters()
        {
            if (lowpass != null && lowpass.Length == ChainCount) return;
            int n = ChainCount;
            lowpass = new AudioLowPassFilter[n];
            highpass = new AudioHighPassFilter[n];
            pitch = new StemPitchShifter[n];
            for (int k = 0; k < n; k++)
            {
                var go = Chain(k).gameObject;
                lowpass[k] = go.GetComponent<AudioLowPassFilter>();
                if (lowpass[k] == null) lowpass[k] = go.AddComponent<AudioLowPassFilter>();
                highpass[k] = go.GetComponent<AudioHighPassFilter>();
                if (highpass[k] == null) highpass[k] = go.AddComponent<AudioHighPassFilter>();
                pitch[k] = go.GetComponent<StemPitchShifter>();
                if (pitch[k] == null) pitch[k] = go.AddComponent<StemPitchShifter>();
            }
            for (int i = 0; i < sources.Length; i++) ApplyFilter(i, effects[i]);
        }
        void ApplyFilter(int i, float effect)
        {
            effects[i] = effect;
            for (int k = i; k < lowpass.Length; k += sources.Length)
            {
                lowpass[k].cutoffFrequency = StemFilter.LowpassCutoff(effect);
                lowpass[k].lowpassResonanceQ = effect < 0 ? StemFilter.Resonance(effect) : StemFilter.BaseResonance;
                highpass[k].cutoffFrequency = StemFilter.HighpassCutoff(effect);
                highpass[k].highpassResonanceQ = effect > 0 ? StemFilter.Resonance(effect) : StemFilter.BaseResonance;
            }
        }
        void ApplyPitch(int i, float semitones) { for (int k = i; k < pitch.Length; k += sources.Length) pitch[k].Semitones = semitones; }
        public void Load(SongMetadata song)
        {
            Stop();
            if (sources == null || sources.Length != 4 || song.stems == null) return;
            AudioClip first = null;
            for (int i = 0; i < 4; i++)
            {
                AudioClip clip = null;
                foreach (var stem in song.stems) if ((int)stem.id == i) clip = stem.clip;
                if (clip == null) return;
                if (first != null && (clip.samples != first.samples || clip.frequency != first.frequency))
                { Debug.LogError("Remix stems must have identical sample counts and rates."); return; }
                first = clip;
                foreach (var source in twins == null ? new[] { sources[i] } : new[] { sources[i], twins[i] })
                {
                    source.clip = clip;
                    source.playOnAwake = false;
                    source.loop = true;
                    source.spatialBlend = 0;
                    source.pitch = 1;
                    source.panStereo = 0;
                    source.volume = SafeHeadroom;
                }
            }
            activeSet = 0;
            manager.clock.Reset((float)((double)first.samples / first.frequency));
            EnsureFilters();
            for (int i = 0; i < sources.Length; i++) { ApplyFilter(i, 0); ApplyPitch(i, 0); pans[i] = 0; }
            Ready = true;
            Resume();
        }
        public void Pause()
        {
            manualPaused = true;
            ApplyPlaybackState();
        }
        void PauseTransport()
        {
            if (!running) return;
            double dsp = AudioSettings.dspTime;
            CommitPendingLoop(dsp);
            offset = Position(dsp);
            running = false;
            loopPending = false;
            foreach (var source in sources) source.Stop();
            if (twins != null) foreach (var twin in twins) twin.Stop();
            manager.clock.elapsed = offset;
            manager.clock.playing = false;
        }
        public void Resume()
        {
            manualPaused = false;
            ApplyPlaybackState();
        }
        void ResumeTransport()
        {
            if (!Ready || running) return;
            ScheduledStart = AudioSettings.dspTime + .2;
            origin = ScheduledStart;
            int sample = (int)((offset % manager.clock.duration) * sources[0].clip.frequency);
            for (int i = 0; i < sources.Length; i++)
            {
                var source = Active(i);
                source.timeSamples = Math.Min(sample, source.clip.samples - 1);
                source.PlayScheduled(ScheduledStart);
            }
            running = true;
            manager.clock.playing = true;
        }
        double Position(double dsp)
        {
            if (loopPending && dsp >= loopSwitchDsp) return LoopStart + (dsp - loopSwitchDsp);
            return offset + (running ? Math.Max(0, dsp - origin) : 0);
        }
        public void Stop()
        {
            if (sources != null) foreach (var source in sources) if (source != null) { source.Stop(); source.clip = null; }
            if (twins != null) foreach (var twin in twins) if (twin != null) { twin.Stop(); twin.clip = null; }
            running = Ready = false; offset = 0; loopPending = false; activeSet = 0;
            editingOwners.Clear(); manualPaused = false;
            Array.Clear(levels, 0, levels.Length);
            if (manager != null) { manager.clock.elapsed = 0; manager.clock.playing = false; }
        }
        void Update()
        {
            if (!Ready) return;
            if (editingOwners.RemoveWhere(owner => owner == null || !owner.isActiveAndEnabled) > 0) ApplyPlaybackState();
            if (manager.clock.playing != running)
            {
                manualPaused = !manager.clock.playing;
                ApplyPlaybackState();
                manager.clock.playing = running;
            }
            double dsp = AudioSettings.dspTime;
            CommitPendingLoop(dsp);
            ScheduleLoop(dsp);
            manager.clock.elapsed = Position(dsp);
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime / (manager.design != null ? Mathf.Max(.001f,manager.design.gainSmoothingSeconds) : .025f));
            var mix = manager.mix;
            Transform head = mix.spatial && Camera.main != null ? Camera.main.transform : null;
            string song = manager.library.Current.id;
            for (int i = 0; i < sources.Length; i++)
            {
                var stem = (StemId)i;
                float gain = manager.automation.EvaluateGain(song, stem, manager.clock.Time,manager.clock.duration,
                    manager.design != null ? manager.design.gainBoundaryFade : .06f);
                gain *= mix.Factor(stem);
                float pan = 0;
                if (head != null && manager.workspace != null && manager.design != null)
                {
                    StemMix.Spatialize(head.position, head.forward, manager.SpatialWorld(mix.positions[i]), out pan, out float distanceGain);
                    gain *= distanceGain;
                }
                pans[i] = Mathf.Lerp(pans[i], pan, blend);
                // Preserve the measured source-peak mix bound, even if Inspector headroom is raised.
                float volume = Mathf.Lerp(sources[i].volume, SafeHeadroom * gain, blend);
                sources[i].volume = volume; sources[i].panStereo = pans[i];
                if (twins != null) { twins[i].volume = volume; twins[i].panStereo = pans[i]; }
                float effect = StemFilter.SafeEffect(manager.automation.Evaluate(song, stem, ParameterType.Effect, manager.clock.Time, manager.clock.duration));
                ApplyFilter(i, Mathf.Lerp(effects[i], effect, blend));
                ApplyPitch(i, manager.PitchAt(stem, manager.clock.Time));
                float sum = 0;
                if (running && AudioSettings.dspTime >= ScheduledStart)
                {
                    Active(i).GetOutputData(samples, 0);
                    for (int j = 0; j < samples.Length; j++) sum += samples[j] * samples[j];
                }
                levels[i] = Mathf.Lerp(levels[i], Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * 12), blend);
            }
        }
        void OnApplicationPause(bool paused) { suspended = paused; ApplyPlaybackState(); }
        void OnDisable() => Stop();
    }
}
