using System;

namespace Driving
{
    /// <summary>Value snapshot in SI units. Contains no scene objects or persistence behavior.</summary>
    [Serializable]
    public struct DrivingSessionSummary
    {
        public float duration;
        public float distance;
        public float currentSpeed;
        public float maxSpeed;
        public float averageSpeed;
        public int brakingEvents;
        public int hardBrakingEvents;
        public int collisionCount;
        public float strongestCollision;
        public float offRoadTime;
        public float averageSteeringInput;
        public float maxSteeringInput;
        public DrivingScenarioType startingScenario;
        public DrivingScenarioType endingScenario;
        public int scenarioChangeCount;
    }
}
