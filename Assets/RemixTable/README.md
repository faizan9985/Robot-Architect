# Remix Table: Sculpt Sound

NIVIROTest uses the existing tracked MP3 and four WAV stems at `Audio/NIVIROTest/Stems/{drums,bass,other,vocals}.wav`. Keep the original sources and their `.meta` files intact. Audio-derived peak caches are stored separately in `Waveforms/`; they do not replace playback audio. Synthetic audio/gesture test fixtures generate their own clips. See AUDIO_IMPLEMENTATION.md and AUDIO_WAVEFORMS.md for details.

Open `Scenes/RemixTable.unity` in Unity 6000.3.23f1. Enter Play Mode for procedural meshes. On Quest, enable passthrough with the inherited left-palm menu. Four curved ribbons surround the initial seated pose over 180 degrees at a radius of 0.8 m, with 9 cm lane spacing and a 0.348 m vertical band centered 0.20 m below the eyes. The full 180 degrees support direct editing when a tracked pinch is close to the lane baseline; outer sections have the same base brightness. The timeline shows four seconds of past and four seconds of future music around a stationary forward playhead. A separate transport canvas remains below the playhead. See [CURVED_TIMELINE.md](CURVED_TIMELINE.md) for configuration, mapping, test scope and hardware acceptance checks. Restart to reposition the stable world-space workspace. SEATED_UX.md records the earlier flat layout.

Timeline zoom: use the expandable **TIMELINE ZOOM** section in the existing palm menu, or pinch both hands in empty forward workspace and spread to show fewer seconds. Presets are 4, 8, 16 and 32 seconds; Normal (8s) is the default. See [TIMELINE_ZOOM.md](TIMELINE_ZOOM.md) for gesture arbitration, mapping and deferred changes during edits.

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

## ROOM mode (your room, scanned)

Press **ROOM** (or turn on passthrough from the palm menu). The table reads the room the user captured in Meta **Space Setup**: walls, floor, desk and furniture.

- **Sits at your desk.** It turns to face across the nearest desk in front of you, within about 1.1 m, and lifts the button rows clear of the desk top. If that lift would push the top rows above eye level, it shrinks the table toward your head (down to 75%) instead.
- **Fits the room.** It turns up to 30° away from walls that would cut through the ribbon arc. If a wall is closer than the ribbons, it shrinks the arc, and below 70% it reports a tight space.
- **Stems on your walls.** With SPATIAL on, stems still at their default spots hang 14 cm off the real walls around you: vocals ahead, drums and bass to the sides, melody behind-left. Release an orb within 30 cm of a wall or table and it sticks there, marked by a mount ring. Stems you placed yourself are never moved.
- **The room plays along.**
  - A light sweep rolls out across the scanned surfaces when the room loads.
  - Every beat (from the song's detected tempo) sends a ripple from the desk across the floor and walls. Downbeats are stronger, and the ripple scales with drum level.
  - A wall holding a stem glows in that stem's color.
  - The desk's near edge becomes four LED level meters.
- **Rooms we never tested:**
  - No scene permission: it asks, explains, and keeps the floating table if denied.
  - No scan: it opens Space Setup once and re-reads the room when you return.
  - No desk: it floats in front of you as usual.
  - Standing desk while seated: the lift is capped and it says so.
  - A scan that updates later: it re-places, but never during an edit or orb grab.
  - Turning ROOM off glides the table back in front of you and takes ROOM-placed stems off the walls.

**How it works:**
- **`RemixRoomScanner`** (`Assets/Scripts/Room`) reads `ARPlaneManager` / `ARBoundingBoxManager` through Unity's Meta OpenXR provider, which shares the XR Origin's tracking space. It turns the scan into a plain `RoomModel`. MRUK isn't used because it requires `OVRCameraRig`, and this project keeps the XRI XR Origin rig.
- **`RoomPlacement`** contains the placement rules, as pure, unit-tested math.
- **`RemixRoomPlacer`** glides the table into place and hangs the stems.
- **`RemixRoomVisuals`** with **`RoomGlow.shader`** handles the light painted on real surfaces. It uses premultiplied output and stereo instancing, with one quad per surface.

**Setup:** run **Tools > Remix Table > Add ROOM Mode (Space Setup Placement)**. It's idempotent, and in batch mode it's `-executeMethod RemixTable.Editor.RemixRoomLayout.Apply`. It enables the OpenXR features *Meta Quest: Planes* and *Meta Quest: Bounding Boxes* for Android, which add the `USE_SCENE` permission. It adds the trackable managers, switched off, to the XR Origin, and adds the `RemixRoom` object.

**Previewing without a headset:** use `RemixRoomPlacer` > context menu **Preview ROOM Mode (sample desk room)**.

## Daily Drop, Vibes and Access (side panels)

Two panels sit on the ribbon arc at ±38°, below the ribbons and facing you.

**DAILY DROP (left):** one remix goal per day. It's checked automatically from what you do, and each one teaches a feature:

1. Karaoke (vocals out for 8 s of playback)
2. Surround (spread four spatial stems at least 1 m apart)
3. Breakdown (drums out for 2 bars, then back)
4. Underwater (filter)
5. Set the Mood (two vibes)
6. Spotlight (solo bass for a bar)
7. Key Change (pitch)
8. Stuck in a Loop (loop three times)
9. Make It Yours (ROOM + a stem on a wall)
10. It's a Wrap (record 10 s)

A new player always starts with Karaoke. Finishing a drop grows the day streak. NEXT DROP offers bonus drops, which count toward unlocking room-glow themes (SUNSET at 1 drop, NEON at 3, AURORA at 6) but not toward the streak. THEME cycles the unlocked themes. Progress is saved in `persistentDataPath/daily-drop.json`; the logic is in `DailyDropRules`, which is unit-tested.

**VIBES (right):** CHILL, HYPE, KARAOKE, BREAKDOWN and BACK TO MY MIX each reshape the whole mix in one tap, as one undoable step. They work by voice too, through MIC: "make it chill", "pump it up", "karaoke", "a cappella", "break it down", "back to normal". Vibes replace each other rather than stacking, and NORMAL restores your own edits.

**ACCESS (right):**
- **HI CONTRAST:** black panels, white outlined buttons, bold white text, and a brighter room glow.
- **CLOSER:** brings the whole table toward you, which shortens the reach and raises the rows. It also works in ROOM mode.

Both settings are remembered. Every hand action also has a voice route, and one-hand alternatives exist: SNAP plus a one-hand pinch makes a beat region, and vibes are single taps.

NEXT and PREVIOUS skip the silent placeholder songs (`SongLibraryManager.skipSilentSongs`).

**Setup:** **Tools > Remix Table > Add Daily Drop, Vibes and Access Panels**, or `-executeMethod RemixTable.Editor.RemixSidePanelsLayout.Apply`. It also re-detects each song's tempo and first beat.

## Quick Start, 3D audio, gaze, FoV and occlusion

- **QUICK START** (`RemixOnboarding`): one line at a time on a card above the tools, plus a ghost pinch (thumb and index tips) that shows the gesture on the vocals ribbon. Each step completes when you do it:
  1. Pull the vocals down (instant karaoke).
  2. Bring them back, or press UNDO.
  3. Tap a vibe (the VIBES panel pulses).
  4. Press ROOM (the button pulses). This step only appears on a headset with passthrough.

  It runs once, and SKIP ends it. Saying "tutorial" or "help" replays it.
- **Real 3D audio** (`RemixAudioManager.use3DAudio` and `Assets/Scripts/Audio/RemixMetaAudio`): with SPATIAL on, each stem's AudioSource sits on its orb and Meta XR Audio renders it, using HRTF, early reflections and reverb. The project's spatializer plugin is Meta XR Audio. The shoebox reverb is sized from the real Space Setup scan in ROOM mode; otherwise a calm default room is used. With SPATIAL off the stems stay plain stereo.
- **Gaze** (`RemixGazeFocus`): the ribbon you look at brightens and is named. Look at a ribbon and hold a pinch in the air (away from the ribbons, one hand, at least 0.35 s) to hear that stem alone. Releasing restores the mix, and nothing in the remix changes. Quick pinches (UI taps), two-hand zoom, ribbon edits and orb grabs never trigger it.
- **FoV-aware layout** (`RemixFovAdapter`): it measures the headset's binocular horizontal FoV from the eye projections. If the side panels would pass the comfortable cone (FoV/2 − 8°), as on Quest 3S, they slide inward and shrink to no less than 72%, never over the centre rows. The ribbons and core rows are central on every device.
- **Pause / resume**: the song pauses when the app is suspended and when the Quest system menu takes focus, and resumes where it was.
- **Occlusion in ROOM mode**: `AROcclusionManager` and `ARShaderOcclusion` on the camera, with the *Meta Quest: Occlusion* OpenXR feature, run only while ROOM is on. `RibbonEnergy.shader` reads the environment depth (`RemixOcclusion.hlsl`, soft 4 cm edge), so your real hands and furniture hide the ribbons. The room-glow surfaces are not occluded, because they lie on the real surfaces.

**Setup:** **Tools > Remix Table > Add Quick Start, 3D Audio, Gaze, FoV and Occlusion**, or `-executeMethod RemixTable.Editor.RemixFinalLayout.Apply`.

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

NIVIROTest has synchronized four-stem playback with audible whole-stem and region gain and audio-derived signed peak envelopes. Per-stem low-pass/high-pass filter DSP and Volume/Filter plus EDIT/LIVE controls are implemented. Real pitch shifting, FFT, disk persistence and cloud accounts are not implemented. Filter automation changes ribbon color without replacing the audio-derived envelope with synthetic waves. Headset orientation, occlusion and hand jitter need Quest tuning. Region selections may wrap across the song loop. There is no multi-user editing. Desk alignment comes from ROOM mode (above) and needs a Space Setup scan. A shared bounded display scale prevents peaks from overlapping adjacent rows. Passthrough starts in the inherited off state and remains controlled by the existing palm menu. No Quest frame-rate, GPU, thermal or real hand-gesture performance is claimed.

Historical branch-isolation note (superseded by the main/UIUX integration): `faizan/remix-table-visuals` already existed clean at `9aeb2c4`, equal to local `main` and `origin/main`. Audio commit `9633b8d` is excluded; no Farhan changes were copied, merged or cherry-picked. Nothing is committed or pushed by this implementation.

Audio playback: NIVIROTest now plays four scheduled stems on scene start. See AUDIO_IMPLEMENTATION.md for architecture, tests and Quest verification limits. Other songs are silent placeholders.



