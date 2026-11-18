using UnityEngine;
namespace RemixTable
{
    // Offline tempo and beat-phase estimate for SNAP, run once per song in the Editor on its drum stem.
    // Onset strength = rectified rise in short-time energy; tempo = the autocorrelation peak of that envelope
    // between 70 and 180 BPM (gently weighted toward 120); phase = the comb offset that lands on most onsets.
    public static class TempoDetector
    {
        public struct Result { public float bpm, offset, confidence; }
        public const int Hop = 512, Frame = 1024;
        public static Result Detect(float[] mono, int sampleRate, float minBpm = 70, float maxBpm = 180)
        {
            var result = new Result();
            if (mono == null || sampleRate <= 0 || mono.Length < sampleRate * 4) return result;
            float[] onset = OnsetEnvelope(mono, out int frames);
            float frameRate = (float)sampleRate / Hop;
            int minLag = Mathf.Max(1, Mathf.FloorToInt(frameRate * 60 / maxBpm)), maxLag = Mathf.CeilToInt(frameRate * 60 / minBpm);
            if (maxLag * 4 >= frames) return result;
            var score = new float[maxLag + 2];
            float best = float.MinValue; int bestLag = minLag; float total = 0;
            for (int lag = minLag; lag <= maxLag + 1; lag++)
            {
                double sum = 0;
                for (int i = lag; i < frames; i++) sum += onset[i] * onset[i - lag];
                float bpm = frameRate * 60 / lag;
                // Log-Gaussian tempo prior centered on 120 BPM resolves half/double-tempo ambiguity.
                float prior = Mathf.Exp(-.5f * Mathf.Pow(Mathf.Log(bpm / 120f, 2) / .9f, 2));
                score[lag] = (float)(sum / (frames - lag)) * prior;
                if (lag <= maxLag) { total += score[lag]; if (score[lag] > best) { best = score[lag]; bestLag = lag; } }
            }
            // Parabolic refinement between neighbouring lags gives sub-frame tempo precision.
            float refined = bestLag;
            if (bestLag > minLag && bestLag < maxLag)
            {
                float a = score[bestLag - 1], b = score[bestLag], c = score[bestLag + 1];
                float denominator = a - 2 * b + c;
                if (Mathf.Abs(denominator) > 1e-12f) refined += Mathf.Clamp(.5f * (a - c) / denominator, -.5f, .5f);
            }
            float mean = total / Mathf.Max(1, maxLag - minLag + 1);
            result.confidence = best > 0 ? Mathf.Clamp01(1 - mean / best) : 0;
            // Refine tempo and phase together. A rounded autocorrelation lag can be close in BPM
            // yet drift several frames across the song, biasing a phase-only comb search.
            float period=refined,bestPhase=0,bestPhaseScore=float.MinValue;
            for(int candidate=0;candidate<=100;candidate++)
            {
                float candidatePeriod=refined-.5f+candidate*.01f;
                if(candidatePeriod<frameRate*60/maxBpm || candidatePeriod>frameRate*60/minBpm) continue;
                int steps=Mathf.Max(8,Mathf.CeilToInt(candidatePeriod*4));
                for(int phaseIndex=0;phaseIndex<steps;phaseIndex++)
                {
                    float phase=candidatePeriod*phaseIndex/steps;double sum=0;int count=0;
                    for(float f=phase;f<frames-1;f+=candidatePeriod)
                    {
                        int i=(int)f;float t=f-i;
                        sum+=onset[i]*(1-t)+onset[i+1]*t;count++;
                    }
                    float value=(float)(sum/Mathf.Max(1,count));
                    if(value>bestPhaseScore) {bestPhaseScore=value;period=candidatePeriod;bestPhase=phase;}
                }
            }
            result.bpm=frameRate*60/period;
            // An attack first shows up in the frame whose window end reaches it, so the attack sits near the end
            // of that frame (half a hop before it on average). Wrap into the first beat.
            float beatSeconds = period * Hop / sampleRate;
            result.offset = Mathf.Repeat((bestPhase * Hop + Frame - Hop * .5f) / sampleRate, beatSeconds);
            return result;
        }
        // Mean onset strength sampled on a beat grid (linear interpolation between frames).
        static float Comb(float[] onset, int frames, float phase, float period)
        {
            double sum = 0; int count = 0;
            for (float f = phase; f < frames - 1; f += period)
            {
                int i = (int)f; float t = f - i;
                sum += onset[i] * (1 - t) + onset[i + 1] * t; count++;
            }
            return count == 0 ? 0 : (float)(sum / count);
        }
        static float[] OnsetEnvelope(float[] mono, out int frames)
        {
            frames = Mathf.Max(0, (mono.Length - Frame) / Hop + 1);
            var energy = new float[frames];
            float previous = 0;
            for (int f = 0; f < frames; f++)
            {
                double sum = 0; int start = f * Hop;
                // First difference acts as a cheap high-pass, emphasising percussive attacks over sustained bass.
                for (int i = 1; i < Frame; i++) { float d = mono[start + i] - mono[start + i - 1]; sum += d * d; }
                float e = Mathf.Log(1e-9f + (float)(sum / Frame));
                energy[f] = f == 0 ? 0 : Mathf.Max(0, e - previous);
                previous = e;
            }
            // Remove the local mean so steady passages contribute nothing.
            var onset = new float[frames];
            const int half = 8;
            for (int f = 0; f < frames; f++)
            {
                float sum = 0; int count = 0;
                for (int k = Mathf.Max(0, f - half); k <= Mathf.Min(frames - 1, f + half); k++) { sum += energy[k]; count++; }
                onset[f] = Mathf.Max(0, energy[f] - sum / count);
            }
            return onset;
        }
    }
}
