# Compact-car WheelCollider dynamics

Scene: `Assets/Scenes/Driving Simulator.unity`. Unity 6000.3.23f1. No commits, external assets/frameworks, packages, new gameplay, reporting fields or environmental conditions.

## Previous architecture

The prototype used a 900 kg Rigidbody supported by its box chassis, four grounding raycasts, direct acceleration/braking forces, lateral-velocity cancellation and a prescribed yaw rate. X/Z rotation was frozen. Slipperiness scaled lateral force, longitudinal force and yaw recovery/angular damping. Road wheels only displayed a steering angle. This architecture could simulate a skid-like response but could not produce physical suspension, weight transfer or rollover.

## New architecture and files

`VehicleController` owns the existing controller/keyboard input, menu/focus/release gates, simple powertrain, torque/brake commands, reset and existing telemetry-facing properties/events. New `WheelVehicleDynamics` owns Rigidbody configuration, four WheelColliders, springs/dampers, Ackermann steering, tire curves/load sensitivity, combined-slip budget, anti-roll bars, road loads and road-wheel visual synchronization.

WheelCollider replaces **all** custom lateral tire forces and prescribed yaw velocity. The only additional chassis forces are aerodynamic drag, rolling resistance and equal/opposite suspension anti-roll forces. There is no upright torque, countersteering assistance, roll trigger, lateral-velocity cancellation, yaw cap or moving-vehicle velocity clamp. Pitch/roll/yaw rotations are unconstrained. Reset is the only code that assigns a zero angular/linear velocity or restores a vehicle rotation.

Created:
- `Assets/Scripts/Driving/WheelVehicleDynamics.cs` and metadata.
- `Assets/Driving/Meshes/Sedan Body Paint.asset` and metadata, extending only exterior panels to match the wheelbase/body envelope.
- This guide.

Modified in this milestone:
- `Assets/Scripts/Driving/VehicleController.cs`.
- `Assets/Scripts/Driving/SteeringWheelVisual.cs`: retains cockpit wheel animation, removes the old road-wheel transform writer.
- `Assets/Driving/Prefabs/Driving Vehicle.prefab`.
- `Assets/Scenes/Driving Simulator.unity`.

Pre-existing uncommitted telemetry/cockpit work was preserved. No other scenes, puzzle/digital-twin systems, XR assets, input bindings, packages, OpenXR settings, timestep, project gravity, render settings or track geometry were changed. Environment/scenario/menu/telemetry/dashboard/summary scripts retain their interfaces and logic.

```
Driving Vehicle (Rigidbody, VehicleController, WheelVehicleDynamics, existing integrations)
  Center Of Mass
  Vehicle Body (revised chassis BoxCollider)
  Cabin Collision (BoxCollider matching the retained cockpit/frame)
  WheelCollider_FL / FR / RL / RR
  Wheel FL / FR / RL / RR (separate visual roots)
  Visual Body / Cockpit Interior / Body Paint (extended exterior mesh)
  Driver Seat Anchor (unchanged)
  XR Vehicle Space / XR Origin (unchanged)
```

## Mass, geometry and inertia

| Setting | Authored value |
| --- | --- |
| Rigidbody mass | 1350 kg |
| Gravity | Existing (0, -9.81, 0) m/s^2 |
| Wheelbase / track | 2.70 / 1.55 m |
| Wheel radius / width | 0.32 / 0.21 m |
| Axle centers | z = +/-1.35 m; x = +/-0.775 m |
| Front/rear static sprung-mass allocation | 60% / 40% |
| Each front/rear sprung mass | 405 / 270 kg |
| Each WheelCollider wheel mass | 20 kg (rotating-wheel model) |
| Nominal body-origin height above road | 0.42 m |
| Nominal CG height above road | 0.53 m |
| Local CG | (0, 0.11, 0.27) m |
| SSF | 1.55 / (2 * 0.53) = **1.4623** |
| SSF*g reference | **14.3448 m/s^2**, about 1.46 g |
| Inertia mass envelope | width 1.80, height 1.43, length 4.48 m |
| Box-derived inertia X/Y/Z | 2487.97 / 2622.42 / 594.55 kg*m^2 |
| Linear/angular damping | 0 / 0.05 |
| Collision detection / interpolation | Continuous Dynamic / Interpolate |
| Rotation constraints | None |

The old exterior was only about 3.2 m long; its painted panels alone are extended to 4.48 m and approximately 1.79 m wide. Tires shrink from 0.40 to 0.32 m radius and move to the new axle positions. The cockpit, steering wheel, seat anchor and XR hierarchy are not globally scaled or moved. The requested 1.43 m height is the mass/inertia envelope, **not** a claim that the retained tall VR windshield frame is 1.43 m high: the existing visual frame reaches about 2.03 m above the road, and its cabin collision envelope reaches about 1.97 m. This preserves the verified first-person cockpit while matching its actual rollover contact envelope. Mass properties remain those of a low-CG compact car; this is not a dimensionally exact production sedan replica.

The Center Of Mass reference is positioned by the exposed CG-height and front-load-fraction fields during Configure/Awake. Change those fields to tune it; hand-moving the reference is overwritten. The inertia estimate is explicit rather than accidentally derived from the tall lightweight cockpit box. No artificially lowered CG or frozen rotation is used.

## Suspension and anti-roll bars

Spring rate: k=(2*pi*f)^2*m_sprung. Damper: c=2*zeta*sqrt(k*m_sprung).

| Setting | Front | Rear |
| --- | ---: | ---: |
| Natural frequency | 1.55 Hz | 1.65 Hz |
| Spring rate, each wheel | 38,413 N/m | 29,019.6 N/m |
| Damping, each wheel | 6,705.26 N*s/m | 4,758.57 N*s/m |
| Damping ratio | 0.85 | 0.85 |
| Travel | 0.24 m | 0.24 m |
| Target position | 0.5 | 0.5 |
| Anti-roll stiffness | 6,000 N/m | 3,500 N/m |

WheelCollider's loaded rest position is centered in its travel. Mounts are placed at wheelRadius - nominalRideHeight + travel*(1-target), about y=0.02 m in local space. An initial full-extension target was rejected by measured ride-height/compression checks. The retained setup settles at about 0.419 m origin height, 0.529 m CG and 0.1189 m compression. The measured front share of WheelHit loads is about 60.7%; WheelHit force samples are diagnostic solver outputs, not a calibrated axle-scale measurement.

Anti-roll force is proportional to left/right compression difference and applied upward at the compressed side and downward at the opposite side. It only acts when both axle wheels contact the road, and is limited to half the lesser reported contact load. It transfers suspension load without applying an artificial global upright torque. Tire/suspension force application height is 0.05 m above nominal tire base, preserving the moment arm below the CG. Acceleration squat, braking dive, cornering load transfer and body roll result from Rigidbody/contact forces. Visual compression/rebound uses native wheel poses.

## Tires, combined traction and slipperiness

| Curve property | Forward | Sideways |
| --- | ---: | ---: |
| Extremum slip | 0.12 | 0.18 |
| Extremum value | 1.0 | 1.0 |
| Asymptote slip | 0.60 | 0.70 |
| Asymptote value | 0.75 | 0.75 |
| Base stiffness | 0.95 | 0.90 |

Sideways front/rear balance factors are 0.98/1.03, providing mild FWD understeer bias. A modest load-sensitivity exponent of 0.12 reduces coefficient as load rises relative to each wheel's nominal axle load. Curve values are WheelCollider tuning coefficients, not a measured tire data set.

A lightweight combined-slip budget evaluates normalized demand from the last native forward/sideways slip sample. When sqrt(longitudinalDemand^2+lateralDemand^2)>1, both stiffnesses scale by its reciprocal. At simultaneous normalized peaks the scale is about 0.707. This avoids deliberately allocating full independent peak grip to both axes. It is a **slip-based approximation**, not a guaranteed instantaneous force-circle clamp: WheelCollider does not expose separate tire-force components for exact force clipping, and sampling is one physics step behind. No extra lateral force is added.

The environment still calls VehicleController.SetRoadSlipperiness. It now maps to:

`gripMultiplier = 0.08 + 0.92 * (1 - slipperiness)^1.5`

| Slipperiness | Grip multiplier |
| --- | ---: |
| 0% | 1.0000 |
| 50% | 0.4053 |
| 80% (Winter Storm) | 0.1623 |
| 90% (Icy Road) | 0.1091 |
| 100% | 0.0800 |

Both forward and sideways tire stiffness change at the next fixed step; neither becomes zero. The old lateral-force multiplier and slipperiness-dependent yaw damping are removed. Visibility/scenario values/Custom behavior remain unchanged. Ordinary PhysicsMaterial friction is used only for chassis contacts, **not** as the tire-grip model.

Native GetGroundHit forwardSlip/sidewaysSlip are available per wheel, along with average absolute front/rear sideways slip, maximum sideways slip, average forward slip, suspension compression and reported contact load. VehicleController also exposes longitudinal/lateral acceleration and yaw rate. These are read-only diagnostics with no new permanent UI/report fields or scoring. Front-dominant slip indicates understeer; rear-dominant slip indicates oversteer. Sustained rear slip while moving can produce a drift, and severe rear loss can spin. The default front-driven compact car is not tuned to guarantee sustained power-oversteer like a rear-driven drift car.

## Steering, torque and braking

Steering input responds at 1.5 normalized units/second. Center steering limit varies smoothly from 32 degrees at low speed toward 10 degrees at 30 m/s. Ackermann computes R=L/tan(centerAngle), inner=atan(L/(R-T/2)), outer=atan(L/(R+T/2)); the inner front wheel turns farther. Thus actual inner angle may exceed the center-angle limit. Unsafe high-speed demands still exceed tire adhesion and can skid; there is no prescribed yaw-rate cap.

The two front wheels each receive up to 700 N*m. Total delivered wheel power is limited to 60 kW, reducing torque with driven-wheel peripheral speed. A smooth speed/rev governor tapers torque over the final 5% before the nominal 30 m/s forward limit. Wheel speed participates so an icy wheel cannot accumulate unlimited spin energy from a body-speed-only constant-torque source. This is a simplified fixed-ratio torque/power model, with no full transmission or slip-triggered traction control. Reverse is front-driven at up to 200 N*m per wheel with a nominal 2 m/s governor and the existing deliberate 0.4-second hold transition. Momentum from slopes/collisions is not hard-clamped.

Service braking is 4,600 N*m total with 65% front bias: 1,495 N*m per front wheel and 805 N*m per rear. Idle engine braking is 15 N*m on each front wheel when motor torque is zero. Brakes consume native tire grip and can lock wheels. There is no ABS or yaw stabilizer. Input blocking for menu/focus/release still commands brakes, and does not command reverse or count as a driver brake event.

Aerodynamic drag uses air density 1.225 kg/m^3 and Cd*A=0.66 m^2. Rolling resistance coefficient is 0.012, applied only with ground contact, with a low-speed overshoot bound. Neither replaces tire lateral forces.

## Substeps, collision geometry and rollover

Project Fixed Timestep stays **0.02 s**, gravity and solver settings unchanged. ConfigureVehicleSubsteps is called once for the vehicle: **5 substeps below 5 m/s, 3 above**. There are four native ground-hit samples per physics step, cached arrays, no grounding raycast loop, no growing runtime history and no per-frame logging.

The old chassis box is shortened vertically and extended longitudinally: size (1.7, 0.5, 4.35) m, center (0, -0.02, 0), giving about 0.15 m ground clearance at rest. A second box matches the existing cabin/frame for body-side/roof contact when tipped; it is not a wheel or suspension support. Old ground-probe objects are removed. Existing rig-collision exclusions still cover the vehicle colliders. Road-wheel visuals use GetWorldPose for steering, rotation and travel, with the old steering-only writer removed.

The existing Test Surface is one smooth, thick box. Perimeter barriers and infield boundaries are unchanged, retaining their deliberate trip geometry; the permanent track was not rebuilt. Continuous Dynamic collision detection remains enabled for the chassis.

SSF*g is a validation reference only. In the aggressive flat dry test, v*yaw gave about 5.46 m/s^2 versus the 14.34 m/s^2 static tipping reference, with front breakaway and about 2.24 degrees of body tilt. Tires slide before rolling in that test. A temporary 0.30 m curb struck with 15 m/s lateral speed produced about 101.9 degrees peak tilt and a vehicle resting on its side. No rollover impulse, angle trigger or forced rotation was used during the trip maneuver. Not every higher-speed curb encounter rolls: contact timing and wheel stepping change the outcome. Reset was separately tested from a seeded 155-degree overturned state.

The XR vehicle space inherits all physical pitch/roll/yaw. Head tracking retains its local pose. There is no headset stabilization, extra camera roll or shake. Existing seat calibration runs as before.

## Validation and observations

Temporary Play Mode checks passed **701 assertions**, including structural checks, with zero failures. Most assertions check missing scripts; the physics/integration assertions and measured traces are in `Logs/Dynamics-Checks.txt`.

Measured on a temporary flat pad (never saved):
- Configured mass 1350 kg, four grounded wheels, measured CG about 0.529 m, static front load share about 60.7%, compression 0.1189 m.
- Front/rear acceleration load transfer; about 0.50 degrees nose-up during acceleration, 1.31 degrees nose-down during braking.
- Forward speed after six seconds about 15.46 m/s. Sustained full-throttle test about **28.72 m/s (103.4 km/h)**, peak telemetry about 28.93 m/s; nominal governor remains 30 m/s. Surface slip/rev limiting and road loads make realized speed lower than the old hard clamp.
- Reverse remained below the nominal 2 m/s limit; measured about 1.52 m/s after six seconds of held reverse.
- Matched rolling starts about 19.5 m/s: dry stop **21.54 m / 2.22 s**; 100% ice **229.79 m / 23.34 s**, to below 0.5 m/s. No ABS; slip approached 0.99 during lock.
- Ordinary dry turn maximum tilt about 0.60 degrees; aggressive dry turn about 2.24 degrees, front/rear peak sideways slip about 0.325/0.121.
- Severe rear-traction test used a **temporary rear balance of 0.25** versus shipped 1.03: rear-minus-front slip exceeded 0.60 during breakaway and yaw reached about 1.79 rad/s. This validates a rear-loss capability, not a claim that the uniform road slider selectively makes rear tires icy.
- From the same moderate yaw disturbance, a short countersteer pulse then center reduced final yaw to about 0.009 rad/s and lateral velocity to 0.098 m/s, versus about 0.301 rad/s and 1.392 m/s while steering into the spin.
- Curb-trip tipping, chassis collision telemetry, native hard-brake event detection, reset from overturn, telemetry preservation, wheel visual synchronization, dashboard conversion, scenario values/Custom, controller menu opening/input gating, retained head-local pose, and one XR Origin/Interaction Manager/EventSystem/XRUIInputModule passed.

These are deterministic editor experiments, not calibration against measured production-car tire data or proof that every maneuver will drift/roll. Device comfort, live controller/hand targeting and sustained Quest CPU performance remain manual checks.

Temporary helpers are archived under `.utmp/dynamics-validation/` and removed from Assets/player compilation. Logs: `Logs/Dynamics-Setup.log`, `Logs/Dynamics-Checks.log`, `Logs/Dynamics-Checks.txt`. Test pad/curb/input overrides/rear-grip tuning were never saved. Existing headless XRSessionSubsystem and gaze-assistance warnings and licensing entitlement/access-token diagnostics appeared; no new gameplay systems were added.

## Quest 3 manual test matrix

| Test | Exact setup/action | Expected observation |
| --- | --- | --- |
| Baseline / tracking | Launch Driving Simulator, Normal Driving, grips/stick released; use usual seated recenter. Lean/look around. | Vehicle settles on four wheels, dashboard near zero, original eye/hand tracking; no floor penetration or continuing suspension bounce. |
| Suspension | Accelerate gently, release, then brake. Repeat a moderate steering arc. | Small physical squat/dive and body roll relative to road; motion settles after input release; cockpit/head local tracking remains independent. Wheel poses follow suspension in Editor inspection if needed. |
| Normal dry grip | Normal, gradual right grip, modest left-stick steering at low/moderate speed. | Predictable grip and stable chassis. Normal arcs skid well before a flat-road rollover; no artificial upright lock. |
| Speed / dashboard | Accelerate progressively on available straight sections, brake before tight corners. | Dashboard live km/h; menu Max Speed retains peak. Nominal governor 108 km/h, measured long-pad level speed about 103-104 km/h. The permanent compact course is unchanged. |
| Ackermann | At low speed, steer left/right and inspect front wheel orientation in Editor if necessary. | Inside wheel turns more; visuals also rotate and move through suspension travel. High-speed steering reduces progressively but does not silently suppress aggressive commands. |
| Dry hard stop | Normal, establish a repeatable straight-line starting speed and landmark; full left grip. | Strong forward weight transfer, possible wheel lock/skid; one brake event per continuous application. Release/reapply gives another event. Compare summary counts. |
| Combined traction | Repeat that stop while holding a moderate turn, with ample room. | Less cornering authority / greater front slip under braking; no independent full-strength cornering plus braking. |
| Ice stopping | Repeat same starting speed/landmark at 90% then 100% slipperiness. | Much longer stopping distance and severe slip. Tire friction remains nonzero. Straight-line stopping may need substantially more space than the course provides. |
| Understeer | Normal, progressively increase corner entry speed/steering. | Front slips/plows outward when demand exceeds adhesion; reducing speed/steering restores grip. Use front/rear slip properties in Editor for numerical inspection, no permanent UI added. |
| Oversteer / recovery | Slipperiness around 50-80%; moderate speed, a brief turn followed by lift/brake or opposite-steer transient. Countersteer briefly then center as yaw recovers. | Rear slip and yaw may develop; moderate disturbances can recover. FWD is understeer-biased, so sustained power drifting is not guaranteed. Avoid interpreting a full constant countersteer hold as a recovery pulse. |
| Severe skid / spin | Icy Road or Winter Storm, progressively harder cornering/braking and excessive/incorrect correction. | Physical lateral skid and potentially unrecoverable spin while unsafe inputs continue; no scripted yaw stabilization. Reducing slip can restore grip. |
| Trip / rollover | After ordinary tests, approach existing raised infield/perimeter geometry during a significant sideways skid. | A contact can trip the body and tip/roll it; outcomes depend on speed/direction/contact. Flat dry ordinary cornering should not suddenly flip. Cockpit and XR space physically roll together. |
| Overturned reset | While tipped/overturned, select existing Reset Vehicle with controller UI or hands. | Upright spawn pose, zero motion, suspension settles; tracked head-local pose retained; dashboard returns to zero. Scenario, environment and telemetry totals remain. |
| Scenarios / Custom | Select all four presets, then move each environment slider manually. | Normal 0/0; Dense Fog 80/0; Icy Road 0/90; Winter Storm 70/80; manual changes select Custom. Friction changes without replacing suspension or resetting session. |
| Telemetry / summary | Drive/brake/collide/leave the marked road, change scenario, then End Session. | Speed/max/distance/brakes/collisions/off-road/steering/scenarios remain sensible; summary freezes. New/Reset Session clears telemetry without changing car pose/conditions; dashboard remains live after End. |
| Menu / controls | Open controller menu while slowly moving/turning, close/release inputs, reopen through palm. Repeat with controller ray, pinch and poke. | Anchoring remains stable, menu applies tire brakes without commanding reverse or counting a driver brake press. On ice, physical stopping still requires traction/distance. |
| Regression / performance | Toggle passthrough, volume; switch controllers/hands, system menu, suspend/resume. | Original behavior and input release gates; no duplicate rig/UI systems, stable Quest frame rate. Check suspension stability and controller/hand targeting on device. |

## Primary implementation references

- [Unity Wheel collider component reference](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/class-wheel-collider): native suspension, slip curves and force-application settings.
- [Unity Wheel collider friction](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/collision-section/collider-shapes/wheel-colliders/friction): explicit tire-friction curves rather than ordinary PhysicsMaterial friction.
- [Unity ConfigureVehicleSubsteps](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/wheelcollider/configurevehiclesubsteps): per-vehicle suspension/tire substeps.

Final Unity 6000.3.23f1 compilation/import after helper removal: **exit 0**, no C# compilation errors or C# warnings, without `-noUpm`. Log: `Logs/Dynamics-Final-Compile.log`. Licensing access-token/entitlement diagnostics and a Curl 42 callback-aborted shutdown message did not prevent success. Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "X:\Capstone\XR_Capstone" `
  -logFile "X:\Capstone\XR_Capstone\Logs\Dynamics-Final-Compile.log"
```
