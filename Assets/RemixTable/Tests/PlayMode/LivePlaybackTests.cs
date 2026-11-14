using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class LivePlaybackTests
    {
        [UnityTest] public IEnumerator MergedSceneSupportsLiveSideModesPausedRegionsZoomAndTransport()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();
            var audio=manager.audioManager;var gestures=manager.interactions;gestures.enabled=false;
            var mode=Object.FindFirstObjectByType<RemixModeSelector>();
            var playback=Object.FindFirstObjectByType<RemixPlaybackModeSelector>();
            var transport=Object.FindFirstObjectByType<RemixTransport>();
            var ribbon=manager.ribbons[0];
            RibbonInteractionManager.HandSample Hand(float x,float lift=0) => new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=ribbon.SurfacePoint(x)+ribbon.transform.up*lift};
            playback.liveButton.onClick.Invoke();Assert.IsTrue(audio.LiveMode);
            foreach(var filter in new[]{false,true}) foreach(float side in new[]{-1f,1f})
            {
                if(filter) mode.filterButton.onClick.Invoke();else mode.volumeButton.onClick.Invoke();
                float x=side*manager.design.width*.45f;
                gestures.ProcessHands(Hand(x),default,.016f);
                Assert.AreSame(ribbon,gestures.ActiveRibbon);
                double before=manager.clock.elapsed;
                gestures.ProcessHands(Hand(x,.03f),default,.016f);
                Assert.IsTrue(manager.IsEditing);Assert.IsTrue(manager.clock.playing);
                var parameter=filter?ParameterType.Effect:ParameterType.Gain;
                Assert.IsTrue(manager.automation.edits.Exists(e=>e.stem==ribbon.stem && e.parameter==parameter));
                float window=manager.zoom.VisibleSeconds;
                yield return new WaitForSeconds(.1f);
                Assert.Greater(manager.clock.elapsed,before);Assert.AreEqual(window,manager.zoom.VisibleSeconds);
                foreach(var source in audio.sources) Assert.IsTrue(source.isPlaying);
                gestures.Cancel();
            }
            mode.filterButton.onClick.Invoke();
            gestures.ProcessHands(Hand(0),default,.016f);
            gestures.ProcessHands(Hand(0,-.06f),default,.016f);
            yield return new WaitForSeconds(.15f);
            Assert.Less(audio.Effect(ribbon.stem),-.4f,"live hand gesture reaches the playing stem filter");
            Assert.Less(audio.Lowpass(ribbon.stem).cutoffFrequency,5000);
            gestures.Cancel();
            playback.editButton.onClick.Invoke();Assert.IsFalse(audio.LiveMode);
            mode.filterButton.onClick.Invoke();
            gestures.ProcessHands(Hand(-manager.design.width*.45f),Hand(manager.design.width*.45f),.016f);
            Assert.AreEqual(RibbonInteractionManager.GestureState.TwoHandRegion,gestures.State);
            Assert.IsFalse(manager.clock.playing);
            double paused=manager.clock.elapsed;
            yield return new WaitForSeconds(.1f);Assert.AreEqual(paused,manager.clock.elapsed);
            gestures.Cancel();playback.liveButton.onClick.Invoke();
            var left=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=manager.workspace.TransformPoint(new Vector3(-.15f,.3f,-.25f))};
            var right=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=manager.workspace.TransformPoint(new Vector3(.15f,.3f,-.25f))};
            double beforeZoom=manager.clock.elapsed;
            gestures.ProcessHands(left,right,.016f);
            left.position=manager.workspace.TransformPoint(new Vector3(-.3f,.3f,-.25f));right.position=manager.workspace.TransformPoint(new Vector3(.3f,.3f,-.25f));
            gestures.ProcessHands(left,right,.016f);
            Assert.AreEqual(RibbonInteractionManager.GestureState.Zooming,gestures.State);
            Assert.AreEqual(4,manager.zoom.TargetSeconds);Assert.IsFalse(manager.IsEditing);
            yield return new WaitForSeconds(.3f);Assert.Greater(manager.clock.elapsed,beforeZoom);
            gestures.Cancel();
            transport.playPause.onClick.Invoke();Assert.IsTrue(audio.IsManuallyPaused);
            transport.playPause.onClick.Invoke();Assert.IsFalse(audio.IsManuallyPaused);
            var song=manager.library.Current;
            transport.nextSong.onClick.Invoke();Assert.AreNotSame(song,manager.library.Current);
            transport.previousSong.onClick.Invoke();Assert.AreSame(song,manager.library.Current);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            foreach(var source in audio.sources) Assert.NotNull(source.clip);
        }
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
