# Driving Simulator: first playable milestone

Scene: `Assets/Scenes/Driving Simulator.unity`.

## Files

- New runtime scripts: `Assets/Scripts/Driving/VehicleController.cs`, `DriverSeatController.cs`, `SteeringWheelVisual.cs`.
- Reusable vehicle prefab: `Assets/Driving/Prefabs/Driving Vehicle.prefab`.
- Driving-only Input Action asset and six Inspector references: `Assets/Driving/Input/`.
- Eight simple URP materials: `Assets/Driving/Materials/`.
- Chassis contact material: `Assets/Driving/Chassis.physicsMaterial`.
- Corresponding Unity metadata files.
- Modified the Driving Simulator scene. Existing global build-list addition from the foundation task remains; this milestone does not change scene order or enable states. The Meta Quest profile inherits that list.

SampleScene, Digital Twins, puzzle/digital-twin scripts and prefabs, existing XR rig prefab, OpenXR settings, packages, and HandMenuController are unchanged. No commits were made.

## Scene and vehicle

```
Driving Vehicle (Rigidbody, VehicleController, DriverSeatController, SteeringWheelVisual)
  Vehicle Body (BoxCollider, four ground probes)
  Driver Seat Anchor (desired eye position)
  XR Vehicle Space
    XR Origin Hands (XR Rig) [existing prefab instance]
      Camera Offset / Main Camera, controllers, hands, visualizers, tracking
      Locomotion [components retained and disabled while seated]
  Visual Body (chassis, open cockpit, dashboard, seats, steering wheel)
  Wheel FL / FR / RL / RR
  Driver Controls (world-space XR canvas / Reset Vehicle button)
  Driving Controls (instruction label)
Driving Test Area (80 x 100 m surface, loop markings, infield, landmarks, barriers)
Directional Light
XR Interaction Manager
Hand Menu Root (existing menu, launcher, passthrough and volume)
AR Session - Meta Passthrough
Floor [old invisible plane inactive; new test surface supplies collision]
```

The 900 kg Rigidbody uses a box chassis contacting the flat surface. Four ray probes gate traction; forward acceleration and lateral grip use forces, with a smoothed bicycle steering model controlling yaw rate. Wheel meshes are visual; there are no WheelColliders, suspension or detailed tire dynamics. Continuous collision detection and interpolated rendering are enabled. Pitch and roll are constrained, preventing cockpit tilting or flipping on this flat course. This is a flat-surface prototype, not a model for ramps or uneven terrain.

Inspector defaults: forward speed 6 m/s, reverse 2 m/s, acceleration 1.5 m/s², braking 4 m/s², steering angle 22 degrees, steering responsiveness 1.5 per second, rolling resistance 0.25 m/s², lateral grip 8, wheelbase 2.3 m, maximum yaw rate 30 degrees/second, center-of-mass offset (0, -0.25, 0). These are tuneable on VehicleController. The steering wheel and front wheel pivots animate with steering input; no physical wheel grabbing is implemented.

## Controls and seated XR

- Right controller analog grip: accelerate.
- Left controller analog grip: brake, then hold at rest for 0.4 seconds to engage reverse.
- Left thumbstick X: steer, with deadzone and smoothing.
- Existing trigger-based XR UI input remains available.
- Optional Editor-only fallback: W accelerates; S brakes/reverses; A/D steer. Keyboard values combine with XR values without replacing bindings. Focus the Game view when testing interactively.

Driving actions are separate from the existing XRI asset. Opening the hand menu, losing application focus, or pausing blocks propulsion and applies braking. After reset, menu use or focus loss, release grips/keys and center the stick before driving again.

The single existing XR Origin is parented beneath XR Vehicle Space, so the complete tracked rig inherits the Rigidbody's movement and yaw. The existing camera TrackedPoseDriver stays enabled. No production driving script writes camera local position or rotation. Physical head translation, looking, leaning and head roll remain tracked normally; vehicle steering never rotates the camera separately.

DriverSeatController translates the XR Origin to place the current head at Driver Seat Anchor plus the Inspector seat offset. It calibrates at startup, once more when initial headset tracking becomes available, and on explicit reset/recenter. It never continually pins the head in place or forces a new head orientation. Look forward and use the Quest system recenter before starting if the initial tracking-space heading is unsuitable.

The scene disables all existing locomotion providers, the mediator, body transformer and CharacterController. The seat controller keeps movement blockers disabled while seated. Teleport interactors are inactive. Only the two scene controller input managers' optional move/turn/teleport action references are unassigned, preventing thumbstick locomotion mediation from switching away from the normal interaction ray. Their UI references and the global XRI actions remain intact.

Reset Vehicle is a dashboard world-space button using the existing XR UI input pipeline and a TrackedDeviceGraphicRaycaster. Its persistent click event targets this scene's vehicle. It restores the captured spawn pose, clears linear/angular velocity and smoothed input, and recalibrates the seat by translating the XR Origin. No scene reload occurs. HandMenuController has no driving dependency or new puzzle logic; palm launcher, passthrough, volume and Close remain intact.

For reuse, the prefab holds vehicle geometry, physics, driving input and internal seat/visual references. Assign the scene's existing XR Origin, movement blockers and optional hand menu on its instance; no XR Origin is embedded in the prefab.

## Validation

Structural inspection passed with no missing scripts or broken serialized references. Required driving input, menu, passthrough and volume references resolve. There are no puzzle or digital-twin components. Play Mode confirmed exactly one XR Origin, Interaction Manager, EventSystem and XRUIInputModule; XRI creates the latter two through its existing runtime registration path.

Temporary Play Mode checks used a virtual keyboard through the actual Input Actions and manually stepped Unity physics. They covered acceleration, braking, delayed reverse, both speed limits, A/D steering, constrained roll/pitch, independent synthetic head poses, XR hierarchy following, seat restoration, reset velocity, held-input release gating, menu/focus braking, the actual dashboard reset event and perimeter collision containment. They passed with exit 0. The test disabled the desktop pose driver only while supplying synthetic head poses; that state was never saved. The test's focus-routing settings were applied to an in-memory InputSettings clone. These checks do not establish real controller tracking, native passthrough, Quest frame rate or comfort.

Temporary authoring/test helpers are retained under `.utmp/driving-validation/`, outside Assets and player builds. Logs: `Logs/DrivingMilestone-Setup.log`, `Logs/DrivingMilestone-Structural.txt`, `Logs/DrivingMilestone-PlayMode.log`, `Logs/DrivingMilestone-PlayMode.txt`. The Unity-rendered preview is `Logs/DrivingMilestone-Cockpit.png`; untracked edit-time rig renderers were suppressed for that preview only.

Final compilation/import after helper removal passed with exit 0 and no script compilation errors, without `-noUpm`. All 37 Play Mode assertions passed; the harness log's count of 38 also includes one input diagnostic line. Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "X:\Capstone\XR_Capstone" `
  -logFile "X:\Capstone\XR_Capstone\Logs\DrivingMilestone-Final-Compile.log"
```

Expected desktop warnings are the existing gaze-assistance warning and no active XRSessionSubsystem. Licensing emitted access-token/entitlement diagnostics while allowing the passing runs. Existing Quest SSAO performance guidance remains outside this milestone; cockpit camera post-processing is disabled locally. No engine audio was added; volume still controls the existing application audio.

## Manual Quest 3 checks

1. For a test build, launch Driving Simulator explicitly; the existing build list still starts with SampleScene. Use the existing Meta Quest Android/OpenXR profile. Physically sit down, look forward, and establish the intended system recenter heading before driving.
2. Start with controls released. Verify the initial eye position is inside the cockpit, the dashboard/wheel are visible, and the road is unobstructed. Lean forward/back/sideways and look around: tracking must remain independent, including while the car turns. Tune DriverSeatController's seat offset if needed.
3. Squeeze right grip gradually, release to coast, and steer with small left/right stick inputs. Verify progressive acceleration, a stable upper speed, wheel animation and no snap turns, teleport rays, forced head rotation or cockpit roll. Move both sticks in every direction; the rig must not leave the seat.
4. While moving forward, squeeze left grip. Verify it brakes to a stop before reversing after the brief hold. Verify reverse stays slow and steering direction reverses naturally. Right grip should brake out of reverse before resuming forward motion.
5. Drive a slow lap around the four corners. Check lane markings and landmarks, then approach a barrier slowly: the car must be contained and remain upright. Assess stable frame rate and comfort directly on the device.
6. Stop, turn toward the dashboard Reset Vehicle button, and select it with a controller UI ray/trigger. Verify the original spawn pose, no continuing drift/yaw, and correct seat position. Reset once while a grip is held: the car must wait for release/neutral controls before moving again. Repeat using a tracked hand's far pinch and near poke.
7. Put down the controllers, acquire hands, show the left-palm launcher and open the menu. Verify launcher visibility rules, Close/reopen, volume percentage changes and passthrough ON/OFF. While driving, opening the menu should brake to rest and must never engage reverse. There is no hand-driven steering/throttle in this milestone.
8. Switch between hands/controllers; confirm meshes, rays and UI targeting recover. Suspend/resume the app and briefly open the Quest system menu while driving: verify braking/input release gating and tracking recovery. Repeat reset and passthrough after resuming.
9. In Editor Play Mode, focus Game view and test W, S, A/D and the reset button. Confirm these keys do not move the XR Origin independently of the car. Editor simulated hands are not a substitute for Quest tracking tests.
