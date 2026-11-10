# Robot Architect

A standalone Unity XR application for Meta Quest 3, using Unity **6000.3.23f1**.
Open `Assets/RobotArchitect/Scenes/RobotArchitect.unity`.

The current application contains a fixed workstation, input/output zones, a visual
robot arm placeholder, a stationary conveyor placeholder, and a grabbable TestCube
with grab color feedback. The existing XR Hands rig supports hand interaction;
the palm menu provides open/close, volume, and passthrough controls.

Robot programming, inverse kinematics, animated robot joints, moving conveyors,
and automated assembly gameplay are not implemented.

Use the existing **Meta Quest** Android build profile. The global build scene list
contains only RobotArchitect. Preserve the existing OpenXR and Meta hand tracking
settings, XRI 3.x infrastructure, and locally restored XR samples.

- [Scene and validation guide](Assets/RobotArchitect/README.md)
- [XR setup and troubleshooting](PROJECT_CONTEXT.md)
- [Hand menu and passthrough](HAND_MENU.md)
- [Sample restoration and source reconstruction](RECONSTRUCTION.md)
- [Security safeguards](SECURITY.md)
- [Cleanup dependency analysis and validation](CLEANUP_REPORT.md)

Do not treat Editor validation as Quest hardware testing. Recheck near/far hand
grabbing, menu poke/ray input, passthrough, recenter, and suspend/resume on Quest
when making future XR changes.
