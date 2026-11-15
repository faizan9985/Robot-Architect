using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
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
        // UNDO / REDO: a compact pair of icon buttons centered above the ribbon panel. The gap keeps the buttons
        // well outside the top (Vocals) ribbon's pinch radius so poking them can never grab a ribbon.
        public const float IconButtonWidth = .05f, IconButtonHeight = .04f, IconPad = .006f, HistoryTopGap = .02f, IconSize = .026f;
        public static Vector2 HistorySize => new Vector2(2 * IconButtonWidth + 3 * IconPad, IconButtonHeight + 2 * IconPad);
        public static float HistoryCanvasY(RemixDesign d) => d.panelHeight * .5f + HistoryTopGap + HistorySize.y * .5f;
        public const string IconFolder = "Assets/RemixTable/UI/Icons", UndoIconPath = IconFolder + "/Undo.png", RedoIconPath = IconFolder + "/Redo.png";
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
        static RectTransform Row(RemixTableManager manager, RemixTransport transport, string name, float y) =>
            Row(manager, transport, name, y, new Vector2(manager.design.panelWidth, Height));
        static RectTransform Row(RemixTableManager manager, RemixTransport transport, string name, float y, Vector2 size)
        {
            var transportCanvas = transport.GetComponent<Canvas>();
            var rect = RemixSeatedLayout.Rect(manager.workspace, name, Vector2.zero, size * 1000);
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
            var (undoIcon, redoIcon) = EnsureIcons();
            var rect = Row(manager, transport, HistoryCanvasName, HistoryCanvasY(manager.design), HistorySize);
            var stale = rect.Find("HistoryHeading"); if (stale != null) Object.DestroyImmediate(stale.gameObject); // Old text row.
            var controls = rect.GetComponent<RemixHistoryControls>(); if (controls == null) controls = rect.gameObject.AddComponent<RemixHistoryControls>();
            controls.manager = manager;
            float x = (IconButtonWidth + IconPad) * 500;
            controls.undoButton = IconButton(rect, "Undo", undoIcon, -x, controls.Undo, out controls.undoIcon);
            controls.redoButton = IconButton(rect, "Redo", redoIcon, x, controls.Redo, out controls.redoIcon);
            controls.Paint(); // Both start dimmed: nothing to undo until the first edit.
            EditorUtility.SetDirty(controls);
            return controls;
        }
        static Button IconButton(RectTransform parent, string name, Sprite icon, float x, UnityAction action, out Image glyph)
        {
            var rect = RemixSeatedLayout.Rect(parent, name, new Vector2(x, 0), new Vector2(IconButtonWidth, IconButtonHeight) * 1000);
            var label = rect.Find("Label"); if (label != null) Object.DestroyImmediate(label.gameObject); // Old text label.
            var image = rect.GetComponent<Image>(); if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.07f, .11f, .15f, 1); image.raycastTarget = true;
            var button = rect.GetComponent<Button>(); if (button == null) button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.highlightedColor = new Color(.65f, .85f, 1); colors.pressedColor = new Color(.35f, .65f, .85f);
            colors.disabledColor = new Color(.8f, .8f, .8f, 1); button.colors = colors; // Dimmed, but the button shape stays visible.
            while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var iconRect = RemixSeatedLayout.Rect(rect, "Icon", Vector2.zero, Vector2.one * IconSize * 1000);
            glyph = iconRect.GetComponent<Image>(); if (glyph == null) glyph = iconRect.gameObject.AddComponent<Image>();
            glyph.sprite = icon; glyph.preserveAspect = true; glyph.raycastTarget = false;
            return button;
        }
        // Curved-arrow icons drawn in code (anti-aliased signed distance), so they need no external art and
        // regenerate byte-identically. Redo is the mirror image of undo.
        static (Sprite undo, Sprite redo) EnsureIcons()
        {
            System.IO.Directory.CreateDirectory(IconFolder);
            return (WriteIcon(UndoIconPath, false), WriteIcon(RedoIconPath, true));
        }
        static Sprite WriteIcon(string path, bool mirror)
        {
            const int N = 128;
            var c = new Vector2(73, 60); const float r = 30, stroke = 10;
            float a0 = -40 * Mathf.Deg2Rad;                                  // Arc runs counter-clockwise over the top to 180 deg.
            Vector2 end = new Vector2(c.x - r, c.y), start = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
            Vector2[] head = { new Vector2(end.x, end.y - 26), new Vector2(end.x + 17, end.y + 2), new Vector2(end.x - 17, end.y + 2) }; // Points down (CCW).
            var pixels = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                var p = new Vector2(x + .5f, y + .5f); var v = p - c;
                float arc = Mathf.Atan2(v.y, v.x) >= a0 ? Mathf.Abs(v.magnitude - r) - stroke * .5f : Vector2.Distance(p, start) - stroke * .5f;
                float tri = float.MinValue;
                for (int i = 0; i < 3; i++)
                {
                    var e = head[(i + 1) % 3] - head[i]; var n = new Vector2(e.y, -e.x).normalized;
                    tri = Mathf.Max(tri, Vector2.Dot(p - head[i], n));
                }
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - Mathf.Min(arc, tri)) * 255);
                pixels[y * N + (mirror ? N - 1 - x : x)] = new Color32(255, 255, 255, alpha);
            }
            var texture = new Texture2D(N, N, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            RemixSceneBuilder.Require(history.undoIcon != null && history.undoIcon.sprite != null && history.redoIcon != null && history.redoIcon.sprite != null, "history buttons show icons");
            RemixSceneBuilder.Require(history.transform.localPosition.y - HistorySize.y * .5f > manager.design.panelHeight * .5f, "history row sits above the ribbon panel");
            Debug.Log("REMIX_MODE_VALIDATION_PASS");
        }
    }
}
