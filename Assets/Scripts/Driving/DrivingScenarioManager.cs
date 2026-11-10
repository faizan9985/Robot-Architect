using System;
using UnityEngine;

namespace Driving
{
    public enum DrivingScenarioType { Normal, DenseFog, IcyRoad, WinterStorm, Custom }

    [Serializable]
    public sealed class DrivingScenarioPreset
    {
        public DrivingScenarioType id;
        public string name;
        [Range(0f, 1f)] public float lowVisibility;
        [Range(0f, 1f)] public float roadSlipperiness;

        public DrivingScenarioPreset(DrivingScenarioType id, string name, float visibility, float slipperiness)
        { this.id = id; this.name = name; lowVisibility = visibility; roadSlipperiness = slipperiness; }
    }

    /// <summary>Preset selection only. Environment controller owns fog and traction; vehicle resets are independent.</summary>
    [DisallowMultipleComponent]
    public sealed class DrivingScenarioManager : MonoBehaviour
    {
        [SerializeField] DrivingEnvironmentController environment;
        [SerializeField] DrivingScenarioPreset[] presets = {
            new DrivingScenarioPreset(DrivingScenarioType.Normal, "Normal Driving", 0f, 0f),
            new DrivingScenarioPreset(DrivingScenarioType.DenseFog, "Dense Fog", 0.8f, 0f),
            new DrivingScenarioPreset(DrivingScenarioType.IcyRoad, "Icy Road", 0f, 0.9f),
            new DrivingScenarioPreset(DrivingScenarioType.WinterStorm, "Winter Storm", 0.7f, 0.8f)
        };
        public DrivingScenarioType ActiveScenario { get; private set; } = DrivingScenarioType.Normal;
        public event Action ScenarioChanged;
        bool applyingPreset;

        void OnEnable() { if (environment != null) environment.ConditionsChanged += OnConditionsChanged; }
        void Start() => ApplyScenario(DrivingScenarioType.Normal);
        void OnDisable() { if (environment != null) environment.ConditionsChanged -= OnConditionsChanged; }

        public void ApplyScenario(DrivingScenarioType scenario)
        {
            if (scenario == DrivingScenarioType.Custom) { SetCustomMode(); return; }
            if (environment == null) return;
            var preset = Array.Find(presets, p => p != null && p.id == scenario);
            if (preset == null) { Debug.LogWarning($"No driving preset configured for {scenario}.", this); return; }
            applyingPreset = true;
            try
            {
                environment.SetLowVisibility(preset.lowVisibility);
                environment.SetRoadSlipperiness(preset.roadSlipperiness);
                ActiveScenario = scenario;
            }
            finally { applyingPreset = false; }
            ScenarioChanged?.Invoke();
        }

        public void SetCustomMode()
        {
            if (ActiveScenario == DrivingScenarioType.Custom) return;
            ActiveScenario = DrivingScenarioType.Custom;
            ScenarioChanged?.Invoke();
        }
        void OnConditionsChanged() { if (!applyingPreset) SetCustomMode(); }
    }
}
