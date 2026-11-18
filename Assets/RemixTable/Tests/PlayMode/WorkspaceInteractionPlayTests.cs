using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace RemixTable.Tests
{
    public sealed class RecenterUiTestInteractor : MonoBehaviour,UnityEngine.XR.Interaction.Toolkit.UI.IUIInteractor
    {
        public UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceModel Snapshot=new UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceModel(0);
        public bool Available=true;
        public void UpdateUIModel(ref UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceModel model){}
        public bool TryGetUIModel(out UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceModel model){model=Snapshot;return Available;}
    }
    public class WorkspaceInteractionPlayTests : RemixSavedSceneTest
    {
        static readonly Pose Head=new Pose(new Vector3(0,1.2f,0),Quaternion.identity);
        static void Align(RemixTableManager m)
        {
            m.ProcessWorkspacePose(Head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            m.ProcessWorkspacePose(Head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
        }
        static RibbonInteractionManager.HandSample Hand(RemixTableManager m,float y,bool pinch=true)=>new RibbonInteractionManager.HandSample{valid=true,pinch=pinch,position=m.workspace.TransformPoint(new Vector3(0,y,-.006f))};
        [UnityTest] public IEnumerator RecenterUiPointerTapDragAndTrackingLossUseOneWorkspaceAuthority()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var m=Object.FindFirstObjectByType<RemixTableManager>();m.enabled=false;m.interactions.enabled=false;Align(m);
            var c=m.workspaceInteraction;var button=c.recenterControl;var rotation=m.workspace.rotation;var start=m.workspace.position;c.SetHeight(.5f);
            m.mix.SetSpatial(true);yield return null;var orb=Object.FindFirstObjectByType<SpatialStemField>().orbs[0];var relative=m.workspace.InverseTransformPoint(orb.transform.position);var sphereScale=orb.transform.lossyScale;
            var ray=new System.Collections.Generic.List<Vector3>{button.transform.position-Vector3.forward,button.transform.position+Vector3.forward};
            var data=new UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=99,rayPoints=ray,pointerCurrentRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=button.gameObject,worldPosition=button.transform.position}};
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            Assert.IsTrue(button.Captured);Assert.IsTrue(m.ExternalHold);button.button.onClick.Invoke();Assert.IsFalse(m.WorkspaceAligning,"poke click-on-down must not recenter");
            var delta=new Vector3(.13f,.09f,-.11f);ray[0]+=delta;ray[1]+=delta;
            yield return new WaitForSeconds(.2f);Assert.IsTrue(c.HasDragged);
            Assert.That(Vector3.Distance(m.workspace.position,start+delta),Is.LessThan(.003f));Assert.AreEqual(rotation,m.workspace.rotation);Assert.AreEqual(.5f,c.Height,.0001);
            Assert.That(Vector3.Distance(relative,m.workspace.InverseTransformPoint(orb.transform.position)),Is.LessThan(.001f));Assert.AreEqual(sphereScale,orb.transform.lossyScale);
            var released=m.workspace.position;UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);button.button.onClick.Invoke();
            yield return null;Assert.AreEqual(released,m.workspace.position);Assert.IsFalse(m.WorkspaceAligning);Assert.IsFalse(m.ExternalHold);Assert.AreEqual(.5f,c.Height,.0001);
            data.pointerCurrentRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=button.gameObject,worldPosition=button.transform.position};
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);ray.Clear();yield return null;
            Assert.IsFalse(button.Captured);Assert.IsFalse(c.OwnsGesture);Assert.IsFalse(m.ExternalHold);Assert.AreEqual(.5f,c.Height,.0001);
            ray.Add(button.transform.position-Vector3.forward);ray.Add(button.transform.position+Vector3.forward);
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);yield return null;
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);button.button.onClick.Invoke();
            Assert.IsTrue(m.WorkspaceAligning);Assert.AreEqual(1,c.VerticalScale);Assert.IsFalse(button.Captured);Align(m);
            var seated=m.design.SeatedPose(Head.position,Vector3.forward);Assert.That(Vector3.Distance(seated.position,m.workspace.position),Is.LessThan(.0001));
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);m.RequestWorkspaceAlignment();Assert.IsFalse(button.Captured);Assert.IsFalse(m.ExternalHold);
        }
        [UnityTest] public IEnumerator RegisteredXriSourceUsesFreshPressAndReleaseEventsThenCommittedHeldModel()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var m=Object.FindFirstObjectByType<RemixTableManager>();m.enabled=false;m.interactions.enabled=false;Align(m);var c=m.workspaceInteraction;c.SetHeight(.5f);var button=c.recenterControl;
            var es=UnityEngine.EventSystems.EventSystem.current;var module=es.currentInputModule as UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule;Assert.NotNull(module);
            var go=new GameObject("registered recenter UI test source");var hand=go.AddComponent<RecenterUiTestInteractor>();module.RegisterInteractor(hand);
            try
            {
                Assert.IsTrue(module.GetTrackedDeviceModel(hand,out var registered));
                var ray=new System.Collections.Generic.List<Vector3>{button.transform.position-Vector3.forward,button.transform.position+Vector3.forward};
                var data=new UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData(es){pointerId=registered.pointerId,rayPoints=ray,pointerCurrentRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=button.gameObject,worldPosition=button.transform.position}};
                Assert.AreSame(hand,data.interactor);Assert.IsFalse(hand.Snapshot.select,"registered cache still has the preceding frame during pointer down");
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);Assert.IsTrue(button.Captured);Assert.IsTrue(m.ExternalHold);
                hand.Snapshot.select=true;hand.Snapshot.raycastPoints.AddRange(ray);
                var start=m.workspace.position;var delta=new Vector3(.10f,.04f,-.06f);for(int i=0;i<2;i++){hand.Snapshot.raycastPoints[i]+=delta;ray[i]+=delta;}
                yield return new WaitForSeconds(.2f);Assert.That(Vector3.Distance(start+delta,m.workspace.position),Is.LessThan(.003f));
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);button.button.onClick.Invoke();Assert.IsFalse(m.WorkspaceAligning);
                data.pointerCurrentRaycast=new UnityEngine.EventSystems.RaycastResult{gameObject=button.gameObject,worldPosition=button.transform.position};
                hand.Snapshot.select=false;UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);Assert.IsTrue(button.Captured);Assert.IsFalse(c.HasDragged);hand.Snapshot.select=true;
                // Tracking teardown clears the event ray before the public cached model is updated.
                ray.Clear();UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);button.button.onClick.Invoke();
                Assert.IsFalse(button.Captured);Assert.IsFalse(m.ExternalHold);Assert.IsFalse(m.WorkspaceAligning);Assert.AreEqual(.5f,c.Height,.0001);
                ray.Add(button.transform.position-Vector3.forward);ray.Add(button.transform.position+Vector3.forward);
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);Assert.IsTrue(button.Captured);hand.Available=false;yield return null;
                Assert.IsFalse(button.Captured);Assert.IsFalse(m.ExternalHold);Assert.IsFalse(m.WorkspaceAligning);
            }
            finally{c.CancelOwnedGesture();module.UnregisterInteractor(hand);Object.Destroy(go);}
        }
        [UnityTest] public IEnumerator ScalingAndPlacementSurviveSongChangesAndReleaseWithoutEditingOrSnapping()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var m=Object.FindFirstObjectByType<RemixTableManager>();m.enabled=false;m.interactions.enabled=false;Align(m);var c=m.workspaceInteraction;
            m.interactions.ProcessHands(Hand(m,-.07f),Hand(m,.07f),.016f);Assert.AreEqual(RemixWorkspaceInteraction.Ownership.VerticalScale,c.State);
            for(int i=0;i<10;i++){m.interactions.ProcessHands(Hand(m,-.12f),Hand(m,.12f),.016f);yield return null;}
            Assert.Greater(c.VerticalScale,1.5f);Assert.IsEmpty(m.automation.edits);Assert.IsFalse(m.history.GestureOpen);
            var height=c.Height;m.interactions.ProcessHands(Hand(m,-.12f,false),Hand(m,.12f),.016f);yield return null;Assert.AreEqual(height,c.Height);
            m.interactions.ProcessHands(Hand(m,-.12f,false),Hand(m,.12f,false),.016f);Assert.IsFalse(c.ReleaseRequired);
            m.MoveWorkspace(m.workspace.position+new Vector3(.1f,.03f,-.04f));var position=m.workspace.position;
            // Exercise the actual SongChanged subscription, not a separate transform persistence path.
            m.enabled=true;m.library.Select(m.library.selectedIndex+1);m.enabled=false;Align(m);yield return null;
            Assert.AreEqual(height,c.Height);Assert.That(Vector3.Distance(position,m.workspace.position),Is.LessThan(.0001f));
            m.interactions.ProcessHands(Hand(m,-.07f),Hand(m,.07f),.016f);m.RequestWorkspaceAlignment();Assert.IsFalse(c.OwnsGesture);Assert.IsFalse(m.ExternalHold);Assert.IsTrue(c.ReleaseRequired);
        }
    }
}
