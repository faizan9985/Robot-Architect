# Remix Table — Claude Code instructions

## What this is
A hands-only mixed-reality music app for Meta Quest 3 (Meta VR Start Developer Competition 2026,
Entertainment track). A song splits into stems, and each stem is a glowing orb on the player's real desk
that they mix with their hands. Deadline: **Nov 18, 2026, 2:00pm CST**.
The repo is still named Robot Architect; that project was dropped. Ignore the robot content in old docs.

Game design, architecture and file ownership: @REMIX_TABLE_SPEC.md (follow it).

## Who I am and what you may touch
- I (Farhan) write runtime C# only: `Assets/RemixTable/Scripts/`, `Tests/`, `Dev/`.
- My teammate (Faizan) owns the main scene, prefabs, materials, shaders, real song assets, project
  settings, packages and builds.
- Never open, edit or save the main scene (`Assets/RobotArchitect/Scenes/RobotArchitect.unity`) or
  existing prefabs. If a task needs that, write instructions for Faizan instead.
- Never add or upgrade packages, or edit `Packages/*` or `ProjectSettings/*`, without asking me.
- I currently have **no headset**. Anything involving XR hands, the spatializer on device, or
  passthrough is unverified until tested on a Quest. Say so plainly.

## Stack (do not change versions)
Unity **6000.3.23f1**, URP 17.3.0, XR Interaction Toolkit **3.3.2**, XR Hands **1.9.0**,
OpenXR + Unity OpenXR Meta 2.6.1, AR Foundation 6.6.0 (passthrough), Meta XR SDK 209 (installed;
Meta XR Audio is the project's spatializer plugin). Unity 6 APIs (`Rigidbody.linearVelocity`,
`FindFirstObjectByType`). XRI 3.x types in `UnityEngine.XR.Interaction.Toolkit.Interactables` /
`.Interactors`. No deprecated "Action Based" components.

## XR rules (from PROJECT_CONTEXT.md, which is worth reading before touching XR)
- The Editor XR simulator does not reproduce Quest hand aim. Only a headset test proves hand input.
- Keep OpenXR hand features enabled; keep debug visuals off in builds.
- Grabbables copy `Assets/RobotArchitect/Prefabs/TestCube.prefab` (Rigidbody + Collider +
  XRGrabInteractable bound to the scene's XR Interaction Manager).

## Working with Unity from Claude Code
- C#: edit directly. Scenes/prefabs/assets: never hand-write `.unity`/`.prefab`/`.asset` YAML or
  invent GUIDs. Create them through Editor scripts (menu items under `Tools > Remix Table`) that I run.
- Unity is usually open on my machine. Batch mode fails while it's open, so ask me to check the
  Console or run the Test Runner instead. If I say Unity is closed, batch mode is fine:
  `Unity -batchmode -projectPath <repo> -runTests -testPlatform EditMode -testResults <file> -logFile <file>`
- Report compile errors and test results honestly. Never claim device behaviour is verified.

## Security
Never commit a non-empty `accessToken` in `Assets/Resources/DevAgentSettings.asset` (Meta's SDK
rewrites it whenever the Editor opens). Check `git diff` on `Assets/Resources` before every commit.

## Team rules
Work on `farhan/<feature>` branches, small commits, pull before starting. Code and scene changes
never go in the same commit.
