using UnityEngine;
using UnityEngine.InputSystem;

namespace Driving
{
    /// <summary>Owns driving input and a simple FWD powertrain. Tire/suspension forces belong to WheelVehicleDynamics.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(WheelVehicleDynamics)), DisallowMultipleComponent]
    public sealed class VehicleController : MonoBehaviour
    {
        [Header("Driving-only input actions (never modifies XRI bindings)")]
        [SerializeField] InputActionReference accelerateInput;
        [SerializeField] InputActionReference brakeReverseInput;
        [SerializeField] InputActionReference steeringInput;
        [SerializeField] InputActionReference keyboardAccelerate;
        [SerializeField] InputActionReference keyboardBrakeReverse;
        [SerializeField] InputActionReference keyboardSteer;
        [SerializeField] bool keyboardFallbackInEditor = true;
        [SerializeField] HandMenuController handMenu;
        [SerializeField] DriverSeatController driverSeat;
        [Header("Simple front-wheel-drive powertrain, SI units")]
        [SerializeField, Min(1f)] float maxForwardSpeed = 30f;
        [SerializeField, Min(.5f)] float maxReverseSpeed = 2f;
        [SerializeField, Min(0f)] float maximumTorquePerDrivenWheel = 700f;
        [Tooltip("Total mechanical power available at both driven wheels, in watts.")]
        [SerializeField, Min(1000f)] float maximumWheelPower = 60000f;
        [SerializeField, Min(0f)] float reverseTorquePerWheel = 200f;
        [SerializeField, Min(0f)] float maximumBrakeTorqueTotal = 4600f;
        [SerializeField, Range(.5f,.8f)] float frontBrakeBias = .65f;
        [SerializeField, Min(0f)] float engineBrakingTorquePerWheel = 15f;
        [SerializeField, Min(.1f)] float steeringResponsiveness = 1.5f;
        Rigidbody body;
        WheelVehicleDynamics dynamics;
        Vector3 spawnPosition, previousVelocity;
        Quaternion spawnRotation;
        float throttle, brake, steering, smoothedThrottle, reverseHoldTime;
        bool focused=true, paused, waitForRelease, safetyBraking;
        public float RoadSlipperiness { get; private set; }
        public float SteeringNormalized { get; private set; }
        public float DriverBrakeInput { get; private set; }
        public float DriverThrottleInput => throttle;
        public float DriverSteeringInput => steering;
        public Vector3 PhysicsPosition => body == null ? transform.position : body.position;
        public float HorizontalSpeed => body == null ? 0f : Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude;
        public float ForwardSpeed => body == null ? 0f : Vector3.Dot(body.linearVelocity,transform.forward);
        public bool IsGrounded => dynamics != null && dynamics.GroundedCount>=2;
        public float MaxForwardSpeed => maxForwardSpeed;
        public float MaxReverseSpeed => maxReverseSpeed;
        public float LateralAcceleration { get; private set; }
        public float LongitudinalAcceleration { get; private set; }
        public float YawRate => body == null ? 0f : Vector3.Dot(body.angularVelocity,transform.up);
        public float AverageFrontSlip => dynamics == null ? 0f : dynamics.AverageFrontSlip;
        public float AverageRearSlip => dynamics == null ? 0f : dynamics.AverageRearSlip;
        public float MaximumSidewaysSlip => dynamics == null ? 0f : dynamics.MaximumSidewaysSlip;
        public event System.Action VehicleReset;
        public void SetRoadSlipperiness(float value)
        {
            RoadSlipperiness=DrivingEnvironmentController.Normalize(value);
            if (dynamics != null) dynamics.SetRoadSlipperiness(RoadSlipperiness);
        }
        void Awake()
        {
            body=GetComponent<Rigidbody>(); dynamics=GetComponent<WheelVehicleDynamics>();
            spawnPosition=body.position; spawnRotation=body.rotation;
            dynamics.SetRoadSlipperiness(RoadSlipperiness);
        }
        void OnEnable() { foreach(var reference in Inputs()) reference?.action?.Enable(); waitForRelease=true; }
        void OnDisable() { foreach(var reference in Inputs()) reference?.action?.Disable(); ClearInput(); if(dynamics!=null) dynamics.ResetWheelState(); }
        InputActionReference[] Inputs() => new[]{accelerateInput,brakeReverseInput,steeringInput,keyboardAccelerate,keyboardBrakeReverse,keyboardSteer};
        void Update()
        {
            float gas=Read(accelerateInput), stop=Read(brakeReverseInput);
            var axis=steeringInput?.action?.ReadValue<Vector2>()??Vector2.zero; float turn=axis.x;
            if(keyboardFallbackInEditor && Application.isEditor)
            {
                gas=Mathf.Max(gas,Read(keyboardAccelerate));stop=Mathf.Max(stop,Read(keyboardBrakeReverse));
                float keyTurn=Read(keyboardSteer);if(Mathf.Abs(keyTurn)>Mathf.Abs(turn))turn=keyTurn;
            }
            bool blocked=!focused||paused||(handMenu!=null&&handMenu.IsMenuOpen);
            if(blocked)waitForRelease=true;
            if(waitForRelease&&gas<.05f&&stop<.05f&&Mathf.Abs(turn)<.1f&&!blocked)waitForRelease=false;
            safetyBraking=blocked||waitForRelease;
            DriverBrakeInput=safetyBraking?0f:Mathf.Clamp01(stop);
            throttle=safetyBraking?0f:Mathf.Clamp01(gas);
            brake=safetyBraking?1f:Mathf.Clamp01(stop);
            steering=safetyBraking?0f:Mathf.Clamp(turn,-1f,1f);
        }
        static float Read(InputActionReference reference)=>reference?.action?.ReadValue<float>()??0f;
        void FixedUpdate()
        {
            if(dynamics==null||!dynamics.IsConfigured)return;
            float dt=Time.fixedDeltaTime;
            var acceleration=(body.linearVelocity-previousVelocity)/dt; previousVelocity=body.linearVelocity;
            LateralAcceleration=Vector3.Dot(acceleration,transform.right);LongitudinalAcceleration=Vector3.Dot(acceleration,transform.forward);
            SteeringNormalized=Mathf.MoveTowards(SteeringNormalized,steering,steeringResponsiveness*dt);
            smoothedThrottle=Mathf.MoveTowards(smoothedThrottle,throttle,dt*2f);
            float forward=ForwardSpeed, motor=0f, serviceBrake=0f;
            if(safetyBraking) { serviceBrake=1f; reverseHoldTime=0f; }
            else if(brake>.05f)
            {
                if(forward>.3f || (HorizontalSpeed>.5f && forward>=-.3f) || throttle>.05f) {serviceBrake=brake;reverseHoldTime=0f;}
                else
                {
                    reverseHoldTime+=dt;
                    if(reverseHoldTime>=.4f)motor=-reverseTorquePerWheel*brake*SpeedTaper(Mathf.Max(Mathf.Max(0f,-forward),dynamics.DrivenWheelSurfaceSpeed),maxReverseSpeed);
                    else serviceBrake=brake;
                }
            }
            else
            {
                reverseHoldTime=0f;
                if(forward<-.3f&&throttle>.05f)serviceBrake=throttle;
                else
                {
                    float torque=Mathf.Min(maximumTorquePerDrivenWheel,maximumWheelPower*dynamics.WheelRadius/(2f*Mathf.Max(1f,Mathf.Max(Mathf.Abs(forward),dynamics.DrivenWheelSurfaceSpeed))));
                    motor=smoothedThrottle*torque*SpeedTaper(Mathf.Max(Mathf.Max(0f,forward),dynamics.DrivenWheelSurfaceSpeed),maxForwardSpeed);
                }
            }
            dynamics.Step(SteeringNormalized,HorizontalSpeed,motor,serviceBrake*maximumBrakeTorqueTotal,frontBrakeBias,
                motor==0f?engineBrakingTorquePerWheel:0f);
        }
        static float SpeedTaper(float speed,float maximum)=>1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(maximum*.95f,maximum,speed));
        public void ResetVehicle()
        {
            body.position=spawnPosition;body.rotation=spawnRotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            transform.SetPositionAndRotation(spawnPosition,spawnRotation);
            ClearInput();waitForRelease=true;previousVelocity=Vector3.zero;LateralAcceleration=LongitudinalAcceleration=0f;
            dynamics.ResetWheelState();body.WakeUp();Physics.SyncTransforms();
            if(driverSeat!=null)driverSeat.RecenterSeat();VehicleReset?.Invoke();
        }
        void ClearInput() {DriverBrakeInput=0f;throttle=brake=steering=smoothedThrottle=reverseHoldTime=0f;SteeringNormalized=0f;safetyBraking=true;}
        void OnApplicationFocus(bool value){focused=value;if(!value){ClearInput();waitForRelease=true;}}
        void OnApplicationPause(bool value){paused=value;if(value){ClearInput();waitForRelease=true;}}
    }
}

