using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class SpeedPlaybackTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator PopoutPresetsDriveSynchronizedSongTimeAndPreserveEditingAndZoom()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var audio=manager.audioManager;
            var selector=Object.FindFirstObjectByType<RemixSpeedSelector>();
            var history=Object.FindFirstObjectByType<RemixHistoryControls>();
            var undo=history.undoButton.transform.position;var redo=history.redoButton.transform.position;var button=selector.speedButton.transform.position;
            Assert.AreEqual(1,audio.PlaybackSpeed);Assert.AreEqual("1\u00d7",selector.speedLabel.text);
            selector.speedButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.33f);
            Assert.IsTrue(selector.optionsGroup.blocksRaycasts);Assert.AreEqual(1,selector.optionsGroup.alpha);
            float zoom=manager.zoom.VisibleSeconds;
            for(int i=0;i<8;i++)
            {
                if(!selector.IsOpen) { selector.speedButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.33f); }
                double previous=manager.clock.elapsed;selector.optionButtons[i].onClick.Invoke();
                Assert.That(manager.clock.elapsed-previous,Is.InRange(0,.12),"speed change does not jump song time");
                yield return new WaitForSecondsRealtime(.1f);
                double before=manager.clock.elapsed,dsp=AudioSettings.dspTime;
                yield return new WaitForSecondsRealtime(.2f);
                double expected=(AudioSettings.dspTime-dsp)*RemixAudioManager.SpeedPreset(i);
                Assert.That(manager.clock.elapsed-before,Is.EqualTo(expected).Within(.08));
                foreach(var source in audio.sources)
                {
                    Assert.AreEqual(RemixAudioManager.SpeedPreset(i),source.pitch);Assert.IsTrue(source.isPlaying);
                    Assert.That(System.Math.Abs(source.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024));
                }
                // Coroutines resume before LateUpdate; sample the normal render path explicitly.
                foreach(var ribbon in manager.ribbons) { ribbon.UpdateWaveform();Assert.AreEqual(manager.clock.elapsed,ribbon.visualTime); }
                Assert.AreEqual(zoom,manager.zoom.VisibleSeconds);Assert.IsFalse(selector.IsOpen);
            }
            Assert.AreEqual(undo,history.undoButton.transform.position);Assert.AreEqual(redo,history.redoButton.transform.position);Assert.AreEqual(button,selector.speedButton.transform.position);
            var selected=manager.ribbons[0];audio.LiveMode=false;selected.SetSelected(true);
            double paused=manager.clock.elapsed;selector.SelectPreset(0);
            selector.Toggle();yield return new WaitForSecondsRealtime(.33f);
            Assert.IsTrue(selector.optionsGroup.blocksRaycasts);Assert.AreEqual(paused,manager.clock.elapsed);
            selected.SetSelected(false);yield return new WaitForSecondsRealtime(.3f);
            audio.LiveMode=true;selected.SetSelected(true);double live=manager.clock.elapsed;
            selector.SelectPreset(7);yield return new WaitForSecondsRealtime(.25f);
            Assert.Greater(manager.clock.elapsed,live+.3);Assert.IsTrue(manager.IsEditing);
            float t=manager.clock.Time;
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=selected.stem,parameter=ParameterType.Gain,start=t-.1f,end=t+.6f,value=.4f});
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=selected.stem,parameter=ParameterType.Effect,start=t-.1f,end=t+.6f,value=-.6f});
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(audio.sources[(int)selected.stem].volume,Is.EqualTo(audio.headroom*.4f).Within(.003));
            Assert.That(audio.Effect(selected.stem),Is.EqualTo(-.6f).Within(.03));
            selected.SetSelected(false);selector.SetOpen(false);selector.AdvanceAnimation(1);
            Assert.IsFalse(selector.optionsPanel.gameObject.activeSelf);Assert.IsFalse(selector.optionsGroup.blocksRaycasts);
        }
    }
}
