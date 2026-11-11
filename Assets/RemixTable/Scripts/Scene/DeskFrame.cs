using UnityEngine;

namespace RemixTable
{
    /// <summary>
    /// The real desk: this transform sits on the desk surface (centre of the play area), its up axis is the desk
    /// normal and its forward axis points away from the player. Also knows the listener (the head / camera with the
    /// AudioListener). Keep this object unscaled.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Remix Table/Desk Frame")]
    public sealed class DeskFrame : MonoBehaviour
    {
        [Tooltip("Head / camera with the AudioListener. Falls back to Camera.main on Awake.")]
        [SerializeField] Transform listener;
        [Tooltip("Usable desk area (width along local X, depth along local Z), in metres.")]
        [SerializeField] Vector2 deskSize = new Vector2(1.2f, 0.7f);

        public Transform Listener => listener;
        public Vector2 DeskSize => deskSize;
        public Vector3 Origin => transform.position;
        public Vector3 Up => transform.up;
        public Vector3 Forward => Vector3.ProjectOnPlane(transform.forward, transform.up).normalized;
        public float DeskHeight => transform.position.y;

        void Awake()
        {
            if (listener == null)
            {
                Camera main = Camera.main;
                if (main != null) listener = main.transform;
                else RemixLog.Warn("DeskFrame has no listener and there is no Main Camera; distance → volume is off.", this);
            }
        }

        public void Configure(Transform listenerTransform, Vector2 size)
        {
            listener = listenerTransform;
            deskSize = size;
        }

        public void SetListener(Transform listenerTransform) => listener = listenerTransform;

        /// <summary>Desk calibration: moves the desk plane to this world height.</summary>
        public void SetDeskHeight(float worldY)
        {
            Vector3 p = transform.position;
            p.y = worldY;
            transform.position = p;
        }

        /// <summary>Signed height of a world point above the desk surface.</summary>
        public float HeightAbove(Vector3 worldPoint) => Vector3.Dot(worldPoint - transform.position, transform.up);

        /// <summary>Distance from the listener to the point, measured along the desk plane (lifting an orb changes
        /// its reverb, not its volume). 0 without a listener.</summary>
        public float HorizontalDistanceToListener(Vector3 worldPoint)
        {
            if (listener == null) return 0f;
            return Vector3.ProjectOnPlane(worldPoint - listener.position, transform.up).magnitude;
        }

        /// <summary>Clamps a point to the desk area (keeping its height above the desk).</summary>
        public Vector3 ClampToDesk(Vector3 worldPoint, float margin = 0f)
        {
            Quaternion rotation = transform.rotation;
            Vector3 local = Quaternion.Inverse(rotation) * (worldPoint - transform.position);
            float halfX = Mathf.Max(0f, deskSize.x * 0.5f - margin);
            float halfZ = Mathf.Max(0f, deskSize.y * 0.5f - margin);
            local.x = Mathf.Clamp(local.x, -halfX, halfX);
            local.z = Mathf.Clamp(local.z, -halfZ, halfZ);
            return transform.position + rotation * local;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.6f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(deskSize.x, 0f, deskSize.y));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.1f);
            Gizmos.matrix = previous;
        }
    }
}
