using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    // VIBES, DAILY DROP, ACCESS (closer) and skipping silent songs.
    public class HackathonFeatureTests
    {
        GameObject root; RemixTableManager manager; RemixToolbox toolbox; RemixVibes vibes;
        const string Song = "hack";
        string folder;

        [SetUp] public void Setup()
        {
            root = new GameObject("hack fixture"); root.SetActive(false);
            manager = root.AddComponent<RemixTableManager>(); manager.enabled = false;
            manager.design = ScriptableObject.CreateInstance<RemixDesign>(); manager.design.curvedTimeline = true;
            manager.workspace = new GameObject("workspace").transform; manager.workspace.SetParent(root.transform);
            var pose = manager.design.SeatedPose(new Vector3(0, 1.2f, 0), Vector3.forward); manager.workspace.SetPositionAndRotation(pose.position, pose.rotation);
            manager.library = root.AddComponent<SongLibraryManager>();
            manager.library.songs = new[] { new SongMetadata { id = Song, duration = 32, bpm = 120 } };
            manager.clock.Reset(32); manager.clock.elapsed = 4;
            toolbox = root.AddComponent<RemixToolbox>(); toolbox.manager = manager; toolbox.enabled = false;
            vibes = root.AddComponent<RemixVibes>(); vibes.manager = manager; vibes.toolbox = toolbox;
            toolbox.vibes = vibes;
            root.SetActive(true);
            folder = Path.Combine(Path.GetTempPath(), "remix-hack-" + Guid.NewGuid().ToString("N"));
        }
        [TearDown] public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(manager.design); UnityEngine.Object.DestroyImmediate(root);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        float WholeGain(StemId s) => manager.automation.EvaluateWholeGain(Song, s, 1);
        float Effect(StemId s) => manager.automation.Evaluate(Song, s, ParameterType.Effect, 1, 32);

        // ---- Voice phrases ----
        [TestCase("make it chill", RemixCommandKind.VibeChill)]
        [TestCase("something more mellow", RemixCommandKind.VibeChill)]
        [TestCase("pump it up", RemixCommandKind.VibeHype)]
        [TestCase("karaoke mode", RemixCommandKind.VibeKaraoke)]
        [TestCase("a cappella", RemixCommandKind.VibeAcapella)]
        [TestCase("break it down", RemixCommandKind.VibeBreakdown)]
        [TestCase("back to normal", RemixCommandKind.VibeNormal)]
        [TestCase("bring it back", RemixCommandKind.VibeNormal)]
        [TestCase("go back", RemixCommandKind.PreviousSong)]
        [TestCase("mute the vocals", RemixCommandKind.Mute)]
        public void VibePhrasesParse(string phrase, RemixCommandKind kind) => Assert.AreEqual(kind, RemixCommands.Parse(phrase).kind);

        // ---- Vibes ----
        [Test] public void VibesReplaceEachOtherAndNormalGivesTheUsersMixBack()
        {
            manager.automation.Upsert(new AutomationEdit { songId = Song, stem = StemId.Vocals, parameter = ParameterType.Gain, wholeStem = true, value = 1.8f }); // the user's own edit
            Assert.IsTrue(toolbox.Run(new RemixCommand(RemixCommandKind.VibeChill)));
            Assert.AreEqual(.55f, WholeGain(StemId.Drums), 1e-4f); Assert.Less(Effect(StemId.Melody), -.4f);
            Assert.AreEqual(RemixVibe.Chill, vibes.Current);
            vibes.Hype();
            Assert.AreEqual(1.5f, WholeGain(StemId.Drums), 1e-4f);
            Assert.AreEqual(0, Effect(StemId.Melody), 1e-4f, "chill's filter is gone, vibes don't stack");
            vibes.Karaoke();
            Assert.IsFalse(manager.mix.Audible(StemId.Vocals)); Assert.AreEqual(1, WholeGain(StemId.Drums), 1e-4f);
            vibes.Normal();
            Assert.IsTrue(manager.mix.Audible(StemId.Vocals));
            Assert.AreEqual(1.8f, WholeGain(StemId.Vocals), 1e-4f, "the user's own edit survives");
            Assert.IsFalse(manager.automation.edits.Exists(e => e.id.StartsWith(RemixVibes.IdPrefix)));
        }

        [Test] public void AVibeIsOneUndoStep()
        {
            vibes.Hype();
            Assert.AreEqual(1.5f, WholeGain(StemId.Drums), 1e-4f);
            Assert.IsTrue(manager.history.Undo(manager.automation));
            Assert.AreEqual(1, WholeGain(StemId.Drums), 1e-4f);
        }

        [Test] public void BreakdownAndAcapellaUseMuteAndSolo()
        {
            vibes.Breakdown();
            Assert.IsFalse(manager.mix.Audible(StemId.Drums)); Assert.IsFalse(manager.mix.Audible(StemId.Bass)); Assert.IsTrue(manager.mix.Audible(StemId.Vocals));
            vibes.Acapella();
            Assert.IsTrue(manager.mix.Audible(StemId.Vocals)); Assert.IsFalse(manager.mix.Audible(StemId.Drums)); Assert.IsFalse(manager.mix.Audible(StemId.Melody));
        }

        // ---- Daily Drop rules ----
        [Test] public void StreaksGrowDayByDayAndBreakAfterAMissedDay()
        {
            var s = new DailyDropState(); int n = RemixDailyDrop.Challenges.Length;
            DailyDropRules.Roll(s, 100, n);
            Assert.AreEqual(0, s.current, "a new player starts with the first drop");
            DailyDropRules.Complete(s, 100);
            Assert.AreEqual(1, s.streak); Assert.AreEqual(1, s.totalCompleted);
            Assert.IsTrue(DailyDropRules.NextBonus(s, n)); DailyDropRules.Complete(s, 100);
            Assert.AreEqual(1, s.streak, "bonus drops don't inflate the streak"); Assert.AreEqual(2, s.totalCompleted);
            DailyDropRules.Roll(s, 101, n);
            Assert.AreEqual(1, s.current); Assert.IsFalse(s.currentDone);
            Assert.AreEqual(1, DailyDropRules.LiveStreak(s, 101), "yesterday's streak is still alive today");
            DailyDropRules.Complete(s, 101);
            Assert.AreEqual(2, s.streak); Assert.AreEqual(2, s.bestStreak);
            DailyDropRules.Roll(s, 103, n);
            Assert.AreEqual(0, DailyDropRules.LiveStreak(s, 103), "a missed day breaks it");
            DailyDropRules.Complete(s, 103);
            Assert.AreEqual(1, s.streak); Assert.AreEqual(2, s.bestStreak);
            Assert.AreEqual(3, DailyDropRules.UnlockedThemes(s), "CLASSIC, SUNSET (1) and NEON (3) unlocked at 4 drops");
            Assert.AreEqual(6, DailyDropRules.NextUnlockAt(s));
        }

        [Test] public void NextBonusNeedsTheCurrentDropDone()
        {
            var s = new DailyDropState(); DailyDropRules.Roll(s, 5, 10);
            Assert.IsFalse(DailyDropRules.NextBonus(s, 10));
        }

        RemixDailyDrop Drop(DateTime now)
        {
            var go = new GameObject("drop"); go.SetActive(false); go.transform.SetParent(root.transform);
            var drop = go.AddComponent<RemixDailyDrop>();
            drop.manager = manager; drop.toolbox = toolbox; drop.vibes = vibes;
            drop.Store = new DailyDropStore(Path.Combine(folder, "daily.json"));
            drop.Clock = () => now;
            go.SetActive(true);
            drop.Refresh(true);
            return drop;
        }

        [Test] public void KaraokeDropCompletesFromWhatTheUserDoesAndIsSaved()
        {
            var day = new DateTime(2026, 11, 17, 10, 0, 0);
            var drop = Drop(day);
            Assert.AreEqual("karaoke", drop.Current.id);
            for (int i = 0; i < 20; i++) drop.Tick(.5f);
            Assert.AreEqual(0, drop.Progress, "vocals still audible: no progress");
            vibes.Karaoke();
            drop.Tick(4); Assert.AreEqual(.5f, drop.Progress, .01f);
            drop.Tick(4.1f);
            Assert.IsTrue(drop.State.currentDone); Assert.AreEqual(1, drop.State.streak);
            var saved = new DailyDropStore(Path.Combine(folder, "daily.json")).Load();
            Assert.AreEqual(1, saved.totalCompleted); Assert.IsTrue(saved.todayDone);
            drop.NextDrop();
            Assert.AreEqual("surround", drop.Current.id);
            manager.mix.SetSpatial(true);
            for (int i = 0; i < 4; i++) manager.mix.SetPosition((StemId)i, new Vector3(i * 1.2f, 0, 1));
            drop.Tick(.1f);
            Assert.IsTrue(drop.State.currentDone); Assert.AreEqual(2, drop.State.totalCompleted);
        }

        [Test] public void ProgressPausesWhileTheSongIsPaused()
        {
            var drop = Drop(new DateTime(2026, 11, 17));
            vibes.Karaoke(); manager.clock.playing = false;
            drop.Tick(10);
            Assert.AreEqual(0, drop.Progress);
        }

        [Test] public void ThemesCycleOnlyThroughUnlockedOnes()
        {
            var drop = Drop(new DateTime(2026, 11, 17));
            drop.CycleTheme(); Assert.AreEqual(0, drop.State.theme, "only CLASSIC to start");
            drop.State.totalCompleted = 3;
            drop.CycleTheme(); Assert.AreEqual(1, drop.State.theme);
            drop.CycleTheme(); Assert.AreEqual(2, drop.State.theme);
            drop.CycleTheme(); Assert.AreEqual(0, drop.State.theme);
        }

        [Test] public void CorruptProgressFileStartsFresh()
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "daily.json"); File.WriteAllText(path, "{ not json");
            var state = new DailyDropStore(path).Load();
            Assert.IsNotNull(state); Assert.AreEqual(0, state.totalCompleted);
        }

        // ---- ACCESS ----
        [Test] public void CloserShrinksTheTableAboutTheHead()
        {
            var head = manager.HeadAnchorWorld;
            var go = new GameObject("access"); go.transform.SetParent(root.transform);
            var access = go.AddComponent<RemixAccessibility>(); access.manager = manager;
            access.SetCloser(true);
            Assert.AreEqual(access.closerScale, manager.workspace.localScale.x, 1e-4f);
            Assert.That(Vector3.Distance(head, manager.HeadAnchorWorld), Is.LessThan(1e-4f), "arc stays centered on the head");
            access.SetCloser(false);
            Assert.AreEqual(1, manager.workspace.localScale.x, 1e-4f);
        }

        // ---- Songs ----
        static SongMetadata WithAudio(string id) => new SongMetadata { id = id, stems = new[] { new StemMetadata { id = StemId.Drums, clip = AudioClip.Create(id, 64, 1, 8000, false) } } };
        static SongMetadata Silent(string id) => new SongMetadata { id = id, stems = new[] { new StemMetadata { id = StemId.Drums } } };

        [Test] public void NextAndPreviousSkipSilentPlaceholders()
        {
            var lib = manager.library;
            lib.songs = new[] { WithAudio("a"), Silent("b"), Silent("c"), WithAudio("d") };
            lib.selectedIndex = 0; lib.skipSilentSongs = true;
            lib.Select(1); Assert.AreEqual(3, lib.selectedIndex);
            lib.Select(4); Assert.AreEqual(0, lib.selectedIndex, "wraps forward past the end");
            lib.Select(-1); Assert.AreEqual(3, lib.selectedIndex, "previous skips backwards");
            lib.skipSilentSongs = false;
            lib.Select(4); Assert.AreEqual(0, lib.selectedIndex); lib.Select(1); Assert.AreEqual(1, lib.selectedIndex);
            foreach (var s in lib.songs) if (s.stems[0].clip != null) UnityEngine.Object.DestroyImmediate(s.stems[0].clip);
        }

        [Test] public void WithOneRealSongNextDoesNotRestartIt()
        {
            var lib = manager.library;
            lib.songs = new[] { WithAudio("only"), Silent("p1"), Silent("p2") };
            lib.selectedIndex = 0; lib.skipSilentSongs = true;
            int changes = 0; lib.SongChanged += _ => changes++;
            lib.Select(1); lib.Select(-1);
            Assert.AreEqual(0, lib.selectedIndex); Assert.AreEqual(0, changes);
            UnityEngine.Object.DestroyImmediate(lib.songs[0].stems[0].clip);
        }
    }
}
