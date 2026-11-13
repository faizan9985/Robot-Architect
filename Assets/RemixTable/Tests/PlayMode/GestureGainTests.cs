using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class GestureGainTests
    {
        [UnityTest] public IEnumerator HandGesturesReachAudioAndPreserveGlobalPauseOwnership()
        {
            var root=new GameObject("gesture audio fixture");root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            var design=ScriptableObject.CreateInstance<RemixDesign>();design.visibleSeconds=1;manager.design=design;
            manager.library=root.AddComponent<SongLibraryManager>();
            var audio=root.AddComponent<RemixAudioManager>();audio.manager=manager;manager.audioManager=audio;
            var gestures=root.AddComponent<RibbonInteractionManager>();gestures.manager=manager;gestures.enabled=false;
            var clip=AudioClip.Create("gesture tone",192000,1,48000,false);
            var data=new float[192000];for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(i*2*Mathf.PI*220/48000)*.2f;
            clip.SetData(data,0);
            var song=new SongMetadata{id="gesture-song",stems=new StemMetadata[4]};
            audio.sources=new AudioSource[4];manager.ribbons=new RibbonView[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("stem "+i);go.transform.SetParent(root.transform);go.transform.localPosition=new Vector3(0,(1.5f-i)*design.spacing,0);
                var ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=(StemId)i;manager.ribbons[i]=ribbon;
                audio.sources[i]=go.AddComponent<AudioSource>();song.stems[i]=new StemMetadata{id=(StemId)i,clip=clip};
            }
            manager.library.songs=new[]{song};
            if(Object.FindFirstObjectByType<AudioListener>()==null)root.AddComponent<AudioListener>();
            root.SetActive(true);audio.Load(song);yield return new WaitForSeconds(.6f);
            var bass=manager.ribbons[1];float y=bass.HeightAt(-.2f);
            var left=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=bass.transform.TransformPoint(new Vector3(-.2f,y,0))};
            var right=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=bass.transform.TransformPoint(new Vector3(.2f,y+.2f,.25f))};
            gestures.ProcessHands(left,default,.016f);double paused=manager.clock.elapsed;
            Assert.IsFalse(manager.clock.playing);Assert.AreEqual(1,audio.EditingPauseCount);
            foreach(var source in audio.sources)Assert.IsFalse(source.isPlaying);
            gestures.ProcessHands(left,right,.016f);Assert.IsEmpty(manager.automation.edits);
            Assert.AreEqual(RibbonInteractionManager.GestureState.TwoHandRegion,gestures.State);
            gestures.ProcessHands(left,default,.016f); // Release right; rebase without a gain jump.
            left.position+=bass.transform.TransformVector(new Vector3(0,-.12f,0));gestures.ProcessHands(left,default,.016f);
            Assert.AreEqual(1,manager.automation.edits.Count);var edit=manager.automation.edits[0];
            Assert.AreEqual(StemId.Bass,edit.stem);Assert.AreEqual(ParameterType.Gain,edit.parameter);Assert.That(edit.value,Is.EqualTo(0).Within(.0001));
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(paused,manager.clock.elapsed);foreach(var ribbon in manager.ribbons)Assert.AreEqual(paused,ribbon.visualTime);
            Assert.Less(audio.sources[1].volume,.001f);for(int i=0;i<4;i++)if(i!=1)Assert.That(audio.sources[i].volume,Is.EqualTo(audio.headroom).Within(.001));
            gestures.ProcessHands(default,default,.016f);Assert.IsTrue(manager.clock.playing);Assert.AreEqual(0,audio.EditingPauseCount);
            Assert.AreEqual(paused,manager.clock.elapsed);
            yield return new WaitForSeconds(.3f);Assert.Less(audio.sources[1].volume,.001f);
            yield return new WaitForSeconds(.5f);Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom).Within(.001));
            foreach(var source in audio.sources)Assert.That(System.Math.Abs(source.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024));
            // Manual pause must survive a fresh selection and explicit cancellation.
            audio.Pause();yield return null;y=bass.HeightAt(0);
            left.position=bass.transform.TransformPoint(new Vector3(0,y,0));gestures.ProcessHands(left,default,.016f);
            Assert.IsTrue(bass.selected);gestures.Cancel();Assert.IsFalse(manager.clock.playing);Assert.AreEqual(0,audio.EditingPauseCount);
            Assert.AreEqual(1,manager.automation.edits.Count);
            audio.Resume();gestures.enabled=true;gestures.ProcessHands(left,default,.016f);
            Assert.AreEqual(1,audio.EditingPauseCount);gestures.enabled=false;
            Assert.AreEqual(0,audio.EditingPauseCount);Assert.IsTrue(manager.clock.playing);Assert.IsFalse(bass.selected);
            // Author a future edit on the curved right side, then hear the shared clock enter it.
            gestures.Cancel();audio.Load(song);design.curvedTimeline=true;design.pastSeconds=design.futureSeconds=4;
            yield return new WaitForSeconds(.3f);
            manager.zoom.Request(4);manager.zoom.Tick(5,false,design.zoomSmoothingSeconds);
            manager.automation.edits.Clear();
            left.position=bass.SurfacePoint(.1f);gestures.ProcessHands(left,default,.016f);
            Assert.IsTrue(bass.selected);double frozen=manager.clock.elapsed;
            float target=Mathf.Repeat((float)frozen+manager.zoom.TimeOffset(.1f,design),manager.clock.duration);
            Assert.That(bass.regionStart,Is.EqualTo(Mathf.Repeat(target-.3f,manager.clock.duration)).Within(.001));
            left.position-=bass.transform.up*.12f;gestures.ProcessHands(left,default,.016f);
            Assert.AreEqual(1,manager.automation.edits.Count);
            Assert.That(manager.automation.EvaluateGain(song.id,StemId.Bass,target,manager.clock.duration,.06f),Is.EqualTo(0).Within(.001));
            yield return new WaitForSeconds(.1f);Assert.AreEqual(frozen,manager.clock.elapsed);
            gestures.Cancel();yield return new WaitForSeconds(manager.zoom.TimeOffset(.1f,design)+.2f);
            Assert.That(audio.sources[1].volume,Is.LessThan(.002f));
            Object.Destroy(root);Object.Destroy(clip);Object.Destroy(design);yield return null;
        }
    }
}
