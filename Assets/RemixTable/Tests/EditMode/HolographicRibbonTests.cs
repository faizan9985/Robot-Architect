using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace RemixTable.Tests
{
    public class HolographicRibbonTests
    {
        GameObject root;
        RemixTableManager manager;
        RemixDesign design;
        RibbonView ribbon;
        WaveformCache cache;
        [SetUp] public void Setup()
        {
            root=new GameObject("hologram test");root.SetActive(false);
            manager=root.AddComponent<RemixTableManager>();manager.enabled=false;
            design=ScriptableObject.CreateInstance<RemixDesign>();design.curvedTimeline=true;design.waveformMotion=0;
            manager.design=design;manager.workspace=root.transform;
            manager.library=root.AddComponent<SongLibraryManager>();
            cache=ScriptableObject.CreateInstance<WaveformCache>();
            var data=new byte[4096*6];
            for(int i=0;i<4096;i++)
            {
                float peak=i>=512 && i<1024?.1f:i>=1024 && i<1536?.8f:i==1700?1:0;
                short encoded=(short)Math.Round(peak*32767);int p=i*6;
                ushort negative=unchecked((ushort)-encoded);
                data[p]=(byte)negative;data[p+1]=(byte)(negative>>8);
                data[p+2]=(byte)encoded;data[p+3]=(byte)(encoded>>8);
                data[p+4]=(byte)encoded;data[p+5]=(byte)(encoded>>8);
            }
            cache.Initialize(51200,409600,100,1f/32767,data,new[]{0},new[]{4096});
            manager.library.songs=new[]{new SongMetadata{id="hologram",duration=8,stems=new[]{new StemMetadata{id=StemId.Drums,waveform=cache}}}};
            manager.clock.Reset(8);manager.clock.elapsed=4;
            var go=new GameObject("ribbon");go.transform.SetParent(root.transform);
            ribbon=go.AddComponent<RibbonView>();ribbon.manager=manager;ribbon.stem=StemId.Drums;
            manager.ribbons=new[]{ribbon};root.SetActive(true);
            typeof(RibbonView).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ribbon,null);
            ribbon.UpdateWaveform();
        }
        [TearDown] public void Cleanup()
        {
            if(ribbon.WaveformMesh!=null)Object.DestroyImmediate(ribbon.WaveformMesh);
            Object.DestroyImmediate(root);Object.DestroyImmediate(design);Object.DestroyImmediate(cache);
        }
        // Recover actual colored-core geometry, excluding the shader's feathered glow margin.
        Vector2 CoreAt(float time)
        {
            float window=manager.zoom.VisibleSeconds;
            float start=(float)(manager.clock.elapsed%8)-window*.5f;
            int i=Mathf.RoundToInt((time-start)/window*ribbon.CurveSegmentCount)*2;
            var points=ribbon.WaveformMesh.vertices;var edges=new List<Vector4>();ribbon.WaveformMesh.GetUVs(1,edges);
            float low=points[i].y,high=points[i+1].y;
            return new Vector2(Mathf.Lerp(low,high,edges[i].x),Mathf.Lerp(low,high,edges[i].y));
        }
        [Test] public void LoudQuietSilenceAndIsolatedDrumHitStayDistinct()
        {
            float quiet=CoreAt(1.5f).y,loud=CoreAt(2.5f).y;
            Assert.Greater(loud,quiet*3);
            Assert.That(CoreAt(.5f).y,Is.EqualTo(design.waveformBaseline).Within(.00001));
            Assert.That(CoreAt(.5f).x,Is.EqualTo(-design.waveformBaseline).Within(.00001));
            float hit=0;for(float t=3.30f;t<3.35f;t+=.002f)hit=Mathf.Max(hit,CoreAt(t).y);
            Assert.Greater(hit,quiet*3);Assert.Greater(hit,design.WaveformHeight(.85f)*.9f);
            // A silent timestamp within the unchanged central 90-degree editing zone.
            Assert.IsTrue(ribbon.CanSelectAt(ribbon.SurfacePoint(-design.width*.025f),out _));
        }
        [Test] public void SmoothingHasAnExplicitPeakFloorAndNeverFillsSilence()
        {
            for(int i=0;i<=10;i++)
            {
                float smoothing=i*.1f;
                Assert.That(RibbonEnvelope.SmoothPeak(1,0,smoothing,.85f),Is.InRange(.85f,1));
                Assert.AreEqual(0,RibbonEnvelope.SmoothPeak(0,1,smoothing,.85f));
            }
            Assert.AreEqual(1,RibbonEnvelope.SmoothPeak(1,0,0,0));
            Assert.AreEqual(1,RibbonEnvelope.SmoothPeak(1,0,1,1));
            Assert.AreEqual(0,RibbonEnvelope.SmoothPeak(1,0,1,0));
        }
        [Test] public void InterpolationIsRoundedContinuousAndBounded()
        {
            Assert.AreEqual(.2f,RibbonEnvelope.Interpolate(.2f,.8f,0));Assert.AreEqual(.8f,RibbonEnvelope.Interpolate(.2f,.8f,1));
            Assert.Less(RibbonEnvelope.Interpolate(0,1,.25f),.25f);
            float previous=.2f;
            for(int i=0;i<=100;i++)
            {
                float next=RibbonEnvelope.Interpolate(.2f,.8f,i/100f);
                Assert.That(next,Is.InRange(.2f,.8f));Assert.GreaterOrEqual(next,previous);previous=next;
            }
            float epsilon=.0001f;
            Assert.Less(RibbonEnvelope.Interpolate(0,1,epsilon)/epsilon,.001f);
            Assert.Less((1-RibbonEnvelope.Interpolate(0,1,1-epsilon))/epsilon,.001f);
            var mesh=ribbon.WaveformMesh;int[] indices=mesh.triangles;
            // Neighbouring spans actually share vertex indices, rather than coincident independent columns.
            for(int i=0;i<ribbon.CurveSegmentCount-1;i++)Assert.AreEqual(indices[i*6+1],indices[(i+1)*6]);
            Assert.AreEqual((512*4+1)*2,mesh.vertexCount);
        }
        [TestCase(4)] [TestCase(8)] [TestCase(16)] [TestCase(32)]
        public void ZoomGainAndSongLoopsKeepCorrectTimestamps(float seconds)
        {
            manager.zoom.Request(seconds);manager.zoom.Tick(10,false,.16f);ribbon.UpdateWaveform();
            float quiet=CoreAt(1.5f<4-seconds*.5f?4:1.5f).y;
            float loud=CoreAt(2.5f).y;
            manager.automation.Upsert(new AutomationEdit{songId="hologram",stem=StemId.Drums,parameter=ParameterType.Gain,start=2.25f,end=2.75f,value=2});
            ribbon.UpdateWaveform();Assert.Greater(CoreAt(2.5f).y,loud);
            Assert.That(CoreAt(1.5f<4-seconds*.5f?4:1.5f).y,Is.EqualTo(quiet).Within(.000001));
            manager.clock.elapsed+=8;ribbon.UpdateWaveform();
            Assert.That(CoreAt(2.5f).y,Is.GreaterThan(loud));
            Assert.That(ribbon.Timestamp((2.5f-4)/seconds*design.width),Is.EqualTo(2.5f).Within(.00001));
            foreach(var p in ribbon.WaveformMesh.vertices)
            {
                Assert.That(new Vector2(p.x,p.z+design.Radius).magnitude,Is.EqualTo(.8).Within(.00001));
                Assert.Less(Mathf.Abs(p.y),design.laneHeight*.5f);
            }
        }
        [Test] public void MotionCannotMoveSilenceOrAStoppedTimelineAndBuffersAreReused()
        {
            design.waveformMotion=1;ribbon.UpdateWaveform();var before=ribbon.WaveformMesh.vertices;
            ribbon.UpdateWaveform();CollectionAssert.AreEqual(before,ribbon.WaveformMesh.vertices);
            Assert.That(CoreAt(.5f).y,Is.EqualTo(design.waveformBaseline).Within(.00001));
            var mesh=ribbon.WaveformMesh;ribbon.UpdateWaveform();long allocated=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20;i++)ribbon.UpdateWaveform();
            Assert.AreEqual(0,GC.GetAllocatedBytesForCurrentThread()-allocated);Assert.AreSame(mesh,ribbon.WaveformMesh);
        }
        [Test] public void MotionFollowsRealAsymmetryWithoutAClockOscillator()
        {
            // A constant positive-heavy envelope can displace the center, but cannot start oscillating.
            short min=-6553,max=26213;ushort unsignedMin=unchecked((ushort)min);
            var packed=new byte[]{(byte)unsignedMin,(byte)(unsignedMin>>8),(byte)max,(byte)(max>>8),0,64};
            cache.Initialize(51200,409600,409600,1f/32767,packed,new[]{0},new[]{1});
            design.waveformMotion=0;ribbon.UpdateWaveform();var neutral=CoreAt(2.5f);
            design.waveformMotion=1;ribbon.UpdateWaveform();var displaced=CoreAt(2.5f);
            Assert.Greater(displaced.x,neutral.x);Assert.Greater(displaced.y,neutral.y);
            Assert.Less(displaced.y-neutral.y,.001f);
            manager.clock.elapsed=104;ribbon.UpdateWaveform();
            Assert.That(CoreAt(2.5f).x,Is.EqualTo(displaced.x).Within(.000001));
            Assert.That(CoreAt(2.5f).y,Is.EqualTo(displaced.y).Within(.000001));
        }
    }
}
