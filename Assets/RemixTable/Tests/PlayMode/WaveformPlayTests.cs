using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    // Captures both values together after normal ribbon rendering, also in batch-mode tests.
    [DefaultExecutionOrder(32000)]
    public sealed class WaveformTimingSample : MonoBehaviour
    {
        public RibbonView ribbon;
        public bool pending;
        public int frame;
        public double visualTime,songTime;
        void LateUpdate()
        {
            if(!pending) return;
            visualTime=ribbon.visualTime;songTime=ribbon.manager.clock.elapsed;
            frame=Time.frameCount;pending=false;
        }
    }
    public class WaveformPlayTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator RealAudioWaveformsFollowDSPZoomEditsAndLoops()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.interactions.enabled=false;
            Assert.IsTrue(manager.audioManager.Ready);
            var ribbon=manager.ribbons[0];var song=manager.library.Current;WaveformCache cache=null;
            foreach(var data in song.stems)if(data.id==ribbon.stem)cache=data.waveform;
            Assert.NotNull(cache);var mesh=ribbon.WaveformMesh;int count=mesh.vertexCount;
            var timing=manager.gameObject.AddComponent<WaveformTimingSample>();timing.ribbon=ribbon;
            AudioSettings.GetDSPBufferSize(out int bufferSamples,out int bufferCount);
            Debug.Log($"WAVEFORM_DSP sampleRate={AudioSettings.outputSampleRate} bufferSamples={bufferSamples} bufferCount={bufferCount}");
            foreach(float seconds in new[]{4,8,16,32})
            {
                manager.zoom.Request(seconds);yield return new WaitForSeconds(1.8f);
                // WaitForSeconds resumes between Update and LateUpdate. Observe the actual render
                // path after the ribbon's LateUpdate, without invoking UpdateWaveform from the test.
                double beforeLateMs=System.Math.Abs(ribbon.visualTime-manager.clock.elapsed)*1000;
                int requestedFrame=Time.frameCount;timing.pending=true;
                yield return null;
                Assert.IsFalse(timing.pending,"render timing sample must complete");
                Assert.GreaterOrEqual(timing.frame,requestedFrame);
                Debug.Log($"WAVEFORM_TIMING zoom={seconds} beforeLateMs={beforeLateMs:F6} renderedDeltaMs={System.Math.Abs(timing.visualTime-timing.songTime)*1000:F6}");
                Assert.That(timing.visualTime,Is.EqualTo(timing.songTime).Within(.04));
                float coordinate=manager.design.width*.2f;float stamp=ribbon.Timestamp(coordinate);
                var expected=cache.Query(stamp,seconds/512);var actual=ribbon.EnvelopeAt(coordinate,seconds/512);
                Assert.AreEqual(expected.maximum,actual.maximum);Assert.AreSame(mesh,ribbon.WaveformMesh);Assert.AreEqual(count,mesh.vertexCount);
                foreach(var source in manager.audioManager.sources) Assert.IsTrue(source.isPlaying);
                foreach(var source in manager.audioManager.sources)
                    Assert.That(System.Math.Abs(source.timeSamples-manager.audioManager.sources[0].timeSamples),Is.LessThan(2048));
            }
            ribbon.SetSelected(true);double paused=manager.clock.elapsed;
            var original=ribbon.EnvelopeAt(0,.02f);
            manager.automation.Upsert(new AutomationEdit{songId=song.id,stem=ribbon.stem,parameter=ParameterType.Gain,wholeStem=true,value=2});
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(paused,manager.clock.elapsed);
            Assert.That(ribbon.EnvelopeAt(0,.02f).maximum,Is.EqualTo(original.maximum*2).Within(.0001));
            var sourceIndex=(int)ribbon.stem;
            Assert.That(manager.audioManager.sources[sourceIndex].volume,Is.EqualTo(manager.audioManager.headroom*2).Within(.002));
            ribbon.SetSelected(false);yield return new WaitForSeconds(.3f);Assert.Greater(manager.clock.elapsed,paused);
            float nearSeam=manager.clock.duration-.005f;ribbon.visualTime=nearSeam;
            var seam=ribbon.EnvelopeAt(0,.02f);var wrapped=cache.Query(nearSeam,.02f);
            Assert.That(seam.maximum,Is.EqualTo(wrapped.maximum*2).Within(.0001));
            ribbon.visualTime=nearSeam+manager.clock.duration;
            Assert.That(ribbon.EnvelopeAt(0,.02f).maximum,Is.EqualTo(seam.maximum).Within(.001));
            Assert.IsTrue(ribbon.CanSelectAt(ribbon.SurfacePoint(0),out _));
            manager.automation.ResetSong(song.id);
        }
    }
}
