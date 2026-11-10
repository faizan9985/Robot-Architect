using System;
using UnityEngine;

namespace Driving
{
    /// <summary>Observes the existing vehicle. Does not control input, physics, environment or resets.</summary>
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class DrivingTelemetryManager : MonoBehaviour
    {
        [SerializeField] VehicleController vehicle;
        [SerializeField] DrivingScenarioManager scenarios;
        [SerializeField] RoadSurfaceTracker road;
        [SerializeField] bool autoStart = true;
        [Header("Metric thresholds (SI units)")]
        [SerializeField, Min(0.01f)] float movementThreshold = 0.2f;
        [SerializeField, Range(0.01f, 1f)] float brakePressThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] float brakeReleaseThreshold = 0.1f;
        [SerializeField, Min(0.1f)] float hardBrakingDeceleration = 3f;
        [SerializeField, Min(0.1f)] float minimumCollisionSpeed = 0.8f;
        [SerializeField, Min(0f)] float collisionCooldown = 0.75f;
        public bool IsSessionActive { get; private set; }
        public bool HasCompletedSession { get; private set; }
        public DrivingSessionSummary CompletedSummary { get; private set; }
        public DrivingSessionSummary Statistics => statistics;
        public event Action SessionStarted;
        public event Action<DrivingSessionSummary> SessionEnded;
        DrivingSessionSummary statistics;
        float movingDuration, steeringIntegral, sampledDuration, previousSpeed;
        bool movementStarted, brakeHeld, brakingEvent, hardBrakeCounted, hasPreviousSpeed;
        bool paused, focused = true;
        double lastCollisionTime = double.NegativeInfinity;
        DrivingScenarioType CurrentScenario => scenarios != null ? scenarios.ActiveScenario : DrivingScenarioType.Custom;

        void OnEnable()
        {
            if (scenarios != null) scenarios.ScenarioChanged += OnScenarioChanged;
            if (vehicle != null) vehicle.VehicleReset += OnVehicleReset;
            OnScenarioChanged();
        }
        void OnDisable()
        {
            if (scenarios != null) scenarios.ScenarioChanged -= OnScenarioChanged;
            if (vehicle != null) vehicle.VehicleReset -= OnVehicleReset;
        }
        void Start() { if (autoStart && !IsSessionActive) StartSession(); }
        void Update()
        {
            if (IsSessionActive && !paused && focused) statistics.duration += Time.unscaledDeltaTime;
        }
        void FixedUpdate()
        {
            if (!IsSessionActive || paused || !focused || vehicle == null) return;
            Observe(Time.fixedDeltaTime, vehicle.HorizontalSpeed, vehicle.ForwardSpeed,
                vehicle.DriverBrakeInput, vehicle.DriverSteeringInput,
                road == null || road.IsOnRoad(vehicle.PhysicsPosition));
        }
        public void StartSession()
        {
            statistics = new DrivingSessionSummary { startingScenario = CurrentScenario, endingScenario = CurrentScenario };
            movingDuration = steeringIntegral = sampledDuration = previousSpeed = 0f;
            movementStarted = brakeHeld = brakingEvent = hardBrakeCounted = hasPreviousSpeed = false;
            lastCollisionTime = double.NegativeInfinity;
            CompletedSummary = default;
            HasCompletedSession = false;
            IsSessionActive = true;
            SessionStarted?.Invoke();
        }
        public void ResetSession() => StartSession();
        public void EndSession()
        {
            if (!IsSessionActive) return;
            OnScenarioChanged();
            IsSessionActive = false;
            CompletedSummary = statistics;
            HasCompletedSession = true;
            SessionEnded?.Invoke(CompletedSummary);
        }
        // One fixed-step observation. Separating accumulation makes definitions testable without driving physics.
        internal void Observe(float dt, float speed, float forwardSpeed, float brakeInput, float steeringInput, bool onRoad)
        {
            if (!IsSessionActive || dt <= 0f) return;
            speed = Mathf.Max(0f, speed);
            statistics.currentSpeed = speed;
            statistics.maxSpeed = Mathf.Max(statistics.maxSpeed, speed);
            if (speed >= movementThreshold) movementStarted = true;
            if (movementStarted)
            {
                statistics.distance += speed * dt;
                movingDuration += dt;
                statistics.averageSpeed = statistics.distance / movingDuration;
            }
            sampledDuration += dt;
            float steering = Mathf.Clamp01(Mathf.Abs(steeringInput));
            steeringIntegral += steering * dt;
            statistics.averageSteeringInput = steeringIntegral / sampledDuration;
            statistics.maxSteeringInput = Mathf.Max(statistics.maxSteeringInput, steering);
            if (!onRoad) statistics.offRoadTime += dt;

            float releaseThreshold = Mathf.Min(brakeReleaseThreshold, brakePressThreshold * 0.9f);
            if (!brakeHeld && brakeInput >= brakePressThreshold)
            {
                brakeHeld = true;
                // This control also drives reverse; a reverse-only press is not a braking event.
                brakingEvent = forwardSpeed >= movementThreshold;
                hardBrakeCounted = false;
                if (brakingEvent) statistics.brakingEvents++;
            }
            if (brakeHeld && brakingEvent && !hardBrakeCounted && hasPreviousSpeed &&
                brakeInput > releaseThreshold && (previousSpeed - speed) / dt >= hardBrakingDeceleration)
            {
                statistics.hardBrakingEvents++;
                hardBrakeCounted = true;
            }
            if (brakeInput <= releaseThreshold) brakeHeld = brakingEvent = hardBrakeCounted = false;
            previousSpeed = speed;
            hasPreviousSpeed = true;
        }
        public void RecordCollision(float impactSpeed)
        {
            if (!IsSessionActive || paused || !focused || !float.IsFinite(impactSpeed) || impactSpeed < minimumCollisionSpeed) return;
            statistics.strongestCollision = Mathf.Max(statistics.strongestCollision, impactSpeed);
            if (Time.unscaledTimeAsDouble - lastCollisionTime < collisionCooldown) return;
            statistics.collisionCount++;
            lastCollisionTime = Time.unscaledTimeAsDouble;
        }
        void OnScenarioChanged()
        {
            if (!IsSessionActive || statistics.endingScenario == CurrentScenario) return;
            statistics.endingScenario = CurrentScenario;
            statistics.scenarioChangeCount++;
        }
        void OnVehicleReset()
        {
            // Velocity integration excludes teleport distance. Clear deceleration history without erasing totals.
            hasPreviousSpeed = false;
            brakeHeld = brakingEvent = hardBrakeCounted = false;
        }
        void OnApplicationPause(bool value) { paused = value; hasPreviousSpeed = false; }
        void OnApplicationFocus(bool value) { focused = value; hasPreviousSpeed = false; }
    }
}

