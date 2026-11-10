using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace DigitalTwins
{
    /// <summary>Session-only placement lock. Uses XRI's grab transformer for all manipulation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRGrabInteractable), typeof(XRGeneralGrabTransformer), typeof(Rigidbody))]
    public sealed class TransformLockController : MonoBehaviour
    {
        [Header("Manipulation")]
        [SerializeField] private bool translationEnabled = true;
        [SerializeField] private bool rotationEnabled = true;
        [SerializeField] private bool scalingEnabled = true;
        [Tooltip("Multiplier of the initial root scale. The initial size is 1.")]
        [SerializeField, Range(0.01f, 1f)] private float minimumScale = 0.25f;
        [Tooltip("Multiplier of the initial root scale. The initial size is 1.")]
        [SerializeField, Range(1f, 10f)] private float maximumScale = 3f;

        public bool IsLocked { get; private set; }
        public event Action<bool> LockStateChanged;

        private XRGrabInteractable grab;
        private XRGeneralGrabTransformer grabTransformer;
        private Rigidbody body;
        private bool wasEnabled, wasKinematic, usedGravity, threwOnDetach;
        private RigidbodyConstraints previousConstraints;

        private void Awake()
        {
            CacheComponents();
            ApplyManipulationSettings();
        }

        // Start runs after XRI has linked the transformer to the initial scale.
        private void Start() => ApplyManipulationSettings();

        private void CacheComponents()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            if (grabTransformer == null) grabTransformer = GetComponent<XRGeneralGrabTransformer>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        private void OnValidate()
        {
            minimumScale = Mathf.Clamp(minimumScale, 0.01f, 1f);
            maximumScale = Mathf.Clamp(maximumScale, 1f, 10f);
            CacheComponents();
            if (grab != null && grabTransformer != null) ApplyManipulationSettings();
        }

        public void ApplyManipulationSettings()
        {
            CacheComponents();
            grab.trackPosition = translationEnabled && !IsLocked;
            grab.trackRotation = rotationEnabled && !IsLocked;
            grab.trackScale = scalingEnabled && !IsLocked;
            grabTransformer.allowOneHandedScaling = false;
            grabTransformer.allowTwoHandedScaling = scalingEnabled;
            grabTransformer.clampScaling = true;
            grabTransformer.minimumScaleRatio = minimumScale;
            grabTransformer.maximumScaleRatio = maximumScale;
        }

        public void LockTransform()
        {
            if (IsLocked) return;
            CacheComponents();
            var position = transform.position;
            var rotation = transform.rotation;
            var scale = transform.localScale;
            wasEnabled = grab.enabled;
            threwOnDetach = grab.throwOnDetach;
            IsLocked = true;
            ApplyManipulationSettings();

            // Cancel every selector through the manager so both interactors receive canceled exits.
            // Suppress throwing, including XRI's deferred detach, before issuing those exits.
            grab.throwOnDetach = false;
            if (grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            grab.enabled = false;

            // Capture the restored, post-drop physics settings (not XRI's temporary grab settings).
            wasKinematic = body.isKinematic;
            usedGravity = body.useGravity;
            previousConstraints = body.constraints;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = false;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
            body.Sleep();
            LockStateChanged?.Invoke(true);
        }

        public void UnlockTransform()
        {
            if (!IsLocked) return;
            body.constraints = previousConstraints;
            body.useGravity = usedGravity;
            body.isKinematic = wasKinematic;
            IsLocked = false;
            ApplyManipulationSettings();
            grab.throwOnDetach = threwOnDetach;
            grab.enabled = wasEnabled;
            LockStateChanged?.Invoke(false);
        }

        public void ToggleLock()
        {
            if (IsLocked) UnlockTransform();
            else LockTransform();
        }
    }
}
