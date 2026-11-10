# Robot Architect foundation

Open `Scenes/RobotArchitect.unity` in Unity **6000.3.23f1**.

The scene preserves the connected XR Origin Hands rig, interaction manager,
left-palm hand menu, AR Session, passthrough controller and volume controls.
Passthrough starts off. XRI creates its runtime UI EventSystem; do not add another.

`RobotArchitect` groups GameManager, Workstation, RobotSystems and Interactables.
GameManager is an empty organizational object. The fixed workstation is 0.7 m
horizontally ahead of the authored starting camera, with a 0.90 m tabletop.
InputZone and OutputZone reserve future gameplay locations. ConveyorPlaceholder
does not move. RobotArmPlaceholder has no IK, animation, joint physics or programming.

`Prefabs/TestCube.prefab` is independent. It retains a Rigidbody, solid BoxCollider,
XRGrabInteractable and GrabColorFeedback. The 6.5 cm scene cube starts on InputZone
and references the scene interaction manager; the prefab has no scene reference.

## Validation

Use **Tools > Robot Architect > Validate Saved Phase 1 Scene** outside Play Mode.
Save current scene edits first: validation reopens the saved scene without saving it.
The validator checks references, missing scripts, infrastructure counts, hierarchy,
TestCube physics/grab settings against its standalone prefab, URP materials and
the single-scene build list. The obsolete one-time template creator was retired.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$project = 'X:\Capstone\Robot Architect'
& $unity -batchmode -nographics -projectPath $project -quit `
  -executeMethod RobotArchitect.Editor.RobotArchitectSampleRestoration.ValidateRestoredAssets `
  -logFile "$project\Logs\RobotArchitect-Validate.log"
# Omit -quit: the smoke check exits after returning from Play Mode.
& $unity -batchmode -nographics -projectPath $project `
  -executeMethod RobotArchitect.Editor.RobotArchitectPlayModeSmoke.Run `
  -logFile "$project\Logs\RobotArchitect-PlayMode.log"
```

The smoke check exercises runtime initialization, cube support/gravity, UI-system
uniqueness, actual menu listeners and volume using synthetic palm eligibility.
It does not test tracked palms, pinches, physical pointer input or native passthrough.
See the root CLEANUP_REPORT.md for the latest cleanup validation results.

## Quest checks

Verify both hand meshes and tracking reacquisition; near grab and far pinch/grab;
release onto the workstation; grab color feedback; left-palm launcher; poke/ray UI;
Close and volume; passthrough on/off; recenter and suspend/resume. Review menu
sprites and material transparency visually. Editor success is not device testing.
