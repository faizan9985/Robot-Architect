# Robot Architect hand menu

The saved `Hand Menu Root` is in `Assets/RobotArchitect/Scenes/RobotArchitect.unity`.
`HandMenuController` uses the existing XR Hands subsystem, rig camera and Camera
Offset. It does not create a tracking subsystem or modify shared XRI action maps.
The scene uses left-palm activation; the optional controller shortcut remains off.

Raise the left palm toward the headset and look at it to reveal the launcher.
Use the other hand to select it. The expanded world-space menu offers Close,
Passthrough and prototype volume. Tracking loss hides the palm menu. Delays,
hysteresis and pose smoothing limit flicker. No gameplay restart action is present.

The canvases retain TrackedDeviceGraphicRaycaster components and the existing
XRI runtime EventSystem registration. Do not add a duplicate EventSystem.
`PrototypeVolumeControl` changes AudioListener.volume rather than a system volume.

`PassthroughController` uses the existing AR Session and camera ARCameraManager.
Passthrough starts off; when supported, the toggle enables the native camera
background and restores the virtual camera background when disabled. In an Editor
without a compatible Meta provider, the toggle reports Unavailable. The virtual
floor remains opaque. Preserve the current OpenXR Meta camera/hand configuration.

The menu sprites come from the locally restored XR Hands Hand Capture and XRI
XR Device Simulator samples. Follow RECONSTRUCTION.md to restore them; do not
replace the sample folders or regenerate their metadata.

The Play Mode smoke check verifies initialization, menu listeners, visibility and
volume with synthetic eligibility. On Quest 3, verify palm activation, near/far UI,
tracking loss/reacquisition, volume with audible content, passthrough on/off,
recenter and suspend/resume. Editor success does not test native passthrough.
