using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable.Editor
{
    // Main menu (RemixMainMenu): floating text and rounded pill buttons in the spot where the ribbons sit, with no
    // card behind them, a soft shadow for legibility over passthrough, and the living stem ribbons (RemixMenuAura)
    // arcing through the room around the listener. Also a small MENU pill above the ribbons (top left, level with
    // UNDO / REDO). Generates its sprites under UI/Menu. Idempotent; batch:
    //   -executeMethod RemixTable.Editor.RemixMainMenuLayout.Apply
    public static class RemixMainMenuLayout
    {
        public const string MenuName = "MainMenuCanvas", MenuButtonName = "MenuButtonCanvas", AuraName = "MenuAura";
        public const string SpriteFolder = "Assets/RemixTable/UI/Menu", PillPath = SpriteFolder + "/Pill.png", RingPath = SpriteFolder + "/PillRing.png",
            GlowPath = SpriteFolder + "/Glow.png", RulePath = SpriteFolder + "/Rule.png", AuraMaterialPath = "Assets/RemixTable/Materials/MenuAura.mat";
        public static readonly Vector2 MenuSize = new Vector2(.72f, .60f), MenuButtonSize = new Vector2(.10f, .042f);
        const int PillRadius = 64;
        static readonly Color Body = new Color(.88f, .92f, .98f), Muted = new Color(.62f, .68f, .80f), Tag = new Color(.62f, .82f, 1f);
        static Sprite pill, ring, glow, rule;

        [MenuItem("Tools/Remix Table/Add Main Menu")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureSprites();
            var auraMaterial = EnsureAuraMaterial();
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            var field = RemixSceneBuilder.All<SpatialStemField>(scene).SingleOrDefault();
            var font = transport.songTitle.font;
            var d = manager.design;

            // ---- Floating menu, centered on the ribbons; the canvas itself draws nothing ----
            var root = RemixModeLayout.Row(manager, transport, MenuName, 0, MenuSize);
            root.localPosition = new Vector3(0, (d.LaneY(0) + d.LaneY(3)) * .5f, transport.transform.localPosition.z - .01f);
            root.GetComponent<Image>().color = Color.clear;
            foreach (var stale in new[] { "Frame", "Inner", "LivingRibbons" }) { var t = root.Find(stale); if (t != null) Object.DestroyImmediate(t.gameObject); }
            var menu = Get<RemixMainMenu>(root.gameObject);
            menu.manager = manager;
            menu.group = Get<CanvasGroup>(root.gameObject);
            menu.alsoHide = field != null && !field.transform.IsChildOf(manager.workspace) ? new[] { field.transform } : new Transform[0];
            // Feathered shadow (no edges) so white text stays readable over a bright room in passthrough.
            var shade = Img(root, "Shade", glow, Vector2.zero, new Vector2(980, 820), new Color(0, 0, 0, .42f));
            shade.transform.SetAsFirstSibling();

            // WELCOME
            var welcome = Page(root, "WelcomePage");
            menu.welcomePage = welcome;
            var title = Text(welcome, "Title", "REMIX TABLE", new Vector2(0, 150), new Vector2(680, 74), 62, font, Color.white);
            title.fontStyle = FontStyles.Bold; title.characterSpacing = 14;
            Img(welcome, "TitleRule", rule, new Vector2(0, 108), new Vector2(300, 3), RemixMainMenu.Accent);
            Text(welcome, "Tagline", "SCULPT SOUND WITH YOUR HANDS", new Vector2(0, 86), new Vector2(680, 24), 14, font, Tag).characterSpacing = 10;
            var greeting = Text(welcome, "Greeting", "GOOD EVENING", new Vector2(0, 44), new Vector2(680, 30), 21, font, Body);
            greeting.characterSpacing = 4; menu.greeting = greeting;
            menu.nowLoaded = Text(welcome, "NowLoaded", "NOW LOADED", new Vector2(0, 16), new Vector2(680, 22), 13, font, Muted);
            menu.nowLoaded.characterSpacing = 2;
            var halo = Img(welcome, "PlayGlow", glow, new Vector2(0, RemixMainMenu.PrimaryY), RemixMainMenu.PrimarySize + new Vector2(120, 90), new Color(RemixMainMenu.Accent.r, RemixMainMenu.Accent.g, RemixMainMenu.Accent.b, .3f));
            menu.playGlow = halo;
            menu.continueButton = Pill(welcome, "Continue", "CONTINUE", new Vector2(0, RemixMainMenu.PrimaryY), RemixMainMenu.PrimarySize, 28, font, Style.Primary);
            menu.continueDetail = Text(welcome, "ContinueDetail", "", new Vector2(0, RemixMainMenu.ContinueDetailY), new Vector2(680, 22), 13, font, Tag);
            menu.continueDetail.characterSpacing = 2;
            menu.playButton = Pill(welcome, "Play", "PLAY", new Vector2(0, RemixMainMenu.PrimaryY), RemixMainMenu.PrimarySize, 30, font, Style.Primary, withRing: true);
            menu.songsButton = Pill(welcome, "SelectSong", "SELECT SONG", new Vector2(-RemixMainMenu.SecondaryGap, RemixMainMenu.SecondaryY), RemixMainMenu.SecondarySize, 17, font, Style.Secondary);
            menu.howToButton = Pill(welcome, "HowToUse", "HOW TO USE", new Vector2(RemixMainMenu.SecondaryGap, RemixMainMenu.SecondaryY), RemixMainMenu.SecondarySize, 17, font, Style.Secondary);
            var footer = Text(welcome, "Footer", "MENU  ABOVE THE RIBBONS BRINGS YOU BACK HERE", new Vector2(0, -258), new Vector2(680, 20), 11, font, Muted);
            footer.characterSpacing = 3;
            menu.continueButton.gameObject.SetActive(false); menu.continueDetail.gameObject.SetActive(false);
            menu.playButton.transform.Find("Ring").gameObject.SetActive(false);

            // SELECT SONG
            var songs = Page(root, "SongsPage");
            menu.songsPage = songs;
            Heading(songs, "SELECT SONG", font);
            Text(songs, "Hint", "MORE SONGS COMING SOON", new Vector2(0, 112), new Vector2(680, 22), 12, font, Muted).characterSpacing = 4;
            menu.songList = RemixSeatedLayout.Rect(songs, "SongList", Vector2.zero, MenuSize * 1000);
            var template = Pill(menu.songList, "SongTemplate", "01    SONG", new Vector2(0, 66), new Vector2(540, 52), 18, font, Style.Row);
            var songTitle = template.transform.Find("Label") as RectTransform;
            songTitle.anchoredPosition = new Vector2(-62, 0); songTitle.sizeDelta = new Vector2(380, 44);
            var songTitleText = songTitle.GetComponent<TMP_Text>(); songTitleText.alignment = TextAlignmentOptions.MidlineLeft; songTitleText.fontStyle = FontStyles.Normal;
            var status = Text(template.transform, "Status", "READY", new Vector2(176, 0), new Vector2(160, 44), 12, font, Tag);
            status.alignment = TextAlignmentOptions.MidlineRight; status.characterSpacing = 2;
            menu.songTemplate = template;
            menu.songsBackButton = Pill(songs, "Back", "BACK", new Vector2(0, -238), new Vector2(180, 48), 16, font, Style.Secondary);

            // HOW TO USE
            var howTo = Page(root, "HowToPage");
            menu.howToPage = howTo;
            menu.howToTitle = Heading(howTo, "HOW TO USE", font);
            menu.howToTitle.fontSize = 28; menu.howToTitle.characterSpacing = 6;
            menu.howToBody = Text(howTo, "Body", "", new Vector2(0, 18), new Vector2(580, 190), 19, font, Body);
            menu.howToBody.textWrappingMode = TextWrappingModes.Normal; menu.howToBody.overflowMode = TextOverflowModes.Truncate;
            menu.howToBody.lineSpacing = 16;
            menu.howToPrevButton = Pill(howTo, "Prev", "PREV", new Vector2(-196, -136), new Vector2(150, 50), 16, font, Style.Secondary);
            menu.howToPageLabel = Text(howTo, "PageLabel", "1 / 6", new Vector2(0, -136), new Vector2(150, 30), 14, font, Muted);
            menu.howToPageLabel.characterSpacing = 4;
            menu.howToNextButton = Pill(howTo, "Next", "NEXT", new Vector2(196, -136), new Vector2(150, 50), 16, font, Style.Primary);
            menu.howToBackButton = Pill(howTo, "Back", "MAIN MENU", new Vector2(0, -210), new Vector2(200, 48), 16, font, Style.Secondary);
            songs.gameObject.SetActive(false); howTo.gameObject.SetActive(false); welcome.gameObject.SetActive(true);

            // ---- Living background around the listener (not part of the canvas) ----
            var auraT = manager.workspace.Find(AuraName);
            if (auraT == null) { auraT = new GameObject(AuraName).transform; auraT.SetParent(manager.workspace, false); }
            auraT.localPosition = manager.HeadAnchorLocal; auraT.localRotation = Quaternion.identity; auraT.localScale = Vector3.one;
            var aura = Get<RemixMenuAura>(auraT.gameObject);
            aura.manager = manager;
            var renderer = auraT.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = auraMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            menu.background = aura;
            EditorUtility.SetDirty(aura); EditorUtility.SetDirty(renderer);

            // ---- MENU pill above the ribbons, top left (UNDO / REDO sit in the middle) ----
            var buttonRow = RemixModeLayout.Row(manager, transport, MenuButtonName, RemixModeLayout.HistoryCanvasY(d), MenuButtonSize);
            var p = buttonRow.localPosition; buttonRow.localPosition = new Vector3(-d.panelWidth * .5f + MenuButtonSize.x * .5f, p.y, p.z);
            buttonRow.GetComponent<Image>().color = Color.clear;
            menu.menuButton = Pill(buttonRow, "Menu", "MENU", Vector2.zero, MenuButtonSize * 1000, 16, font, Style.Secondary);

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
        static TMP_Text Heading(RectTransform page, string text, TMP_FontAsset font)
        {
            var heading = Text(page, "Heading", text, new Vector2(0, 168), new Vector2(680, 44), 32, font, Color.white);
            heading.fontStyle = FontStyles.Bold; heading.characterSpacing = 10;
            Img(page, "HeadingRule", rule, new Vector2(0, 136), new Vector2(220, 3), RemixMainMenu.Accent);
            return heading;
        }
        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
        static Image Img(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var rect = RemixSeatedLayout.Rect(parent, name, pos, size);
            var image = Get<Image>(rect.gameObject);
            image.sprite = sprite; image.type = Image.Type.Simple; image.color = color; image.raycastTarget = false;
            EditorUtility.SetDirty(image);
            return image;
        }
        static TMP_Text Text(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset font, Color color)
        {
            var label = RemixSeatedLayout.Text(parent, name, text, pos, size, fontSize, font);
            label.color = color; label.characterSpacing = 0; label.fontStyle = FontStyles.Normal;
            EditorUtility.SetDirty(label);
            return label;
        }

        enum Style { Primary, Secondary, Row }
        // Rounded pill. Primary: solid accent fill that brightens on hover. Secondary / Row: dark glass fill with a
        // thin accent ring that lights up on hover. Click handlers are bound by RemixMainMenu at runtime.
        static Button Pill(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset font, Style style, bool withRing = false)
        {
            var rect = RemixSeatedLayout.Rect(parent, name, pos, size);
            var fill = Get<Image>(rect.gameObject);
            fill.sprite = pill; fill.type = Image.Type.Sliced; fill.raycastTarget = true;
            fill.color = style == Style.Primary ? RemixMainMenu.PrimaryFill : style == Style.Row ? RemixMainMenu.RowFill : RemixMainMenu.SecondaryFill;
            Image ringImage = null;
            var ringT = rect.Find("Ring") as RectTransform;
            if (style != Style.Primary || withRing)
            {
                if (ringT == null) { ringT = (RectTransform)new GameObject("Ring", typeof(RectTransform)).transform; ringT.SetParent(rect, false); }
                ringT.gameObject.layer = 5; ringT.anchorMin = Vector2.zero; ringT.anchorMax = Vector2.one; ringT.offsetMin = ringT.offsetMax = Vector2.zero;
                ringT.SetAsFirstSibling();
                ringImage = Get<Image>(ringT.gameObject);
                ringImage.sprite = ring; ringImage.type = Image.Type.Sliced; ringImage.raycastTarget = false;
                ringImage.color = new Color(RemixMainMenu.Accent.r, RemixMainMenu.Accent.g, RemixMainMenu.Accent.b, .42f);
            }
            else if (ringT != null) Object.DestroyImmediate(ringT.gameObject);
            var button = Get<Button>(rect.gameObject);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.fadeDuration = .08f; colors.colorMultiplier = 2;
            // Multiplier 2: a tint of (.5, .5, .5, .5) leaves the graphic exactly as authored.
            if (style == Style.Primary)
            {
                button.targetGraphic = fill;
                colors.normalColor = new Color(.5f, .5f, .5f, .5f); colors.highlightedColor = new Color(.62f, .62f, .62f, .5f); colors.pressedColor = new Color(.38f, .38f, .38f, .5f);
            }
            else
            {
                // Ring at 42 % alpha idle, ~100 % on hover (multiplier 2 doubles it).
                button.targetGraphic = ringImage;
                colors.normalColor = new Color(.5f, .5f, .5f, .5f); colors.highlightedColor = new Color(.6f, .6f, .6f, 1f); colors.pressedColor = new Color(.42f, .42f, .42f, 1f);
            }
            colors.selectedColor = colors.normalColor; colors.disabledColor = new Color(.3f, .3f, .3f, .25f);
            button.colors = colors;
            while (button.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(button.onClick, 0);
            var label = Text(rect, "Label", text, Vector2.zero, size - new Vector2(16, 6), fontSize, font, Color.white);
            label.fontStyle = FontStyles.Bold; label.characterSpacing = style == Style.Row ? 1 : 5;
            label.transform.SetAsLastSibling();
            RemixMainMenu.Place(button, pos, size);
            EditorUtility.SetDirty(button);
            return button;
        }

        // ---- Generated assets ----------------------------------------------------------------------------
        static void EnsureSprites()
        {
            Directory.CreateDirectory(SpriteFolder);
            pill = Sprite(PillPath, 256, 128, (x, y) => Capsule(x, y, 256, 128, PillRadius, 0), PillRadius);
            ring = Sprite(RingPath, 256, 128, (x, y) => Capsule(x, y, 256, 128, PillRadius, 5), PillRadius);
            glow = Sprite(GlowPath, 128, 128, (x, y) => { float dx = (x + .5f) / 64f - 1, dy = (y + .5f) / 64f - 1; float r2 = dx * dx + dy * dy; return Mathf.Exp(-r2 * 4.5f) * Mathf.Clamp01((1 - r2) * 4); }, 0);
            rule = Sprite(RulePath, 256, 8, (x, y) => { float u = (x + .5f) / 256f; return Mathf.Sin(u * Mathf.PI) * Mathf.Sin(u * Mathf.PI); }, 0);
        }
        // Coverage of a capsule (rounded rect with full-radius ends); stroke > 0 draws only an outline that wide.
        static float Capsule(int x, int y, int w, int h, float r, float stroke)
        {
            float px = x + .5f, py = y + .5f;
            float cx = Mathf.Clamp(px, r, w - r), cy = h * .5f;
            float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - (r - 1.5f); // <0 inside
            float inside = Mathf.Clamp01(.5f - dist);
            if (stroke <= 0) return inside;
            return inside * Mathf.Clamp01(.5f + dist + stroke);
        }
        static Sprite Sprite(string path, int w, int h, System.Func<int, int, float> alpha, int border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255));
            tex.SetPixels32(px); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border); importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static Material EnsureAuraMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AuraMaterialPath);
            var shader = Shader.Find("RemixTable/MenuAura");
            if (material == null) { material = new Material(shader) { name = "MenuAura" }; AssetDatabase.CreateAsset(material, AuraMaterialPath); }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
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
            RemixSceneBuilder.Require(menu.GetComponent<Image>().color.a == 0 && menu.transform.Find("Frame") == null && menu.transform.Find("Inner") == null, "no card behind the menu");
            RemixSceneBuilder.Require(menu.welcomePage != null && menu.songsPage != null && menu.howToPage != null, "menu pages");
            RemixSceneBuilder.Require(menu.greeting != null && menu.nowLoaded != null && menu.howToTitle != null && menu.howToBody != null && menu.howToPageLabel != null && menu.playGlow != null && menu.continueDetail != null, "menu labels");
            foreach (var b in new[] { menu.playButton, menu.continueButton, menu.songsButton, menu.howToButton, menu.songsBackButton, menu.howToBackButton, menu.howToPrevButton, menu.howToNextButton, menu.menuButton, menu.songTemplate })
            {
                RemixSceneBuilder.Require(b != null && b.onClick.GetPersistentEventCount() == 0 && b.targetGraphic != null, "menu button " + (b != null ? b.name : "missing"));
                var fill = b.GetComponent<Image>();
                RemixSceneBuilder.Require(fill.sprite != null && fill.sprite.name == "Pill" && fill.type == Image.Type.Sliced, b.name + " is a rounded pill");
            }
            RemixSceneBuilder.Require(menu.songTemplate != null && menu.songList != null && menu.songTemplate.GetComponentsInChildren<TMP_Text>(true).Length == 2, "song row template has title + status");
            RemixSceneBuilder.Require(menu.howToPages != null && menu.howToPages.Length > 0, "how-to pages");
            var aura = menu.background;
            RemixSceneBuilder.Require(aura != null && aura.manager == manager && !aura.transform.IsChildOf(menu.transform) && aura.transform.IsChildOf(manager.workspace), "living background lives in the room, not on the canvas");
            var auraRenderer = aura.GetComponent<MeshRenderer>();
            RemixSceneBuilder.Require(auraRenderer.sharedMaterial != null && auraRenderer.sharedMaterial.shader.name == "RemixTable/MenuAura" && auraRenderer.sharedMaterial.shader.isSupported, "aura material");
            RemixSceneBuilder.Require(aura.intensity <= .3f && aura.centerStrength <= .25f, "background stays low-contrast behind the buttons");
            var buttonCanvas = menu.menuButton.GetComponentInParent<Canvas>(true);
            RemixSceneBuilder.Require(buttonCanvas != null && buttonCanvas.transform != menu.transform && buttonCanvas.transform.IsChildOf(manager.workspace) && buttonCanvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>() != null, "MENU button is pokeable and outside the menu");
            float top = manager.design.LaneY(0) + manager.design.laneHeight * .5f;
            RemixSceneBuilder.Require(buttonCanvas.transform.localPosition.y - MenuButtonSize.y * .5f > top, "MENU button sits above the ribbons");
            Debug.Log("REMIX_MAIN_MENU_PASS: floating menu, room-scale living background, pills and MENU button wired.");
        }
    }
}
