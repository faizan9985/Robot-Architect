using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace RobotArchitect.Editor
{
    /// <summary>Imports locally installed official packages; contains no sample implementation.</summary>
    public static class RobotArchitectSampleRestoration
    {
        const string State = "RobotArchitect.Restoration.";
        const string Xri = "com.unity.xr.interaction.toolkit";
        const string Hands = "com.unity.xr.hands";
        const string Tmp = "Assets/TextMesh Pro";
        static double quietSince;
        static bool ticking;

        // Dependency order matters: imported samples contain auto-referenced assemblies.
        static readonly string[,] Required = {
            { Xri, "3.3.2", "Starter Assets" },
            { Hands, "1.9.0", "HandVisualizer" },
            { Xri, "3.3.2", "Hands Interaction Demo" },
            { Xri, "3.3.2", "Spatial Keyboard" },
            { Hands, "1.9.0", "Hand Capture" },
            { Hands, "1.9.0", "Gestures" },
            { Xri, "3.3.2", "XR Device Simulator" },
            { Xri, "3.3.2", "XR Interaction Simulator" }
        };

        [MenuItem("Tools/Robot Architect/Restore Required Samples")]
        public static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and wait for compilation before restoration.");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Start(false);
        }

        // Batch invocation must omit -quit: imports can reload this assembly several times.
        public static void RestoreBatch() => Start(true);

        static void Start(bool batch)
        {
            if (SessionState.GetBool(State + "active", false))
                throw new InvalidOperationException("A restoration is already active in this Editor session.");
            VerifyVersions();
            SessionState.SetInt(State + "step", 0);
            SessionState.SetBool(State + "batch", batch);
            SessionState.SetBool(State + "active", true);
            SessionState.SetBool(State + "archivePending", false);
            SessionState.SetString(State + "deadline", DateTime.UtcNow.AddMinutes(30).Ticks.ToString());
            Resume();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.update -= Tick;
            if (SessionState.GetBool(State + "active", false))
            {
                quietSince = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
                AssetDatabase.importPackageCompleted -= ArchiveCompleted;
                AssetDatabase.importPackageCompleted += ArchiveCompleted;
                AssetDatabase.importPackageFailed -= ArchiveFailed;
                AssetDatabase.importPackageFailed += ArchiveFailed;
                AssetDatabase.importPackageCancelled -= ArchiveCancelled;
                AssetDatabase.importPackageCancelled += ArchiveCancelled;
            }
        }

        static void ArchiveCompleted(string name)
        {
            SessionState.SetBool(State + "archivePending", false);
            quietSince = EditorApplication.timeSinceStartup;
        }
        static void ArchiveFailed(string name, string reason) => Fail("Official TMP archive import failed.");
        static void ArchiveCancelled(string name) => Fail("Official TMP archive import was cancelled.");

        static void Tick()
        {
            if (ticking) return;
            if (!SessionState.GetBool(State + "active", false)) return;
            if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(State + "deadline", "0")))
            {
                Fail("Restoration exceeded 30 minutes; inspect package availability and compilation.");
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                quietSince = EditorApplication.timeSinceStartup;
                return;
            }
            // Allow delayed package-specific callbacks and material converters to settle.
            if (EditorApplication.timeSinceStartup - quietSince < 5) return;
            ticking = true;
            try
            {
                int step = SessionState.GetInt(State + "step", 0);
                if (SessionState.GetBool(State + "archivePending", false))
                {
                    string previous = step == 1 ? Tmp + "/Resources/TMP Settings.asset" : Tmp + "/Examples & Extras";
                    // A reload may occur after import completion but before its event is observed.
                    if (!File.Exists(previous) && !Directory.Exists(previous)) return;
                    SessionState.SetBool(State + "archivePending", false);
                }
                if (step < 2)
                {
                    ImportTmp(step);
                }
                else if (step < Required.GetLength(0) + 2)
                {
                    int index = step - 2;
                    var sample = FindSample(index);
                    string expected = ExpectedPath(index);
                    Check(sample.importPath.Replace('\\', '/').EndsWith(expected, StringComparison.Ordinal),
                        "Unexpected Package Manager sample destination.");
                    SessionState.SetInt(State + "step", step + 1);
                    if (Directory.Exists(expected))
                        Debug.Log("ROBOT_RESTORE_KEEP: " + expected + " (existing folder; no overwrite)");
                    else
                    {
                        Check(sample.Import(Sample.ImportOptions.None), "Package Manager sample import failed: " + Required[index, 2]);
                        Debug.Log("ROBOT_RESTORE_IMPORT: " + expected);
                    }
                }
                else
                {
                    ValidateRestoredAssets();
                    Finish(true);
                }
                quietSince = EditorApplication.timeSinceStartup;
            }
            catch (Exception exception)
            {
                // Do not log arbitrary package exception messages, URLs, or serialized data.
                Fail("Restoration failed (" + exception.GetType().Name + "). " +
                    (exception is RestorationFailure ? exception.Message : "Inspect package/compilation diagnostics; no credentials are logged by this utility."));
            }
            finally { ticking = false; }
        }

        static void ImportTmp(int step)
        {
            string sentinel = step == 0 ? Tmp + "/Resources/TMP Settings.asset" : Tmp + "/Examples & Extras";
            if (File.Exists(sentinel) || Directory.Exists(sentinel))
            {
                Debug.Log("ROBOT_RESTORE_KEEP: " + sentinel + " (existing resources; no overwrite)");
                SessionState.SetInt(State + "step", step + 1);
                return;
            }
            // Partial installs need manual review, not a silent archive overwrite.
            if (step == 0) Check(!Directory.Exists(Tmp), "Partial TMP installation found; restore into a clean project instead.");
            else Check(!Directory.Exists(Tmp + "/Examples & Extras"), "Partial TMP examples installation found.");
            string archive = Path.Combine(Package("com.unity.ugui").resolvedPath, "Package Resources",
                step == 0 ? "TMP Essential Resources.unitypackage" : "TMP Examples & Extras.unitypackage");
            Check(File.Exists(archive), "Official TMP archive is unavailable.");
            SessionState.SetInt(State + "step", step + 1);
            SessionState.SetBool(State + "archivePending", true);
            AssetDatabase.ImportPackage(archive, false);
        }

        static Sample FindSample(int index)
        {
            var matches = Sample.FindByPackage(Required[index, 0], Required[index, 1])
                .Where(s => s.displayName == Required[index, 2]).ToArray();
            Check(matches.Length == 1, "Required official sample definition is missing: " + Required[index, 2]);
            return matches[0];
        }

        static string ExpectedPath(int index) => "Assets/Samples/" +
            (Required[index, 0] == Xri ? "XR Interaction Toolkit/" : "XR Hands/") +
            Required[index, 1] + "/" + Required[index, 2];

        [Serializable] sealed class SampleDefinition { public string displayName; public string path; }
        [Serializable] sealed class PackageDefinition { public SampleDefinition[] samples; }
        static string SourcePath(int index)
        {
            var package = Package(Required[index, 0]);
            var definition = JsonUtility.FromJson<PackageDefinition>(File.ReadAllText(Path.Combine(package.resolvedPath, "package.json")));
            var sample = definition.samples.Single(s => s.displayName == Required[index, 2]);
            return Path.GetFullPath(Path.Combine(package.resolvedPath, sample.path));
        }

        static PackageInfo Package(string name)
        {
            var package = PackageInfo.GetAllRegisteredPackages().FirstOrDefault(p => p.name == name);
            Check(package != null, "Required resolved package is missing: " + name);
            return package;
        }

        static void VerifyVersions()
        {
            Check(Application.unityVersion == "6000.3.23f1", "Use Unity 6000.3.23f1.");
            var versions = new Dictionary<string, string> {
                { Xri, "3.3.2" }, { Hands, "1.9.0" }, { "com.unity.ugui", "2.0.0" },
                { "com.unity.xr.openxr", "1.18.0" }, { "com.unity.xr.meta-openxr", "2.6.1" },
                { "com.unity.xr.management", "4.7.0" }, { "com.unity.xr.core-utils", "2.6.0" },
                { "com.unity.xr.arfoundation", "6.6.0" }, { "com.unity.xr.compositionlayers", "2.5.0" },
                { "com.unity.inputsystem", "1.20.0" }, { "com.unity.render-pipelines.universal", "17.3.0" },
                { "com.unity.shadergraph", "17.3.0" }
            };
            foreach (var pair in versions)
                Check(Package(pair.Key).version == pair.Value, "Resolved package version mismatch: " + pair.Key + "; expected " + pair.Value);
        }

        [MenuItem("Tools/Robot Architect/Validate Restored Samples")]
        public static void ValidateRestoredAssets()
        {
            VerifyVersions();
            int guidCount = 0;
            for (int i = 0; i < Required.GetLength(0); i++)
            {
                FindSample(i);
                string source = SourcePath(i);
                string destination = ExpectedPath(i);
                Check(Directory.Exists(destination), "Missing sample folder: " + destination);
                foreach (string original in Directory.GetFiles(source, "*.meta", SearchOption.AllDirectories))
                {
                    string relative = original.Substring(source.Length).TrimStart('/', '\\');
                    string imported = Path.Combine(destination, relative);
                    Check(File.Exists(imported), "Missing imported metadata: " + imported);
                    string expected = MetaGuid(original);
                    Check(expected == MetaGuid(imported), "Imported asset GUID differs: " + imported);
                    guidCount++;
                }
            }

            string xri = "Assets/Samples/XR Interaction Toolkit/3.3.2/";
            string hands = "Assets/Samples/XR Hands/1.9.0/";
            Check(guidCount == 694, "Unexpected official sample metadata inventory; review package contents.");
            var tmpGuids = new Dictionary<string, string> {
                { Tmp + "/Resources/TMP Settings.asset", "3f5b5dff67a942289a9defa416b206f3" },
                { Tmp + "/Resources/Fonts & Materials/LiberationSans SDF.asset", "8f586378b4e144a9851e7b34d9b748ee" },
                { Tmp + "/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset", "2e498d1c8094910479dc3e1b768306a4" },
                { Tmp + "/Shaders/TMP_SDF-Mobile.shader", "fe393ace9b354375a9cb14cdbbc28be4" }
            };
            foreach (var pair in tmpGuids)
                Check(AssetDatabase.AssetPathToGUID(pair.Key) == pair.Value && AssetDatabase.LoadMainAssetAtPath(pair.Key) != null,
                    "Required TMP asset GUID or import is invalid: " + pair.Key);
            foreach (string path in new[] {
                xri + "Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab",
                xri + "Hands Interaction Demo/Prefabs/LeftHandQuestVisual.prefab",
                xri + "Hands Interaction Demo/Prefabs/RightHandQuestVisual.prefab",
                "Assets/RobotArchitect/Prefabs/TestCube.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null, "Required prefab cannot load: " + path);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                        "Missing script in required prefab: " + path);
            }
            foreach (string path in new[] {
                hands + "HandVisualizer/Scripts/HandVisualizer.cs",
                xri + "Starter Assets/Scripts/ControllerInputActionManager.cs",
                xri + "Hands Interaction Demo/Scripts/PokeGestureDetector.cs" })
                Check(AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass() != null, "Required sample script has not compiled: " + path);

            Check(AssetDatabase.LoadMainAssetAtPath(xri + "Starter Assets/XRI Default Input Actions.inputactions")
                is UnityEngine.InputSystem.InputActionAsset, "Default input actions did not import.");
            foreach (string path in new[] { hands + "Hand Capture/Sprites/Button 1.png",
                hands + "Hand Capture/Sprites/Button 2.png", xri + "XR Device Simulator/UI/General/DeviceSimUI_bg.png" })
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Check(sprites.Any(sprite => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id)
                    && id == 21300000 && guid == MetaGuid(path + ".meta")), "Scene sprite file ID cannot resolve: " + path);
            }

            var dependencies = AssetDatabase.GetDependencies(RobotArchitectPhase1.ScenePath, true);
            Check(!dependencies.Any(p => p.StartsWith("Assets/Real Stars Skybox/", StringComparison.Ordinal)
                || p.StartsWith("Assets/pipes/", StringComparison.Ordinal)), "Robot Architect depends on an excluded asset.");
            int materialCount = 0;
            foreach (string path in dependencies.Where(p => p.EndsWith(".mat", StringComparison.Ordinal)))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Check(material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader",
                    "Required material shader cannot resolve: " + path);
                Check(material.shader.name != "Standard" && material.shader.name != "Standard (Specular setup)",
                    "Required material still needs URP conversion: " + path);
                materialCount++;
            }
            RobotArchitectPhase1.Validate();
            Debug.Log("ROBOT_RESTORE_PASS: " + guidCount + " sample GUIDs; 3 scene sprite file IDs; " +
                materialCount + " scene materials; required prefabs, scripts, input actions and structural validation.");
        }

        static string MetaGuid(string path)
        {
            var match = Regex.Match(File.ReadAllText(path), @"(?m)^guid: ([0-9a-f]{32})");
            Check(match.Success, "Metadata GUID is absent: " + path);
            return match.Groups[1].Value;
        }

        public static void BuildAndroid()
        {
            ValidateRestoredAssets();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { RobotArchitectPhase1.ScenePath }, target = BuildTarget.Android,
                locationPathName = "Builds/RobotArchitect-Reconstruction.apk", options = BuildOptions.None
            });
            Check(report.summary.result == BuildResult.Succeeded, "Android reconstruction build failed.");
            Debug.Log("ROBOT_ANDROID_BUILD_PASS");
        }

        sealed class RestorationFailure : Exception { public RestorationFailure(string message) : base(message) { } }
        static void Check(bool condition, string message) { if (!condition) throw new RestorationFailure(message); }
        static void Fail(string message) { Debug.LogError("ROBOT_RESTORE_FAIL: " + message); Finish(false); }
        static void Finish(bool success)
        {
            SessionState.SetBool(State + "active", false);
            EditorApplication.update -= Tick;
            AssetDatabase.importPackageCompleted -= ArchiveCompleted;
            AssetDatabase.importPackageFailed -= ArchiveFailed;
            AssetDatabase.importPackageCancelled -= ArchiveCancelled;
            if (SessionState.GetBool(State + "batch", false)) EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
