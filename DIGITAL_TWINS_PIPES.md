# Digital Twins pipes manipulation

Scene: `Assets/Scenes/Digital Twins.unity`. Unity 6000.3.23f1, installed XRI 3.3.2.

## Files and hierarchy

Created:
- `Assets/Scripts/DigitalTwins/TransformLockController.cs` and `.meta`
- `Assets/Scripts/DigitalTwins/TransformLockUI.cs` and `.meta`
- `Assets/Scripts/DigitalTwins.meta`
- This report.

Modified: `Assets/Scenes/Digital Twins.unity` (already untracked when this task started).

Added hierarchy:

```text
Pipes Manipulation Root
    pipes                       (original connected FBX instance)
Pipes Lock Control              (separate scene root)
    Lock Button
        Label
```

The original FBX has a single MeshFilter/MeshRenderer, 1,257 vertices, and no child objects. The wrapper pivot is centered on its bounds; reparenting preserves its world-space geometry, orientation and size. The asset is small and remains near the scene origin/floor, as originally placed. Its source file and import settings are byte-for-byte unchanged.

The wrapper owns XRGrabInteractable, XRGeneralGrabTransformer, Rigidbody, BoxCollider, and TransformLockController. No components were added to the FBX itself. The collider is one non-trigger box centered at the wrapper origin, size **0.110808 × 0.141234 × 0.194305 m**, covering the renderer bounds plus 5 mm padding on each face. This deliberately provides a forgiving selection volume, including empty space between pipe sections, without mesh collision cost.

The control has a world-space Canvas, TrackedDeviceGraphicRaycaster and TransformLockUI. Its child has an Image/Button and a TextMeshPro label. The existing rig creates the shared EventSystem/XRUIInputModule at runtime through XRI's RegisteredUIInteractorCache; no new EventSystem was authored. Play Mode confirmed one EventSystem.

All pre-existing scene records remain identical except the pipes prefab instance's parent/local transform and the scene root list. The XR rig, camera, hands, controller setup, hand menu, passthrough and volume objects were preserved. Hash checks also confirmed no task changes to SampleScene, HandMenuController, OpenXR settings, package manifest/lock or EditorBuildSettings. Pre-existing user changes were retained.

## Manipulation and locking

- XRGrabInteractable uses Multiple selection, dynamic attachment, Instantaneous movement, no throwing, and the existing scene interaction manager. Its Rigidbody is kinematic and gravity-free, so releasing the object keeps its placement.
- One hand moves and rotates the object using the supported XRI general transformer.
- Two hands rotate using TwoHandedAverage; separating/bringing hands together scales all three axes uniformly. One-handed scaling is disabled.
- TransformLockController exposes translation, rotation and scaling toggles, plus minimum/maximum scale in the Inspector. Defaults are **0.25–3 times the initial wrapper scale**. XRI's supported ratio ranges are 0.01–1 for the minimum and 1–10 for the maximum; the initial size stays within the range. The built-in transformer clamps scaling.
- `LockTransform()` captures current world position/rotation and local scale, suppresses throwing, calls `XRInteractionManager.CancelInteractableSelection((IXRSelectInteractable)grab)`, and disables the grab component. All selectors receive canceled exit events and release their references.
- After XRI restores its post-drop physics settings, the controller stores them, clears dynamic velocities if applicable, makes the body kinematic/gravity-free and freezes all constraints. It restores the captured transform once. There is no frame-by-frame transform reset.
- `UnlockTransform()` restores the previous physics/interaction settings and configured manipulation channels. `ToggleLock()` provides the button action. Calls are idempotent.
- The button switches between **Lock** and **Unlock**. It follows the renderer bounds 12 cm above the geometry and faces the camera; it stays 22 × 7.2 cm regardless of pipe scale. Only the UI follows each frame.
- State lasts only for the runtime session. No persistence, spatial anchors, puzzle systems or unrelated assets were added. Nothing was committed.

## Validation

Setup/import completed with exit 0. Temporary Play Mode checks completed with exit 0: **61 assertions**, including one-hand translation/rotation, two-hand rotation, uniform scaling, both scale limits, one- and two-selector canceled exits, exact lock transform preservation, grab unregistration/physics freeze, manipulation after unlock, Inspector toggles, twenty button lock/unlock cycles, and exact stability across later frames/physics updates.

The checks used synthetic XRI selectors. They do not establish real Quest tracking quality or physical UI targeting. Normal grab easing was bypassed only in the synchronous test harness. Prefab linkage was checked in Edit Mode; the runtime mesh reference was also verified.

Temporary setup/test scripts were removed from Assets and retained under `.utmp/pipes-validation/`, outside the player build. Logs are `Logs/Pipes-Inspection.txt`, `Logs/Pipes-Setup.log`, `Logs/Pipes-PlayMode.log`, `Logs/Pipes-PlayMode-result.txt`, and `Logs/Pipes-Final-Compile.log`.

Final batch import/compile after removing the temporary scripts **passed with exit 0**, with no script compilation errors. It used the existing validation command, without `-noUpm`:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath "X:\Capstone\XR_Capstone" `
  -logFile "X:\Capstone\XR_Capstone\Logs\Pipes-Final-Compile.log"
```

Play Mode reported the existing gaze-assistance warning ("No ray and select interactor found!") and no active XRSessionSubsystem on the desktop. Licensing emitted access-token/entitlement diagnostics but permitted the successful runs. The initial sandboxed launch could not connect to licensing; subsequent launches used approved external execution. No remaining script compilation errors or test exceptions occurred in the passing run.

## Quest 3 manual checks

1. Launch Digital Twins on Quest 3. Locate the small pipes near the original scene origin/floor. Confirm hands/controllers, hand menu, volume and passthrough still work.
2. With controllers, target the pipes, hold grab, move and rotate. Release: they should stay where placed. Repeat with near-hand pinch and far-hand selection.
3. Select the pipes with both hands/controllers at distinct points. Move hands apart/together and rotate the two-hand arrangement. Shape proportions must stay fixed; size must stop at 0.25× and 3× the initial size. Release one hand and continue moving/rotating with the other; check for jumps.
4. Release the pipes, press Lock using the controller ray, then repeat using hand UI targeting. Confirm the label becomes Unlock. Attempt near/far grabbing, moving, twisting and scaling; the pipes must remain fixed.
5. While one hand holds the pipes, use the other hand/controller to press Lock. The held selection must release immediately without a positional, rotational or scale jump. Keep moving the previously grabbing hand and confirm no drift.
6. Press Unlock, then deliberately release and re-grab. Confirm manipulation resumes at the same size and pose. Repeat twenty times, including at both scale limits. Confirm the button remains readable and outside the geometry as the model rotates/scales.
7. In the Inspector, test each manipulation toggle individually in a headset development session; disabled channels must remain unchanged. Adjust the scale limits and confirm the new bounds.
8. Restart the scene/app. It should return to the authored transform and unlocked state. No placement should persist.

Two simultaneous selection cancellation was exercised programmatically because both occupied hands normally leave no spare pointer for pressing a UI button. No extra shortcut or interaction system was added for that test.
