using UnityEngine;

namespace Driving
{
    /// <summary>Four-wheel suspension/tire dynamics. WheelCollider alone supplies tire contact forces.</summary>
    [DefaultExecutionOrder(-100), RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent]
    public sealed class WheelVehicleDynamics : MonoBehaviour
    {
        [Header("FL, FR, RL, RR; authored in the reusable prefab")]
        [SerializeField] WheelCollider[] wheels = new WheelCollider[4];
        [SerializeField] Transform[] visualWheels = new Transform[4];
        [SerializeField] Transform centerOfMassReference;
        [Header("Compact sedan: SI units")]
        [SerializeField, Min(100f)] float mass = 1350f;
        [SerializeField, Min(1f)] float wheelbase = 2.7f;
        [SerializeField, Min(0.5f)] float trackWidth = 1.55f;
        [SerializeField, Range(0.4f, 0.7f)] float frontWeightFraction = 0.6f;
        [SerializeField, Min(0.1f)] float wheelRadius = 0.32f;
        [SerializeField, Min(0.1f)] float nominalRideHeight = 0.42f;
        [SerializeField, Min(0.1f)] float centerOfGravityHeight = 0.53f;
        [Tooltip("Approximate mass envelope for box-derived inertia; visual cockpit tracking is independent.")]
        [SerializeField] Vector3 inertiaDimensions = new Vector3(1.8f, 1.43f, 4.48f);
        [Header("Suspension: derive k=(2*pi*f)^2*m, c=2*zeta*sqrt(k*m)")]
        [SerializeField, Range(1f, 2.5f)] float frontNaturalFrequency = 1.55f;
        [SerializeField, Range(1f, 2.5f)] float rearNaturalFrequency = 1.65f;
        [SerializeField, Range(0.3f, 1.2f)] float dampingRatio = 0.85f;
        [SerializeField, Min(0.1f)] float suspensionTravel = 0.24f;
        [SerializeField, Range(0f, 1f)] float suspensionTargetPosition = 0.5f;
        [SerializeField, Min(0f)] float frontAntiRollStiffness = 6000f;
        [SerializeField, Min(0f)] float rearAntiRollStiffness = 3500f;
        [Tooltip("Meters above nominal tire contact. Low application point preserves physical roll moment.")]
        [SerializeField, Min(0f)] float forceApplicationHeight = 0.05f;
        [Header("Slip curves: peak followed by lower sliding grip")]
        [SerializeField] WheelFrictionCurve forwardCurve = new WheelFrictionCurve { extremumSlip = 0.12f, extremumValue = 1f, asymptoteSlip = 0.6f, asymptoteValue = 0.75f, stiffness = 0.95f };
        [SerializeField] WheelFrictionCurve sidewaysCurve = new WheelFrictionCurve { extremumSlip = 0.18f, extremumValue = 1f, asymptoteSlip = 0.7f, asymptoteValue = 0.75f, stiffness = 0.9f };
        [SerializeField, Range(0.1f, 1.5f)] float frontGripBalance = 0.98f;
        [SerializeField, Range(0.1f, 1.5f)] float rearGripBalance = 1.03f;
        [SerializeField] bool combinedSlipBudget = true;
        [SerializeField, Range(0f, 0.3f)] float loadSensitivity = 0.12f;
        [SerializeField, Range(0.02f, 0.3f)] float iceGripMultiplier = 0.08f;
        [SerializeField, Range(0.5f, 3f)] float slipperinessExponent = 1.5f;
        [Header("Ackermann steering")]
        [SerializeField, Range(15f, 45f)] float lowSpeedSteeringAngle = 32f;
        [SerializeField, Range(5f, 25f)] float highSpeedSteeringAngle = 10f;
        [SerializeField, Min(1f)] float fullSteeringReductionSpeed = 30f;
        [Header("Road loads and vehicle-specific substeps")]
        [SerializeField, Min(0f)] float dragArea = 0.66f;
        [SerializeField, Range(0f, 0.04f)] float rollingResistanceCoefficient = 0.012f;
        [SerializeField, Min(1f)] float substepSpeedThreshold = 5f;
        [SerializeField, Range(1, 8)] int lowSpeedSubsteps = 5;
        [SerializeField, Range(1, 8)] int highSpeedSubsteps = 3;
        Rigidbody body;
        readonly WheelHit[] hits = new WheelHit[4];
        readonly bool[] grounded = new bool[4];
        readonly float[] compression = new float[4];
        public float WheelRadius => wheelRadius;
        public float DrivenWheelSurfaceSpeed => !IsConfigured ? 0f : (Mathf.Abs(wheels[0].rpm)+Mathf.Abs(wheels[1].rpm))*.5f*(2f*Mathf.PI*wheelRadius/60f);
        public bool IsConfigured { get; private set; }
        public int GroundedCount { get; private set; }
        public float GripMultiplier { get; private set; } = 1f;
        public float StaticStabilityFactor => trackWidth / (2f * centerOfGravityHeight);
        public float AverageFrontSlip { get; private set; }
        public float AverageRearSlip { get; private set; }
        public float MaximumSidewaysSlip { get; private set; }
        public float AverageForwardSlip { get; private set; }
        public float FrontSpringRate => SpringRate(mass * frontWeightFraction * 0.5f, frontNaturalFrequency);
        public float RearSpringRate => SpringRate(mass * (1f-frontWeightFraction) * 0.5f, rearNaturalFrequency);
        public float FrontDamperRate => 2f * dampingRatio * Mathf.Sqrt(FrontSpringRate * mass * frontWeightFraction * 0.5f);
        public float RearDamperRate => 2f * dampingRatio * Mathf.Sqrt(RearSpringRate * mass * (1f-frontWeightFraction) * 0.5f);
        public float WheelLoad(int index) => grounded[index] ? hits[index].force : 0f;
        public float SuspensionCompression(int index) => compression[index];
        public float SidewaysSlip(int index) => grounded[index] ? hits[index].sidewaysSlip : 0f;
        public float ForwardSlip(int index) => grounded[index] ? hits[index].forwardSlip : 0f;
        public float SteerAngle(int index) => wheels[index].steerAngle;
        static float SpringRate(float sprungMass, float frequency) => Mathf.Pow(2f * Mathf.PI * frequency, 2f) * sprungMass;
        void Awake() => Configure();
        public void Configure()
        {
            body = GetComponent<Rigidbody>();
            IsConfigured = wheels.Length == 4 && visualWheels.Length == 4;
            for (int i=0; i<4 && IsConfigured; i++) IsConfigured &= wheels[i] != null && visualWheels[i] != null;
            if (!IsConfigured) { Debug.LogError("WheelVehicleDynamics requires FL/FR/RL/RR colliders and visual wheels.", this); return; }
            body.mass = mass; body.useGravity = true; body.constraints = RigidbodyConstraints.None;
            body.linearDamping = 0f; body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 cg = new Vector3(0f, centerOfGravityHeight - nominalRideHeight, (frontWeightFraction-0.5f)*wheelbase);
            if (centerOfMassReference != null) { centerOfMassReference.localPosition = cg; body.centerOfMass = transform.InverseTransformPoint(centerOfMassReference.position); }
            else body.centerOfMass = cg;
            var d = inertiaDimensions;
            body.inertiaTensorRotation = Quaternion.identity;
            body.inertiaTensor = mass/12f * new Vector3(d.y*d.y+d.z*d.z, d.x*d.x+d.z*d.z, d.x*d.x+d.y*d.y);
            for (int i=0; i<4; i++)
            {
                bool front = i<2; float sprung = mass * (front ? frontWeightFraction : 1f-frontWeightFraction) * 0.5f;
                float k = front ? FrontSpringRate : RearSpringRate;
                var w = wheels[i];
                w.transform.localPosition = new Vector3((i%2==0 ? -1f : 1f)*trackWidth*.5f,
                    wheelRadius-nominalRideHeight+suspensionTravel*(1f-suspensionTargetPosition), front ? wheelbase*.5f : -wheelbase*.5f);
                w.transform.localRotation = Quaternion.identity;
                w.radius = wheelRadius; w.mass = 20f; w.suspensionDistance = suspensionTravel;
                w.sprungMass = sprung; w.wheelDampingRate = 0.25f; w.forceAppPointDistance = forceApplicationHeight;
                w.suspensionSpring = new JointSpring { spring = k, damper = front ? FrontDamperRate : RearDamperRate, targetPosition = suspensionTargetPosition };
                w.forwardFriction = forwardCurve; w.sidewaysFriction = sidewaysCurve;
            }
            wheels[0].ConfigureVehicleSubsteps(substepSpeedThreshold, lowSpeedSubsteps, highSpeedSubsteps);
        }
        public void SetRoadSlipperiness(float value)
        {
            GripMultiplier = Mathf.Lerp(iceGripMultiplier, 1f, Mathf.Pow(1f-DrivingEnvironmentController.Normalize(value), slipperinessExponent));
        }
        public void Step(float steering, float speed, float frontMotorTorque, float brakeTorqueTotal, float frontBrakeBias, float engineBrakingTorque)
        {
            if (!IsConfigured) return;
            SampleContacts();
            float limit = Mathf.Lerp(lowSpeedSteeringAngle, highSpeedSteeringAngle, Mathf.SmoothStep(0f, 1f, speed/fullSteeringReductionSpeed));
            float centerAngle = steering * limit;
            float radius = Mathf.Abs(centerAngle) < 0.01f ? float.PositiveInfinity : wheelbase / Mathf.Tan(Mathf.Abs(centerAngle)*Mathf.Deg2Rad);
            float inner = Mathf.Atan(wheelbase/Mathf.Max(.1f, radius-trackWidth*.5f))*Mathf.Rad2Deg;
            float outer = Mathf.Atan(wheelbase/(radius+trackWidth*.5f))*Mathf.Rad2Deg;
            wheels[0].steerAngle = centerAngle > 0f ? outer : -inner;
            wheels[1].steerAngle = centerAngle > 0f ? inner : -outer;
            wheels[2].steerAngle = wheels[3].steerAngle = 0f;
            for (int i=0; i<4; i++)
            {
                wheels[i].motorTorque = i<2 ? frontMotorTorque : 0f;
                wheels[i].brakeTorque = brakeTorqueTotal*(i<2 ? frontBrakeBias : 1f-frontBrakeBias)*.5f + (i<2 ? engineBrakingTorque : 0f);
                float nominalLoad = wheels[i].sprungMass * Physics.gravity.magnitude;
                float loadScale = grounded[i] ? Mathf.Pow(Mathf.Clamp(hits[i].force/nominalLoad,.25f,4f),-loadSensitivity) : 1f;
                float budget = 1f;
                if (combinedSlipBudget && grounded[i])
                {
                    float longitudinal = CurveDemand(Mathf.Abs(hits[i].forwardSlip), forwardCurve);
                    float lateral = CurveDemand(Mathf.Abs(hits[i].sidewaysSlip), sidewaysCurve);
                    budget = 1f / Mathf.Max(1f, Mathf.Sqrt(longitudinal*longitudinal + lateral*lateral));
                }
                var forward = forwardCurve; forward.stiffness *= GripMultiplier * loadScale * budget;
                var side = sidewaysCurve; side.stiffness *= GripMultiplier * loadScale * budget * (i<2 ? frontGripBalance : rearGripBalance);
                wheels[i].forwardFriction = forward; wheels[i].sidewaysFriction = side;
            }
            AntiRoll(0, 1, frontAntiRollStiffness); AntiRoll(2, 3, rearAntiRollStiffness);
            var velocity = body.linearVelocity;
            body.AddForce(-0.5f*1.225f*dragArea*velocity.magnitude*velocity, ForceMode.Force);
            var roadVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            if (GroundedCount>0 && roadVelocity.sqrMagnitude>0.0025f)
                body.AddForce(-roadVelocity.normalized*Mathf.Min(rollingResistanceCoefficient*mass*Physics.gravity.magnitude, mass*roadVelocity.magnitude/Time.fixedDeltaTime), ForceMode.Force);
        }
        static float CurveDemand(float slip, WheelFrictionCurve curve)
        {
            if (slip < curve.extremumSlip) return slip/Mathf.Max(.001f,curve.extremumSlip);
            return Mathf.Lerp(1f,curve.asymptoteValue/Mathf.Max(.001f,curve.extremumValue),Mathf.InverseLerp(curve.extremumSlip,curve.asymptoteSlip,slip));
        }
        void SampleContacts()
        {
            GroundedCount=0; AverageFrontSlip=AverageRearSlip=MaximumSidewaysSlip=AverageForwardSlip=0f;
            for (int i=0; i<4; i++)
            {
                grounded[i] = wheels[i].GetGroundHit(out hits[i]);
                wheels[i].GetWorldPose(out var position, out _);
                float extension = -wheels[i].transform.InverseTransformPoint(position).y;
                compression[i] = grounded[i] ? Mathf.Clamp(suspensionTravel-extension,0f,suspensionTravel) : 0f;
                if (!grounded[i]) continue;
                GroundedCount++;
                float slip = Mathf.Abs(hits[i].sidewaysSlip);
                if (i<2) AverageFrontSlip+=slip*.5f; else AverageRearSlip+=slip*.5f;
                MaximumSidewaysSlip=Mathf.Max(MaximumSidewaysSlip,slip);
                AverageForwardSlip+=Mathf.Abs(hits[i].forwardSlip)*.25f;
            }
        }
        void AntiRoll(int left, int right, float stiffness)
        {
            if (!grounded[left] || !grounded[right]) return;
            float limit = .5f*Mathf.Min(hits[left].force,hits[right].force);
            float force = Mathf.Clamp((compression[left]-compression[right])*stiffness,-limit,limit);
            body.AddForceAtPosition(transform.up*force,wheels[left].transform.position);
            body.AddForceAtPosition(-transform.up*force,wheels[right].transform.position);
        }
        void LateUpdate() => SyncVisuals();
        public void SyncVisuals()
        {
            if (!IsConfigured) return;
            for (int i=0; i<4; i++) { wheels[i].GetWorldPose(out var p,out var r); visualWheels[i].SetPositionAndRotation(p,r); }
        }
        public void ResetWheelState()
        {
            if (!IsConfigured) return;
            GroundedCount=0;
            for (int i=0; i<4; i++) { wheels[i].motorTorque=0f; wheels[i].brakeTorque=0f; wheels[i].steerAngle=0f; wheels[i].enabled=false; wheels[i].enabled=true; grounded[i]=false; compression[i]=0f; }
            AverageFrontSlip=AverageRearSlip=MaximumSidewaysSlip=AverageForwardSlip=0f;
            SyncVisuals();
        }
        void OnDisable()
        {
            if (!IsConfigured) return;
            foreach (var wheel in wheels) { wheel.motorTorque=0f; wheel.brakeTorque=0f; }
        }
    }
}

