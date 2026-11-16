using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    // Audio behaviour of the new features with real AudioSources: mute / solo levels, the gapless LOOP,
    // pitch reaching the shifter, spatial pan, the recorder and remix persistence.
    public class FeatureAudioTests : RemixSavedSceneTest
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
                manager.mix.SetMuted(StemId.Drums,true);manager.mix.SetSoloed(StemId.Bass,true);
                yield return new WaitForSeconds(.4f);
                Assert.IsTrue(recorder.StartRecording());
                yield return new WaitForSeconds(.8f);
                Assert.That(audio.sources[(int)StemId.Drums].volume,Is.LessThan(.001f));
                Assert.That(audio.sources[(int)StemId.Bass].volume,Is.EqualTo(audio.headroom).Within(.002f));
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

        [UnityTest] public IEnumerator SpeedPitchAndAutomationSurviveLoopBoundaries()
        {
            audio.Load(song);audio.LiveMode=true;
            manager.automation.Upsert(new AutomationEdit {songId=song.id,stem=StemId.Bass,parameter=ParameterType.Gain,wholeStem=true,value=.5f});
            manager.automation.Upsert(new AutomationEdit {songId=song.id,stem=StemId.Bass,parameter=ParameterType.Effect,wholeStem=true,value=-.5f});
            manager.automation.Upsert(new AutomationEdit {songId=song.id,stem=StemId.Bass,parameter=ParameterType.Pitch,wholeStem=true,value=7});
            foreach(float speed in new[]{.25f,2f,.75f})
            {
                Assert.IsTrue(audio.SetPlaybackSpeed(speed));Assert.IsTrue(audio.SetLoop(1,1.6f));audio.Seek(1.4f);
                yield return new WaitForSecondsRealtime(.2f+.9f/speed);
                Assert.That(manager.clock.Time,Is.InRange(.99f,1.61f));
                for(int i=0;i<4;i++)
                {
                    var source=audio.ActiveSource((StemId)i);Assert.IsTrue(source.isPlaying);Assert.AreEqual(speed,source.pitch);
                    Assert.That(System.Math.Abs(source.timeSamples-audio.ActiveSource(StemId.Drums).timeSamples),Is.LessThan(1024));
                }
                Assert.That(audio.Pitch(StemId.Bass).Semitones,Is.EqualTo(7));
                Assert.That(audio.Effect(StemId.Bass),Is.EqualTo(-.5f).Within(.03f));
                Assert.That(audio.ActiveSource(StemId.Bass).volume,Is.EqualTo(audio.headroom*.5f).Within(.002f));
            }
        }
        [UnityTest] public IEnumerator ResetAllUndoRedoRestoresMixerLoopAndPitch()
        {
            audio.Load(song);manager.history.Manager=manager;
            manager.mix.SetMuted(StemId.Drums,true);manager.mix.SetSoloed(StemId.Bass,true);
            manager.automation.Upsert(new AutomationEdit {songId=song.id,stem=StemId.Bass,parameter=ParameterType.Pitch,wholeStem=true,value=7});
            audio.SetLoop(1,3);manager.ResetSong();
            Assert.IsFalse(audio.LoopActive);Assert.IsFalse(manager.mix.muted[0]);Assert.IsFalse(manager.mix.AnySolo);
            Assert.IsTrue(manager.history.Undo(manager.automation));
            Assert.IsTrue(audio.LoopActive);Assert.AreEqual(1,audio.LoopStart);Assert.AreEqual(3,audio.LoopEnd);
            Assert.IsTrue(manager.mix.muted[(int)StemId.Drums]);Assert.IsTrue(manager.mix.soloed[(int)StemId.Bass]);Assert.AreEqual(7,manager.PitchAt(StemId.Bass,0));
            Assert.IsTrue(manager.history.Redo(manager.automation));Assert.IsFalse(audio.LoopActive);Assert.IsFalse(manager.mix.AnySolo);Assert.AreEqual(0,manager.PitchAt(StemId.Bass,0));
            yield return null;
        }
        [UnityTest] public IEnumerator RecorderRejectsMismatchedFormatsAndNeverOverwrites()
        {
            var go=new GameObject("format recorder");go.SetActive(false);
            go.AddComponent<AudioListener>().enabled=false;var recorder=go.AddComponent<RemixRecorder>();
            string first=null,second=null;
            try
            {
                Assert.IsTrue(recorder.StartRecording());
                int rate=AudioSettings.outputSampleRate;int channels=AudioSettings.speakerMode==AudioSpeakerMode.Mono?1:2;
                recorder.CaptureBlock(new float[1024*channels],channels,rate);
                recorder.CaptureBlock(new float[1024*channels],channels==1?2:1,rate);
                recorder.CaptureBlock(new float[1024*channels],channels,rate+1);
                first=recorder.StopRecording();var before=File.ReadAllBytes(first);
                Assert.AreEqual(44+1024*channels*2,before.Length,"only the matching-format block is exported");
                Assert.IsTrue(recorder.StartRecording());second=recorder.StopRecording();
                Assert.AreNotEqual(first,second);CollectionAssert.AreEqual(before,File.ReadAllBytes(first));
                Assert.Throws<IOException>(()=>new WavWriter(first,rate,channels));
            }
            finally {Object.Destroy(go);if(first!=null)File.Delete(first);if(second!=null)File.Delete(second);}
            yield return null;
        }
    }
}
