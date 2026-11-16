using RemixTable;
using UnityEngine;

/// <summary>
/// ROOM button on the Remix Table toolbar: switches Meta Quest passthrough through the existing
/// PassthroughController (same one the palm menu uses) and hides the virtual floor and decor while the
/// real room is visible, so the remix table floats in your own space.
/// </summary>
[DisallowMultipleComponent]
public sealed class RemixPassthroughToggle : MonoBehaviour, IRemixToggle
{
    [SerializeField] private PassthroughController passthrough;
    [Tooltip("Hidden while passthrough is on (e.g. the virtual Floor).")]
    [SerializeField] private GameObject[] hideInPassthrough = new GameObject[0];

    public bool Available => passthrough != null && passthrough.IsSupported;
    public bool IsOn => passthrough != null && passthrough.IsPassthroughEnabled;

    public void SetOn(bool on)
    {
        if (!Available) return;
        passthrough.SetPassthroughEnabled(on);
    }

    private void OnEnable()
    {
        if (passthrough != null) passthrough.StateChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        if (passthrough != null) passthrough.StateChanged -= Apply;
    }

    // Also follows the palm menu's passthrough toggle, since both drive the same controller.
    private void Apply()
    {
        bool on = IsOn;
        foreach (var go in hideInPassthrough) if (go != null) go.SetActive(!on);
    }
}
