using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace RemixTable.Editor
{
    // Adds the VOLUME / FILTER mode row under the transport bar. Idempotent and touches nothing else,
    // so it can run on the existing scene without re-applying the whole seated layout.
    public static class RemixModeLayout
    {
        public const string CanvasName = "ModeCanvas";
        public const float Height = .07f, Gap = .008f;
        public static float CanvasY(RemixDesign d) => d.TransportY - d.transportHeight * .5f - Gap - Height * .5f;
        [MenuItem("Tools/Remix Table/Add Volume-Filter Mode Buttons")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            Build(manager, transport);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }
        public static RemixModeSelector Build(RemixTableManager manager, RemixTransport transport)
        {
            var d = manager.design; var font = transport.songTitle.font;
            var transportCanvas = transport.GetComponent<Canvas>();
            var rect = RemixSeatedLayout.Rect(manager.workspace, CanvasName, Vector2.zero, new Vector2(d.panelWidth * 1000, Height * 1000));
            rect.localScale = Vector3.one * .001f; rect.localRotation = Quaternion.identity;
            rect.localPosition = new Vector3(0, CanvasY(d), transport.transform.localPosition.z);
            var canvas = rect.GetComponent<Canvas>(); if (canvas == null) canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = transportCanvas.worldCamera;
            canvas.additionalShaderChannels = transportCanvas.additionalShaderChannels;
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var background = rect.GetComponent<Image>(); if (background == null) background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.018f, .026f, .04f, 1); background.raycastTarget = false;
            var selector = rect.GetComponent<RemixModeSelector>(); if (selector == null) selector = rect.gameObject.AddComponent<RemixModeSelector>();
            selector.manager = manager;
            RemixSeatedLayout.Text(rect, "ModeHeading", "PINCH EDITS", new Vector2(-235, 0), new Vector2(170, 40), 15, font);
            selector.volumeButton = RemixSeatedLayout.Button(rect, "VolumeMode", "VOLUME", -40, 180, font, selector.SelectVolume);
            selector.filterButton = RemixSeatedLayout.Button(rect, "FilterMode", "FILTER", 160, 180, font, selector.SelectFilter);
            foreach (var b in new[] { selector.volumeButton, selector.filterButton }) ((RectTransform)b.transform).anchoredPosition = new Vector2(((RectTransform)b.transform).anchoredPosition.x, 0);
            selector.volumeButton.image.color = selector.selectedColor; selector.filterButton.image.color = selector.idleColor;
            transport.palmInstructions.text = RemixTransport.Guidance;
            EditorUtility.SetDirty(selector); EditorUtility.SetDirty(transport.palmInstructions);
            return selector;
        }
        public static void Validate()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var selector = RemixSceneBuilder.All<RemixModeSelector>(scene).Single();
            RemixSceneBuilder.Require(manager.interactions != null && selector.manager == manager, "mode selector wired to manager");
            RemixSceneBuilder.Require(selector.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace && selector.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "mode canvas is pokeable world UI");
            foreach (var (button, method) in new[] { (selector.volumeButton, nameof(RemixModeSelector.SelectVolume)), (selector.filterButton, nameof(RemixModeSelector.SelectFilter)) })
                RemixSceneBuilder.Require(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == selector && button.onClick.GetPersistentMethodName(0) == method, "mode binding " + button.name);
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            RemixSceneBuilder.Require(transport.palmInstructions.text == RemixTransport.Guidance, "palm guidance mentions modes");
            Debug.Log("REMIX_MODE_VALIDATION_PASS");
        }
    }
}
