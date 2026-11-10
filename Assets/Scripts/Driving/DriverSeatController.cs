using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace Driving
{
    /// <summary>Calibrates the origin, never the tracked camera. Vehicle-space parenting carries every tracked device.</summary>
    [DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class DriverSeatController : MonoBehaviour
    {
        [SerializeField] XROrigin xrOrigin;
        [SerializeField] Transform vehicleSpace;
        [Tooltip("Desired eye position, not the seat cushion.")]
        [SerializeField] Transform driverSeatAnchor;
        [SerializeField] Vector3 seatOffset;
        [Tooltip("Existing locomotion components, mediator, body transformer and character controller only.")]
        [SerializeField] Behaviour[] seatedMovementBlockers = System.Array.Empty<Behaviour>();
        bool[] previousEnabled;
        bool awaitingTrackedHead;
        CharacterController characterController;
        bool characterWasEnabled;

        void OnEnable()
        {
            previousEnabled = new bool[seatedMovementBlockers.Length];
            for (int i = 0; i < seatedMovementBlockers.Length; i++)
            {
                var component = seatedMovementBlockers[i];
                if (component == null) continue;
                previousEnabled[i] = component.enabled;
                component.enabled = false;
            }
            // CharacterController is a Collider rather than a Behaviour; handled separately.
            characterController = xrOrigin != null ? xrOrigin.GetComponent<CharacterController>() : null;
            if (characterController != null) { characterWasEnabled = characterController.enabled; characterController.enabled = false; }
            if (xrOrigin != null && vehicleSpace != null) xrOrigin.transform.SetParent(vehicleSpace, false);
        }
        void Start()
        {
            RecenterSeat();
            awaitingTrackedHead = true; // OpenXR may initialize after Start; calibrate once it actually supplies a pose.
            IgnoreRigCollisions();
        }
        void LateUpdate()
        {
            // ControllerInputActionManager can toggle providers on modality/UI changes. Keep the authored seat lock.
            foreach (var component in seatedMovementBlockers)
                if (component != null && component.enabled) component.enabled = false;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (awaitingTrackedHead && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
            {
                RecenterSeat();
                awaitingTrackedHead = false;
            }
        }
        public void RecenterSeat()
        {
            if (xrOrigin == null || xrOrigin.Camera == null || driverSeatAnchor == null) return;
            // Only translate the XR Origin. Its camera's local position/rotation belong exclusively to tracking.
            xrOrigin.transform.position += driverSeatAnchor.TransformPoint(seatOffset) - xrOrigin.Camera.transform.position;
        }
        void IgnoreRigCollisions()
        {
            if (xrOrigin == null) return;
            var vehicleColliders = GetComponentsInChildren<Collider>(true);
            var rigColliders = xrOrigin.GetComponentsInChildren<Collider>(true);
            foreach (var bodyCollider in vehicleColliders)
            {
                if (bodyCollider.transform.IsChildOf(xrOrigin.transform)) continue;
                foreach (var rigCollider in rigColliders) Physics.IgnoreCollision(bodyCollider, rigCollider);
            }
        }
        void OnDisable()
        {
            if (previousEnabled == null) return;
            for (int i = 0; i < seatedMovementBlockers.Length; i++)
                if (seatedMovementBlockers[i] != null) seatedMovementBlockers[i].enabled = previousEnabled[i];
            if (characterController != null) characterController.enabled = characterWasEnabled;
        }
    }
}
