using UnityEngine;

namespace Driving
{
    /// <summary>Tests a reference point against a closed road-centerline corridor in road-local XZ space.
    /// No colliders, triggers, allocations or physics queries; authored path follows the visible lane markings.</summary>
    [DisallowMultipleComponent]
    public sealed class RoadSurfaceTracker : MonoBehaviour
    {
        [SerializeField] Transform roadSpace;
        [SerializeField] Vector3[] centerLine = System.Array.Empty<Vector3>();
        [SerializeField, Min(0.1f)] float roadHalfWidth = 6f;
        [SerializeField, Min(0.1f)] float verticalTolerance = 3f;
        public bool IsOnRoad(Vector3 worldPoint)
        {
            if (roadSpace == null || centerLine.Length < 2) return false;
            var p = roadSpace.InverseTransformPoint(worldPoint);
            float widthSquared = roadHalfWidth * roadHalfWidth;
            for (int i = 0; i < centerLine.Length; i++)
            {
                var a = centerLine[i];
                var b = centerLine[(i + 1) % centerLine.Length];
                var delta = new Vector2(b.x - a.x, b.z - a.z);
                var offset = new Vector2(p.x - a.x, p.z - a.z);
                float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(offset, delta) / delta.sqrMagnitude) : 0f;
                if ((offset - delta * t).sqrMagnitude <= widthSquared &&
                    Mathf.Abs(p.y - Mathf.Lerp(a.y, b.y, t)) <= verticalTolerance) return true;
            }
            return false;
        }
        void OnDrawGizmosSelected()
        {
            if (roadSpace == null || centerLine.Length < 2) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < centerLine.Length; i++)
                Gizmos.DrawLine(roadSpace.TransformPoint(centerLine[i]), roadSpace.TransformPoint(centerLine[(i + 1) % centerLine.Length]));
        }
    }
}
