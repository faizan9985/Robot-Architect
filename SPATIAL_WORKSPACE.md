# Session-only spatial workspace controls

The Remix Table scene uses `RemixWorkspaceInteraction` on the existing manager.
`RemixWorkspaceRoot` remains the placement authority. Its `RibbonViewport` child
contains the four lanes and the fixed playback indicator; only that child's Y
scale changes. The six workspace canvases and `SpatialStemField` are outside the
scaled child. Lane labels counter-scale vertically to retain their physical size.
UI roots shift above/below the resized lanes without scaling buttons or text.

## Controls

- Pinch the central playhead with both hands, initially at least 6 cm apart.
  Vertical separation scales the height continuously around the workspace center.
  The default height is 34.8 cm; Inspector limits default to 25-65 cm. Capture
  radius is 3 cm. Smoothing uses a 35 ms time constant. Releasing either hand
  retains the current height and requires both hands to open before ribbon edits.
- Pinch the thick violet navigation frame to move the complete workspace in XYZ.
  Four 10 mm border bands leave the original buttons' interiors unobstructed.
  Existing XRI selection supplies ownership and the moving attach point; the
  workspace follows with a 25 ms time constant while retaining its orientation.
  The frame and existing colliders resize with animated Speed/Zoom expansion.
- Tools / Recenter restores the seated headset-relative pose and height 1x.
- Tools / Master Volume opens the migrated volume slider and gesture guidance.
  Tools / Room retains passthrough control. No palm gesture opens any Remix UI.

## Ownership and lifecycle

The existing ribbon hand samples drive playhead acquisition before ribbon edits
or timeline zoom. Both scaling and XRI frame dragging reserve the existing
external-hold authority, suppress transient ribbon contact feedback, block UI
selection, and temporarily disable sphere selection. No duplicate pinch detector,
XR rig, EventSystem, AudioSource, or waveform collider is introduced.

Tracking loss, interactor removal, focus loss, cancellation, and recenter release
ownership. Selection exits restore the original CanvasGroup flags. A release gate
prevents a hand still pinching after scaling from starting an unintended edit.
Frame selection does not translate its own XRGrabInteractable: the manager moves
the common root once, avoiding double transforms.

The manager stores a nonserialized offset in the last seated pose's frame. Song
changes and mode changes leave placement and scale alone. Native Quest recenter
re-applies that offset in the new headset-relative pose and retains height; the
explicit Tools Recenter button clears both. A fresh scene/application starts at
the default pose and 1x. Per-song JSON has no workspace transform fields.

## Removal and preservation

The Remix scene's Hand Menu Root, Palm Launcher Canvas, Hand Menu Canvas, palm
activation component, and their event bindings are removed. Shared
`HandMenuController` assets remain for the separate RobotArchitect scene.
The shared passthrough controller remains on a renamed `Workspace Services`
object with its original camera and Tools references. `PrototypeVolumeControl`
and its original slider were migrated into the existing Tools canvas.
Recenter and Master Volume use two new atlas sprites; existing
sprite IDs and action references remain intact.

Waveform meshes, audio sampling and automation, sphere shaders, waveform shaders,
and the approved holographic hover/grab coefficients are unchanged. Vertical
scaling acts through transforms, including analytical interaction coordinates.
The navigation border is the only new physics target; its Image cannot raycast.

## Validation and device follow-up

Automated suites cover scaling acquisition, smoothing/clamping, unchanged local
meshes, timestamps and zoom, unchanged UI/sphere sizes, border/button separation,
actual XRI drag/release/interactor loss, session/song lifecycle, explicit/native
recenter, allocation budget, references, and palm removal. Existing holographic,
audio, automation, toolbar and persistence suites remain required.

Physical Quest checks remain necessary for playhead reach, two-hand tracking,
border grabbing beside expanded buttons, seated comfort at both height limits,
native recenter after manual placement, and perceived drag latency. Automated
tests and an Android/Vulkan build do not establish on-device performance.

## Changed-file inventory

- Runtime: new `RemixWorkspaceInteraction.cs`; updated
  `RemixTableManager.cs`, `RibbonInteractionManager.cs`, `SpatialStemField.cs`,
  `RemixTransport.cs`, `RemixIconButton.cs`, `RemixUiPresentation.cs`.
- Editor: new `RemixWorkspaceLayout.cs`; updated `RemixSceneBuilder.cs`,
  `RemixSeatedLayout.cs`, `RemixModeLayout.cs`, `RemixSpeedLayout.cs`,
  `RemixZoomValidation.cs`, `RemixUiLayout.cs`, `VocalsBuildValidation.cs`.
- Edit Mode tests: new `WorkspaceInteractionTests.cs`; updated
  `NavigationToolbarTests.cs`, `RemixIconUiTests.cs`, `RibbonContactGlowTests.cs`,
  `SeatedLayoutTests.cs`, `TimelineZoomTests.cs`.
- Play Mode tests: new `WorkspaceInteractionPlayTests.cs`; updated
  `WorkspaceRecenterPlayTests.cs`, `PlaybackTests.cs`,
  `RibbonContactGlowPlayTests.cs`, `SeatedWorkspaceTests.cs`.
- Assets/tools: `Assets/RemixTable/Scenes/RemixTable.unity`,
  `Assets/Resources/RemixUiAtlas.png` and its metadata,
  `Tools/GenerateRemixUiAtlas.py`, and this report. Each of the four new C# files
  has a new `.meta` with a unique GUID. No existing script GUID was changed.

## Completed automated validation

- Unity 6000.3.23f1: compilation passed.
- Full Edit Mode: 366 passed, 0 failed, 0 skipped (graphics enabled, including
  existing rendering assertions).
- Full Play Mode: 34 passed, 0 failed, 0 skipped.
- Scene/serialized references, XR prefab/manager, audio, seated layout, timeline,
  UI and holographic materials: passed in the Quest build preflight.
- Vulkan: waveform and sphere shader stages passed. Required materials, shaders
  and UI atlas passed BuildReport packed-asset checks.
- Metadata: 1,151 unique asset GUIDs; all 29 existing sprite IDs and icon tiles
  preserved. Whitespace and protected-file checks passed.
- Quest profile APK: `Builds/RemixTable-SpatialWorkspace.apk`, 130,091,286 bytes.
  Signature verified. Private audit scanned 443 APK entries and 470 decoded
  UnityFS blocks; development credentials were absent. The tracked blank
  DevAgent template and unchanged credential guard were used in isolation.
- No physical Quest validation was performed for this milestone.

Local test/build evidence is under `Logs/WorkspaceInteraction/`:
`EditModeFinalValidated.xml`, `PlayModeFinalValidated.xml`, and
`QuestBuildFinal.log`. These logs and the APK are excluded from the commit.
