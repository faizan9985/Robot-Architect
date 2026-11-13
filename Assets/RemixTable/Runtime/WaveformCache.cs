using System;
using UnityEngine;

namespace RemixTable
{
    /// <summary>Editor-baked signed stereo envelope. No audio decoding or sample extraction at runtime.</summary>
    public sealed class WaveformCache : ScriptableObject
    {
        [SerializeField] int sampleRate, sampleFrames, blockFrames;
        [SerializeField] float quantizationScale;
        [SerializeField] byte[] peaks;
        [SerializeField] int[] levelOffsets, levelCounts;
        public int SampleRate => sampleRate;
        public int SampleFrames => sampleFrames;
        public int BlockFrames => blockFrames;
        public int LevelCount => levelCounts.Length;
        public int DataBytes => peaks.Length;
        public double Duration => (double)sampleFrames / sampleRate;
        public struct Envelope
        {
            public float minimum, maximum, rms;
        }
        // Used by the importer and deterministic test fixtures; packed triples are little endian.
        public void Initialize(int rate, int frames, int block, float scale, byte[] data, int[] offsets, int[] counts)
        {
            sampleRate=rate; sampleFrames=frames; blockFrames=block; quantizationScale=scale;
            peaks=data; levelOffsets=offsets; levelCounts=counts;
        }
        public int LevelForSpan(double seconds)
        {
            int level=0;
            while(level+1<LevelCount && (blockFrames*(1L<<(level+1))) <= seconds*sampleRate) level++;
            return level;
        }
        int U16(int index) => peaks[index] | peaks[index+1]<<8;
        Envelope Read(int level,int index)
        {
            int p=levelOffsets[level]+index*6;
            return new Envelope {minimum=(short)U16(p)*quantizationScale,
                maximum=(short)U16(p+2)*quantizationScale, rms=U16(p+4)*quantizationScale};
        }
        // Cover the interval with aligned pyramid nodes. Only edge base bins are conservative
        // (less than one base block extra at either edge); no interior transient is skipped.
        void Accumulate(double start,double end,ref Envelope result,ref double energy,ref long weight)
        {
            int first=Math.Max(0,(int)Math.Floor(start*sampleRate/blockFrames+1e-9));
            int last=Math.Min(levelCounts[0],(int)Math.Ceiling(end*sampleRate/blockFrames-1e-9));
            while(first<last)
            {
                int level=0;
                while(level+1<LevelCount && (first % (1<<(level+1)))==0 && first+(1<<(level+1))<=last) level++;
                var e=Read(level,first>>level);
                int length=Math.Min(sampleFrames,(first+(1<<level))*blockFrames)-first*blockFrames;
                result.minimum=Mathf.Min(result.minimum,e.minimum); result.maximum=Mathf.Max(result.maximum,e.maximum);
                energy+=(double)e.rms*e.rms*length; weight+=length;
                first+=1<<level;
            }
        }
        /// <summary>Looping half-open interval, in seconds, including signed peaks across the seam.</summary>
        public Envelope Query(double start,double span)
        {
            var result=new Envelope();
            if(span<=0 || double.IsNaN(start) || double.IsInfinity(start) || double.IsNaN(span) || double.IsInfinity(span)) return result;
            result.minimum=float.PositiveInfinity;result.maximum=float.NegativeInfinity;
            double duration=Duration;
            start=((start%duration)+duration)%duration;
            span=Math.Min(span,duration);
            double energy=0; long weight=0;
            double end=Math.Min(duration,start+span);
            Accumulate(start,end,ref result,ref energy,ref weight);
            if(start+span>duration) Accumulate(0,start+span-duration,ref result,ref energy,ref weight);
            if(weight==0) return default;
            result.rms=(float)Math.Sqrt(energy/weight);
            return result;
        }
    }
}
