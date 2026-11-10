# Driving training telemetry

Scene: `Assets/Scenes/Driving Simulator.unity`. Runtime only: no scoring, grading, AI, charts, persistence, network upload or new environmental conditions.

## Files and ownership

Created runtime scripts (with Unity metadata):
- `Assets/Scripts/Driving/DrivingSessionSummary.cs`: serializable value snapshot containing duration, distance, current/max/average speed, braking/hard-braking counts, collision count/strongest collision, off-road time, average/max absolute steering, starting/ending scenario and scenario-change count.
- `Assets/Scripts/Driving/DrivingTelemetryManager.cs`: central session state, accumulation, thresholds, completed snapshot and lifecycle events.
- `Assets/Scripts/Driving/VehicleCollisionTelemetry.cs`: vehicle collision-enter observer forwarding meaningful impacts.
- `Assets/Scripts/Driving/RoadSurfaceTracker.cs`: reusable closed-polyline road-corridor membership test, with selected-object gizmos.
- `Assets/Scripts/Driving/DrivingTrainingUI.cs`: tab selection, 5 Hz visible live statistics, summary rendering and lifecycle button wiring.

Modified `VehicleController.cs` only to expose read-only driver input, horizontal Rigidbody speed, physics position and a VehicleReset event. Existing force calculations, input decisions, traction, limits and seat calibration are unchanged. Modified only Driving Simulator among scenes. No shared menu, environment, scenario, passthrough, volume, prefab, package, input or XR configuration changes. No puzzle or digital-twin dependencies. No commits.

## Session lifecycle

The manager starts automatically after DrivingScenarioManager has initialized Normal Driving (execution order 200). `StartSession()` and `ResetSession()` clear values and start a new session at the currently selected scenario. Neither changes the vehicle or environment. SessionStarted hides any old summary. `EndSession()` is idempotent, freezes statistics and publishes a copy through SessionEnded. CompletedSummary is a value snapshot; callers cannot mutate the manager by editing their copy. No growing sample history is retained.

Reset Vehicle keeps the session and all accumulated values. Its notification only clears deceleration and brake-latch history, preventing a reset-induced speed drop from being classified as hard braking. Velocity integration never includes teleport distance. Repeatedly resetting the session does not accumulate listeners or duplicate UI systems.

Duration excludes application suspension/lost focus. Physics observations and collisions are also ignored during suspension/lost focus. Disabling the manager stops observation until it is enabled again. Ending telemetry does not stop vehicle physics or reset conditions.

## Exact metric definitions and Inspector defaults

| Metric | Definition |
| --- | --- |
| Duration | Sum of unscaled foreground Update seconds while the session is active, including menu and stationary time. Frozen by EndSession. |
| Current speed | Magnitude of Rigidbody velocity projected onto the horizontal plane, in m/s. Includes forward, reverse and sideways drift. Dashboard shows live km/h = m/s x 3.6; the hand menu shows session maximum km/h. Frozen at the last observation when ended. |
| Max speed | Highest horizontal speed observed at fixed-physics cadence during this session. |
| Distance | Sum of horizontal speed x fixed timestep, starting at the first sample at or above **0.2 m/s**. Tiny motion before this threshold is ignored. Afterwards all motion, including reverse and drift, contributes. No transform-position deltas. |
| Average speed | Distance divided by fixed-step seconds since that first meaningful movement sample. Pre-movement idle is excluded; later stops remain included. Zero before meaningful motion. |
| Brake events | One driver brake/reverse control press crossing **0.25**, while forward speed is at least **0.2 m/s**. Rearms only at or below **0.1**. Holding a press, reverse-only input, automatic menu/focus braking and reset release-gating do not add events. |
| Hard brake events | At most one per counted brake application, when consecutive horizontal speed samples decelerate by at least **3 m/s^2**, while driver brake input remains above the release threshold. Resets invalidate the previous-speed sample. No traction/physics coefficients are changed. |
| Steering | Time-weighted average absolute driver steering command and largest absolute command, normalized 0..1, over all fixed-step samples including idle. Automatic input blocking reads as zero. This is input activity, not a quality score. |
| Collisions | Collision-enter with a side-contact normal (abs(normal.y) < **0.7**) and normal-relative impact speed at least **0.8 m/s**. A global **0.75 s** cooldown merges chatter and simultaneous/multiple collider impacts into one event. No CollisionStay counting. |
| Strongest collision | Maximum qualifying contact-normal relative speed in m/s, including stronger impacts merged within the cooldown. Not impulse or a damage score. |
| Off-road time | Fixed-step seconds when the vehicle's Rigidbody reference position is outside the configured road corridor, even while stationary. No speed-based inference. |
| Scenario | Starting and current/ending enum plus a count of actual enum transitions. Re-selecting the same preset is not a transition. Custom via slider events is recorded. After End, these freeze with the other summary fields. |

Movement, brake press/release, hard-deceleration, impact and cooldown thresholds are Inspector-editable on Driving Training Session. Road width/height/path are Inspector-editable on Training Road Corridor. UI refresh interval defaults to 0.2 s; summary distance defaults to 1.1 m.

## Road detection

The existing scene has one large physical asphalt collider and a visible rounded rectangular loop of lane dashes. The entire asphalt collider is not treated as valid road, because that would classify its shoulders/infield as road. Training Road Corridor contains the exact 52-point centerline used to author the visible lane-dash loop, relative to Driving Test Area. Valid road is within **6 m** of any centerline segment in local XZ (a 12 m corridor), within **3 m** vertically. Its four straight centerlines are x = +/-28 and z = +/-38 with radius-12 corners centered at (+/-16, +/-26).

Spawn (-28, 0.42, -24), straights and rounded corners are on road. The central infield, outer shoulders (e.g. x = -36 on the west straight) and points above vertical tolerance are off road. This detects the vehicle reference point, not wheel overlap. It uses bounded vector math over 52 segments at physics cadence, without allocations, triggers, scene searches, added physical colliders or raycasts. Ground contact with Test Surface is explicitly ignored by collision telemetry even on an off-road shoulder.

## UI hierarchy and behavior

```
Hand Menu Canvas / Panel                 (existing 420 x 730 footprint)
  Driving Settings Page                 (existing controls, unchanged coordinates)
    passthrough, volume, conditions, presets
  Settings                              (tab)
  Training Session                      (tab)
  Training Session Page                 (initially inactive)
    Training Session Heading
    Current Session Statistics          (time, distance, collisions, maximum speed)
    End Session
    Reset Session
  Close Button                          (existing)

Driving Training Session
  DrivingTelemetryManager
  DrivingTrainingUI

Training Summary Canvas                 (initially inactive; existing XR UI pipeline)
  Panel
    Training Session Complete
    Session Summary                     (scenario start/end/change count and all required metrics)
    New Session
    Close
    Session State Hint
```

The tabs avoid enlarging the verified hand menu. Existing controller anchoring and palm tracking are unchanged. The summary is a separate world-space canvas with TrackedDeviceGraphicRaycaster, using the existing camera/EventSystem/XRUIInputModule. On End, the hand menu closes and the summary captures a level pose 1.1 m ahead of the head. It stays fixed relative to the vehicle tracking space, follows vehicle travel/reset and does not chase head movement. The canvas is 0.72 x 0.76 m. Live text updates only when the Training page is visible, at 5 Hz; summary text is built once per End event.

Close hides the summary and keeps telemetry ended. New Session starts fresh telemetry, hides the summary, and preserves the current vehicle pose and scenario. Reset Session in the hand menu does the same. Settings are initially selected. Returning to Settings exposes the existing controls. Application suspension hides the summary without restarting telemetry. Closing/ending telemetry does not pause or freeze vehicle physics; ordinary menu input release gating remains in effect.

## Quest 3 manual checks

1. Launch Driving Simulator through the existing Meta Quest/OpenXR profile with controls released. Open the left-controller menu, select Training Session: running status, increasing time, near-zero maximum speed/distance and zero collisions. Check both tabs fit and all text/buttons are readable. Close/reopen: active page and totals remain.
2. Drive the marked loop. Reopen Training: maximum speed and distance rise sensibly, distance does not decrease in reverse, and normal loop driving does not add off-road time. End later to inspect average/max; an initial idle should not dilute average, but a stop after moving should.
3. On Normal Driving, accelerate forward then apply/hold left grip: one braking event. Release below 10%, apply again above 25%: one more event. A strong full brake should qualify as hard braking at the default 3 m/s^2 threshold, at most once per application. A mild brake should not. Open the hand menu while moving: its automatic braking must not add a driver braking event. A reverse-only press at rest must not count as braking.
4. At low controlled speed, approach a barrier: a qualifying impact adds one collision. Holding contact must not add more; separate impacts more than 0.75 s apart can add another. Tiny touches below 0.8 m/s normal impact speed and ordinary ground contact must not count. Summary strongest impact should be nonzero after a qualifying crash.
5. Drive onto the west outer shoulder (approximately x < -34 near the straight, before the barrier at x = -40), then stop there for several seconds. Off-road time must increase while both moving and stopped. Return to within 6 m of the lane-dash centerline: accumulation stops. Check rounded corners do not falsely count as off road. Use summary to inspect this metric; it is intentionally omitted from live menu text.
6. Switch Normal -> Winter Storm -> Custom using a slider -> Dense Fog. End later: starting/ending scenarios and change count reflect actual changes. Re-selecting the same preset adds no change. All verified preset percentages, slider sync and visibility/grip behavior must remain intact.
7. After accumulating distance, a collision and off-road time, select Winter Storm and press dashboard Reset Vehicle with controller ray, then repeat with hand pinch/poke. Totals/session remain, no teleport distance or reset hard-brake is added, scenario stays Winter Storm and conditions stay 70%/80%.
8. Select End Session while safely stopped. The separate summary appears level in front of the headset; look/lean around to verify it does not follow head motion. Read all metrics, compare time/distance/collisions with the menu, and confirm values remain frozen if time passes, the vehicle moves or the scenario changes.
9. Select Close: summary hides, telemetry remains ended. Reopen Training: ended status and frozen totals. Press Reset Session: fresh running totals, current scenario and car pose preserved. End again, then select New Session: same clearing behavior and summary hides. Repeat several cycles to check no duplicate callbacks.
10. Repeat tabs, End, Close and New Session using real hands (far pinch and near poke). Confirm palm launcher, controller UI rays and controller-anchored menu still work; open while slowly driving/turning and verify stable anchoring and original propulsion blocking. The summary is designed for far-ray/pinch use at 1.1 m; lean/reach only if comfortable for near interaction.
11. Test passthrough both ways, volume, conditions sliders, every scenario preset, hand/controller switching, focus/system-menu loss and suspend/resume. Duration and telemetry should pause during focus loss/suspension; an ended session must not restart. Assess Quest frame rate, readability and comfort directly on device.

## Automated validation

Temporary Play Mode checks passed **1,166 assertions** (including missing-script structural checks). Coverage: startup and Normal scenario; stationary/moving distance; average/max/current speed and pre-movement idle exclusion; absolute steering accumulation; both brake thresholds, continuous hold, mild versus hard deceleration and reverse exclusion; collision minimum/cooldown/strongest impact; real barrier collision relay and real ground filtering; road straight/corner/spawn/shoulder/infield/height tests; actual Rigidbody speed/position observation; Custom and same-preset scenario events; actual dashboard reset preservation; End freeze and completed snapshot equality; live Training/Settings tabs; summary content/stable pose/Close; New and Reset lifecycle; and 20 repeated preset/Custom cycles. Exactly one EventSystem and XRUIInputModule were found. Summary and menu were rendered for inspection.

Logs: `Logs/Telemetry-Checks.txt`, `Logs/Telemetry-Checks.log`. Preview images: `Logs/Telemetry-Menu.png`, `Logs/Telemetry-Settings.png`, `Logs/Telemetry-Summary.png` (synthetic test values). Temporary helpers are archived in `.utmp/telemetry-validation/` and removed from Assets/player compilation. Batch focus is explicitly simulated in the test harness; production behavior is unchanged. The road test exposed an interpolated-transform lag, corrected by observing Rigidbody position.

The brake release threshold is bounded below the press threshold (at most 90% of press) to retain hysteresis if Inspector values are mistuned. Existing desktop warnings include unavailable XRSessionSubsystem and missing gaze-assistance interactors. Unity licensing emitted access-token/entitlement diagnostics while allowing the approved runs. Device-specific tracking, UI targeting, passthrough, controller anchoring, frame rate and comfort require the Quest tests above.

Final Unity 6000.3.23f1 compilation/import after helper removal passed with **exit 0**, with no C# compilation errors and no `-noUpm`. Log: `Logs/Telemetry-Final-Compile.log`. Licensing diagnostics and a Curl 42 callback-aborted shutdown message did not prevent successful completion. Command:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "X:\Capstone\XR_Capstone" `
  -logFile "X:\Capstone\XR_Capstone\Logs\Telemetry-Final-Compile.log"
```

Dashboard/cockpit milestone: current speed now lives on the dashboard independently of session state. The menu shows Max Speed, and summary average/max speeds use km/h. See DRIVING_COCKPIT.md for current display details and higher-speed validation.

