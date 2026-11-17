# Vocals Filter and Pitch visualization

This extends the existing holographic shader on
`Faizan/Vocals-Holographic-Redesign`. The two material assets, violet gradient,
3 mm radial depth shell, 6 mm halo geometry, mesh positions/indices, audio data,
DSP, gestures, and the other three stems' renderer paths are unchanged.

`RibbonView.UpdateWaveform` already evaluates Filter (`ParameterType.Effect`) and
Pitch at each final vertex's `Timestamp(x)`. Vocals now encodes those same values
in UV2 and forwards them to the front, rear/side shell, and halo. No separate
visual automation store, global material parameter, material clone, new draw
call, particle system, or post-processing is used. Existing gain, mute,
selection, beat and loop brightness remain; the legacy Vocals filter/pitch tint
is bypassed to avoid applying the effect twice. Other stems keep that legacy tint.

UV2 contains signed filter, semitones, harmonic lines per meter, and harmonic
strength. Filter uses `StemFilter.SafeEffect`, just like audio; pitch is bounded
to the existing +/-12 range. Neutral encodes four zeroes. All data/geometry
buffers are allocated once and reused; UV2 is updated in the same call as the
waveform. The existing samples, timestamps, zoom mapping and amplitude geometry
are not resampled or modified.

Negative Filter progressively darkens the body (up to 48%), reduces contour
contrast, broadens the contour's anti-aliasing feather, and dims/softens the
existing halo. Positive Filter brightens the edge illumination and narrows its
feather, with a restrained halo-brightness increase. Zero leaves the original
shader equations' appearance unchanged. The original gradient colors and
material blend/depth settings are retained.

Nonzero Pitch reveals luminous horizontal harmonics inside the existing fill
and shell. Spacing is `1 / (250 * 2^(semitones/12))` meters: 8 mm at -12 and 2 mm
at +12. Strength grows with absolute pitch adjustment, and returns exactly to
zero at neutral. The phase uses local vertical position, not wall-clock time,
so lines stay anchored to the lane baseline while the real audio silhouette
scrolls. Derivative anti-aliasing and a subpixel fade prevent dense lines from
turning into a bright solid fill. Contours are applied over the internal pattern;
the harmonic pattern does not deform vertices or add a second waveform.

Every update reads the current song's authoritative automation. Release,
undo/redo, reset, loops, song changes and disk save/load therefore need no new
state synchronization. Transport speed changes the song clock; it does not
scale the visual pitch value. Values are exact at existing mesh vertices;
interpolation through a region endpoint occupies at most one existing time
segment (1.95 to 15.63 ms for the 4-32 second zoom range at 2,048 segments).
No additional region smoothing or visual-only automation is introduced.

Automated coverage:

- `VocalsAutomationTests`: neutral buffers, regional curves and precedence,
  wrapped song boundaries, zoom, song scoping, release, history, reset, real disk
  save/load, unchanged positions/colors/indices/materials on unaffected stems,
  preserved gain sculpting, and both octave spacing endpoints.
- `VocalsAutomationRenderTests`: actual D3D11/URP render checks for progressive
  low-pass darkening, reduced contour contrast, brighter high-pass edges,
  tighter high-pitch lines, visible one-semitone adjustments, and neutral reset.
- `VocalsAutomationPlayTests`: all eight transport speeds, live zoom changes,
  looping, release/undo/redo/reset, material isolation and zero managed allocation
  during measured waveform updates.
- A before/after neutral screenshot comparison against the previous committed
  shader, plus existing scene/reference validation and Quest packed-asset checks.

Validation artifacts are kept outside Git under `Logs/VocalsAutomation`.
Builds use the existing Meta Quest profile and unchanged credential guard in
an isolated project copy, starting with the tracked blank settings template.
The user's local credentials and pre-existing settings changes are preserved.
Quest still needs eventual checks of both pitch extremes at seated distance,
filter contrast in passthrough, stereo anti-aliasing and frame/thermal cost.

Final validation (Unity 6000.3.23f1, 2026-11-17):

- Edit Mode: 234 passed, 0 failed, 0 skipped (including actual GPU render tests).
- Play Mode: 28 passed, 0 failed, 0 skipped.
- Neutral before/after waveform viewport: max/mean channel difference exactly 0.
- GPU harmonic test: 5 low-pitch bands and 17 high-pitch bands in the measured crop.
- Existing scene, serialized reference, XR, audio, seated-layout, waveform and
  material/shader validation passed. Shader compiled for D3D11 and the Quest build.
- Existing Meta Quest profile APK build succeeded; required materials and shader
  were verified in the packed build report. APK signature and copy hash passed.
- Output: `Builds/RemixTable-VocalsAutomation.apk` (129,817,166 bytes).
- Credential audit: 443 APK entries and 459 decoded player blocks checked;
  SDK-restored token absent. The original credential file was untouched, and the
  isolated settings were restored to the tracked blank template after auditing.

Changed files: RibbonView.cs, VocalsHologram.cs, VocalsHolographic.shader;
VocalsAutomationTests.cs, VocalsAutomationRenderTests.cs,
VocalsAutomationPlayTests.cs and their matching metadata; this report.
The material assets and the user's existing settings changes are not part of
this commit. No push or merge into main is performed.
