using UnityEngine;
namespace RemixTable
{
    [CreateAssetMenu(menuName = "Remix Table/Design")]
    public class RemixDesign : ScriptableObject
    {
        public Color[] colors = { new Color(1,.24f,.035f), new Color(.15f,1,.3f), new Color(.03f,.85f,1), new Color(.65f,.18f,1) };
        public float emission = 1.8f, width = .86f, thickness = .007f, spacing = .105f;
        public float interactionRadius = .055f, animationSpeed = 1, highlight = 1.8f;
        public float maxSemitones = 12, pitchDistance = .12f, amplitude = .018f;
        public float minGain = 0, maxGain = 2.5f, resyncSpeed = 6, visibleSeconds = 8;
        [Min(.001f)] public float gainBoundaryFade = .06f;
        [Min(.001f)] public float gainSmoothingSeconds = .025f;
        [Min(.01f)] public float gainGestureSensitivity = 1;
        [Min(.01f)] public float gainDragDistance = .12f;
        [Min(0)] public float gainDragDeadzone = .008f;
        [Header("Filter (lift in FILTER mode)")]
        [Tooltip("Hand lift (meters) from neutral to a full high-pass (up) or low-pass (down) sweep.")]
        [Min(.01f)] public float filterDragDistance = .12f;
        public float MapDragFilter(float initial, float localDisplacement) =>
            StemFilter.SafeEffect(initial + localDisplacement / Mathf.Max(.01f, filterDragDistance));
        public float MapDragGain(float initial, float localDisplacement)
        {
            float low=AutomationStore.SafeGain(minGain), high=Mathf.Max(low,AutomationStore.SafeGain(maxGain));
            return Mathf.Clamp(AutomationStore.SafeGain(initial+localDisplacement/Mathf.Max(.01f,gainDragDistance)*gainGestureSensitivity),low,high);
        }
        public float MapGain(float initial, float separation, float initialSeparation)
        {
            float ratio=separation/Mathf.Max(.025f,initialSeparation);
            float low=AutomationStore.SafeGain(minGain), high=Mathf.Max(low,AutomationStore.SafeGain(maxGain));
            return Mathf.Clamp(AutomationStore.SafeGain(initial+(ratio-1)*gainGestureSensitivity),low,high);
        }
        [Range(32,256)] public int segments = 128;
        public int particleBudget = 24;
        public Material ribbonMaterial;
        [Header("Seated workspace (meters)")]
        public float panelWidth = .74f, panelHeight = .40f;
        public float laneHeight = .078f, interactionDepth = .06f;
        public float workspaceDistance = .62f, workspaceEyeDrop = .20f;
        public float transportGap = .015f, transportHeight = .10f;
        public bool constrainToLanes;
        public float TransportY => -panelHeight*.5f-transportGap-transportHeight*.5f;
        public float LaneY(int row) => (1.5f-row)*spacing;
        public float WaveCenterLimit => Mathf.Max(.001f,laneHeight*.5f-thickness*1.8f-.003f);
        public Pose SeatedPose(Vector3 eye, Vector3 gaze)
        {
            var forward=Vector3.ProjectOnPlane(gaze,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f) forward=Vector3.forward;
            return new Pose(eye+forward*workspaceDistance-Vector3.up*workspaceEyeDrop,Quaternion.LookRotation(forward));
        }
    }
}
