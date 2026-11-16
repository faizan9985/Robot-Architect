using System;
using UnityEngine;
namespace RemixTable
{
    // Real-time pitch shifter that keeps tempo: two read heads sweep through a short delay line at the
    // pitch ratio and are cross-faded with a sine window, so the stem stays in sync with the others while
    // its pitch moves. Allocation-free on the audio thread. When the shift is zero the input passes through
    // untouched (a short dry/wet ramp avoids clicks when a shift starts or ends).
    public sealed class PitchShiftDsp
    {
        public const float MaxSemitones = 12;
        readonly float[][] delay;              // One ring buffer per channel.
        readonly int size, mask;
        readonly float window;                 // Read-head sweep length in samples.
        int write;
        float phase;                           // 0..1 position of head A in its sweep; head B is half a sweep behind.
        float wet;                             // Smoothed dry/wet mix.
        readonly float wetStep;
        public int Channels { get; }
        public PitchShiftDsp(int sampleRate, int channels, float windowSeconds = .06f)
        {
            Channels = Mathf.Max(1, channels);
            window = Mathf.Max(64, windowSeconds * Mathf.Max(8000, sampleRate));
            size = Mathf.NextPowerOfTwo(Mathf.CeilToInt(window * 2 + 8)); mask = size - 1;
            delay = new float[Channels][];
            for (int c = 0; c < Channels; c++) delay[c] = new float[size];
            wetStep = 1f / Mathf.Max(1, .01f * sampleRate); // 10 ms ramp.
        }
        public static float Ratio(float semitones) => Mathf.Pow(2, Mathf.Clamp(semitones, -MaxSemitones, MaxSemitones) / 12f);
        // data: interleaved samples with `channels` channels (must match the constructor).
        public void Process(float[] data, int channels, float semitones)
        {
            if (channels != Channels || data == null) return;
            bool active = Mathf.Abs(semitones) > .01f;
            if (!active && wet <= 0)
            {
                // Keep the delay line filled so a shift can start without a gap of silence.
                for (int i = 0; i < data.Length; i += channels)
                {
                    for (int c = 0; c < channels; c++) delay[c][write] = data[i + c];
                    write = (write + 1) & mask;
                }
                return;
            }
            float ratio = Ratio(semitones);
            // Each output sample the delay changes by (1 - ratio) samples; phase advances by that over the window.
            float step = (1 - ratio) / window;
            for (int i = 0; i < data.Length; i += channels)
            {
                wet = active ? Mathf.Min(1, wet + wetStep) : Mathf.Max(0, wet - wetStep);
                float pa = phase, pb = phase + .5f; if (pb >= 1) pb -= 1;
                float da = pa * window + 1, db = pb * window + 1;          // Delays in samples (>= 1).
                float ga = Mathf.Sin(pa * Mathf.PI), gb = Mathf.Sin(pb * Mathf.PI); // sin^2 + cos^2 = 1 window pair.
                ga *= ga; gb *= gb;
                for (int c = 0; c < channels; c++)
                {
                    var line = delay[c];
                    line[write] = data[i + c];
                    float shifted = Read(line, write - da) * ga + Read(line, write - db) * gb;
                    data[i + c] = data[i + c] + (shifted - data[i + c]) * wet;
                }
                write = (write + 1) & mask;
                phase += step;
                if (phase >= 1) phase -= 1; else if (phase < 0) phase += 1;
            }
        }
        float Read(float[] line, float position)
        {
            float floor = Mathf.Floor(position);
            int i0 = (int)floor & mask, i1 = (i0 + 1) & mask;
            float t = position - floor;
            return line[i0] + (line[i1] - line[i0]) * t;
        }
        public void Clear()
        {
            foreach (var line in delay) Array.Clear(line, 0, line.Length);
            phase = 0; wet = 0;
        }
    }
}
