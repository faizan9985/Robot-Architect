# Hand menu milestone

Implemented in `Assets/Scenes/SampleScene.unity`. There is one saved menu root. The initial hand-menu milestone preserved the existing XR configuration. The subsequent Meta OpenXR passthrough integration is documented below. No packages, controller bindings, puzzle scripts, or completion UI scripts were changed by either implementation. Nothing was committed.

## Files

Created (plus Unity `.meta` files):

- `Assets/Scripts/UI/HandMenuController.cs`
- `Assets/Scripts/UI/PassthroughController.cs`
- `Assets/Scripts/UI/PrototypeVolumeControl.cs`
- `HAND_MENU.md` (this implementation and validation report)

Modified: `Assets/Scenes/SampleScene.unity`. All existing serialized object blocks are preserved; only new menu objects, a reference to the existing Camera Offset transform, and one scene-root entry were added.

Temporary scene-authoring and Play Mode validation scripts were removed after use.

## Scene hierarchy

```text
Hand Menu Root                         HandMenuController, PassthroughController
  Palm Launcher Canvas                 World Space, TrackedDeviceGraphicRaycaster
    Open Menu Button
      Label
  Hand Menu Canvas                     World Space, TrackedDeviceGraphicRaycaster
    Panel
      Title
      Passthrough Toggle               enabled when the Meta camera provider is available
        Background / Checkmark
        Label
      Volume Label
      Volume Slider                    PrototypeVolumeControl
        Background
        Fill Area / Fill
        Handle Slide Area / Handle
      Restart Puzzle Button / Label
      Close Button / Label
```

Both canvases are saved inactive. The controller stays active outside the canvases, so it can reacquire tracking while the UI is hidden. All graphics use the UI layer; labels use the project's existing TextMeshPro font and do not intercept raycasts. The launcher is 10 x 6.4 cm; the expanded panel is approximately 30.6 x 30.6 cm. Hidden canvases are deactivated, including their raycasters.

## Tracking and visibility

The Inspector on `Hand Menu Root` exposes Left/Right hand choice, references, thresholds, position/rotation offsets, and smoothing speeds. Left is the default.

`HandMenuController` reuses the running `XRHandSubsystem`. It reads `leftHand` or `rightHand`, checks `isTracked`, and calls `GetJoint(XRHandJointID.Palm).TryGetPose`. It does not create a subsystem, gesture recognizer, input device, or tracking transform. Joint poses are transformed through the active rig's existing Camera Offset, matching the tracking-space parent used by the hand visuals. This also follows rig movement/rotation.

XR Hands defines the outward palm normal as local **-Y** for both hands (confirmed in the installed XR Hands orientation utility). The palm faces the player when the angle between that normal and the palm-to-headset vector is at most **65 degrees**.

Gaze means **head direction**, using the XR camera's forward vector, not eye tracking. The angle toward the launcher, or toward the expanded menu while open, must be at most **45 degrees**. Palm-to-headset distance must be **0.15–0.9 m**.

Acquisition requires 0.12 seconds of qualifying conditions. Visible UI gets a 10-degree angular margin and 5 cm distance margin; sustained failure for 0.25 seconds closes it. Tracking loss or an invalid palm pose hides everything immediately. Close restores the launcher if eligible. Regeneration resets menu state, then the launcher can reappear after the normal acquisition delay.

References are serialized and cached. Per-frame work is lightweight pose/math checking in `LateUpdate`; subsystem discovery retries at most once per second only while no running subsystem is cached. There are no per-frame scene searches. An active-instance guard prevents duplicate active menus.

## Positioning and UI interaction

Position offsets use palm-local coordinates in meters: -Y out of the palm, +Z toward fingertips. Defaults:

- Launcher: `(0, -0.065, 0)`.
- Expanded menu: `(0, -0.10, 0.03)`.

Canvases face the headset and remain upright relative to the tracking space. Separate Euler rotation offsets apply relative to that facing orientation. Position and rotation use frame-rate-independent exponential smoothing with sharpness 18 and 14, respectively, using unscaled time. A first/reacquired pose snaps to the current hand instead of moving from a stale location. Sharpness zero disables smoothing.

The existing rig's hand/controller UI interactors and XRI 3.3.2 UI registration path are reused. In this saved scene XRI's `RegisteredUIInteractorCache` creates the EventSystem and XRUIInputModule at runtime. The menu adds neither. Play Mode confirmed exactly one runtime EventSystem and an XRUIInputModule. Each new canvas uses XRI's `TrackedDeviceGraphicRaycaster`, supporting the existing ray/poke pipeline.

## Meta OpenXR 2.6.1 passthrough follow-up

After the user installed Unity OpenXR Meta 2.6.1, real Quest passthrough was integrated into the existing `PassthroughController`. Package files were not changed. At the start of this follow-up, the lock file already resolved OpenXR 1.18.0, AR Foundation 6.6.0, and Composition Layers 2.4.0, as required by Meta 2.6.1; the manifest's older direct OpenXR request was left untouched.

Implementation uses `UnityEngine.XR.ARFoundation.ARCameraManager.enabled` on the existing active rig camera. The loaded provider is checked through `XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRCameraSubsystem>()` and must be `UnityEngine.XR.OpenXR.Features.Meta.MetaOpenXRCameraSubsystem`. The controller does not treat XR Simulation as Quest passthrough.

The package's documented camera/composition-layer path is used: starting the camera manager starts the Meta camera subsystem, which creates a `CompositionLayer` containing `PassthroughLayerData`, with layer order -1 behind the default scene layer. Stopping the manager removes that underlay. No custom native calls, manually duplicated layer, Oculus plugin, ARCameraBackground, second XR rig, or second EventSystem was added. Camera image capture remains disabled: passthrough compositing does not require CPU/GPU camera image access.

Scene changes for this follow-up:

- Added one **disabled ARCameraManager** to the existing active rig's Main Camera. Camera clear flags/color remain unchanged in the saved scene.
- Added **AR Session - Meta Passthrough**, containing one ARSession. `matchFrameRateRequested` is false to preserve the game's frame-rate policy. Existing OpenXR input tracking remains responsible for rig input.
- Wired the existing PassthroughController to that camera/manager and the hand menu to its toggle label. The saved label is "Passthrough" and the toggle is interactable; runtime capability checks disable it when no Meta camera provider exists.

Required Android OpenXR features:

- **Meta Quest: Camera (Passthrough)** (`ARCameraFeature`): enabled.
- **Meta Quest: Session** (`ARSessionFeature`): enabled, as required by the camera feature's validation rules.
- **OpenXR Composition Layers** (`OpenXRCompositionLayersFeature`): already enabled; preserved.
- The package's hidden **OpenXRLifeCycleFeature**: enabled as its required native lifecycle dependency.
- **Passthrough Pre Splash Screen** and **Camera Image Support**: remain OFF. Composition-layer splash passthrough is not enabled.

Other existing OpenXR features, including hand tracking, aim, interaction profiles and latency settings, were preserved. Minimum Android API was raised to **32 (Android 12L)** in both `ProjectSettings/ProjectSettings.asset` (previously 25) and `Assets/Settings/Build Profiles/Meta Quest.asset` (previously 29), matching the camera feature's explicit requirement. No unrelated Player or render-pipeline settings were changed.

Passthrough starts **OFF**. ON temporarily uses a transparent black Solid Color background and disables HDR/post-processing on this camera so URP preserves alpha. All puzzle objects, floor/table geometry, hands, and UI keep rendering normally over the passthrough underlay. OFF restores the camera's exact previous clear flags, background color, HDR, and post-processing settings. Opaque virtual geometry still covers the corresponding part of the real world by design.

`IsSupported`, `IsPassthroughEnabled`, `SetPassthroughEnabled(bool)`, and `TogglePassthrough()` remain the reusable API. `StateChanged` updates the hand-menu toggle when support/state changes, including delayed XR initialization. Support is checked at most twice per second without scene searches. An unavailable provider or a subsystem that cannot start leaves the normal background intact. State reflects the running camera subsystem; the native compositor's visible output still requires a headset test.

Source verification used the installed 2.6.1 package's `Documentation~/features/camera/passthrough.md`, `composition-layers.md`, graphics/scene setup documentation, camera code samples, `ARCameraFeature`, `MetaOpenXRCameraSubsystem`, `MetaOpenXRPassthroughLayer`, and `PassthroughLayerCreateUtil`. These sources explicitly prescribe ARCameraManager enable/disable and the automatic underlay. The sample image-capture code was inspected but is not used for this milestone.

Modified by this follow-up: `PassthroughController.cs`, `HandMenuController.cs`, `SampleScene.unity`, `OpenXRPackageSettings.asset`, the Meta Quest build profile, global Player settings, and this report. Pre-existing user/package-install changes were retained. Temporary setup/test helpers were removed.

Validation: scene setup/import/compilation passed (exit 0). Twelve temporary Play Mode assertions passed (exit 0): OFF startup, unsupported-device behavior, state notifications/toggle synchronization, transparent camera setup, exact restoration including repeated requests and component disable, hand-menu opening/retry, one runtime EventSystem, and one active XR rig. Final batch compilation/import after removing the temporary helper also passed (exit 0), without `-noUpm`. Scene/settings changes were audited against a snapshot of the user's files at the start of this follow-up; package manifest/lock and EditorBuildSettings are unchanged. These are Editor tests; they do not claim successful native passthrough rendering. Logs: `Logs/Passthrough-Setup.log`, `Logs/Passthrough-PlayMode.log`, and `Logs/Passthrough-Final-Compile.log`.

Expected Editor warnings: no active XRSessionSubsystem without a supported XR runtime, the existing gaze-assistance warning, and an explicit unsupported-passthrough warning when the test attempts ON. Unity licensing/account messages also occurred. Quest 3 native compositing and suspend/resume need the manual checks below.

## Volume and restart

No central AudioMixer asset was found. `PrototypeVolumeControl` uses a continuous 0–1 slider to update `AudioListener.volume` immediately. Every menu open refreshes the slider and percentage label from current volume. It does not introduce audio persistence or a new audio system.

Restart hides the menu, then calls the existing `PuzzleGenerator.RetryCurrentPuzzle()`. That method reuses the saved layout and its original shape prefabs, pair IDs, colors, piece/target positions, and rotations. The existing `ClearGeneratedPuzzle` path cancels active XRI selections and deactivates old pieces before replacement. No scene reload or random regeneration was added. The menu subscribes to `PuzzleSpawned` to close safely after either retry or new-puzzle generation; completion UI continues to receive its existing events.

## Validation

Unity version: **6000.3.23f1**. Batch runs used the existing project and normal UPM resolution, never `-noUpm`.

- Scene-authoring/import/compilation: passed, exit 0.
- Temporary automated Play Mode checks: passed, exit 0. Synthetic world-space palm poses drove the real controller's visibility logic; absent tracking exercised the actual subsystem lookup path. These checks do not emulate Quest tracking or pointer input.
- Checks covered: absent tracking, show/hide, acquisition/loss debounce, angular hysteresis, gaze failure, open/close button listeners, exclusive visibility, inactive raycasters, volume refresh/mute/full range, following/smoothing, unsupported passthrough state, exact retry layout/shape/color/pair/pose preservation, actual XRI selection cancellation, new-puzzle survival, completion UI interaction, re-enabling listeners, and one runtime EventSystem/menu.
- A Unity-rendered preview was inspected; slider geometry and panel sizing were corrected, then all Play Mode checks passed again.
- Final compilation/import after removal of temporary helpers: passed, exit 0; see `Logs/HandMenu-Final-Compile.log`. Final scene checks also verified that every pre-existing object block is unchanged and all new local references resolve. `git diff --check` passed.

Local artifacts (ignored by Git): `Logs/HandMenu-Build.log`, `Logs/HandMenu-PlayMode.log`, `Logs/HandMenu-PlayMode-result.txt`, `Logs/HandMenu-preview.png`, and `Logs/HandMenu-Final-Compile.log`.

Initial sandbox execution could not connect to Unity licensing; authorized runs outside the sandbox succeeded. Unity logged account/entitlement lookup messages and the existing `XRGazeAssistance` warning, “No ray and select interactor found!” The final batch shutdown logged “Curl error 42: Callback aborted” but exited successfully. No runtime hand-menu errors occurred in the passing run. The known Quest SSAO performance concern was left unchanged. Early temporary-helper assertion/compiler issues were corrected before the passing run; the helper is not shipped.

## Manual Unity and Quest checks

1. Open `SampleScene`. Inspect `Hand Menu Root` references and its two inactive canvases. Enter Play Mode without XR Hands tracking: neither UI should appear. Confirm one EventSystem appears and existing puzzle/completion UI still work. Simulator visual hand poses alone may not supply XR Hands joint tracking.
2. Build/run on Quest with the existing Android/OpenXR configuration. Put down the controllers and obtain working hand tracking. Bring the **left palm** roughly 30–60 cm from the headset; turn its palm toward your face and look at it. The Menu launcher should appear after a short delay. Small angle changes should not flicker it.
3. Turn the palm away, look away, and cover/lower the hand. Check that UI hides; tracking loss should hide it immediately. Reacquire the hand and check that the UI appears at the current hand position rather than its old position.
4. Select Menu using the other hand's far ray/pinch, then test near poke. Check that the launcher hides, the panel follows the menu hand smoothly, and controls are readable without clipping into its mesh. Test Close and reopening. Where the device supports concurrent tracked-hand/controller input, test the other controller's UI ray too; a controller-only session intentionally has no palm launcher.
5. Drag volume through 0, 0.5, and 1. Verify sound level changes if audio is playing and the label updates. Close/reopen and verify the slider reflects current volume. This milestone adds no audio source.
6. Move/hold a puzzle piece, note shapes/colors/pairs, and press Restart Puzzle. Confirm the grab releases safely, the menu closes, and the same puzzle returns to original positions. Complete a puzzle and restart from the hand menu; verify completion UI dismisses. Also use completion UI's New Puzzle and Retry Puzzle, then reopen the hand menu.
7. On Quest 3, verify the app starts with the original virtual background and Passthrough unchecked. Open the hand menu and switch ON: the room should appear behind virtual content while the puzzle, hands and UI stay visible. Close/reopen the hand menu and confirm the toggle remains ON. Switch OFF and confirm the original virtual background returns. Repeat several times, including after Restart, puzzle completion, and New Puzzle. In the Editor without a Meta camera provider, the toggle should remain Unavailable.
8. Move/turn/recenter using the existing rig and repeat the palm test. Suspend/resume the app and reacquire hands. Check for duplicate UI, stale positioning, and regressions in grabbing, controller UI, and completion UI.
9. Outside Play Mode, change `Hand Menu Root > Hand` to Right, build/run, and repeat the visibility and interaction checks. Tune Inspector angles, offsets, and smoothing only if headset testing indicates a comfort/readability issue.
