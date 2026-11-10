using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features.Meta;

/// <summary>
/// Controls the Meta OpenXR camera subsystem through AR Foundation's documented manager API.
/// The provider owns the PassthroughLayerData composition underlay; do not create another layer.
/// </summary>
[DisallowMultipleComponent]
public sealed class PassthroughController : MonoBehaviour
{
    [SerializeField] private Camera xrCamera;
    [SerializeField] private ARCameraManager cameraManager;

    private UniversalAdditionalCameraData cameraData;
    private CameraClearFlags virtualClearFlags;
    private Color virtualBackground;
    private bool virtualAllowHDR;
    private bool virtualPostProcessing;
    private bool backgroundOverridden;
    private float nextSupportCheck;

    public bool IsSupported { get; private set; }
    public bool IsPassthroughEnabled { get; private set; }
    public string UnavailableReason => "Meta Quest passthrough requires a running OpenXR loader with the Meta Camera and Session features enabled.";
    public event Action StateChanged;

    private void Awake()
    {
        if (xrCamera != null) xrCamera.TryGetComponent(out cameraData);
        // The scene also saves this disabled, preventing a passthrough flash during startup.
        if (cameraManager != null) cameraManager.enabled = false;
    }

    private void OnEnable()
    {
        nextSupportCheck = 0f;
        RefreshSupport();
    }

    private void Update()
    {
        // XR initialization can finish after scene activation; also recover from loader shutdown.
        if (Time.unscaledTime < nextSupportCheck) return;
        nextSupportCheck = Time.unscaledTime + 0.5f;
        RefreshSupport();
    }

    private void RefreshSupport()
    {
        var previousSupport = IsSupported;
        var previousState = IsPassthroughEnabled;
        var loader = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager?.activeLoader : null;
        var subsystem = loader != null ? loader.GetLoadedSubsystem<XRCameraSubsystem>() : null;
        // An XR Simulation camera is not Quest passthrough. Never clear the virtual background for it.
        IsSupported = isActiveAndEnabled && xrCamera != null && cameraManager != null &&
            subsystem is MetaOpenXRCameraSubsystem;
        if (IsPassthroughEnabled && (!IsSupported || !cameraManager.isActiveAndEnabled || !subsystem.running))
            DisablePassthrough();
        if (previousSupport != IsSupported || previousState != IsPassthroughEnabled)
            StateChanged?.Invoke();
    }

    public void SetPassthroughEnabled(bool enabled)
    {
        RefreshSupport();
        if (!enabled)
        {
            DisablePassthrough();
            StateChanged?.Invoke();
            return;
        }
        if (IsPassthroughEnabled) return;
        if (!IsSupported)
        {
            Debug.LogWarning("Passthrough unavailable: " + UnavailableReason, this);
            StateChanged?.Invoke();
            return;
        }

        SetCameraBackground(true);
        // MetaOpenXRCameraSubsystem.Start creates the composition layer at order -1.
        cameraManager.enabled = true;
        IsPassthroughEnabled = cameraManager.subsystem is MetaOpenXRCameraSubsystem && cameraManager.subsystem.running;
        if (!IsPassthroughEnabled)
        {
            DisablePassthrough();
            Debug.LogWarning("The Meta passthrough camera subsystem could not start. Virtual background restored.", this);
        }
        StateChanged?.Invoke();
    }

    public void TogglePassthrough() => SetPassthroughEnabled(!IsPassthroughEnabled);

    private void DisablePassthrough()
    {
        // MetaOpenXRCameraSubsystem.Stop removes its owned passthrough composition layer.
        if (cameraManager != null) cameraManager.enabled = false;
        SetCameraBackground(false);
        IsPassthroughEnabled = false;
    }

    private void SetCameraBackground(bool passthrough)
    {
        if (xrCamera == null) return;
        if (passthrough && !backgroundOverridden)
        {
            virtualClearFlags = xrCamera.clearFlags;
            virtualBackground = xrCamera.backgroundColor;
            virtualAllowHDR = xrCamera.allowHDR;
            virtualPostProcessing = cameraData != null && cameraData.renderPostProcessing;
            backgroundOverridden = true;
            xrCamera.clearFlags = CameraClearFlags.SolidColor;
            xrCamera.backgroundColor = Color.clear;
            // Preserve alpha through URP without changing shared pipeline assets.
            xrCamera.allowHDR = false;
            if (cameraData != null) cameraData.renderPostProcessing = false;
        }
        else if (!passthrough && backgroundOverridden)
        {
            xrCamera.clearFlags = virtualClearFlags;
            xrCamera.backgroundColor = virtualBackground;
            xrCamera.allowHDR = virtualAllowHDR;
            if (cameraData != null) cameraData.renderPostProcessing = virtualPostProcessing;
            backgroundOverridden = false;
        }
    }

    private void OnDisable()
    {
        DisablePassthrough();
        IsSupported = false;
        StateChanged?.Invoke();
    }
}
