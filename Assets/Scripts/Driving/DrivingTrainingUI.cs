using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Driving
{
    /// <summary>Presentation only: modest live updates and an event-driven, in-memory session summary.</summary>
    [DisallowMultipleComponent]
    public sealed class DrivingTrainingUI : MonoBehaviour
    {
        [SerializeField] DrivingTelemetryManager telemetry;
        [SerializeField] HandMenuController handMenu;
        [SerializeField] Camera xrCamera;
        [SerializeField] Transform trackingSpace;
        [SerializeField] GameObject settingsPage;
        [SerializeField] GameObject trainingPage;
        [SerializeField] Button settingsTab;
        [SerializeField] Button trainingTab;
        [SerializeField] TMP_Text liveStats;
        [SerializeField] Button endSession;
        [SerializeField] Button resetSession;
        [SerializeField] Canvas summaryCanvas;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] Button newSession;
        [SerializeField] Button closeSummary;
        [SerializeField, Range(0.1f, 1f)] float refreshInterval = 0.2f;
        [SerializeField, Min(0.5f)] float summaryDistance = 1.1f;
        float nextRefresh;

        void Awake() { if (summaryCanvas != null) summaryCanvas.gameObject.SetActive(false); }
        void OnEnable()
        {
            if (telemetry == null) { if (trainingTab != null) trainingTab.gameObject.SetActive(false); return; }
            settingsTab.onClick.AddListener(ShowSettings);
            trainingTab.onClick.AddListener(ShowTraining);
            endSession.onClick.AddListener(telemetry.EndSession);
            resetSession.onClick.AddListener(telemetry.ResetSession);
            newSession.onClick.AddListener(telemetry.ResetSession);
            closeSummary.onClick.AddListener(HideSummary);
            telemetry.SessionEnded += ShowSummary;
            telemetry.SessionStarted += OnSessionStarted;
        }
        void OnDisable()
        {
            if (telemetry == null) return;
            settingsTab.onClick.RemoveListener(ShowSettings);
            trainingTab.onClick.RemoveListener(ShowTraining);
            endSession.onClick.RemoveListener(telemetry.EndSession);
            resetSession.onClick.RemoveListener(telemetry.ResetSession);
            newSession.onClick.RemoveListener(telemetry.ResetSession);
            closeSummary.onClick.RemoveListener(HideSummary);
            telemetry.SessionEnded -= ShowSummary;
            telemetry.SessionStarted -= OnSessionStarted;
            HideSummary();
        }
        void Update()
        {
            if (telemetry == null || !trainingPage.activeInHierarchy || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            RefreshLive();
        }
        void ShowSettings() { trainingPage.SetActive(false); settingsPage.SetActive(true); }
        void ShowTraining() { settingsPage.SetActive(false); trainingPage.SetActive(true); RefreshLive(); }
        void OnSessionStarted() { HideSummary(); nextRefresh = 0f; }
        void RefreshLive()
        {
            var s = telemetry.Statistics;
            liveStats.text = $"{(telemetry.IsSessionActive ? "Session running" : "Session ended")}\n\nTime: {s.duration:F1} s\nDistance: {s.distance:F1} m\nCollisions: {s.collisionCount}\nMax Speed: {s.maxSpeed * 3.6f:F1} km/h";
            endSession.interactable = telemetry.IsSessionActive;
        }
        void ShowSummary(DrivingSessionSummary s)
        {
            summaryText.text = $"Scenario: {ScenarioName(s.startingScenario)} to {ScenarioName(s.endingScenario)}\n" +
                $"Scenario changes: {s.scenarioChangeCount}\n\n" +
                $"Duration: {s.duration:F1} s\nDistance: {s.distance:F1} m\n" +
                $"Average speed: {s.averageSpeed * 3.6f:F1} km/h\nMax speed: {s.maxSpeed * 3.6f:F1} km/h\n" +
                $"Braking events: {s.brakingEvents}\nHard braking: {s.hardBrakingEvents}\n" +
                $"Collisions: {s.collisionCount}  (max {s.strongestCollision:F1} m/s)\n" +
                $"Off-road time: {s.offRoadTime:F1} s\n" +
                $"Average steering: {s.averageSteeringInput:P0}\nMax steering: {s.maxSteeringInput:P0}";
            // Capture one cockpit-relative pose. Never chase the headset every frame.
            var head = xrCamera.transform;
            var forward = Vector3.ProjectOnPlane(head.forward, trackingSpace.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = trackingSpace.forward;
            summaryCanvas.transform.SetParent(trackingSpace, true);
            summaryCanvas.transform.SetPositionAndRotation(head.position + forward * summaryDistance,
                Quaternion.LookRotation(forward, trackingSpace.up));
            summaryCanvas.worldCamera = xrCamera;
            handMenu.CloseMenu();
            summaryCanvas.gameObject.SetActive(true);
            RefreshLive();
        }
        public void HideSummary() { if (summaryCanvas != null) summaryCanvas.gameObject.SetActive(false); }
        void OnApplicationPause(bool paused) { if (paused) HideSummary(); }
        static string ScenarioName(DrivingScenarioType type) => type switch
        {
            DrivingScenarioType.Normal => "Normal Driving",
            DrivingScenarioType.DenseFog => "Dense Fog",
            DrivingScenarioType.IcyRoad => "Icy Road",
            DrivingScenarioType.WinterStorm => "Winter Storm",
            _ => "Custom"
        };
    }
}

