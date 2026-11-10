using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR;

/// <summary>Uses the existing XR Hands subsystem; never creates tracking or input systems.</summary>
[DefaultExecutionOrder(1100), DisallowMultipleComponent]
public sealed class HandMenuController : MonoBehaviour
{
    public enum MenuHand { Left, Right }

    [Header("Existing scene references")]
    [SerializeField] private Camera xrCamera;
    [Tooltip("The active XR Origin's Camera Offset, which contains tracking-space poses.")]
    [SerializeField] private Transform trackingSpace;
    [Tooltip("Optional. Leave unassigned in scenes without puzzles; the restart button is hidden.")]
    [SerializeField] private PuzzleGenerator puzzleGenerator;
    [SerializeField] private PassthroughController passthrough;
    [SerializeField] private MenuHand hand = MenuHand.Left;

    [Header("UI")]
    [SerializeField] private Canvas launcherCanvas;
    [SerializeField] private Canvas menuCanvas;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Toggle passthroughToggle;
    [SerializeField] private TMP_Text passthroughLabel;
    [SerializeField] private PrototypeVolumeControl volumeControl;

    [Header("Visibility")]
    [SerializeField, Range(1f, 89f)] private float palmFacingAngle = 65f;
    [SerializeField, Range(1f, 89f)] private float gazeAngle = 45f;
    [SerializeField, Range(0f, 20f)] private float angleHysteresis = 10f;
    [SerializeField, Min(0f)] private float showDelay = 0.12f;
    [SerializeField, Min(0f)] private float hideDelay = 0.25f;
    [SerializeField, Min(0.01f)] private float minimumDistance = 0.15f;
    [SerializeField, Min(0.1f)] private float maximumDistance = 0.9f;
    [SerializeField, Min(0f)] private float distanceHysteresis = 0.05f;

    [Header("Positioning")]
    [Tooltip("Meters in palm-local space: -Y points out of the palm; +Z toward fingers.")]
    [SerializeField] private Vector3 launcherPositionOffset = new Vector3(0f, -0.065f, 0f);
    [SerializeField] private Vector3 menuPositionOffset = new Vector3(0f, -0.10f, 0.03f);
    [Tooltip("Euler offsets from a canvas facing the headset, upright relative to the XR Origin.")]
    [SerializeField] private Vector3 launcherRotationOffset;
    [SerializeField] private Vector3 menuRotationOffset;
    [SerializeField, Min(0f)] private float positionSharpness = 18f;
    [SerializeField, Min(0f)] private float rotationSharpness = 14f;

    [Header("Optional controller shortcut (off in other scenes)")]
    [SerializeField] private bool enableControllerShortcut;
    [Tooltip("Tracked Left Controller under the active XR Origin's Camera Offset. Required only when the shortcut is enabled.")]
    [SerializeField] private Transform leftControllerAnchor;
    [Tooltip("Meters in controller-local space. Raise the panel above the controller and slightly outward.")]
    [SerializeField] private Vector3 controllerPositionOffset = new Vector3(-0.04f, 0.28f, 0.12f);
    [Tooltip("Local Euler adjustment from a headset-facing panel, upright relative to tracking space.")]
    [SerializeField] private Vector3 controllerRotationOffset;
    [SerializeField, Min(0f)] private float controllerPositionSharpness = 24f;
    [SerializeField, Min(0f)] private float controllerRotationSharpness = 18f;

    private enum OpenSource { None, Palm, Controller }
    private OpenSource openSource;
    private InputAction controllerMenuAction;
    private bool focused = true, paused;
    private Vector3 controllerLocalPosition;
    private Quaternion controllerLocalRotation;
    private bool hasControllerPose;
    private Transform originalMenuParent;
    private bool controllerParented;
    private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();

    private static HandMenuController activeMenu;
    private readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    private XRHandSubsystem handSubsystem;
    private float nextSubsystemSearch;
    private float conditionTime;
    private bool eligible;
    private bool hasPose;
    public bool IsMenuOpen => openSource != OpenSource.None;
    public bool IsLauncherVisible => launcherCanvas != null && launcherCanvas.gameObject.activeSelf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeMenu = null;

    private void OnEnable()
    {
        HideAll();
        if (activeMenu != null && activeMenu != this)
        {
            Debug.LogWarning("Only one Hand Menu Controller can be active.", this);
            enabled = false;
            return;
        }
        if (xrCamera == null || trackingSpace == null ||
            launcherCanvas == null || menuCanvas == null || openButton == null ||
            closeButton == null || passthroughToggle == null ||
            passthrough == null || volumeControl == null)
        {
            Debug.LogError("Hand Menu Controller requires its scene and UI references.", this);
            enabled = false;
            return;
        }
        activeMenu = this;
        launcherCanvas.worldCamera = xrCamera;
        menuCanvas.worldCamera = xrCamera;
        openButton.onClick.AddListener(OpenMenu);
        closeButton.onClick.AddListener(CloseMenu);
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(puzzleGenerator != null);
            if (puzzleGenerator != null) restartButton.onClick.AddListener(RestartPuzzle);
        }
        passthroughToggle.onValueChanged.AddListener(SetPassthrough);
        passthrough.StateChanged += RefreshPassthrough;
        if (puzzleGenerator != null) puzzleGenerator.PuzzleSpawned += HideAll;
        nextSubsystemSearch = 0f;
        RefreshPassthrough();
        if (enableControllerShortcut)
        {
            // Owned action: never enables/disables or edits any shared XRI action map.
            controllerMenuAction = new InputAction("ToggleHandMenu", InputActionType.Button,
                "<XRController>{LeftHand}/primaryButton", interactions: "press(behavior=0)");
            controllerMenuAction.performed += OnControllerMenuPressed;
            if (focused && !paused) controllerMenuAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (controllerMenuAction != null)
        {
            controllerMenuAction.performed -= OnControllerMenuPressed;
            controllerMenuAction.Disable();
            controllerMenuAction.Dispose();
            controllerMenuAction = null;
        }
        if (openButton != null) openButton.onClick.RemoveListener(OpenMenu);
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseMenu);
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartPuzzle);
        if (passthroughToggle != null) passthroughToggle.onValueChanged.RemoveListener(SetPassthrough);
        if (passthrough != null) passthrough.StateChanged -= RefreshPassthrough;
        if (puzzleGenerator != null) puzzleGenerator.PuzzleSpawned -= HideAll;
        if (activeMenu == this) activeMenu = null;
        handSubsystem = null;
        HideAll();
    }

    private void OnApplicationPause(bool value) { paused = value; RefreshSessionInput(); }
    private void OnApplicationFocus(bool value) { focused = value; RefreshSessionInput(); }

    private void RefreshSessionInput()
    {
        if (!focused || paused)
        {
            controllerMenuAction?.Disable();
            HideAll();
        }
        else if (isActiveAndEnabled) controllerMenuAction?.Enable();
    }

    private void OnControllerMenuPressed(InputAction.CallbackContext context)
    {
        if (!isActiveAndEnabled || !focused || paused) return;
        if (IsMenuOpen) { CloseMenu(); return; }
        if (xrCamera == null || trackingSpace == null || menuCanvas == null ||
            leftControllerAnchor == null || !leftControllerAnchor.gameObject.activeInHierarchy) return;
        originalMenuParent = menuCanvas.transform.parent;
        menuCanvas.transform.SetParent(trackingSpace, true);
        controllerParented = true;
        hasControllerPose = false;
        FollowController(0f); // Snap on opening; subsequent frames smooth only tracked hand motion.
        eligible = false;
        hasPose = false;
        conditionTime = 0f;
        openSource = OpenSource.Controller;
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (var subsystem in inputSubsystems) subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
        RefreshPassthrough();
        RefreshVisibility();
        volumeControl.Refresh();
    }

    private void FollowController(float dt)
    {
        var position = leftControllerAnchor.TransformPoint(controllerPositionOffset);
        var forward = position - xrCamera.transform.position;
        var up = trackingSpace.up;
        if (Vector3.Cross(forward, up).sqrMagnitude < 0.0001f) up = xrCamera.transform.up;
        var rotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward, up) * Quaternion.Euler(controllerRotationOffset)
            : menuCanvas.transform.rotation;
        var localPosition = trackingSpace.InverseTransformPoint(position);
        var localRotation = Quaternion.Inverse(trackingSpace.rotation) * rotation;
        var positionBlend = !hasControllerPose || controllerPositionSharpness <= 0f
            ? 1f : 1f - Mathf.Exp(-controllerPositionSharpness * dt);
        var rotationBlend = !hasControllerPose || controllerRotationSharpness <= 0f
            ? 1f : 1f - Mathf.Exp(-controllerRotationSharpness * dt);
        // Smooth in tracking space so car motion and seat resets carry the menu immediately.
        controllerLocalPosition = Vector3.Lerp(controllerLocalPosition, localPosition, positionBlend);
        controllerLocalRotation = Quaternion.Slerp(controllerLocalRotation, localRotation, rotationBlend);
        menuCanvas.transform.SetLocalPositionAndRotation(controllerLocalPosition, controllerLocalRotation);
        hasControllerPose = true;
    }

    private void RestoreMenuParent()
    {
        if (!controllerParented) return;
        if (menuCanvas != null) menuCanvas.transform.SetParent(originalMenuParent, true);
        controllerParented = false;
        originalMenuParent = null;
    }

    private void OnTrackingOriginUpdated(XRInputSubsystem subsystem) => HideAll();

    private void StopWatchingTrackingOrigin()
    {
        foreach (var subsystem in inputSubsystems) subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
        inputSubsystems.Clear();
    }

    private void LateUpdate()
    {
        if (!focused || paused) { HideAll(); return; }
        if (openSource == OpenSource.Controller)
        {
            if (xrCamera == null || trackingSpace == null || leftControllerAnchor == null ||
                !leftControllerAnchor.gameObject.activeInHierarchy) { HideAll(); return; }
            FollowController(Time.unscaledDeltaTime);
            RefreshVisibility();
            return;
        }
        if (handSubsystem == null || !handSubsystem.running)
        {
            handSubsystem = null;
            // XR can initialize after OnEnable or restart after suspend. No scene searches.
            if (Time.unscaledTime >= nextSubsystemSearch)
            {
                nextSubsystemSearch = Time.unscaledTime + 1f;
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var candidate in subsystems)
                    if (candidate.running) { handSubsystem = candidate; break; }
            }
        }
        if (handSubsystem == null)
        {
            HideAll();
            return;
        }
        var trackedHand = hand == MenuHand.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
        if (!trackedHand.isTracked || !trackedHand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm))
        {
            HideAll(); // Lost tracking never leaves an interactive panel at a stale pose.
            return;
        }
        palm.position = trackingSpace.TransformPoint(palm.position);
        palm.rotation = trackingSpace.rotation * palm.rotation;
        UpdatePalm(palm, Time.unscaledDeltaTime);
    }

    private void UpdatePalm(Pose palm, float deltaTime)
    {
        if (xrCamera == null || trackingSpace == null) { HideAll(); return; }
        var head = xrCamera.transform;
        var launcherPosition = palm.position + palm.rotation * launcherPositionOffset;
        var menuPosition = palm.position + palm.rotation * menuPositionOffset;
        var toHead = head.position - palm.position;
        var distance = toHead.magnitude;
        var angleMargin = eligible ? angleHysteresis : 0f;
        var distanceMargin = eligible ? distanceHysteresis : 0f;
        // XR Hands defines the outward palm normal as local -Y on BOTH hands.
        var facing = Vector3.Angle(palm.rotation * Vector3.down, toHead) <= palmFacingAngle + angleMargin;
        var gazeTarget = IsMenuOpen ? menuPosition : launcherPosition;
        var looking = Vector3.Angle(head.forward, gazeTarget - head.position) <= gazeAngle + angleMargin;
        var conditions = facing && looking && distance >= Mathf.Max(0.01f, minimumDistance - distanceMargin)
            && distance <= maximumDistance + distanceMargin;

        Follow(launcherCanvas.transform, launcherPosition, launcherRotationOffset, deltaTime);
        Follow(menuCanvas.transform, menuPosition, menuRotationOffset, deltaTime);
        hasPose = true;
        if (conditions == eligible) conditionTime = 0f;
        else
        {
            conditionTime += deltaTime;
            if (conditionTime >= (conditions ? showDelay : hideDelay))
            {
                eligible = conditions;
                conditionTime = 0f;
                if (!eligible) openSource = OpenSource.None;
            }
        }
        RefreshVisibility();
    }

    private void Follow(Transform target, Vector3 position, Vector3 eulerOffset, float dt)
    {
        var forward = position - xrCamera.transform.position;
        var up = trackingSpace.up;
        if (Vector3.Cross(forward, up).sqrMagnitude < 0.0001f) up = xrCamera.transform.up;
        var rotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward, up) * Quaternion.Euler(eulerOffset) : target.rotation;
        var positionBlend = !hasPose || positionSharpness <= 0f ? 1f : 1f - Mathf.Exp(-positionSharpness * dt);
        var rotationBlend = !hasPose || rotationSharpness <= 0f ? 1f : 1f - Mathf.Exp(-rotationSharpness * dt);
        target.SetPositionAndRotation(Vector3.Lerp(target.position, position, positionBlend),
            Quaternion.Slerp(target.rotation, rotation, rotationBlend));
    }

    public void OpenMenu()
    {
        if (!isActiveAndEnabled || !eligible || IsMenuOpen) return;
        openSource = OpenSource.Palm;
        RefreshPassthrough();
        RefreshVisibility();
        volumeControl.Refresh();
    }

    public void CloseMenu()
    {
        StopWatchingTrackingOrigin();
        RestoreMenuParent();
        hasControllerPose = false;
        bool wasController = openSource == OpenSource.Controller;
        openSource = OpenSource.None;
        if (wasController) { eligible = false; hasPose = false; conditionTime = 0f; }
        RefreshVisibility();
    }

    public void RestartPuzzle()
    {
        if (!isActiveAndEnabled || !IsMenuOpen || puzzleGenerator == null) return;
        HideAll();
        puzzleGenerator.RetryCurrentPuzzle();
    }

    private void SetPassthrough(bool value)
    {
        passthrough.SetPassthroughEnabled(value);
        RefreshPassthrough();
    }

    private void RefreshPassthrough()
    {
        passthroughToggle.interactable = passthrough.IsSupported;
        passthroughToggle.SetIsOnWithoutNotify(passthrough.IsPassthroughEnabled);
        if (passthroughLabel != null)
            passthroughLabel.text = passthrough.IsSupported ? "Passthrough" : "Passthrough\nUnavailable";
    }

    private void RefreshVisibility()
    {
        SetVisible(launcherCanvas, eligible && !IsMenuOpen);
        SetVisible(menuCanvas, IsMenuOpen && (openSource == OpenSource.Controller || eligible));
    }

    private static void SetVisible(Canvas canvas, bool value)
    {
        if (canvas != null && canvas.gameObject.activeSelf != value) canvas.gameObject.SetActive(value);
    }

    private void HideAll()
    {
        StopWatchingTrackingOrigin();
        RestoreMenuParent();
        hasControllerPose = false;
        eligible = false;
        openSource = OpenSource.None;
        hasPose = false;
        conditionTime = 0f;
        SetVisible(launcherCanvas, false);
        SetVisible(menuCanvas, false);
    }
}
