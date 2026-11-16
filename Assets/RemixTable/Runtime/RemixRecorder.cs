using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;
namespace RemixTable
{
    // REC: records exactly what you hear (the final mix at the AudioListener, including mute / solo, filters,
    // pitch, LIVE edits and spatial placement) to a WAV file in Application.persistentDataPath/Recordings.
    // On Quest that is /sdcard/Android/data/<package>/files/Recordings (copy it off with `adb pull`).
    [RequireComponent(typeof(AudioListener))]
    public sealed class RemixRecorder : MonoBehaviour
    {
        public const float MaxSeconds = 15 * 60;
        // Audio-thread blocks waiting to be written by the main thread. The pool avoids per-block allocation.
        sealed class AudioBlock { public float[] samples; public int channels,rate,session; }
        readonly ConcurrentQueue<AudioBlock> pending = new ConcurrentQueue<AudioBlock>();
        readonly ConcurrentQueue<AudioBlock> pool = new ConcurrentQueue<AudioBlock>();
        int session;
        volatile bool capturing;
        volatile int channels;
        WavWriter writer;
        volatile int sampleRate;
        public bool Recording => writer != null;
        public float Seconds => writer != null ? writer.Seconds : 0;
        public string LastFile { get; private set; }
        public string Folder => Path.Combine(Application.persistentDataPath, "Recordings");
        public event Action<string> Saved;
        void Awake() => sampleRate = AudioSettings.outputSampleRate;
        void OnEnable() => AudioSettings.OnAudioConfigurationChanged+=AudioConfigurationChanged;
        void AudioConfigurationChanged(bool changed)
        {
            StopRecording();sampleRate=AudioSettings.outputSampleRate;
        }
        public bool StartRecording()
        {
            if (writer != null) return true;
            int ch = channels > 0 ? channels : AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            string file = Path.Combine(Folder, "remix-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".wav");
            try { writer = new WavWriter(file, sampleRate > 0 ? sampleRate : 48000, ch); }
            catch (Exception exception) { Debug.LogWarning("Recording could not start: " + exception.Message); writer = null; return false; }
            while (pending.TryDequeue(out _)) { }
            System.Threading.Interlocked.Increment(ref session);
            capturing = true;
            return true;
        }
        public string StopRecording()
        {
            if (writer == null) return null;
            capturing = false;
            Drain();
            var path = writer.Path;
            writer.Dispose(); writer = null;
            LastFile = path;
            Debug.Log("Remix recording saved: " + path);
            Saved?.Invoke(path);
            return path;
        }
        public void Toggle() { if (Recording) StopRecording(); else StartRecording(); }
        void Update()
        {
            if (writer == null) return;
            Drain();
            if (writer.Seconds >= MaxSeconds) StopRecording();
        }
        void Drain()
        {
            while (pending.TryDequeue(out var block))
            {
                // A channel-count change mid-recording (device switch) cannot be mixed into one file: drop it.
                if (writer != null && block.session == session && block.channels == writer.Channels && block.rate == writer.SampleRate && block.samples.Length % block.channels == 0) writer.Write(block.samples, block.samples.Length);
                pool.Enqueue(block);
            }
        }
        void OnAudioFilterRead(float[] data, int channelCount) => CaptureBlock(data, channelCount, sampleRate);
        // Audio-thread entry point. Each queued block retains its format and recording session.
        public void CaptureBlock(float[] data, int channelCount, int rate)
        {
            channels = channelCount;
            int currentSession = session;
            if (!capturing || data == null || channelCount <= 0) return;
            if (!pool.TryDequeue(out var block)) block = new AudioBlock();
            if (block.samples == null || block.samples.Length != data.Length) block.samples = new float[data.Length];
            Array.Copy(data, block.samples, data.Length);
            block.channels=channelCount;block.rate=rate;block.session=currentSession;
            pending.Enqueue(block);
        }
        void OnApplicationPause(bool paused) { if (paused) StopRecording(); }
        void OnDisable() {AudioSettings.OnAudioConfigurationChanged-=AudioConfigurationChanged;StopRecording();}
    }
}
