using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    // Audio behaviour of the new features with real AudioSources: mute / solo levels, the gapless LOOP,
    // pitch reaching the shifter, spatial pan, the recorder and remix persistence.
    public class FeatureAudioTests
    {
        GameObject root;
        RemixTableManager manager;
        RemixAudioManager audio;
        AudioClip clip;
        SongMetadata song;
        const float Seconds = 8;
        [SetUp] public void Setup()
        {
            root = new GameObject("feature audio"); root.SetActive(false);
            manager = root.AddComponent<RemixTableManager>(); manager.enabled = false;
            manager.design = ScriptableObject.CreateInstance<RemixDesign>(); manager.workspace = root.transform;
            manager.library = root.AddComponent<SongLibraryManager>();
            audio = root.AddComponent<RemixAudioManager>(); manager.audioManager = audio; audio.manager = manager;
            audio.sources = new AudioSource[4];
            int rate = 48000, samples = (int)(rate * Seconds);
            clip = AudioClip.Create("tone", samples, 1, rate, false);
            var data = new float[samples]; for (int i = 0; i < samples; i++) data[i] = Mathf.Sin(i * 2 * Mathf.PI * 330 / rate) * .3f;
            clip.SetData(data, 0);
            song = new SongMetadata { id = "feature-audio", bpm = 120, stems = new StemMetadata[4] };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("stem " + i); go.transform.SetParent(root.transform);
                audio.sources[i] = go.AddComponent<AudioSource>();
                song.stems[i] = new StemMetadata { id = (StemId)i, clip = clip };
            }
            manager.library.songs = new[] { song };
            root.SetActive(true);
        }
        [TearDown] public void Cleanup() { Object.Destroy(root); Object.Destroy(clip); Object.Destroy(manager.design); }

        [UnityTest] public IEnumerator MuteAndSoloDriveSourceVolumes()
        {
            audio.Load(song);
            manager.mix.SetMuted(StemId.Bass, true);
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(0, audio.sources[(int)StemId.Bass].volume, .001f);
            Assert.AreEqual(audio.headroom, audio.sources[(int)StemId.Drums].volume, .001f);
            manager.mix.SetSoloed(StemId.Vocals, true);
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(audio.headroom, audio.sources[(int)StemId.Vocals].volume, .001f);
            Assert.AreEqual(0, audio.sources[(int)StemId.Drums].volume, .001f);
            manager.mix.ClearMuteSolo();
            yield return new WaitForSeconds(.4f);
            foreach (var s in audio.sources) Assert.AreEqual(audio.headroom, s.volume, .001f);
        }
        [UnityTest] public IEnumerator LoopRepeatsTheRegionWithoutDrift()
        {
            audio.Load(song);
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(audio.SetLoop(1f, 1.8f));
            Assert.IsTrue(audio.LoopActive);
            float minimum = float.MaxValue, maximum = float.MinValue;
            int wraps = 0; float previous = manager.clock.Time;
            float until = Time.realtimeSinceStartup + 3.2f; // ~4 loop passes
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                float t = manager.clock.Time;
                if (Time.realtimeSinceStartup > until - 2.9f) { minimum = Mathf.Min(minimum, t); maximum = Mathf.Max(maximum, t); }
                if (t < previous - .3f) wraps++;
                previous = t;
            }
            Assert.GreaterOrEqual(wraps, 3, "loop wrapped repeatedly");
            Assert.GreaterOrEqual(minimum, .99f); Assert.LessOrEqual(maximum, 1.81f);
            // The playing set stays sample-aligned across stems after the source swaps.
            var first = audio.ActiveSource(StemId.Drums);
            foreach (StemId s in System.Enum.GetValues(typeof(StemId)))
            {
                var source = audio.ActiveSource(s);
                Assert.IsTrue(source.isPlaying, s + " playing");
                Assert.Less(Mathf.Abs(source.timeSamples - first.timeSamples), 1024, s + " in sync");
            }
            audio.ClearLoop();
            Assert.IsFalse(audio.LoopActive);
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(manager.clock.Time, 1.8f, "playback continues past the old loop end");
        }
        [UnityTest] public IEnumerator LoopSurvivesPauseAndResume()
        {
            audio.Load(song);
            yield return new WaitForSeconds(.3f);
            audio.SetLoop(2f, 3f);
            yield return new WaitForSeconds(1.5f);
            audio.Pause(); float paused = manager.clock.Time;
            Assert.That(paused, Is.InRange(1.99f, 3.01f));
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(paused, manager.clock.Time, .0001f);
            audio.Resume();
            yield return new WaitForSeconds(1.5f);
            Assert.That(manager.clock.Time, Is.InRange(1.99f, 3.01f));
        }
        [UnityTest] public IEnumerator EnablingALoopOutsideThePlayheadJumpsToIt()
        {
            audio.Load(song);
            yield return new WaitForSeconds(.3f);
            audio.SetLoop(5f, 6f);
            yield return new WaitForSeconds(.4f);
            Assert.That(manager.clock.Time, Is.InRange(5f, 6.01f));
        }
        [UnityTest] public IEnumerator PitchAutomationReachesTheShifter()
        {
            audio.Load(song);
            manager.automation.Upsert(new AutomationEdit { songId = song.id, stem = StemId.Vocals, parameter = ParameterType.Pitch, wholeStem = true, value = 5 });
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(5, audio.Pitch(StemId.Vocals).Semitones);
            Assert.AreEqual(0, audio.Pitch(StemId.Drums).Semitones);
            Assert.AreEqual(1, audio.sources[(int)StemId.Vocals].pitch, "playback speed is untouched");
        }
        [UnityTest] public IEnumerator SpatialModePansStemsByOrbPosition()
        {
            var cameraObject = new GameObject("head") { tag = "MainCamera" }; cameraObject.AddComponent<Camera>();
            try
            {
                cameraObject.transform.SetPositionAndRotation(manager.SpatialWorld(Vector3.zero), root.transform.rotation);
                audio.use3DAudio = false; // 2D fallback: stereo pan from the orb direction
                audio.Load(song);
                manager.mix.SetPosition(StemId.Drums, new Vector3(-1, 0, 0));
                manager.mix.SetPosition(StemId.Bass, new Vector3(1, 0, 0));
                manager.mix.SetSpatial(true);
                yield return new WaitForSeconds(.4f);
                Assert.Less(audio.sources[(int)StemId.Drums].panStereo, -.5f);
                Assert.Greater(audio.sources[(int)StemId.Bass].panStereo, .5f);
                manager.mix.SetSpatial(false);
                yield return new WaitForSeconds(.4f);
                Assert.AreEqual(0, audio.sources[(int)StemId.Drums].panStereo, .01f);

                // Real 3D: each source sits on its orb, fully spatialized; leaving SPATIAL returns to 2D.
                audio.use3DAudio = true;
                manager.mix.SetSpatial(true);
                yield return null; yield return null;
                Assert.IsTrue(audio.Spatial3DActive);
                var drums = audio.sources[(int)StemId.Drums];
                Assert.AreEqual(1, drums.spatialBlend, 1e-4f);
                Assert.Less(Vector3.Distance(drums.transform.position, manager.SpatialWorld(new Vector3(-1, 0, 0))), 1e-3f);
                Assert.Less(cameraObject.transform.InverseTransformPoint(drums.transform.position).x, 0, "drums to the left of the head");
                manager.mix.SetSpatial(false);
                yield return null; yield return null;
                Assert.IsFalse(audio.Spatial3DActive);
                Assert.AreEqual(0, drums.spatialBlend, 1e-4f);
            }
            finally { Object.Destroy(cameraObject); }
        }
        [UnityTest] public IEnumerator RecorderWritesWhatIsPlaying()
        {
            var listenerObject = new GameObject("listener");
            var existing = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var l in existing) l.enabled = false;
            listenerObject.AddComponent<AudioListener>();
            var recorder = listenerObject.AddComponent<RemixRecorder>();
            string path = null;
            try
            {
                audio.Load(song);
                yield return new WaitForSeconds(.4f);
                Assert.IsTrue(recorder.StartRecording());
                yield return new WaitForSeconds(.8f);
                path = recorder.StopRecording();
                Assert.IsTrue(File.Exists(path));
                var bytes = File.ReadAllBytes(path);
                int channels = System.BitConverter.ToInt16(bytes, 22), dataBytes = System.BitConverter.ToInt32(bytes, 40);
                Assert.AreEqual(bytes.Length - 44, dataBytes);
                float seconds = dataBytes / 2f / channels / System.BitConverter.ToInt32(bytes, 24);
                Assert.That(seconds, Is.InRange(.5f, 1.2f));
                int peak = 0;
                for (int i = 44; i + 1 < bytes.Length; i += 2) peak = Mathf.Max(peak, Mathf.Abs(System.BitConverter.ToInt16(bytes, i)));
                Assert.Greater(peak, 200, "recording contains the tone, not silence");
            }
            finally
            {
                Object.Destroy(listenerObject);
                foreach (var l in existing) if (l != null) l.enabled = true;
                if (path != null && File.Exists(path)) File.Delete(path);
            }
        }
        [UnityTest] public IEnumerator PersistenceSavesAfterAChangeAndRestoresOnLoad()
        {
            string folder = Path.Combine(Path.GetTempPath(), "remix-persist-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var holder = new GameObject("persistence"); holder.SetActive(false); holder.transform.SetParent(root.transform);
                var persistence = holder.AddComponent<RemixPersistence>();
                persistence.manager = manager; persistence.saveDelaySeconds = .1f; persistence.Store = new RemixSaveStore(folder);
                holder.SetActive(true);
                manager.history.BeginGesture(manager.automation);
                manager.automation.Upsert(new AutomationEdit { songId = song.id, stem = StemId.Bass, parameter = ParameterType.Gain, wholeStem = true, value = .4f });
                manager.history.EndGesture(manager.automation);
                manager.mix.SetMuted(StemId.Drums, true);
                yield return new WaitForSeconds(.4f);
                Assert.IsFalse(persistence.Dirty);
                Assert.IsTrue(File.Exists(persistence.Store.PathFor(song.id)));
                // A new session: empty state, then the song loads.
                manager.automation.edits.Clear(); manager.mix.ClearMuteSolo();
                persistence.Load(song);
                Assert.AreEqual(.4f, manager.automation.EvaluateWholeGain(song.id, StemId.Bass, 0), .001f);
                Assert.IsTrue(manager.mix.muted[(int)StemId.Drums]);
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
