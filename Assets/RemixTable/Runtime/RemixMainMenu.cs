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
        public RemixMenuRibbons background;
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
            if (background != null) { var c = background.color; c.a = page == welcomePage ? 1 : .35f; background.color = c; }
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
                var labels = b.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Length > 0) labels[0].text = $"{i + 1:00}   {song.title}";
                if (labels.Length > 1) labels[1].text = !playable ? "COMING SOON" : current ? "LOADED" : $"{Mathf.RoundToInt(song.bpm)} BPM  ·  {Clock(Length(song))}";
                var image = b.targetGraphic as Image;
                if (image != null) image.color = current ? new Color(.16f, .36f, .52f, 1) : playable ? new Color(.07f, .11f, .15f, 1) : new Color(.05f, .06f, .08f, .8f);
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
                nowLoaded.text = "LOADED:  " + manager.library.Current.title;
            // With a session to resume, CONTINUE is the big glowing button and PLAY starts over below it.
            bool canContinue = HasContinue(out int index, out float time);
            if (continueButton != null) continueButton.gameObject.SetActive(canContinue);
            if (continueDetail != null) { continueDetail.gameObject.SetActive(canContinue); if (canContinue) continueDetail.text = ContinueText(index, time); }
            if (playButton != null)
            {
                var r = (RectTransform)playButton.transform;
                r.anchoredPosition = new Vector2(0, canContinue ? -88 : -20);
                r.sizeDelta = new Vector2(canContinue ? 260 : 310, canContinue ? 52 : 76);
                var label = playButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null) { label.text = canContinue ? "PLAY FROM START" : "PLAY"; label.fontSize = canContinue ? 18 : 32; label.rectTransform.sizeDelta = r.sizeDelta - new Vector2(10, 6); }
                var image = playButton.targetGraphic as Image;
                if (image != null) image.color = canContinue ? new Color(.07f, .11f, .15f, 1) : new Color(.22f, .62f, .95f, 1);
            }
            if (playGlow != null && playButton != null)
            {
                var primary = canContinue && continueButton != null ? (RectTransform)continueButton.transform : (RectTransform)playButton.transform;
                playGlow.rectTransform.anchoredPosition = primary.anchoredPosition;
                playGlow.rectTransform.sizeDelta = primary.sizeDelta + new Vector2(20, 18);
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
        bool Mine(Transform t) => t.IsChildOf(transform);
        void SetMenuVisible(bool on)
        {
            var canvas = GetComponent<Canvas>(); if (canvas != null) canvas.enabled = on;
            if (group != null) { group.alpha = on ? 0 : 1; group.interactable = group.blocksRaycasts = on; }
            if (menuButton != null) menuButton.gameObject.SetActive(!on);
            if (background != null) background.enabled = on; // No mesh rebuilds while the table is in use.
        }

        void Update()
        {
            if (!IsOpen) return;
            // Fade in once the table has been placed in front of the listener, then let PLAY breathe.
            if (manager.anchored) fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / .6f);
            if (group != null) group.alpha = fade;
            if (playGlow != null)
            {
                var c = playGlow.color; c.a = .35f + .3f * (.5f + .5f * Mathf.Sin((Time.unscaledTime - shownAt) * 3f));
                playGlow.color = c;
            }
        }
    }
}
