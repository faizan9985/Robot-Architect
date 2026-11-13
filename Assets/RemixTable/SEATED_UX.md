# Seated Remix Table UX

This revision updates the existing scene in place. It does not regenerate the XR rig, audio engine, four AudioSources, song data, automation store or hand-menu controller. The pre-edit scene (including the user's uncommitted instruction positioning) is preserved locally at Logs/RemixSeatedBefore.unity. No original Robot Architect scene changes are required.

## Layout and placement

| Item | Authored value |
| --- | --- |
| Rectangular panel | 0.74 m wide x 0.40 m high |
| Ribbon waveform width | 0.64 m |
| Row order, top to bottom | Vocals violet, Drums orange, Bass green, Other cyan |
| Row center spacing | 0.09 m |
| Row centers relative to panel | +0.135, +0.045, -0.045, -0.135 m |
| Per-lane visual / hit height | 0.078 m |
| Four-lane interactive vertical span | 0.348 m |
| Gap between lane hit zones | 0.012 m |
| Ribbon half-thickness | 0.004 m; selected maximum 0.0072 m |
| Neutral procedural amplitude parameter | 0.012 m |
| Fixed playhead | x=0, height 0.348 m |
| Transport | 0.74 x 0.10 m, centered at local y=-0.265 m |
| Panel-to-transport gap | 0.015 m |
| Overall frame plus transport height | 0.515 m |
| Button touch targets | Previous/Next 0.12 x 0.05 m; Play/Pause 0.16 x 0.05 m |
| Initial placement | 0.62 m forward, 0.20 m below initial camera eye position |

The first camera pose after the existing one-second tracking settle period determines workspace position. Gaze is projected onto the horizontal plane, with upright world Y. The root stays in world space under RemixTable; it is not parented to a camera or hand and does not follow subsequent head motion. No tabletop, room mesh or fixed standing eye height is assumed. Restart/reapply to reposition; automatic recenter handling is not added.

These are configurable starting values in Materials/RemixDesign.asset. After changing width, thickness, lane height, spacing, panel size or transport size, run Tools > Remix Table > Apply Seated Layout (existing scene), then validate. Apply reads the authored values instead of restoring defaults. Distance and eye drop are used by initial runtime placement. Keep lane height below spacing and all four lanes inside panel height. The runtime uses the same width for mesh geometry and timestamp sampling. Existing song-relative edit timestamps do not change when physical width changes.

A simple reach check assumes a shoulder midpoint 0.20 m below the eyes and checks the panel/transport interaction points within 0.80 m of that point. This is a layout assumption, not a guarantee for individual seated users or a validated ergonomic standard. Quest sessions must tune reach for different arm lengths, chairs and seated heights.

## Structure

RemixWorkspaceRoot (existing workspace reference)
- WorkspaceFrame: opaque unlit rectangular backing, lane backgrounds, small accents; no colliders/shadows
- RibbonViewport: the same four procedural RibbonView objects and AudioSources, plus FixedPlaybackIndicator
- TransportCanvas: world-space UGUI, existing font, TrackedDeviceGraphicRaycaster, three real Buttons and cached title/time labels

Ribbons are 3D meshes outside every Canvas. AudioSource array order and StemId remain unchanged: Drums=0, Bass=1, Melody/Other=2, Vocals=3. Only the spatial row order changes. Old song-picker/instruction objects are retained under inactive LegacyWorkspaceDecor, outside the workspace, so references are not destroyed and there is only one active song-selection interface. Ambient particles are disabled. Small lane labels occupy the gaps above their wave bands; a measured renderer-bounds fit prevents the old oversized TextMesh labels from covering the waveforms.

No scrolling was needed: four 78 mm lanes and three 12 mm gaps fit a 348 mm interactive span. There is no ScrollRect or scrollbar, and no offscreen interactive content needing a scroll mask.

## Hit zones and deformation

The original ribbons had no interaction colliders: custom joint-to-wave proximity selection owns input. Adding physics colliders would duplicate that system. Each seated lane now gates initial selection to an analytical local box (0.656 x 0.078 x 0.060 m), then checks 32 mm proximity to the waveform. The boxes have a 12 mm vertical gap. Frame decorations and transport have no physics colliders. UI buttons use the XRI graphic raycaster.

Once selected, vertical dragging can leave the lane without switching stems; the existing session remains the owner. Hand-to-ribbon inverse transforms, horizontal timestamps, two-hand boundary updates, one-hand gain mapping and editing-pause ownership are preserved. Visible waveform centers are bounded to +/-28.8 mm; selected thickness and capped +/-3 mm reactive displacement keep vertices within the 78 mm lane. Normal 0..2.5 gain waveforms fit; extreme historical visual pitch/effect offsets saturate at the lane edge. Only display geometry is bounded: stored automation and audible gain are not clamped differently or erased.

## Transport and palm menu

Persistent Button bindings are PreviousSong -> library.Select(index-1), NextSong -> library.Select(index+1), and TogglePlayback -> audioManager.Pause()/Resume(). Navigation preserves a manual pause. Song switches use the existing manager path, cancel selection, reset the shared timeline for the new song and retain per-song edits. During an editing pause, Play changes the manual intent but does not override editing owners. Silent placeholders display No audio / AUDIO UNAVAILABLE and disable Play; they do not reuse NIVIROTest recordings.

The existing XRI UI-interactor registration creates the one runtime EventSystem/XRUIInputModule. No EventSystem or input module is added to the saved scene. The new world-space canvas uses the inherited camera and a TrackedDeviceGraphicRaycaster, matching the palm UI. Near-hand UI routing still requires physical Quest verification. No claim is made that synthetic Button.onClick tests reproduce finger-poke tracking.

The existing left palm canvas contains the new static guidance. Its panel is 360 x 430 canvas units at the inherited 0.00085 scale (0.306 x 0.3655 m). Passthrough toggle, volume slider, close/open controls, references and the HandMenuController script are preserved; controls are repositioned to fit the guidance without overlap. Instructions inherit the menu's existing visibility. The main workspace has no active gesture instructions.

Guidance: PINCH + LIFT / GAIN; TWO HANDS / REGION; TWIST / FILTER SWEEP; HOLD ENDPOINT / WHOLE STEM. Gain and 0.65-second endpoint whole-stem editing are implemented and tested. Twist remains a visual preview, not audible effect DSP. Existing stored pitch records remain readable; no new pitch gesture is introduced.

## Performance and checks

Two small opaque unlit surface materials, no added lights, no fullscreen transparent backdrops, no new particles, no layout groups or per-frame UI construction. Mesh vertex/color arrays are reused. UI text refreshes only when the displayed second, song, playback intent, audio readiness or editing state changes. Existing URP/XR settings remain unchanged.

Desktop tests cover lane and analytical hit-zone separation, label separation, extreme geometry bounds, local hand/timestamp mapping, pose/reach assumptions, UI target sizes and persistent bindings, guidance visibility/control non-overlap, one runtime XRUI module, real button actions, manual/editing pause precedence, song-specific gain restoration and world stability after head movement. Existing audio, gesture and automation regressions are also run. Logs and a desktop preview are under ignored Logs/. These are not Quest hardware tests.

Quest 3 acceptance still required: seated reach for multiple body sizes, panel/text readability and contrast in passthrough, near-hand poke and two-hand selection reliability, pinch hit-zone tuning, palm guidance legibility/occlusion, extreme gain sculpting comfort, stereo depth/playhead clarity, sustained frame time/GPU cost, streaming audio continuity and pause/resume while using UI. No Quest deployment or hardware verification was performed for this revision.

## Validation results and changed files

Unity 6000.3.23f1 compilation passed. Saved scene, inherited rig/palm/passthrough reference validation, one-listener/four-source audio validation and seated-layout validation all passed. Final Edit Mode: 34/34 passed (Logs/RemixSeatedEditFinal.xml). Final Play Mode: 7/7 passed (Logs/RemixSeatedPlayFinal.xml). No failed tests. Preview: Logs/RemixSeatedPreview.png, visually inspected after correcting the legacy label overlap. Headless unit fixtures / desktop OpenXR emit inherited missing-listener and no-headset warnings; the saved scene still validates one active listener. No Quest hardware verification was performed.

Exact files changed, relative to Assets/RemixTable (new Unity assets include their .meta files):
- Runtime/RemixDesign.cs
- Runtime/RemixTableManager.cs
- Runtime/RemixTable.Runtime.asmdef
- Runtime/RibbonView.cs
- Runtime/RibbonInteractionManager.cs
- Runtime/RemixTransport.cs and Runtime/RemixTransport.cs.meta
- Editor/RemixSeatedLayout.cs and Editor/RemixSeatedLayout.cs.meta
- Materials/RemixDesign.asset
- Materials/LaneSurface.mat and Materials/LaneSurface.mat.meta
- Materials/WorkspaceSurface.mat and Materials/WorkspaceSurface.mat.meta
- Scenes/RemixTable.unity
- Tests/EditMode/RemixTable.EditMode.asmdef
- Tests/EditMode/SeatedLayoutTests.cs and Tests/EditMode/SeatedLayoutTests.cs.meta
- Tests/PlayMode/RemixTable.PlayMode.asmdef
- Tests/PlayMode/SeatedWorkspaceTests.cs and Tests/PlayMode/SeatedWorkspaceTests.cs.meta
- README.md
- SEATED_UX.md and SEATED_UX.md.meta
- AUDIO_IMPLEMENTATION.md
- IMPLEMENTATION_REPORT.md

The assembly references use already installed UGUI/TMP/XRI packages; no packages were installed. The original Robot Architect scene, original palm controller script, audio engine, stems/import settings, package manifests and project settings are unchanged. Unity's incidental removal of a build-profile define during testing was restored to the pre-task value without resetting any other files. No commit, push or Farhan integration was performed.

Use RemixTable.Editor.RemixSeatedLayout.Validate for the combined saved-scene/audio/layout check. Apply Seated Layout updates the current scene in place. The older Generate Scene command recreates the legacy prototype; if used intentionally, restore audio with RemixAudioSetup.Configure and reapply the seated layout. Existing custom scene edits should be preserved before using Generate Scene.
