# Holographic spatial spheres

The four existing spatial grab targets now use saturated holographic cores and a
bloom-independent silhouette halo. Waveform rendering, audio DSP, positions,
spatial persistence, XRI selection, and gesture ownership retain their existing
implementation. No scene, XR rig, input, or URP settings were changed.

## Rendering

`SpatialOrbHologram` initializes once through `SpatialStemField`, assigning two
shared Resources materials and one cached MaterialPropertyBlock per sphere. It
reuses the original sphere mesh for a single child halo with no collider,
interactable, rigidbody, or light. Initialization is idempotent.

The new URP shader uses sphere normals and the per-eye view direction rather than
waveform-specific attributes. The opaque core uses directional light-to-dark
shading, a restrained soft highlight, and a colored Fresnel rim. The halo uses
straight alpha, no depth writes, normal depth testing, and an analytic falloff
outside the core silhouette. Only the halo's GPU vertices expand; neither the
grab object's transform nor its collider changes size. Rendering bounds include
that expansion. Radiance compression preserves channel ratios instead of
whitening the color.

The palette comes directly from the unchanged `StemHologramPalette`. Its sRGB
values are decoded once to Linear vectors:

| Stem | Primary | Shadow | Highlight | Edge |
| --- | --- | --- | --- | --- |
| Vocals | #B49AFF | #6441C8 | #E8D6FF | #F0DFFF |
| Drums | #FFB26B | #A83C08 | #FFE3AB | #FFECCC |
| Bass | #54DBAA | #08764C | #B8FFE3 | #C9FFEB |
| Other (`Melody`) | #5DD6EF | #087B96 | #CCF7FF | #DCFAFF |

When all four spheres are visible, the implementation has four core draws and
four halo draws before any platform batching. It adds four transparent surfaces
to the previous four-core implementation, shares two materials and one shader,
and adds no textures, particles, post-processing, packages, or Unity lights. The
halo clips negligible alpha and excludes the core interior. Existing labels are
unchanged. This is a rendering-cost inventory, not a headset performance claim.

## Interaction and mixer priority

`SpatialStemField` samples the actual XRI `isHovered` and `isSelected` state in its
existing LateUpdate. It does not infer a grab from hand proximity or replace any
grab listeners. Idle has continuous rim/halo illumination without pulsing.
Hover modestly boosts the core, rim, and halo. Selection brightens on the first
rendered frame and reaches full intensity in 60 ms. Release uses a 250 ms
smoothstep from the current intensity; regrabbing reverses from the current
value without a jump. Halo radius increases from 1.14 to 1.22 times the core.

Mixer audibility sets the brightness ceiling first, followed by solo feedback,
then grab, then hover. A muted or solo-suppressed grabbed core remains dimmer than
an audible idle core; its grab enhancement remains visible. Solo follows the
existing `StemMix.Audible` rule, including solo winning over mute for that stem.
No mixer logic changed.

The existing recenter cancellation still releases selection and gesture ownership.
Visual state also resets on workspace tracking loss, alignment, component disable,
or leaving spatial mode. `RemixTableManager.WorkspaceTracked` only exposes the
already authoritative tracking sample for that purpose. It does not change
tracking, alignment, or audio decisions. The previous scale-based audio pulse and
held-size multiplier were retired so the authored sphere and collider stay fixed.

## Validation

- Unity 6000.3.23f1: compilation passed in an isolated copy of the project.
- Edit Mode: **294 passed, 0 failed, 0 skipped** (`Logs/SpatialHolograms/EditModeFinal2.xml`).
- Play Mode: **30 passed, 0 failed, 0 skipped** (`Logs/SpatialHolograms/PlayMode.xml`).
- The new tests cover all four exact Linear palettes, shared material references,
  initialization, hover/grab/release, rapid reversals, muted/solo states, unchanged
  transforms/colliders/XRI targets, tracking loss, recentering, and spatial movement.
- GPU render tests verify colored idle halos without bloom, brighter grabbed cores,
  enlarged halos, dimmer muted grabs, and pixel-identical idle restoration. At the
  test camera's seated angular scale, idle halos occupy 437 pixels and grabbed
  halos 1,201 pixels above the luminance threshold for each stem.
- The Play Mode test uses actual XRI selection and movement. Thirty warmed-up
  spatial update calls allocate **0 managed bytes** on the tested editor thread.
- Existing waveform, spatial audio, and other regression tests remain included.
- Shader/material and saved-scene references are checked by tests and the build
  validator. The build validator requires both sphere materials and the shader
  in the player's packed assets, alongside all existing holographic waveforms.

- The sphere shader compiled **4 Vulkan variants**, with no shader errors.
- Quest APK: **succeeded** using the existing Meta Quest build profile; output
  `Builds/RemixTable-SpatialHolographic.apk` (**129,866,038 bytes**).
  All eight required holographic shader/material assets were present in the
  packed-asset manifest. APK signature verification passed.
- APK SHA-256:
  `de878d636a5335f5dbc818c351b3d61797ca0c5cb5f1821a2436d21156ae03f8`.
- The isolated build started with the verified tracked blank DevAgent settings
  template. The existing credential guard remained active. A private scan of
  443 APK entries and 460 decoded UnityFS blocks (60,166,444 decoded bytes)
  confirmed the SDK-restored development token was absent from the player.
  The isolated settings were returned to the blank template after the audit.
  The user's local settings file was never copied or modified.
- Unity metadata validation checked **1,135 unique GUIDs**, all seven new asset
  `.meta` files, and the new material-to-shader references. No duplicates or
  missing new metadata were found. Git whitespace checks passed.
- SHA-256 checks confirm the waveform implementation/materials, scene, mixer,
  and audio manager remain byte-for-byte unchanged. The five pre-existing local
  modifications (DevAgent settings, Quest profile, mobile RP asset, URP global
  settings, and EditorBuildSettings) remain byte-for-byte unchanged and excluded
  from the feature commit.

## Files

- Modified: `Assets/RemixTable/Runtime/SpatialStemField.cs`,
  `Assets/RemixTable/Runtime/RemixTableManager.cs`,
  `Assets/RemixTable/Editor/VocalsBuildValidation.cs`.
- Created: `Assets/RemixTable/Runtime/SpatialOrbHologram.cs`,
  `Assets/RemixTable/Shaders/SpatialHolographic.shader`,
  `Assets/Resources/SpatialHolographicCore.mat`,
  `Assets/Resources/SpatialHolographicHalo.mat`.
- Created tests: `Assets/RemixTable/Tests/EditMode/SpatialOrbHologramTests.cs`,
  `Assets/RemixTable/Tests/EditMode/SpatialOrbRenderTests.cs`,
  `Assets/RemixTable/Tests/PlayMode/SpatialOrbHologramPlayTests.cs`.
- All seven new Unity assets include their `.meta` files. This document is the
  remaining feature file. Generated logs, previews, isolated builds, and APKs are
  excluded from the commit.

## Remaining device checks

No new Quest hardware validation was performed. Check both eyes in passthrough
and dark backgrounds at normal viewing distance; near/far hand and controller
hover/grab; muted/solo appearance; rapid release/regrab; tracking loss and
recenter recovery; and frame time/overdraw with all four spheres and waveforms
visible. The automated render checks use the editor GPU; they do not establish
on-device legibility or stable headset performance.
