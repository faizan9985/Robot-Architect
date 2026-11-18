using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace RemixTable.Tests
{
    public class WorkspaceInteractionPlayTests : RemixSavedSceneTest
    {
        static readonly Pose Head=new Pose(new Vector3(0,1.2f,0),Quaternion.identity);
        static void Align(RemixTableManager m)
        {
            m.ProcessWorkspacePose(Head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            m.ProcessWorkspacePose(Head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
        }
        static RibbonInteractionManager.HandSample Hand(RemixTableManager m,float y,bool pinch=true)=>new RibbonInteractionManager.HandSample{valid=true,pinch=pinch,position=m.workspace.TransformPoint(new Vector3(0,y,-.006f))};
        [UnityTest] public IEnumerator XriBorderMovesWholeWorkspaceAndCancelsOnInteractorLossAndRecenter()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var m=Object.FindFirstObjectByType<RemixTableManager>();m.enabled=false;m.interactions.enabled=false;Align(m);
            var c=m.workspaceInteraction;var handle=c.navigationHandle;var rotation=m.workspace.rotation;var start=m.workspace.position;
            m.mix.SetSpatial(true);yield return null;
            var orb=Object.FindFirstObjectByType<SpatialStemField>().orbs[0];var relative=m.workspace.InverseTransformPoint(orb.transform.position);var sphereScale=orb.transform.lossyScale;
            var go=new GameObject("workspace XRI test hand");go.SetActive(false);go.transform.position=handle.transform.position;
            var hand=go.AddComponent<SpatialVisualTestInteractor>();hand.interactionManager=handle.interactionManager;hand.Target=handle;go.SetActive(true);
            try
            {
                yield return null;hand.SelectRequested=true;handle.interactionManager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)handle);
                Assert.AreEqual(RemixWorkspaceInteraction.Ownership.NavigationDrag,c.State);Assert.IsTrue(m.ExternalHold);
                go.transform.position+=new Vector3(.13f,.09f,-.11f);yield return new WaitForSeconds(.2f);
                Assert.That(Vector3.Distance(m.workspace.position,start+new Vector3(.13f,.09f,-.11f)),Is.LessThan(.003f));Assert.AreEqual(rotation,m.workspace.rotation);
                Assert.That(Vector3.Distance(relative,m.workspace.InverseTransformPoint(orb.transform.position)),Is.LessThan(.001f));Assert.AreEqual(sphereScale,orb.transform.lossyScale);
                var released=m.workspace.position;hand.SelectRequested=false;handle.interactionManager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)handle);yield return null;
                Assert.AreEqual(released,m.workspace.position);Assert.IsFalse(m.ExternalHold);Assert.IsFalse(c.OwnsGesture);
                hand.SelectRequested=true;handle.interactionManager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)handle);go.SetActive(false);yield return null;
                Assert.IsFalse(handle.isSelected);Assert.IsFalse(m.ExternalHold);Assert.IsFalse(c.OwnsGesture);
                go.SetActive(true);hand.SelectRequested=true;handle.interactionManager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)handle);yield return null;
                c.SetHeight(.5f);m.RecenterWorkspace();hand.SelectRequested=false;hand.HoverRequested=false;Align(m);yield return null;
                Assert.IsFalse(handle.isSelected);Assert.IsFalse(m.ExternalHold);Assert.AreEqual(1,c.VerticalScale);
                var seated=m.design.SeatedPose(Head.position,Vector3.forward);Assert.That(Vector3.Distance(seated.position,m.workspace.position),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(seated.rotation,m.workspace.rotation),Is.LessThan(.0001f));
            }
            finally {Object.Destroy(go);}
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
