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
        [Header("Curved timeline")]
        public bool curvedTimeline;
        [Range(.5f,1.2f)] public float arcRadius = .8f;
        [Range(90,200)] public float arcAngle = 180;
        [Range(40,110)] public float interactiveAngle = 90;
        [Min(.1f)] public float pastSeconds = 4, futureSeconds = 4;
        [Range(.5f,1.5f)] public float arcScale = 1;
        [Header("Timeline zoom")]
        [Min(.01f)] public float zoomSmoothingSeconds = .16f;
        [Range(.25f,3)] public float zoomGestureSensitivity = 1;
        [Min(.005f)] public float zoomMovementThreshold = .025f;
        public float Radius => Mathf.Max(.1f,arcRadius*arcScale);
        public float HalfAngle => Mathf.Clamp(arcAngle,1,200)*Mathf.Deg2Rad*.5f;
        // Ribbon coordinates retain a normalized width parameter; world positions use angle, never flat X.
        public Vector3 ArcPoint(float coordinate, float height = 0)
        {
            if(!curvedTimeline) return new Vector3(coordinate,height,0);
            float angle=coordinate/width*2*HalfAngle;
            return new Vector3(Mathf.Sin(angle)*Radius,height,(Mathf.Cos(angle)-1)*Radius);
        }
        public float ArcCoordinate(Vector3 local)
        {
            if(!curvedTimeline) return Mathf.Clamp(local.x,-width*.5f,width*.5f);
            float angle=Mathf.Atan2(local.x,local.z+Radius);
            return Mathf.Clamp(angle,-HalfAngle,HalfAngle)/(2*HalfAngle)*width;
        }
        public float TimeOffset(float coordinate) => !curvedTimeline ? coordinate/width*visibleSeconds :
            coordinate/width*2*(coordinate<0?pastSeconds:futureSeconds);
        public float TransportY => -panelHeight*.5f-transportGap-transportHeight*.5f;
        public float LaneY(int row) => (1.5f-row)*spacing;
        public float WaveCenterLimit => Mathf.Max(.001f,laneHeight*.5f-thickness*1.8f-.003f);
        public Pose SeatedPose(Vector3 eye, Vector3 gaze)
        {
            var forward=Vector3.ProjectOnPlane(gaze,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f) forward=Vector3.forward;
            return new Pose(eye+forward*(curvedTimeline?Radius:workspaceDistance)-Vector3.up*workspaceEyeDrop,Quaternion.LookRotation(forward));
        }
    }
}
