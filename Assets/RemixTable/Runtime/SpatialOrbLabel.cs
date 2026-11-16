using UnityEngine;
namespace RemixTable
{
    // Keeps a spatial orb's name label facing the headset.
    public sealed class SpatialOrbLabel : MonoBehaviour
    {
        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var away = transform.position - camera.transform.position;
            if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }
    }
}
