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
        public const string CanvasName = "ModeCanvas", PlaybackCanvasName = "PlaybackModeCanvas", HistoryCanvasName = "HistoryCanvas";
        public const float Height = .07f, Gap = .008f;
        public static float CanvasY(RemixDesign d) => d.TransportY - d.transportHeight * .5f - Gap - Height * .5f;
        // EDIT / LIVE row sits directly under the VOLUME / FILTER row.
        public static float PlaybackCanvasY(RemixDesign d) => CanvasY(d) - Height - Gap;
        // UNDO / REDO row sits directly under the EDIT / LIVE row.
        public static float HistoryCanvasY(RemixDesign d) => PlaybackCanvasY(d) - Height - Gap;
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
        // Same menu step; both rows are idempotent, so either item (re)builds both.
        [MenuItem("Tools/Remix Table/Add Live-Edit Playback Buttons")]
        public static void ApplyPlayback() => Apply();
        [MenuItem("Tools/Remix Table/Add Undo-Redo Buttons")]
        public static void ApplyHistory() => Apply();
        static RectTransform Row(RemixTableManager manager, RemixTransport transport, string name, float y)
        {
            var d = manager.design;
            var transportCanvas = transport.GetComponent<Canvas>();
            var rect = RemixSeatedLayout.Rect(manager.workspace, name, Vector2.zero, new Vector2(d.panelWidth * 1000, Height * 1000));
            rect.localScale = Vector3.one * .001f; rect.localRotation = Quaternion.identity;
            rect.localPosition = new Vector3(0, y, transport.transform.localPosition.z);
            var canvas = rect.GetComponent<Canvas>(); if (canvas == null) canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = transportCanvas.worldCamera;
            canvas.additionalShaderChannels = transportCanvas.additionalShaderChannels;
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var background = rect.GetComponent<Image>(); if (background == null) background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(.018f, .026f, .04f, 1); background.raycastTarget = false;
            return rect;
        }
        public static RemixModeSelector Build(RemixTableManager manager, RemixTransport transport)
        {
            var d = manager.design; var font = transport.songTitle.font;
            var rect = Row(manager, transport, CanvasName, CanvasY(d));
            var selector = rect.GetComponent<RemixModeSelector>(); if (selector == null) selector = rect.gameObject.AddComponent<RemixModeSelector>();
            selector.manager = manager;
            RemixSeatedLayout.Text(rect, "ModeHeading", "PINCH EDITS", new Vector2(-235, 0), new Vector2(170, 40), 15, font);
            selector.volumeButton = RemixSeatedLayout.Button(rect, "VolumeMode", "VOLUME", -40, 180, font, selector.SelectVolume);
            selector.filterButton = RemixSeatedLayout.Button(rect, "FilterMode", "FILTER", 160, 180, font, selector.SelectFilter);
            foreach (var b in new[] { selector.volumeButton, selector.filterButton }) ((RectTransform)b.transform).anchoredPosition = new Vector2(((RectTransform)b.transform).anchoredPosition.x, 0);
            selector.volumeButton.image.color = selector.selectedColor; selector.filterButton.image.color = selector.idleColor;
            transport.palmInstructions.text = RemixTransport.Guidance;
            EditorUtility.SetDirty(selector); EditorUtility.SetDirty(transport.palmInstructions);
            BuildPlayback(manager, transport);
            BuildHistory(manager, transport);
            return selector;
        }
        public static RemixPlaybackModeSelector BuildPlayback(RemixTableManager manager, RemixTransport transport)
        {
            var font = transport.songTitle.font;
            var rect = Row(manager, transport, PlaybackCanvasName, PlaybackCanvasY(manager.design));
            var selector = rect.GetComponent<RemixPlaybackModeSelector>(); if (selector == null) selector = rect.gameObject.AddComponent<RemixPlaybackModeSelector>();
            selector.manager = manager;
            RemixSeatedLayout.Text(rect, "PlaybackHeading", "PLAYBACK", new Vector2(-235, 0), new Vector2(170, 40), 15, font);
            selector.editButton = RemixSeatedLayout.Button(rect, "EditMode", "EDIT: PAUSE", -40, 180, font, selector.SelectEdit);
            selector.liveButton = RemixSeatedLayout.Button(rect, "LiveMode", "LIVE: PLAY", 160, 180, font, selector.SelectLive);
            foreach (var b in new[] { selector.editButton, selector.liveButton }) ((RectTransform)b.transform).anchoredPosition = new Vector2(((RectTransform)b.transform).anchoredPosition.x, 0);
            bool live = manager.audioManager != null && manager.audioManager.LiveMode;
            selector.editButton.image.color = live ? selector.idleColor : selector.selectedColor;
            selector.liveButton.image.color = live ? selector.selectedColor : selector.idleColor;
            EditorUtility.SetDirty(selector);
            return selector;
        }
        public static RemixHistoryControls BuildHistory(RemixTableManager manager, RemixTransport transport)
        {
            var font = transport.songTitle.font;
            var rect = Row(manager, transport, HistoryCanvasName, HistoryCanvasY(manager.design));
            var controls = rect.GetComponent<RemixHistoryControls>(); if (controls == null) controls = rect.gameObject.AddComponent<RemixHistoryControls>();
            controls.manager = manager;
            RemixSeatedLayout.Text(rect, "HistoryHeading", "HISTORY", new Vector2(-235, 0), new Vector2(170, 40), 15, font);
            controls.undoButton = RemixSeatedLayout.Button(rect, "Undo", "UNDO", -40, 180, font, controls.Undo);
            controls.redoButton = RemixSeatedLayout.Button(rect, "Redo", "REDO", 160, 180, font, controls.Redo);
            foreach (var b in new[] { controls.undoButton, controls.redoButton })
            {
                ((RectTransform)b.transform).anchoredPosition = new Vector2(((RectTransform)b.transform).anchoredPosition.x, 0);
                var colors = b.colors; colors.disabledColor = new Color(.35f, .35f, .35f, .5f); b.colors = colors;
                b.interactable = false; // Nothing to undo until the first edit.
            }
            EditorUtility.SetDirty(controls);
            return controls;
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
            var playback = RemixSceneBuilder.All<RemixPlaybackModeSelector>(scene).Single();
            RemixSceneBuilder.Require(playback.manager == manager && manager.audioManager != null, "playback selector wired to manager");
            RemixSceneBuilder.Require(playback.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace && playback.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "playback canvas is pokeable world UI");
            foreach (var (button, method) in new[] { (playback.editButton, nameof(RemixPlaybackModeSelector.SelectEdit)), (playback.liveButton, nameof(RemixPlaybackModeSelector.SelectLive)) })
                RemixSceneBuilder.Require(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == playback && button.onClick.GetPersistentMethodName(0) == method, "playback binding " + button.name);
            var history = RemixSceneBuilder.All<RemixHistoryControls>(scene).Single();
            RemixSceneBuilder.Require(history.manager == manager, "history controls wired to manager");
            RemixSceneBuilder.Require(history.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace && history.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "history canvas is pokeable world UI");
            foreach (var (button, method) in new[] { (history.undoButton, nameof(RemixHistoryControls.Undo)), (history.redoButton, nameof(RemixHistoryControls.Redo)) })
                RemixSceneBuilder.Require(button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == history && button.onClick.GetPersistentMethodName(0) == method, "history binding " + button.name);
            Debug.Log("REMIX_MODE_VALIDATION_PASS");
        }
    }
}
