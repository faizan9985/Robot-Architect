using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace RemixTable.Tests
{
    public sealed class SpatialVisualTestInteractor : XRBaseInteractor
    {
        public IXRInteractable Target;
        public bool SelectRequested,HoverRequested=true;
        public override bool isSelectActive=>SelectRequested;
        public override bool isHoverActive=>HoverRequested;
        public override void GetValidTargets(List<IXRInteractable> targets){targets.Clear();if(Target!=null)targets.Add(Target);}
    }
    public class SpatialOrbHologramPlayTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator AuthoritativeXriHoverGrabReleaseTrackingAndRecenterPreserveSpatialBehavior()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.interactions.enabled=false;manager.enabled=false;
            var head=new Pose(manager.workspaceCamera.transform.position,manager.workspaceCamera.transform.rotation);
            manager.RequestWorkspaceAlignment();manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            Assert.IsFalse(manager.WorkspaceAligning);Assert.IsTrue(manager.WorkspaceTracked);
            var field=Object.FindFirstObjectByType<SpatialStemField>();manager.mix.SetSpatial(true);yield return null;
            var orb=field.orbs[(int)StemId.Vocals];var visual=orb.GetComponent<SpatialOrbHologram>();
            var scale=orb.transform.localScale;float radius=orb.GetComponent<SphereCollider>().radius;var saved=manager.mix.positions[(int)StemId.Vocals];
            var go=new GameObject("authoritative spatial visual test interactor");go.SetActive(false);go.transform.position=orb.transform.position;
            var interactor=go.AddComponent<SpatialVisualTestInteractor>();interactor.interactionManager=orb.interactionManager;interactor.Target=orb;go.SetActive(true);
            try
            {
                yield return new WaitForSeconds(.15f);
                Assert.IsTrue(orb.isHovered);Assert.Greater(visual.HoverAmount,0);Assert.IsFalse(orb.isSelected);Assert.IsFalse(manager.ExternalHold);
                Assert.AreEqual(saved,manager.mix.positions[(int)StemId.Vocals]);
                interactor.SelectRequested=true;orb.interactionManager.SelectEnter((IXRSelectInteractor)interactor,(IXRSelectInteractable)orb);
                yield return new WaitForSeconds(.1f);
                Assert.IsTrue(orb.isSelected);Assert.IsTrue(manager.ExternalHold);Assert.Greater(visual.GrabAmount,.9f);
                var start=orb.transform.position;go.transform.position+=new Vector3(.12f,.04f,.05f);yield return new WaitForSeconds(.2f);
                Assert.Greater(Vector3.Distance(start,orb.transform.position),.04f);
                Assert.That(Vector3.Distance(manager.SpatialLocal(orb.transform.position),manager.mix.positions[(int)StemId.Vocals]),Is.LessThan(.001f));
                Assert.AreEqual(scale,orb.transform.localScale);Assert.AreEqual(radius,orb.GetComponent<SphereCollider>().radius);
                interactor.SelectRequested=false;interactor.HoverRequested=false;orb.interactionManager.SelectExit((IXRSelectInteractor)interactor,(IXRSelectInteractable)orb);
                Assert.Greater(visual.GrabAmount,.9f,"release does not snap");yield return new WaitForSeconds(.13f);
                Assert.That(visual.GrabAmount,Is.InRange(.05f,.85f));yield return new WaitForSeconds(.17f);Assert.That(visual.GrabAmount,Is.EqualTo(0).Within(.0001));Assert.IsFalse(manager.ExternalHold);
                // A real tracking-loss path removes the interactor; the existing XRI ownership exits normally.
                interactor.HoverRequested=true;interactor.SelectRequested=true;orb.interactionManager.SelectEnter((IXRSelectInteractor)interactor,(IXRSelectInteractable)orb);
                yield return new WaitForSeconds(.1f);go.SetActive(false);yield return new WaitForSeconds(.3f);
                Assert.IsFalse(orb.isSelected);Assert.IsFalse(manager.ExternalHold);Assert.AreEqual(0,visual.GrabAmount);
                go.SetActive(true);interactor.SelectRequested=true;orb.interactionManager.SelectEnter((IXRSelectInteractor)interactor,(IXRSelectInteractable)orb);yield return new WaitForSeconds(.1f);
                manager.ProcessWorkspacePose(head,false,Pose.identity,TrackingOriginModeFlags.Floor,0,.016f);yield return null;
                Assert.IsFalse(manager.WorkspaceTracked);Assert.AreEqual(0,visual.GrabAmount);Assert.AreEqual(0,visual.HoverAmount);
                // Existing recenter cancellation still releases XRI and the external gesture owner.
                manager.RequestWorkspaceAlignment();yield return null;
                Assert.IsFalse(orb.isSelected);Assert.IsFalse(manager.ExternalHold);Assert.AreEqual(0,visual.GrabAmount);
                interactor.SelectRequested=false;interactor.HoverRequested=false;
                manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,1,.5f);manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,1,.5f);yield return null;
                Assert.AreEqual(scale,orb.transform.localScale);Assert.AreEqual(radius,orb.GetComponent<SphereCollider>().radius);
                var tick=(System.Action)System.Delegate.CreateDelegate(typeof(System.Action),field,typeof(SpatialStemField).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance));
                tick();long before=System.GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<30;i++)tick();Assert.AreEqual(0,System.GC.GetAllocatedBytesForCurrentThread()-before);
                manager.mix.SetSpatial(false);yield return null;Assert.IsFalse(orb.gameObject.activeSelf);Assert.AreEqual(0,visual.GrabAmount);
            }
            finally {Object.Destroy(go);}
        }
    }
}
