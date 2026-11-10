using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;

namespace RobotArchitect.Editor
{
    /// <summary>One-time scene authoring and repeatable, non-mutating saved-scene validation.</summary>
    public static class RobotArchitectPhase1
    {
        public const string ScenePath = "Assets/RobotArchitect/Scenes/RobotArchitect.unity";
        const string Template = "Assets/Scenes/Digital Twins.unity";
        const string CubeSource = "Assets/Prefabs/CubePiece.prefab";
        const string CubePath = "Assets/RobotArchitect/Prefabs/TestCube.prefab";
        const string Materials = "Assets/RobotArchitect/Materials";
        static int checks;

        [MenuItem("Tools/Robot Architect/Create Phase 1 Scene")]
        public static void Create()
        {
            try
            {
                Require(!File.Exists(ScenePath), "Destination scene must not already exist; never overwrite authored work.");
                Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Author outside Play Mode.");
                Require(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty),
                    "Save open scenes before running the authoring command.");
                Folder("Assets/RobotArchitect/Scenes");
                Folder("Assets/RobotArchitect/Prefabs");
                Folder(Materials);
                // Unity creates a new scene GUID, retaining the identities of every referenced source asset.
                Require(AssetDatabase.CopyAsset(Template, ScenePath), "Duplicate template through AssetDatabase.CopyAsset.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var originalBuildScenes = EditorBuildSettings.scenes;
                var origin = One<XROrigin>(scene);
                var manager = One<XRInteractionManager>(scene);
                foreach (var name in new[] { "Pipes Lock Control", "Pipes Manipulation Root" })
                {
                    var go = scene.GetRootGameObjects().SingleOrDefault(x => x.name == name);
                    Require(go != null, "Find template-specific root: " + name);
                    UnityEngine.Object.DestroyImmediate(go);
                }

                var dark = Material("DarkMetal", new Color(0.075f, 0.095f, 0.12f), 0.72f, 0.48f);
                var inset = Material("Graphite", new Color(0.13f, 0.17f, 0.2f), 0.45f, 0.35f);
                var cyan = Material("CyanAccent", new Color(0.02f, 0.8f, 0.95f), 0.25f, 0.55f, true);
                var cubeMaterial = Material("TestCube", new Color(0.18f, 0.65f, 0.78f), 0.35f, 0.4f);

                var root = Node("RobotArchitect", null);
                Node("GameManager", root); // Reserved organizational object; no premature gameplay system.
                var workstation = Node("Workstation", root);
                var forward = Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                var position = origin.Camera.transform.position + forward * 0.7f;
                position.y = origin.transform.position.y + 0.90f;
                workstation.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
                Primitive("WorkstationBase", workstation, PrimitiveType.Cube, new Vector3(0, -0.045f, 0),
                    new Vector3(0.64f, 0.09f, 0.40f), dark, true);
                Primitive("Support", workstation, PrimitiveType.Cube, new Vector3(0, -0.48f, 0),
                    new Vector3(0.16f, 0.78f, 0.18f), dark, true);
                Primitive("Foot", workstation, PrimitiveType.Cube, new Vector3(0, -0.87f, 0),
                    new Vector3(0.42f, 0.06f, 0.30f), inset, true);
                Primitive("FrontAccent", workstation, PrimitiveType.Cube, new Vector3(0, -0.018f, -0.202f),
                    new Vector3(0.58f, 0.008f, 0.004f), cyan, false);
                Primitive("RearAccent", workstation, PrimitiveType.Cube, new Vector3(0, -0.018f, 0.202f),
                    new Vector3(0.58f, 0.008f, 0.004f), cyan, false);

                var input = Node("InputZone", workstation);
                input.localPosition = new Vector3(-0.22f, 0, -0.025f);
                Zone(input, inset, cyan);
                var output = Node("OutputZone", workstation);
                output.localPosition = new Vector3(0.22f, 0, -0.025f);
                Zone(output, inset, cyan);
                var conveyor = Node("ConveyorPlaceholder", workstation);
                conveyor.localPosition = new Vector3(0, 0.008f, -0.09f);
                Primitive("Belt", conveyor, PrimitiveType.Cube, Vector3.zero, new Vector3(0.24f, 0.016f, 0.12f), inset, true);
                for (var i = 0; i < 5; i++)
                    Primitive("BeltMarker" + (i + 1), conveyor, PrimitiveType.Cube,
                        new Vector3(-0.09f + i * 0.045f, 0.009f, 0), new Vector3(0.004f, 0.002f, 0.095f), cyan, false);
                var spawn = Node("RobotSpawnPoint", workstation);
                spawn.localPosition = new Vector3(0, 0.012f, 0.09f);

                var systems = Node("RobotSystems", root);
                var arm = Node("RobotArmPlaceholder", systems);
                arm.SetPositionAndRotation(spawn.position, spawn.rotation);
                Primitive("Base", arm, PrimitiveType.Cylinder, new Vector3(0, 0.022f, 0),
                    new Vector3(0.09f, 0.022f, 0.09f), dark, false);
                Primitive("BaseAccent", arm, PrimitiveType.Cylinder, new Vector3(0, 0.046f, 0),
                    new Vector3(0.075f, 0.003f, 0.075f), cyan, false);
                var shoulder = new Vector3(0, 0.075f, 0);
                var elbow = new Vector3(0.015f, 0.225f, 0.015f);
                var wrist = new Vector3(0.09f, 0.20f, -0.08f);
                Link("UpperArm", arm, shoulder, elbow, 0.043f, inset);
                Link("Forearm", arm, elbow, wrist, 0.034f, dark);
                Primitive("ShoulderJoint", arm, PrimitiveType.Sphere, shoulder, Vector3.one * 0.057f, cyan, false);
                Primitive("ElbowJoint", arm, PrimitiveType.Sphere, elbow, Vector3.one * 0.049f, cyan, false);
                var gripper = Node("Gripper", arm);
                gripper.localPosition = wrist;
                Primitive("Housing", gripper, PrimitiveType.Cube, Vector3.zero, new Vector3(0.06f, 0.026f, 0.035f), inset, false);
                foreach (var side in new[] { -1f, 1f })
                    Primitive(side < 0 ? "LeftFinger" : "RightFinger", gripper, PrimitiveType.Cube,
                        new Vector3(side * 0.023f, -0.025f, 0), new Vector3(0.009f, 0.04f, 0.016f), cyan, false);

                var interactables = Node("Interactables", root);
                var cube = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CubeSource), scene);
                // Unpack only the new copy; source CubePiece and all of its configuration stay untouched.
                PrefabUtility.UnpackPrefabInstance(cube, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<PuzzlePiece>());
                cube.name = "TestCube";
                cube.transform.SetParent(interactables, false);
                cube.transform.localScale = Vector3.one * 0.065f;
                cube.transform.SetPositionAndRotation(input.TransformPoint(new Vector3(0, 0.045f, 0)), workstation.rotation);
                cube.GetComponent<Renderer>().sharedMaterial = cubeMaterial;
                // Scene manager stays a scene reference rather than leaking into the reusable prefab.
                var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(cube, CubePath, InteractionMode.AutomatedAction);
                Require(prefab != null, "Save independent TestCube prefab.");
                cube.GetComponent<XRGrabInteractable>().interactionManager = manager;
                PrefabUtility.RecordPrefabInstancePropertyModifications(cube.GetComponent<XRGrabInteractable>());
                Require(EditorSceneManager.SaveScene(scene), "Save RobotArchitect scene using Unity Editor APIs.");
                var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
                scenes.AddRange(originalBuildScenes);
                EditorBuildSettings.scenes = scenes.ToArray();
                AssetDatabase.SaveAssets();
                Validate();
                Debug.Log("ROBOT_ARCHITECT_CREATE_PASS: Phase 1 scene and assets saved.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        [MenuItem("Tools/Robot Architect/Validate Saved Phase 1 Scene")]
        public static void Validate()
        {
            try
            {
                checks = 0;
                Require(File.Exists(ScenePath), "Scene exists.");
                // Reopen from disk so the validation covers saved serialization, not just authoring memory.
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var origin = One<XROrigin>(scene);
                var manager = One<XRInteractionManager>(scene);
                var menu = One<HandMenuController>(scene);
                var session = One<ARSession>(scene);
                Require(origin.gameObject.activeInHierarchy && manager.isActiveAndEnabled && menu.isActiveAndEnabled && session.isActiveAndEnabled,
                    "Exactly one active rig, interaction manager, menu and AR session.");
                Require(All<Camera>(scene).Length == 1, "One camera only.");
                Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(origin.gameObject).EndsWith("XR Origin Hands (XR Rig).prefab"),
                    "Original hands-rig prefab connection survives.");
                var menuData = new SerializedObject(menu);
                foreach (var field in new[] { "xrCamera", "trackingSpace", "passthrough", "launcherCanvas", "menuCanvas", "openButton",
                    "closeButton", "passthroughToggle", "passthroughLabel", "volumeControl" })
                    Require(menuData.FindProperty(field).objectReferenceValue != null, "Menu reference: " + field);
                Require(menuData.FindProperty("xrCamera").objectReferenceValue == origin.Camera, "Menu uses original rig camera.");
                Require(menuData.FindProperty("trackingSpace").objectReferenceValue == origin.CameraFloorOffsetObject.transform, "Menu uses Camera Offset.");
                Require(menuData.FindProperty("hand").enumValueIndex == 0, "Left-palm menu preserved.");
                Require(!menuData.FindProperty("enableControllerShortcut").boolValue, "Template palm-only activation preserved.");
                Require(menuData.FindProperty("puzzleGenerator").objectReferenceValue == null, "No puzzle manager dependency.");
                foreach (var field in new[] { "launcherCanvas", "menuCanvas" })
                {
                    var canvas = (Canvas)menuData.FindProperty(field).objectReferenceValue;
                    Require(!canvas.gameObject.activeSelf && canvas.renderMode == RenderMode.WorldSpace, "Hidden world-space canvas: " + field);
                    Require(canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null, "XR UI raycaster: " + field);
                }
                var passthrough = One<PassthroughController>(scene);
                var pd = new SerializedObject(passthrough);
                var cameraManager = One<ARCameraManager>(scene);
                Require(!cameraManager.enabled && cameraManager.gameObject == origin.Camera.gameObject, "Original camera ARCameraManager starts disabled.");
                Require(pd.FindProperty("xrCamera").objectReferenceValue == origin.Camera && pd.FindProperty("cameraManager").objectReferenceValue == cameraManager,
                    "Passthrough camera references resolve.");
                Require(All<UnityEngine.EventSystems.EventSystem>(scene).Length == 0, "No added EventSystem; inherited XRI registration retained.");
                Require(All<PuzzlePiece>(scene).Length == 0 && All<PuzzleManager>(scene).Length == 0, "No puzzle components.");
                Require(All<DigitalTwins.TransformLockUI>(scene).Length == 0 && All<DigitalTwins.TransformLockController>(scene).Length == 0, "Pipes lock content removed.");
                Require(!scene.GetRootGameObjects().Any(x => x.name.StartsWith("Pipes")), "No pipes roots.");

                var root = scene.GetRootGameObjects().Single(x => x.name == "RobotArchitect").transform;
                foreach (var path in new[] { "GameManager", "Workstation/WorkstationBase", "Workstation/InputZone", "Workstation/OutputZone",
                    "Workstation/ConveyorPlaceholder", "Workstation/RobotSpawnPoint", "RobotSystems/RobotArmPlaceholder/Base",
                    "RobotSystems/RobotArmPlaceholder/UpperArm", "RobotSystems/RobotArmPlaceholder/Forearm", "RobotSystems/RobotArmPlaceholder/Gripper", "Interactables/TestCube" })
                    Require(root.Find(path) != null, "Hierarchy: " + path);
                var cube = root.Find("Interactables/TestCube").gameObject;
                var grab = cube.GetComponent<XRGrabInteractable>();
                Require(grab != null && grab.enabled && grab.interactionManager == manager, "Cube grab component and scene manager.");
                Require(cube.GetComponent<Rigidbody>() != null && cube.GetComponent<BoxCollider>() != null && !cube.GetComponent<BoxCollider>().isTrigger,
                    "Cube Rigidbody and solid BoxCollider.");
                Require(cube.GetComponent<GrabColorFeedback>() != null && cube.GetComponent<PuzzlePiece>() == null, "Cube feedback without puzzle behavior.");
                Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cube) == CubePath, "New cube prefab connection.");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(CubeSource);
                Compare(source.GetComponent<Rigidbody>(), cube.GetComponent<Rigidbody>(), "m_Mass", "m_LinearDamping", "m_AngularDamping", "m_UseGravity", "m_IsKinematic", "m_Interpolate", "m_Constraints", "m_CollisionDetection");
                Compare(source.GetComponent<XRGrabInteractable>(), grab, "m_InteractionLayers.m_Bits", "m_MovementType", "m_SelectMode", "m_TrackPosition", "m_TrackRotation", "m_TrackScale", "m_ThrowOnDetach", "m_ThrowVelocityScale", "m_ThrowAngularVelocityScale", "m_UseDynamicAttach");
                Compare(source.GetComponent<GrabColorFeedback>(), cube.GetComponent<GrabColorFeedback>(), "grabbedColor");
                var feedback = new SerializedObject(cube.GetComponent<GrabColorFeedback>());
                Require(feedback.FindProperty("targetRenderer").objectReferenceValue == cube.GetComponent<Renderer>(), "Cube feedback renderer resolves.");
                var horizontal = Vector3.ProjectOnPlane(root.Find("Workstation").position - origin.Camera.transform.position, Vector3.up);
                Require(Mathf.Abs(horizontal.magnitude - 0.7f) < 0.01f, "Workstation is 0.7 m ahead horizontally.");
                Require(Vector3.Dot(horizontal.normalized, Vector3.ProjectOnPlane(origin.Camera.transform.forward, Vector3.up).normalized) > 0.99f, "Workstation faces starting viewpoint.");
                foreach (var go in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Select(x => x.gameObject))
                {
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0, "No missing scripts: " + go.name);
                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        var serialized = new SerializedObject(component);
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                            if (property.propertyType == SerializedPropertyType.ObjectReference)
                                Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                                    "No broken object reference: " + go.name + "." + property.propertyPath);
                    }
                }
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    Require(renderer.sharedMaterials.All(x => x != null && x.shader != null && x.shader.name == "Universal Render Pipeline/Lit"), "URP material: " + renderer.name);
                Require(EditorBuildSettings.scenes[0].path == ScenePath && EditorBuildSettings.scenes[0].enabled, "New scene first and enabled in build list.");
                Debug.Log("ROBOT_ARCHITECT_VALIDATION_PASS: " + checks + " structural/reference checks; no Quest runtime test performed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void Compare(UnityEngine.Object source, UnityEngine.Object target, params string[] fields)
        {
            var a = new SerializedObject(source);
            var b = new SerializedObject(target);
            foreach (var field in fields)
            {
                var x = a.FindProperty(field);
                var y = b.FindProperty(field);
                Require(x != null && y != null && SerializedProperty.DataEquals(x, y), "Source configuration preserved: " + source.GetType().Name + "." + field);
            }
        }

        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
        static T One<T>(Scene scene) where T : Component
        {
            var items = All<T>(scene);
            Require(items.Length == 1, "Exactly one " + typeof(T).Name + "; found " + items.Length);
            return items[0];
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ROBOT_ARCHITECT_CHECK_FAILED: " + message);
            checks++;
        }
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }
        static Material Material(string name, Color color, float metallic, float smoothness, bool emissive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "URP Lit shader available.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.7f);
            }
            AssetDatabase.CreateAsset(material, Materials + "/" + name + ".mat");
            return material;
        }
        static Transform Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }
        static void Zone(Transform root, Material surface, Material accent)
        {
            Primitive("Surface", root, PrimitiveType.Cube, new Vector3(0, 0.006f, 0), new Vector3(0.15f, 0.012f, 0.15f), surface, true);
            foreach (var side in new[] { -1f, 1f })
            {
                Primitive(side < 0 ? "LeftEdge" : "RightEdge", root, PrimitiveType.Cube,
                    new Vector3(side * 0.071f, 0.013f, 0), new Vector3(0.003f, 0.002f, 0.145f), accent, false);
                Primitive(side < 0 ? "FrontEdge" : "RearEdge", root, PrimitiveType.Cube,
                    new Vector3(0, 0.013f, side * 0.071f), new Vector3(0.145f, 0.002f, 0.003f), accent, false);
            }
        }
        static void Link(string name, Transform root, Vector3 start, Vector3 end, float width, Material material)
        {
            var link = Primitive(name, root, PrimitiveType.Cube, (start + end) * 0.5f, new Vector3(width, (end - start).magnitude, width), material, false);
            link.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }
    }
}
