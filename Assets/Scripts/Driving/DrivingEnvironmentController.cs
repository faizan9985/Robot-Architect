using System;
using UnityEngine;

namespace Driving
{
    /// <summary>Independent normalized training conditions. No dependency on menu visibility or vehicle reset.</summary>
    [DisallowMultipleComponent]
    public sealed class DrivingEnvironmentController : MonoBehaviour
    {
        [SerializeField] VehicleController vehicle;
        [Header("Exponential squared distance fog (URP shader fog)")]
        [SerializeField, Min(0f)] float clearFogDensity = 0f;
        [SerializeField, Min(0.001f)] float maximumFogDensity = 0.14f;
        [Tooltip("Limits maximum density so at least 1% scene transmission remains at this distance (meters).")]
        [SerializeField, Min(1f)] float minimumVisibleDistance = 15f;
        [SerializeField] Color fogColor = new Color(0.65f, 0.68f, 0.7f, 1f);
        public float LowVisibility { get; private set; }
        public float RoadSlipperiness { get; private set; }
        public event Action ConditionsChanged;
        bool originalFog;
        FogMode originalMode;
        Color originalColor;
        float originalDensity, originalStart, originalEnd;

        void Awake()
        {
            originalFog = RenderSettings.fog;
            originalMode = RenderSettings.fogMode;
            originalColor = RenderSettings.fogColor;
            originalDensity = RenderSettings.fogDensity;
            originalStart = RenderSettings.fogStartDistance;
            originalEnd = RenderSettings.fogEndDistance;
        }
        void OnEnable() { ApplyVisibility(); vehicle?.SetRoadSlipperiness(RoadSlipperiness); }
        void OnDisable() { RestoreFog(); vehicle?.SetRoadSlipperiness(0f); }
        public void SetLowVisibility(float value)
        {
            LowVisibility = Normalize(value);
            if (isActiveAndEnabled) ApplyVisibility();
            ConditionsChanged?.Invoke();
        }
        public void SetRoadSlipperiness(float value)
        {
            RoadSlipperiness = Normalize(value);
            if (isActiveAndEnabled) vehicle?.SetRoadSlipperiness(RoadSlipperiness);
            ConditionsChanged?.Invoke();
        }
        internal static float Normalize(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        void ApplyVisibility()
        {
            if (LowVisibility == 0f) { RestoreFog(); return; }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            float maximum = Mathf.Min(Mathf.Max(0.001f, maximumFogDensity), Mathf.Sqrt(-Mathf.Log(0.01f)) / Mathf.Max(1f, minimumVisibleDistance));
            RenderSettings.fogDensity = Mathf.Lerp(Mathf.Clamp(clearFogDensity, 0f, maximum), maximum, LowVisibility);
        }
        void RestoreFog()
        {
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalMode;
            RenderSettings.fogColor = originalColor;
            RenderSettings.fogDensity = originalDensity;
            RenderSettings.fogStartDistance = originalStart;
            RenderSettings.fogEndDistance = originalEnd;
        }
    }
}
