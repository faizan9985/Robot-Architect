PROJECT CONTEXT — XR_Capstone

Environment
- Unity version: 6000.3.23f1 LTS
- Project template: Universal 3D / SRP
- Project name: XR_Capstone
- Target platform: Meta Quest
- XR stack:
  - XR Interaction Toolkit 3.3.2
  - XR Plugin Management
  - OpenXR
  - XR Hands 1.9.0
- Starter Assets imported
- Hands Interaction Demo imported
- XR Hands samples imported:
  - Gestures
  - HandVisualizer
  - Hand Capture

OpenXR / Meta Quest configuration
- OpenXR enabled for Android / Meta Quest.
- Meta Quest Support enabled.
- Hand Interaction Profile enabled.
- Hand Tracking Subsystem enabled.
- Meta Hand Tracking Aim enabled.
- Meta Quest Touch Pro Controller Profile enabled.
- Latency Optimization changed to:
  - Prioritize Input Polling
- There was a Project Validation warning about Screen Space Ambient Occlusion performance on Meta Quest. It was left alone.
- Important discovery:
  - Hand Tracking Subsystem + Meta Hand Tracking Aim were required for actual Quest hand tracking and far-hand aim poses.
  - Before enabling those, controllers worked on Quest but hands did not initialize correctly.

XR Rig history
Originally created:
- XR Origin (VR)
  - Camera Offset
    - Main Camera

Also added manually:
- Left Controller
- Right Controller
- Right Ray Interactor
- Input Action Manager
  - XRI Default Input Actions

Later switched to using the prefab from the Hands Interaction Demo:
- XR Origin Hands (XR Rig)

The original XR Origin (VR) was disabled rather than immediately deleted.

Current active rig is:
XR Origin Hands (XR Rig)
  Camera Offset
    Main Camera
    Gaze Interactor
    Gaze Stabilized
    Left Controller
    Left Controller Teleport Stabilized Origin
    Right Controller
    Right Controller Teleport Stabilized Origin
    Left Hand
      Poke Interactor
      Near-Far Interactor
      Aim Pose
      Pinch Point Stabilized
      Pinch Grab Pose
      LeftHandQuestVisual
      LeftHandAndroidXRVisual
    Right Hand
      Poke Interactor
      Near-Far Interactor
      Aim Pose
      Pinch Grab Pose
      Pinch Point Stabilized
      RightHandQuestVisual
      RightHandAndroidXRVisual
    Hand Visualizer
    Gaze Assistance objects
    Locomotion
    Hands Smoothing Post Processor
    Interaction Attach Controller objects
    CurveInteractionCaster stabilization objects
    Trackables

There is also:
- XR Interaction Manager
- EventSystem
- Floor
- GrabCube

Input
- XRI Default Input Actions was added to the Input Action Manager.
- Hand interactions use the default XRI action maps.
- Left hand Near-Far Interactor:
  - Handedness = Left
  - Interaction Manager points to scene XR Interaction Manager
  - Interaction Layer Mask = Everything
- Right hand equivalent configured for Right.

Locomotion
The old XRI "Locomotion System" GameObject menu entry does not exist in XRI 3.3.

Instead locomotion components were added manually:
- Locomotion Mediator
- XR Body Transformer
- Continuous Move Provider
- Continuous Turn Provider
- Snap Turn Provider

Important:
- "Snap Turn Provider (Action Based)" is deprecated in XRI 3.x.
- It was initially added, showed a deprecation warning, then removed.
- Correct component is:
  - Snap Turn Provider
- Locomotion providers were wired to the Locomotion Mediator.

Basic test scene
Created:
- Floor
  - Plane at approximately 0,0,0
- GrabCube
  - Cube around 0,1,2
  - Rigidbody
  - XR Grab Interactable
  - Interaction Layer Mask = Everything
  - XR Interaction Manager assigned

Controller interaction
- Controller ray interaction works.
- Controller rays highlight the GrabCube when targeting it.
- GrabCube can be grabbed successfully with Quest controllers.
- Therefore the cube/interactable setup is known-good.

HAND INTERACTION DEBUGGING HISTORY

Initial symptom in Editor
- Hands rendered in the XR Interaction Simulator.
- Simulator UI allowed Poke / Pinch / Grab visual poses.
- Purple simulator warning said approximately:
  "Add the Hands Visualizer sample of Hand Poses. Hand poses are not interactive."
- Hands initially could not grab or even far-hover the cube.
- Controllers could grab it.

Important conclusion:
- The cube was not the problem.
- Controller interaction proved XR Grab Interactable and XR Interaction Manager were working.

Near vs far hand interaction
- When the simulated hands were physically brought very close to the cube, fingertips turned orange.
- This proved near interaction / proximity detection worked.
- From a distance, hand rays initially did not highlight the cube.

Near-Far Interactor inspection
Left Hand > Near-Far Interactor contained:
- Near-Far Interactor
- Interaction Attach Controller
- Sphere Interaction Caster
- Curve Interaction Caster

Near-Far Interactor settings observed:
- Interaction Manager assigned
- Interaction Layer Mask = Everything
- Handedness = Left
- Enable Near Casting = on
- Near Caster = Sphere Interaction Caster
- Enable Far Casting = on
- Far Caster = Curve Interaction Caster
- Far Attach Mode = Far
- UI Interaction = enabled

Interactor filters:
- Starting Target Filter = None
- Starting Hover Filters = 0
- Starting Select Filters = 0

Curve Interaction Caster observed settings:
- Cast Origin = Aim Pose
- Aim Target Object = Near-Far Interactor
- Raycast Mask = Default, UI
- Raycast Trigger Interaction = Ignore
- Raycast Snap Volume Interaction = Collide
- Cast Distance = 10
- Hit Detection Type = Cone Cast
- Target Num Curve Segments = 1
- Cone Cast Angle = 6

Debugging far cast
- Interaction Attach Controller had:
  - Debug Configuration > Enable Debug Lines
- Curve Interaction Caster had:
  - Live Cone Cast Debug Visuals
- These were temporarily enabled for debugging.

When Game view Gizmos was enabled, the cone cast became visible.
- The yellow cone cast was initially visibly offset from the actual hand/cube.
- This led to inspecting Left Hand > Aim Pose.

Aim Pose
Aim Pose contained:
- Tracked Pose Driver (Input System)
- Tracking Type = Rotation and Position
- Update Type = Update And Before Render
- Position Input = XRI Left / Aim Position
- Rotation Input = XRI Left / Aim Rotation
- Tracking State Input = XRI Left / Tracking State

Critical observation in Editor simulator:
- Aim Pose Transform stayed at 0,0,0 while moving the simulated hand.
- Therefore the simulator hand visual moved, but the OpenXR-style Aim Pose did not receive tracking.
- XRI Left / Aim Position had bindings including:
  - devicePosition [LeftHand Meta Aim Hand]
  - pointer/Position [LeftHand Hand Interaction (OpenXR)]
  - pointer/Position [LeftHand Hand Interaction Poses (OpenXR)]
  - pointer/Position [LeftHand HoloLens Hand (OpenXR)]

Input Debugger showed no simulated XR hand device.
Only normal devices were visible:
- keyboard
- mouse
- controller HID
- unsupported USB HID devices

Conclusion:
- Editor XR Interaction Simulator does not necessarily provide the same OpenXR hand aim-device path used by the Quest runtime.
- Editor far-hand behavior was therefore not a reliable indication of real Quest hand tracking.
- Decided to test directly on Quest.

Quest build results
First Quest build:
- Controllers worked.
- Cube could be grabbed with controllers.
- App initially appeared to require controllers.
- Hands did not render.

OpenXR feature inspection revealed:
- Hand Tracking Subsystem was OFF
- Meta Hand Tracking Aim was OFF

After enabling:
- Hand Tracking Subsystem
- Meta Hand Tracking Aim

New Quest build:
- Hand far rays detected the cube.
- Hand interaction worked.
- Cube could be interacted with / grabbed using real hands.
- This confirmed the far-interaction/Aim Pose issue was mainly an Editor simulator issue.

Hand rendering/debug visual issue
On Quest, hands eventually rendered but had lots of red/blue blocks and rays attached to them.

These were debug/visualization artifacts.

Hand Visualizer component:
- Meta Quest Left Hand Mesh = LeftHandQuestVisual
- Meta Quest Right Hand Mesh = RightHandQuestVisual
- Android XR hand meshes also assigned
- Draw Meshes = enabled
- Debug Draw Joints = disabled
- Velocity Prefab assigned
- Velocity Type was initially = Linear

Important final fix:
- Set:
  Hand Visualizer > Velocity Type = None

After setting Velocity Type = None:
- Red/blue velocity debug blocks/rays disappeared.
- Quest hand meshes rendered normally.
- Hand interaction still worked.

Also:
- Debug Draw Joints was already OFF.
- Enable Debug Lines and Live Cone Cast Debug Visuals were debugging tools and should remain OFF for normal builds.

Quest hand visual prefab details
LeftHandQuestVisual and RightHandQuestVisual contain:
- XR Hand Tracking Events
- XR Hand Skeleton Driver
- XR Hand Mesh Controller
- XR Hand Skeleton Poke Displacer

XR Hand Mesh Controller references:
- corresponding XR Hand Tracking Events
- corresponding Skinned Mesh Renderer
- Show Mesh When Tracking Is Acquired = enabled
- Hide Mesh When Tracking Is Lost = enabled

There was temporary confusion because the prefab root appeared disabled in the prefab asset Inspector. The final working state proves the runtime hand visual pipeline is functioning.

ADB / Quest deployment issue encountered
At one point Unity stopped showing the Quest as a valid Run Device.

Unity console showed:
- Unauthorized device detected
- please add debug authorization and reconnect

Unity's bundled adb path:
C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe

Useful PowerShell command:
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" devices

Observed states:
1. At first:
   List of devices attached
   [nothing]

2. Later:
   1WMHHA6DTF2294    unauthorized

Meaning:
- USB connection existed
- ADB saw Quest
- PC was not authorized for USB debugging

ADB restart commands:
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" kill-server

& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" start-server

Quest sometimes automatically opened Meta Horizon Link when connected over USB.

Known non-blocking warnings encountered
1. XRGazeAssistance:
   "No ray and select interactor found!"
   - Appeared multiple times.
   - Did not prevent controller or hand grabbing.
   - Likely related specifically to gaze assistance configuration.

2. XRSimulatedController haptics:
   "Failed to get haptic capabilities of XRSimulatedController..."
   - Simulator-specific warning.
   - Did not block interactions.

3. Account API:
   "Account API did not become accessible within 30 seconds..."
   - Not related to XR grabbing.

4. OpenXR Project Validation:
   Screen Space Ambient Occlusion caused a Meta Quest performance warning.
   - Not fixed yet.
   - Potential optimization task later.

Important architectural lessons from debugging
1. Do not assume Editor XR Interaction Simulator behavior exactly matches real Quest OpenXR hand tracking.
2. Near interaction and far interaction are separate pipelines:
   - Near: Sphere Interaction Caster / proximity
   - Far: Curve Interaction Caster using Aim Pose
3. If near interaction works but far interaction does not:
   - inspect Aim Pose
   - inspect Meta Hand Tracking Aim
   - inspect Curve Interaction Caster cast origin
4. If controller interaction works on an XR Grab Interactable, do not immediately modify the interactable when hands fail.
5. XRI 3.x differs substantially from older tutorials:
   - no old "Action Based Controller" creation workflow
   - old ActionBasedSnapTurnProvider is deprecated
   - locomotion uses Locomotion Mediator + XR Body Transformer + providers
6. Quest hand tracking requires actual OpenXR hand features, not just:
   - XR Hands package
   - hand prefab
   - Hand Interaction Profile
7. On Quest, ensure these are enabled:
   - Meta Quest Support
   - Hand Interaction Profile
   - Hand Tracking Subsystem
   - Meta Hand Tracking Aim
8. Debug visuals can ship into Quest builds if left enabled.
   Check:
   - Hand Visualizer > Debug Draw Joints
   - Hand Visualizer > Velocity Type
   - Interaction Attach Controller > Enable Debug Lines
   - Curve Interaction Caster > Live Cone Cast Debug Visuals

CURRENT WORKING STATE
- Meta Quest build launches.
- Controllers work.
- Controller ray hover works.
- Controller grabbing works.
- Real Quest hand tracking works.
- Hand meshes render.
- Hand far-ray targeting works.
- Hand interaction/grabbing works.
- Debug velocity blocks/rays have been removed by setting:
  Hand Visualizer > Velocity Type = None
- Basic Floor + GrabCube test scene works.

Recommended caution for future Codex changes
- Do not replace or rewrite the XR Hands rig unless necessary.
- Prefer modifying the existing XR Origin Hands (XR Rig).
- Be careful with XRI documentation/tutorials written for XRI 2.x because this project uses XRI 3.3.2.
- Do not re-add deprecated Action Based locomotion providers.
- If adding scripts that interact with XR, target current XRI 3.x APIs.
- Preserve OpenXR Meta hand features.
- Keep debugging visualization disabled in production builds.