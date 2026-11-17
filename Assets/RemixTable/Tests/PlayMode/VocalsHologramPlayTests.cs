using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class VocalsHologramPlayTests : RemixSavedSceneTest
    {
        [UnityTest] public IEnumerator VisualDerivativesFollowPlaybackZoomGainAndSilence()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.interactions.enabled=false;
            var vocals=manager.ribbons.First(r=>r.stem==StemId.Vocals);
            var mesh=vocals.WaveformMesh;
            var original=Resources.Load<Material>(VocalsHologram.BodyPath);
            Assert.AreSame(original,vocals.GetComponent<MeshRenderer>().sharedMaterial);
            foreach(var ribbon in manager.ribbons.Where(r=>r.stem!=StemId.Vocals))
            {
                Assert.AreSame(manager.design.ribbonMaterial,ribbon.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.IsNull(ribbon.GetComponent<VocalsHologram>());
            }
            int colliderCount=vocals.GetComponentsInChildren<Collider>(true).Length;
            foreach(float zoom in new[]{4,16,32})
            {
                manager.zoom.Request(zoom);yield return new WaitForSeconds(.3f);
                foreach(float gain in new[]{0,1,2})
                {
                    manager.automation.Upsert(new AutomationEdit {songId=manager.library.Current.id,stem=StemId.Vocals,parameter=ParameterType.Gain,wholeStem=true,value=gain});
                    yield return null;
                    vocals.UpdateWaveform();
                    Assert.AreSame(mesh,vocals.WaveformMesh);
                    var front=mesh.vertices;
                    var rear=vocals.transform.Find("Vocals depth").GetComponent<MeshFilter>().sharedMesh.vertices;
                    var halo=vocals.transform.Find("Vocals halo").GetComponent<MeshFilter>().sharedMesh.vertices;
                    for(int i=0;i<front.Length/2;i++)
                    {
                        Assert.AreEqual(front[i*2],rear[i*4]);Assert.AreEqual(front[i*2+1],rear[i*4+1]);
                        Assert.AreEqual(front[i*2],halo[i*4]);Assert.AreEqual(front[i*2+1],halo[i*4+2]);
                    }
                    Assert.AreEqual(colliderCount,vocals.GetComponentsInChildren<Collider>(true).Length);
                    Assert.IsTrue(vocals.CanSelectAt(vocals.SurfacePoint(0),out _));
                }
            }
            vocals.UpdateWaveform();long before=System.GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20;i++) vocals.UpdateWaveform();
            Assert.AreEqual(0,System.GC.GetAllocatedBytesForCurrentThread()-before);
            manager.automation.ResetSong(manager.library.Current.id);
        }
    }
}
