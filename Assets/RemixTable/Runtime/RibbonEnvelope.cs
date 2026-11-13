using UnityEngine;

namespace RemixTable
{
    /// <summary>Spatial smoothing only: no clock lag, oscillators or per-window normalization.</summary>
    public static class RibbonEnvelope
    {
        public static float SmoothPeak(float peak,float neighborMean,float strength,float preservation) =>
            Mathf.Max(peak*Mathf.Clamp01(preservation),Mathf.Lerp(peak,Mathf.Min(peak,neighborMean),Mathf.Clamp01(strength)));

        // Zero slope at knot boundaries; convex interpolation cannot overshoot or invent peaks.
        public static float Interpolate(float a,float b,float fraction)
        {
            float u=Mathf.Clamp01(fraction);
            return Mathf.Lerp(a,b,u*u*(3-2*u));
        }

        public static WaveformCache.Envelope Smooth(WaveformCache.Envelope[] raw,int i,RemixDesign design)
        {
            var result=raw[i];
            float minMean=0,maxMean=0,rmsMean=0;
            for(int j=-2;j<=2;j++)
            {
                float weight=j==0?6:Mathf.Abs(j)==1?4:1;
                var sample=raw[i+j];
                minMean+=Mathf.Max(0,-sample.minimum)*weight;
                maxMean+=Mathf.Max(0,sample.maximum)*weight;rmsMean+=sample.rms*weight;
            }
            result.minimum=-SmoothPeak(Mathf.Max(0,-result.minimum),minMean/16,design.waveformSmoothing,design.waveformPeakPreservation);
            result.maximum=SmoothPeak(Mathf.Max(0,result.maximum),maxMean/16,design.waveformSmoothing,design.waveformPeakPreservation);
            result.rms=SmoothPeak(result.rms,rmsMean/16,design.waveformSmoothing,design.waveformPeakPreservation);
            return result;
        }
    }
}
