# Robot Architect — Remix Table

A standalone Unity XR application for Meta Quest 3, using Unity **6000.3.23f1**.

The app that ships is the **Remix Table**: a seated, hand-tracked stem remixer.
Open `Assets/RemixTable/Scenes/RemixTable.unity` (the only enabled scene in the
build list) and build with the **Meta Quest** build profile
(`Assets/Settings/Build Profiles/Meta Quest.asset`).

The original Robot Architect workstation scene
(`Assets/RobotArchitect/Scenes/RobotArchitect.unity`) is still in the project but
disabled in the build. It contains a fixed workstation, a robot arm placeholder,
a conveyor placeholder and a grabbable TestCube; robot programming and assembly
gameplay are not implemented.

## Using the Remix Table

A song is split into four stems (Vocals, Drums, Bass, Other), each shown as a
glowing ribbon on an arc around you. Pinch a ribbon and move your hand up or down.

| Control | Where | What it does |
| --- | --- | --- |
| VOLUME / FILTER / PITCH | Row under the transport | Picks what a pinch edits. PITCH shifts by whole semitones without changing tempo. |
| Two hands on one ribbon | Ribbons | Edits the region between your hands. |
| Hold a ribbon's end | Ribbons | Edits the whole stem. |
| EDIT / LIVE | Playback row | EDIT pauses while you hold; LIVE keeps playing. |
| M / S / CLR | MIX row (per stem) | Mute, solo, or clear all edits on that stem (CLR can be undone). |
| UNDO / REDO | Icons above the ribbons | One step per pinch hold. |
| SNAP | TOOLS row (top) | Region edges land on beats; beat lines appear on the ribbons. |
| LOOP | TOOLS row | Loops the last region you edited (or the current bar), gaplessly. Press again to stop. |
| SPATIAL | TOOLS row | Shows one orb per stem around you. Grab and move an orb to move that stem left/right and nearer/farther. |
| REC | TOOLS row | Records exactly what you hear to a WAV file. Press again to stop. |
| ROOM | TOOLS row | Passthrough: see your real room (Quest only). Hides the virtual floor. |
| MIC | TOOLS row | Speak one command (needs voice set up, below). |
| RESET | TOOLS row | Clears the song's edits and mute/solo (UNDO brings the edits back). |

Each song's remix (edits, mute/solo, spatial positions) is **saved automatically**
and restored the next time the app opens.

Files on the headset (copy them off with `adb pull`):

- Recordings: `/sdcard/Android/data/<package>/files/Recordings/remix-*.wav`
- Saved remixes: `/sdcard/Android/data/<package>/files/Remixes/<song>.json`

### Voice commands

Examples: "mute the drums", "solo vocals", "unmute everything", "clear the bass",
"pitch mode", "filter", "live mode", "snap on", "loop", "stop looping", "spatial on",
"start recording", "stop recording", "undo", "redo", "next song", "pause", "play",
"reset everything", "show me my room".

Voice uses the Meta Voice SDK's dictation and needs a free Wit.ai app:

1. Create an app at <https://wit.ai> and copy its **Client Access Token**
   (Settings).
2. In Unity: **Meta > Voice SDK > Get Started**, paste the token and save the
   generated Wit configuration asset.
3. Select `VoiceInput/Dictation` in the RemixTable scene and assign that
   configuration on **App Dictation Experience**. Leave the object inactive; it
   switches on when MIC is pressed.

Until this is done, MIC shows "VOICE NEEDS A WIT.AI APP".

### Adding songs

Songs live on `RemixTableManager > Song Library Manager > Songs`. Each needs four
stems with identical length and sample rate (`drums`, `bass`, `other`, `vocals`,
e.g. from a stem separator such as Demucs). After adding one, run
**Tools > Remix Table > Add Feature Rows (Mix, Tools, Spatial, Voice)**: it detects
each song's tempo and first beat from its drum stem for SNAP.

### Editor tools and tests

All scene UI is built by idempotent menu steps under **Tools > Remix Table**.
Tests live in `Assets/RemixTable/Tests` (EditMode and PlayMode) and run from the
Test Runner or the CLI:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath . -runTests -testPlatform EditMode -testResults EditMode.xml
```

## Project notes

Preserve the existing OpenXR and Meta hand tracking settings, XRI 3.x
infrastructure, and locally restored XR samples.

- [Scene and validation guide](Assets/RobotArchitect/README.md)
- [XR setup and troubleshooting](PROJECT_CONTEXT.md)
- [Hand menu and passthrough](HAND_MENU.md)
- [Sample restoration and source reconstruction](RECONSTRUCTION.md)
- [Security safeguards](SECURITY.md)
- [Cleanup dependency analysis and validation](CLEANUP_REPORT.md)

Do not treat Editor validation as Quest hardware testing. Recheck near/far hand
grabbing, menu poke/ray input, passthrough, recenter, and suspend/resume on Quest
when making future XR changes.
