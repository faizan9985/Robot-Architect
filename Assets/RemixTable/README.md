# Remix Table: Sculpt Sound

NIVIROTest uses the existing tracked MP3 and four WAV stems at `Audio/NIVIROTest/Stems/{drums,bass,other,vocals}.wav`. Keep the original sources and their `.meta` files intact. Audio-derived peak caches are stored separately in `Waveforms/`; they do not replace playback audio. Synthetic audio/gesture test fixtures generate their own clips. See AUDIO_IMPLEMENTATION.md and AUDIO_WAVEFORMS.md for details.

Open `Scenes/RemixTable.unity` in Unity 6000.3.23f1. Enter Play Mode for procedural meshes. On Quest, enable passthrough with the inherited left-palm menu. Four curved ribbons surround the initial seated pose over 180 degrees at a radius of 0.8 m, with 9 cm lane spacing and a 0.348 m vertical band centered 0.20 m below the eyes. The central 90 degrees support direct editing; dimmed outer sections provide preview. The timeline shows four seconds of past and four seconds of future music around a stationary forward playhead. A separate transport canvas remains below the playhead. See [CURVED_TIMELINE.md](CURVED_TIMELINE.md) for configuration, mapping, test scope and hardware acceptance checks. Restart to reposition the stable world-space workspace. SEATED_UX.md records the earlier flat layout.

Timeline zoom: use the expandable **TIMELINE ZOOM** section in the existing palm menu, or pinch both hands in empty forward workspace and spread to show fewer seconds. Presets are 4, 8, 16 and 32 seconds; Normal (8s) is the default. See [TIMELINE_ZOOM.md](TIMELINE_ZOOM.md) for gesture arbitration, mapping and deferred changes during edits.

## Gesture controls

Waveforms now form smooth holographic ribbons driven by all four real stems, with peak-preserving envelope smoothing and rounded continuous geometry. See [HOLOGRAPHIC_RIBBONS.md](HOLOGRAPHIC_RIBBONS.md) for visual controls, gain mapping, budgets and validation, and [AUDIO_WAVEFORMS.md](AUDIO_WAVEFORMS.md) for cache regeneration. Silent placeholders remain flat.

Direct sculpting reads the existing XR Hands subsystem's left and right hands independently. Index/thumb tips determine pinch; wrist tracking is optional and used only for the existing visual twist effect. No rig, input map or hand tracker is added. Initial selection is near-hand; the inherited XRI pipeline is unchanged. Previous/Play-Pause/Next buttons replace the retired spatial song picker.

| Gesture | Result |
| --- | --- |
| Pinch within 3.2 cm of a waveform, inside its lane | Select a 0.6-second region and pause all stems and all scrolling |
| Drag the held pinch up/down | Increase/decrease audible gain and waveform amplitude for that region |
| Add the other hand's pinch | Enter region-boundary mode; both hands' angular positions set the endpoints continuously |
| Move either hand along the arc while both pinch | Adjust that boundary; no gain/pitch writes, regardless of hand Y separation |
| Release either hand | Keep the region and rebase gain drag to the remaining hand's current position, without a jump |
| Hold a pinch still near an interactive-zone endpoint for 0.65 seconds | Select whole-stem gain editing; drag vertically to change it |
| Twist a held wrist before gain dragging | Existing visual effect preview; no audible effect DSP |
| Release the final hand or lose both hands | Retain edits and release this editing pause; resume unless another owner/manual pause remains |
| Use Previous / Next below the playhead | Cycle NIVIROTest and silent placeholders |

Pinch hysteresis is 24 mm to enter, 35 mm to release, tracked separately per hand. States are Idle, OneHandGain and TwoHandRegion. Both hands are read on every runtime update; a second pinch is not rejected for having a different depth or vertical separation. Boundary mode preserves the pause and highlight. It orders angular endpoints before mapping to the fixed song timeline, so intervals can wrap across a loop. If the remaining hand moves vertically after the other hand releases, gain editing continues from the selected region's current gain. Tracking loss of one participating hand transfers to the other; cancellation/disable releases ownership and keeps stored edits.

Gain defaults: neutral 1x, range 0..2.5x, 12 cm ribbon-local Y movement per gain unit, sensitivity 1, 8 mm initiation threshold. Gain dragging back to its starting Y restores the baseline. The selected region factor multiplies whole-stem gain and the final product is capped at 2.5x. Stored pitch edits remain readable, but one-hand Y drag no longer writes pitch. See AUDIO_IMPLEMENTATION.md for smoothing, overlap rules and frame-based precision limits.

## Architecture

- `Automation.cs`: serializable song/stem metadata, plain automation records/store, and double-precision looping clock. No dependency on audio, input or geometry. `Upsert` updates by stable ID; latest overlapping matching edit wins. Pitch/effect defaults are zero, gain is one. Curves use seconds relative to region start, or song seconds for whole-stem curves. `ResetStem` and `ResetSong` are public APIs. The store round-trips through Unity JSON, but this milestone stores it only in session memory.
- `SongLibraryManager`: selection state and song-change events. NIVIROTest has four assigned audio stems; additional entries are silent placeholders with independent edits.
- `RemixTableManager`: shared playback, workspace anchoring, song reset and coordination. Every ribbon reads this same clock. A song switch releases selection and resets visual phase, retaining all song-specific edits.
- `RibbonInteractionManager`: near-hand sampling, mutually gated gestures and automation creation. Selection and automation hierarchy nodes are organizational markers; their behavior is owned here and in the independent store, avoiding duplicate update loops.
- `RibbonView`: reusable dynamic mesh buffers, deterministic scrolling geometry, timestamp sampling and automation previews. Every ribbon reads the shared DSP clock, which freezes during selection and resumes after the final editing owner releases. The fixed vertical playhead marks the center timestamp. Geometry reads stable song-relative automation on each loop.
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

NIVIROTest has synchronized four-stem playback with audible whole-stem and region gain and audio-derived signed peak envelopes. Real pitch shifting, effect DSP, FFT, disk persistence and cloud accounts are not implemented. Wrist effects retain color feedback without displacing the accurate envelope. Headset orientation, occlusion and hand jitter need Quest tuning. Region selections may wrap across the song loop. There is no multi-user editing, undo UI, or automatic desk alignment. A shared bounded display scale prevents peaks from overlapping adjacent rows. Passthrough starts in the inherited off state and remains controlled by the existing palm menu. No Quest frame-rate, GPU, thermal or real hand-gesture performance is claimed.

Branch isolation: `faizan/remix-table-visuals` already existed clean at `9aeb2c4`, equal to local `main` and `origin/main`. Audio commit `9633b8d` is excluded; no Farhan changes were copied, merged or cherry-picked. Nothing is committed or pushed by this implementation.

Audio playback: NIVIROTest now plays four scheduled stems on scene start. See AUDIO_IMPLEMENTATION.md for architecture, tests and Quest verification limits. Other songs are silent placeholders.



