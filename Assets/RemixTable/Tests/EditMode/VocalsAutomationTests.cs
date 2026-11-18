using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    [TestFixture(StemId.Drums)] [TestFixture(StemId.Bass)] [TestFixture(StemId.Melody)] [TestFixture(StemId.Vocals)]
    public class VocalsAutomationTests
    {
        readonly StemId target;
        public VocalsAutomationTests(StemId stem) { target=stem; }
        RemixTableManager manager;
        RibbonView vocals;
        string Song => manager.library.Current.id;
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            manager=Object.FindFirstObjectByType<RemixTableManager>();
            manager.clock.duration=manager.library.Current.duration;manager.clock.elapsed=65;
            foreach(var ribbon in manager.ribbons)
                typeof(RibbonView).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ribbon,null);
            vocals=manager.ribbons.Single(r=>r.stem==target);
            foreach(var ribbon in manager.ribbons) ribbon.UpdateWaveform();
        }
        [TearDown] public void Cleanup()
        {
            foreach(var ribbon in manager.ribbons) if(ribbon.WaveformMesh!=null) Object.DestroyImmediate(ribbon.WaveformMesh);
        }
        AutomationEdit Edit(ParameterType parameter,float value,float start=64,float end=66) =>
            new AutomationEdit{songId=Song,stem=target,parameter=parameter,value=value,start=start,end=end};
        static Vector4[] Data(Mesh mesh) { var data=new List<Vector4>();mesh.GetUVs(2,data);return data.ToArray(); }
        void CheckEveryTimestamp()
        {
            vocals.UpdateWaveform();var data=Data(vocals.WaveformMesh);Assert.AreEqual(vocals.WaveformMesh.vertexCount,data.Length);
            var shell=Data(vocals.GetComponent<VocalsHologram>().DepthMesh);
            var halo=Data(vocals.GetComponent<VocalsHologram>().HaloMesh);
            for(int i=0;i<=vocals.CurveSegmentCount;i++)
            {
                float x=(i/(float)vocals.CurveSegmentCount-.5f)*manager.design.width,t=vocals.Timestamp(x);
                float filter=manager.automation.Evaluate(Song,target,ParameterType.Effect,t,manager.clock.duration);
                float pitch=manager.PitchAt(target,t);
                Assert.AreEqual(StemFilter.SafeEffect(filter),data[i*2].x,"filter at "+t);
                Assert.AreEqual(pitch,data[i*2].y,"pitch at "+t);
                Assert.AreEqual(data[i*2],data[i*2+1]);
                for(int j=0;j<4;j++) {Assert.AreEqual(data[i*2],shell[i*4+j]);Assert.AreEqual(data[i*2],halo[i*4+j]);}
            }
        }
        [Test] public void NeutralIsZeroAndEffectsPreserveGeometryGainAndOtherStems()
        {
            Assert.AreEqual(Vector4.zero,VocalsHologram.EncodeAutomation(0,0));
            Assert.IsTrue(Data(vocals.WaveformMesh).All(v=>v==Vector4.zero));
            var vertices=vocals.WaveformMesh.vertices;var indices=vocals.WaveformMesh.triangles;
            var originalColors=vocals.WaveformMesh.colors;var materials=manager.ribbons.Select(r=>r.GetComponent<MeshRenderer>().sharedMaterial).ToArray();
            var otherVertices=manager.ribbons.Where(r=>r!=vocals).Select(r=>r.WaveformMesh.vertices).ToArray();
            var otherColors=manager.ribbons.Where(r=>r!=vocals).Select(r=>r.WaveformMesh.colors).ToArray();
            manager.automation.Upsert(Edit(ParameterType.Effect,-.8f));manager.automation.Upsert(Edit(ParameterType.Pitch,12));
            CheckEveryTimestamp();
            CollectionAssert.AreEqual(vertices,vocals.WaveformMesh.vertices);CollectionAssert.AreEqual(indices,vocals.WaveformMesh.triangles);
            CollectionAssert.AreEqual(originalColors,vocals.WaveformMesh.colors,"effects must not double-tint legacy vertex brightness");
            int k=0;foreach(var ribbon in manager.ribbons)
            {
                ribbon.UpdateWaveform();Assert.AreSame(materials[(int)ribbon.stem],ribbon.GetComponent<MeshRenderer>().sharedMaterial);
                if(ribbon==vocals)continue;
                CollectionAssert.AreEqual(otherVertices[k],ribbon.WaveformMesh.vertices);CollectionAssert.AreEqual(otherColors[k++],ribbon.WaveformMesh.colors);
                var otherData=Data(ribbon.WaveformMesh);
                Assert.AreEqual(ribbon.WaveformMesh.vertexCount,otherData.Length);
                Assert.IsTrue(otherData.All(v=>v==Vector4.zero),"Target automation must not leak into another hologram");
            }
            manager.automation.Upsert(Edit(ParameterType.Gain,2));vocals.UpdateWaveform();
            var boosted=vocals.WaveformMesh.vertices;
            Assert.IsTrue(vertices.Where((v,i)=>v!=boosted[i]).Any(),"gain still sculpts amplitude");
            manager.automation.ResetStem(Song,target);vocals.UpdateWaveform();
            CollectionAssert.AreEqual(vertices,vocals.WaveformMesh.vertices);CollectionAssert.AreEqual(originalColors,vocals.WaveformMesh.colors);
            Assert.IsTrue(Data(vocals.WaveformMesh).All(v=>v==Vector4.zero));
        }
        [Test] public void RegionCurvesPrecedenceWrappingZoomAndSongSelectionUseAuthoritativeValues()
        {
            var filter=Edit(ParameterType.Effect,-1);filter.curve=AnimationCurve.Linear(0,-1,2,1);
            manager.automation.Upsert(filter);manager.automation.Upsert(Edit(ParameterType.Pitch,7));
            manager.automation.Upsert(Edit(ParameterType.Pitch,-12,65,65.5f));
            manager.automation.Upsert(Edit(ParameterType.Effect,.9f,manager.clock.duration-1,1));
            manager.automation.Upsert(Edit(ParameterType.Pitch,-5,manager.clock.duration-1,1));
            foreach(float zoom in new[]{4,8,16,32})
            {
                manager.zoom.Request(zoom);manager.zoom.Tick(10,false,.16f);
                foreach(double time in new[]{65d,manager.clock.duration-.2d,manager.clock.duration+65d})
                {manager.clock.elapsed=time;CheckEveryTimestamp();}
            }
            // Song-scoped automation must never leak into another song.
            manager.library.selectedIndex=1;manager.clock.duration=manager.library.Current.duration;
            CheckEveryTimestamp();Assert.IsTrue(Data(vocals.WaveformMesh).All(v=>v==Vector4.zero));
        }
        [Test] public void ReleaseUndoRedoResetAndDiskSaveLoadRestoreVisualState()
        {
            manager.history.BeginGesture(manager.automation);
            manager.automation.Upsert(Edit(ParameterType.Effect,-.7f));manager.automation.Upsert(Edit(ParameterType.Pitch,-12));
            Assert.IsTrue(manager.history.EndGesture(manager.automation));vocals.SetSelected(false);
            CheckEveryTimestamp();var edited=Data(vocals.WaveformMesh);
            Assert.IsTrue(edited.Any(v=>v.x<0 && v.y==-12));
            Assert.IsTrue(manager.history.Undo(manager.automation));vocals.UpdateWaveform();Assert.IsTrue(Data(vocals.WaveformMesh).All(v=>v==Vector4.zero));
            Assert.IsTrue(manager.history.Redo(manager.automation));vocals.UpdateWaveform();CollectionAssert.AreEqual(edited,Data(vocals.WaveformMesh));
            string folder=Path.Combine(Path.GetTempPath(),"vocals-automation-"+System.Guid.NewGuid().ToString("N"));
            try
            {
                var store=new RemixSaveStore(folder);store.Save(RemixSaveStore.Capture(Song,manager.automation,manager.mix));
                manager.automation.ResetSong(Song);vocals.UpdateWaveform();Assert.IsTrue(Data(vocals.WaveformMesh).All(v=>v==Vector4.zero));
                RemixSaveStore.Apply(store.Load(Song),Song,manager.automation,manager.mix);vocals.UpdateWaveform();
                CollectionAssert.AreEqual(edited,Data(vocals.WaveformMesh));
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        }
        [Test] public void HarmonicSpacingCoversBothOctavesAndSafeInputs()
        {
            float prior=0;
            for(int semitones=-12;semitones<=12;semitones++)
            {
                var data=VocalsHologram.EncodeAutomation(0,semitones);
                if(semitones==0){Assert.AreEqual(Vector4.zero,data);continue;}
                Assert.Greater(data.z,prior);prior=data.z;Assert.That(data.w,Is.InRange(0,1));
            }
            Assert.That(1/VocalsHologram.EncodeAutomation(0,-12).z,Is.EqualTo(.008f).Within(.000001));
            Assert.That(1/VocalsHologram.EncodeAutomation(0,12).z,Is.EqualTo(.002f).Within(.000001));
            Assert.AreEqual(Vector4.zero,VocalsHologram.EncodeAutomation(float.NaN,float.PositiveInfinity));
            Assert.AreEqual(1,VocalsHologram.EncodeAutomation(5,40).x);Assert.AreEqual(12,VocalsHologram.EncodeAutomation(5,40).y);
        }
    }
}
