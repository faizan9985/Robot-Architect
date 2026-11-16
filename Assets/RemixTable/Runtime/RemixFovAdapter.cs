using UnityEngine;
using UnityEngine.XR;
namespace RemixTable
{
    // FoV-aware layout: Meta headsets differ in field of view (Quest 3S is narrower than Quest 3). This measures
    // the real binocular horizontal FoV from the eye projections and keeps the side panels (DAILY DROP, VIBES)
    // inside a comfortable cone: if they would spill past it, they slide inward and shrink a little, never
    // overlapping the centre rows. The ribbons, playhead and all core rows are already central.
    public sealed class RemixFovAdapter : MonoBehaviour
    {
        public RemixTableManager manager;
        [Tooltip("Side panels; their sign of x decides the side.")]
        public RectTransform[] sidePanels;
        [Range(20, 60)] public float designAngle = 38;
        [Tooltip("Half the panel's width as seen from the head, at full size (degrees).")]
        [Range(4, 20)] public float panelHalfWidth = 11;
        [Tooltip("Outer edge of the centre rows (degrees): panels never cross it.")]
        [Range(10, 40)] public float rowsEdge = 26;
        [Tooltip("Keep this much clear of the FoV edge (degrees).")]
        [Range(0, 20)] public float comfortMargin = 8;
        [Range(.5f, 1)] public float minScale = .72f;

        public float MeasuredFov { get; private set; }
        public float Angle { get; private set; }
        public float Scale { get; private set; } = 1;
        float nextCheck; float appliedFov = -1;
        Vector3[] baseScales;

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2;
            var cam = Camera.main;
            if (cam == null || !XRSettings.isDeviceActive) return; // Editor game view FoV is not a headset's.
            float fov = HorizontalFov(cam);
            if (fov < 40 || fov > 200 || Mathf.Abs(fov - appliedFov) < 1) return;
            Apply(fov);
        }

        // Binocular horizontal FoV: left edge of the left eye to right edge of the right eye.
        public static float HorizontalFov(Camera cam)
        {
            if (cam.stereoEnabled)
            {
                var l = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                var r = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                if (Mathf.Abs(l.m00) > 1e-4f && Mathf.Abs(r.m00) > 1e-4f)
                {
                    float leftTan = (1 - l.m02) / l.m00, rightTan = (1 + r.m02) / r.m00;
                    return (Mathf.Atan(leftTan) + Mathf.Atan(rightTan)) * Mathf.Rad2Deg;
                }
            }
            return 2 * Mathf.Atan(Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect) * Mathf.Rad2Deg;
        }

        // Panel centre angle and size for a given FoV.
        public static void Layout(float fov, float designAngle, float halfWidth, float rowsEdge, float margin, float minScale, out float angle, out float scale)
        {
            float limit = fov * .5f - margin;
            if (designAngle + halfWidth <= limit) { angle = designAngle; scale = 1; return; }
            scale = Mathf.Clamp((limit - rowsEdge) / (2 * halfWidth), minScale, 1);
            angle = Mathf.Min(designAngle, rowsEdge + halfWidth * scale);
        }

        public void Apply(float fov)
        {
            MeasuredFov = fov; appliedFov = fov;
            Layout(fov, designAngle, panelHalfWidth, rowsEdge, comfortMargin, minScale, out float angle, out float scale);
            Angle = angle; Scale = scale;
            if (sidePanels == null || manager == null) return;
            if (baseScales == null || baseScales.Length != sidePanels.Length)
            {
                baseScales = new Vector3[sidePanels.Length];
                for (int i = 0; i < sidePanels.Length; i++) if (sidePanels[i] != null) baseScales[i] = sidePanels[i].localScale;
            }
            float r = -manager.HeadAnchorLocal.z;
            for (int i = 0; i < sidePanels.Length; i++)
            {
                var p = sidePanels[i]; if (p == null) continue;
                float side = Mathf.Sign(p.localPosition.x == 0 ? 1 : p.localPosition.x), a = side * angle * Mathf.Deg2Rad;
                p.localPosition = new Vector3(Mathf.Sin(a) * r, p.localPosition.y, (Mathf.Cos(a) - 1) * r);
                p.localRotation = Quaternion.Euler(0, side * angle, 0);
                p.localScale = baseScales[i] * scale;
            }
            Debug.Log($"REMIX_FOV: {fov:0}° horizontal -> side panels at ±{angle:0}°, {scale * 100:0}% size");
        }
    }
}
