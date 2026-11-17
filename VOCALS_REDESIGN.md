# Vocals holographic waveform

Vocals alone uses `RemixTable/VocalsHolographic`. `RibbonView.Awake` loads the two
materials from Resources only for `StemId.Vocals`; the saved scene, shared design,
legacy RibbonEnergy material/shader, and the other three stems are unchanged.
Resources references keep both materials and their shader in Android players.
No MaterialPropertyBlock or runtime material clone is needed.

The original filled mesh retains its vertex positions, triangle indices, UV0,
audio queries, interpolation, smoothing, amplitude mapping and timestamps.
UV1.w supplies its physical height for consistent contour thickness. Its
existing editing/selection/mute brightness is converted to neutral intensity for
the new violet palette. Source colors and audio/envelope buffers are not mutated.

The single-pass URP shader explicitly decodes the requested sRGB palette into
Linear RGB. An opaque, depth-writing body graduates from #6441C8 below through
#B49AFF to a restrained #E8D6FF upper highlight. Derivative-antialiased #F0DFFF
contours follow the exact upper/lower mesh boundaries, nominally 0.45 mm wide and
capped to 12% of thickness for silent sections.

Two visual-only child renderers are updated from the final vertex array in the
same UpdateWaveform call. The depth mesh repeats the boundary 3 mm radially behind
the front surface and joins the upper/lower sides, shaded to 48% brightness. The
halo consists of two 6 mm outer strips with a power alpha falloff (maximum 0.42
at normal intensity). Only the halo uses straight alpha blending, queue 3000,
ZTest LEqual and no depth writes. The opaque body/shell use queue 2010. The halo
has no full-fill transparent plane, bloom, texture, light, noise or animation.
All passes support instancing/stereo macros; the existing Android Vulkan/OpenGLES
and URP settings are retained.

Children contain only Transform, MeshFilter and MeshRenderer. No colliders,
interactors, ray targets or analytical lane coordinates are changed. Each mesh
and managed buffer is allocated once; updates reuse them and fixed bounds.
At 512 columns, the feature adds two draws, 16,392 vertices and 20,480 triangles.
The four waveform lanes together total 32,784 vertices / 36,864 triangles in six
draws. The original waveforms retain their prior data-driven motion; this feature
adds no motion or reactive embellishments.

Validation entry points:

- Full Edit Mode and Play Mode test suites, including VocalsHologramTests and
  VocalsHologramPlayTests (materials, mesh preservation, depth/halo alignment,
  interaction targets, zoom/gain/silence and managed allocation).
- `RemixTable.Editor.VocalsBuildValidation.Validate` for existing scene/cache
  validation and shader/material checks.
- `RemixTable.Editor.VocalsBuildValidation.Build` builds the existing Meta Quest
  profile to `Builds/RemixTable-VocalsHolographic.apk`, retaining the credential guard.
- `RemixTable.Editor.RibbonVisualValidation.Preview` provides a desktop render.

Build in an isolated project copy with the tracked blank DevAgentSettings template;
never copy the local credential-bearing asset. The build guard is unchanged.
Desktop rendering and automated validation cannot measure Quest passthrough
contrast, stereo comfort, thermal behavior or on-device GPU cost; those remain
for eventual hardware validation.

Changed files (with new Unity assets' matching `.meta` files):

- `Assets/RemixTable/Runtime/RibbonView.cs`: Vocals-only visual integration.
- `Assets/RemixTable/Runtime/VocalsHologram.cs`: visual shell and halo buffers.
- `Assets/RemixTable/Shaders/VocalsHolographic.shader`: body, contours and halo.
- `Assets/Resources/VocalsHolographicBody.mat` and
  `VocalsHolographicHalo.mat`: build-retained material assets.
- `Assets/RemixTable/Editor/VocalsBuildValidation.cs`: preview, validation and
  profile-based APK build with packed-asset checks.
- `Assets/RemixTable/Tests/EditMode/VocalsHologramTests.cs` and
  `Tests/PlayMode/VocalsHologramPlayTests.cs`: automated feature coverage.
- Existing `SpeedSelectorTests.cs.meta` and `SpeedPlaybackTests.cs.meta`: repaired
  invalid 33-character GUIDs so Unity imports the original speed tests. Neither
  invalid GUID had serialized references; the repaired GUIDs are unique.
- `VOCALS_REDESIGN.md`: implementation and validation notes.

Automated results on Unity 6000.3.23f1 (2026-11-17), after the final visual settings:

- Full Edit Mode: 228 passed, 0 failed, 0 skipped.
- Full Play Mode with D3D11: 27 passed, 0 failed, 0 skipped.
- Existing scene/reference, XR/passthrough, audio, waveform, seated-layout and zoom
  validation: passed. New shader/material validation: passed.
- All 199 tracked/feature metadata GUIDs: valid and unique.
- Desktop preview: rendered and inspected; `Logs/VocalsRedesign/Preview.png`.
- Test XML: `Logs/VocalsRedesign/EditMode.xml` and `PlayMode.xml`.
- No Quest hardware session was used. Existing desktop OpenXR initialization and
  SDK warnings occur without a connected headset; both test suites pass.
- Quest APK: succeeded using the existing `Assets/Settings/Build Profiles/Meta Quest.asset`.
  Output: `Builds/RemixTable-VocalsHolographic.apk` (129,813,294 bytes).
  Vulkan shader compilation retained four valid variants. Detailed build report
  confirmed both materials and the shader in packed player assets.
- APK signature and copied-artifact SHA-256 verification: passed.
- Credential audit: all 443 ZIP entries and 459 decoded UnityFS blocks checked;
  the SDK-restored token is absent from packaged data. Meta's SDK restores its
  editor token after the build; the guard clears it before player serialization.
  The isolated settings were reset to the tracked blank template after auditing.
  The user's original local settings remained byte-for-byte unchanged.
- Build/preview logs, packed-asset manifest, credential audit summary and APK hash
  are retained under `Logs/VocalsRedesign` and excluded from Git.

Branch: `Faizan/Vocals-Holographic-Redesign`.
Base: `origin/main` at `b78b7c873542da29deedb6d8e78ff7c5fa47ae44`.
No push or merge into main is part of this milestone.
