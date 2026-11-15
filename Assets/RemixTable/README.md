# Remix Table: Sculpt Sound

NIVIROTest uses the existing tracked MP3 and four WAV stems at `Audio/NIVIROTest/Stems/{drums,bass,other,vocals}.wav`. Keep the original sources and their `.meta` files intact. Audio-derived peak caches are stored separately in `Waveforms/`; they do not replace playback audio. Synthetic audio/gesture test fixtures generate their own clips. See AUDIO_IMPLEMENTATION.md and AUDIO_WAVEFORMS.md for details.

Open `Scenes/RemixTable.unity` in Unity 6000.3.23f1. Enter Play Mode for procedural meshes. On Quest, enable passthrough with the inherited left-palm menu. Four curved ribbons surround the initial seated pose over 180 degrees at a radius of 0.8 m, with 9 cm lane spacing and a 0.348 m vertical band centered 0.20 m below the eyes. The full 180 degrees support direct editing when a tracked pinch is close to the lane baseline; outer sections have the same base brightness. The timeline shows four seconds of past and four seconds of future music around a stationary forward playhead. A separate transport canvas remains below the playhead. See [CURVED_TIMELINE.md](CURVED_TIMELINE.md) for configuration, mapping, test scope and hardware acceptance checks. Restart to reposition the stable world-space workspace. SEATED_UX.md records the earlier flat layout.

The centered top navigation bar is **Undo | Redo | Speed 1× | Zoom 8s**. All header and preset buttons retain 5 × 4 cm targets with 6 mm spacing. Speed reveals eight presets in two rows of four; Zoom reveals 4s, 8s, 16s and 32s in one row. Each menu expands inline to its owner's right on the existing HistoryCanvas. The palm menu retains passthrough, volume, close controls and permanently visible gesture guidance; its old zoom header and preset container are removed.

`RemixNavigationToolbar` is the sole layout/animation owner. Opening uses a 280 ms cubic ease-out; closing uses a 250 ms smoothstep ease-in-out, both on unscaled time. A RectMask2D viewport grows from zero to 230 units while full-size contents remain unscaled. Opacity and the reserved horizontal slot use the same rendered reveal fraction. Speed's occupied width continuously pushes Zoom to the right. Interruptions restart from the current rendered fraction; selecting the other menu closes the old target and opens the new one. Only fully revealed, currently selected menu options accept XR input. The bar never changes Y. Its collapsed padded width is 23 cm; either fully expanded state is 46.6 cm. The group shifts horizontally only when rendered bounds would exceed the configured seated panel width. Undo/Redo keep their original objects, styles, dimensions, icons and bindings, reparented into the animated content group.

`TimelineZoomUI` remains the adapter on the existing transport and writes exclusively through `manager.zoom.Request`. The toolbar label displays actual `VisibleSeconds` to tenths, including intermediate gesture values; a preset highlights only within 0.025 song-seconds of its current value. Requests made during an edit remain queued under the existing interpolation rules. Speed and Zoom are independent: at 2× playback, an 8s timeline still spans eight seconds of song time. Existing empty-space two-hand zoom remains available when toolbar menus and the palm menu are closed. Opening either toolbar menu cancels a pending/active zoom session with the existing tracked-release latch.

Playback rate still uses the existing eight presets, default 1×, shared DSP song clock and four synchronized AudioSource pitches. Song-relative gain/filter automation, LIVE/paused editing and Undo/Redo are preserved. Rates survive song changes; opening, closing or disabling UI never resets the rate. Pitch follows speed; pitch-preserving time stretching is not included.

Use **Tools / Remix Table / Apply Navigation Toolbar** to rebuild these controls and remove obsolete palm zoom UI in the current scene. Mode-layout regeneration and Apply Toolbar Timeline Zoom use the same builder. No extra canvas, EventSystem, XR rig, package or audio-clock implementation is introduced. See [TIMELINE_ZOOM.md](TIMELINE_ZOOM.md) for mapping and gesture details.

Toolbar validation (Unity 6000.3.23f1, 2026-11-15): compilation and scene/XR/audio/layout/mode/navigation validation passed. Edit Mode **143/143** and Play Mode **13/13** passed. Tests cover collapsed order, inline direction, fixed targets, exact easing/durations, width/position continuity, rapid exclusive switching, invisible-input blocking, preset bindings, gesture-driven labels, song-time zoom at 2×, fixed playhead, palm cleanup, editing, synchronized playback and Undo/Redo. Reports: Logs/RemixToolbarSceneValidation.log, Logs/RemixToolbarEditMode.xml and Logs/RemixToolbarPlayMode.xml. Quest 3 hardware verification remains required for text readability, ray/pinch targeting while controls move, reach during crossover, transition comfort, passthrough and audio perception. No hardware validation is claimed.

## Gesture controls

Waveforms now form smooth holographic ribbons driven by all four real stems, with peak-preserving envelope smoothing and rounded continuous geometry. See [HOLOGRAPHIC_RIBBONS.md](HOLOGRAPHIC_RIBBONS.md) for visual controls, gain mapping, budgets and validation, and [AUDIO_WAVEFORMS.md](AUDIO_WAVEFORMS.md) for cache regeneration. Silent placeholders remain flat.

Direct sculpting reads the existing XR Hands subsystem's left and right hands independently. Index/thumb tips determine pinch; optional wrist positions provide a steadier vertical drag reference, with pinch position as fallback. No rig, input map or hand tracker is added. Initial selection is near-hand; the inherited XRI pipeline is unchanged. Previous/Play-Pause/Next buttons replace the retired spatial song picker.

| Gesture | Result |
| --- | --- |
| Pinch within 3.2 cm of a waveform, inside its lane | Select a 0.6-second region; EDIT pauses all stems, LIVE keeps playback and scrolling running |
| Drag the held pinch up/down | VOLUME changes gain and waveform amplitude; FILTER sweeps from low-pass (down) through neutral to high-pass (up) |
| Add the other hand's pinch | Enter region-boundary mode; both hands' angular positions set the endpoints continuously |
| Move either hand along the arc while both pinch | Adjust that boundary; no gain/pitch writes, regardless of hand Y separation |
| Release either hand | Keep the region and rebase gain drag to the remaining hand's current position, without a jump |
| Hold a pinch still near an interactive-zone endpoint for 0.65 seconds | Select whole-stem gain editing; drag vertically to change it |
| Choose EDIT: PAUSE / LIVE: PLAY | Switch playback behavior; manual pause still takes precedence |
| Release the final hand or lose both hands | Retain edits and release this editing pause; resume unless another owner/manual pause remains |
| Use Previous / Next below the playhead | Cycle NIVIROTest and silent placeholders |

Pinch hysteresis is 24 mm to enter, 35 mm to release, tracked separately per hand. States are Idle, OneHandGain (Volume or Filter), TwoHandRegion, ZoomPending and Zooming. Ribbon acquisition has priority over empty-space zoom; a zoom session cannot acquire a ribbon until both hands release. Both hands are read on every runtime update; a second pinch is not rejected for having a different depth or vertical separation. Boundary mode preserves editing ownership and highlight; EDIT pauses playback while LIVE continues. Authored gain and filter regions resize with both hands. It orders angular endpoints before mapping to the fixed song timeline, so intervals can wrap across a loop. If the remaining hand moves vertically after the other hand releases, gain editing continues from the selected region's current gain. Tracking loss of one participating hand transfers to the other; cancellation/disable releases ownership and keeps stored edits.

Gain defaults: neutral 1x, range 0..2.5x, 12 cm ribbon-local Y movement per gain unit, sensitivity 1, 8 mm initiation threshold. Gain dragging back to its starting Y restores the baseline. The selected region factor multiplies whole-stem gain and the final product is capped at 2.5x. Stored pitch edits remain readable, but one-hand Y drag no longer writes pitch. See AUDIO_IMPLEMENTATION.md for smoothing, overlap rules and frame-based precision limits.

## Architecture

- `Automation.cs`: serializable song/stem metadata, plain automation records/store, and double-precision looping clock. No dependency on audio, input or geometry. `Upsert` updates by stable ID; latest overlapping matching edit wins. Pitch/effect defaults are zero, gain is one. Curves use seconds relative to region start, or song seconds for whole-stem curves. `ResetStem` and `ResetSong` are public APIs. The store round-trips through Unity JSON, but this milestone stores it only in session memory.
- `SongLibraryManager`: selection state and song-change events. NIVIROTest has four assigned audio stems; additional entries are silent placeholders with independent edits.
- `RemixTableManager`: shared playback, workspace anchoring, song reset and coordination. Every ribbon reads this same clock. A song switch releases selection and resets visual phase, retaining all song-specific edits.
- `RibbonInteractionManager`: near-hand sampling, mutually gated gestures and automation creation. Selection and automation hierarchy nodes are organizational markers; their behavior is owned here and in the independent store, avoiding duplicate update loops.
- `RibbonView`: reusable dynamic mesh buffers, deterministic scrolling geometry, timestamp sampling and automation previews. Every ribbon reads the shared DSP clock: EDIT freezes it during selection; LIVE keeps it advancing. Timeline zoom is deferred during either kind of edit. The fixed vertical playhead marks the center timestamp. Geometry reads stable song-relative automation on each loop.
- `RemixDesign.asset`: centralized palette, emission, size, spacing, interaction distance, gain limits and waveform quality budgets. Set waveform column count before Play Mode. Orange drums, green bass, cyan other/melody, violet vocals.
- `WaveformCache` / `WaveformBaker` / `WaveformImporter`: compact signed stereo peak pyramids, streaming editor WAV analysis and reproducible imported caches assigned through stem metadata.
- `RibbonEnvelope`: song-time envelope smoothing with a configurable peak floor and bounded cubic interpolation; no oscillator or temporal lag.
- `RibbonEnergy.shader` / material: URP unlit premultiplied translucent body, neon edge strands and a narrow feathered glow fringe in one pass; stereo-instancing macros and no added postprocessing.
- `SpatialSongPicker`: small spatial title with inherited near/far XRI pinch selection; no extra EventSystem.

The saved scene was duplicated with Unity `EditorSceneManager.SaveScene(..., saveAsCopy: true)` and reopened before removing the `RobotArchitect` gameplay root. The original hands-rig prefab, interaction manager, hand menu, camera, AR session, input and XR UI registration remain serialized as inherited. Original scene, packages, Android/OpenXR configuration, global build scenes and build profile are unchanged. Select the Remix scene explicitly when making a future build; the inherited build list still targets Robot Architect.

## Validation

Use **Tools > Remix Table > Validate Saved Scene** for non-mutating serialized validation. **Generate Scene** regenerates the Remix scene and will replace its authored contents; preserve custom scene edits first. Unity's save prompt protects dirty open scenes in interactive use.

Run Unity Test Runner's `RemixTable.EditMode` and `RemixTable.PlayMode` assemblies. The tests cover region boundaries/loop replay, whole-stem behavior, update IDs, curves, overlap precedence, resets, song isolation, JSON readiness, clock pause, shared editing pause ownership, manual pause precedence and saved-scene runtime startup. They are synthetic checks, not physical gesture or Quest hardware tests.

Batch examples (PowerShell; logs/results go under ignored `Logs/`):

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
& $unity -batchmode -nographics -projectPath 'X:\Capstone\Robot Architect' -executeMethod RemixTable.Editor.RemixSceneBuilder.Validate -quit -logFile Logs/RemixValidate.log
& $unity -batchmode -nographics -projectPath 'X:\Capstone\Robot Architect' -runTests -testPlatform EditMode -assemblyNames RemixTable.EditMode -testResults Logs/RemixEditMode.xml -logFile Logs/RemixEditMode.log
& $unity -batchmode -nographics -projectPath 'X:\Capstone\Robot Architect' -runTests -testPlatform PlayMode -assemblyNames RemixTable.PlayMode -testResults Logs/RemixPlayMode.xml -logFile Logs/RemixPlayMode.log
```

## Performance and limitations

Default four ribbons total 16,392 vertices / 16,384 triangles and four transparent waveform draws, plus the existing playhead. Mesh arrays and indices are allocated once; no geometry arrays are created per frame. Four unchanged peak caches occupy approximately 3.63 MiB. Ambient particles and old halo objects remain disabled in the seated layout; the shader supplies its own narrow fringe. No new bloom, render features, lights or packages are enabled. Automation evaluation scans edits, so very long editing sessions may eventually need indexed curves/baking. Existing URP costs (including inherited SSAO if active) remain unchanged.

NIVIROTest has synchronized four-stem playback with audible whole-stem and region gain and audio-derived signed peak envelopes. Per-stem low-pass/high-pass filter DSP and Volume/Filter plus EDIT/LIVE controls are implemented. Real pitch shifting, FFT, disk persistence and cloud accounts are not implemented. Filter automation changes ribbon color without replacing the audio-derived envelope with synthetic waves. Headset orientation, occlusion and hand jitter need Quest tuning. Region selections may wrap across the song loop. There is no multi-user editing, undo UI, or automatic desk alignment. A shared bounded display scale prevents peaks from overlapping adjacent rows. Passthrough starts in the inherited off state and remains controlled by the existing palm menu. No Quest frame-rate, GPU, thermal or real hand-gesture performance is claimed.

Historical branch-isolation note (superseded by the main/UIUX integration): `faizan/remix-table-visuals` already existed clean at `9aeb2c4`, equal to local `main` and `origin/main`. Audio commit `9633b8d` is excluded; no Farhan changes were copied, merged or cherry-picked. Nothing is committed or pushed by this implementation.

Audio playback: NIVIROTest now plays four scheduled stems on scene start. See AUDIO_IMPLEMENTATION.md for architecture, tests and Quest verification limits. Other songs are silent placeholders.



