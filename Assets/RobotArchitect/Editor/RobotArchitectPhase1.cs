using System;
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
    /// <summary>Repeatable, non-mutating validation of the standalone saved scene and prefab.</summary>
    public static class RobotArchitectPhase1
    {
        public const string ScenePath = "Assets/RobotArchitect/Scenes/RobotArchitect.unity";
        const string CubePath = "Assets/RobotArchitect/Prefabs/TestCube.prefab";
        static int checks;

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
                Require(cube.GetComponent<GrabColorFeedback>() != null, "Cube grab feedback is present.");
                Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cube) == CubePath, "New cube prefab connection.");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(CubePath);
                Require(source != null, "Standalone TestCube prefab loads.");
                var body = cube.GetComponent<Rigidbody>();
                Require(body.useGravity && !body.isKinematic && Mathf.Approximately(body.mass, 1f), "TestCube remains a 1 kg gravity-driven body.");
                Require(grab.movementType == XRBaseInteractable.MovementType.Kinematic && grab.throwOnDetach
                    && grab.trackPosition && grab.trackRotation && grab.interactionLayers.value == -1,
                    "TestCube retains kinematic grab movement, tracking, throw and all interaction layers.");
                Require(Mathf.Abs(cube.transform.localScale.x - 0.065f) < 0.0001f, "TestCube remains 6.5 cm.");
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
                Require(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == ScenePath && EditorBuildSettings.scenes[0].enabled, "Only Robot Architect is included and enabled in build list.");
                ValidateQuestConfiguration();
                Debug.Log("ROBOT_ARCHITECT_VALIDATION_PASS: " + checks + " structural/reference checks; no Quest runtime test performed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        static void ValidateQuestConfiguration()
        {
            Require(Application.unityVersion == "6000.3.23f1", "Expected Unity Editor version.");
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            Require(settings != null, "Android OpenXR settings exist.");
            foreach (var name in new[] { "MetaQuestFeature", "HandInteractionProfile", "HandTracking",
                "MetaHandTrackingAim", "ARCameraFeature", "ARSessionFeature" })
                Require(settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>().Any(f => f != null && f.GetType().Name == name && f.enabled),
                    "Enabled Android OpenXR feature: " + name);
            var general = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            Require(general != null && general.Manager != null && general.Manager.activeLoaders.Any(
                loader => loader != null && loader.GetType().Name == "OpenXRLoader"), "Android OpenXR loader retained.");
            var config = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Oculus/OculusProjectConfig.asset"));
            Require(config.FindProperty("handTrackingSupport").intValue != 0, "Hand tracking support is not ControllersOnly.");
            var profile = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Build Profiles/Meta Quest.asset"));
            Require(profile.FindProperty("m_BuildTarget").intValue == (int)BuildTarget.Android
                && !profile.FindProperty("m_OverrideGlobalSceneList").boolValue, "Meta Quest profile targets Android and inherits the global scene list.");
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
    }
}
