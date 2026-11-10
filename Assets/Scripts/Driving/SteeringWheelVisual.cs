using UnityEngine;

namespace Driving
{
    /// <summary>Only animates the cockpit wheel. Road-wheel poses are supplied by WheelVehicleDynamics.</summary>
    public sealed class SteeringWheelVisual : MonoBehaviour
    {
        [SerializeField] VehicleController vehicle;
        [SerializeField] Transform steeringWheel;
        [SerializeField, Range(0f,180f)] float wheelRotation=95f;
        Quaternion restingRotation;
        void Awake(){if(steeringWheel!=null)restingRotation=steeringWheel.localRotation;}
        void LateUpdate()
        {
            if(vehicle!=null&&steeringWheel!=null)steeringWheel.localRotation=restingRotation*Quaternion.AngleAxis(-vehicle.SteeringNormalized*wheelRotation,Vector3.forward);
        }
    }
}
