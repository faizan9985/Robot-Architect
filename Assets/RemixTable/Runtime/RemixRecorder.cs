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
        readonly ConcurrentQueue<float[]> pending = new ConcurrentQueue<float[]>();
        readonly ConcurrentQueue<float[]> pool = new ConcurrentQueue<float[]>();
        volatile bool capturing;
        volatile int channels;
        WavWriter writer;
        int sampleRate;
        public bool Recording => writer != null;
        public float Seconds => writer != null ? writer.Seconds : 0;
        public string LastFile { get; private set; }
        public string Folder => Path.Combine(Application.persistentDataPath, "Recordings");
        public event Action<string> Saved;
        void Awake() => sampleRate = AudioSettings.outputSampleRate;
        public bool StartRecording()
        {
            if (writer != null) return true;
            int ch = channels > 0 ? channels : AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            string file = Path.Combine(Folder, "remix-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".wav");
            try { writer = new WavWriter(file, sampleRate > 0 ? sampleRate : 48000, ch); }
            catch (Exception exception) { Debug.LogWarning("Recording could not start: " + exception.Message); writer = null; return false; }
            while (pending.TryDequeue(out _)) { }
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
                if (writer != null && block.Length > 0 && block.Length % writer.Channels == 0) writer.Write(block, block.Length);
                pool.Enqueue(block);
            }
        }
        void OnAudioFilterRead(float[] data, int channelCount)
        {
            channels = channelCount;
            if (!capturing) return;
            if (!pool.TryDequeue(out var block) || block.Length != data.Length) block = new float[data.Length];
            Array.Copy(data, block, data.Length);
            pending.Enqueue(block);
        }
        void OnApplicationPause(bool paused) { if (paused) StopRecording(); }
        void OnDisable() => StopRecording();
    }
}
