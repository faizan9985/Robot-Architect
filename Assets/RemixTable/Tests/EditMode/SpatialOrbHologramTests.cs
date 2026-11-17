using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    [TestFixture(StemId.Drums)] [TestFixture(StemId.Bass)] [TestFixture(StemId.Melody)] [TestFixture(StemId.Vocals)]
    public class SpatialOrbHologramTests
    {
        readonly StemId stem;
        public SpatialOrbHologramTests(StemId id){stem=id;}
        GameObject orb;SpatialOrbHologram visual;Renderer core;
        [SetUp] public void Setup()
        {
            orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.transform.localScale=Vector3.one*.09f;
            orb.transform.position=new Vector3(.2f,.1f,1.1f);core=orb.GetComponent<Renderer>();
            visual=orb.AddComponent<SpatialOrbHologram>();visual.Initialize(stem,core);
        }
        [TearDown] public void Cleanup()=>Object.DestroyImmediate(orb);
        [Test] public void SharedWaveformPaletteIdleGlowAndPhysicalGeometryArePreserved()
        {
            var palette=StemHologramPalette.For(stem);var block=new MaterialPropertyBlock();core.GetPropertyBlock(block);
            Assert.AreEqual((Vector4)new Color(palette.Primary.x,palette.Primary.y,palette.Primary.z,1).linear,block.GetVector("_PrimaryLinear"));
            Assert.AreEqual(stem,visual.Stem);Assert.Greater(visual.HaloIntensity,0);Assert.Greater(visual.RimIntensity,0);
            var mesh=orb.GetComponent<MeshFilter>().sharedMesh;var scale=orb.transform.localScale;var position=orb.transform.position;
            var collider=orb.GetComponent<SphereCollider>();float radius=collider.radius;var bounds=collider.bounds;
            visual.Initialize(stem,core);Assert.AreEqual(1,orb.transform.Cast<Transform>().Count(t=>t.name==SpatialOrbHologram.HaloName));
            visual.Advance(.2f,true,true,true,false);visual.Advance(.25f,false,false,true,false);
            Assert.AreEqual(scale,orb.transform.localScale);Assert.AreEqual(position,orb.transform.position);
            Assert.AreEqual(radius,collider.radius);Assert.AreEqual(bounds,collider.bounds);Assert.AreEqual(1,orb.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreSame(mesh,orb.transform.Find(SpatialOrbHologram.HaloName).GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(Vector3.one,visual.HaloRenderer.transform.localScale);Assert.AreEqual(0,orb.GetComponentsInChildren<Light>(true).Length);
            Assert.AreSame(Resources.Load<Material>(SpatialOrbHologram.CorePath),core.sharedMaterial);
            Assert.AreSame(Resources.Load<Material>(SpatialOrbHologram.HaloPath),visual.HaloRenderer.sharedMaterial);
            Assert.IsFalse(ShaderUtil.ShaderHasError(core.sharedMaterial.shader));
        }
        [Test] public void HoverGrabAndQuarterSecondReleaseHaveDistinctContinuousStates()
        {
            float idleCore=visual.CoreIntensity,idleHalo=visual.HaloIntensity,idleRim=visual.RimIntensity;
            visual.Advance(.1f,true,false,true,false);float hoverCore=visual.CoreIntensity;
            Assert.Greater(hoverCore,idleCore);Assert.Greater(visual.HaloIntensity,idleHalo);
            visual.Advance(.016f,true,true,true,false);Assert.Greater(visual.CoreIntensity,hoverCore,"grab brightens on its first rendered frame");
            visual.Advance(.1f,true,true,true,false);Assert.AreEqual(1,visual.GrabAmount);
            Assert.Greater(visual.RimIntensity,idleRim);Assert.Greater(visual.HaloExpansion,1.14f);
            visual.Advance(0,false,false,true,false);Assert.AreEqual(1,visual.GrabAmount,"release must not snap");
            visual.Advance(.125f,false,false,true,false);Assert.That(visual.GrabAmount,Is.EqualTo(.5f).Within(.00001));
            visual.Advance(.124f,false,false,true,false);Assert.Greater(visual.GrabAmount,0);
            visual.Advance(.001f,false,false,true,false);Assert.That(visual.GrabAmount,Is.EqualTo(0).Within(.000001));
            Assert.AreEqual(idleCore,visual.CoreIntensity);Assert.AreEqual(idleHalo,visual.HaloIntensity);
        }
        [Test] public void RegrabReversesFromCurrentIntensityAndCancellationClearsState()
        {
            visual.Advance(.1f,false,true,true,false);visual.Advance(.10f,false,false,true,false);float current=visual.GrabAmount;
            visual.Advance(0,true,true,true,false);Assert.AreEqual(current,visual.GrabAmount);
            visual.Advance(.01f,true,true,true,false);Assert.Greater(visual.GrabAmount,current);Assert.Less(visual.GrabAmount,1);
            float resumed=visual.GrabAmount;visual.Advance(0,false,false,true,false);Assert.AreEqual(resumed,visual.GrabAmount);
            visual.Advance(.125f,false,false,true,false);Assert.That(visual.GrabAmount,Is.EqualTo(resumed*.5f).Within(.00001));
            visual.Advance(.016f,true,true,true,false,true);Assert.AreEqual(0,visual.GrabAmount);Assert.AreEqual(0,visual.HoverAmount);
        }
        [Test] public void MixerAudibilityCapsMutedGrabAndSoloFollowsExistingMixerRules()
        {
            var mix=new StemMix();float audibleIdle=visual.CoreIntensity,idleHalo=visual.HaloIntensity;
            mix.SetMuted(stem,true);visual.Advance(.1f,false,false,mix.Audible(stem),mix.soloed[(int)stem]);float muted=visual.CoreIntensity;
            visual.Advance(.1f,true,true,mix.Audible(stem),mix.soloed[(int)stem]);
            Assert.Greater(visual.CoreIntensity,muted);Assert.Less(visual.CoreIntensity,audibleIdle);Assert.Less(visual.HaloIntensity,idleHalo);
            mix.SetSoloed(stem,true);Assert.IsTrue(mix.Audible(stem));visual.Advance(.1f,false,true,mix.Audible(stem),true);Assert.Greater(visual.CoreIntensity,audibleIdle);
            mix.SetSoloed(stem,false);mix.SetMuted(stem,false);mix.SetSoloed(stem==StemId.Vocals?StemId.Drums:StemId.Vocals,true);
            visual.Advance(.1f,true,true,mix.Audible(stem),false);Assert.Less(visual.CoreIntensity,audibleIdle);
        }
    }
    public class SpatialOrbSceneTests
    {
        [Test] public void SavedSceneOrbsKeepColliderBindingsAndShareTwoValidMaterials()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");var field=Object.FindFirstObjectByType<SpatialStemField>();
            var scales=field.orbs.Select(o=>o.transform.localScale).ToArray();var positions=field.orbs.Select(o=>o.transform.position).ToArray();
            var colliders=field.orbs.Select(o=>o.GetComponentsInChildren<Collider>(true)).ToArray();var targets=field.orbs.Select(o=>o.colliders.ToArray()).ToArray();
            field.InitializeVisuals();field.InitializeVisuals();
            for(int i=0;i<4;i++)
            {
                var orb=field.orbs[i];Assert.AreEqual(scales[i],orb.transform.localScale);Assert.AreEqual(positions[i],orb.transform.position);
                CollectionAssert.AreEqual(colliders[i],orb.GetComponentsInChildren<Collider>(true));CollectionAssert.AreEqual(targets[i],orb.colliders);
                Assert.AreEqual((StemId)i,orb.GetComponent<SpatialOrbHologram>().Stem);Assert.AreEqual(1,orb.GetComponents<SpatialOrbHologram>().Length);
            }
            foreach(string path in new[]{SpatialOrbHologram.CorePath,SpatialOrbHologram.HaloPath})
            {
                var mat=Resources.Load<Material>(path);Assert.NotNull(mat);Assert.IsFalse(ShaderUtil.ShaderHasError(mat.shader));
                Assert.AreEqual("RemixTable/SpatialHolographic",mat.shader.name);bool halo=path==SpatialOrbHologram.HaloPath;
                Assert.AreEqual(halo?0:1,mat.GetFloat("_ZWrite"));Assert.AreEqual(halo?3000:2010,mat.renderQueue);
                CollectionAssert.Contains(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(mat)),AssetDatabase.GetAssetPath(mat.shader));
            }
        }
    }
}
