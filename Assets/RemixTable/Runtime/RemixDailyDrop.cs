using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // Saved progress: streak, totals and the active challenge. One small JSON file, written atomically.
    [Serializable] public class DailyDropState
    {
        public int version = 1;
        public int firstDay = -1, lastCompletedDay = -1, streak, bestStreak, totalCompleted;
        public int day = -1, current, todayIndex;    // `current` can move past today's drop to bonus drops
        public bool currentDone, todayDone;
        public int theme;
    }

    // Day rollover, streaks and unlocks with no Unity state, so they are unit-tested.
    public static class DailyDropRules
    {
        public static readonly DateTime Epoch = new DateTime(2000, 1, 1);
        public static int DayNumber(DateTime local) => (int)(local.Date - Epoch).TotalDays;
        // Total completions needed for each room-glow theme (CLASSIC is free).
        public static readonly int[] ThemeUnlocks = { 0, 1, 3, 6 };
        public static readonly string[] ThemeNames = { "CLASSIC", "SUNSET", "NEON", "AURORA" };
        public static int UnlockedThemes(DailyDropState s) { int n = 0; foreach (var t in ThemeUnlocks) if (s.totalCompleted >= t) n++; return n; }
        public static int NextUnlockAt(DailyDropState s) { foreach (var t in ThemeUnlocks) if (s.totalCompleted < t) return t; return -1; }

        // A new day picks the next drop in the rotation; the very first day starts at the beginning, so a new
        // player's first drop is always the friendliest one.
        public static void Roll(DailyDropState s, int today, int challengeCount)
        {
            if (challengeCount <= 0) return;
            if (s.firstDay < 0 || s.firstDay > today) s.firstDay = today;
            if (s.day == today) return;
            s.day = today;
            s.todayIndex = (today - s.firstDay) % challengeCount;
            s.current = s.todayIndex; s.currentDone = false; s.todayDone = false;
        }
        // Streak shown right now: still alive if the last completion was today or yesterday.
        public static int LiveStreak(DailyDropState s, int today) => s.lastCompletedDay >= today - 1 ? s.streak : 0;

        public static void Complete(DailyDropState s, int today)
        {
            if (s.currentDone) return;
            s.currentDone = true; s.totalCompleted++;
            if (s.todayDone) return; // Bonus drops count toward unlocks, not the streak.
            s.todayDone = true;
            s.streak = s.lastCompletedDay == today - 1 ? s.streak + 1 : s.lastCompletedDay == today ? Math.Max(1, s.streak) : 1;
            s.lastCompletedDay = today;
            s.bestStreak = Math.Max(s.bestStreak, s.streak);
        }
        public static bool NextBonus(DailyDropState s, int challengeCount)
        {
            if (!s.currentDone || challengeCount <= 0) return false;
            s.current = (s.current + 1) % challengeCount; s.currentDone = false;
            return true;
        }
    }

    public sealed class DailyDropStore
    {
        public string Path { get; }
        public DailyDropStore(string path) { Path = path; }
        public static Func<DailyDropStore> DefaultFactory { get; set; }
        public static DailyDropStore Default() => DefaultFactory != null ? DefaultFactory() : new DailyDropStore(System.IO.Path.Combine(Application.persistentDataPath, "daily-drop.json"));
        public DailyDropState Load()
        {
            try { return File.Exists(Path) ? JsonUtility.FromJson<DailyDropState>(File.ReadAllText(Path)) ?? new DailyDropState() : new DailyDropState(); }
            catch (Exception e) { Debug.LogWarning("Daily Drop progress could not be read and was reset: " + e.Message); return new DailyDropState(); }
        }
        public void Save(DailyDropState state)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                string temp = Path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(state, true));
                if (File.Exists(Path)) File.Replace(temp, Path, null); else File.Move(temp, Path);
            }
            catch (Exception e) { Debug.LogWarning("Daily Drop progress could not be saved: " + e.Message); }
        }
    }

    // DAILY DROP: one small remix goal per day that teaches a feature and pays off in under a minute, checked
    // automatically from what the user actually does. Finishing it grows a streak; every completion (bonus
    // drops included) counts toward unlocking room-glow themes, so there is visible growth between sessions.
    public sealed class RemixDailyDrop : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixToolbox toolbox;
        public RemixVibes vibes;
        public SpatialStemField spatialField;
        public RemixRoomVisuals roomVisuals;
        [Header("UI")]
        public TMP_Text heading, title, description, streakLabel, themeLabel;
        public Image progressFill, panel;
        public Button nextButton, themeButton;
        public Color accent = new Color(1f, .62f, .2f, 1), panelColor = new Color(.018f, .026f, .04f, 1);

        public DailyDropStore Store { get; set; }
        public DailyDropState State { get; private set; }
        public float Progress { get; private set; }
        public Func<DateTime> Clock = () => DateTime.Now;
        public event Action<Challenge> Completed;

        public sealed class Challenge
        {
            public string id, title, description;
            public Func<RemixDailyDrop, float, float> step; // (drop, dt) -> progress 0..1
        }
        public static readonly Challenge[] Challenges = BuildChallenges();
        public Challenge Current => Challenges[Mathf.Clamp(State?.current ?? 0, 0, Challenges.Length - 1)];

        // Per-challenge scratch, reset whenever the active challenge changes.
        float held; bool phaseDone; readonly HashSet<RemixVibe> vibesTried = new HashSet<RemixVibe>();
        int shownChallenge = -1; float flashUntil;

        void Awake() { Store ??= DailyDropStore.Default(); State = Store.Load(); Refresh(true); }
        void OnEnable() { if (vibes != null) vibes.Applied += OnVibe; }
        void OnDisable() { if (vibes != null) vibes.Applied -= OnVibe; }
        void OnVibe(RemixVibe v) { if (v != RemixVibe.Normal) vibesTried.Add(v); }
        void OnApplicationPause(bool paused) { if (paused && State != null) Store.Save(State); }

        public int Today => DailyDropRules.DayNumber(Clock());
        public void Refresh(bool save)
        {
            State ??= new DailyDropState();
            int before = State.day;
            DailyDropRules.Roll(State, Today, Challenges.Length);
            if (save && before != State.day) Store?.Save(State);
            ApplyTheme();
        }

        void Update() => Tick(Time.unscaledDeltaTime);
        public void Tick(float dt)
        {
            if (manager == null || State == null) return;
            Refresh(true);
            if (shownChallenge != State.current) { shownChallenge = State.current; held = 0; phaseDone = false; vibesTried.Clear(); }
            if (!State.currentDone)
            {
                Progress = Mathf.Clamp01(Current.step(this, dt));
                if (Progress >= 1) Finish();
            }
            else Progress = 1;
            Paint();
        }

        void Finish()
        {
            var challenge = Current;
            DailyDropRules.Complete(State, Today);
            Store?.Save(State);
            int unlocked = DailyDropRules.UnlockedThemes(State);
            string unlock = unlocked > 1 && DailyDropRules.ThemeUnlocks[unlocked - 1] == State.totalCompleted ? $"  ·  {DailyDropRules.ThemeNames[unlocked - 1]} THEME UNLOCKED" : "";
            if (toolbox != null) toolbox.Say($"DROP COMPLETE: {challenge.title}  ·  STREAK {DailyDropRules.LiveStreak(State, Today)}{unlock}", 6);
            if (roomVisuals != null) roomVisuals.Celebrate();
            flashUntil = Time.unscaledTime + 1.2f;
            Completed?.Invoke(challenge);
        }

        // Buttons ---------------------------------------------------------------------------------------------
        public void NextDrop() { if (State != null && DailyDropRules.NextBonus(State, Challenges.Length)) { Store?.Save(State); Paint(); } }
        public void CycleTheme()
        {
            if (State == null) return;
            int unlocked = Mathf.Max(1, DailyDropRules.UnlockedThemes(State));
            State.theme = (State.theme + 1) % unlocked;
            Store?.Save(State);
            ApplyTheme();
            if (toolbox != null) toolbox.Say("ROOM THEME: " + DailyDropRules.ThemeNames[State.theme]);
        }

        // Room-glow themes (wall, desk).
        public static readonly Color[,] Themes =
        {
            { new Color(.32f, .78f, 1f), new Color(1f, .78f, .45f) },   // CLASSIC
            { new Color(1f, .45f, .3f), new Color(1f, .82f, .35f) },     // SUNSET
            { new Color(1f, .22f, .85f), new Color(.3f, 1f, .92f) },     // NEON
            { new Color(.35f, 1f, .55f), new Color(.62f, .45f, 1f) },    // AURORA
        };
        void ApplyTheme()
        {
            if (State == null) return;
            State.theme = Mathf.Clamp(State.theme, 0, Mathf.Max(0, DailyDropRules.UnlockedThemes(State) - 1));
            if (roomVisuals != null) { roomVisuals.wallColor = Themes[State.theme, 0]; roomVisuals.deskColor = Themes[State.theme, 1]; }
        }

        void Paint()
        {
            if (State == null) return;
            bool bonus = State.current != State.todayIndex;
            Set(heading, State.currentDone ? (bonus ? "BONUS DROP  ·  DONE" : "DAILY DROP  ·  DONE") : bonus ? "BONUS DROP" : "DAILY DROP");
            Set(title, Current.title);
            Set(description, State.currentDone ? "NICE. COME BACK TOMORROW FOR A NEW DROP, OR TAKE A BONUS ONE." : Current.description);
            int streak = DailyDropRules.LiveStreak(State, Today);
            Set(streakLabel, $"STREAK {streak}   ·   BEST {State.bestStreak}   ·   DROPS {State.totalCompleted}");
            int unlocked = DailyDropRules.UnlockedThemes(State), next = DailyDropRules.NextUnlockAt(State);
            Set(themeLabel, next > 0 ? $"{DailyDropRules.ThemeNames[State.theme]}  ·  NEXT THEME AT {next}" : DailyDropRules.ThemeNames[State.theme]);
            if (progressFill != null)
            {
                // Left-anchored bar stretched to the progress (no sprite needed).
                var r = progressFill.rectTransform; r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(Mathf.Max(.001f, Progress), 1);
                r.offsetMin = r.offsetMax = Vector2.zero; progressFill.color = accent;
            }
            if (nextButton != null) nextButton.interactable = State.currentDone;
            if (themeButton != null) themeButton.interactable = unlocked > 1;
            if (panel != null) panel.color = Time.unscaledTime < flashUntil ? Color.Lerp(panelColor, accent * .55f, Mathf.PingPong(Time.unscaledTime * 4, 1)) : panelColor;
        }
        static void Set(TMP_Text t, string s) { if (t != null && t.text != s) t.text = s; }

        // Challenge checks -------------------------------------------------------------------------------------
        bool Playing => manager.clock.playing && (manager.audioManager == null || manager.audioManager.Ready);
        string Song => manager.library.Current.id;
        float Bar { get { var s = manager.library.Current; return BeatGrid.Valid(s.bpm) ? BeatGrid.BeatLength(s.bpm) * BeatGrid.BeatsPerBar : 2; } }
        // Muted, soloed out, or turned down to near silence (whole stem or a region under the playhead).
        bool Silenced(StemId stem) => !manager.mix.Audible(stem) ||
            manager.automation.EvaluateGain(Song, stem, manager.clock.Time, manager.clock.duration, manager.design != null ? manager.design.gainBoundaryFade : .06f) <= .15f;
        float Hold(bool condition, float dt, float seconds) { if (condition && Playing) held += dt; return held / Mathf.Max(.01f, seconds); }
        bool AnyEdit(ParameterType parameter, float threshold)
        {
            foreach (var e in manager.automation.edits)
            {
                if (e.songId != Song || e.parameter != parameter) continue;
                if (Mathf.Abs(e.value) >= threshold) return true;
                if (e.curve != null) foreach (var k in e.curve.keys) if (Mathf.Abs(k.value) >= threshold) return true;
            }
            return false;
        }

        static Challenge[] BuildChallenges() => new[]
        {
            new Challenge { id = "karaoke", title = "KARAOKE NIGHT",
                description = "TAKE THE VOCALS OUT AND LET THE SONG PLAY FOR 8 SECONDS. TRY: PINCH THE VOCALS RIBBON AND PULL DOWN, OR VIBES > KARAOKE.",
                step = (d, dt) => d.Hold(d.Silenced(StemId.Vocals), dt, 8) },
            new Challenge { id = "surround", title = "SURROUND SOUND",
                description = "TURN ON SPATIAL AND GRAB THE ORBS: SPREAD ALL FOUR STEMS AT LEAST 1 M APART AROUND YOU.",
                step = (d, dt) => d.Spread() },
            new Challenge { id = "breakdown", title = "THE BREAKDOWN",
                description = "DROP THE DRUMS OUT FOR 2 BARS... THEN BRING THEM BACK IN FOR THE HIT.",
                step = (d, dt) => d.BreakdownStep(dt) },
            new Challenge { id = "underwater", title = "UNDERWATER",
                description = "PICK FILTER, PINCH A RIBBON AND PULL DOWN TO MUFFLE IT (OR UP TO THIN IT OUT).",
                step = (d, dt) => d.AnyEdit(ParameterType.Effect, .35f) ? 1 : 0 },
            new Challenge { id = "mood", title = "SET THE MOOD",
                description = "TRY TWO DIFFERENT VIBES. SAY \"MAKE IT CHILL\" OR TAP THE VIBES PANEL.",
                step = (d, dt) => d.vibesTried.Count / 2f },
            new Challenge { id = "spotlight", title = "SPOTLIGHT",
                description = "SOLO THE BASS (S IN THE MIX ROW) AND LISTEN TO IT ALONE FOR ONE BAR.",
                step = (d, dt) => d.Hold(d.manager.mix.soloed[(int)StemId.Bass] && d.SoloCount() == 1, dt, d.Bar) },
            new Challenge { id = "keychange", title = "KEY CHANGE",
                description = "PICK PITCH, PINCH ANY RIBBON AND LIFT IT TO SHIFT THAT PART UP A FEW SEMITONES.",
                step = (d, dt) => d.AnyEdit(ParameterType.Pitch, 1) ? 1 : 0 },
            new Challenge { id = "loop", title = "STUCK IN A LOOP",
                description = "EDIT A REGION, PRESS LOOP AND LET IT GO ROUND THREE TIMES.",
                step = (d, dt) => d.LoopStep(dt) },
            new Challenge { id = "room", title = "MAKE IT YOURS",
                description = "TURN ON ROOM AND SPATIAL, THEN STICK A STEM ON YOUR REAL WALL.",
                step = (d, dt) => d.RoomStep() },
            new Challenge { id = "record", title = "IT'S A WRAP",
                description = "PRESS REC AND RECORD 10 SECONDS OF YOUR REMIX.",
                step = (d, dt) => d.toolbox != null && d.toolbox.recorder != null && d.toolbox.recorder.Recording ? d.toolbox.recorder.Seconds / 10f : 0 },
        };

        int SoloCount() { int n = 0; foreach (var s in manager.mix.soloed) if (s) n++; return n; }
        float Spread()
        {
            if (!manager.mix.spatial) return 0;
            var p = manager.mix.positions; float closest = float.MaxValue;
            for (int i = 0; i < p.Length; i++) for (int j = i + 1; j < p.Length; j++) closest = Mathf.Min(closest, Vector3.Distance(p[i], p[j]));
            return closest >= 1 ? 1 : .5f * Mathf.Clamp01(closest);
        }
        float BreakdownStep(float dt)
        {
            float need = 2 * Bar;
            if (!phaseDone) { if (Silenced(StemId.Drums) && Playing) held += dt; if (held >= need) phaseDone = true; return .85f * Mathf.Clamp01(held / need); }
            return Silenced(StemId.Drums) ? .85f : 1;
        }
        float LoopStep(float dt)
        {
            var audio = manager.audioManager;
            if (audio == null || !audio.LoopActive) return 0;
            float length = Mathf.Max(1, AutomationStore.RegionLength(audio.LoopStart, audio.LoopEnd, manager.clock.duration));
            return Hold(true, dt, length * 3);
        }
        float RoomStep()
        {
            if (manager.room == null) return 0;
            if (!manager.mix.spatial) return .5f;
            if (spatialField == null) return 1;
            for (int i = 0; i < StemMix.StemCount; i++) if (spatialField.Mounted(i, out _, out _)) return 1;
            return .75f;
        }
    }
}
