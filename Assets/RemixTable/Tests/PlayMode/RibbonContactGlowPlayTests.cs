using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR;
namespace RemixTable.Tests
{
    public class RibbonContactGlowPlayTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator SavedScenePublishesPerHandContactsWithoutChangingTargetsAndClearsOnRecenter()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(1);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var gestures=manager.interactions;
            manager.enabled=false;gestures.enabled=false;manager.audioManager.Pause();manager.audioManager.enabled=false;manager.clock.playing=false;
            manager.navigationToolbar.ResetClosed();manager.palmMenuCanvas.gameObject.SetActive(false);
            var head=new Pose(manager.workspaceCamera.transform.position,manager.workspaceCamera.transform.rotation);
            manager.RequestWorkspaceAlignment();manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);manager.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            Assert.IsFalse(manager.WorkspaceAligning);
            var targets=manager.ribbons.Select(r=>r.GetComponentsInChildren<Collider>(true)).ToArray();
            var positions=manager.ribbons.Select(r=>r.transform.localPosition).ToArray();var scales=manager.ribbons.Select(r=>r.transform.localScale).ToArray();
            RibbonInteractionManager.HandSample Hand(RibbonView r,float x,bool pinch=false,float y=0)=>new RibbonInteractionManager.HandSample{valid=true,pinch=pinch,position=r.transform.TransformPoint(manager.design.ArcPoint(x,y))};
            var away=new RibbonInteractionManager.HandSample{valid=true,position=new Vector3(100,100,100)};
            foreach(var ribbon in manager.ribbons)
            {
                gestures.ProcessHands(Hand(ribbon,-.04f),away,.1f);yield return null;
                var block=new MaterialPropertyBlock();ribbon.GetComponent<Renderer>().GetPropertyBlock(block);Assert.AreEqual(1,block.GetVector("_Contact0").z);
                foreach(var other in manager.ribbons)if(other!=ribbon){other.GetComponent<Renderer>().GetPropertyBlock(block);Assert.AreEqual(Vector4.zero,block.GetVector("_Contact0"));}
                var vertices=ribbon.WaveformMesh.vertices;
                gestures.ProcessHands(Hand(ribbon,-.04f,true),away,.1f);yield return null;
                Assert.AreSame(ribbon,gestures.ActiveRibbon);Assert.IsTrue(ribbon.selected);CollectionAssert.AreEqual(vertices,ribbon.WaveformMesh.vertices);
                gestures.ProcessHands(Hand(ribbon,.04f,true),away,.1f);yield return null;
                ribbon.GetComponent<Renderer>().GetPropertyBlock(block);Assert.AreEqual(1,block.GetVector("_Contact0").w);Assert.Greater(block.GetVector("_Contact0").x,0);
                gestures.ProcessHands(away,away,.125f);yield return null;ribbon.GetComponent<Renderer>().GetPropertyBlock(block);Assert.That(block.GetVector("_Contact0").w,Is.EqualTo(.5f).Within(.0001));
                gestures.ProcessHands(away,away,.125f);yield return null;ribbon.GetComponent<Renderer>().GetPropertyBlock(block);Assert.AreEqual(Vector4.zero,block.GetVector("_Contact0"));
            }
            var first=manager.ribbons[0];gestures.ProcessHands(Hand(first,0,true),away,.1f);manager.RequestWorkspaceAlignment();yield return null;
            Assert.IsNull(gestures.ActiveRibbon);Assert.IsFalse(manager.IsEditing);
            for(int i=0;i<manager.ribbons.Length;i++)
            {
                var r=manager.ribbons[i];var block=new MaterialPropertyBlock();r.GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.AreEqual(Vector4.zero,block.GetVector("_Contact0"));Assert.AreEqual(positions[i],r.transform.localPosition);Assert.AreEqual(scales[i],r.transform.localScale);
                CollectionAssert.AreEqual(targets[i],r.GetComponentsInChildren<Collider>(true));Assert.AreEqual(0,r.GetComponentsInChildren<Light>(true).Length);
                Assert.AreSame(Resources.Load<Material>(VocalsHologram.BodyFor(r.stem)),r.GetComponent<Renderer>().sharedMaterial);
            }
            Assert.IsEmpty(manager.automation.edits);
        }
    }
}
