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
    }
}
