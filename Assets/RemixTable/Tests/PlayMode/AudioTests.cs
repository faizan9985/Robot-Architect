using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class AudioTests
    {
        [UnityTest] public IEnumerator RegionGainRoutesToStemAndSurvivesEditingPauseLoopAndSongSwitch()
        {
            var root=new GameObject("region audio");root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            var design=ScriptableObject.CreateInstance<RemixDesign>();manager.design=design;
            manager.library=root.AddComponent<SongLibraryManager>();
            var audio=root.AddComponent<RemixAudioManager>();manager.audioManager=audio;audio.manager=manager;
            audio.sources=new AudioSource[4];
            var clip=AudioClip.Create("two second tone",96000,1,48000,false);
            var data=new float[96000];for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(i*2*Mathf.PI*220/48000)*.25f;
            clip.SetData(data,0);
            var song=new SongMetadata{id="region",stems=new StemMetadata[4]};
            for(int i=0;i<4;i++){audio.sources[i]=root.AddComponent<AudioSource>();song.stems[i]=new StemMetadata{id=(StemId)i,clip=clip};}
            var ribbonObject=new GameObject("gain ribbon");ribbonObject.transform.SetParent(root.transform);
            var ribbon=ribbonObject.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=StemId.Bass;
            manager.library.songs=new[]{song,new SongMetadata{id="different",stems=song.stems}};
            var edit=new AutomationEdit{songId="region",stem=StemId.Bass,parameter=ParameterType.Gain,start=.1f,end=1,value=.25f};
            manager.automation.Upsert(edit);
            root.SetActive(true);audio.Load(song);
            yield return new WaitForSeconds(.6f);
            Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom*.25f).Within(.001));
            for(int i=0;i<4;i++)if(i!=1)Assert.That(audio.sources[i].volume,Is.EqualTo(audio.headroom).Within(.001));
            ribbon.SetSelected(true);double paused=manager.clock.elapsed;
            yield return null;float height=ribbon.HeightAt(0);
            edit.value=.5f;manager.automation.Upsert(edit);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(paused,manager.clock.elapsed);
            Assert.That(ribbon.HeightAt(0),Is.EqualTo(height*2).Within(.00001));
            Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom*.5f).Within(.001));
            ribbon.SetSelected(false);Assert.AreEqual(paused,manager.clock.elapsed);
            yield return new WaitForSeconds(.4f);
            Assert.Greater(manager.clock.elapsed,paused);Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom*.5f).Within(.001));
            foreach(var source in audio.sources)Assert.That(System.Math.Abs(source.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024));
            yield return new WaitForSeconds(.8f);
            Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom).Within(.001));
            yield return new WaitForSeconds(.95f);
            Assert.Greater(manager.clock.elapsed,2);Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom*.5f).Within(.001));
            manager.library.Select(1);audio.Load(manager.library.Current);
            yield return new WaitForSeconds(.6f);Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom).Within(.001));
            manager.library.Select(0);audio.Load(song);
            yield return new WaitForSeconds(.6f);Assert.That(audio.sources[1].volume,Is.EqualTo(audio.headroom*.5f).Within(.001));
            Assert.AreEqual(1,manager.automation.edits.Count);
            Object.Destroy(root);Object.Destroy(clip);Object.Destroy(design);yield return null;
        }
        [UnityTest] public IEnumerator EditingOwnersFreezeAllRibbonsAndRespectManualPause()
        {
            var root=new GameObject("editing test");root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();
            manager.library=root.AddComponent<SongLibraryManager>();
            var audio=root.AddComponent<RemixAudioManager>();audio.manager=manager;manager.audioManager=audio;
            var clip=AudioClip.Create("loop",48000,1,48000,false);
            var song=new SongMetadata{id="edit",stems=new StemMetadata[4]};
            audio.sources=new AudioSource[4];manager.ribbons=new RibbonView[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
                var ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=(StemId)i;manager.ribbons[i]=ribbon;
                audio.sources[i]=go.AddComponent<AudioSource>();song.stems[i]=new StemMetadata{id=(StemId)i,clip=clip};
            }
            manager.library.songs=new[]{song};root.SetActive(true);audio.Load(song);
            yield return new WaitForSeconds(.4f);
            var first=manager.ribbons[0];var second=manager.ribbons[1];
            for(int session=0;session<3;session++)
            {
                first.SetSelected(true);first.SetSelected(true);
                Assert.AreEqual(1,audio.EditingPauseCount);
                double paused=manager.clock.elapsed;
                foreach(var source in audio.sources) Assert.IsFalse(source.isPlaying);
                yield return null;
                float before=first.HeightAt(0);
                manager.automation.Upsert(new AutomationEdit{songId="edit",stem=StemId.Drums,wholeStem=true,parameter=ParameterType.Pitch,value=7+session});
                yield return new WaitForSeconds(.1f);
                Assert.IsTrue(first.selected);Assert.AreNotEqual(before,first.HeightAt(0));
                Assert.AreEqual(paused,manager.clock.elapsed);
                foreach(var ribbon in manager.ribbons)Assert.AreEqual(paused,ribbon.visualTime);
                second.SetSelected(true);first.SetSelected(false);
                Assert.AreEqual(1,audio.EditingPauseCount);Assert.IsFalse(manager.clock.playing);
                second.SetSelected(false);
                Assert.IsTrue(manager.clock.playing);Assert.AreEqual(paused,manager.clock.elapsed);
                yield return new WaitForSeconds(.05f);Assert.AreEqual(paused,manager.clock.elapsed);
                yield return new WaitForSeconds(.4f);
                Assert.That(manager.clock.elapsed-paused,Is.InRange(.15,.4));
                foreach(var source in audio.sources)Assert.That(System.Math.Abs(source.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024));
            }
            audio.Pause();first.SetSelected(true);first.SetSelected(false);
            Assert.IsFalse(manager.clock.playing);Assert.IsTrue(audio.IsManuallyPaused);
            audio.Resume();first.SetSelected(true);audio.Pause();first.SetSelected(false);
            Assert.IsFalse(manager.clock.playing);
            audio.Resume();first.SetSelected(true);second.SetSelected(true);
            first.gameObject.SetActive(false);Assert.IsFalse(manager.clock.playing);
            second.enabled=false;Assert.AreEqual(0,audio.EditingPauseCount);Assert.IsTrue(manager.clock.playing);
            Assert.IsFalse(first.selected);Assert.IsFalse(second.selected);
            Object.Destroy(root);Object.Destroy(clip);Object.Destroy(manager.design);yield return null;
        }
        [UnityTest] public IEnumerator ScheduledStemsPauseLoopAndGain()
        {
            var root=new GameObject("audio test"); root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.library=root.AddComponent<SongLibraryManager>();
            var audio=root.AddComponent<RemixAudioManager>();audio.manager=manager;manager.audioManager=audio;
            audio.sources=new AudioSource[4];
            var song=new SongMetadata{id="test",stems=new StemMetadata[4]};
            var clip=AudioClip.Create("one second",48000,1,48000,false);
            for(int i=0;i<4;i++){audio.sources[i]=root.AddComponent<AudioSource>();song.stems[i]=new StemMetadata{id=(StemId)i,clip=clip};}
            manager.library.songs=new[]{song};root.SetActive(true);audio.Load(song);
            Assert.Greater(audio.ScheduledStart,AudioSettings.dspTime);
            yield return new WaitForSeconds(.5f);
            Assert.That(manager.clock.elapsed,Is.InRange(.2,.5));
            foreach(var s in audio.sources) Assert.That(System.Math.Abs(s.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024));
            audio.Pause();double paused=manager.clock.elapsed;
            yield return new WaitForSeconds(.1f);Assert.AreEqual(paused,manager.clock.elapsed);
            audio.Resume();yield return new WaitForSeconds(1.1f);
            Assert.Greater(manager.clock.elapsed,1);Assert.Less(manager.clock.Time,1);
            manager.automation.Upsert(new AutomationEdit{songId="test",stem=StemId.Drums,parameter=ParameterType.Gain,wholeStem=true,value=0});
            yield return new WaitForSeconds(.3f);Assert.Less(audio.sources[0].volume,.001f);
            audio.Load(new SongMetadata{id="placeholder"});Assert.IsFalse(audio.Ready);
            foreach(var s in audio.sources)Assert.IsFalse(s.isPlaying);
            Object.Destroy(root);Object.Destroy(clip);yield return null;
        }
    }
}
