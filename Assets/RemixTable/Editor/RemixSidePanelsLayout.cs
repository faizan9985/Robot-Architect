using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace RemixTable.Editor
{
    // Side panels on the ribbon arc, below the ribbons, angled to face the listener:
    //   left  (-38 deg): DAILY DROP  - today's challenge, progress, streak, NEXT DROP, THEME
    //   right (+38 deg): VIBES (CHILL / HYPE / KARAOKE / BREAKDOWN / NORMAL) and ACCESS (HI CONTRAST / CLOSER)
    // Also hides silent placeholder songs from NEXT / PREVIOUS. Idempotent; batch:
    //   -executeMethod RemixTable.Editor.RemixSidePanelsLayout.Apply
    public static class RemixSidePanelsLayout
    {
        public const string DailyName = "DailyDropCanvas", VibesName = "VibesCanvas";
        public const float PanelAngle = 38, PanelY = -.41f;
        public static readonly Vector2 PanelSize = new Vector2(.30f, .30f);

        [MenuItem("Tools/Remix Table/Add Daily Drop, Vibes and Access Panels")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            var toolbox = RemixSceneBuilder.All<RemixToolbox>(scene).Single();
            var field = RemixSceneBuilder.All<SpatialStemField>(scene).SingleOrDefault();
            var visuals = RemixSceneBuilder.All<RemixRoomVisuals>(scene).SingleOrDefault();
            var placer = RemixSceneBuilder.All<RemixRoomPlacer>(scene).SingleOrDefault();
            var font = transport.songTitle.font;

            manager.library.skipSilentSongs = true; EditorUtility.SetDirty(manager.library);
            RemixFeatureLayout.DetectTempo(manager); // Re-run with the joint period + phase fit (SNAP, beat ripples).
            foreach (var s in manager.library.songs) if (SongLibraryManager.HasAudio(s)) Debug.Log($"REMIX_TEMPO {s.id}: {s.bpm:0.00} BPM, first beat {s.beatOffset:0.000}s");

            // ---- VIBES + ACCESS (right) ----
            var right = Panel(manager, transport, VibesName, PanelAngle);
            var vibes = Get<RemixVibes>(right.gameObject);
            vibes.manager = manager; vibes.toolbox = toolbox;
            Heading(right, "VibesHeading", "VIBES", 125, font);
            vibes.chillButton = Button(right, "Chill", "CHILL", -72, 80, 136, font, Action(vibes, nameof(RemixVibes.Chill)));
            vibes.hypeButton = Button(right, "Hype", "HYPE", 72, 80, 136, font, Action(vibes, nameof(RemixVibes.Hype)));
            vibes.karaokeButton = Button(right, "Karaoke", "KARAOKE", -72, 25, 136, font, Action(vibes, nameof(RemixVibes.Karaoke)));
            vibes.breakdownButton = Button(right, "Breakdown", "BREAKDOWN", 72, 25, 136, font, Action(vibes, nameof(RemixVibes.Breakdown)));
            vibes.normalButton = Button(right, "Normal", "BACK TO MY MIX", 0, -30, 280, font, Action(vibes, nameof(RemixVibes.Normal)));
            Heading(right, "AccessHeading", "ACCESS", -75, font);
            var access = Get<RemixAccessibility>(right.gameObject);
            access.manager = manager; access.toolbox = toolbox; access.placer = placer; access.roomVisuals = visuals;
            access.contrastButton = Button(right, "Contrast", "HI CONTRAST", -72, -120, 136, font, Action(access, nameof(RemixAccessibility.ToggleContrast)));
            access.reachButton = Button(right, "Reach", "CLOSER", 72, -120, 136, font, Action(access, nameof(RemixAccessibility.ToggleReach)));
            toolbox.vibes = vibes;
            EditorUtility.SetDirty(vibes); EditorUtility.SetDirty(access); EditorUtility.SetDirty(toolbox);

            // ---- DAILY DROP (left) ----
            var left = Panel(manager, transport, DailyName, -PanelAngle);
            var daily = Get<RemixDailyDrop>(left.gameObject);
            daily.manager = manager; daily.toolbox = toolbox; daily.vibes = vibes; daily.spatialField = field; daily.roomVisuals = visuals;
            daily.panel = left.GetComponent<Image>();
            daily.heading = Text(left, "DropHeading", "DAILY DROP", new Vector2(0, 125), new Vector2(280, 28), 15, font, daily.accent);
            daily.title = Text(left, "DropTitle", "KARAOKE NIGHT", new Vector2(0, 94), new Vector2(280, 34), 24, font, Color.white);
            daily.description = Text(left, "DropDescription", "", new Vector2(0, 42), new Vector2(280, 68), 12.5f, font, new Color(.8f, .88f, .94f));
            daily.description.overflowMode = TextOverflowModes.Truncate; daily.description.textWrappingMode = TextWrappingModes.Normal;
            var track = RemixSeatedLayout.Rect(left, "ProgressTrack", new Vector2(0, -6), new Vector2(280, 12));
            var trackImage = Get<Image>(track.gameObject); trackImage.color = new Color(.1f, .14f, .19f, 1); trackImage.raycastTarget = false;
            var fill = RemixSeatedLayout.Rect(track, "ProgressFill", Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(.001f, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            daily.progressFill = Get<Image>(fill.gameObject); daily.progressFill.color = daily.accent; daily.progressFill.raycastTarget = false;
            daily.streakLabel = Text(left, "DropStreak", "STREAK 0", new Vector2(0, -34), new Vector2(280, 22), 13, font, Color.white);
            daily.themeLabel = Text(left, "DropTheme", "CLASSIC", new Vector2(0, -58), new Vector2(280, 20), 11.5f, font, new Color(.7f, .78f, .86f));
            daily.nextButton = Button(left, "NextDrop", "NEXT DROP", -72, -110, 136, font, Action(daily, nameof(RemixDailyDrop.NextDrop)));
            daily.themeButton = Button(left, "Theme", "THEME", 72, -110, 136, font, Action(daily, nameof(RemixDailyDrop.CycleTheme)));
            EditorUtility.SetDirty(daily);

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        // A world-space panel on the ribbon circle at `angle` degrees, turned to face the listener's head.
        static RectTransform Panel(RemixTableManager manager, RemixTransport transport, string name, float angle)
        {
            var rect = RemixModeLayout.Row(manager, transport, name, PanelY, PanelSize);
            float r = -manager.HeadAnchorLocal.z, a = angle * Mathf.Deg2Rad;
            rect.localPosition = new Vector3(Mathf.Sin(a) * r, PanelY, (Mathf.Cos(a) - 1) * r);
            rect.localRotation = Quaternion.Euler(0, angle, 0);
            return rect;
        }
        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
        static void Heading(RectTransform parent, string name, string text, float y, TMP_FontAsset font) =>
            Text(parent, name, text, new Vector2(0, y), new Vector2(280, 28), 15, font, new Color(.55f, .8f, 1f));
        static TMP_Text Text(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset font, Color color)
        {
            var label = RemixSeatedLayout.Text(parent, name, text, pos, size, fontSize, font);
            label.color = color; EditorUtility.SetDirty(label);
            return label;
        }
        static Button Button(RectTransform parent, string name, string text, float x, float y, float width, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
        {
            var b = RemixSeatedLayout.Button(parent, name, text, x, width, font, action);
            var r = (RectTransform)b.transform; r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, 46);
            var label = r.Find("Label") as RectTransform;
            if (label != null) { label.sizeDelta = new Vector2(width - 8, 40); var t = label.GetComponent<TMP_Text>(); t.fontSize = text.Length > 10 ? 15 : 17; }
            return b;
        }
        static UnityEngine.Events.UnityAction Action<T>(T target, string method) where T : Object =>
            (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);

        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            RemixSceneBuilder.Require(manager.library.skipSilentSongs, "silent placeholder songs are skipped");
            RemixSceneBuilder.Require(SongLibraryManager.HasAudio(manager.library.songs[manager.library.selectedIndex]), "the starting song has audio");
            var vibes = RemixSceneBuilder.All<RemixVibes>(scene).Single();
            var access = RemixSceneBuilder.All<RemixAccessibility>(scene).Single();
            var daily = RemixSceneBuilder.All<RemixDailyDrop>(scene).Single();
            var toolbox = RemixSceneBuilder.All<RemixToolbox>(scene).Single();
            RemixSceneBuilder.Require(vibes.manager == manager && toolbox.vibes == vibes, "vibes wired");
            RemixSceneBuilder.Require(access.manager == manager && access.toolbox != null, "access wired");
            RemixSceneBuilder.Require(daily.manager == manager && daily.vibes == vibes && daily.progressFill != null && daily.title != null, "daily drop wired");
            foreach (var (button, target, method) in new (Button, Object, string)[] {
                (vibes.chillButton, vibes, nameof(RemixVibes.Chill)), (vibes.hypeButton, vibes, nameof(RemixVibes.Hype)), (vibes.karaokeButton, vibes, nameof(RemixVibes.Karaoke)),
                (vibes.breakdownButton, vibes, nameof(RemixVibes.Breakdown)), (vibes.normalButton, vibes, nameof(RemixVibes.Normal)),
                (access.contrastButton, access, nameof(RemixAccessibility.ToggleContrast)), (access.reachButton, access, nameof(RemixAccessibility.ToggleReach)),
                (daily.nextButton, daily, nameof(RemixDailyDrop.NextDrop)), (daily.themeButton, daily, nameof(RemixDailyDrop.CycleTheme)) })
                RemixSceneBuilder.Require(button != null && button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == target && button.onClick.GetPersistentMethodName(0) == method, "binding " + method);
            foreach (var c in new Component[] { vibes, daily })
            {
                var canvas = c.GetComponent<Canvas>();
                RemixSceneBuilder.Require(canvas != null && canvas.renderMode == RenderMode.WorldSpace && c.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null, c.name + " is pokeable world UI");
                RemixSceneBuilder.Require(c.transform.localPosition.y + PanelSize.y * .5f < manager.design.LaneY(3) - manager.design.laneHeight * .5f, c.name + " sits below the ribbons");
            }
            Debug.Log("REMIX_SIDE_PANELS_PASS: daily drop, vibes, access panels wired; silent songs skipped.");
        }
    }
}
