Paste everything below the line into Claude Code (Opus 5.5), run from the repo root, on branch
`farhan/audio-engine`, with Unity open on the project.

---

Read `CLAUDE.md` and `REMIX_TABLE_SPEC.md` fully first. Follow the ownership rules strictly. I have no
headset right now, so everything in this task must be testable on my PC in the Unity Editor with mouse
and keyboard.

## Goal: milestone 1, the audio engine and mix logic, playable on desktop

Build the core of Remix Table: synced stem playback, `MixState`, the gesture-to-sound mapping, the drop,
per-stem analysis, a generated test song, and a desktop sandbox scene where I can mix with the mouse and
hear it.

### Hard rules
- Only create or modify files under `Assets/RemixTable/Scripts/`, `Assets/RemixTable/Tests/`,
  `Assets/RemixTable/Dev/`, plus `INTEGRATION_M1.md` at the repo root.
- Do not touch the main scene, existing prefabs, `ProjectSettings/`, `Packages/`, or `Assets/Scripts/`.
  No new packages. Don't hand-write YAML or GUIDs; create assets and scenes with Editor scripts.
- No HandGestureInput or networking yet, but design for both (see spec §3, especially the golden rule).

### Step 1 — Assemblies
`RemixTable` runtime asmdef (references `Unity.XR.Interaction.Toolkit`, `Unity.XR.Hands`), and an
Editor-only `RemixTable.Tests` EditMode asmdef (references `RemixTable`, test runner assemblies,
`nunit.framework.dll`, constraint `UNITY_INCLUDE_TESTS`). A `RemixTable.Dev.Editor` editor asmdef for dev
tooling if useful. Make sure the existing `Assets/RobotArchitect/Editor` scripts still compile untouched.

### Step 2 — Pure logic first, with EditMode tests
1. `MixState`: as in spec §3. Tests: mute/solo rules (solo ducks the others; muting a soloed stem),
   version increments, events fire once per real change, snapshot round-trip, holder locking (a
   second holder can't take a held stem).
2. `MixMapping` + `MixTuning` ScriptableObject defaults: distance → gain (≤ 0.25 m full, ≥ 0.85 m
   quiet floor ~0.05, smooth curve), height above desk → reverb (0 at ≤ 3 cm, 1 at ≥ 30 cm), twist degrees
   → filter (±90° = ±1, dead zone ±8°), filter → low-pass cutoff (22 kHz → 350 Hz, exponential) and
   high-pass cutoff (10 Hz → 2.5 kHz, exponential). Tests for monotonicity, dead zones and clamping.
3. `MusicClock` math: beat/bar from dsp time, next downbeat, quantise. `ITimeSource` with a local
   implementation. Tests including the loop wrap-around.

### Step 3 — Runtime components
`SongDefinition`/`StemDefinition`/`StemRole`, `StemPlayer`, `IStemEffects` + `FilterComponentEffects`,
`StemAnalyzer`, `DropController`, `StemOrb`, `DeskFrame`, all per spec §3. Requirements:
- All stems start via `PlayScheduled` at one shared dsp time (at least 0.2 s ahead), loop, and stay
  sample-locked. Add a debug check that logs if any source's `timeSamples` drifts from stem 0 by more than
  256 samples (dev builds only).
- Gain changes are smoothed (≥ 20 ms) so nothing clicks. Mute and solo never stop sources.
- Spatial setup per spec: `spatialize = true`, `spatialBlend = 1`, built-in distance rolloff neutralised so
  `MixMapping` controls loudness. Add a Dev toggle to turn spatialization off, in case the Meta plugin
  misbehaves in the Editor.
- `StemAnalyzer`: preallocated buffers, RMS + 3 bands per stem, smoothed, no per-frame allocations.
- `DropController`: Build phase (all filters sweep towards low-pass over 2 bars, riser plays), Hit
  quantised to the next downbeat (restore everything with a short gain bump), Release. Triggered by
  `TriggerDrop()`; the desktop key and later the slam gesture both call it.
- Pause/resume handling per spec.

### Step 4 — Generated test song
An Editor menu item **Tools > Remix Table > Dev > Generate Test Song** that synthesises 16-bit WAV files
into `Assets/RemixTable/Dev/TestSong/`: 120 BPM, 4/4, 8 bars, all stems exactly the same sample count, mono,
48 kHz. Stems: Drums (kick on every beat, hat on off-beats, snare on 2 and 4), Bass (simple root-note
line), Keys (sustained chords), Lead (arpeggio), FX (filtered noise swells), plus a separate 2-bar Riser
clip. It must sound musical enough to judge a mix, and make any drift obvious. Then create a
`SongDefinition` asset for it (licence: "Generated test content"). Set the importer for these clips to
Decompress On Load and mono. Re-running overwrites only this folder.

### Step 5 — Desktop sandbox
Menu item **Tools > Remix Table > Dev > Build Desktop Sandbox** that creates and saves
`Assets/RemixTable/Dev/DesktopSandbox.unity` (not added to build settings). It contains a camera seated
at a desk (camera = listener), a light, a desk plane, `DeskFrame`, `StemPlayer` with the test song, one
simple coloured sphere per stem with `StemOrb`, `DropController`, and `DesktopDevInput` with these controls:
- Left-drag an orb: move it across the desk (distance → volume). Shift + drag: lift/lower (reverb).
- Hover + mouse wheel: twist (filter). Hover + M: mute. Hover + S: hold for solo.
- Space: drop. R: reset the layout. P: pause/resume.
- A small on-screen readout (IMGUI is fine) showing each stem's gain/filter/reverb/mute/solo, the
  current bar:beat, and the drift check status.
Orb size or brightness should pulse from `StemAnalyzer` so I can see that the analysis works.

### Step 6 — Verify
- Zero compile errors (ask me to check the Console, since Unity is open).
- Ask me to run the EditMode tests in Test Runner, and report the results I paste back.
- Tell me exactly what I should hear and see in the sandbox, step by step, as a checklist to confirm.

### Step 7 — Hand-off note
`INTEGRATION_M1.md` for Faizan: what each component is, which GameObject it goes on, every Inspector
field, the events to hook, the audio import settings for real songs, and **a headset test plan** for the
risky items: (1) Meta XR Audio + filter components together on Quest, (2) the stems stay in sync on
device, (3) loudness feels right with distance on device. Include what to switch to if (1) fails
(AudioMixer-based `IStemEffects`).

Finish with a summary: files created, test results, what's verified versus unverified, and anything you
need from me or Faizan.
