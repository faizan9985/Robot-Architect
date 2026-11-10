using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum PuzzlePieceType
{
    Cube,
    Sphere,
    Capsule
}

[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public sealed class PuzzlePiece : MonoBehaviour
{
    [SerializeField] private PuzzlePieceType pieceType;

    public PuzzlePieceType PieceType => pieceType;
    public bool IsCompleted { get; private set; }
    public event System.Action<PuzzlePiece> Completed;
    public event System.Action<PuzzlePiece> Removed;

    private void OnDestroy()
    {
        Removed?.Invoke(this);
    }

    public bool TryLockTo(Transform target)
    {
        if (IsCompleted || target == null)
            return false;

        var grab = GetComponent<XRGrabInteractable>();
        var body = GetComponent<Rigidbody>();
        if (grab.isSelected && grab.interactionManager == null)
        {
            Debug.LogWarning("Cannot lock a selected puzzle piece without its XR Interaction Manager.", this);
            return false;
        }

        // Mark first so selection-exit callbacks cannot complete this piece twice.
        IsCompleted = true;
        grab.throwOnDetach = false;
        if (grab.isSelected)
            grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);

        // Unregister from XRI before changing the final physics pose.
        grab.enabled = false;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.useGravity = false;
        body.isKinematic = true;
        body.position = target.position;
        body.rotation = target.rotation;
        transform.SetPositionAndRotation(target.position, target.rotation);
        Completed?.Invoke(this);
        return true;
    }
}
