using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace RemixTable.Tests
{
    // Pure logic behind the new features: mute / solo, spatial panning, beat grid, pitch mapping, voice
    // phrases, remix save files, WAV output and tempo detection.
    public class FeatureLogicTests
    {
        // ---- Mute / solo ----
        [Test] public void MuteSilencesOnlyThatStem()
        {
            var mix = new StemMix();
            mix.SetMuted(StemId.Drums, true);
            Assert.AreEqual(0, mix.Factor(StemId.Drums));
            Assert.AreEqual(1, mix.Factor(StemId.Bass));
        }
        [Test] public void SoloSilencesEveryOtherStemAndOverridesItsOwnMute()
        {
            var mix = new StemMix();
            mix.SetMuted(StemId.Vocals, true); mix.SetSoloed(StemId.Vocals, true); mix.SetSoloed(StemId.Bass, true);
            Assert.AreEqual(1, mix.Factor(StemId.Vocals));
            Assert.AreEqual(1, mix.Factor(StemId.Bass));
            Assert.AreEqual(0, mix.Factor(StemId.Drums));
            Assert.AreEqual(0, mix.Factor(StemId.Melody));
            mix.ClearMuteSolo();
            foreach (StemId s in System.Enum.GetValues(typeof(StemId))) Assert.AreEqual(1, mix.Factor(s));
        }
        [Test] public void MixChangesBumpVersionOnlyWhenSomethingChanges()
        {
            var mix = new StemMix(); int v = mix.Version;
            mix.SetMuted(StemId.Bass, false); Assert.AreEqual(v, mix.Version);
            mix.ToggleMute(StemId.Bass); Assert.AreEqual(v + 1, mix.Version);
            mix.SetSpatial(true); Assert.AreEqual(v + 2, mix.Version);
        }
        [Test] public void SpatialPanFollowsLeftRightAndDistanceLowersLevel()
        {
            var head = new Vector3(0, 1.2f, 0);
            StemMix.Spatialize(head, Vector3.forward, head + Vector3.right, out float right, out float near);
            StemMix.Spatialize(head, Vector3.forward, head + Vector3.left, out float left, out _);
            StemMix.Spatialize(head, Vector3.forward, head + Vector3.forward, out float center, out _);
            StemMix.Spatialize(head, Vector3.forward, head + Vector3.right * 4, out _, out float far);
            StemMix.Spatialize(head, Vector3.forward, head + Vector3.back, out _, out float behind);
            Assert.Greater(right, .5f); Assert.Less(left, -.5f); Assert.AreEqual(0, center, .001f);
            Assert.AreEqual(1, near, .001f); Assert.Less(far, near); Assert.Less(behind, 1);
            // Turning the head to face the source centers it.
            StemMix.Spatialize(head, Vector3.right, head + Vector3.right, out float facing, out _);
            Assert.AreEqual(0, facing, .001f);
        }
        [Test] public void DefaultOrbsSitBeyondTheRibbonArc()
        {
            foreach (var p in StemMix.DefaultPositions()) Assert.Greater(new Vector2(p.x, p.z).magnitude, .9f);
        }

        // ---- Beat grid ----
        [Test] public void BeatGridSnapsToNearestBeatFromOffset()
        {
            // 120 BPM = 0.5 s beats starting at 0.1 s.
            Assert.AreEqual(1.1f, BeatGrid.Nearest(1.2f, 120, .1f, 30), .0001f);
            Assert.AreEqual(1.6f, BeatGrid.Nearest(1.4f, 120, .1f, 30), .0001f);
            Assert.AreEqual(1.1f, BeatGrid.Floor(1.55f, 120, .1f, 30), .0001f);
            Assert.AreEqual(1.6f, BeatGrid.Floor(1.6f, 120, .1f, 30), .0001f);
        }
        [Test] public void SnappedRegionIsAtLeastOneBeatAndWholeBeats()
        {
            float s = 2.02f, e = 2.1f; BeatGrid.SnapRegion(ref s, ref e, 120, 0, 30);
            Assert.AreEqual(2f, s, .0001f); Assert.AreEqual(2.5f, e, .0001f);
            s = 3.9f; e = 5.3f; BeatGrid.SnapRegion(ref s, ref e, 120, 0, 30);
            Assert.AreEqual(4f, s, .0001f); Assert.AreEqual(5.5f, e, .0001f);
            BeatGrid.BeatAround(7.3f, 120, 0, 30, out s, out e);
            Assert.AreEqual(7f, s, .0001f); Assert.AreEqual(7.5f, e, .0001f);
        }
        [Test] public void InvalidTempoLeavesTimesAlone()
        {
            Assert.AreEqual(1.234f, BeatGrid.Nearest(1.234f, 0, 0, 30));
            float s = 1, e = 2; BeatGrid.SnapRegion(ref s, ref e, float.NaN, 0, 30);
            Assert.AreEqual(1, s); Assert.AreEqual(2, e);
        }
        [Test] public void OnBeatReportsBarDownbeats()
        {
            Assert.IsTrue(BeatGrid.OnBeat(2.0f, 120, 0, .01f, out bool bar)); Assert.IsTrue(bar);   // beat 4 = bar 2 start
            Assert.IsTrue(BeatGrid.OnBeat(2.5f, 120, 0, .01f, out bar)); Assert.IsFalse(bar);
            Assert.IsFalse(BeatGrid.OnBeat(2.25f, 120, 0, .01f, out _));
        }

        // ---- Pitch ----
        [Test] public void PitchLiftSnapsToWholeSemitonesWithinRange()
        {
            var d = ScriptableObject.CreateInstance<RemixDesign>();
            try
            {
                Assert.AreEqual(0, d.MapDragPitch(0, .004f));
                Assert.AreEqual(6, d.MapDragPitch(0, d.pitchDistance));
                Assert.AreEqual(-12, d.MapDragPitch(0, -1));
                Assert.AreEqual(12, d.MapDragPitch(5, 1));
                foreach (float lift in new[] { .013f, .037f, -.051f }) Assert.AreEqual(Mathf.Round(d.MapDragPitch(0, lift)), d.MapDragPitch(0, lift));
            }
            finally { Object.DestroyImmediate(d); }
        }
        [Test] public void PitchAutomationIsClampedAndDefaultsToZero()
        {
            var store = new AutomationStore();
            Assert.AreEqual(0, store.Evaluate("s", StemId.Bass, ParameterType.Pitch, 1));
            store.Upsert(new AutomationEdit { songId = "s", stem = StemId.Bass, parameter = ParameterType.Pitch, wholeStem = true, value = 40 });
            Assert.AreEqual(12, store.Evaluate("s", StemId.Bass, ParameterType.Pitch, 1));
        }
        static float Frequency(float[] data, int start, int sampleRate)
        {
            int crossings = 0; int first = -1, last = -1;
            for (int i = start + 1; i < data.Length; i++)
                if (data[i - 1] <= 0 && data[i] > 0) { if (first < 0) first = i; last = i; crossings++; }
            return crossings < 2 ? 0 : (crossings - 1) * (float)sampleRate / (last - first);
        }
        [Test] public void PitchShifterMovesFrequencyAndKeepsLength([Values(12f, 7f, -12f)] float semitones)
        {
            const int rate = 48000; var dsp = new PitchShiftDsp(rate, 1);
            var data = new float[rate];
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(2 * Mathf.PI * 440 * i / rate) * .5f;
            for (int i = 0; i < data.Length; i += 1024) { var block = new float[Mathf.Min(1024, data.Length - i)]; System.Array.Copy(data, i, block, 0, block.Length); dsp.Process(block, 1, semitones); System.Array.Copy(block, 0, data, i, block.Length); }
            float expected = 440 * PitchShiftDsp.Ratio(semitones);
            Assert.AreEqual(expected, Frequency(data, rate / 4, rate), expected * .03f);
        }
        [Test] public void PitchShifterAtZeroIsBitExactPassThrough()
        {
            var dsp = new PitchShiftDsp(48000, 2);
            var data = new float[2048]; for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * .01f);
            var copy = (float[])data.Clone();
            dsp.Process(data, 2, 0);
            CollectionAssert.AreEqual(copy, data);
        }

        // ---- Voice phrases ----
        [TestCase("mute the drums", RemixCommandKind.Mute, StemId.Drums)]
        [TestCase("Solo vocals please", RemixCommandKind.Solo, StemId.Vocals)]
        [TestCase("unmute the bass", RemixCommandKind.Unmute, StemId.Bass)]
        [TestCase("mute the base", RemixCommandKind.Mute, StemId.Bass)]
        [TestCase("play only the guitar", RemixCommandKind.Solo, StemId.Melody)]
        [TestCase("clear the vocals", RemixCommandKind.ClearStem, StemId.Vocals)]
        public void StemPhrasesParse(string phrase, RemixCommandKind kind, StemId stem)
        {
            var c = RemixCommands.Parse(phrase);
            Assert.AreEqual(kind, c.kind); Assert.AreEqual(stem, c.stem);
        }
        [TestCase("undo", RemixCommandKind.Undo)]
        [TestCase("redo that", RemixCommandKind.Redo)]
        [TestCase("start recording", RemixCommandKind.RecordStart)]
        [TestCase("stop recording", RemixCommandKind.RecordStop)]
        [TestCase("loop this", RemixCommandKind.LoopOn)]
        [TestCase("stop looping", RemixCommandKind.LoopOff)]
        [TestCase("turn snap off", RemixCommandKind.SnapOff)]
        [TestCase("snap to the beat", RemixCommandKind.SnapOn)]
        [TestCase("pitch mode", RemixCommandKind.PitchMode)]
        [TestCase("filter", RemixCommandKind.FilterMode)]
        [TestCase("live mode", RemixCommandKind.LiveMode)]
        [TestCase("next song", RemixCommandKind.NextSong)]
        [TestCase("pause", RemixCommandKind.Pause)]
        [TestCase("play", RemixCommandKind.Play)]
        [TestCase("reset everything", RemixCommandKind.ResetAll)]
        [TestCase("unmute everything", RemixCommandKind.UnmuteAll)]
        [TestCase("spatial on", RemixCommandKind.SpatialOn)]
        [TestCase("show me my room", RemixCommandKind.PassthroughOn)]
        [TestCase("turn off passthrough", RemixCommandKind.PassthroughOff)]
        [TestCase("banana", RemixCommandKind.None)]
        [TestCase("", RemixCommandKind.None)]
        public void CommandPhrasesParse(string phrase, RemixCommandKind kind) => Assert.AreEqual(kind, RemixCommands.Parse(phrase).kind);

        // ---- Save files ----
        string folder;
        [SetUp] public void Folder() => folder = Path.Combine(Path.GetTempPath(), "remix-save-test-" + System.Guid.NewGuid().ToString("N"));
        [TearDown] public void Clean() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        [Test] public void SaveRoundTripsEditsCurvesAndMix()
        {
            var store = new AutomationStore(); var mix = new StemMix();
            store.Upsert(new AutomationEdit { songId = "a", stem = StemId.Drums, parameter = ParameterType.Gain, start = 1, end = 2, value = .5f });
            store.Upsert(new AutomationEdit { songId = "a", stem = StemId.Bass, parameter = ParameterType.Effect, start = 3, end = 4, value = -.6f,
                curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.5f, -.6f), new Keyframe(1, 0)) });
            store.Upsert(new AutomationEdit { songId = "a", stem = StemId.Vocals, parameter = ParameterType.Pitch, wholeStem = true, value = 3 });
            store.Upsert(new AutomationEdit { songId = "b", stem = StemId.Drums, parameter = ParameterType.Gain, wholeStem = true, value = 2 });
            mix.SetMuted(StemId.Melody, true); mix.SetSoloed(StemId.Vocals, true); mix.SetSpatial(true); mix.SetPosition(StemId.Bass, new Vector3(.3f, .1f, .9f));
            var saves = new RemixSaveStore(folder);
            saves.Save(RemixSaveStore.Capture("a", store, mix));

            var loadedStore = new AutomationStore(); var loadedMix = new StemMix();
            loadedStore.Upsert(new AutomationEdit { songId = "a", stem = StemId.Drums, parameter = ParameterType.Gain, wholeStem = true, value = .1f }); // Replaced by the save.
            loadedStore.Upsert(new AutomationEdit { songId = "c", stem = StemId.Drums, parameter = ParameterType.Gain, wholeStem = true, value = .2f }); // Other song kept.
            RemixSaveStore.Apply(saves.Load("a"), "a", loadedStore, loadedMix);
            Assert.AreEqual(4, loadedStore.edits.Count);
            Assert.AreEqual(.5f, loadedStore.EvaluateGain("a", StemId.Drums, 1.5f, 30, .001f), .001f);
            Assert.AreEqual(-.6f, loadedStore.Evaluate("a", StemId.Bass, ParameterType.Effect, 3.5f, 30), .01f);
            Assert.AreEqual(3, loadedStore.Evaluate("a", StemId.Vocals, ParameterType.Pitch, 10, 30));
            Assert.AreEqual(.2f, loadedStore.EvaluateWholeGain("c", StemId.Drums, 0), .001f);
            Assert.IsTrue(loadedMix.muted[(int)StemId.Melody]); Assert.IsTrue(loadedMix.soloed[(int)StemId.Vocals]); Assert.IsTrue(loadedMix.spatial);
            Assert.AreEqual(new Vector3(.3f, .1f, .9f), loadedMix.positions[(int)StemId.Bass]);
        }
        [Test] public void MissingOrCorruptSaveLoadsAsNothing()
        {
            var saves = new RemixSaveStore(folder);
            Assert.IsNull(saves.Load("none"));
            Directory.CreateDirectory(folder); File.WriteAllText(saves.PathFor("bad"), "{ not json");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsNull(saves.Load("bad"));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            var store = new AutomationStore(); var mix = new StemMix(); mix.SetMuted(StemId.Bass, true);
            store.Upsert(new AutomationEdit { songId = "bad", stem = StemId.Bass, parameter = ParameterType.Gain, wholeStem = true, value = 2 });
            RemixSaveStore.Apply(null, "bad", store, mix); // No save = a fresh remix.
            Assert.AreEqual(0, store.edits.Count); Assert.IsFalse(mix.muted[(int)StemId.Bass]);
        }
        [Test] public void SongIdsBecomeSafeFileNames()
        {
            var saves = new RemixSaveStore(folder);
            StringAssert.DoesNotContain("/", Path.GetFileName(saves.PathFor("a/b:c")));
            saves.Save(RemixSaveStore.Capture("a/b:c", new AutomationStore(), new StemMix()));
            Assert.IsNotNull(saves.Load("a/b:c"));
        }

        // ---- WAV ----
        [Test] public void WavWriterProducesAValidPcmFile()
        {
            Directory.CreateDirectory(folder); string path = Path.Combine(folder, "t.wav");
            using (var w = new WavWriter(path, 48000, 2))
            {
                w.Write(new[] { 0f, 1f, -1f, .5f, 2f, -2f }, 6);
                w.Write(new[] { .25f, .25f, .1f }, 3); // Odd tail: the incomplete frame is dropped.
                Assert.AreEqual(4, w.SamplesWritten);
            }
            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual(44 + 16, bytes.Length);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.AreEqual(36 + 16, System.BitConverter.ToInt32(bytes, 4));
            Assert.AreEqual(2, System.BitConverter.ToInt16(bytes, 22));
            Assert.AreEqual(48000, System.BitConverter.ToInt32(bytes, 24));
            Assert.AreEqual(16, System.BitConverter.ToInt32(bytes, 40));
            Assert.AreEqual(32767, System.BitConverter.ToInt16(bytes, 46));
            Assert.AreEqual(-32767, System.BitConverter.ToInt16(bytes, 48));
            Assert.AreEqual(32767, System.BitConverter.ToInt16(bytes, 52)); // Clipped 2.0
        }

        // ---- Tempo ----
        [Test] public void TempoDetectorFindsClickTrackTempoAndPhase([Values(96f, 120f, 128f)] float bpm)
        {
            const int rate = 22050; float offset = .21f; var data = new float[rate * 20];
            var random = new System.Random(3);
            for (int i = 0; i < data.Length; i++) data[i] = (float)(random.NextDouble() - .5) * .01f; // Quiet noise floor.
            float beat = 60 / bpm;
            for (float t = offset; t < 20; t += beat)
            {
                int start = (int)(t * rate);
                for (int i = 0; i < 600 && start + i < data.Length; i++) data[start + i] += Mathf.Sin(i * .9f) * Mathf.Exp(-i / 120f);
            }
            var result = TempoDetector.Detect(data, rate);
            Assert.AreEqual(bpm, result.bpm, 1.5f);
            float phaseError = Mathf.Abs(Mathf.Repeat(result.offset - offset + beat * .5f, beat) - beat * .5f);
            Assert.Less(phaseError, .03f);
        }
        [Test] public void TempoDetectorRejectsTooShortAudio() => Assert.AreEqual(0, TempoDetector.Detect(new float[100], 48000).bpm);

        [Test] public void GestureLabelIncludesPitch()
        {
            Assert.AreEqual("PITCH +3", RemixTransport.GestureLabel(null, null, 3));
            Assert.AreEqual("VOL 80%  PITCH -2", RemixTransport.GestureLabel(80, null, -2));
            Assert.AreEqual("VOL 80%", RemixTransport.GestureLabel(80, null, null));
        }
    }
}
