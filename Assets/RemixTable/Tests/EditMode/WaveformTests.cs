using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RemixTable.Tests
{
    public class WaveformTests
    {
        const string Folder="Assets/RemixTable/Waveforms/";
        static WaveformCache Cache(string name) => AssetDatabase.LoadAssetAtPath<WaveformCache>(Folder+name+".rwave");
        // Independent direct float32 WAV reader, used only in editor tests.
        static WaveformCache.Envelope Raw(string file,int first,int count)
        {
            using(var r=new BinaryReader(File.OpenRead("Assets/RemixTable/Audio/NIVIROTest/Stems/"+file+".wav")))
            {
                r.BaseStream.Position=12;
                while(r.BaseStream.Position<r.BaseStream.Length)
                {
                    string id=new string(r.ReadChars(4));int size=r.ReadInt32();long next=r.BaseStream.Position+size+(size&1);
                    if(id!="data") { r.BaseStream.Position=next;continue; }
                    r.BaseStream.Position+=first*8L;float min=float.PositiveInfinity,max=float.NegativeInfinity;double energy=0;
                    for(int i=0;i<count*2;i++) { float x=r.ReadSingle();min=Math.Min(min,x);max=Math.Max(max,x);energy+=(double)x*x; }
                    return new WaveformCache.Envelope{minimum=min,maximum=max,rms=(float)Math.Sqrt(energy/(count*2))};
                }
            }
            throw new InvalidDataException();
        }
        [TestCase("drums")] [TestCase("bass")] [TestCase("other")] [TestCase("vocals")]
        public void RealCachesMatchSourceSamplesAndTimeline(string name)
        {
            var cache=Cache(name);Assert.NotNull(cache);Assert.AreEqual(44100,cache.SampleRate);
            Assert.That(cache.Duration,Is.EqualTo(230.017).Within(.001));Assert.Less(cache.DataBytes,1000000);
            // Exact base-bin times, including the final partial block and several song entrances.
            foreach(int block in new[]{0,1,5000,16000,40000,60000,(cache.SampleFrames-1)/cache.BlockFrames})
            {
                int first=block*cache.BlockFrames,count=Math.Min(cache.BlockFrames,cache.SampleFrames-first);
                var raw=Raw(name,first,count);var baked=cache.Query((double)first/cache.SampleRate,(double)count/cache.SampleRate);
                Assert.That(baked.minimum,Is.EqualTo(raw.minimum).Within(.0002),name+" negative peak at "+first);
                Assert.That(baked.maximum,Is.EqualTo(raw.maximum).Within(.0002),name+" positive peak at "+first);
                Assert.That(baked.rms,Is.EqualTo(raw.rms).Within(.0002));
            }
        }
        [TestCase(4)] [TestCase(8)] [TestCase(16)] [TestCase(32)]
        public void ZoomAggregationPreservesEveryInteriorTransientAndWrap(float seconds)
        {
            var cache=Cache("drums");double span=seconds/512d;
            Assert.AreEqual((int)Math.Floor(Math.Log(span*44100/128,2)),cache.LevelForSpan(span));
            for(int i=0;i<30;i++)
            {
                double start=30+i*.137;int first=(int)Math.Floor(start*44100/128)*128;
                int end=(int)Math.Ceiling((start+span)*44100/128)*128;
                var raw=Raw("drums",first,end-first);var cached=cache.Query(start,span);
                Assert.That(cached.minimum,Is.EqualTo(raw.minimum).Within(.0002));Assert.That(cached.maximum,Is.EqualTo(raw.maximum).Within(.0002));
            }
            var a=cache.Query(cache.Duration-.01,.04);var b=cache.Query(-.01,.04);
            Assert.AreEqual(a.minimum,b.minimum);Assert.AreEqual(a.maximum,b.maximum);
            var tail=cache.Query(cache.Duration-.01,.01);var head=cache.Query(0,.03);
            Assert.That(a.maximum,Is.EqualTo(Math.Max(tail.maximum,head.maximum)).Within(.0002));
            Assert.That(a.minimum,Is.EqualTo(Math.Min(tail.minimum,head.minimum)).Within(.0002));
        }
        [Test] public void BakerExtractsSignedStereoSilenceDynamicsAndSingleSampleImpulse()
        {
            string wav="Logs/WaveformFixture.wav",output="Logs/WaveformFixture.rwave";
            try
            {
                using(var w=new BinaryWriter(File.Create(wav)))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+513*8);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    w.Write(16);w.Write((short)3);w.Write((short)2);w.Write(44100);w.Write(44100*8);w.Write((short)8);w.Write((short)32);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(513*8);
                    for(int i=0;i<513;i++) { w.Write(i<128?0:i<256?.1f:i==300?1f:.3f);w.Write(i<128?0:i<256?-.05f:i==300?-.8f:-.2f); }
                }
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("RemixTable.Editor.WaveformBaker")).First(t=>t!=null);
                type.GetMethod("Bake",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{wav,output});
                using(var r=new BinaryReader(File.OpenRead(output)))
                {
                    r.ReadInt32();int rate=r.ReadInt32(),frames=r.ReadInt32(),block=r.ReadInt32();float scale=r.ReadSingle();int levels=r.ReadInt32();
                    var offsets=new int[levels];var counts=new int[levels];int bytes=0;
                    for(int i=0;i<levels;i++){offsets[i]=bytes;counts[i]=r.ReadInt32();bytes+=counts[i]*6;}r.ReadBytes(32);
                    var cache=ScriptableObject.CreateInstance<WaveformCache>();cache.Initialize(rate,frames,block,scale,r.ReadBytes(bytes),offsets,counts);
                    try
                    {
                        var silent=cache.Query(0,128d/44100);Assert.AreEqual(0,silent.minimum);Assert.AreEqual(0,silent.maximum);Assert.AreEqual(0,silent.rms);
                        var quiet=cache.Query(128d/44100,128d/44100);var loud=cache.Query(256d/44100,128d/44100);
                        Assert.That(quiet.maximum,Is.EqualTo(.1).Within(.00004));Assert.That(quiet.minimum,Is.EqualTo(-.05).Within(.00004));
                        Assert.That(loud.maximum,Is.EqualTo(1).Within(.00004));Assert.That(loud.minimum,Is.EqualTo(-.8).Within(.00004));Assert.Greater(loud.rms,quiet.rms);
                        Assert.That(cache.Query(0,512d/44100).maximum,Is.EqualTo(1).Within(.00004));
                        Assert.That(cache.Query(512d/44100,1d/44100).maximum,Is.EqualTo(.3).Within(.00004));
                    }
                    finally { Object.DestroyImmediate(cache); }
                }
            }
            finally { File.Delete(wav);File.Delete(output); }
        }
        [Test] public void RealDrumsRetainTransientContrastAndCacheProvenance()
        {
            var cache=Cache("drums");float loud=0,quiet=float.PositiveInfinity,maxCrest=0;
            for(int i=0;i<1000;i++)
            {
                var e=cache.Query(i*.23,.015625);loud=Math.Max(loud,e.rms);quiet=Math.Min(quiet,e.rms);
                if(e.rms>.01f)maxCrest=Math.Max(maxCrest,Math.Max(-e.minimum,e.maximum)/e.rms);
            }
            Assert.Greater(loud,quiet*10);Assert.Greater(maxCrest,2);
            foreach(string name in new[]{"drums","bass","other","vocals"})
            {
                using(var r=new BinaryReader(File.OpenRead(Folder+name+".rwave")))
                {
                    r.BaseStream.Position=20;int levels=r.ReadInt32();r.BaseStream.Position+=levels*4;
                    byte[] expected=r.ReadBytes(32);
                    using(var sha=System.Security.Cryptography.SHA256.Create())
                    using(var source=File.OpenRead("Assets/RemixTable/Audio/NIVIROTest/Stems/"+name+".wav"))
                        CollectionAssert.AreEqual(expected,sha.ComputeHash(source));
                }
            }
        }
        [Test] public void GeometryAutomationSilenceSelectionAndAllocationBudget()
        {
            EditorSceneManager.OpenScene("Assets/RemixTable/Scenes/RemixTable.unity");
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var ribbon=manager.ribbons[0];
            // Awake does not run in an edit-mode scene; initialize the renderer exactly once.
            typeof(RibbonView).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ribbon,null);
            manager.clock.elapsed=40;manager.clock.duration=(float)Cache("drums").Duration;ribbon.visualTime=40;
            var original=ribbon.EnvelopeAt(0,.01f);
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=ribbon.stem,parameter=ParameterType.Gain,start=39,end=41,value=2});
            var boosted=ribbon.EnvelopeAt(0,.01f);
            Assert.That(boosted.maximum,Is.EqualTo(original.maximum*2).Within(.00001));Assert.That(boosted.minimum,Is.EqualTo(original.minimum*2).Within(.00001));
            float outside=manager.design.width*.4f;var cache=manager.library.Current.stems.First(s=>s.id==ribbon.stem).waveform;
            Assert.That(ribbon.EnvelopeAt(outside,.01f).maximum,Is.EqualTo(cache.Query(ribbon.Timestamp(outside),.01).maximum).Within(.00001));
            ribbon.UpdateWaveform();var mesh=ribbon.WaveformMesh;var vertices=mesh.vertices;
            Assert.AreEqual(2048,mesh.vertexCount);Assert.AreEqual(1024,mesh.triangles.Length/3);
            foreach(var p in vertices)
            {
                Assert.That(new Vector2(p.x,p.z+manager.design.Radius).magnitude,Is.EqualTo(manager.design.Radius).Within(.00001));
                Assert.Less(Mathf.Abs(p.y),manager.design.laneHeight*.5f);
            }
            Assert.IsTrue(ribbon.CanSelectAt(ribbon.SurfacePoint(0),out _));
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=ribbon.stem,parameter=ParameterType.Gain,wholeStem=true,value=0});
            ribbon.UpdateWaveform();Assert.IsTrue(ribbon.CanSelectAt(ribbon.SurfacePoint(0),out _));
            foreach(var p in mesh.vertices) Assert.That(Mathf.Abs(p.y),Is.EqualTo(manager.design.waveformBaseline).Within(.000001));
            // Warm both native mesh setters and managed lookup before measuring actual updates.
            ribbon.UpdateWaveform();long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<50;i++)ribbon.UpdateWaveform();
            Assert.AreEqual(0,GC.GetAllocatedBytesForCurrentThread()-before);Assert.AreSame(mesh,ribbon.WaveformMesh);
            Object.DestroyImmediate(mesh);
        }
    }
}
