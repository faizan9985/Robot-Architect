using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    // PITCH holds, SNAP regions, the remembered LOOP region, orb holds blocking ribbon edits, reset as an
    // undoable step, and the toolbox running commands.
    public class FeatureGestureTests
    {
        GameObject root;
        RemixTableManager manager;
        RibbonView ribbon;
        RibbonInteractionManager gestures;
        RemixToolbox toolbox;
        const string Song = "feature";
        [SetUp] public void Setup()
        {
            root = new GameObject("feature fixture"); root.SetActive(false);
            manager = root.AddComponent<RemixTableManager>(); manager.enabled = false;
            manager.design = ScriptableObject.CreateInstance<RemixDesign>(); manager.workspace = root.transform;
            manager.library = root.AddComponent<SongLibraryManager>();
            manager.library.songs = new[] { new SongMetadata { id = Song, duration = 32, bpm = 120, beatOffset = 0 }, new SongMetadata { id = "second", duration = 32, bpm = 120 } };
            manager.clock.Reset(32); manager.clock.elapsed = 10;
            var go = new GameObject("ribbon"); go.transform.SetParent(root.transform);
            ribbon = go.AddComponent<RibbonView>(); ribbon.manager = manager; ribbon.stem = StemId.Drums; ribbon.visualTime = 10;
            manager.ribbons = new[] { ribbon };
            gestures = root.AddComponent<RibbonInteractionManager>(); gestures.manager = manager; gestures.enabled = false;
            manager.interactions = gestures;
            toolbox = root.AddComponent<RemixToolbox>(); toolbox.manager = manager; toolbox.enabled = false;
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { gestures.Cancel(); Object.DestroyImmediate(manager.design); Object.DestroyImmediate(root); }
        RibbonInteractionManager.HandSample Hand(float x, float lift) => new RibbonInteractionManager.HandSample { valid = true, pinch = true,
            position = ribbon.transform.TransformPoint(manager.design.ArcPoint(x, ribbon.HeightAt(x) + lift)),
            wrist = ribbon.transform.TransformPoint(manager.design.ArcPoint(x, ribbon.HeightAt(x) - .03f + lift) + Vector3.back * .12f), wristValid = true };
        void Hold(float x, float lift)
        {
            gestures.ProcessHands(Hand(x, 0), default, .016f);
            gestures.ProcessHands(Hand(x, lift), default, .016f);
            gestures.ProcessHands(default, default, .016f);
        }
        float PitchAt(float t) => manager.automation.Evaluate(Song, StemId.Drums, ParameterType.Pitch, t, 32);

        [Test] public void PitchModeLiftWritesWholeSemitonesAndLeavesGainAlone()
        {
            gestures.Mode = RibbonInteractionManager.ControlMode.Pitch;
            float t = ribbon.Timestamp(0);
            gestures.ProcessHands(Hand(0, 0), default, .016f);
            gestures.ProcessHands(Hand(0, manager.design.pitchDistance), default, .016f);
            Assert.IsTrue(gestures.IsPitching); Assert.AreEqual(6, gestures.PitchValue);
            gestures.ProcessHands(default, default, .016f);
            Assert.AreEqual(6, PitchAt(t));
            Assert.AreEqual(1, manager.automation.EvaluateGain(Song, StemId.Drums, t, 32, manager.design.gainBoundaryFade), .0001f);
            Assert.AreEqual(1, manager.history.UndoCount, "a pitch hold is one undo step");
            manager.history.Undo(manager.automation);
            Assert.AreEqual(0, PitchAt(t));
        }
        [Test] public void SnapMakesAOneHandPinchExactlyOneBeat()
        {
            manager.snapToBeat = true;
            float t = ribbon.Timestamp(0);
            Hold(0, .03f);
            Assert.IsTrue(gestures.HasLastRegion);
            Assert.AreEqual(Mathf.Floor(t * 2) / 2, gestures.LastRegionStart, .001f);
            Assert.AreEqual(.5f, AutomationStore.RegionLength(gestures.LastRegionStart, gestures.LastRegionEnd, 32), .001f);
        }
        [Test] public void SnapAlignsTwoHandRegionsToBeats()
        {
            manager.snapToBeat = true;
            gestures.ProcessHands(Hand(-.1f, 0), default, .016f);
            gestures.ProcessHands(Hand(-.1f, 0), Hand(.12f, 0), .016f);
            foreach (float edge in new[] { ribbon.regionStart, ribbon.regionEnd })
                Assert.AreEqual(0, Mathf.Abs(edge * 2 - Mathf.Round(edge * 2)), .002f, "region edge on a 0.5 s beat");
            gestures.Cancel();
        }
        [Test] public void WithoutSnapRegionsAreNotQuantized()
        {
            Hold(0, .03f);
            Assert.AreEqual(.6f, AutomationStore.RegionLength(gestures.LastRegionStart, gestures.LastRegionEnd, 32), .001f);
        }
        [Test] public void HoldThatChangesNothingIsNotALoopRegion()
        {
            Hold(0, 0);
            Assert.IsFalse(gestures.HasLastRegion);
        }
        [Test] public void HoldingAnOrbBlocksRibbonEdits()
        {
            var orb = new GameObject("orb");
            try
            {
                manager.BeginExternalHold(orb);
                Hold(0, .05f);
                Assert.AreEqual(0, manager.automation.edits.Count);
                manager.EndExternalHold(orb);
                Hold(0, .05f);
                Assert.AreEqual(1, manager.automation.edits.Count);
            }
            finally { Object.DestroyImmediate(orb); }
        }
        [Test] public void ClearStemAndResetAllAreUndoable()
        {
            Hold(0, .05f);
            manager.automation.Upsert(new AutomationEdit { songId = Song, stem = StemId.Bass, parameter = ParameterType.Gain, wholeStem = true, value = .5f });
            manager.ResetStem(StemId.Drums);
            Assert.AreEqual(1, manager.automation.edits.Count);
            manager.history.Undo(manager.automation);
            Assert.AreEqual(2, manager.automation.edits.Count);
            manager.mix.SetMuted(StemId.Bass, true);
            toolbox.ResetAll();
            Assert.AreEqual(0, manager.automation.edits.Count);
            Assert.IsFalse(manager.mix.muted[(int)StemId.Bass]);
            manager.history.Undo(manager.automation);
            Assert.AreEqual(2, manager.automation.edits.Count);
        }
        [Test] public void ResetOnlyTouchesTheCurrentSong()
        {
            manager.automation.Upsert(new AutomationEdit { songId = "second", stem = StemId.Bass, parameter = ParameterType.Gain, wholeStem = true, value = .5f });
            Hold(0, .05f);
            toolbox.ResetAll();
            Assert.AreEqual(1, manager.automation.edits.Count);
            Assert.AreEqual("second", manager.automation.edits[0].songId);
        }
        [Test] public void ToolboxRunsVoicePhrases()
        {
            Assert.IsNotNull(toolbox.RunPhrase("mute the bass"));
            Assert.IsTrue(manager.mix.muted[(int)StemId.Bass]);
            toolbox.RunPhrase("solo drums");
            Assert.AreEqual(0, manager.mix.Factor(StemId.Bass)); Assert.AreEqual(1, manager.mix.Factor(StemId.Drums));
            toolbox.RunPhrase("pitch mode");
            Assert.AreEqual(RibbonInteractionManager.ControlMode.Pitch, gestures.Mode);
            toolbox.RunPhrase("snap on"); Assert.IsTrue(manager.snapToBeat);
            toolbox.RunPhrase("snap off"); Assert.IsFalse(manager.snapToBeat);
            toolbox.RunPhrase("spatial on"); Assert.IsTrue(manager.mix.spatial);
            toolbox.RunPhrase("unmute everything");
            Assert.IsFalse(manager.mix.AnySolo); Assert.IsFalse(manager.mix.muted[(int)StemId.Bass]);
            Assert.IsNull(toolbox.RunPhrase("make me a sandwich"));
            StringAssert.Contains("DIDN'T CATCH", toolbox.Message);
        }
        [Test] public void ToolboxUndoRedoMatchHistory()
        {
            Hold(0, .05f);
            Assert.IsTrue(toolbox.Run(new RemixCommand(RemixCommandKind.Undo)));
            Assert.AreEqual(0, manager.automation.edits.Count);
            Assert.IsTrue(toolbox.Run(new RemixCommand(RemixCommandKind.Redo)));
            Assert.AreEqual(1, manager.automation.edits.Count);
        }
        [Test] public void SnapRefusesASongWithoutTempo()
        {
            manager.library.songs[0].bpm = 0;
            Assert.IsFalse(toolbox.Run(new RemixCommand(RemixCommandKind.SnapOn)));
            Assert.IsFalse(manager.snapToBeat);
            StringAssert.Contains("TEMPO", toolbox.Message);
        }
        [Test] public void FeaturesWithoutHardwareReportInsteadOfFailing()
        {
            Assert.IsFalse(toolbox.Run(new RemixCommand(RemixCommandKind.LoopOn)));        // No audio loaded.
            Assert.IsFalse(toolbox.Run(new RemixCommand(RemixCommandKind.PassthroughOn))); // No passthrough toggle.
            StringAssert.Contains("PASSTHROUGH", toolbox.Message);
            toolbox.ToggleVoice();
            StringAssert.Contains("VOICE", toolbox.Message);
        }
        [Test] public void MixControlsToggleMuteAndSolo()
        {
            var mix = root.AddComponent<RemixMixControls>(); mix.manager = manager;
            mix.MuteOther(); Assert.IsTrue(manager.mix.muted[(int)StemId.Melody]);
            mix.MuteOther(); Assert.IsFalse(manager.mix.muted[(int)StemId.Melody]);
            mix.SoloVocals(); Assert.IsTrue(manager.mix.soloed[(int)StemId.Vocals]);
            Hold(0, .05f); mix.ClearDrums(); Assert.AreEqual(0, manager.automation.edits.Count);
        }
        [Test] public void SpatialPositionsRoundTripThroughWorkspace()
        {
            root.transform.SetPositionAndRotation(new Vector3(1, 1.1f, 2), Quaternion.Euler(0, 40, 0));
            var local = new Vector3(.4f, -.1f, .9f);
            Assert.That(Vector3.Distance(local, manager.SpatialLocal(manager.SpatialWorld(local))), Is.LessThan(1e-4f));
        }
    }
}
