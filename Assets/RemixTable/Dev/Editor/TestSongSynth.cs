using System;

namespace RemixTable.Dev.Editor
{
    /// <summary>
    /// Deterministic synthesis of the test song: A minor, Am | F | C | G twice, 120 BPM, 4/4, 8 bars.
    /// Every voice is written with wrap-around indexing, so tails at the end of the loop continue at its start and
    /// the loop is seamless. Sharp transients on the grid (kick, hats, bass and arp plucks) make any drift between
    /// stems audible as flams.
    /// </summary>
    static class TestSongSynth
    {
        const double TwoPi = Math.PI * 2.0;

        // Chord per bar (cycled): Am, F, C (2nd inversion), G.
        static readonly int[][] Chords =
        {
            new[] { 57, 60, 64 },
            new[] { 53, 57, 60 },
            new[] { 55, 60, 64 },
            new[] { 55, 59, 62 },
        };
        static readonly int[] BassRoots = { 33, 29, 36, 31 };
        static readonly int[][] ArpNotes =
        {
            new[] { 69, 72, 76, 81 },
            new[] { 65, 69, 72, 77 },
            new[] { 67, 72, 76, 79 },
            new[] { 67, 71, 74, 79 },
        };
        static readonly int[] ArpPattern = { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 3, 2, 1, 2 };

        public struct Layout
        {
            public int SampleRate;
            public int SamplesPerBeat;
            public int BeatsPerBar;
            public int Bars;
            public int SamplesPerBar => SamplesPerBeat * BeatsPerBar;
            public int TotalSamples => SamplesPerBar * Bars;
        }

        // ------------------------------------------------------------------ stems

        public static float[] Drums(Layout l)
        {
            var buffer = new float[l.TotalSamples];
            var rng = new Random(101);
            int beats = l.BeatsPerBar * l.Bars;
            for (int b = 0; b < beats; b++)
            {
                long start = (long)b * l.SamplesPerBeat;
                int beatInBar = b % l.BeatsPerBar;
                Kick(buffer, start, l.SampleRate);
                if (beatInBar == 1 || beatInBar == 3) Snare(buffer, start, l.SampleRate, rng);
                Hat(buffer, start + l.SamplesPerBeat / 2, l.SampleRate, rng, beatInBar == 3 ? 0.07 : 0.022);
            }
            Normalize(buffer, 0.85f);
            return buffer;
        }

        public static float[] Bass(Layout l)
        {
            var buffer = new float[l.TotalSamples];
            int eighth = l.SamplesPerBeat / 2;
            for (int bar = 0; bar < l.Bars; bar++)
            {
                int root = BassRoots[bar % 4];
                for (int e = 0; e < l.BeatsPerBar * 2; e++)
                {
                    int note = e == l.BeatsPerBar * 2 - 1 ? root + 12 : e == 3 ? root + 7 : root;
                    long start = (long)bar * l.SamplesPerBar + e * eighth;
                    BassNote(buffer, start, Hz(note), 0.21, l.SampleRate);
                }
            }
            Normalize(buffer, 0.5f); // bass is dense; keep its RMS near the others
            return buffer;
        }

        public static float[] Keys(Layout l)
        {
            var buffer = new float[l.TotalSamples];
            double barSeconds = l.SamplesPerBar / (double)l.SampleRate;
            for (int bar = 0; bar < l.Bars; bar++)
            {
                long start = (long)bar * l.SamplesPerBar;
                foreach (int note in Chords[bar % 4]) PadNote(buffer, start, Hz(note), barSeconds, l.SampleRate);
            }
            Normalize(buffer, 0.6f);
            return buffer;
        }

        public static float[] Lead(Layout l)
        {
            var buffer = new float[l.TotalSamples];
            int sixteenth = l.SamplesPerBeat / 4;
            int steps = l.BeatsPerBar * 4;
            for (int bar = 0; bar < l.Bars; bar++)
            {
                int[] notes = ArpNotes[bar % 4];
                for (int step = 0; step < steps; step++)
                {
                    long start = (long)bar * l.SamplesPerBar + step * sixteenth;
                    double accent = step % 4 == 0 ? 1.0 : 0.65;
                    Pluck(buffer, start, Hz(notes[ArpPattern[step % ArpPattern.Length]]), accent, l.SampleRate);
                }
            }
            Normalize(buffer, 0.45f);
            return buffer;
        }

        /// <summary>Band-passed noise swells, one every 2 bars, silent at each swell boundary (seamless loop).</summary>
        public static float[] Fx(Layout l)
        {
            var buffer = new float[l.TotalSamples];
            var rng = new Random(303);
            int period = l.SamplesPerBar * 2;
            var svf = new StateVariableFilter();
            for (int n = 0; n < buffer.Length; n++)
            {
                double phase = (n % period) / (double)period;
                double shape = Math.Sin(Math.PI * phase);
                double swell = shape * shape;
                double cutoff = 350.0 * Math.Pow(12.0, shape);
                double noise = rng.NextDouble() * 2.0 - 1.0;
                double band = svf.BandPass(noise, cutoff, 0.3, l.SampleRate);
                buffer[n] = (float)(band * swell);
            }
            Normalize(buffer, 0.4f);
            return buffer;
        }

        /// <summary>Rising noise sweep plus a pitch sweep, loudest at its very end (where the hit lands).</summary>
        public static float[] Riser(int samples, int sampleRate)
        {
            var buffer = new float[samples];
            var rng = new Random(404);
            var svf = new StateVariableFilter();
            double phase = 0;
            int fade = sampleRate / 200; // 5 ms
            for (int n = 0; n < samples; n++)
            {
                double p = n / (double)samples;
                double amp = p * p;
                double cutoff = 300.0 * Math.Pow(23.0, p);
                double noise = svf.BandPass(rng.NextDouble() * 2.0 - 1.0, cutoff, 0.4, sampleRate);
                phase += TwoPi * (200.0 * Math.Pow(8.0, p)) / sampleRate;
                double v = (noise * 0.9 + Math.Sin(phase) * 0.3) * amp;
                if (n > samples - fade) v *= (samples - n) / (double)fade;
                buffer[n] = (float)v;
            }
            Normalize(buffer, 0.8f);
            return buffer;
        }

        /// <summary>Sub boom plus a noise burst.</summary>
        public static float[] Hit(int samples, int sampleRate)
        {
            var buffer = new float[samples];
            var rng = new Random(505);
            double phase = 0, lp = 0;
            for (int n = 0; n < samples; n++)
            {
                double t = n / (double)sampleRate;
                double attack = Math.Min(1.0, t / 0.001);
                phase += TwoPi * (38.0 + 70.0 * Math.Exp(-t / 0.07)) / sampleRate;
                double boom = Math.Sin(phase) * Math.Exp(-t / 0.6);
                lp += 0.25 * (rng.NextDouble() * 2.0 - 1.0 - lp);
                double burst = lp * Math.Exp(-t / 0.15) * 0.8;
                double tail = n > samples - sampleRate / 100 ? (samples - n) / (sampleRate / 100.0) : 1.0;
                buffer[n] = (float)((boom + burst) * attack * tail);
            }
            Normalize(buffer, 0.9f);
            return buffer;
        }

        // ------------------------------------------------------------------ voices

        static void Kick(float[] buffer, long start, int rate)
        {
            int length = (int)(0.4 * rate);
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate;
                phase += TwoPi * (48.0 + 120.0 * Math.Exp(-t / 0.03)) / rate;
                double amp = Math.Exp(-t / 0.14) * Math.Min(1.0, t / 0.001);
                Add(buffer, start + i, Math.Sin(phase) * amp);
            }
        }

        static void Snare(float[] buffer, long start, int rate, Random rng)
        {
            int length = (int)(0.25 * rate);
            double previous = 0;
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate;
                double noise = rng.NextDouble() * 2.0 - 1.0;
                double bright = noise - 0.5 * previous;
                previous = noise;
                double tone = Math.Sin(TwoPi * 190.0 * t) * Math.Exp(-t / 0.05) * 0.5;
                double body = bright * Math.Exp(-t / 0.09) * 0.45;
                Add(buffer, start + i, (tone + body) * Math.Min(1.0, t / 0.001));
            }
        }

        static void Hat(float[] buffer, long start, int rate, Random rng, double decaySeconds)
        {
            int length = (int)(decaySeconds * 6.0 * rate);
            double x1 = 0, d1 = 0;
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate;
                double x = rng.NextDouble() * 2.0 - 1.0;
                double d = x - x1;
                double hp = d - d1;
                x1 = x;
                d1 = d;
                Add(buffer, start + i, hp * 0.1 * Math.Exp(-t / decaySeconds));
            }
        }

        static void BassNote(float[] buffer, long start, double frequency, double seconds, int rate)
        {
            int length = (int)(seconds * rate);
            int release = (int)(0.015 * rate);
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1.0, t / 0.004) * (0.55 + 0.45 * Math.Exp(-t / 0.08));
                if (i > length - release) env *= (length - i) / (double)release;
                double v = 0;
                for (int k = 1; k <= 6; k++) v += Math.Sin(TwoPi * k * frequency * t) / Math.Pow(k, 1.3);
                Add(buffer, start + i, v * env);
            }
        }

        static void PadNote(float[] buffer, long start, double frequency, double holdSeconds, int rate)
        {
            const double releaseSeconds = 0.15;
            const double detune = 0.0015;
            double[] harmonics = { 1.0, 0.45, 0.25, 0.12 };
            int length = (int)((holdSeconds + releaseSeconds) * rate);
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1.0, t / 0.05) * Math.Exp(-t / 4.0);
                if (t > holdSeconds) env *= Math.Max(0.0, 1.0 - (t - holdSeconds) / releaseSeconds);
                double tremolo = 1.0 + 0.08 * Math.Sin(TwoPi * 4.5 * t);
                double v = 0;
                for (int k = 0; k < harmonics.Length; k++)
                {
                    double f = frequency * (k + 1);
                    v += harmonics[k] * 0.5 * (Math.Sin(TwoPi * f * (1.0 + detune) * t) + Math.Sin(TwoPi * f * (1.0 - detune) * t));
                }
                Add(buffer, start + i, v * env * tremolo);
            }
        }

        static void Pluck(float[] buffer, long start, double frequency, double accent, int rate)
        {
            int length = (int)(0.16 * rate);
            for (int i = 0; i < length; i++)
            {
                double t = i / (double)rate;
                double env = Math.Min(1.0, t / 0.002) * Math.Exp(-t / 0.06) * accent;
                double v = 0;
                for (int k = 1; k <= 7; k += 2) v += Math.Sin(TwoPi * k * frequency * t) / k;
                Add(buffer, start + i, v * env);
            }
        }

        // ------------------------------------------------------------------ helpers

        sealed class StateVariableFilter
        {
            double low, band;

            public double BandPass(double input, double cutoffHz, double damping, int rate)
            {
                double f = 2.0 * Math.Sin(Math.PI * Math.Min(cutoffHz, rate / 7.0) / rate);
                low += f * band;
                double high = input - low - damping * band;
                band += f * high;
                return band;
            }
        }

        static double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        static void Add(float[] buffer, long index, double value)
        {
            int n = buffer.Length;
            int i = (int)(((index % n) + n) % n);
            buffer[i] += (float)value;
        }

        static void Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++) max = Math.Max(max, Math.Abs(buffer[i]));
            if (max <= 0f) return;
            float scale = peak / max;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
        }
    }
}
