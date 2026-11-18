using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable.Editor
{
    // Add source-branch features to the authored main scene, retaining its XR prefab and local file IDs.
    public static class RemixIntegrationValidation
    {
        public static void Integrate()
        {
            RemixRoomLayout.Apply();
            RemixSidePanelsLayout.Apply();
            RemixMainMenuLayout.Apply();
            RemixSongSetup.Apply();
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            manager.accessibility = RemixSceneBuilder.All<RemixAccessibility>(scene).Single();
            EditorUtility.SetDirty(manager);
            foreach(var b in manager.workspace.GetComponentsInChildren<Button>(true))
                if(b.GetComponentInParent<RemixVibes>(true)!=null || b.GetComponentInParent<RemixDailyDrop>(true)!=null)
                {
                    var image=b.GetComponent<Image>();image.sprite=RemixUiPresentation.Sprite("Surface");image.type=Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier=2.5f;EditorUtility.SetDirty(image);
                }
            manager.workspaceInteraction.uiCanvases = manager.workspace.GetComponentsInChildren<Canvas>(true);
            EditorUtility.SetDirty(manager.workspaceInteraction);
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/RemixTable/Voice/RemixWitConfig.asset");
            RemixSceneBuilder.Require(config != null, "existing voice configuration imported");
            int wired = 0;
            foreach (var component in RemixSceneBuilder.All<MonoBehaviour>(scene))
            {
                var data = new SerializedObject(component);
                var field = data.FindProperty("runtimeConfiguration.witConfiguration");
                if (field == null) continue;
                field.objectReferenceValue = config;
                data.ApplyModifiedPropertiesWithoutUndo(); wired++;
            }
            RemixSceneBuilder.Require(wired == 1, "one dictation experience configured");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }
        public static void Validate()
        {
            VocalsBuildValidation.Validate();
            RemixRoomLayout.Validate();
            RemixSidePanelsLayout.Validate();
            RemixMainMenuLayout.Validate();
            RemixSongSetup.Validate();
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            RemixSceneBuilder.Require(manager.workspaceInteraction.uiCanvases.Length == manager.workspace.GetComponentsInChildren<Canvas>(true).Length, "all UI participates in workspace gesture ownership");
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var name in new[]{"HandTracking", "MetaHandTrackingAim"})
                RemixSceneBuilder.Require(settings.GetFeatures().Any(f => f.GetType().Name.Contains(name) && f.enabled), "enabled " + name);
            Debug.Log("REMIX_INTEGRATION_VALIDATION_PASS");
        }
        public static void Build() { Validate(); VocalsBuildValidation.Build(); }
    }
}
