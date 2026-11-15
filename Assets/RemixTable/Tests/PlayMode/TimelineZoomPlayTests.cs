using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class TimelineZoomPlayTests
    {
        [UnityTest] public IEnumerator InlineToolbarSwitchesMenusAndTracksGestureZoomAtDoubleSpeed()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var nav=manager.navigationToolbar;var ui=nav.zoom;var speed=nav.speed;
            var workspacePosition=manager.workspace.position;var workspaceRotation=manager.workspace.rotation;float height=nav.transform.localPosition.y;
            var lines=manager.workspace.GetComponentsInChildren<LineRenderer>();var playhead=System.Array.Find(lines,l=>l.name=="FixedPlaybackIndicator");
            var first=playhead.GetPosition(0);var last=playhead.GetPosition(1);
            speed.speedButton.onClick.Invoke();nav.AdvanceAnimation(.1f);
            Assert.That(nav.SpeedReveal,Is.InRange(.01f,.99f));Assert.Greater(nav.zoomRoot.anchoredPosition.x,140);Assert.IsFalse(speed.optionsGroup.blocksRaycasts);
            var position=nav.zoomRoot.anchoredPosition;float width=speed.optionsPanel.sizeDelta.x;
            ui.expand.onClick.Invoke();Assert.AreEqual(position,nav.zoomRoot.anchoredPosition);Assert.AreEqual(width,speed.optionsPanel.sizeDelta.x);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(0,nav.SpeedReveal);Assert.AreEqual(1,nav.ZoomReveal);Assert.IsFalse(speed.optionsPanel.gameObject.activeSelf);Assert.IsTrue(ui.optionsGroup.blocksRaycasts);
            ui.detail.onClick.Invoke();Assert.AreEqual(4,manager.zoom.TargetSeconds);Assert.IsFalse(ui.optionsGroup.blocksRaycasts);
            yield return new WaitForSecondsRealtime(1.3f);Assert.AreEqual("4s",ui.buttonLabel.text);Assert.IsFalse(ui.options.activeSelf);
            speed.speedButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.33f);speed.optionButtons[7].onClick.Invoke();
            double before=manager.clock.elapsed;yield return new WaitForSecondsRealtime(.35f);Assert.Greater(manager.clock.elapsed,before+.4);
            Assert.AreEqual(2,manager.audioManager.PlaybackSpeed);ui.normal.onClick.Invoke();yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual("8s",ui.buttonLabel.text);Assert.AreEqual(8,manager.zoom.VisibleSeconds,.01f);
            var ribbon=manager.ribbons[0];ribbon.UpdateWaveform();
            Assert.AreEqual(4,Mathf.Repeat(ribbon.Timestamp(manager.design.width*.5f)-ribbon.Timestamp(0),manager.clock.duration),.001f,"eight song seconds remain eight song seconds at 2x");
            var gestures=manager.interactions;gestures.enabled=false;
            var left=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=manager.workspace.TransformPoint(new Vector3(-.15f,.3f,-.25f))};
            var right=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=manager.workspace.TransformPoint(new Vector3(.15f,.3f,-.25f))};
            gestures.ProcessHands(left,right,.016f);left.position=manager.workspace.TransformPoint(new Vector3(-.12f,.3f,-.25f));right.position=manager.workspace.TransformPoint(new Vector3(.12f,.3f,-.25f));
            gestures.ProcessHands(left,right,.016f);Assert.AreEqual(RibbonInteractionManager.GestureState.Zooming,gestures.State);Assert.AreEqual(10,manager.zoom.TargetSeconds,.01f);
            yield return new WaitForSecondsRealtime(.4f);ui.Refresh();
            Assert.AreEqual((Mathf.Round(manager.zoom.VisibleSeconds*10)*.1f).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"s",ui.buttonLabel.text);
            foreach(var preset in new[]{ui.detail,ui.normal,ui.wide,ui.overview}) Assert.AreEqual(ui.idleColor,preset.targetGraphic.color);
            ui.expand.onClick.Invoke();Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);Assert.IsTrue(gestures.ZoomReleaseRequired);
            yield return new WaitForSecondsRealtime(.33f);ui.wide.onClick.Invoke();yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual("16s",ui.buttonLabel.text);Assert.AreEqual(2,manager.audioManager.PlaybackSpeed);
            Assert.AreEqual(first,playhead.GetPosition(0));Assert.AreEqual(last,playhead.GetPosition(1));Assert.AreEqual(workspacePosition,manager.workspace.position);Assert.AreEqual(workspaceRotation,manager.workspace.rotation);Assert.AreEqual(height,nav.transform.localPosition.y);
            var history=nav.GetComponent<RemixHistoryControls>();manager.history.BeginGesture(manager.automation);
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=StemId.Bass,parameter=ParameterType.Gain,wholeStem=true,value=.4f});manager.history.EndGesture(manager.automation);
            yield return null;Assert.IsTrue(history.undoButton.interactable);history.undoButton.onClick.Invoke();Assert.IsEmpty(manager.automation.edits);
            Assert.IsTrue(history.redoButton.interactable);history.redoButton.onClick.Invoke();Assert.AreEqual(.4f,manager.automation.edits[0].value);
            nav.enabled=false;Assert.IsFalse(nav.MenusVisible);Assert.IsFalse(speed.optionsGroup.blocksRaycasts);Assert.IsFalse(ui.optionsGroup.blocksRaycasts);
        }
        [UnityTest] public IEnumerator SavedSceneZoomKeepsAudioRunningAndFreezesDuringEditing()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var ui=Object.FindFirstObjectByType<TimelineZoomUI>();
            Assert.IsTrue(manager.audioManager.Ready);Assert.AreEqual(8,manager.zoom.VisibleSeconds);
            var gestures=manager.interactions;gestures.enabled=false;
            var root=manager.workspace;var position=root.position;var rotation=root.rotation;
            var mesh=manager.ribbons[0].GetComponent<MeshFilter>().sharedMesh;int count=mesh.vertexCount;
            double before=manager.clock.elapsed;
            var left=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=root.TransformPoint(new Vector3(-.15f,.3f,-.25f))};
            var right=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=root.TransformPoint(new Vector3(.15f,.3f,-.25f))};
            gestures.ProcessHands(left,right,.016f);
            left.position=root.TransformPoint(new Vector3(-.3f,.3f,-.25f));right.position=root.TransformPoint(new Vector3(.3f,.3f,-.25f));
            gestures.ProcessHands(left,right,.016f);Assert.AreEqual(4,manager.zoom.TargetSeconds);
            yield return new WaitForSeconds(.3f);
            Assert.Greater(manager.clock.elapsed,before);Assert.AreEqual(0,manager.audioManager.EditingPauseCount);
            Assert.That(manager.zoom.VisibleSeconds,Is.InRange(4,7.9f));foreach(var source in manager.audioManager.sources)Assert.IsTrue(source.isPlaying);
            gestures.enabled=true;gestures.enabled=false; // Actual runtime OnDisable must cancel and latch release.
            Assert.IsTrue(gestures.ZoomReleaseRequired);Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);
            ui.wide.onClick.Invoke();yield return new WaitForSeconds(.15f);
            Assert.That(manager.zoom.VisibleSeconds,Is.InRange(4.1f,15.99f));
            var ribbon=manager.ribbons[0];ribbon.SetSelected(true);float frozen=manager.zoom.VisibleSeconds;double time=manager.clock.elapsed;
            ui.overview.onClick.Invoke();yield return new WaitForSeconds(.3f);
            Assert.AreEqual(32,manager.zoom.TargetSeconds);Assert.AreEqual(frozen,manager.zoom.VisibleSeconds);Assert.AreEqual(time,manager.clock.elapsed);
            foreach(var r in manager.ribbons)Assert.AreEqual(ribbon.Timestamp(.1f),r.Timestamp(.1f));
            ribbon.SetSelected(false);yield return new WaitForSeconds(1.8f);
            Assert.AreEqual(32,manager.zoom.VisibleSeconds);Assert.Greater(manager.clock.elapsed,time);
            Assert.AreEqual(position,root.position);Assert.AreEqual(rotation,root.rotation);
            Assert.AreSame(mesh,ribbon.GetComponent<MeshFilter>().sharedMesh);Assert.AreEqual(count,mesh.vertexCount);
            Assert.That(ui.workspaceRange.text,Does.Contain("32").And.Contain("MAX"));
        }
    }
}
