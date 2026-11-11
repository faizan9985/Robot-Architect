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
        float SafeHeadroom => Mathf.Clamp(AutomationStore.SafeGain(headroom),0,.06f);
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
            if (manualPaused || suspended || editingOwners.Count > 0) PauseTransport();
            else ResumeTransport();
        }
        readonly float[] samples = new float[128];
        readonly float[] levels = new float[4];
        public float Level(StemId stem) => levels[(int)stem];
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
                sources[i].clip = clip;
                sources[i].playOnAwake = false;
                sources[i].loop = true;
                sources[i].spatialBlend = 0;
                sources[i].pitch = 1;
                sources[i].volume = SafeHeadroom;
            }
            manager.clock.Reset((float)((double)first.samples / first.frequency));
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
            offset = Position(AudioSettings.dspTime);
            running = false;
            foreach (var source in sources) source.Stop();
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
            foreach (var source in sources)
            {
                source.timeSamples = Math.Min(sample, source.clip.samples - 1);
                source.PlayScheduled(ScheduledStart);
            }
            running = true;
            manager.clock.playing = true;
        }
        double Position(double dsp) => offset + (running ? Math.Max(0, dsp - origin) : 0);
        public void Stop()
        {
            if (sources != null) foreach (var source in sources) if (source != null) { source.Stop(); source.clip = null; }
            running = Ready = false; offset = 0;
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
            manager.clock.elapsed = Position(AudioSettings.dspTime);
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime / (manager.design != null ? Mathf.Max(.001f,manager.design.gainSmoothingSeconds) : .025f));
            for (int i = 0; i < sources.Length; i++)
            {
                float gain = manager.automation.EvaluateGain(manager.library.Current.id, (StemId)i, manager.clock.Time,manager.clock.duration,
                    manager.design != null ? manager.design.gainBoundaryFade : .06f);
                // Preserve the measured source-peak mix bound, even if Inspector headroom is raised.
                sources[i].volume = Mathf.Lerp(sources[i].volume, SafeHeadroom * gain, blend);
                float sum = 0;
                if (running && AudioSettings.dspTime >= ScheduledStart)
                {
                    sources[i].GetOutputData(samples, 0);
                    for (int j = 0; j < samples.Length; j++) sum += samples[j] * samples[j];
                }
                levels[i] = Mathf.Lerp(levels[i], Mathf.Clamp01(Mathf.Sqrt(sum / samples.Length) * 12), blend);
            }
        }
        void OnApplicationPause(bool paused) { suspended = paused; ApplyPlaybackState(); }
        void OnDisable() => Stop();
    }
}

