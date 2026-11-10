# Dashboard speed, higher cap and cockpit remodel

Scene: `Assets/Scenes/Driving Simulator.unity`. Vehicle prefab: `Assets/Driving/Prefabs/Driving Vehicle.prefab`.

## Changes in this milestone

Created:
- `Assets/Scripts/Driving/DashboardSpeedometer.cs` and metadata.
- `Assets/Driving/Meshes/` and metadata: eight baked mesh assets (Cockpit Body Paint, Cockpit Dashboard and Side Panels, Cockpit Instruments and Vents, Cockpit Metal Details, Cockpit Seats and Bolsters, Cockpit Warm Trim, Steering Dashboard and Side Panels, Steering Metal Details).
- Four shared URP Lit materials and metadata in `Assets/Driving/Materials/`: Instrument Black, Interior Charcoal, Interior Upholstery, Interior Warm Grey.
- This guide.

Modified:
- `VehicleController.cs`: default cap and acceleration/braking tuning only in this milestone; physics algorithm unchanged. Previous telemetry properties/events remain intact.
- `DrivingTrainingUI.cs`: menu maximum-speed binding and km/h presentation conversion for summary speeds.
- Driving Vehicle prefab: same tuning plus remodeled visual body, retained steering-wheel transform/animation reference, and reusable dashboard display.
- Driving Simulator scene: training-menu initial text, tuned vehicle instance, and existing instruction-label placement.
- `DRIVING_TELEMETRY.md`: current display-location documentation.

Existing uncommitted telemetry milestone files were preserved. No commits. SampleScene, Digital Twins, puzzles, digital-twin systems, input assets, packages, OpenXR settings, XR rig, hand-menu anchoring logic, environment/scenario logic and telemetry accumulation logic are unchanged.

## Speed and handling

| Parameter | Before | After |
| --- | --- | --- |
| Maximum forward speed | 6 m/s (21.6 km/h) | 30 m/s (108 km/h), exactly 5x |
| Maximum reverse speed | 2 m/s (7.2 km/h) | 2 m/s (7.2 km/h) |
| Acceleration | 1.5 m/s^2 | 4.5 m/s^2 |
| Braking force parameter | 4 m/s^2 | 12 m/s^2 |

Threefold acceleration/braking improves usability at the higher cap without rewriting the vehicle model. Throttle smoothing, reverse delay, input release gating, rolling resistance, grip and slipperiness curves, steering angle/responsiveness, 30 degrees/s yaw limit, wheelbase, Rigidbody constraints/interpolation and continuous collision detection remain unchanged. Reverse still uses the existing shared acceleration parameter with its unchanged low speed cap. Brake early for the existing tight loop corners; the course dimensions are unchanged.

Telemetry remains in SI units internally. All speed presentations now use km/h: dashboard current speed, menu session maximum, summary average and maximum. Strongest collision retains its explicitly labeled m/s impact measurement.

## Dashboard and menu

`DashboardSpeedometer` observes the existing `VehicleController.HorizontalSpeed`, including reverse and sideways motion, and converts with speed * 3.6. This remains live when telemetry is ended/frozen. It caches references, checks at 10 Hz, and changes the TMP number only when the rounded integer changes; it performs no scene searches or runtime object creation. The display is a 0.34 x 0.15 m noninteractive world-space canvas with a large numeric TMP readout, CURRENT SPEED heading and km/h label. It adds no raycaster or input system. Colors use pale mint/white on a black instrument recess.

The instrument anchor is at vehicle-local (-0.3, 0.895, 0.525), tilted 12 degrees toward the seated driver. The existing seat anchor stays exactly (-0.3, 1.05, -0.35). The wheel moves slightly down/back to (-0.3, 0.72, 0.17), keeping the readout above the wheel rim and below the forward sightline. No XR camera or seat-calibration code/pose is changed.

Training Session now shows Time, Distance, Collisions and Max Speed (one decimal km/h), retaining the 5 Hz visible-page refresh rate. It contains no current-speed row. Summary average/max values convert from telemetry m/s to km/h without changing recorded data. End/Close/New/Reset Session behavior remains unchanged. Reset Vehicle clears the live dashboard speed when the car stops but retains session maximum and all accumulated telemetry. New/Reset Session clears the menu maximum without forcing dashboard speed to zero if the car is moving.

## Interior and hierarchy

```
Driving Vehicle                         (existing Rigidbody, colliders, controllers)
  Driver Seat Anchor                    (unchanged)
  XR Vehicle Space / XR Origin          (unchanged)
  Visual Body
    Cockpit Interior                    (six baked meshes grouped by material)
      Body Paint
      Dashboard and Side Panels
      Warm Trim
      Seats and Bolsters
      Metal Details
      Instruments and Vents
    Steering Wheel                      (retained animated transform, two baked meshes)
    Dashboard Instrument Anchor
      Dashboard Speedometer             (Canvas, DashboardSpeedometer)
        Speed Label
        Speed Value
        Speed Units
  Wheel FL / FR / RL / RR                (unchanged)
  Driver Controls                       (existing empty canvas anchor, preserved)
  Driving Controls                      (existing instruction text, repositioned)
```

The verified Reset Vehicle button remains in its existing hand-menu location with its original callback. No replacement button was created.

The remodel adds a layered chamfered dashboard, instrument shroud, inset fascia, simple slatted vents, low center console/armrest, shaped driver and passenger seats with cushions/backrests/headrests/bolsters, interior side panels, narrow windshield pillars/header and side roof rails. A rounded low-polygon rim, three spokes, hub and steering-column shape replace the old segmented wheel. The driver seat is centered on the unchanged XR eye anchor. The windshield remains open, with no transparent glass or mirrors. No doors, interactions, gauges, new lights, textures or external packages/assets were added.

Geometry is built from simple beveled cuboids/beams and a 32-by-6-segment steering torus, baked at authoring time and merged by material into eight mesh renderers. Existing body-paint and metal materials are reused alongside four small shared flat-color URP Lit materials. Interior renderers do not cast/receive shadows or use reflection/light probes. There are no new cockpit colliders, physics bodies or per-frame geometry work. The chassis collider, ground probes and wheel physics remain unchanged.

## Quest 3 manual tests

1. Launch Driving Simulator with the usual seated/recenter setup. With menu closed, confirm CURRENT SPEED / 0 / km/h appears above the steering wheel. Look straight ahead and slightly down: instrument text, lane markings and distant road should remain readable. Lean/look around normally and check unchanged head tracking.
2. Accelerate gradually on a straight. Dashboard speed should rise promptly and fall during braking/coasting. Reverse: the positive speed magnitude should stay at or below about 7 km/h. Check that hands/controllers remain visible around the wheel/side panels and do not appear trapped by the visual geometry.
3. Open Training Session: Time, Distance, Collisions and Max Speed appear, with no current-speed row. Reach a higher speed, slow down and reopen: Max Speed retains the peak while the dashboard shows the slower live speed. Dashboard must continue updating with the hand menu closed.
4. Test increased acceleration and braking progressively. On the compact course, brake early before tight corners and barriers. Verify steering remains predictable, no pitch/roll or unintended locomotion occurs, and the existing grip-release gating still works. The 108 km/h cap is validated on a temporary long straight by the automated test; no enlarged course is saved.
5. End a session: summary average/max should match the recorded values converted to km/h. Close keeps telemetry ended; drive slowly again and verify the dashboard still changes while menu totals remain frozen. New/Reset Session clears session maximum and retains scenario/car pose; dashboard continues reflecting actual motion.
6. Accumulate telemetry, select Winter Storm, then use the existing Reset Vehicle button. Confirm spawn/seat reset, dashboard returns to zero, session maximum/distance/collisions remain, and scenario/70% visibility/80% slipperiness remain. Repeat reset with hands and controllers.
7. Open the controller menu while slowly driving/turning: stable controller anchoring, full tab/button reachability, original propulsion blocking. Put controllers down and test palm launcher/open/close, far pinch and near poke. Check new pillars/panels do not obstruct the rays or hand view.
8. Test every scenario, both environment sliders and Custom synchronization, passthrough, volume, session End/Close/New/Reset and suspend/resume. Inspect dashboard under fog and passthrough. Assess the new interior from normal seated posture, including side view, and confirm readable text and comfortable frame rate on Quest 3.

## Validation results

Temporary Play Mode validation passed **490 assertions**, including structural/no-missing-script checks. Exactly one XR Origin, XR Interaction Manager, EventSystem and XRUIInputModule were present. Coverage included dashboard km/h conversions at multiple speeds and reverse, live updates after End Session, menu maximum-only binding and retention while slowing, unchanged seat anchor/chassis collider, no cockpit colliders, actual virtual-keyboard-driven acceleration to 30 m/s on a temporary long straight, unchanged 2 m/s reverse cap, distance/max/average telemetry, hard braking, reset/snapshot/scenario preservation, slider synchronization on reopening, actual controller-menu opening/tracking-space parenting/across-reset anchoring, steering and upright constraints, and retained steering-wheel animation reference.

Measured actual cap: **30.0 m/s**. Telemetry distance **186.8298 m** versus actual straight displacement **186.8444 m** during the acceleration test. Full braking from 30 m/s reached **5.500066 m/s after two seconds** in Normal Driving. The temporary long ground collider and test inputs were never saved.

Art budget: **4,160 triangles and eight MeshRenderers** for Visual Body, including remodeled cockpit/body/steering wheel; road-wheel meshes and UI are additional existing elements. Eight mesh assets and four new shared materials. No external art/packages. First-person renders from the unchanged eye anchor were visually reviewed with a clear windshield view and unobscured speed display/instructions.

Results: `Logs/Cockpit-Checks.txt`, `Logs/Cockpit-Checks.log`. Previews: `Logs/Cockpit-FirstPerson.png`, `Logs/Cockpit-Dashboard.png`, `Logs/Cockpit-LiveSpeed.png`. Helpers archived under `.utmp/cockpit-validation/` and removed from Assets/player compilation. Existing headless XRSessionSubsystem/gaze-assistance warnings and licensing access-token/entitlement diagnostics appeared. Quest device comfort, real hand/controller targeting, passthrough and sustained performance require the manual checks above.

Final Unity 6000.3.23f1 batch compilation/import after helper removal passed with **exit 0**, no C# compilation errors and no `-noUpm`. Log: `Logs/Cockpit-Final-Compile.log`. The log includes licensing diagnostics and a Curl 23 output-destination message during shutdown; neither prevented successful completion. Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "X:\Capstone\XR_Capstone" `
  -logFile "X:\Capstone\XR_Capstone\Logs\Cockpit-Final-Compile.log"
```
