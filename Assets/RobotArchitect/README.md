# Robot Architect — Phase 1

Open `Assets/RobotArchitect/Scenes/RobotArchitect.unity` in Unity **6000.3.23f1**.

The scene was duplicated from `Assets/Scenes/Digital Twins.unity` through
`AssetDatabase.CopyAsset`, then authored and saved through Unity Editor APIs.
The original scene and referenced shared assets retain their identities.
The new scene has its own GUID and is the first enabled global build scene;
all pre-existing entries retain their order and enabled states.

## Foundation

- Original connected XR Origin Hands rig, camera, Camera Offset and hand interactors.
- Original XR Interaction Manager and runtime XRI UI registration behavior.
- Complete original Hand Menu Root: left-palm launcher, open/close, passthrough and volume.
- Original AR Session and disabled camera ARCameraManager; passthrough starts off.
- Pipes Manipulation Root and Pipes Lock Control removed only from this scene.
- Existing shared menu scripts, OpenXR settings, packages and Quest build profile are unchanged.

## New content

`RobotArchitect` contains GameManager, Workstation, RobotSystems and Interactables.
GameManager is intentionally an empty organizational object; Phase 1 needs no
additional runtime manager. InputZone, OutputZone, ConveyorPlaceholder and
RobotSpawnPoint reserve future gameplay locations. The conveyor does not move.

The workstation center is 0.7 m horizontally ahead of the authored starting camera
direction, with a tabletop 0.90 m above the XR Origin base. It is a fixed virtual
table, not detected room geometry, and does not follow the player's head.
Dark metallic and cyan URP Lit materials use no new rendering package or effect.

RobotArmPlaceholder has Base, UpperArm, Forearm and Gripper visuals. It has no
IK, animation, joint physics, programming, colliders or independent interaction.

`Prefabs/TestCube.prefab` is an independent prefab derived from a Unity-instantiated
copy of `Assets/Prefabs/CubePiece.prefab`. Only the copied PuzzlePiece component
was removed. Rigidbody, BoxCollider, XRGrabInteractable and GrabColorFeedback
configuration is retained. The test cube is scaled to 6.5 cm, uses a new material,
and starts on InputZone. Its scene instance explicitly references the existing
interaction manager; the reusable prefab does not reference a scene object.

## Validation commands

Use **Tools > Robot Architect > Validate Saved Phase 1 Scene** outside Play Mode.
It opens the saved scene, so save any current scene edits first. It checks
references, missing scripts, infrastructure counts, required hierarchy, original
cube settings, URP materials and build-list position. It does not save the scene.

The one-time **Create Phase 1 Scene** command refuses to overwrite an existing
scene. Do not delete the delivered scene to rerun it during normal development.

Batch commands use the installed Unity Editor:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'X:\Capstone\Robot Architect' `
  -executeMethod RobotArchitect.Editor.RobotArchitectPhase1.Validate `
  -quit -logFile 'X:\Capstone\Robot Architect\Logs\RobotArchitect-Phase1-Validate.log'

# Intentionally omit -quit: this check exits after returning from Play Mode.
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'X:\Capstone\Robot Architect' `
  -executeMethod RobotArchitect.Editor.RobotArchitectPlayModeSmoke.Run `
  -logFile 'X:\Capstone\Robot Architect\Logs\RobotArchitect-Phase1-PlayMode.log'
```

The smoke check exercises runtime initialization, cube support/gravity, UI-system
uniqueness, real menu button listeners and volume. Menu eligibility is injected
synthetically. It does not simulate tracked palms, pinches or physical pointer input.
Editor helpers are under Editor folders and are excluded from player builds.

## Recorded implementation validation

- `Logs/RobotArchitect-Phase1-Create.log`: compilation succeeded; 5,322 saved-scene
  structural/reference checks passed; Unity returned code 0.
- `Logs/RobotArchitect-Phase1-PlayMode.log`: 13 smoke assertions passed, including
  runtime UI-system uniqueness, cube support, open/close listeners and volume.
- A before/after SHA-256 comparison confirmed existing files under Assets,
  Packages and ProjectSettings were unchanged except the requested global
  build-list edit. Pre-existing working-tree OpenXR changes were preserved.
- No Quest build, device interaction, visual rendering review or native
  passthrough test was performed. Existing Unity licensing/account, gaze and
  unsupported Editor XR warnings are not evidence of a failed scene reference.

## Manual Quest 3 checks still required

1. Open the new scene and verify the original Meta Quest profile remains selected
   when preparing an Android build. Build and Run using the existing profile.
2. Check the workstation appears ahead of the initial viewpoint. Real headset
   height/recenter affects perceived reach; no spatial/table detection is provided.
3. Acquire both hands. Use near grab and far pinch selection on TestCube; release
   onto the workstation. Confirm the existing grab color feedback changes/restores.
4. Raise the left palm toward your face and look at it. Select the launcher using
   the other hand; test poke/ray UI, Close, and the volume slider with audible content.
5. Toggle passthrough on/off. Verify the room appears behind virtual geometry and
   the original virtual camera background returns. The virtual floor remains opaque.
6. Test tracking loss/reacquisition, recenter and suspend/resume for menu and rig.

No Quest runtime testing is implied by successful Editor validation. Existing
missing/unresolved entries in `Assets/Settings/DefaultVolumeProfile.asset` are
outside Phase 1 scope and have not been repaired.
