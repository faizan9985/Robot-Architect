using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR;
namespace RemixTable.Tests
{
    public class WorkspaceRecenterPlayTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator SavedSceneRecentersCoherentlyAndReleasesPausedLiveAndZoomOwners()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();
            Assert.IsTrue(manager.anchored);Assert.NotNull(manager.workspaceCamera);
            Assert.NotNull(manager.navigationToolbar);Assert.NotNull(manager.speedSelector);
            Assert.NotNull(Object.FindFirstObjectByType<RemixToolbox>());Assert.NotNull(Object.FindFirstObjectByType<RemixMixControls>());
            Assert.NotNull(Object.FindFirstObjectByType<RemixPersistence>());
            var root=manager.workspace;var audio=manager.audioManager;var gestures=manager.interactions;
            manager.enabled=false;gestures.enabled=false;
            var ribbon=manager.ribbons[0];
            var transport=root.GetComponentInChildren<RemixTransport>();
            var controls=root.GetComponentsInChildren<Transform>(true);
            var locals=new Vector3[controls.Length];for(int i=0;i<locals.Length;i++)locals[i]=controls[i].localPosition;
            var palmParent=manager.palmMenuCanvas.transform.parent;
            var head=new Pose(manager.workspaceCamera.transform.position,Quaternion.Euler(0,60,0));
            void Align(int count)
            {
                manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,count,.4f);
                manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,count,.4f);
            }
            RibbonInteractionManager.HandSample Hand(float lift=0) => new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=ribbon.SurfacePoint(manager.design.width*.45f)+ribbon.transform.up*lift};
            void Release()
            {
                var release=Hand();release.pinch=false;gestures.ProcessHands(release,release,.016f);
            }
            for(int pass=0;pass<3;pass++)
            {
                audio.LiveMode=pass==1;
                gestures.Mode=pass==0?RibbonInteractionManager.ControlMode.Volume:pass==1?RibbonInteractionManager.ControlMode.Filter:RibbonInteractionManager.ControlMode.Pitch;
                Release();gestures.ProcessHands(Hand(),default,.016f);gestures.ProcessHands(Hand(.03f),default,.016f);
                Assert.AreEqual(1,audio.EditingPauseCount);Assert.AreEqual(pass==1,manager.clock.playing);
                var edit=manager.automation.edits[manager.automation.edits.Count-1];
                float start=edit.start,end=edit.end,value=edit.value;
                Assert.IsTrue(manager.history.GestureOpen);
                int undoBefore=manager.history.UndoCount;
                manager.RequestWorkspaceAlignment();head.position+=new Vector3(.2f,.04f,-.1f);Align(pass+1);
                Assert.IsFalse(manager.WorkspaceAligning);Assert.IsNull(gestures.ActiveRibbon);Assert.IsFalse(manager.IsEditing);
                Assert.IsFalse(manager.history.GestureOpen,"recenter closes the undo gesture");
                Assert.AreEqual(undoBefore+1,manager.history.UndoCount,"recenter preserves the completed edit as one undo step");
                Assert.AreEqual(0,audio.EditingPauseCount);Assert.IsTrue(manager.clock.playing);
                Assert.AreEqual(start,edit.start);Assert.AreEqual(end,edit.end);Assert.AreEqual(value,edit.value);
                for(int i=0;i<locals.Length;i++) if(controls[i]!=root) Assert.AreEqual(locals[i],controls[i].localPosition,controls[i].name);
                Assert.AreSame(palmParent,manager.palmMenuCanvas.transform.parent);
                Assert.That(root.position.y,Is.EqualTo(head.position.y-manager.design.workspaceEyeDrop).Within(.00001));
                yield return new WaitForSeconds(.3f);
                foreach(var source in audio.sources) Assert.IsTrue(source.isPlaying);
                foreach(var source in audio.sources) Assert.That(System.Math.Abs(source.timeSamples-audio.sources[0].timeSamples),Is.LessThan(1024));
            }
            Release();
            var left=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=root.TransformPoint(new Vector3(-.15f,.3f,-.25f))};
            var right=new RibbonInteractionManager.HandSample{valid=true,pinch=true,position=root.TransformPoint(new Vector3(.15f,.3f,-.25f))};
            gestures.ProcessHands(left,right,.016f);Assert.AreEqual(RibbonInteractionManager.GestureState.ZoomPending,gestures.State);
            manager.RequestWorkspaceAlignment();head.rotation=Quaternion.Euler(0,-80,0);Align(3);
            Assert.AreEqual(RibbonInteractionManager.GestureState.Idle,gestures.State);Assert.IsTrue(gestures.ZoomReleaseRequired);
            Assert.IsTrue(manager.clock.playing);Assert.AreEqual(0,audio.EditingPauseCount);
            transport.playPause.onClick.Invoke();Assert.IsTrue(audio.IsManuallyPaused);
            ribbon.SetSelected(true);manager.RequestWorkspaceAlignment();Align(4);
            Assert.IsFalse(manager.clock.playing);Assert.AreEqual(0,audio.EditingPauseCount);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(4,manager.ribbons.Length);
            foreach(float x in new[]{-.5f,-.45f,.45f,.5f})
                Assert.IsTrue(ribbon.CanSelectAt(ribbon.SurfacePoint(manager.design.width*x),out _));
            audio.Resume();
        }
    }
}
