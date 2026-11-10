using UnityEngine;

namespace Driving
{
    /// <summary>Observes collision-enter only; never modifies collision response.</summary>
    [RequireComponent(typeof(Rigidbody)), DisallowMultipleComponent]
    public sealed class VehicleCollisionTelemetry : MonoBehaviour
    {
        [SerializeField] DrivingTelemetryManager telemetry;
        [Tooltip("Physical ground colliders, not road-corridor membership. These never count as crashes.")]
        [SerializeField] Collider[] ignoredSurfaces = System.Array.Empty<Collider>();
        void OnCollisionEnter(Collision collision)
        {
            if (telemetry == null) return;
            foreach (var surface in ignoredSurfaces) if (collision.collider == surface) return;
            // Ignore support contacts, even on a surface not explicitly listed.
            float impactSpeed = 0f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var normal = collision.GetContact(i).normal;
                if (Mathf.Abs(normal.y) < 0.7f)
                    impactSpeed = Mathf.Max(impactSpeed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal)));
            }
            if (impactSpeed > 0f) telemetry.RecordCollision(impactSpeed);
        }
    }
}

