using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // Main menu shown when the app starts, in the spot where the ribbons sit:
    //   WELCOME     - title, greeting, the song that is loaded, CONTINUE (when there is a session to resume),
    //                 PLAY / PLAY FROM START, SELECT SONG, HOW TO USE; living stem ribbons drift behind it
    //   SELECT SONG - one row per library song; songs without audio show as COMING SOON
    //   HOW TO USE  - pages of instructions (edit `howToPages` in the Inspector)
    // While it is open, the remix table is hidden (renderers, canvases and colliders off), ribbon input is off
    // and the song is held paused (RemixAudioManager.MenuHold). PLAY closes it; the MENU button above the
    // ribbons opens it again. Built by Tools/Remix Table/Add Main Menu (RemixMainMenuLayout).
    public sealed class RemixMainMenu : MonoBehaviour
    {
        // Scene tests load the table directly; they turn this off so the table starts ready to use.
        public static bool OpenOnStart = true;

        public RemixTableManager manager;
        // Extra things outside the workspace to hide while the menu is open (palm menu, spatial stem orbs).
        public Transform[] alsoHide;
        public CanvasGroup group;
        public RectTransform welcomePage, songsPage, howToPage;
        public TMP_Text greeting, nowLoaded, howToTitle, howToBody, howToPageLabel;
        public Button playButton, songsButton, howToButton, songsBackButton, howToBackButton, howToPrevButton, howToNextButton, menuButton;
        public RectTransform songList;
        public Button songTemplate;
        public Graphic playGlow;
        // CONTINUE: back to the song and spot the listener left, with their saved edits (RemixPersistence).
        public Button continueButton;
        public TMP_Text continueDetail;
        // Stem ribbons of light arcing through the space around the listener while the menu is open.
        public RemixMenuAura background;

        // Menu look, shared with the editor builder (RemixMainMenuLayout): floating rounded pills, no card.
        public static readonly Color Accent = new Color(.30f, .68f, 1f, 1);
        public static readonly Color PrimaryFill = new Color(.24f, .58f, 1f, .92f);
        public static readonly Color SecondaryFill = new Color(.06f, .08f, .13f, .72f);
        public static readonly Color RowFill = new Color(.06f, .08f, .13f, .66f);
        public static readonly Color RowCurrent = new Color(.16f, .34f, .62f, .85f);
        public static readonly Color RowLocked = new Color(.05f, .06f, .09f, .40f);
        public const string ContinueSongKey = "remix.continue.song", ContinueTimeKey = "remix.continue.time", ContinueStampKey = "remix.continue.stamp";

        [System.Serializable] public struct HowToPage { public string title; [TextArea(3, 8)] public string body; }
        public HowToPage[] howToPages =
        {
            new HowToPage { title = "WELCOME TO REMIX TABLE", body = "Every song is split into four glowing ribbons, one per stem:\nVOCALS, DRUMS, BASS and OTHER (top to bottom).\nThe wave you see is the sound you hear." },
            new HowToPage { title = "PINCH TO SCULPT", body = "Reach out and pinch a ribbon.\nMove your hand UP to make that part louder, DOWN to make it quieter.\nLet go and the change is saved into the song." },
            new HowToPage { title = "VOLUME OR FILTER", body = "VOLUME / FILTER choose what a pinch edits.\nFILTER: up makes the stem brighter, down makes it darker and muffled.\nEDIT pauses while you shape; LIVE keeps the music playing." },
            new HowToPage { title = "MIX LIKE A PRO", body = "M / S mute or solo a stem.  PITCH shifts it up or down.\nSNAP locks edits to the beat.  LOOP repeats a section.\nUNDO / REDO step through your edits." },
            new HowToPage { title = "GO BIGGER", body = "SPATIAL puts each stem around you in 3D.\nROOM shows your real room and places the table on your desk.\nVIBES, DAILY DROP and REC to save your remix as a WAV." },
            new HowToPage { title = "COME BACK ANY TIME", body = "Press MENU above the ribbons to return here,\npick another song or read this guide again.\nNow press PLAY and make it yours." },
        };

        readonly List<Button> songButtons = new List<Button>();
        readonly List<Behaviour> hidden = new List<Behaviour>();
        readonly List<Collider> hiddenColliders = new List<Collider>();
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        bool interactionsWereEnabled;
        int howToIndex;
        float shownAt, fade;
        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (songTemplate != null) songTemplate.gameObject.SetActive(false);
            Bind(playButton, PlayFromStart); Bind(continueButton, Continue); Bind(songsButton, ShowSongs); Bind(howToButton, ShowHowTo);
            Bind(songsBackButton, ShowWelcome); Bind(howToBackButton, ShowWelcome);
            Bind(howToPrevButton, PreviousHowTo); Bind(howToNextButton, NextHowTo);
            Bind(menuButton, Open);
        }
        static void Bind(Button b, UnityEngine.Events.UnityAction a)
        {
            // Wired at runtime so the scene keeps no duplicate persistent bindings.
            if (b == null) return; b.onClick.RemoveListener(a); b.onClick.AddListener(a);
        }
        void OnEnable() { if (manager != null && manager.library != null) manager.library.SongChanged += SongChanged; }
        void OnDisable() { if (manager != null && manager.library != null) manager.library.SongChanged -= SongChanged; }
        void Start()
        {
            BuildSongList();
            if (OpenOnStart) Open(); else SetMenuVisible(false);
        }

        public void Open()
        {
            if (IsOpen || manager == null) return;
            if (played) RememberSession(); // Leaving the table: CONTINUE comes back here.
            IsOpen = true;
            if (manager.interactions != null)
            {
                interactionsWereEnabled = manager.interactions.enabled;
                manager.interactions.Cancel();
                manager.interactions.enabled = false;
            }
            if (manager.audioManager != null) manager.audioManager.MenuHold = true;
            HideTable();
            SetMenuVisible(true);
            ShowWelcome();
            shownAt = Time.unscaledTime; fade = 0;
        }
        // PLAY: with a session to continue this starts the loaded song from the top; otherwise it plays the
        // loaded song. Saved edits are kept either way.
        public void PlayFromStart()
        {
            if (HasContinue(out _, out _) && manager.audioManager != null) manager.audioManager.Seek(0);
            Play();
        }
        public void Continue()
        {
            if (!HasContinue(out int index, out float time)) { Play(); return; }
            if (index != manager.library.selectedIndex) manager.library.Select(index); // Loads it with its saved edits.
            if (manager.audioManager != null) manager.audioManager.Seek(time);
            Play();
        }
        public void Play()
        {
            if (!IsOpen) return;
            IsOpen = false; played = true;
            RestoreTable();
            SetMenuVisible(false);
            if (manager.interactions != null) manager.interactions.enabled = interactionsWereEnabled;
            if (manager.audioManager != null) manager.audioManager.MenuHold = false;
        }

        public void ShowWelcome() => ShowPage(welcomePage);
        public void ShowSongs() { BuildSongList(); ShowPage(songsPage); }
        public void ShowHowTo() { howToIndex = 0; ShowPage(howToPage); RefreshHowTo(); }
        public void NextHowTo() { if (howToIndex < howToPages.Length - 1) howToIndex++; RefreshHowTo(); }
        public void PreviousHowTo() { if (howToIndex > 0) howToIndex--; RefreshHowTo(); }
        void ShowPage(RectTransform page)
        {
            foreach (var p in new[] { welcomePage, songsPage, howToPage }) if (p != null) p.gameObject.SetActive(p == page);
            // Ribbons stay bright behind the welcome page and dim behind the text-heavy pages.
            if (background != null) background.pageDim = page == welcomePage ? 1 : .45f;
            RefreshWelcome();
        }

        // ---- CONTINUE ------------------------------------------------------------------------------------
        bool played;
        RemixSaveStore persistenceStore;
        // Lets tests point CONTINUE's edit count at their own save folder.
        public RemixSaveStore PersistenceStore { get => persistenceStore; set => persistenceStore = value; }
        void OnApplicationPause(bool paused) { if (paused && played && !IsOpen) RememberSession(); }
        void OnApplicationQuit() { if (played && !IsOpen) RememberSession(); }
        public void RememberSession()
        {
            if (manager == null || manager.library.songs == null || manager.library.songs.Length == 0) return;
            PlayerPrefs.SetString(ContinueSongKey, manager.library.Current.id);
            PlayerPrefs.SetFloat(ContinueTimeKey, manager.clock.Time);
            PlayerPrefs.SetString(ContinueStampKey, System.DateTime.UtcNow.Ticks.ToString());
            PlayerPrefs.Save();
        }
        public static void ForgetSession()
        {
            PlayerPrefs.DeleteKey(ContinueSongKey); PlayerPrefs.DeleteKey(ContinueTimeKey); PlayerPrefs.DeleteKey(ContinueStampKey);
        }
        // A remembered session whose song is still in the library and has audio.
        public bool HasContinue(out int index, out float time)
        {
            index = -1; time = 0;
            if (manager == null || manager.library.songs == null || !PlayerPrefs.HasKey(ContinueSongKey)) return false;
            string id = PlayerPrefs.GetString(ContinueSongKey);
            index = System.Array.FindIndex(manager.library.songs, s => s != null && s.id == id);
            if (index < 0 || !SongLibraryManager.HasAudio(manager.library.songs[index])) { index = -1; return false; }
            time = Mathf.Max(0, PlayerPrefs.GetFloat(ContinueTimeKey, 0));
            return true;
        }
        string ContinueText(int index, float time)
        {
            var song = manager.library.songs[index];
            var save = (persistenceStore ??= RemixSaveStore.Default()).Load(song.id);
            int edits = save != null ? save.edits.Count : 0;
            string ago = "";
            if (long.TryParse(PlayerPrefs.GetString(ContinueStampKey, ""), out long ticks))
            {
                var span = System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc);
                ago = span.TotalMinutes < 2 ? "  ·  JUST NOW" : span.TotalHours < 1 ? $"  ·  {(int)span.TotalMinutes} MIN AGO" : span.TotalDays < 1 ? $"  ·  {(int)span.TotalHours} H AGO" : $"  ·  {(int)span.TotalDays} D AGO";
            }
            return $"{song.title}  ·  {Clock(time)}  ·  {edits} EDIT{(edits == 1 ? "" : "S")}{ago}";
        }

        // ---- SELECT SONG ---------------------------------------------------------------------------------
        // One row per library song, cloned from the template, so new songs show up without editing the menu.
        public void BuildSongList()
        {
            if (songTemplate == null || songList == null || manager == null || manager.library.songs == null) return;
            var songs = manager.library.songs;
            while (songButtons.Count < songs.Length)
            {
                var b = Instantiate(songTemplate, songList);
                b.name = "Song" + songButtons.Count;
                int index = songButtons.Count;
                b.onClick.AddListener(() => SelectSong(index));
                songButtons.Add(b);
            }
            var templateRect = (RectTransform)songTemplate.transform;
            float rowHeight = templateRect.sizeDelta.y + 8, top = templateRect.anchoredPosition.y;
            for (int i = 0; i < songButtons.Count; i++)
            {
                var b = songButtons[i];
                bool exists = i < songs.Length;
                b.gameObject.SetActive(exists);
                if (!exists) continue;
                ((RectTransform)b.transform).anchoredPosition = new Vector2(templateRect.anchoredPosition.x, top - i * rowHeight);
                var song = songs[i];
                bool playable = SongLibraryManager.HasAudio(song), current = i == manager.library.selectedIndex;
                b.interactable = playable;
                var title = b.transform.Find("Label")?.GetComponent<TMP_Text>();
                var status = b.transform.Find("Status")?.GetComponent<TMP_Text>();
                if (title != null) { title.text = $"{i + 1:00}    {song.title}"; title.alpha = playable ? 1 : .4f; }
                if (status != null)
                {
                    status.text = !playable ? "COMING SOON" : current ? "NOW LOADED" : $"{Mathf.RoundToInt(song.bpm)} BPM  ·  {Clock(Length(song))}";
                    status.alpha = playable ? 1 : .45f;
                }
                var image = b.GetComponent<Image>(); // The fill; the hover ring is the button's target graphic.
                if (image != null) image.color = current ? RowCurrent : playable ? RowFill : RowLocked;
                Place(b, ((RectTransform)b.transform).anchoredPosition, templateRect.sizeDelta, resizeLabel: false); // Row labels keep their own title/status layout.
            }
        }
        public void SelectSong(int index)
        {
            var library = manager.library;
            if (index < 0 || index >= library.songs.Length || !SongLibraryManager.HasAudio(library.songs[index])) return;
            if (index != library.selectedIndex) library.Select(index); // Loads it; MenuHold keeps it paused until PLAY.
            ShowWelcome();
        }
        void SongChanged(SongMetadata song) { BuildSongList(); RefreshWelcome(); }

        // ---- WELCOME / HOW TO ----------------------------------------------------------------------------
        void RefreshWelcome()
        {
            if (greeting != null)
            {
                int h = System.DateTime.Now.Hour;
                greeting.text = h < 5 ? "LATE NIGHT SESSION" : h < 12 ? "GOOD MORNING" : h < 18 ? "GOOD AFTERNOON" : "GOOD EVENING";
            }
            if (nowLoaded != null && manager != null && manager.library.songs != null && manager.library.songs.Length > 0)
                nowLoaded.text = "NOW LOADED  ·  " + manager.library.Current.title;
            // With a session to resume, CONTINUE is the big glowing button and PLAY starts over below it.
            bool canContinue = HasContinue(out int index, out float time);
            if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
            if (continueDetail != null) { continueDetail.gameObject.SetActive(canContinue); if (canContinue) continueDetail.text = ContinueText(index, time); }
            if (playButton != null)
            {
                Place(playButton, new Vector2(0, canContinue ? PlayWithContinueY : PrimaryY), canContinue ? new Vector2(250, 50) : PrimarySize);
                var label = playButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) { label.text = canContinue ? "PLAY FROM START" : "PLAY"; label.fontSize = canContinue ? 17 : 30; }
                var image = playButton.GetComponent<Image>();
                if (image != null) image.color = canContinue ? SecondaryFill : PrimaryFill;
                var ring = playButton.transform.Find("Ring"); // Outline only in its secondary (PLAY FROM START) role.
                if (ring != null) ring.gameObject.SetActive(canContinue);
            }
            float secondaryY = canContinue ? SecondaryWithContinueY : SecondaryY;
            if (songsButton != null) Place(songsButton, new Vector2(-SecondaryGap, secondaryY), SecondarySize);
            if (howToButton != null) Place(howToButton, new Vector2(SecondaryGap, secondaryY), SecondarySize);
            if (playGlow != null && playButton != null)
            {
                var primary = canContinue && continueButton != null ? (RectTransform)continueButton.transform : (RectTransform)playButton.transform;
                playGlow.rectTransform.anchoredPosition = primary.anchoredPosition;
                playGlow.rectTransform.sizeDelta = primary.sizeDelta + new Vector2(120, 90); // Soft halo bleeding past the pill.
            }
        }
        void RefreshHowTo()
        {
            if (howToPages == null || howToPages.Length == 0) return;
            howToIndex = Mathf.Clamp(howToIndex, 0, howToPages.Length - 1);
            var page = howToPages[howToIndex];
            if (howToTitle != null) howToTitle.text = page.title;
            if (howToBody != null) howToBody.text = page.body;
            if (howToPageLabel != null) howToPageLabel.text = $"{howToIndex + 1} / {howToPages.Length}";
            if (howToPrevButton != null) howToPrevButton.interactable = howToIndex > 0;
            if (howToNextButton != null) howToNextButton.interactable = howToIndex < howToPages.Length - 1;
        }
        // Welcome page layout (canvas units = mm). Without a session to continue, PLAY is the big button; with one,
        // CONTINUE takes that spot and PLAY FROM START sits under it.
        public const float PrimaryY = -40, ContinueDetailY = -84, PlayWithContinueY = -126, SecondaryY = -132, SecondaryWithContinueY = -196, SecondaryGap = 122;
        public static readonly Vector2 PrimarySize = new Vector2(300, 72), SecondarySize = new Vector2(224, 52);
        // Sizes a pill button and keeps its corners fully round.
        public static void Place(Button button, Vector2 position, Vector2 size, bool resizeLabel = true)
        {
            var r = (RectTransform)button.transform;
            r.anchoredPosition = position; r.sizeDelta = size;
            var label = button.transform.Find("Label") as RectTransform;
            if (label != null && resizeLabel) label.sizeDelta = size - new Vector2(16, 6);
            foreach (var image in button.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null || image.type != Image.Type.Sliced || image.sprite.border.y <= 0) continue;
                var ir = image.rectTransform;
                float height = ir == r ? size.y : ir.rect.height > 0 && ir.anchorMin == ir.anchorMax ? ir.sizeDelta.y : size.y;
                image.pixelsPerUnitMultiplier = image.sprite.border.y / Mathf.Max(1, height * .5f);
            }
        }
        static float Length(SongMetadata song)
        {
            if (song.stems != null) foreach (var s in song.stems) if (s != null && s.clip != null) return s.clip.length;
            return song.duration;
        }
        static string Clock(float seconds) { int s = Mathf.Max(0, Mathf.RoundToInt(seconds)); return $"{s / 60}:{s % 60:00}"; }

        // ---- Hiding the table ----------------------------------------------------------------------------
        void HideTable()
        {
            hidden.Clear(); hiddenColliders.Clear(); hiddenRenderers.Clear();
            var roots = new List<Transform>();
            if (manager.workspace != null) roots.Add(manager.workspace);
            if (manager.palmMenuCanvas != null) roots.Add(manager.palmMenuCanvas.transform);
            if (alsoHide != null) foreach (var t in alsoHide) if (t != null) roots.Add(t);
            foreach (var root in roots)
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (r.enabled && !Mine(r.transform)) { r.enabled = false; hiddenRenderers.Add(r); }
                foreach (var c in root.GetComponentsInChildren<Canvas>(true)) if (c.enabled && !Mine(c.transform)) { c.enabled = false; hidden.Add(c); }
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) if (c.enabled && !Mine(c.transform)) { c.enabled = false; hiddenColliders.Add(c); }
            }
        }
        void RestoreTable()
        {
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var b in hidden) if (b != null) b.enabled = true;
            foreach (var c in hiddenColliders) if (c != null) c.enabled = true;
            hidden.Clear(); hiddenColliders.Clear(); hiddenRenderers.Clear();
        }
        bool Mine(Transform t) => t.IsChildOf(transform) || (background != null && t.IsChildOf(background.transform));
        void SetMenuVisible(bool on)
        {
            var canvas = GetComponent<Canvas>(); if (canvas != null) canvas.enabled = on;
            if (group != null) { group.alpha = on ? 0 : 1; group.interactable = group.blocksRaycasts = on; }
            if (menuButton != null) menuButton.gameObject.SetActive(!on);
            if (background != null) background.gameObject.SetActive(on); // No mesh updates while the table is in use.
        }

        void Update()
        {
            if (!IsOpen) return;
            // Fade in once the table has been placed in front of the listener, then let PLAY breathe.
            if (manager.anchored) fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / .6f);
            if (group != null) group.alpha = fade;
            if (playGlow != null)
            {
                var c = playGlow.color; c.a = .22f + .16f * (.5f + .5f * Mathf.Sin((Time.unscaledTime - shownAt) * 2.4f));
                playGlow.color = c;
            }
        }
    }
}
