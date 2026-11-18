# Four-stem holographic waveforms

This milestone carries forward the approved, previously uncommitted Filter
visibility correction and extends the same renderer to Drums, Bass and Other.
Vocals' approved appearance is the reference, not a redesigned approximation.

## Shared implementation and compatibility

`RibbonView.Awake` now initializes the shared renderer for every stem. The
`VocalsHologram` class/file and shader name retain their original names and GUIDs
for compatibility. Initialization accepts a StemId; the full mesh, depth-shell,
halo, automation upload, lifetime and shader effect logic remain shared.
No existing scene, XR rig, input configuration, navigation toolbar or interaction
hierarchy is serialized differently. Runtime adds only the two visual children
per ribbon, with no colliders/interactors. Other is the existing `StemId.Melody`.

Vocals keeps its original body/halo materials and the original shader constants.
The three other stems share one new body/halo material pair, enabling the local
`_STEM_PALETTE` shader variant. This compile-time split avoids a one-code-value
rounding difference caused by a runtime palette branch in the initial refactor.
There is still one common shader and one shared set of effect equations.
No shader/component rename or GUID migration is required.

There are four material assets total, not one clone per renderer. Per-stem
MaterialPropertyBlocks are built once, decoded from sRGB with Color.linear once,
and copied to the front/shell/halo renderers. Shader Vector properties receive
Linear values directly; they are not Color properties and are not decoded twice.
Vocals receives no palette property block, preserving the approved constants.
No palette conversion, material creation or property-block update happens per frame.

## Palettes (sRGB targets)

| Stem | Primary | Shadow | Highlight | Contour | Low-pass target |
|---|---|---|---|---|---|
| Vocals | #B49AFF | #6441C8 | #E8D6FF | #F0DFFF | #7D33F0 |
| Drums | #FFB26B | #A83C08 | #FFE3AB | #FFECCC | #DC651C |
| Bass | #54DBAA | #08764C | #B8FFE3 | #C9FFEB | #138E57 |
| Other | #5DD6EF | #087B96 | #CCF7FF | #DCFAFF | #1689AD |

The existing dimensional fill, 0.45 mm contours, 3 mm radial shell and 6 mm
halo width are shared. Each palette's dark fill and controlled aura retain the
stem's identity. The original Vocals material assets themselves are unchanged.

## Automation and geometry

Every original mesh vertex evaluates that ribbon's Filter, Pitch and Gain from
the authoritative AutomationStore at its existing Timestamp(x). UV2 carries
filter, semitones, harmonic density and harmonic strength to all three surfaces.
No global automation material value or second visual automation system exists.

Pitch retains the existing horizontal harmonic pattern (8 mm at -12 semitones,
2 mm at +12, absent at neutral). Filter retains the corrected smooth 65 mm
low-pass bands, darker saturated fill/softer contours, and fine diagonal
high-pass streaks/sharper illumination. These are parameter-driven metaphors,
not spectral audio analysis. All color operations use the chosen stem palette.

Gain continues to sculpt the original amplitude silhouette. No audio sampling,
smoothing, envelope math, timestamp, topology, DSP, gesture or persistence format
changes were made. Depth/halo arrays derive from the current final front vertices,
so they follow Gain automatically. Release/history/reset/save-load/song switching
continue through existing automation, while playback speed/loops/zoom/recenter
use the existing clock and workspace transforms.

## Rendering budget

At the saved 512 columns / 2,048 segments per stem:

- 12 waveform renderers/passes: 4 front, 4 depth shell, 4 halo.
- Four shared material assets; zero runtime Material clones.
- 81,960 vertices and 98,304 triangles across the four full arcs.
- Compared with the single-stem prototype: 6 additional waveform draws.
- Eight opaque/depth-writing surfaces and four narrow transparent halo strips.
  Full-fill additive planes, bloom and particles are not introduced.
- Fixed bounds and reused mesh/attribute buffers; all-four-stem update allocation
  is measured by Play Mode tests.

These are structural pass/geometry counts, not headset profiler measurements.
Property blocks on the nine non-Vocals surfaces prevent their use of the SRP
Batcher; they are immutable after initialization. No claim of stable Quest FPS,
thermal behavior or stereo GPU cost is made without eventual on-device profiling.

## Validation coverage

Existing Vocals material-isolation assertions were updated to the new all-stem
contract, with stronger per-stem material, palette, neutral-automation and
collider checks. No numeric readability, waveform accuracy, allocation or
interaction assertions were relaxed. Existing automation/GPU fixtures run for
all four stems, rather than only Vocals.

Coverage includes exact primary targets and Linear property-block values;
all-stem front/shell/halo bindings; shared material counts; every vertex's
region-local Filter/Pitch values; separate stem automation; Gain deformation;
source cache/audio provenance; release, undo/redo, reset, real disk save/load,
wrapped regions, song changes, all transport speeds, LIVE editing, snapping,
loops, zoom, existing recenter tests and managed allocation.

Twelve Vocals fixture images (neutral, both Pitch extremes, regional Filter,
low/high-pass at 6/16 mm and normal/selected intensity) are compared against
fresh renders of the approved pre-refactor shader under the same settings.
All twelve match exactly in every channel. Logs/images remain outside Git under
`Logs/FourStemHolograms`. Desktop previews do not save the scene or alter the rig.

Builds use the existing Meta Quest profile in an isolated copy with the verified
tracked blank DevAgentSettings template and unchanged credential guard. The
user's five pre-existing settings/credential changes are hash-checked and excluded
from the feature commit. Required material/shader references are checked in the
actual packed build report. No push or merge into main is part of this milestone.

## Final automated results

- Unity compilation and scene/reference/material/shader validation: passed.
- Full final Edit Mode: 273 passed, 0 failed, 0 skipped.
- Full final Play Mode: 29 passed, 0 failed, 0 skipped.
- Approved Vocals: all twelve complete fixture images match exactly, every
  channel, with the final CPU-decoded palettes and shader variants.
- New palettes pass the original low/high-pass contrast and harmonic-spacing
  assertions at normal and selected brightness; no thresholds were relaxed.
- Quest APK build: succeeded with the existing Vulkan-only Meta Quest profile.
  Packed-asset validation found both original materials, both shared stem
  materials and the common shader.
- Output: `Builds/RemixTable-FourStemHolographic.apk` (129,829,626 bytes).
- APK signature and copied-artifact SHA-256: passed.
- Credential audit: 443 APK entries and 459 decoded Unity player blocks;
  SDK-restored token absent. Isolated settings reset to the tracked blank
  template after the audit; original credentials were not modified.
- Logs, test XML, palette renders, material manifest, credential audit and APK
  hash are in `Logs/FourStemHolograms`, excluded from Git.

Remaining device validation: all-four-stem GPU/CPU and thermal cost, stereo
aliasing at the +/-12 extremes, per-palette passthrough contrast, and readability
at seated distance while turning toward the edges of the 180-degree workspace.
No Quest hardware performance or stability claim is made from desktop tests.

## File manifest

Modified:

- `Assets/RemixTable/Runtime/RibbonView.cs`
- `Assets/RemixTable/Runtime/VocalsHologram.cs`
- `Assets/RemixTable/Shaders/VocalsHolographic.shader` (retains approved Filter fix)
- `Assets/RemixTable/Editor/VocalsBuildValidation.cs`
- `Assets/RemixTable/Tests/EditMode/VocalsHologramTests.cs`
- `Assets/RemixTable/Tests/EditMode/VocalsAutomationTests.cs`
- `Assets/RemixTable/Tests/EditMode/VocalsAutomationRenderTests.cs` (retains and
  expands the previously uncommitted Filter readability assertions)
- `Assets/RemixTable/Tests/PlayMode/VocalsHologramPlayTests.cs`
- `Assets/RemixTable/Tests/PlayMode/VocalsAutomationPlayTests.cs`

Created (including matching metadata for each new Unity asset):

- `Assets/RemixTable/Runtime/StemHologramPalette.cs` and `.meta`
- `Assets/Resources/StemHolographicBody.mat` and `.meta`
- `Assets/Resources/StemHolographicHalo.mat` and `.meta`
- `Assets/RemixTable/Tests/EditMode/StemHologramTests.cs` and `.meta`
- `Assets/RemixTable/Tests/PlayMode/StemHologramPlayTests.cs` and `.meta`
- `FOUR_STEM_HOLOGRAMS.md`
- `FILTER_VISIBILITY_FIX.md` (pre-existing untracked correction report, retained
  as historical context and included with the completed dependent feature)
