using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    public class StemHologramTests
    {
        static Vector4 Linear(Vector4 value) => (Vector4)new Color(value.x,value.y,value.z,1).linear;
        [Test] public void AllPalettesUseExactSrgbTargetsAndOriginalMaterials()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.clock.duration=manager.library.Current.duration;manager.clock.elapsed=36;
            var expected=new Dictionary<StemId,string>{{StemId.Vocals,"#B49AFF"},{StemId.Drums,"#FFB26B"},{StemId.Bass,"#54DBAA"},{StemId.Melody,"#5DD6EF"}};
            var parts=new List<MeshRenderer>();int vertices=0,triangles=0;
            foreach(var ribbon in manager.ribbons)
            {
                var before=ribbon.GetComponentsInChildren<Collider>(true);var bounds=ribbon.InteractionBounds;
                typeof(RibbonView).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ribbon,null);ribbon.UpdateWaveform();
                var hologram=ribbon.GetComponent<VocalsHologram>();Assert.NotNull(hologram);Assert.AreEqual(ribbon.stem,hologram.Stem);
                Assert.AreEqual(1,ribbon.GetComponents<VocalsHologram>().Length);
                var palette=StemHologramPalette.For(ribbon.stem);ColorUtility.TryParseHtmlString(expected[ribbon.stem],out Color color);
                Assert.AreEqual(new Vector4(color.r,color.g,color.b,1),palette.Primary);
                var renderers=new[]{ribbon.GetComponent<MeshRenderer>(),ribbon.transform.Find(VocalsHologram.DepthName(ribbon.stem)).GetComponent<MeshRenderer>(),ribbon.transform.Find(VocalsHologram.HaloName(ribbon.stem)).GetComponent<MeshRenderer>()};
                foreach(var renderer in renderers)
                {
                    parts.Add(renderer);Assert.AreEqual(1,renderer.sharedMaterial.passCount);var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                    if(ribbon.stem==StemId.Vocals)Assert.IsTrue(block.isEmpty,"approved Vocals must use its unchanged default constants");
                    else
                    {
                        Assert.IsTrue(renderer.sharedMaterial.IsKeywordEnabled("_STEM_PALETTE"));Assert.AreEqual(Linear(palette.Primary),block.GetVector("_PrimaryLinear"));
                        Assert.AreEqual(Linear(palette.Shadow),block.GetVector("_DeepLinear"));Assert.AreEqual(Linear(palette.Highlight),block.GetVector("_HighlightLinear"));
                        Assert.AreEqual(Linear(palette.Edge),block.GetVector("_EdgeLinear"));Assert.AreEqual(Linear(palette.Lowpass),block.GetVector("_LowpassLinear"));
                    }
                }
                Assert.AreSame(Resources.Load<Material>(VocalsHologram.BodyFor(ribbon.stem)),renderers[0].sharedMaterial);Assert.AreSame(renderers[0].sharedMaterial,renderers[1].sharedMaterial);
                Assert.AreSame(Resources.Load<Material>(VocalsHologram.HaloFor(ribbon.stem)),renderers[2].sharedMaterial);
                CollectionAssert.AreEqual(before,ribbon.GetComponentsInChildren<Collider>(true));Assert.AreEqual(bounds,ribbon.InteractionBounds);
                foreach(var mesh in new[]{ribbon.WaveformMesh,hologram.DepthMesh,hologram.HaloMesh}){vertices+=mesh.vertexCount;triangles+=mesh.triangles.Length/3;}
            }
            Assert.AreEqual(12,parts.Count);Assert.AreEqual(4,parts.Select(r=>r.sharedMaterial).Distinct().Count());
            Assert.LessOrEqual(vertices,82000);Assert.LessOrEqual(triangles,99000);
            Debug.Log($"STEM_RENDER_BUDGET parts={parts.Count} materials=4 vertices={vertices} triangles={triangles}");
            foreach(var ribbon in manager.ribbons)Object.DestroyImmediate(ribbon.WaveformMesh);
        }
    }
}
