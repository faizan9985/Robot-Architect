using System;
using System.Collections.Generic;
using UnityEngine;

namespace RemixTable
{
    public enum StemId { Drums, Bass, Melody, Vocals }
    public enum ParameterType { Pitch, Gain, Effect }
    [Serializable] public class AutomationEdit
    {
        public string id = Guid.NewGuid().ToString("N");
        public string songId;
        public StemId stem;
        public float start, end, value;
        public ParameterType parameter;
        public bool wholeStem;
        public AnimationCurve curve;
        public bool Contains(float time) => wholeStem || (start <= end ? time >= start && time <= end : time >= start || time <= end);
        public float Evaluate(float time, float duration = 0) => curve != null && curve.length > 0 ? curve.Evaluate(wholeStem ? time : time - start + (time < start && end < start ? duration : 0)) : value;
    }
    [Serializable] public class AutomationStore
    {
        public List<AutomationEdit> edits = new List<AutomationEdit>();
        public void Upsert(AutomationEdit edit)
        {
            if(edit.parameter==ParameterType.Gain) edit.value=SafeGain(edit.value);
            int index = edits.FindIndex(x => x.id == edit.id);
            if (index >= 0) edits.RemoveAt(index);
            edits.Add(edit); // Last committed/updated edit has priority.
        }
        public float Evaluate(string song, StemId stem, ParameterType parameter, float time, float duration = 0)
        {
            for (int i = edits.Count - 1; i >= 0; --i)
            {
                var e = edits[i];
                if (e.songId == song && e.stem == stem && e.parameter == parameter && e.Contains(time)) return e.Evaluate(time,duration);
            }
            return parameter == ParameterType.Gain ? 1 : 0;
        }
        public float EvaluateWholeGain(string song, StemId stem, float time) { for (int i=edits.Count-1;i>=0;i--) { var e=edits[i]; if(e.songId==song && e.stem==stem && e.parameter==ParameterType.Gain && e.wholeStem) return e.Evaluate(time); } return 1; }
        public const float MaximumGain = 2.5f;
        public static float SafeGain(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1 : Mathf.Clamp(value, 0, MaximumGain);
        public static float RegionLength(float start, float end, float duration) => end >= start ? end - start : duration - start + end;
        public AutomationEdit FindGainEdit(string song, StemId stem, bool whole, float start, float end)
        {
            for (int i=edits.Count-1;i>=0;i--)
            {
                var e=edits[i];
                if(e.songId==song && e.stem==stem && e.parameter==ParameterType.Gain && e.wholeStem==whole &&
                    (whole || (Mathf.Abs(e.start-start)<.025f && Mathf.Abs(e.end-end)<.025f))) return e;
            }
            return null;
        }
        public float EvaluateRegionGain(string song, StemId stem, float time, float duration, float fade)
        {
            if (duration <= 0 || float.IsNaN(time) || float.IsInfinity(time)) return 1;
            time=Mathf.Repeat(time,duration);
            float gain=1;
            for (int i=0;i<edits.Count;i++)
            {
                var e=edits[i];
                if(e.songId!=song || e.stem!=stem || e.parameter!=ParameterType.Gain || e.wholeStem || !e.Contains(time)) continue;
                float length=RegionLength(e.start,e.end,duration);
                if(length<=0 || length>duration) continue;
                float distance=time-e.start; if(distance<0) distance+=duration;
                // Fade only at the two region endpoints, never at the loop seam inside a wrapped region.
                float width=Mathf.Min(Mathf.Max(.001f,fade),length*.5f);
                float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(distance,length-distance)/width));
                gain=Mathf.Lerp(gain,SafeGain(e.Evaluate(time,duration)),weight);
            }
            return SafeGain(gain);
        }
        public float EvaluateGain(string song, StemId stem, float time, float duration, float fade)
        {
            if(duration<=0 || float.IsNaN(duration) || float.IsInfinity(duration) || float.IsNaN(time) || float.IsInfinity(time)) return 1;
            time=Mathf.Repeat(time,duration);
            return SafeGain(SafeGain(EvaluateWholeGain(song,stem,time))*EvaluateRegionGain(song,stem,time,duration,fade));
        }
        public void ResetStem(string song, StemId stem) => edits.RemoveAll(x => x.songId == song && x.stem == stem);
        public void ResetSong(string song) => edits.RemoveAll(x => x.songId == song);
    }
    [Serializable] public class StemMetadata { public StemId id; public string label; public AudioClip clip; public WaveformCache waveform; }
    [Serializable] public class SongMetadata
    {
        public string id, title;
        public float duration = 32, bpm = 110;
        public int seed;
        public StemMetadata[] stems;
    }
    [Serializable] public class PlaybackClock
    {
        public double elapsed;
        public float duration = 32;
        public bool playing = true;
        public float Time => (float)(elapsed % Math.Max(0.01f, duration));
        public void Advance(float delta) { if (playing) elapsed += Math.Max(0, delta); }
        public void Reset(float length) { duration = Mathf.Max(.01f, length); elapsed = 0; }
    }
}

