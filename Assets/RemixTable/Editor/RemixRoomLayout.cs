using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;
namespace RemixTable.Editor
{
    // Adds ROOM mode to the Remix Table scene: Meta Quest plane + bounding box detection (Space Setup data via
    // AR Foundation on the existing XR Origin, so the XRI / OpenXR rig is untouched), the RemixRoom object
    // (scanner, placer, room visuals) and the RoomGlow material. Idempotent; batch-mode friendly:
    //   Unity.exe -batchmode -quit -projectPath <p> -executeMethod RemixTable.Editor.RemixRoomLayout.Apply
    public static class RemixRoomLayout
    {
        public const string RootName = "RemixRoom", MaterialPath = "Assets/RemixTable/Materials/RoomGlow.mat", ShaderName = "RemixTable/RoomGlow";

        [MenuItem("Tools/Remix Table/Add ROOM Mode (Space Setup Placement)")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnableOpenXRFeatures();
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var toolbox = RemixSceneBuilder.All<RemixToolbox>(scene).Single();
            var field = RemixSceneBuilder.All<SpatialStemField>(scene).SingleOrDefault();
            var passthrough = RemixSceneBuilder.All<PassthroughController>(scene).Single();
            var session = RemixSceneBuilder.All<ARSession>(scene).Single();
            var origin = RemixSceneBuilder.All<XROrigin>(scene).Single(o => o.gameObject.activeInHierarchy);

            // Trackable managers live on the XR Origin (AR Foundation requirement) and stay off until ROOM is on.
            var planes = origin.GetComponent<ARPlaneManager>(); if (planes == null) planes = origin.gameObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            planes.enabled = false; EditorUtility.SetDirty(planes);
            var boxes = origin.GetComponent<ARBoundingBoxManager>(); if (boxes == null) boxes = origin.gameObject.AddComponent<ARBoundingBoxManager>();
            boxes.enabled = false; EditorUtility.SetDirty(boxes);

            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root == null) { root = new GameObject(RootName); EditorSceneManager.MoveGameObjectToScene(root, scene); }
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;

            var visuals = Get<RemixRoomVisuals>(root);
            visuals.manager = manager; visuals.spatialField = field; visuals.glowMaterial = EnsureMaterial();
            EditorUtility.SetDirty(visuals);
            var placer = Get<RemixRoomPlacer>(root);
            placer.manager = manager; placer.visuals = visuals; placer.toolbox = toolbox;
            EditorUtility.SetDirty(placer);
            var scanner = Get<RemixRoomScanner>(root);
            var data = new SerializedObject(scanner);
            data.FindProperty("passthrough").objectReferenceValue = passthrough;
            data.FindProperty("session").objectReferenceValue = session;
            data.FindProperty("planeManager").objectReferenceValue = planes;
            data.FindProperty("boxManager").objectReferenceValue = boxes;
            data.FindProperty("placer").objectReferenceValue = placer;
            data.FindProperty("toolbox").objectReferenceValue = toolbox;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }

        static Material EnsureMaterial()
        {
            var shader = Shader.Find(ShaderName);
            RemixSceneBuilder.Require(shader != null, ShaderName + " shader is imported");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader) { name = "RoomGlow" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = shader;
            material.renderQueue = 3010;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Meta Quest: Planes and Meta Quest: Bounding Boxes (they also add the USE_SCENE permission to the manifest).
        static void EnableOpenXRFeatures()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            RemixSceneBuilder.Require(settings != null, "Android OpenXR settings exist");
            foreach (var feature in settings.GetFeatures())
            {
                if (feature is ARPlaneFeature || feature is ARBoundingBoxFeature)
                {
                    feature.enabled = true; EditorUtility.SetDirty(feature);
                }
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            RemixSceneBuilder.Require(root != null, "RemixRoom object exists");
            var visuals = root.GetComponent<RemixRoomVisuals>(); var placer = root.GetComponent<RemixRoomPlacer>(); var scanner = root.GetComponent<RemixRoomScanner>();
            RemixSceneBuilder.Require(visuals != null && placer != null && scanner != null, "RemixRoom has visuals, placer and scanner");
            RemixSceneBuilder.Require(visuals.manager != null && visuals.glowMaterial != null && visuals.glowMaterial.shader != null && visuals.glowMaterial.shader.isSupported, "room visuals wired with a supported RoomGlow material");
            RemixSceneBuilder.Require(placer.manager != null && placer.visuals == visuals && placer.toolbox != null, "room placer wired");
            var data = new SerializedObject(scanner);
            foreach (var name in new[] { "passthrough", "session", "planeManager", "boxManager", "placer", "toolbox" })
                RemixSceneBuilder.Require(data.FindProperty(name).objectReferenceValue != null, "room scanner field " + name + " assigned");
            var planes = (ARPlaneManager)data.FindProperty("planeManager").objectReferenceValue;
            RemixSceneBuilder.Require(!planes.enabled, "plane detection starts off (only runs in ROOM mode)");
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            RemixSceneBuilder.Require(settings.GetFeature<ARPlaneFeature>() != null && settings.GetFeature<ARPlaneFeature>().enabled, "Meta Quest: Planes OpenXR feature enabled for Android");
            RemixSceneBuilder.Require(settings.GetFeature<ARBoundingBoxFeature>() != null && settings.GetFeature<ARBoundingBoxFeature>().enabled, "Meta Quest: Bounding Boxes OpenXR feature enabled for Android");
            Debug.Log("REMIX_ROOM_LAYOUT_PASS: scanner, placer, visuals and OpenXR scene features configured.");
        }
    }
}
