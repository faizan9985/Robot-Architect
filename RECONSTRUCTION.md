# Robot Architect source-only reconstruction

Use Unity **6000.3.23f1** with Android Build Support, its SDK/NDK and OpenJDK.
This workflow restores official package content locally rather than redistributing
sample source in Git. It does not grant redistribution rights to imported content.

## Package resolution

Keep both `Packages/manifest.json` and `Packages/packages-lock.json`. The restorer
checks the resolved versions below; it does not install, upgrade or change packages.

| Package | Resolved version |
| --- | --- |
| com.unity.xr.interaction.toolkit | 3.3.2 |
| com.unity.xr.hands | 1.9.0 |
| com.unity.xr.openxr | 1.18.0 |
| com.unity.xr.meta-openxr | 2.6.1 |
| com.unity.xr.management | 4.7.0 |
| com.unity.xr.core-utils | 2.6.0 |
| com.unity.xr.arfoundation | 6.6.0 |
| com.unity.xr.compositionlayers | 2.5.0 |
| com.unity.inputsystem | 1.20.0 |
| com.unity.ugui | 2.0.0 |
| com.unity.render-pipelines.universal | 17.3.0 |
| com.unity.shadergraph | 17.3.0 |

Preserve the other locked dependencies too, including Meta XR 209.0.0 and Voice
85.0.1. The current manifest asks for OpenXR 1.16.1, but Meta OpenXR 2.6.1 requires
1.18.0 and the lockfile resolves that version. Do not downgrade it to 1.16.1.

## Restore

1. Open a fresh source-only copy, let Package Manager resolve packages, and wait
   for compilation. Do not save a scene while sample references are unresolved.
2. Run **Tools > Robot Architect > Restore Required Samples** outside Play Mode.
3. The utility restores official TMP Essential Resources and Examples & Extras
   from the installed uGUI package archives, then imports these official samples:
   - XRI 3.3.2: Starter Assets, Hands Interaction Demo, Spatial Keyboard,
     XR Device Simulator, XR Interaction Simulator.
   - XR Hands 1.9.0: HandVisualizer, Hand Capture, Gestures.
4. Wait for `ROBOT_RESTORE_PASS`. Run **Validate Restored Samples** again if desired.
5. Open `Assets/RobotArchitect/Scenes/RobotArchitect.unity`.

**Importing samples executes package-specific Editor scripts and can trigger
additional imports, project validation, script compilation, and URP material
conversion.** Use a separate reconstruction workspace first. Never use reimport
as a way to overwrite a customized working sample directory.

Existing sample folders are explicitly kept, not overwritten. Existing TMP
installations are kept; a partial Essential Resources installation is rejected.
Failed or partial sample imports require inspection in the isolated workspace;
the utility does not delete folders to repair them. It checks official metadata
GUIDs and rejects missing or incompatible assets. Package-specific diagnostics
may still appear in Unity's own logs; the utility never prints credential values.

The restorer persists its progress in Editor SessionState across domain reloads,
waits for compilation/import activity and delayed converters, and times out after
30 minutes. Batch restoration must **omit `-quit`**; the utility exits the Editor
with code 0 on success or 1 on failure:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$copy = 'C:\path\to\isolated\RobotArchitect'
& $unity -batchmode -nographics -projectPath $copy `
  -executeMethod RobotArchitect.Editor.RobotArchitectSampleRestoration.RestoreBatch `
  -logFile "$copy\Logs\Restore.log"
```

## Repository boundaries

Omit these directories and their adjacent root `.meta` files from a source-only
copy and eventual Git staging (the rules have not been added to `.gitignore` by
this tooling task):

- `Assets/Samples/`
- `Assets/Real Stars Skybox/`
- `Assets/TextMesh Pro/`
- `Assets/pipes/` (redistribution provenance unresolved)

Also omit Library, Temp, Logs, Build/Builds, UserSettings, IDE caches, generated
project files, APK/AAB files, local credentials and private signing material.
Do not copy the working Library to a reconstruction project.

Keep original source and `.meta` files under RobotArchitect, Scripts, Prefabs,
Settings, XR, XRI, CompositionLayers, sanitized Resources and required Oculus
settings; keep ProjectSettings, Packages and documentation. Keep `SECURITY.md`
and `DevAgentCredentialBuildGuard.cs` and its metadata. The credential guard must
remain enabled for Android builds because Meta's build processor injects a token.

Robot Architect has no identified dependency on Real Stars Skybox or pipes.
The legacy Digital Twins scene requires pipes. Legacy scenes and recovery copies
need separate review before inclusion; excluding pipes does not make those scenes
reproducible. Do not silently edit them or their build entries. The structural
validator requires `Assets/Prefabs/CubePiece.prefab`, although the playable scene
uses the independent `Assets/RobotArchitect/Prefabs/TestCube.prefab`.

## Known import differences

The audit found 694 matching sample metadata GUIDs, no modified sample scripts,
prefabs, assembly definitions or input actions, and no missing/extra sample files.
Local differences included 141 sprite metadata files, 29 materials, and the
HandVisualizer demonstration scene. These are consistent with importer upgrades,
URP conversion and serialization; fresh import must verify the resulting behavior.

The palm menu uses Hand Capture's `Button 1.png` and `Button 2.png`, and XR Device
Simulator's `DeviceSimUI_bg.png`. Their serialized scene sprite file ID is
21300000. The utility verifies that exact file ID resolves after import. Both
XR Hands and XRI include material pipeline handlers; allow their delayed callbacks
to finish before validating or building. GUID equality alone does not prove that
sprite subassets or material appearance are identical.

The package-source `Unity_Hand_Medium.mat` itself contains an unresolved legacy
texture reference. This pre-existing reference needs rendering review; do not
automatically rewrite the licensed material or the working rig to hide it.

## Validation

Restoration validates package versions, all source sample metadata GUIDs,
required prefab scripts, compiled sample types, default input actions, three sprite
file IDs, scene material shaders and the existing Phase 1 structural checks. It
does not change or save the Robot Architect scene.

Run existing tests in the isolated copy:

```powershell
& $unity -batchmode -nographics -projectPath $copy -quit `
  -executeMethod RobotArchitect.Editor.RobotArchitectPhase1.Validate `
  -logFile "$copy\Logs\Structure.log"
# Omit -quit for the asynchronous Play Mode check.
& $unity -batchmode -nographics -projectPath $copy `
  -executeMethod RobotArchitect.Editor.RobotArchitectPlayModeSmoke.Run `
  -logFile "$copy\Logs\PlayMode.log"
& $unity -batchmode -nographics -projectPath $copy -buildTarget Android -quit `
  -executeMethod RobotArchitect.Editor.RobotArchitectSampleRestoration.BuildAndroid `
  -logFile "$copy\Logs\Android.log"
```

The optional Android command writes `Builds/RobotArchitect-Reconstruction.apk`
in that copy. Package validation, Android tool availability and build failures
must be reported separately from scene/reference checks. Keep the APK out of Git.

Quest 3 tests remain necessary: both hand meshes, tracking reacquisition, near
grab, far pinch/grab, cube release, palm launcher, poke/ray UI, volume, passthrough
on/off, recenter and suspend/resume. Review material transparency and all three
menu sprites visually. Editor and build success do not constitute hardware testing.
