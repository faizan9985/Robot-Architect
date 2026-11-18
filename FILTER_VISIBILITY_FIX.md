Historical note: this report records the earlier Filter correction. That approved correction is preserved and incorporated into the four-stem milestone; see FOUR_STEM_HOLOGRAMS.md.

# Filter visibility fix � diagnosis and validation

Scope: `Faizan/Vocals-Holographic-Redesign`. Changes are intentionally uncommitted.
Only `Assets/RemixTable/Shaders/VocalsHolographic.shader`, the existing
`VocalsAutomationRenderTests.cs`, and this report are changed for the fix.

## Root cause

The Filter data path is correct. `ParameterType.Effect` is bipolar [-1,1], with
negative low-pass and positive high-pass. `RibbonView.UpdateWaveform` samples
it at each final waveform vertex's authoritative timestamp.
`VocalsHologram.EncodeAutomation` uses the same safe clamp as audio; UV2.x is
then uploaded to the front, shell and halo. There is no subsequent overwrite,
filter keyword or material property block that disables it. Pitch shares UV2
and continues to work. The body material selects the opaque body branch; the
separate halo uses its existing alpha blend. Linear color conversion is correct.

The previous implementation had no internal Filter texture: low-pass primarily
scaled brightness, and high-pass modified only a 0.45 mm contour. These cues are
weak on a waveform only a few pixels tall. Selected brightness is 1.8x, which
nearly cancels maximum low-pass dimming during editing (1.8 * .52 = .936).
High-pass contour highlights also have little headroom when selected.
The previous close-up GPU tests checked brightness, not recognizable detail
at normal viewing scale.

Before changing the shader, a narrow-waveform regression reproduced this:
existing data-path and Pitch/close-up tests passed, while both 6 mm and 16 mm
Filter readability cases measured zero internal horizontal contrast and failed.
The diagnostic run was 7 passed / 2 expected failures; see Diagnosis.xml/log.
This supports insufficient spatial contrast, rather than broken automation,
color space, blending, a missing variant or an overwritten shader input.

## Exact rendering changes

- Added one interpolated local X coordinate; no new material property, texture,
  mesh topology, renderer, draw call, automation buffer or per-frame allocation.
- Low-pass now blends toward saturated violet (#7D33F0 target) with broad,
  smoothly varying 65 mm bands across the fill. Strength scales continuously
  with the existing negative filter value. Contours become more violet and
  softer, retaining the prior feather and brightness response.
- High-pass adds thin diagonal internal streaks. Density grows from 48 to 112
  cycles/m with the positive filter amount; amplitude grows continuously too.
  Derivative anti-aliasing and subpixel suppression control aliasing/overdraw.
  The original sharper/brighter high-pass contour treatment is retained.
- These patterns are a visual metaphor driven by the Filter control. They are
  not a claim of FFT, spectral analysis or measured per-frequency audio content.
- Filter detail is applied before the untouched horizontal Pitch harmonic
  calculation. Existing Pitch phase, frequency, strength and its prior Filter
  attenuation are unchanged. Gain, geometry and the other stems are untouched.
- Neutral Filter makes both new pattern contributions zero. Existing gradient,
  3 mm depth, 6 mm halo and all material depth/blend settings are retained.
- Region locality and persistence still come from the existing per-vertex
  automation samples. No changes to release, loops, zoom, speed, history,
  reset, song switching or save/load are needed.

## Regression coverage and measured results

- Full Edit Mode suite: 239 passed, 0 failed, 0 skipped.
- New GPU readability checks cover 6/16 mm waveform heights at both normal
  brightness and the 1.8x gesture selection brightness. Both signs must produce
  spatial detail, and strong effects must have more contrast than mild ones.
- At normal brightness: low-pass horizontal brightness range .1502/.1505;
  high-pass .2439/.2621. During selection: low-pass .1874/.1905;
  high-pass .2909/.3091. All clear the unchanged .12/.08 thresholds.
- Measured detail: 5 broad low-pass bands versus 33 fine high-pass streaks.
- Region pixel tests compare low-pass, neutral and high-pass regions with their
  uniform reference renders, excluding only the existing interpolated boundary.
- Neutral and +/-12 Pitch images were compared against the pre-fix shader:
  zero difference in all channels for all three complete fixture images.
- Existing tests still verify actual automation reaches the real scene meshes,
  shell and halo, plus history, release, disk save/load, region curves/precedence,
  wrapped timelines, gain geometry, materials and unaffected stems.

Artifacts live under `Logs/FilterVisibilityFix`, outside Git. Before/After hold
GPU fixtures; After also has seated-distance captures using the actual cached
Vocals silhouette. The preview initializes the real song duration and hides only
untracked editor hand placeholders in memory. It does not save or alter the scene.

An isolated project copy uses the current existing Meta Quest profile and the
tracked blank DevAgentSettings template. The credential guard stays unchanged.
All five pre-existing local changes (credentials, profile, URP and scene-list
settings) are hash-checked and excluded from this fix. No commit, staging, push
or merge is performed.

Final completion:

- Full Play Mode: 28 passed, 0 failed, 0 skipped, including all playback speeds,
  zoom/loops/history, persistence, unaffected stems and zero allocation checks.
- Scene/material-reference validation and D3D11 shader checks passed. The existing
  Quest Vulkan profile APK build succeeded and packed-asset validation confirmed
  both original materials and the updated shader are included.
- APK: `Builds/RemixTable-FilterVisibilityFix.apk` (129,819,182 bytes).
  APK signature and copied-file SHA-256 checks passed.
- Credential audit: 443 APK entries / 459 decoded Unity player blocks;
  SDK-restored token absent. Isolated settings were reset to the tracked blank
  template after auditing. The original local settings were not modified.
- A stale Unity/Bee worker lock interrupted the first build launch; restarting
  only that isolated build editor resolved it. `QuestBuildFinal.log` records the
  successful build and packed-asset validation.
- No Quest headset session was used. Desktop seated-distance evidence is in
  `Logs/FilterVisibilityFix/After/Seated_Regions.png` (low-pass left, neutral
  middle, high-pass right). Actual passthrough contrast, stereo anti-aliasing and
  device performance still require eventual hardware confirmation.
