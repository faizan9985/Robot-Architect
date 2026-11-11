# Remix Table — Game & Code Spec (shared contract)

Owners: **Farhan = runtime C# (audio engine, mix logic, gestures, networking)** ·
**Faizan = Unity scene, prefabs, visuals/shaders, MR setup, audio import, builds, headset testing**

If you need to change this contract, tell the other person first.

---

## 1. The experience

Track: **Entertainment**. Seated, hands-only, mixed reality on your real desk.

*Pull a song apart with your hands. Every instrument becomes a glowing object on your desk.
Move, mute, twist, and drop your own remix.*

1. **Desk calibration:** tap your real desk with your palm. That sets the desk height.
2. **Pull apart:** a glowing record plays the full song. Grab it with both hands and pull, and it
   splits into 5–6 **stem orbs** (drums, bass, keys, lead, vocals, FX) that land across the desk.
3. **Mix with your hands** (each orb plays from where it sits, through Meta XR Audio spatial sound):

| Gesture | Effect |
|---|---|
| Move orb | Distance to the player → volume. Left/right is handled by spatial audio. |
| Pinch-tap orb | Mute / unmute (fast volume fade, never stop the source) |
| Grab + twist wrist | Filter: twist one way = low-pass ("underwater"), the other way = high-pass ("thin"), centre = neutral |
| Lift orb above desk | Reverb amount |
| Hold orb near ear | Solo: the other stems duck down |
| Gather everything, then slam the desk | **Drop**: build-up (filters sweep down, riser), then the hit lands on the next downbeat with a light shockwave |

4. **Record a take** (stretch): record about 60 s of mix changes, saved as a playable record on the shelf.
5. **Jam Session** (stretch, go/no-go Saturday night): room code; both players see the same orbs on
   their own desks and each other's ghost hands; whoever grabs an orb holds it until they let go. Only
   the mix state is sent over the network, never audio.
6. **Bring your own stems** (stretch): a folder of stems on the device becomes a new record.

Song licences: **CC0 or CC BY only** (no NC, no ND). Credit every artist in the app and on Devpost.

---

## 2. Ownership

| Path | Owner |
|---|---|
| `Assets/RemixTable/Scripts/**`, `Tests/**`, `Dev/**` | Farhan |
| Main scene (currently `Assets/RobotArchitect/Scenes/RobotArchitect.unity`) | **Faizan only** |
| `Assets/RemixTable/Prefabs/**`, `Materials/**`, `Shaders/**`, `Audio/**`, `Songs/**` (real song assets) | Faizan |
| `ProjectSettings/**`, `Packages/**`, XR/Settings folders | Faizan. Package changes (e.g. Photon) need both of you to agree. |
| Old `Assets/Scripts/UI/**` (hand menu, passthrough) | Faizan. New code must not depend on it. |

Branches `farhan/<x>` and `faizan/<x>`. Code and scene changes never go in the same commit.

---

## 3. Code architecture

Namespace `RemixTable`. Runtime assembly `RemixTable` (`Assets/RemixTable/Scripts/RemixTable.asmdef`,
references `Unity.XR.Interaction.Toolkit` and `Unity.XR.Hands`). EditMode tests in `RemixTable.Tests`.

**Golden rule: `MixState` is the single source of truth.** All input (desktop dev controls, XR hands,
and later the network) changes the mix *only* through `MixState`. The audio engine and visuals only
*read* it. That's what makes multiplayer and record-a-take add-ons later instead of rewrites.

| Component | Kind | Responsibility |
|---|---|---|
| `SongDefinition` | ScriptableObject | Title, artist, licence, attribution URL, BPM, beats per bar, loop length in bars, list of `StemDefinition` (name, `StemRole`, clip, colour). |
| `MixState` | plain C# | Per stem: `muted`, `soloed`, `gain01`, `filter` (−1..1), `reverb01`, `holderId`. Global: `dropPhase`. Change events; a `version` counter; serialisable snapshot for networking. Mutations take a source (local/remote). |
| `MixMapping` | static/pure | Position → gain, height → reverb, twist angle → filter, filter → cutoff Hz. All curves and dead zones in one place, with constants in a `MixTuning` ScriptableObject. |
| `MusicClock` | plain C# + driver | Song start on the audio clock (`AudioSettings.dspTime`), current beat/bar, next-downbeat time, quantisation. The start time comes from an `ITimeSource`, so network time can plug in later. |
| `StemPlayer` | MonoBehaviour | One `AudioSource` per stem, all started with `PlayScheduled` at the same dsp time, looping. Applies `MixState` with smoothing (no zipper noise, no clicks). Never stops one stem by itself. |
| `IStemEffects` / `FilterComponentEffects` | interface + impl | Low-pass/high-pass/reverb on a stem. First implementation uses Unity filter components; it can be swapped for an AudioMixer version if those don't work with the Meta spatializer. |
| `StemAnalyzer` | MonoBehaviour | Per stem: RMS level and low/mid/high bands, calculated every frame with no allocations. Public getters for Faizan's shaders. |
| `DropController` | MonoBehaviour | Idle → Build (filter sweep + riser) → Hit (quantised to next downbeat) → Release. `UnityEvent`s for VFX/sound. |
| `StemOrb` | MonoBehaviour | Links a scene object to a stem index. Methods: `BeginHold(holderId)`, `EndHold()`, `ToggleMute()`, `SetTwist(float)`, `SetSolo(bool)`; reports its world position to the mapping. |
| `DeskFrame` | MonoBehaviour | Desk plane (height + forward), listener reference (head/camera). |
| `DesktopDevInput` | dev MonoBehaviour | Mouse/keyboard controls so you can mix without a headset (see the prompt). |
| *(later)* `HandGestureInput` | MonoBehaviour | XR Hands: grab, twist, pinch-tap, ear-solo, slam, pull-apart. |
| *(later)* `JamSession` | MonoBehaviour | Photon Fusion shared mode: room code, `MixState` sync, ghost hands. |

### Audio rules
- Stems in a song are identical in length (sample-exact), and the loop length is a whole number of bars.
- **Import stems as Force To Mono** (spatial audio needs mono sources), Decompress On Load, Vorbis.
- Distance loudness comes from `MixMapping`. Neutralise the built-in distance rolloff so it isn't applied
  twice. Spatial audio provides direction only. *(Check on the headset.)*
- The project's spatializer is **Meta XR Audio** (`ProjectSettings/AudioManager.asset`). Sources use
  `spatialize = true`, `spatialBlend = 1`.
- Ramps of at least 20 ms on every gain change. No `AudioSource.Stop()` on individual stems during play.
- Pause or resume everything cleanly in `OnApplicationPause` / `OnApplicationFocus`.

### Events Faizan wires in the Inspector
`StemOrb`: `OnMuted(bool)`, `OnSoloed(bool)`, `OnHeld(bool)`. `DropController`: `OnBuildStarted`, `OnHit`, `OnReleased`.
`MusicClock`: `OnBeat(int)`, `OnBar(int)`.

---

## 4. Faizan's checklist (single-player MVP)
1. Orb prefab per stem role (glowing material driven by `StemAnalyzer` getters) with `StemOrb`, a collider and an `XRGrabInteractable`.
2. A record prefab for the pull-apart moment.
3. Main scene: `DeskFrame`, `StemPlayer`, `DropController`, passthrough on at start, `Floor` hidden.
4. Import the 3 real songs as `SongDefinition` assets (stems Force To Mono).
5. Build and upload to the Meta dashboard "Competition" channel early (by Monday at the latest).

## 5. Quality bars
72 FPS on Quest 3 · no per-frame allocations or `Find` calls · one-handed and seated · no debug visuals in
builds · a `[RemixTable]` log prefix, quiet during normal play.
