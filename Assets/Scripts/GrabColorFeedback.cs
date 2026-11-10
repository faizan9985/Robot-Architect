using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Changes the renderer's first material color while the object is grabbed.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class GrabColorFeedback : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color grabbedColor = Color.green;

    private XRGrabInteractable grabInteractable;
    private Material originalMaterial;
    private Material runtimeMaterial;
    private Color originalColor;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            Debug.LogWarning("GrabColorFeedback requires a renderer with a material.", this);
            enabled = false;
            return;
        }

        originalMaterial = targetRenderer.sharedMaterial;
        // Renderer.material creates one private instance here, never every frame.
        runtimeMaterial = targetRenderer.material;
        originalColor = runtimeMaterial.color;
    }

    private void OnEnable()
    {
        if (runtimeMaterial == null)
            return;

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
        runtimeMaterial.color = grabInteractable.isSelected ? grabbedColor : originalColor;
    }

    public void SetRestingColor(Color color)
    {
        originalColor = color;
        if (runtimeMaterial != null)
            runtimeMaterial.color = grabInteractable.isSelected ? grabbedColor : originalColor;
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        if (runtimeMaterial != null)
            runtimeMaterial.color = originalColor;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        runtimeMaterial.color = grabbedColor;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        // Also supports interactables that allow more than one selecting hand.
        if (!grabInteractable.isSelected)
            runtimeMaterial.color = originalColor;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;

        if (targetRenderer != null && targetRenderer.sharedMaterial == runtimeMaterial)
            targetRenderer.sharedMaterial = originalMaterial;

        Destroy(runtimeMaterial);
    }
}
