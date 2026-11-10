using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalTwins
{
    /// <summary>Keeps a constant-size XR UI control above the visible model, facing the viewer.</summary>
    public sealed class TransformLockUI : MonoBehaviour
    {
        [SerializeField] private TransformLockController target;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Camera viewer;
        [SerializeField] private Renderer[] modelRenderers;
        [SerializeField, Min(0.05f)] private float clearance = 0.12f;

        private void OnEnable()
        {
            if (target == null || button == null || label == null) return;
            button.onClick.AddListener(target.ToggleLock);
            target.LockStateChanged += Refresh;
            Refresh(target.IsLocked);
        }

        private void OnDisable()
        {
            if (target == null) return;
            if (button != null) button.onClick.RemoveListener(target.ToggleLock);
            target.LockStateChanged -= Refresh;
        }

        private void Refresh(bool locked) => label.text = locked ? "Unlock" : "Lock";

        private void LateUpdate()
        {
            if (target == null) return;
            var bounds = new Bounds(target.transform.position, Vector3.zero);
            bool found = false;
            foreach (var model in modelRenderers)
            {
                if (model == null) continue;
                if (!found) { bounds = model.bounds; found = true; }
                else bounds.Encapsulate(model.bounds);
            }
            transform.position = new Vector3(bounds.center.x, bounds.max.y + clearance, bounds.center.z);
            if (viewer == null) viewer = Camera.main;
            if (viewer != null)
            {
                var forward = transform.position - viewer.transform.position;
                if (forward.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(forward, viewer.transform.up);
            }
        }
    }
}
