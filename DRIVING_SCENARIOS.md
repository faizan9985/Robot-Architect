# Driving scenario presets

Scene: `Assets/Scenes/Driving Simulator.unity`.

`DrivingScenarioManager` stores Inspector-editable serializable `DrivingScenarioPreset` entries keyed by `DrivingScenarioType`. It applies normalized visibility/slipperiness through the existing `DrivingEnvironmentController`; fog and traction remain owned by that controller and VehicleController. Startup applies Normal once in Start, so closing/reopening UI or resetting the car never reapplies startup defaults.

| Preset | Low Visibility | Slippery Roads |
| --- | --- | --- |
| Normal Driving | 0 | 0 |
| Dense Fog | 0.8 | 0 |
| Icy Road | 0 | 0.9 |
| Winter Storm | 0.7 | 0.8 |
| Custom | unchanged | unchanged |

Tune values on the manager beside DrivingEnvironmentController in the scene. Custom is a mode, without a forced data entry. Adding future condition fields belongs in the preset model and environment controller, with selection kept separate from effect logic.

The hand menu's Driving Conditions section gains Scenario Presets, containing a current-scenario label and five buttons in two rows. DrivingScenarioUI is attached beside DrivingConditionsUI on Panel. It uses the existing world-space canvas, raycaster and TMP button style. The panel grows from 420 x 590 to 420 x 730 canvas units (about 0.357 x 0.621 m at the existing scale). Anchoring/tracking scripts are unchanged. No description text was added.

Every environment ConditionsChanged event outside an active preset application changes selection to Custom. This covers both existing slider callbacks without adding scenario logic to HandMenuController. The manager guards its own environment writes, then emits ScenarioChanged once. DrivingConditionsUI already uses SetValueWithoutNotify to update sliders; updating the selection only changes text/color, without invoking button callbacks. All listeners are removed on disable and refreshed on enable.

ResetVehicle changes vehicle pose, motion and seat calibration only; it does not change the manager or environment values. Scenario UI references are optional: its section hides if no manager is assigned. Only Driving Simulator is authored; shared menu scripts, other scenes, prefab assets, XR configuration, input and package settings are unchanged. No additional conditions, telemetry, scoring, effects or per-frame searches are added. No commits.

Quest 3 manual checks:
1. Launch Driving Simulator using the existing Quest/OpenXR setup. Open the controller menu: Normal Driving selected, both sliders 0%.
2. Select each preset and verify the percentages in the table, matching visibility and grip. All five buttons must be reachable using controller ray/trigger.
3. Select Winter Storm, move Slippery Roads to 50%: Custom selected, visibility stays 70%. Change visibility too; Custom remains selected without snapping.
4. Select Dense Fog after Custom: visibility 80%, slipperiness 0%. Select Custom directly: values stay unchanged.
5. Close/reopen with controllers and then with the palm launcher: selected scenario and slider values persist. Repeat selection with hand far pinch and near poke.
6. Select Winter Storm and press the actual dashboard Reset Vehicle button with controllers, then hands: spawn/seat restore, scenario stays Winter Storm, sliders stay 70%/80%, fog and grip persist.
7. Open the controller menu while driving/turning: it stays anchored above the controller, propulsion blocks as before, all rows remain readable. Close, release inputs and resume driving.
8. Test palm-facing/gaze visibility, tracking loss/reacquisition, controller/hand switching and suspend/resume.
9. Toggle passthrough both ways and adjust volume; verify original behavior and no obstruction from the new controls.
10. Repeat rapid scenario selection and slider adjustments: no flicker, snapping, growing panel or noticeable pause. Assess comfort/readability on device.

Validation: temporary Play Mode harness passed 801 assertions, including 40 preset button switches with exact event counts, environment/slider values, manual Custom from each slider, explicit Custom retaining values, reopening UI, ResetVehicle preservation, one EventSystem and XRUIInputModule, and missing-script checks. See `Logs/Scenario-Checks.txt` and `Logs/Scenario-Checks.log`. Temporary authoring/test scripts are archived under `.utmp/scenario-validation/` and removed from Assets. A final layout pass adds clearance between the second button row and visibility label. Desktop warnings were the existing unavailable XRSessionSubsystem and gaze-assistance warnings; license entitlement diagnostics occurred but approved Unity runs succeeded. Real Quest interaction, passthrough, tracking and controller anchoring require the manual checks above.
Final Unity 6000.3.23f1 batch compilation/import after helper removal: exit 0, no C# compilation errors, no `-noUpm`. Log: `Logs/Scenario-Final-Compile.log`. The log also contains licensing/access-token diagnostics and a Curl 42 callback-aborted message during shutdown; neither prevented successful completion.
