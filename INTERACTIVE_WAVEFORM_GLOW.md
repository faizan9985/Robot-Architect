# Hand-responsive holographic waveform glow

All four existing filled waveforms now respond to nearby hands and owned edits
with transient, localized colored illumination. The underlying Gain, Filter,
Pitch, contour, halo, and depth rendering remain intact. Idle illumination is
unchanged. The approved spatial sphere implementation and its shader are unchanged.

## Interaction integration

`RibbonInteractionManager.ProcessHands` first runs the existing gesture state
machine, then publishes visual state. Hover uses the same `NearestRibbon` helper
as pinch acquisition, including existing `CanSelectAt` checks, distance limit,
lane bounds, enabled state, and nearest-target ordering. It never writes an edit.
Pinch illumination follows `active`, `owner`, `selected`, and `TwoHandRegion` from
the existing gesture state machine. No additional pinch detection or targets exist.

Each ribbon has two transient contact slots, one for each hand. Both hands can
hover independently; an owned edit follows the existing single-ribbon ownership
rules. Two-hand region editing can illuminate both actual contact positions on
that ribbon. Overlapping shader contributions use a maximum, avoiding doubled
intensity. Other stems retain their own independent inputs.

Visual feedback is suppressed during zoom gestures, external sphere holds,
workspace alignment/tracking loss, and open palm/navigation menus. The current
XR UI module's authoritative per-interactor UI hover events suppress only the
corresponding hand. The pointer state is seeded once when the module is bound,
then cached without per-frame SDK model lookups. This avoids allocations from
the SDK's failed `GetTrackedDeviceModel` path. Listeners are removed on disable,
destruction, or input-module changes. UI observation does not change gesture logic.

## Intensity reference and shading

Inspection confirmed the committed sphere code exactly matches the requested
reference: core hover/grab contributions **0.50 / 6.5**, rim **1.25 / 8.0**, and
halo **1.25 / 10.0**. `RibbonContactGlow` uses those exact constants; the sphere
code is not rewritten. Tests cover the approved sphere values as well.

The shader uses each hand's hover contribution multiplied by `(1 - grab)`.
Localized core and contour contributions are normalized with `e / (1 + e)` for
the thin waveform surface, then add saturated primary-colored light. A narrower
contact focus identifies the pinch point. The existing halo's opacity is scaled
locally by `1 + 1.25 * hover + 10 * grab`. This preserves the stem palette and
avoids multiplying the entire waveform by the sphere's large grab multiplier.
Existing vertex intensity continues to carry mute, selection, beat, and loop
feedback. No full-screen bloom, particles, lights, or additional geometry are used.

With zero contact input the shader follows the original body and halo paths.
Vocals also retains its original empty property block at idle; other stems retain
their original palette blocks. All 41 captured idle/filter preview PNGs were
byte-for-byte identical to the pre-change captures after full Edit Mode validation.

## Position, falloff, and release

`RemixDesign.contactGlowRadius` configures the falloff in world meters, default
**0.09 m**. Renderer scale is accounted for. The shader converts the original
mesh's cylindrical position to arc length (`atan2(x, z + radius) * radius`) at
vertices, then interpolates this coordinate across segments. Flat timelines use
local X. The support is `1 - smoothstep(0, radius, abs(arc - contactArc))`.
Body, depth, and existing halo therefore use the same coordinate and continuous
falloff without changing any mesh positions, indices, sampling, or amplitude data.

An active contact follows the actual hand projected onto the ribbon arc. Hover
position uses 35 ms exponential smoothing; an owned pinch has no position lag.
The visual pinch ramp uses the same **60 ms** attack as the spheres. On normal
release, strength follows a **250 ms smoothstep** from its current value. A rapid
regrab resumes from that value. If the hand remains hovering, the final state is
hover rather than idle.

The released spot is anchored to authoritative song time and mapped through the
current visible window every mesh update. The previous visible occurrence is
retained when a short song appears more than once in the window. Clock changes,
song wrap, region-loop jumps, zoom, and playback speed therefore use the timeline's
own coordinates, with no independent visual clock or repeating fake waveform.

Explicit cancellation, invalid hand tracking, workspace alignment, application
pause/focus loss, disabled ribbons, and song reset clear transient state. Normal
release retains the existing automation/history finalization and fades only the
new visual state. Nothing is added to saved automation or edit history.

## Performance and compatibility

One cached MaterialPropertyBlock per waveform is copied to its existing body,
depth, and halo renderers. There are no per-frame material instances, managed
contact arrays, or CPU vertex updates solely for contact glow. The existing four
waveforms still have twelve renderer parts and four shared materials, with no
additional draw calls or transparent surfaces. The original XR single-pass shader
macros and URP depth/blending settings remain in place. Shader arithmetic and
parameter upload cost still require device measurement; no headset frame-rate
claim is made.

The old sphere mute tests assumed the earlier low grab coefficients. They were
updated to assert the exact approved 10x values and mute attenuation relative to
the equivalent grabbed state, including a GPU comparison. Runtime sphere behavior
was preserved, rather than reduced to satisfy obsolete idle-brightness assertions.

## Validation

- Unity 6000.3.23f1 compilation passed, including Android player assemblies.
- **Edit Mode: 348 passed, 0 failed, 0 skipped**
  (`Logs/InteractiveGlow/EditModeFinal4.xml`).
- **Play Mode: 31 passed, 0 failed, 0 skipped**
  (`Logs/InteractiveGlow/PlayModeFinal.xml`).
- The 54 added Edit Mode cases cover four-stem palettes/idle inputs, the approved
  intensity reference, per-hand arbitration, ownership, smoothing/release/regrab,
  UI blocking, tracking/cancellation/recenter, persistent Gain/Filter/Pitch and
  undo/redo, flat/curved layouts, 4/32 second zoom, 0.25x/2x speed, wrap/loop jumps,
  world scale, geometry invariance, and shader rendering. The new saved-scene
  Play Mode test verifies all four stems' actual property-block uploads, movement
  of the selected point, release, materials, recentering, and unchanged targets.
- Warmed hover and UI-owned pointer update loops each allocated **0 managed bytes**
  across 30 calls. These checks cover the new visual path, not all existing app code.
- GPU checks use a narrow filled strip at seated angular scale. Hover and pinch
  are distinguishable across neutral, low/high-pass, and +/-12-semitone pitch.
  Pixels outside the radius remain unchanged. Both contacts render independently,
  the existing halo becomes more visible, and zero contact restores identical pixels.
- **41 pre-change idle/filter PNGs remain byte-for-byte identical.**
- Scene/material validation passed. The shader compiled **8 Vulkan variants**
  including both palette paths and instanced stereo, with no shader errors.
- **1,139 unique Unity metadata GUIDs** validated; all four new assets have `.meta`
  files. Existing shader/material GUIDs and the scene are unchanged.
- The rendering inventory remains **12 parts, 4 materials, 81,960 vertices,
  98,304 triangles** for the four waveforms.

- **Quest APK build succeeded** using the existing Meta Quest profile:
  `Builds/RemixTable-InteractiveWaveformGlow.apk` (**129,885,546 bytes**).
  All eight required waveform/sphere shader and material assets are in the packed
  manifest. APK signature verification passed.
- APK SHA-256:
  `bf4c287c2d52213dcbcf69a0a38c33e33c087c520d1838e952bff046172956a8`.
- The isolated build started with the verified tracked blank DevAgent template;
  the existing credential guard remained active. A private audit checked **443 APK
  entries and 460 decoded UnityFS blocks (60,171,228 bytes)** and confirmed the
  SDK-restored development token was absent. The isolated settings were restored
  to the blank template afterward. The user's settings were never copied or modified.
- Protected-file SHA-256 checks passed for the approved sphere script/shader,
  scene, audio manager, automation core, credential file, and Quest/URP settings.
  Staged scope, credential-pattern, metadata, validated-source matching, and Git
  whitespace checks passed. Logs, previews, APKs, and build copies are excluded.

## Files

Created, each with `.meta`:

- `Assets/RemixTable/Runtime/RibbonContactGlow.cs`
- `Assets/RemixTable/Tests/EditMode/RibbonContactGlowTests.cs`
- `Assets/RemixTable/Tests/EditMode/RibbonContactRenderTests.cs`
- `Assets/RemixTable/Tests/PlayMode/RibbonContactGlowPlayTests.cs`

Modified:

- `Assets/RemixTable/Runtime/RemixDesign.cs`
- `Assets/RemixTable/Runtime/RibbonInteractionManager.cs`
- `Assets/RemixTable/Runtime/RibbonView.cs`
- `Assets/RemixTable/Runtime/VocalsHologram.cs`
- `Assets/RemixTable/Shaders/VocalsHolographic.shader`
- `Assets/RemixTable/Editor/VocalsBuildValidation.cs`
- `Assets/RemixTable/Tests/EditMode/SpatialOrbHologramTests.cs`
- `Assets/RemixTable/Tests/EditMode/SpatialOrbRenderTests.cs`

This report is the seventeenth feature file. No waveform materials, scenes,
colliders, XR prefabs, audio DSP files, credentials, or build settings were changed.

## Remaining Quest checks

No physical Quest validation was performed for this milestone. Check perceived
strength against the approved spheres, all four colors in passthrough and dark
backgrounds, the 9 cm support at normal seated distance, both hands and region
editing, automation legibility under contact light, menu blocking, rapid regrabs,
tracking/recenter recovery, and loops at 4–32 second zoom and 0.25x–2x playback.
Measure GPU/CPU frame times and overdraw with the full workspace visible.
