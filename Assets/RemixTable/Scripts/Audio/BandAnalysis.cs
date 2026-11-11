using UnityEngine;

namespace RemixTable
{
    /// <summary>RMS levels of one analysis window: whole signal plus low / mid / high bands.</summary>
    public struct BandLevels
    {
        public float rms;
        public float low;
        public float mid;
        public float high;
    }

    /// <summary>
    /// Allocation-free level and 3-band analysis. Bands come from two one-pole low-passes:
    /// low = LP(lowCut), mid = LP(highCut) − LP(lowCut), high = x − LP(highCut). Gentle slopes, but plenty for
    /// driving visuals, and far cheaper than an FFT.
    /// </summary>
    public static class BandAnalysis
    {
        public static float OnePoleCoefficient(float cutoffHz, int sampleRate) =>
            sampleRate <= 0 ? 1f : 1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / sampleRate);

        /// <param name="samples">Interleaved samples; every frame is averaged to mono.</param>
        public static BandLevels Analyze(float[] samples, int channels, float lowCoefficient, float highCoefficient)
        {
            var result = default(BandLevels);
            if (samples == null || channels <= 0) return result;
            int frames = samples.Length / channels;
            if (frames == 0) return result;

            float first = FrameValue(samples, 0, channels);
            float lp1 = first, lp2 = first;
            double sumAll = 0, sumLow = 0, sumMid = 0, sumHigh = 0;

            for (int f = 0; f < frames; f++)
            {
                float x = FrameValue(samples, f, channels);
                lp1 += lowCoefficient * (x - lp1);
                lp2 += highCoefficient * (x - lp2);
                float mid = lp2 - lp1;
                float high = x - lp2;
                sumAll += x * x;
                sumLow += lp1 * lp1;
                sumMid += mid * mid;
                sumHigh += high * high;
            }

            result.rms = (float)System.Math.Sqrt(sumAll / frames);
            result.low = (float)System.Math.Sqrt(sumLow / frames);
            result.mid = (float)System.Math.Sqrt(sumMid / frames);
            result.high = (float)System.Math.Sqrt(sumHigh / frames);
            return result;
        }

        static float FrameValue(float[] samples, int frame, int channels)
        {
            if (channels == 1) return samples[frame];
            int start = frame * channels;
            float sum = 0f;
            for (int c = 0; c < channels; c++) sum += samples[start + c];
            return sum / channels;
        }
    }
}
