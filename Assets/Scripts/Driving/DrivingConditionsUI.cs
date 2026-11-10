using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Driving
{
    /// <summary>Optional menu section; existing hand-menu behavior remains independent.</summary>
    public sealed class DrivingConditionsUI : MonoBehaviour
    {
        [SerializeField] DrivingEnvironmentController environment;
        [SerializeField] GameObject section;
        [SerializeField] Slider visibilitySlider;
        [SerializeField] Slider slipperinessSlider;
        [SerializeField] TMP_Text visibilityLabel;
        [SerializeField] TMP_Text slipperinessLabel;
        void OnEnable()
        {
            if (section != null) section.SetActive(environment != null);
            if (environment == null) return;
            Configure(visibilitySlider); Configure(slipperinessSlider);
            visibilitySlider.onValueChanged.AddListener(environment.SetLowVisibility);
            slipperinessSlider.onValueChanged.AddListener(environment.SetRoadSlipperiness);
            environment.ConditionsChanged += Refresh;
            Refresh();
        }
        void OnDisable()
        {
            if (environment == null) return;
            visibilitySlider.onValueChanged.RemoveListener(environment.SetLowVisibility);
            slipperinessSlider.onValueChanged.RemoveListener(environment.SetRoadSlipperiness);
            environment.ConditionsChanged -= Refresh;
        }
        static void Configure(Slider slider) { slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false; }
        public void Refresh()
        {
            if (environment == null) return;
            visibilitySlider.SetValueWithoutNotify(environment.LowVisibility);
            slipperinessSlider.SetValueWithoutNotify(environment.RoadSlipperiness);
            visibilityLabel.text = $"Low Visibility: {Mathf.RoundToInt(environment.LowVisibility * 100f)}%";
            slipperinessLabel.text = $"Slippery Roads: {Mathf.RoundToInt(environment.RoadSlipperiness * 100f)}%";
        }
    }
}
