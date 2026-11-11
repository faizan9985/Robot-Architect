# Remix Table · Milestone 1 integration (audio engine + mix logic)

For: Faizan · From: Farhan · Branch: `farhan/audio-engine`

This milestone is the sound side of the single-player MVP. It covers synced stem playback, the mix state,
the gesture → sound mapping, the drop, per-stem analysis for your shaders, a generated test song and a
desktop sandbox. Hand input (`HandGestureInput`) and networking are not in this milestone.

**Status: Editor only.** Nothing here has run on a Quest. The three risky items are covered in the headset
test plan at the end. Please run it on the first device build.

---

## 1. The one rule

**`MixState` is the single source of truth.** Every input (desktop keys, hands later, the network later) changes
the mix only through `MixState`. The audio engine and the visuals only read it. So if you need to change
something about the sound from a script or an event, call a `StemOrb` / `DropController` method. Don't
touch the AudioSources.

## 2. What goes where in the main scene

| GameObject | Components | Notes |
|---|---|---|
| **RemixTable Engine** (new, anywhere) | `StemPlayer`, `MusicClockDriver`, `StemAnalyzer`, `DropController` | At runtime the StemPlayer creates one child per stem ("Stem 0 Drums", …) holding the AudioSource and filters. Don't add AudioSources yourself. |
| **DeskFrame** (new) | `DeskFrame` | Place it **on the desk surface**, centred on the play area, unscaled. Forward points away from the player. Listener = the XR rig's Main Camera. |
| **Each orb prefab** | `StemOrb`, a collider, plus what `TestCube.prefab` has (Rigidbody + `XRGrabInteractable`) | The stem's sound follows the orb automatically. |
| Record prefab | nothing from me yet | The pull-apart gesture comes with `HandGestureInput`. |

Script execution order is fixed in code (`[DefaultExecutionOrder]`), so nothing needs setting in Project Settings.

## 3. Components and every Inspector field

### StemPlayer (`Remix Table/Stem Player`)
Plays the song. Every stem is scheduled with `PlayScheduled` at one shared dsp time, loops sample-locked, and is
smoothed (no clicks). It owns the `MixState` (`player.Mix`) and the `MusicClock` (`player.Clock`).

| Field | Default | What it does |
|---|---|---|
| Song | – | The `SongDefinition` to load on Awake. |
| Tuning | – | A `MixTuning` asset. Empty = built-in defaults. |
| Play On Start | on | Starts playback in `Start`. |
| Start Delay Seconds | 0.3 | How far ahead stems are scheduled (min 0.2 s). |
| Spatialize | on | Uses the project spatializer (Meta XR Audio). Dev toggle (`T` in the sandbox). |
| Spatialize Post Effects | **off** | Off = spatialize first, then filters/reverb (Unity default). On = filters first, then spatialize. See test 1. |
| Source Max Distance | 20 | Range only. The built-in rolloff is replaced by a flat curve, so loudness comes from `MixMapping`. |
| Effects Backend | Filter Components | Only option for now (see the fallback in test 1). |
| Pause On Focus Loss | on | Pauses when the app loses focus (Quest system menu). |
| Handle Focus In Editor | off | The Editor loses focus every time you click another window, so this is ignored there. |
| Drift Tolerance Samples | 256 | Dev builds: warn if any stem is further than this from stem 0. |
| Drift Check Interval Seconds | 0.5 | Dev builds: how often to check. |

Per stem, the AudioSource gets `spatialize = true`, `spatialBlend = 1`, custom flat rolloff, `dopplerLevel = 0`
(so moving an orb never bends the pitch), `spread = 0`, `priority = 0`.
Pause and resume: `OnApplicationPause` and focus loss pause immediately. Resume re-schedules every stem at the
same sample and dsp time, so they come back locked together, with a 50 ms fade-in.

### MusicClockDriver (`Remix Table/Music Clock Driver`)
| Field | What it does |
|---|---|
| Player | The StemPlayer (found on the same object if empty). |
| **OnBeat(int)** | Every beat. Argument = beat in bar, 0-based (0 = downbeat). |
| **OnBar(int)** | Every bar. Argument = bar in loop, 0-based. |

These fire on the first frame after the beat, so they can be up to about 20 ms late. That's fine for VFX. For anything
audio, ask me and I'll schedule it with PlayScheduled.

### StemAnalyzer (`Remix Table/Stem Analyzer`): getters for your shaders
Per stem, per frame, no allocations. Reads the clip at the play position (pre-fader).

| Field | Default | |
|---|---|---|
| Player | – | Same object if empty. |
| Window Samples | 1024 | Analysis window. |
| Low Band Hz / High Band Hz | 250 / 4000 | Band split: low < 250, mid 250–4 k, high > 4 k. |
| Attack / Release Seconds | 0.02 / 0.18 | Smoothing. |
| Level Scale | 3 | Maps RMS to the 0..1 getters. |

Getters (all return smoothed values, and 0 for a bad index):
`GetLevel01(stem)`, `GetLow01(stem)`, `GetMid01(stem)`, `GetHigh01(stem)`, **`GetPostFaderLevel01(stem)`**
(includes mute, solo, ducking and distance, which is the one that best matches what you hear), `GetRms(stem)`, `GetBands(stem)`.
`DevOrbVisual` (in `Assets/RemixTable/Dev/Scripts`) shows one way to use them, with a MaterialPropertyBlock.
**Tell me your shader property names and I'll write the production glue component** so you don't need C#.

### DropController (`Remix Table/Drop Controller`)
Idle → **Build** (every filter sweeps towards low-pass, riser plays) → **Hit** (on the first downbeat at least 2 bars
after the trigger; filters snap open with a +25% gain bump) → **Release** (the bump fades over 3 beats) → Idle.
Trigger: `TriggerDrop()` (desktop: Space; later the desk slam). It ignores triggers while a drop is running.

| Field | Default | |
|---|---|---|
| Player | – | Same object if empty. |
| Riser Clip | – | Should be 2 bars long; it ends exactly on the hit. Placeholder: `Assets/RemixTable/Dev/TestSong/Riser.wav`. |
| Riser Volume | 0.6 | |
| Hit Clip | – | Optional one-shot starting exactly on the hit. Placeholder: `.../TestSong/Hit.wav`. |
| Hit Volume | 0.8 | |
| **OnBuildStarted / OnHit / OnReleased** | | One event per phase entry. `OnHit` = shockwave VFX. |

`BuildProgress01` (0..1 during the build) is there if you want the VFX to ramp up. Pausing cancels a drop.
Both clips play non-spatial (2D).

### StemOrb (`Remix Table/Stem Orb`)
Turns the orb's position into gain and reverb every frame, unless someone else is holding it.

| Field | Default | |
|---|---|---|
| Player / Desk | – | Found automatically if empty (once, at Start). Better to assign them. |
| Stem Index | 0 | Which stem of the song (0-based, in the SongDefinition's order). |
| Rest Height | 0.04 | Distance from the pivot to the orb's **bottom** (radius for a sphere). Reverb starts 3 cm above the desk, measured from the bottom. |
| Map Position | on | Leave on. |
| Local Holder Id | 1 | Leave as is (networking will use other ids). |
| **OnMuted(bool) / OnSoloed(bool) / OnHeld(bool)** | | They fire for any change, whoever made it. |

Methods for gestures/UI: `BeginHold()`, `EndHold()`, `ToggleMute()`, `SetMuted(bool)`, `SetSolo(bool)`,
`SetTwist(degrees)`, `AddTwist(degrees)`. There are also void versions for Inspector wiring:
**`BeginLocalHold()` / `EndLocalHold()`**. Until hand input lands, wire the orb's `XRGrabInteractable`
*Select Entered* → `StemOrb.BeginLocalHold` and *Select Exited* → `StemOrb.EndLocalHold`.
Moving an orb by grabbing it already changes its volume on device. Twist, mute and solo need `HandGestureInput`.

Design note for the orb prefab: if orbs have gravity, a released orb falls back to the desk and its reverb goes back to 0.
That's possibly fine ("lift to add reverb while you hold it"), but say if you'd rather the orbs float where they're left.

### DeskFrame (`Remix Table/Desk Frame`)
| Field | Default | |
|---|---|---|
| Listener | Camera.main | The head (the XR camera with the AudioListener). |
| Desk Size | 1.2 × 0.7 m | Usable area (local X × Z). Desktop dragging is clamped to it; a gizmo shows it. |

`SetDeskHeight(y)` is the hook for palm-tap calibration. Volume uses the distance **along the desk plane** from
head to orb, so lifting an orb changes only its reverb.

### MixTuning (Create > Remix Table > Mix Tuning)
Every curve constant lives here. Defaults: full gain ≤ 0.25 m, quiet floor 0.05 at ≥ 0.85 m (smooth, dB-linear) ·
reverb 0 at 3 cm, 1 at 30 cm · twist dead zone ±8°, full at ±90° · low-pass 22 kHz → 350 Hz, high-pass
10 Hz → 2.5 kHz (exponential) · solo ducks others to 0.15 · gain ramps 30 ms (never below 20 ms) · master
headroom 0.8 · drop 2 bars, bump +25%. Create one asset for the main scene and assign it to the StemPlayer.
`Assets/RemixTable/Dev/DevMixTuning.asset` is the sandbox's copy.

### SongDefinition (Create > Remix Table > Song Definition)
Title, Artist, Licence (CC0 / CC BY only), Attribution URL, BPM, Beats Per Bar, Loop Length Bars, and the
Stems list (Name, Role, Clip, Color). On load it checks the audio rules and logs `[RemixTable]` warnings. The beat
clock follows the real clip length, so a slightly wrong BPM can't make the beats drift.

## 4. Audio import settings for real songs

For every stem clip (select them all in the Project window and set them at once):

| Setting | Value | Why |
|---|---|---|
| **Force To Mono** | on | The spatializer needs mono sources. |
| **Load Type** | **Decompress On Load** | Sample-exact sync, and StemAnalyzer can read the samples. |
| Compression Format | Vorbis, quality ~70 | Per the spec. |
| Sample Rate Setting | Preserve (48 kHz sources preferred) | All stems of a song must share the rate. |
| Load In Background | off | Everything is ready before we schedule. |
| Preload Audio Data | on | |
| Android override | none, or the same as above | An override with another load type breaks sync on Quest only. |

Exporting stems from the DAW: start them all at the same point, make them **exactly the same length**, use a whole
number of bars, and don't trim silence. Then fill BPM / Beats Per Bar / Loop Length Bars to match. The Console
warns if the lengths differ by even one sample.
The generated test song (`Assets/RemixTable/Dev/TestSong`) is PCM rather than Vorbis on purpose, so it's an exact
reference. Real songs should still be Vorbis (sync test 2 below covers that).

## 5. Dev tools (don't ship)

- **Tools > Remix Table > Dev > Generate Test Song** writes 5 stems + Riser + Hit and a `TestSong` SongDefinition
  into `Assets/RemixTable/Dev/TestSong/`.
- **Tools > Remix Table > Dev > Build Desktop Sandbox** writes `Assets/RemixTable/Dev/DesktopSandbox.unity`
  (not in build settings). It never touches the scene you have open.
- **`AudioSelfTest`** (`Remix Table/Dev/Audio Self Test`): a hands-free run through every effect, logged step by
  step. Use it for the headset tests below and remove it afterwards.
- `DesktopDevInput`, `DevOrbVisual`: sandbox only. `DesktopDevInput` disables itself in release builds.

## 6. Headset test plan (first device build)

Setup for all three tests: a **Development Build** of the main scene with the engine, DeskFrame and 5 orbs wired up
as above, using the **test song** first and then one real song. Add `AudioSelfTest` to the engine object for the
build. Watch the log from a PC during the test:
`adb logcat -s Unity | findstr RemixTable` (Windows) or `adb logcat -s Unity | grep RemixTable`.
Use headphones and the Quest speakers, one after the other. Write down the results for each numbered step.

### Test 1: Meta XR Audio + filter components together (highest risk)
Unity's filter components (low-pass, high-pass, reverb) sit in the same AudioSource chain as the Meta spatializer.
They might not run, might run in the wrong order, or might cost too much CPU.
1. Launch. Within 4 s the log says *"Self-test: starting…"*. All 5 stems play.
2. **Direction:** move your head / walk round the desk. Each stem stays fixed in space at its orb. *(spatializer works)*
3. **Low-pass bars:** log *"low-pass sweep"*. Everything should get muffled over 2 bars, like hearing it underwater.
4. **High-pass bars:** everything thins out and the bass disappears.
5. **Reverb bars:** a tail builds up.
6. **Drop:** riser for 2 bars while the filters close, then the hit lands on a downbeat and the sound opens back up.
7. **Mute walk:** one stem drops out per bar, with a soft fade and no clicks.
8. **Solo:** stem 0 stays loud while the others duck. Then *"finished"*.
9. OVR Metrics Tool: stays at **72 FPS**, with no audio crackle during the reverb bars.

If 3–5 do nothing, sound wrong or crackle:
- a) Toggle **Spatialize Post Effects** on the StemPlayer, rebuild and repeat. Note which setting works.
- b) If neither works: we switch to the **AudioMixer backend**. I'd write `MixerStemEffects : IStemEffects`, which drives
  exposed parameters on one AudioMixer group per stem (Lowpass, Highpass, a send to a shared reverb return),
  and set `outputAudioMixerGroup` on each stem's source. You'd need to make the mixer asset (one group per stem
  plus a Reverb return, with exposed params named the way I'll list), because assets are yours. Only `IStemEffects`
  changes; nothing else does. A shared reverb also costs less CPU than 5 reverb filters, so if step 9 is tight we'd
  switch anyway.
- c) Spatializer silent or broken: set **Spatialize** off (Unity's built-in 3D panning). That tells us whether the problem is
  the plugin or the filters.

### Test 2: stems stay in sync on device
1. During the self-test, listen to the drums against the lead arpeggio. Every hit should land as one sound. A "flam"
   (double hit) means drift.
2. The log must **not** show *"Stems drifted"* or *"stopped playing"* (a dev-build check every 0.5 s, 256-sample
   tolerance).
3. Let it loop for **5 minutes**, then listen again. It should be just as tight.
4. **Pause/resume:** press the Quest button (system menu) and come back, then take the headset off for 10 s and put it back on.
   Audio stops, comes back with a short fade, and is still in sync with no drift warning. If you see *"Music clock was … ms off
   the audio; re-anchored."*, write it down (it's harmless, but I want to know).
5. Repeat 1–3 with a **real Vorbis song** (Decompress On Load).

### Test 3: loudness with distance feels right
After the self-test finishes, the orbs control the mix again.
1. Grab one orb (XRGrabInteractable) and slide it from right next to you (≤ 25 cm from your head, horizontally) to the far
   edge of the desk (~85 cm). It should fade smoothly from full to quiet but still be audible, with no steps and no zipper noise while moving.
2. It should not be **double-quiet** far away, which would mean Unity's rolloff and ours both apply. If far orbs are
   nearly inaudible, tell me.
3. Lift the orb 30 cm: reverb comes in and the volume stays the same.
4. Tell me how the curve feels (e.g. "everything past 50 cm is too quiet"). It's all in `MixTuning`, so we can
   tune it together without code changes.

## 7. What I need from you

- Shader property names for the orb glow (I'll write the glue component).
- Results of tests 1–3, especially 1a/1b.
- Orb physics preference (fall vs float), and the orb radius so I can confirm `Rest Height`.
