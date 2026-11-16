using UnityEngine;
namespace RemixTable
{
    // Audio filter on a stem's AudioSource GameObject that applies the PITCH automation without changing
    // speed (AudioSource.pitch would also change tempo and break stem sync).
    [DisallowMultipleComponent]
    public sealed class StemPitchShifter : MonoBehaviour
    {
        volatile float semitones;
        int sampleRate;
        PitchShiftDsp dsp;
        public float Semitones
        {
            get => semitones;
            set => semitones = float.IsNaN(value) ? 0 : Mathf.Clamp(value, -PitchShiftDsp.MaxSemitones, PitchShiftDsp.MaxSemitones);
        }
        void Awake() => sampleRate = AudioSettings.outputSampleRate;
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (dsp == null || dsp.Channels != channels) dsp = new PitchShiftDsp(sampleRate > 0 ? sampleRate : 48000, channels);
            dsp.Process(data, channels, semitones);
        }
    }
}
