using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class StemHologramPlayTests : RemixSavedSceneTest
    {
        static void Check(RemixTableManager manager)
        {
            foreach(var ribbon in manager.ribbons)
            {
                ribbon.UpdateWaveform();var data=new List<Vector4>();ribbon.WaveformMesh.GetUVs(2,data);
                var hologram=ribbon.GetComponent<VocalsHologram>();Assert.AreEqual(ribbon.stem,hologram.Stem);
                var shell=new List<Vector4>();hologram.DepthMesh.GetUVs(2,shell);
                for(int i=0;i<=ribbon.CurveSegmentCount;i++)
                {
                    float x=(i/(float)ribbon.CurveSegmentCount-.5f)*manager.design.width,t=ribbon.Timestamp(x);
                    Assert.AreEqual(StemFilter.SafeEffect(manager.automation.Evaluate(manager.library.Current.id,ribbon.stem,ParameterType.Effect,t,manager.clock.duration)),data[i*2].x);
                    Assert.AreEqual(manager.PitchAt(ribbon.stem,t),data[i*2].y);Assert.AreEqual(data[i*2],shell[i*4]);
                }
                Assert.AreEqual(manager.clock.elapsed,ribbon.visualTime);
            }
        }
        [UnityTest] public IEnumerator IndependentStemAutomationSurvivesTransportHistoryGainAndSongChanges()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.interactions.enabled=false;var audio=manager.audioManager;
            string song=manager.library.Current.id;manager.history.BeginGesture(manager.automation);
            foreach(var ribbon in manager.ribbons)
            {
                int i=(int)ribbon.stem;
                manager.automation.Upsert(new AutomationEdit{songId=song,stem=ribbon.stem,parameter=ParameterType.Effect,start=.2f+i*.1f,end=1.6f,value=(i%2==0?-1:1)*(.4f+i*.15f)});
                manager.automation.Upsert(new AutomationEdit{songId=song,stem=ribbon.stem,parameter=ParameterType.Pitch,start=.4f,end=1.4f-i*.1f,value=-12+i*8});
                manager.automation.Upsert(new AutomationEdit{songId=song,stem=ribbon.stem,parameter=ParameterType.Gain,start=.4f,end=1.4f,value=1.3f+i*.2f});
            }
            Assert.IsTrue(manager.history.EndGesture(manager.automation));
            foreach(var ribbon in manager.ribbons)ribbon.SetSelected(false);
            for(int i=0;i<8;i++)
            {Assert.IsTrue(audio.SetPlaybackSpeed(RemixAudioManager.SpeedPreset(i)));manager.zoom.Request(i%2==0?4:32);yield return new WaitForSecondsRealtime(.15f);Check(manager);}
            Assert.IsTrue(audio.SetLoop(.2f,1.6f));audio.LiveMode=true;
            var held=manager.ribbons[0];held.SetSelected(true);yield return new WaitForSecondsRealtime(.25f);Check(manager);held.SetSelected(false);
            manager.snapToBeat=true;
            for(int i=0;i<8;i++){yield return new WaitForSecondsRealtime(.15f);Check(manager);}
            Assert.IsTrue(manager.history.Undo(manager.automation));Check(manager);
            Assert.IsTrue(manager.history.Redo(manager.automation));Check(manager);
            foreach(var ribbon in manager.ribbons)ribbon.UpdateWaveform();long before=System.GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20;i++)foreach(var ribbon in manager.ribbons)ribbon.UpdateWaveform();
            Assert.AreEqual(0,System.GC.GetAllocatedBytesForCurrentThread()-before);
            audio.ClearLoop();manager.ResetSong();Check(manager);Assert.IsEmpty(manager.automation.edits);
            manager.library.Select(1);yield return new WaitForSeconds(.2f);Check(manager);
        }
    }
}
