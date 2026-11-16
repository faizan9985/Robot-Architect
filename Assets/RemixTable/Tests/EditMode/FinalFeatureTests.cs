using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    // QUICK START flow, FoV-aware panel layout, gaze focus / audition, and the tutorial voice command.
    public class FinalFeatureTests
    {
        GameObject root; RemixTableManager manager; RemixToolbox toolbox; RemixVibes vibes;
        RibbonView[] ribbons;
        const string Song = "final";

        [SetUp] public void Setup()
        {
            root = new GameObject("final fixture"); root.SetActive(false);
            manager = root.AddComponent<RemixTableManager>(); manager.enabled = false;
            manager.design = ScriptableObject.CreateInstance<RemixDesign>(); manager.design.curvedTimeline = true;
            manager.workspace = new GameObject("workspace").transform; manager.workspace.SetParent(root.transform);
            var pose = manager.design.SeatedPose(new Vector3(0, 1.2f, 0), Vector3.forward); manager.workspace.SetPositionAndRotation(pose.position, pose.rotation);
            manager.library = root.AddComponent<SongLibraryManager>();
            manager.library.songs = new[] { new SongMetadata { id = Song, duration = 32, bpm = 120 } };
            manager.clock.Reset(32); manager.clock.elapsed = 4; manager.anchored = true;
            StemId[] order = { StemId.Vocals, StemId.Drums, StemId.Bass, StemId.Melody };
            ribbons = new RibbonView[4];
            for (int row = 0; row < 4; row++)
            {
                var go = new GameObject("ribbon " + order[row]); go.transform.SetParent(manager.workspace, false);
                go.transform.localPosition = new Vector3(0, manager.design.LaneY(row), 0);
                ribbons[row] = go.AddComponent<RibbonView>(); ribbons[row].manager = manager; ribbons[row].stem = order[row];
            }
            manager.ribbons = ribbons;
            toolbox = root.AddComponent<RemixToolbox>(); toolbox.manager = manager; toolbox.enabled = false;
            vibes = root.AddComponent<RemixVibes>(); vibes.manager = manager; vibes.toolbox = toolbox; toolbox.vibes = vibes;
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(manager.design); Object.DestroyImmediate(root); PlayerPrefs.DeleteKey("remix.test.onboarding"); }

        RemixOnboarding Onboarding(bool room)
        {
            var go = new GameObject("onboarding"); go.SetActive(false); go.transform.SetParent(root.transform);
            var o = go.AddComponent<RemixOnboarding>();
            o.manager = manager; o.toolbox = toolbox; o.vibes = vibes; o.doneKey = "remix.test.onboarding"; o.roomAvailable = () => room;
            go.SetActive(true);
            toolbox.onboarding = o;
            return o;
        }

        [Test] public void QuickStartFollowsWhatTheUserDoes()
        {
            var o = Onboarding(room: true);
            o.Tick(2);
            Assert.AreEqual(RemixOnboarding.Step.Pull, o.Current, "starts once the table is anchored");
            o.Tick(.5f); Assert.AreEqual(RemixOnboarding.Step.Pull, o.Current, "waits for the pull");
            // The user pinches the vocals and pulls them down at the playhead.
            manager.automation.Upsert(new AutomationEdit { songId = Song, stem = StemId.Vocals, parameter = ParameterType.Gain, wholeStem = true, value = .1f });
            o.Tick(.1f); Assert.AreEqual(RemixOnboarding.Step.Back, o.Current);
            manager.automation.edits.Clear(); // undo
            o.Tick(.7f); Assert.AreEqual(RemixOnboarding.Step.Vibe, o.Current);
            vibes.Chill();
            o.Tick(.1f); Assert.AreEqual(RemixOnboarding.Step.Room, o.Current);
            manager.room = new RoomModel();
            o.Tick(.1f); Assert.AreEqual(RemixOnboarding.Step.Done, o.Current);
            o.Tick(7); Assert.AreEqual(RemixOnboarding.Step.Off, o.Current);
            Assert.AreEqual(1, PlayerPrefs.GetInt("remix.test.onboarding", 0), "remembered");
        }

        [Test] public void WithoutPassthroughTheRoomStepIsSkipped()
        {
            var o = Onboarding(room: false);
            o.Go(RemixOnboarding.Step.Vibe);
            vibes.Karaoke(); o.Tick(.1f);
            Assert.AreEqual(RemixOnboarding.Step.Done, o.Current);
        }

        [Test] public void MutingTheVocalsAlsoCountsAsThePull()
        {
            var o = Onboarding(room: false);
            o.Go(RemixOnboarding.Step.Pull);
            manager.mix.SetMuted(StemId.Vocals, true); o.Tick(.1f);
            Assert.AreEqual(RemixOnboarding.Step.Back, o.Current);
        }

        [Test] public void SkipEndsItAndTutorialReplaysIt()
        {
            var o = Onboarding(room: false);
            o.Go(RemixOnboarding.Step.Pull); o.Skip();
            Assert.AreEqual(RemixOnboarding.Step.Off, o.Current);
            Assert.AreEqual(RemixCommandKind.Tutorial, RemixCommands.Parse("show me the tutorial").kind);
            Assert.IsTrue(toolbox.RunPhrase("help") != null);
            Assert.AreEqual(RemixOnboarding.Step.Waiting, o.Current);
            o.Tick(2); Assert.AreEqual(RemixOnboarding.Step.Pull, o.Current);
        }

        // ---- FoV ----
        [TestCase(130f, 38f, 1f)]
        public void WideFovKeepsTheDesignLayout(float fov, float angle, float scale)
        {
            RemixFovAdapter.Layout(fov, 38, 11, 26, 8, .72f, out float a, out float s);
            Assert.AreEqual(angle, a, .01f); Assert.AreEqual(scale, s, .01f);
        }
        [Test] public void NarrowFovPullsPanelsInWithoutCrossingTheRows()
        {
            RemixFovAdapter.Layout(110, 38, 11, 26, 8, .72f, out float q3, out float q3Scale);
            Assert.LessOrEqual(q3 + 11 * q3Scale, 55 - 8 + .01f, "fits a 110° headset");
            RemixFovAdapter.Layout(96, 38, 11, 26, 8, .72f, out float q3s, out float q3sScale);
            Assert.Less(q3s, q3); Assert.Less(q3sScale, q3Scale);
            Assert.GreaterOrEqual(q3s - 11 * q3sScale, 26 - .01f, "never over the centre rows");
            Assert.GreaterOrEqual(q3sScale, .72f - 1e-4f, "stays readable");
        }

        // ---- Gaze ----
        Ray GazeAt(int row)
        {
            var head = manager.HeadAnchorWorld;
            var target = manager.workspace.TransformPoint(new Vector3(0, manager.design.LaneY(row), 0));
            return new Ray(head, (target - head).normalized);
        }

        [Test] public void GazeFindsTheRibbonYouLookAt()
        {
            var go = new GameObject("gaze"); go.transform.SetParent(root.transform);
            var gaze = go.AddComponent<RemixGazeFocus>(); gaze.manager = manager;
            Assert.AreEqual(0, gaze.Raycast(GazeAt(0), out _), "vocals lane");
            Assert.AreEqual(2, gaze.Raycast(GazeAt(2), out _), "bass lane");
            Assert.AreEqual(-1, gaze.Raycast(new Ray(manager.HeadAnchorWorld, Vector3.up), out _), "looking at the ceiling");
            Assert.AreEqual(-1, gaze.Raycast(new Ray(manager.HeadAnchorWorld, Vector3.back), out _), "looking behind");
        }

        [Test] public void GazeFocusDwellsThenAPinchInTheAirAuditions()
        {
            var go = new GameObject("gaze"); go.transform.SetParent(root.transform);
            var gaze = go.AddComponent<RemixGazeFocus>(); gaze.manager = manager;
            var air = manager.HeadAnchorWorld + Vector3.forward * .35f + Vector3.down * .3f; // between you and the table
            gaze.Tick(GazeAt(2), 0, default, default, .05f);
            Assert.AreEqual(-1, gaze.Focus, "dwell: no flicker on a glance");
            gaze.Tick(GazeAt(2), 0, default, default, .1f);
            Assert.AreEqual(2, gaze.Focus);
            gaze.Tick(GazeAt(2), 1, air, default, .2f);
            Assert.IsFalse(gaze.Auditioning, "a quick pinch (UI tap) does nothing");
            gaze.Tick(GazeAt(2), 1, air, default, .2f);
            Assert.IsTrue(gaze.Auditioning);
            Assert.Greater(ribbons[2].gazeGlow, 0);
            gaze.Tick(GazeAt(2), 0, default, default, .1f);
            Assert.IsFalse(gaze.Auditioning, "release brings the mix back");
            gaze.Tick(GazeAt(2), 2, air, air, 1);
            Assert.IsFalse(gaze.Auditioning, "two hands = zoom, not audition");
        }

        [Test] public void APinchOnARibbonIsEditingNotAudition()
        {
            var go = new GameObject("gaze"); go.transform.SetParent(root.transform);
            var gaze = go.AddComponent<RemixGazeFocus>(); gaze.manager = manager;
            var onRibbon = ribbons[2].SurfacePoint(0);
            gaze.Tick(GazeAt(2), 0, default, default, .2f);
            gaze.Tick(GazeAt(2), 1, onRibbon, default, 1);
            Assert.IsFalse(gaze.Auditioning);
        }
    }
}
