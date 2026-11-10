using TMPro;
using UnityEngine;

namespace Driving
{
    /// <summary>Live vehicle speed, independent of the training-session lifecycle. Presentation is always km/h.</summary>
    [DisallowMultipleComponent]
    public sealed class DashboardSpeedometer : MonoBehaviour
    {
        [SerializeField] VehicleController vehicle;
        [SerializeField] TMP_Text speedText;
        [SerializeField, Range(0.05f, 0.5f)] float refreshInterval = 0.1f;
        float nextRefresh;
        int displayedSpeed = -1;
        void OnEnable() { displayedSpeed = -1; Refresh(); }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            Refresh();
        }
        void Refresh()
        {
            if (vehicle == null || speedText == null) return;
            int speed = Mathf.RoundToInt(vehicle.HorizontalSpeed * 3.6f);
            if (speed == displayedSpeed) return;
            speedText.SetText("{0:0}", speed);
            displayedSpeed = speed;
        }
    }
}
