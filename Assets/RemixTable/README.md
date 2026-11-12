# Remix Table: Sculpt Sound

Audio distribution: the milestone commit excludes the local NIVIROTest MP3 and four WAV stems because redistribution rights have not been verified. Their Unity metadata is retained. Before opening the scene on a fresh checkout, restore authorized copies at `Audio/NIVIROTest.mp3` and `Audio/NIVIROTest/Stems/{drums,bass,other,vocals}.wav` alongside the committed `.meta` files. Without them, the scene cannot play NIVIROTest and tests requiring that scene's music cannot pass. Synthetic audio/gesture tests generate their own clips. See AUDIO_IMPLEMENTATION.md for details.

Open `Scenes/RemixTable.unity` in Unity 6000.3.23f1. Enter Play Mode for procedural meshes. On Quest, enable passthrough with the inherited left-palm menu. The 0.74 x 0.40 m seated panel anchors once, 0.62 m ahead and 0.20 m below the initial eye pose; four rows at 9 cm spacing occupy a 0.348 m interactive span. A separate functional transport canvas sits immediately below. See SEATED_UX.md for configuration, bounds and device acceptance checks. This is a seated prototype, not automatic desk detection. Restart to reposition it.

## Gesture controls

Direct sculpting reads the existing XR Hands subsystem's left and right hands independently. Index/thumb tips determine pinch; wrist tracking is optional and used only for the existing visual twist effect. No rig, input map or hand tracker is added. Initial selection is near-hand; the inherited XRI pipeline is unchanged. Previous/Play-Pause/Next buttons replace the retired spatial song picker.

| Gesture | Result |
| --- | --- |
| Pinch within 3.2 cm of a waveform, inside its lane | Select a 0.6-second region and pause all stems and all scrolling |
| Drag the held pinch up/down | Increase/decrease audible gain and waveform amplitude for that region |
| Add the other hand's pinch | Enter region-boundary mode; both hands' ribbon-local X positions set the endpoints continuously |
| Move either hand horizontally while both pinch | Adjust that boundary; no gain/pitch writes, regardless of hand Y separation |
| Release either hand | Keep the region and rebase gain drag to the remaining hand's current position, without a jump |
| Hold a pinch still near an endpoint for 0.65 seconds | Select whole-stem gain editing; drag vertically to change it |
| Twist a held wrist before gain dragging | Existing visual effect preview; no audible effect DSP |
| Release the final hand or lose both hands | Retain edits and release this editing pause; resume unless another owner/manual pause remains |
| Use Previous / Next below the panel | Cycle NIVIROTest and silent placeholders |

Pinch hysteresis is 24 mm to enter, 35 mm to release, tracked separately per hand. States are Idle, OneHandGain and TwoHandRegion. Both hands are read on every runtime update; a second pinch is not rejected for having a different depth or vertical separation. Boundary mode preserves the pause and highlight. It orders local X endpoints before mapping to the fixed song timeline, so intervals can wrap across a loop. If the remaining hand moves vertically after the other hand releases, gain editing continues from the selected region's current gain. Tracking loss of one participating hand transfers to the other; cancellation/disable releases ownership and keeps stored edits.

Gain defaults: neutral 1x, range 0..2.5x, 12 cm ribbon-local Y movement per gain unit, sensitivity 1, 8 mm initiation threshold. Gain dragging back to its starting Y restores the baseline. The selected region factor multiplies whole-stem gain and the final product is capped at 2.5x. Stored pitch edits remain readable, but one-hand Y drag no longer writes pitch. See AUDIO_IMPLEMENTATION.md for smoothing, overlap rules and frame-based precision limits.

## Architecture

- `Automation.cs`: serializable song/stem metadata, plain automation records/store, and double-precision looping clock. No dependency on audio, input or geometry. `Upsert` updates by stable ID; latest overlapping matching edit wins. Pitch/effect defaults are zero, gain is one. Curves use seconds relative to region start, or song seconds for whole-stem curves. `ResetStem` and `ResetSong` are public APIs. The store round-trips through Unity JSON, but this milestone stores it only in session memory.
- `SongLibraryManager`: selection state and song-change events. NIVIROTest has four assigned audio stems; additional entries are silent placeholders with independent edits.
- `RemixTableManager`: shared playback, workspace anchoring, song reset and coordination. Every ribbon reads this same clock. A song switch releases selection and resets visual phase, retaining all song-specific edits.
- `RibbonInteractionManager`: near-hand sampling, mutually gated gestures and automation creation. Selection and automation hierarchy nodes are organizational markers; their behavior is owned here and in the independent store, avoiding duplicate update loops.
- `RibbonView`: reusable dynamic mesh buffers, deterministic scrolling geometry, timestamp sampling and automation previews. Every ribbon reads the shared DSP clock, which freezes during selection and resumes after the final editing owner releases. The fixed vertical playhead marks the center timestamp. Geometry reads stable song-relative automation on each loop.
- `RemixDesign.asset`: centralized palette, emission, size, spacing, interaction distance, deformation/gain limits, animation, resync and quality budgets. Set segment count before Play Mode. Orange drums, green bass, cyan melody, violet vocals.
- `RibbonEnergy.shader` / material: URP unlit additive vertex-color energy, stereo-instancing macros, no lighting/shadows, no new postprocessing. Thin halos and a capped ambient particle system provide restrained glow without bloom.
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

Default four ribbons total 1,032 vertices / 1,024 triangles, plus four thin halo lines and one playhead. Mesh arrays and indices are allocated once; no geometry arrays are created per frame. Ambient particles are disabled in the seated layout. No new bloom, render features, lights or packages are enabled. Automation evaluation scans edits newest-first, so very long editing sessions may eventually need indexed curves/baking. Existing URP costs (including inherited SSAO if active) remain unchanged.

NIVIROTest has synchronized four-stem playback with audible whole-stem and region gain, plus RMS audio analysis. Real pitch shifting, effect DSP, FFT, disk persistence and cloud accounts are not implemented. Pulses derive from procedural waveforms and song BPM. Effects use wrist world-Z rotation; headset orientation, occlusion and hand jitter need Quest tuning. Region selections may wrap across the song loop. There is no multi-user editing, undo UI, or automatic desk alignment. Seated lane bounds prevent extreme visual deformations from overlapping adjacent rows. Passthrough starts in the inherited off state and remains controlled by the existing palm menu. No Quest frame-rate, GPU, thermal or real hand-gesture performance is claimed.

Branch isolation: `faizan/remix-table-visuals` already existed clean at `9aeb2c4`, equal to local `main` and `origin/main`. Audio commit `9633b8d` is excluded; no Farhan changes were copied, merged or cherry-picked. Nothing is committed or pushed by this implementation.

Audio playback: NIVIROTest now plays four scheduled stems on scene start. See AUDIO_IMPLEMENTATION.md for architecture, tests and Quest verification limits. Other songs are silent placeholders.



