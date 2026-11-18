using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class VocalsAutomationPlayTests : RemixSavedSceneTest
    {
        static void Check(RibbonView ribbon)
        {
            var manager=ribbon.manager;ribbon.UpdateWaveform();
            var data=new List<Vector4>();ribbon.WaveformMesh.GetUVs(2,data);
            for(int i=0;i<=ribbon.CurveSegmentCount;i++)
            {
                float x=(i/(float)ribbon.CurveSegmentCount-.5f)*manager.design.width,t=ribbon.Timestamp(x);
                Assert.AreEqual(manager.automation.Evaluate(manager.library.Current.id,StemId.Vocals,ParameterType.Effect,t,manager.clock.duration),data[i*2].x);
                Assert.AreEqual(manager.PitchAt(StemId.Vocals,t),data[i*2].y);
            }
        }
        [UnityTest] public IEnumerator PlaybackSpeedsZoomLoopsAndReleaseKeepLocalizedAutomation()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.interactions.enabled=false;
            var vocals=manager.ribbons.Single(r=>r.stem==StemId.Vocals);var audio=manager.audioManager;
            string song=manager.library.Current.id;
            manager.history.BeginGesture(manager.automation);
            manager.automation.Upsert(new AutomationEdit{songId=song,stem=StemId.Vocals,parameter=ParameterType.Effect,start=.3f,end=1.1f,value=-.8f});
            manager.automation.Upsert(new AutomationEdit{songId=song,stem=StemId.Vocals,parameter=ParameterType.Pitch,start=.7f,end=1.4f,value=12});
            Assert.IsTrue(manager.history.EndGesture(manager.automation));vocals.SetSelected(false);
            for(int i=0;i<RemixAudioManager.SpeedPresetCount;i++)
            {
                Assert.IsTrue(audio.SetPlaybackSpeed(RemixAudioManager.SpeedPreset(i)));
                manager.zoom.Request(i%2==0?4:32);yield return new WaitForSecondsRealtime(.15f);Check(vocals);
                foreach(var other in manager.ribbons.Where(r=>r!=vocals))
                {
                    Assert.AreEqual(other.stem,other.GetComponent<VocalsHologram>().Stem);
                    Assert.AreSame(Resources.Load<Material>(VocalsHologram.BodyFor(other.stem)),other.GetComponent<MeshRenderer>().sharedMaterial);
                    var otherState=new List<Vector4>();other.WaveformMesh.GetUVs(2,otherState);
                    Assert.IsTrue(otherState.All(v=>v==Vector4.zero),"Vocals edits do not affect other stem automation");
                }
            }
            Assert.IsTrue(audio.SetLoop(.3f,1.4f));
            for(int i=0;i<12;i++){yield return new WaitForSecondsRealtime(.12f);Check(vocals);}
            Assert.IsTrue(manager.history.Undo(manager.automation));Check(vocals);
            var data=new List<Vector4>();vocals.WaveformMesh.GetUVs(2,data);Assert.IsTrue(data.All(v=>v==Vector4.zero));
            Assert.IsTrue(manager.history.Redo(manager.automation));Check(vocals);
            // Measured after warmup, without test readbacks or assertions in the measured section.
            vocals.UpdateWaveform();long before=System.GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20;i++)vocals.UpdateWaveform();
            Assert.AreEqual(0,System.GC.GetAllocatedBytesForCurrentThread()-before);
            audio.ClearLoop();manager.automation.ResetSong(song);Check(vocals);
        }
    }
}
