using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class LivePlaybackTests
    {
        [UnityTest] public IEnumerator LiveModeKeepsPlayingWhileEditingAndEditModePauses()
        {
            var root=new GameObject("live playback");root.SetActive(false);
            var manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            manager.design=ScriptableObject.CreateInstance<RemixDesign>();
            manager.library=root.AddComponent<SongLibraryManager>();
            var audio=root.AddComponent<RemixAudioManager>();audio.manager=manager;manager.audioManager=audio;
            var clip=AudioClip.Create("live loop",96000,1,48000,false);
            var song=new SongMetadata{id="live",stems=new StemMetadata[4]};
            audio.sources=new AudioSource[4];manager.ribbons=new RibbonView[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("ribbon "+i);go.transform.SetParent(root.transform);
                var ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=(StemId)i;manager.ribbons[i]=ribbon;
                audio.sources[i]=go.AddComponent<AudioSource>();song.stems[i]=new StemMetadata{id=(StemId)i,clip=clip};
            }
            manager.library.songs=new[]{song};root.SetActive(true);audio.Load(song);
            yield return new WaitForSeconds(.4f);
            var held=manager.ribbons[0];

            audio.LiveMode=true;
            held.SetSelected(true);
            Assert.AreEqual(1,audio.EditingPauseCount,"the hold is still tracked as an edit");
            Assert.IsTrue(manager.clock.playing,"LIVE: holding a ribbon does not pause");
            double before=manager.clock.elapsed;
            yield return new WaitForSeconds(.3f);
            Assert.Greater(manager.clock.elapsed-before,.15,"song time keeps advancing while held");
            foreach(var source in audio.sources) Assert.IsTrue(source.isPlaying);

            audio.LiveMode=false;                               // switch to EDIT mid-hold
            Assert.IsFalse(manager.clock.playing,"EDIT: the current hold now pauses");
            double paused=manager.clock.elapsed;
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual(paused,manager.clock.elapsed);

            audio.LiveMode=true;                                // and back to LIVE mid-hold
            Assert.IsTrue(manager.clock.playing);
            held.SetSelected(false);
            Assert.IsTrue(manager.clock.playing);

            audio.Pause();held.SetSelected(true);held.SetSelected(false);
            Assert.IsFalse(manager.clock.playing,"a manual pause still wins in LIVE");
            Assert.IsTrue(audio.IsManuallyPaused);
            Object.Destroy(root);Object.Destroy(clip);Object.Destroy(manager.design);yield return null;
        }
    }
}
