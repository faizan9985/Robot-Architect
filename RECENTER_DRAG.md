# Recenter button: tap to reset, drag to move

The existing Tools-row Recenter icon and 80 x 50 mm UI target are retained.
The navigation bar's thick Grab Border GameObject, Image, Rigidbody,
XRGrabInteractable, and four BoxColliders are removed. Its original thin rounded
presentation and animated Undo/Redo/Speed/Zoom controls remain unchanged.

`RecenterWorkspaceButton` consumes the existing XR UI module's pointer press,
drag, and release events. No new pinch detector, physics target, input rig,
package, Canvas, or movement authority is introduced.

- A valid release within 0.6 seconds, with movement below 12 mm, calls the existing
  `RemixTableManager.RecenterWorkspace` command. Position, orientation and vertical
  height return to their defaults through the existing alignment pipeline.
- Crossing the configurable world-space threshold latches a drag. Returning to
  the starting point cannot turn it back into a tap. A long stationary hold or
  canceled press performs no recenter action.
- Dragging calls the existing workspace interaction/placement code. It retains
  the original grab offset and 25 ms smoothing; orientation and vertical scale
  remain unchanged. Every workspace canvas, waveform and sphere follows the same
  root. Releasing retains the last rendered position.
- Pointer click-on-down and the following Button click are suppressed for a
  captured press. Keyboard/programmatic Button clicks still use Recenter.
- Movement samples the UI interactor's world ray at a fixed press distance,
  including ray translation in Z. The moving button's plane is never used to
  calculate subsequent positions, avoiding feedback loops. Shortened ray paths
  extrapolate to the captured distance.
- Ownership uses the existing external-hold mechanism. Other UI and ribbon
  edits are blocked during capture; two-hand playhead scaling remains exclusive.
  Pointer loss, tracking loss, focus loss, disable, cancellation and Quest system
  recenter release capture without dispatching a tap.

Native Quest recenter retains the established session offset/vertical scale;
an explicit short Recenter tap resets both. Song changes preserve session
placement. Workspace transforms are not stored in song JSON or across launches.
No holographic shader, audio DSP, sampling, waveform geometry, or sphere palette
is changed.

## Changed files

- New runtime component: `Assets/RemixTable/Runtime/RecenterWorkspaceButton.cs`
  and its `.meta`.
- Updated runtime: `RemixWorkspaceInteraction.cs`, `RemixTransport.cs`, and
  `RemixIconButton.cs` (tooltip discoverability).
- Updated migration/reference validation: `Editor/RemixWorkspaceLayout.cs`.
- Updated scene: `Scenes/RemixTable.unity`.
- Tests: `EditMode/WorkspaceInteractionTests.cs`, `EditMode/SeatedLayoutTests.cs`,
  and `PlayMode/WorkspaceInteractionPlayTests.cs`.
- Documentation: this report and the supersession note in `SPATIAL_WORKSPACE.md`.

## Hardware follow-up

Automated validation does not establish headset performance or physical tracking
quality. Quest checks remain for near/far pinch capture, depth movement, jitter
around the 12 mm threshold, release outside the button, recenter cancellation,
and two-hand playhead stretching after a drag.

## Completed validation

- Unity 6000.3.23f1 compilation: passed.
- Full Edit Mode: **368 passed, 0 failed, 0 skipped**, including graphics tests.
- Full Play Mode: **35 passed, 0 failed, 0 skipped**. The registered-XRI-source
  regression verifies fresh pointer events are used during callbacks, before XRI
  commits the updated public UI model; held movement reads the committed model.
- Scene/serialized references, XR setup, navigation animation, audio, materials,
  Vulkan shaders and packaged-asset validation: passed.
- Metadata: **1,152 unique asset GUIDs**. Source/isolated-copy parity, protected
  file hashes, whitespace and staged credential-pattern checks: passed.
- Existing Meta Quest profile APK: `Builds/RemixTable-RecenterDrag.apk`,
  **130,098,614 bytes**. Signature verified. Private credential audit passed
  across 443 APK entries and 470 decoded UnityFS blocks. The blank tracked
  DevAgent template and existing credential guard were used in isolation.
- Local evidence: `Logs/RecenterDrag/EditModeFinal.xml`, `PlayModeFinal.xml`,
  and `QuestBuildFinal.log`; logs and APK are excluded from Git.
- No physical Quest validation was performed for this change.
