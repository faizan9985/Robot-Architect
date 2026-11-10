# Robot Architect repository cleanup

Scope: `X:\Capstone\Robot Architect`, branch `main`, Unity 6000.3.23f1.
The working tree was clean before this task. No changes were made to the original
XR_Capstone project, Git remotes, or Git history. Nothing was committed or pushed.

## Dependency analysis

Before asset deletion, scanned the full local Assets tree and serialized/code
configuration in Assets, Packages and ProjectSettings. Reviewed all application
C# scripts, the scene and prefab, hand-menu fields, runtime loading paths, Editor
validators, sample restoration, build profiles, input and XR configuration.
A static recursive GUID scan found 93 local scene dependencies. Unity's
AssetDatabase inventory covered 1,091 asset paths and found 179 recursive scene
dependencies including package assets. Its reverse-reference scan found **zero**
incoming references from retained assets to the removal candidates.

Unity inventories are recorded in `Logs/Cleanup-AssetInventory.txt`,
`Logs/Cleanup-SceneDependencies.txt`, `Logs/Cleanup-StaticDependencies.txt` and
`Logs/Cleanup-ReverseReferences.txt`. The temporary guarded removal helper was
removed after use. Assets were removed through `AssetDatabase.DeleteAsset`,
which also removed their metadata. Documentation was removed separately.

Compile-time dependencies required refactoring even though the scene had no
legacy gameplay asset references: HandMenuController optionally used
PuzzleGenerator, and RobotArchitectPhase1 used puzzle/Digital Twins types,
CubePiece.prefab and a legacy scene template.

## Removed

156 tracked files, including associated metadata:

- `Assets/Driving/`: driving input actions, physics material, materials, meshes,
  vehicle prefab and directory metadata.
- `Assets/Scripts/Driving/`: all driving gameplay, telemetry, environment,
  cockpit, UI and scenario scripts.
- `Assets/Scripts/DigitalTwins/`: transform lock controller and UI.
- `Assets/Scripts/PuzzleGenerator.cs`, `PuzzleManager.cs`, `PuzzlePiece.cs`,
  `PuzzleTarget.cs`, `UI/PuzzleCompletionUI.cs` and `UI/ConfettiUI.cs`.
- `Assets/Prefabs/`: Cube, Capsule and Sphere piece/target prefabs.
- `Assets/Scenes/`: Driving Simulator, Digital Twins and SampleScene.
- `Assets/_Recovery/`: both obsolete scene recovery copies.
- `Assets/TutorialInfo/` and `Assets/Readme.asset`: template tutorial content.
- Root `DIGITAL_TWINS_PIPES.md` and the five `DRIVING_*.md` documents.

`Assets/pipes/` and `Assets/Real Stars Skybox/` were already absent locally;
the audit found no Robot Architect dependency on either. No removal of local
copies is claimed. A full tracked removal manifest is in
`Logs/Cleanup-RemovedFiles.txt` (logs remain ignored by Git).

## Modified and added

- `RobotArchitectPhase1.cs`: retired the obsolete one-time scene creator;
  compares the scene cube against the standalone TestCube prefab, checks required
  physics/grab behavior, and enforces the single-scene build list. Adds repeatable
  Android OpenXR loader/feature, hand-support and Meta Quest profile checks.
- `HandMenuController.cs`: removed unused puzzle fields, restart listener/action,
  and puzzle event subscription. Palm tracking, optional controller shortcut,
  open/close, volume and passthrough behavior remain.
- `RobotArchitectSampleRestoration.cs`: explicit PackageInfo alias resolves an
  existing ambiguous-type compile error under this Unity Editor. Restoration
  content, import order and overwrite protections remain intact.
- `ProjectSettings/EditorBuildSettings.asset`: removed all three disabled legacy
  scene entries; RobotArchitect is the only entry and is enabled.
- Updated `Assets/RobotArchitect/README.md`, `HAND_MENU.md`, `PROJECT_CONTEXT.md`,
  `RECONSTRUCTION.md` and `Agents.md` for the standalone project and its tooling.
- Added root `README.md` and this report. Robotics programming, IK, moving
  conveyors and automated assembly are explicitly described as unimplemented.

RobotArchitectPlayModeSmoke.cs was inspected; it already uses the standalone
scene and cube and contains no legacy gameplay dependency, so it is unchanged.

## Preserved

Robot Architect scene/prefab/material asset bytes and GUIDs; connected hands rig;
interaction manager; menu sprites, UI raycasters and runtime UI registration;
AR Session and camera/passthrough wiring; workstation, arm and TestCube;
GrabColorFeedback, PassthroughController and PrototypeVolumeControl;
local Samples and TMP assets; XR, XRI, Oculus and CompositionLayers configuration;
Packages and all ProjectSettings except the global scene list; all three Meta
Quest profiles; Resources security configuration and credential build guard;
security documentation and sample reconstruction/restoration tooling.

Settings assets, including SampleSceneProfile and DefaultVolumeProfile, remain:
the task protects project configuration and their broader pipeline use is not
sufficiently established to remove them safely. Generic input assets, Oculus
resources and optional controller shortcut support also remain. Licensed samples
may contain demonstration content; the entire local sample dependency installation
is intentionally preserved as requested.

## Validation

Final Unity runs completed:

| Check | Result |
| --- | --- |
| Unity 6000.3.23f1 compilation with Android selected | PASS; final validation Editor exited with code 0 |
| Saved scene structural/reference/configuration validation | PASS; 5,332 checks |
| Local sample restoration validator | PASS; 694 GUIDs, 3 sprite file IDs, 16 scene materials, required prefab scripts and input actions |
| Play Mode smoke test | PASS; all 13 assertions, process exited after returning to Edit Mode |
| Retained source/configuration references to deleted GUIDs | PASS; zero |
| Intended build scene list | PASS; exactly RobotArchitect, enabled |
| Android OpenXR loader and required hand/passthrough features | PASS |
| Meta Quest Android profile and non-ControllersOnly support | PASS |
| Protected tracked asset/configuration hashes versus pre-cleanup | PASS; unchanged |
| Git whitespace validation | PASS; `git diff --check` |

Logs: `Logs/Cleanup-Android-Validation.log` and `Logs/Cleanup-PlayMode.log`.
The smoke checks cover runtime rig/manager/UI uniqueness, menu initialization,
cube support/settling, grab feedback initialization, passthrough-off startup,
menu open/close listeners and volume with synthetic eligibility.

The Meta SDK rewrote the local DevAgent access-token field on Editor startup.
After all Unity runs, the file was restored to its exact original safe bytes;
no token value was logged. Two optional Meta audio settings assets generated
solely by the smoke run were removed, leaving the original Resources inventory.
The credential build guard remains unchanged.

No new APK was built and no Quest hardware testing was performed. Native hand
tracking, physical UI input, visual quality and passthrough need device checks.
No cleanup-induced unresolved failures remain. The Editor logged unavailable
OpenXR runtime diagnostics, licensing/account and package warnings; these do not
establish device behavior and did not prevent the smoke assertions from passing.

Known pre-existing limitations: sample Hand material legacy texture reference,
DefaultVolumeProfile unresolved entries, package shader fallback warnings and
Editor XR/gaze/account diagnostics. These were not repaired by deleting shared
assets or changing the working XR configuration.


## Final Git review

On `main`: **156 deleted tracked files, 9 modified tracked files, 2 new documents**.
All changes are unstaged. No commit, push, remote change or unrelated deletion.
The only modified ProjectSettings file is EditorBuildSettings.asset. Existing
Resources, Packages, XR, XRI, Oculus, CompositionLayers, build profiles and Robot
Architect scene/prefab/material files match their pre-cleanup hashes.

The nine modified tracked files are Agents.md, RobotArchitectPhase1.cs,
RobotArchitectSampleRestoration.cs, Assets/RobotArchitect/README.md,
HandMenuController.cs, HAND_MENU.md, PROJECT_CONTEXT.md,
ProjectSettings/EditorBuildSettings.asset and RECONSTRUCTION.md.
The two new documents are README.md and CLEANUP_REPORT.md.
