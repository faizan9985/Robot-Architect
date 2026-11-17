using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    public class VocalsHologramTests
    {
        [Test] public void SavedSceneAssignsOnlyVocalsAndPreservesGeometryAndTargets()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            var manager=Object.FindFirstObjectByType<RemixTableManager>();
            foreach(var ribbon in manager.ribbons)
            {
                var material=ribbon.GetComponent<MeshRenderer>().sharedMaterial;
                var colliders=ribbon.GetComponentsInChildren<Collider>(true);
                var bounds=ribbon.InteractionBounds;
                typeof(RibbonView).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ribbon,null);
                manager.clock.elapsed=65;ribbon.UpdateWaveform();
                if(ribbon.stem!=StemId.Vocals)
                {
                    Assert.AreSame(material,ribbon.GetComponent<MeshRenderer>().sharedMaterial);
                    Assert.IsNull(ribbon.GetComponent<VocalsHologram>());continue;
                }
                Assert.AreSame(Resources.Load<Material>(VocalsHologram.BodyPath),ribbon.GetComponent<MeshRenderer>().sharedMaterial);
                var mesh=ribbon.WaveformMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                var envelope=ribbon.EnvelopeAt(0,.02f);
                ribbon.GetComponent<VocalsHologram>().UpdateGeometry(mesh.vertices,mesh.colors);
                CollectionAssert.AreEqual(vertices,mesh.vertices);CollectionAssert.AreEqual(triangles,mesh.triangles);
                Assert.AreEqual(envelope,ribbon.EnvelopeAt(0,.02f));Assert.AreEqual(bounds,ribbon.InteractionBounds);
                CollectionAssert.AreEqual(colliders,ribbon.GetComponentsInChildren<Collider>(true));
                Assert.IsTrue(ribbon.CanSelectAt(ribbon.SurfacePoint(0),out _));
                var shell=ribbon.transform.Find("Vocals depth").GetComponent<MeshFilter>().sharedMesh.vertices;
                var halo=ribbon.transform.Find("Vocals halo").GetComponent<MeshFilter>().sharedMesh.vertices;
                for(int i=0;i<vertices.Length/2;i++)
                {
                    Assert.AreEqual(vertices[i*2],shell[i*4]);Assert.AreEqual(vertices[i*2+1],shell[i*4+1]);
                    Assert.That(Vector3.Distance(shell[i*4],shell[i*4+2]),Is.EqualTo(VocalsHologram.Depth).Within(.000001));
                    Assert.AreEqual(vertices[i*2],halo[i*4]);Assert.AreEqual(vertices[i*2+1],halo[i*4+2]);
                    Assert.That(halo[i*4].y-halo[i*4+1].y,Is.EqualTo(VocalsHologram.HaloWidth).Within(.000001));
                }
            }
        }
        [Test] public void ResourceMaterialsIncludeValidShaderAndCorrectDepthBlending()
        {
            foreach(string name in new[]{VocalsHologram.BodyPath,VocalsHologram.HaloPath})
            {
                var material=Resources.Load<Material>(name);Assert.NotNull(material);
                Assert.AreEqual("RemixTable/VocalsHolographic",material.shader.name);
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
                string path=AssetDatabase.GetAssetPath(material);
                Assert.IsTrue(path.StartsWith("Assets/Resources/"),"Resources must survive build stripping");
                CollectionAssert.Contains(AssetDatabase.GetDependencies(path),AssetDatabase.GetAssetPath(material.shader));
                bool halo=name==VocalsHologram.HaloPath;
                Assert.AreEqual(halo?0:1,material.GetFloat("_ZWrite"));
                Assert.AreEqual(halo?3000:2010,material.renderQueue);
                Assert.AreEqual(halo?5:1,material.GetFloat("_SrcBlend"));
                Assert.AreEqual(halo?10:0,material.GetFloat("_DstBlend"));
            }
            Assert.AreEqual(ColorSpace.Linear,PlayerSettings.colorSpace);
        }
    }
}
