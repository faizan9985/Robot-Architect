using System;
using UnityEngine;
namespace RemixTable
{
    /// <summary>One runtime time window shared by meshes, hand mapping and menu controls.</summary>
    [Serializable]
    public sealed class TimelineZoom
    {
        public const float Minimum = 4, Maximum = 32;
        [SerializeField] float visibleSeconds = 8;
        [SerializeField] float targetSeconds = 8;
        public float VisibleSeconds => visibleSeconds;
        public float TargetSeconds => targetSeconds;
        public bool AtMinimum => visibleSeconds <= Minimum + .01f;
        public bool AtMaximum => visibleSeconds >= Maximum - .01f;
        public void Request(float seconds)
        {
            if(float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            targetSeconds = Mathf.Clamp(seconds, Minimum, Maximum);
        }
        // Freeze both in-flight interpolation and new requests while any edit owns the timeline.
        public void Tick(float deltaTime, bool editing, float smoothingSeconds)
        {
            if(editing || deltaTime <= 0) return;
            float blend = 1 - Mathf.Exp(-deltaTime / Mathf.Max(.01f,smoothingSeconds));
            visibleSeconds = Mathf.Lerp(visibleSeconds,targetSeconds,blend);
            if(Mathf.Abs(visibleSeconds-targetSeconds)<.001f) visibleSeconds=targetSeconds;
        }
        public float TimeOffset(float coordinate, RemixDesign design) => coordinate / design.width * visibleSeconds;
        public float Timestamp(float coordinate, double playhead, float duration, RemixDesign design) =>
            Mathf.Repeat((float)(playhead % duration) + TimeOffset(coordinate,design),duration);
        public float AngleToTimestamp(float radians, double playhead, float duration, RemixDesign design) =>
            Timestamp(radians / (2*design.HalfAngle) * design.width,playhead,duration,design);
        // A song-relative timestamp can occur more than once in a window longer than a short loop.
        // Choose its nearest occurrence; callers that know a loop occurrence can use OffsetToAngle.
        public float TimestampToAngle(float timestamp, double playhead, float duration, RemixDesign design)
        {
            float delta=Mathf.Repeat(timestamp-(float)(playhead%duration)+duration*.5f,duration)-duration*.5f;
            return OffsetToAngle(delta,design);
        }
        public float OffsetToAngle(float seconds, RemixDesign design) => seconds/visibleSeconds*2*design.HalfAngle;
        public static float GestureWindow(float initialWindow, float initialDistance, float distance, float sensitivity) =>
            Mathf.Clamp(initialWindow*Mathf.Pow(Mathf.Max(.08f,initialDistance)/Mathf.Max(.08f,distance),sensitivity),Minimum,Maximum);
    }
}
