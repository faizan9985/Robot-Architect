using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class TimelineZoomPlayTests
    {
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
