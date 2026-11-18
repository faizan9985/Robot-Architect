using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable.Editor
{
    // Main menu (RemixMainMenu) in the spot where the ribbons sit, plus a small MENU button above the ribbons
    // (top left, level with UNDO / REDO). Idempotent; batch:
    //   -executeMethod RemixTable.Editor.RemixMainMenuLayout.Apply
    public static class RemixMainMenuLayout
    {
        public const string MenuName = "MainMenuCanvas", MenuButtonName = "MenuButtonCanvas";
        public static readonly Vector2 MenuSize = new Vector2(.64f, .50f), MenuButtonSize = new Vector2(.10f, .046f);
        static readonly Color Accent = new Color(.22f, .62f, .95f, 1), Heading = new Color(.55f, .8f, 1f), Body = new Color(.86f, .91f, .97f), Muted = new Color(.6f, .68f, .78f);

        [MenuItem("Tools/Remix Table/Add Main Menu")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            var field = RemixSceneBuilder.All<SpatialStemField>(scene).SingleOrDefault();
            var font = transport.songTitle.font;
            var d = manager.design;

            // ---- Menu card, centered on the ribbons ----
            var root = RemixModeLayout.Row(manager, transport, MenuName, 0, MenuSize);
            root.localPosition = new Vector3(0, (d.LaneY(0) + d.LaneY(3)) * .5f, transport.transform.localPosition.z - .01f);
            root.GetComponent<Image>().color = new Color(.016f, .022f, .036f, .96f);
            var menu = Get<RemixMainMenu>(root.gameObject);
            menu.manager = manager;
            menu.group = Get<CanvasGroup>(root.gameObject);
            menu.alsoHide = field != null && !field.transform.IsChildOf(manager.workspace) ? new[] { field.transform } : new Transform[0];
            var frame = RemixSeatedLayout.Rect(root, "Frame", Vector2.zero, MenuSize * 1000 + new Vector2(6, 6));
            frame.SetAsFirstSibling();
            var frameImage = Get<Image>(frame.gameObject); frameImage.color = new Color(Accent.r, Accent.g, Accent.b, .35f); frameImage.raycastTarget = false;
            var inner = RemixSeatedLayout.Rect(root, "Inner", Vector2.zero, MenuSize * 1000);
            inner.SetSiblingIndex(1);
            var innerImage = Get<Image>(inner.gameObject); innerImage.color = new Color(.016f, .022f, .036f, 1); innerImage.raycastTarget = false;
            // Living background: the loaded song's stems drift behind every page.
            var ribbonsRect = RemixSeatedLayout.Rect(root, "LivingRibbons", Vector2.zero, MenuSize * 1000 - new Vector2(8, 8));
            ribbonsRect.SetSiblingIndex(2);
            var ribbons = Get<RemixMenuRibbons>(ribbonsRect.gameObject);
            ribbons.manager = manager; ribbons.raycastTarget = false;
            menu.background = ribbons;
            EditorUtility.SetDirty(ribbons);

            // WELCOME
            var welcome = Page(root, "WelcomePage");
            menu.welcomePage = welcome;
            Text(welcome, "Title", "REMIX TABLE", new Vector2(0, 175), new Vector2(600, 64), 52, font, Color.white).fontStyle = FontStyles.Bold;
            Text(welcome, "Tagline", "SCULPT SOUND WITH YOUR HANDS", new Vector2(0, 132), new Vector2(600, 26), 16, font, Heading).characterSpacing = 8;
            menu.greeting = Text(welcome, "Greeting", "GOOD EVENING", new Vector2(0, 92), new Vector2(600, 32), 22, font, Body);
            menu.nowLoaded = Text(welcome, "NowLoaded", "LOADED:", new Vector2(0, 62), new Vector2(600, 24), 14, font, Muted);
            var glow = RemixSeatedLayout.Rect(welcome, "PlayGlow", new Vector2(0, -20), new Vector2(330, 94));
            var glowImage = Get<Image>(glow.gameObject); glowImage.color = new Color(Accent.r, Accent.g, Accent.b, .45f); glowImage.raycastTarget = false;
            menu.playGlow = glowImage;
            // CONTINUE (shown only when there is a session to resume; RemixMainMenu then moves PLAY below it).
            menu.continueButton = MakeButton(welcome, "Continue", "CONTINUE", new Vector2(0, 12), new Vector2(310, 64), 28, font, Accent);
            menu.continueDetail = Text(welcome, "ContinueDetail", "", new Vector2(0, -34), new Vector2(600, 22), 13, font, Heading);
            menu.playButton = MakeButton(welcome, "Play", "PLAY", new Vector2(0, -20), new Vector2(310, 76), 32, font, Accent);
            menu.songsButton = MakeButton(welcome, "SelectSong", "SELECT SONG", new Vector2(-118, -155), new Vector2(220, 52), 18, font);
            menu.howToButton = MakeButton(welcome, "HowToUse", "HOW TO USE", new Vector2(118, -155), new Vector2(220, 52), 18, font);
            Text(welcome, "Footer", "PRESS MENU ABOVE THE RIBBONS TO COME BACK HERE", new Vector2(0, -218), new Vector2(600, 22), 11, font, Muted);
            menu.continueButton.gameObject.SetActive(false); menu.continueDetail.gameObject.SetActive(false);

            // SELECT SONG
            var songs = Page(root, "SongsPage");
            menu.songsPage = songs;
            Text(songs, "Heading", "SELECT SONG", new Vector2(0, 200), new Vector2(600, 40), 28, font, Heading).fontStyle = FontStyles.Bold;
            Text(songs, "Hint", "MORE SONGS COMING SOON", new Vector2(0, 168), new Vector2(600, 22), 12, font, Muted);
            menu.songList = RemixSeatedLayout.Rect(songs, "SongList", Vector2.zero, MenuSize * 1000);
            var template = MakeButton(menu.songList, "SongTemplate", "01   SONG", new Vector2(0, 118), new Vector2(560, 50), 18, font);
            var title = template.transform.Find("Label") as RectTransform;
            title.anchoredPosition = new Vector2(-70, 0); title.sizeDelta = new Vector2(400, 44);
            title.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;
            var status = Text(template.transform, "Status", "READY", new Vector2(190, 0), new Vector2(170, 44), 13, font, Heading);
            status.alignment = TextAlignmentOptions.MidlineRight;
            menu.songTemplate = template;
            menu.songsBackButton = MakeButton(songs, "Back", "BACK", new Vector2(0, -205), new Vector2(180, 48), 18, font);

            // HOW TO USE
            var howTo = Page(root, "HowToPage");
            menu.howToPage = howTo;
            menu.howToTitle = Text(howTo, "Heading", "HOW TO USE", new Vector2(0, 190), new Vector2(600, 40), 26, font, Heading);
            menu.howToTitle.fontStyle = FontStyles.Bold;
            menu.howToBody = Text(howTo, "Body", "", new Vector2(0, 40), new Vector2(570, 230), 19, font, Body);
            menu.howToBody.textWrappingMode = TextWrappingModes.Normal; menu.howToBody.overflowMode = TextOverflowModes.Truncate;
            menu.howToBody.lineSpacing = 12;
            menu.howToPrevButton = MakeButton(howTo, "Prev", "PREV", new Vector2(-200, -140), new Vector2(150, 50), 18, font);
            menu.howToPageLabel = Text(howTo, "PageLabel", "1 / 6", new Vector2(0, -140), new Vector2(150, 30), 15, font, Muted);
            menu.howToNextButton = MakeButton(howTo, "Next", "NEXT", new Vector2(200, -140), new Vector2(150, 50), 18, font, Accent);
            menu.howToBackButton = MakeButton(howTo, "Back", "MAIN MENU", new Vector2(0, -205), new Vector2(200, 48), 18, font);
            songs.gameObject.SetActive(false); howTo.gameObject.SetActive(false); welcome.gameObject.SetActive(true);

            // ---- MENU button above the ribbons, top left (UNDO / REDO sit in the middle) ----
            var buttonRow = RemixModeLayout.Row(manager, transport, MenuButtonName, RemixModeLayout.HistoryCanvasY(d), MenuButtonSize);
            var p = buttonRow.localPosition; buttonRow.localPosition = new Vector3(-d.panelWidth * .5f + MenuButtonSize.x * .5f, p.y, p.z);
            buttonRow.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            menu.menuButton = MakeButton(buttonRow, "Menu", "MENU", Vector2.zero, MenuButtonSize * 1000, 18, font);

            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        static RectTransform Page(RectTransform root, string name)
        {
            var page = RemixSeatedLayout.Rect(root, name, Vector2.zero, MenuSize * 1000);
            page.SetAsLastSibling();
            return page;
        }
        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
        static TMP_Text Text(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset font, Color color)
        {
            var label = RemixSeatedLayout.Text(parent, name, text, pos, size, fontSize, font);
            label.color = color; EditorUtility.SetDirty(label);
            return label;
        }
        // Click handlers are bound by RemixMainMenu at runtime, so the button keeps no persistent listeners.
        static Button MakeButton(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset font, Color? fill = null)
        {
            var rect = RemixSeatedLayout.Rect(parent, name, pos, size);
            var image = Get<Image>(rect.gameObject); image.color = fill ?? new Color(.07f, .11f, .15f, 1); image.raycastTarget = true;
            var button = Get<Button>(rect.gameObject); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f); colors.pressedColor = new Color(.7f, .85f, 1);
            colors.selectedColor = Color.white; colors.disabledColor = new Color(.55f, .55f, .55f, .6f); colors.colorMultiplier = 1;
            button.colors = colors;
            while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
            var label = Text(rect, "Label", text, Vector2.zero, size - new Vector2(10, 6), fontSize, font, Color.white);
            label.fontStyle = FontStyles.Bold;
            return button;
        }

        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var menu = RemixSceneBuilder.All<RemixMainMenu>(scene).Single();
            RemixSceneBuilder.Require(menu.manager == manager && menu.group != null, "menu wired to the table");
            RemixSceneBuilder.Require(menu.transform.IsChildOf(manager.workspace), "menu moves with the table");
            var canvas = menu.GetComponent<Canvas>();
            RemixSceneBuilder.Require(canvas != null && canvas.renderMode == RenderMode.WorldSpace && menu.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null, "menu is pokeable world UI");
            RemixSceneBuilder.Require(menu.welcomePage != null && menu.songsPage != null && menu.howToPage != null, "menu pages");
            RemixSceneBuilder.Require(menu.greeting != null && menu.nowLoaded != null && menu.howToTitle != null && menu.howToBody != null && menu.howToPageLabel != null && menu.playGlow != null, "menu labels");
            RemixSceneBuilder.Require(menu.background != null && menu.background.manager == manager && !menu.background.raycastTarget && menu.background.transform.IsChildOf(menu.transform), "living ribbon background");
            RemixSceneBuilder.Require(menu.continueDetail != null, "continue detail label");
            foreach (var b in new[] { menu.playButton, menu.continueButton, menu.songsButton, menu.howToButton, menu.songsBackButton, menu.howToBackButton, menu.howToPrevButton, menu.howToNextButton, menu.menuButton })
                RemixSceneBuilder.Require(b != null && b.onClick.GetPersistentEventCount() == 0 && b.targetGraphic != null, "menu button " + (b != null ? b.name : "missing"));
            RemixSceneBuilder.Require(menu.songTemplate != null && menu.songList != null && menu.songTemplate.GetComponentsInChildren<TMP_Text>(true).Length == 2, "song row template has title + status");
            RemixSceneBuilder.Require(menu.howToPages != null && menu.howToPages.Length > 0, "how-to pages");
            var buttonCanvas = menu.menuButton.GetComponentInParent<Canvas>(true);
            RemixSceneBuilder.Require(buttonCanvas != null && buttonCanvas.transform != menu.transform && buttonCanvas.transform.IsChildOf(manager.workspace) && buttonCanvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null, "MENU button is pokeable and outside the menu");
            float top = manager.design.LaneY(0) + manager.design.laneHeight * .5f;
            RemixSceneBuilder.Require(buttonCanvas.transform.localPosition.y - MenuButtonSize.y * .5f > top, "MENU button sits above the ribbons");
            Debug.Log("REMIX_MAIN_MENU_PASS: welcome / select song / how to use pages and MENU button wired.");
        }
    }
}
