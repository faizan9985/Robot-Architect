# Audio-derived holographic ribbons

The RemixTable scene now renders a continuous flowing ribbon for each stem, using the same signed stereo peak/RMS caches described in [AUDIO_WAVEFORMS.md](AUDIO_WAVEFORMS.md). Original WAVs, cache bytes and scene references are unchanged. No audio extraction, procedural oscillation, per-window normalization, new packages, XR changes or post-processing are introduced.

## Shape and accuracy

512 envelope bins cover the visible time window (7.8125–62.5 ms per bin at 4–32 seconds). The existing multiresolution query preserves every source transient within each bin. Bin centers are anchored in song time, rather than moving with screen columns, so geometry flows with the DSP timeline. Two extra neighboring bins at each edge allow filtering across the visible window and song loop. The lookup's existing 2.90 ms conservative base-bin tolerance still applies.

A symmetric five-tap binomial filter (weights 1,4,6,4,1) supplies the local reference for smoothing positive and negative amplitudes independently. The filter only softens peaks: it never raises a quiet bin above its original amplitude or fills an exactly silent bin. `waveformSmoothing` blends between the original peak and this softer value. `waveformPeakPreservation` imposes a lower bound on retained amplitude; the default .85 retains at least 85% of every sampled peak before gain/display mapping. Setting preservation to 1 keeps full original knot peaks; setting smoothing to 0 bypasses the filter. This is a stylized envelope, not a sample-exact DAW outline.

Between bin centers, cubic smoothstep interpolation gives rounded transitions, zero endpoint slopes and no overshoot. Four subdivisions per bin approximate the curves with shared vertices. An isolated impulse remains a rounded crest instead of a rectangular spike. Its visible extent can reach neighboring knot centers (up to one bin on either side), so Overview is intentionally less time-local than Detail. Existing raw `EnvelopeAt` remains available unchanged for source-accurate analysis and tests.

Automation is evaluated at each final vertex's exact existing timeline timestamp, **after** source-envelope smoothing/interpolation. Consequently a region's gain does not smear into neighboring regions through the source filter. Whole-stem/region precedence, boundary fades and the 2.5x cap remain in `AutomationStore`; audio playback and edit records are unchanged. Gain drag continuously resizes the smooth shape. No temporal filter delays gain edits or shifts future-edit timestamps.

Signed display height is `H * a / (1 + abs(a))`, where `a = filtered amplitude * evaluated gain * waveformAmplitudeScale`. All stems and the entire song share this mapping. `H = max(0, laneHeight/2 - 6mm - waveformBaseline)` reserves room for the thin core, glow and subtle displacement. The saved 78 mm lanes use H = 32.4 mm. The common soft bound accommodates high float WAV peaks and gain above one without crossing lanes. The min/max envelopes surround a stable editable baseline.

Optional audio-reactive motion adds less than 0.25 mm at the default setting. Direction follows the signed imbalance between positive and negative envelope magnitudes, and strength follows RMS activity. Motion is computed from the same song-time data and gain as the shape. It has no wall-clock animation, sine wave, random signal or live output-meter input. Silence and zero gain have zero displacement. With a fixed DSP clock, shape and motion freeze together.

## Design controls

All controls are on the existing `Materials/RemixDesign.asset`:

| Setting | Saved default | Meaning |
| --- | --- | --- |
| `waveformAmplitudeScale` | 1 | Global audio amplitude multiplier before the bounded display mapping |
| `waveformSmoothing` | .7 | Strength of local peak softening |
| `waveformPeakPreservation` | .85 | Minimum retained fraction of every source-bin peak |
| `waveformBaseline` | .0006 m | Minimum **half**-thickness; the colored core is 1.2 mm thick in silence |
| `emission` | 1.8 | Existing overall neon radiance multiplier |
| `waveformGlow` | .35 | Feathered halo intensity, with up to 1.5 mm geometric fringe per edge |
| `waveformMotion` | .25 | Strength of the RMS/asymmetry-driven submillimeter displacement |
| `waveformColumns` | 512 | Audio envelope bin density; set before Play Mode |

The violet/orange/green/cyan palette, peripheral dimming and selection brightness remain. `RibbonEnergy.shader` renders a translucent body, luminous edge strands and a soft external fringe with premultiplied blending. The mesh carries core-edge coordinates, so thin silence and louder envelopes use the same core/halo treatment. No bloom, duplicate glow mesh, extra pass, texture, particle system or light is required. The fringe is local to the ribbon; it does not illuminate nearby scene objects.

## Spatial and runtime budgets

Radius remains 0.8 m, arc 180 degrees, lane spacing 9 cm, center playhead fixed, with past left and future right. Existing stable analytical selection zones do not follow detailed peaks. One-hand gain, two-hand region editing, empty-space two-hand zoom, palm presets, pause ownership, transport and four synchronized sources use their original paths.

At the saved 512 bins and four subdivisions: 2,048 longitudinal segments, **4,098 vertices / 4,096 triangles per lane**, or **16,392 vertices / 16,384 triangles total**, in four waveform draw calls. This increases tessellation from the prior rectangular display while retaining one mesh/material/pass per stem. Position, color, UV and envelope buffers are allocated once and reused. Cache payload remains approximately 3.63 MiB for all four stems. No per-frame audio sample read or cache generation occurs. Narrow transparent geometry introduces local overdraw; full Quest CPU/GPU and thermal performance still needs measurement.

## Validation

Run both complete `RemixTable.EditMode` and `RemixTable.PlayMode` suites. New tests cover loud/quiet/silent geometry, an isolated cached drum impulse, peak retention controls, rounded bounded interpolation and shared topology, all zoom presets, region-only gain changes, loop timestamps, radius/lane limits, frozen motion and zero managed allocation after warmup. Existing waveform-source comparisons and hand/audio/zoom tests remain.

**Tools > Remix Table > Validate Holographic Ribbons** runs scene/audio/seated/zoom/cache validation plus the preserved geometry and shader/settings checks. `RemixTable.Editor.RibbonVisualValidation.Preview` can be run through the Unity CLI with graphics enabled to render `Logs/RemixHologramPreview.png` without saving scene changes. Test/validation logs use the `Logs/RemixHologram*` prefix and remain ignored.

Physical Quest 3 acceptance remains required: flow/readability at seated distance and peripheral angles, onset timing and transient recognition, subtle glow against passthrough, silent-region acquisition, hand gesture comfort, UI occlusion, and CPU/GPU timing, allocations, thermals and audio underruns at target refresh rate. Desktop previews and synthetic-hand tests do not establish device performance.

Validation on 2026-11-13, Unity 6000.3.23f1: compilation and scene/audio/seated/zoom/cache/hologram validators passed. Edit Mode **74/74**, Play Mode **9/9** passed using Direct3D 11 for graphical checks. Warmed-up mesh updates measured zero managed allocation. The desktop preview was rendered and visually inspected; physical Quest testing has not been performed.
