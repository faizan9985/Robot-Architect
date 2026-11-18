using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    [TestFixture(StemId.Drums)] [TestFixture(StemId.Bass)] [TestFixture(StemId.Melody)] [TestFixture(StemId.Vocals)]
    public class VocalsAutomationRenderTests
    {
        readonly StemId stem;
        public VocalsAutomationRenderTests(StemId id) { stem=id; }
        GameObject surface,eye;
        Mesh mesh;
        Camera camera;
        RenderTexture target;
        Texture2D pixels;
        [SetUp] public void Setup()
        {
            surface=new GameObject("automation shader fixture",typeof(MeshFilter),typeof(MeshRenderer));
            surface.layer=31;surface.transform.position=new Vector3(10,10,10);
            mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.01f,-.02f,0),new Vector3(-.01f,.02f,0),new Vector3(.01f,-.02f,0),new Vector3(.01f,.02f,0)};
            mesh.triangles=new[]{0,1,2,2,1,3};mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.right,Vector2.one};
            mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};
            var edge=new Vector4(0,1,0,.04f);mesh.SetUVs(1,new[]{edge,edge,edge,edge});
            surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            surface.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>(VocalsHologram.BodyFor(stem));
            if(stem!=StemId.Vocals)
            {var block=new MaterialPropertyBlock();StemHologramPalette.For(stem).Apply(block);surface.GetComponent<MeshRenderer>().SetPropertyBlock(block);}
            eye=new GameObject("automation test camera");camera=eye.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(10,10,9);camera.orthographic=true;camera.orthographicSize=.025f;
            camera.nearClipPlane=.01f;camera.farClipPlane=2;camera.aspect=.5f;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            target=new RenderTexture(128,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
            pixels=new Texture2D(128,256,TextureFormat.RGBA32,false);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(surface);Object.DestroyImmediate(eye);Object.DestroyImmediate(mesh);
            target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);
        }
        Color[] Render(Vector4 state)
        {
            var states=new Vector4[mesh.vertexCount];for(int i=0;i<states.Length;i++)states[i]=state;
            mesh.SetUVs(2,states);
            return ReadFrame();
        }
        Color[] ReadFrame()
        {
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            try {pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();return pixels.GetPixels();}
            finally {RenderTexture.active=previous;}
        }
        void SaveFrame(string name) => System.IO.File.WriteAllBytes("Logs/"+(stem==StemId.Vocals?"":stem+"_")+name+".png",pixels.EncodeToPNG());
        void Strip(float height,System.Func<float,Vector4> state)
        {
            // About 8-20 pixels tall: the seated-distance case missed by the original close-up tests.
            camera.orthographicSize=.05f;camera.aspect=4;
            target.Release();target.width=512;target.height=128;target.Create();
            Object.DestroyImmediate(pixels);pixels=new Texture2D(512,128,TextureFormat.RGBA32,false);
            const int columns=96;var points=new Vector3[(columns+1)*2];var uv=new Vector2[points.Length];
            var edges=new Vector4[points.Length];var states=new Vector4[points.Length];var colors=new Color[points.Length];var indices=new int[columns*6];
            for(int i=0;i<=columns;i++)
            {
                float x=(i/(float)columns-.5f)*.3f;int v=i*2;
                points[v]=new Vector3(x,-height*.5f,0);points[v+1]=new Vector3(x,height*.5f,0);
                uv[v]=new Vector2(i/(float)columns,0);uv[v+1]=new Vector2(i/(float)columns,1);
                edges[v]=edges[v+1]=new Vector4(0,1,0,height);states[v]=states[v+1]=state(x);colors[v]=colors[v+1]=Color.white;
                if(i==columns)continue;int k=i*6;indices[k]=v;indices[k+1]=v+1;indices[k+2]=v+2;indices[k+3]=v+2;indices[k+4]=v+1;indices[k+5]=v+3;
            }
            mesh.Clear();mesh.vertices=points;mesh.triangles=indices;mesh.uv=uv;mesh.colors=colors;mesh.SetUVs(1,edges);mesh.SetUVs(2,states);
        }
        static float HorizontalRange(Color[] frame,out int bands)
        {
            float low=1,high=0;for(int x=70;x<442;x++){float value=Light(frame[64*512+x]);low=Mathf.Min(low,value);high=Mathf.Max(high,value);}
            float threshold=(low+high)*.5f;bands=0;bool inside=false;
            for(int x=70;x<442;x++){bool bright=Light(frame[64*512+x])>threshold;if(bright && !inside)bands++;inside=bright;}
            return high-low;
        }
        [TestCase(.006f,1f)] [TestCase(.016f,1f)]
        [TestCase(.006f,1.8f)] [TestCase(.016f,1.8f)]
        public void FilterHasDistinctSpatialCuesAtSeatedDistance(float height,float intensity)
        {
            Strip(height,x=>Vector4.zero);
            var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=new Color(intensity,intensity,intensity,1);mesh.colors=colors;
            string suffix=Mathf.RoundToInt(height*1000)+"_"+Mathf.RoundToInt(intensity*10);
            var neutral=Render(Vector4.zero);var low=Render(VocalsHologram.EncodeAutomation(-1,0));SaveFrame("FilterLow_"+suffix);
            var high=Render(VocalsHologram.EncodeAutomation(1,0));SaveFrame("FilterHigh_"+suffix);
            float lowRange=HorizontalRange(low,out int lowBands),highRange=HorizontalRange(high,out int highBands);
            Debug.Log($"FILTER_READABILITY stem={stem} height={height} intensity={intensity} lowContrast={lowRange:F4} highContrast={highRange:F4} lowBands={lowBands} highBands={highBands}");
            Assert.Less(HorizontalRange(neutral,out _),.01f);
            Assert.Greater(lowRange,.12f,"low-pass needs broad spatial bands, not just a global brightness change");
            Assert.Greater(highRange,.08f,"high-pass needs internal frequency streaks visible on a narrow waveform");
            Assert.That(lowBands,Is.InRange(3,7));Assert.Greater(highBands,lowBands*3);
            var mildLow=Render(VocalsHologram.EncodeAutomation(-.25f,0));var mildHigh=Render(VocalsHologram.EncodeAutomation(.25f,0));
            Assert.Less(HorizontalRange(mildLow,out _),lowRange);Assert.Less(HorizontalRange(mildHigh,out _),highRange);
            CollectionAssert.AreEqual(neutral,Render(Vector4.zero));
        }
        [Test] public void RegionalFilterPixelsMatchUniformReferencesWithoutChangingNeutralRegion()
        {
            Strip(.016f,x=>Vector4.zero);
            var low=Render(VocalsHologram.EncodeAutomation(-1,0));var neutral=Render(Vector4.zero);var high=Render(VocalsHologram.EncodeAutomation(1,0));
            Strip(.016f,x=>VocalsHologram.EncodeAutomation(x<-.055f?-1:x>.055f?1:0,0));var regional=ReadFrame();SaveFrame("FilterRegions");
            for(int y=60;y<68;y++)for(int x=90;x<422;x++)
            {
                Color[] reference=x<174?low:x>=216 && x<296?neutral:x>=338?high:null;if(reference==null)continue;
                var actual=regional[y*512+x];var expected=reference[y*512+x];
                Assert.That(Mathf.Abs(actual.r-expected.r)+Mathf.Abs(actual.g-expected.g)+Mathf.Abs(actual.b-expected.b),Is.LessThan(.012f));
            }
        }
        static float Light(Color c) => c.r*.2126f+c.g*.7152f+c.b*.0722f;
        static float Body(Color[] image)
        {float sum=0;for(int y=40;y<216;y++)sum+=Light(image[y*128+64]);return sum/176;}
        static float Edges(Color[] image)
        {float sum=0;for(int y=25;y<30;y++)sum+=Light(image[y*128+64])+Light(image[(255-y)*128+64]);return sum/10;}
        static int Bands(Color[] image,Color[] neutral)
        {
            int count=0;bool inside=false;
            for(int y=40;y<216;y++)
            {
                bool bright=Light(image[y*128+64])-Light(neutral[y*128+64])>.035f;
                if(bright && !inside)count++;inside=bright;
            }
            return count;
        }
        [Test] public void FilterActuallyDarkensSoftensAndBrightensContoursOnGPU()
        {
            var neutral=Render(Vector4.zero);Assert.Greater(Body(neutral),.1f,"fixture must render visible pixels");
            CollectionAssert.AreEqual(neutral,Render(VocalsHologram.EncodeAutomation(0,0)));
            var mild=Render(VocalsHologram.EncodeAutomation(-.3f,0));var low=Render(VocalsHologram.EncodeAutomation(-1,0));
            Assert.Less(Body(mild),Body(neutral));Assert.Less(Body(low),Body(mild));
            Assert.Less(Edges(low)/Body(low),Edges(neutral)/Body(neutral),"low-pass reduces contour contrast");
            var high=Render(VocalsHologram.EncodeAutomation(1,0));Assert.Greater(Edges(high),Edges(neutral));
            System.IO.File.WriteAllBytes("Logs/VocalsFilterHighpass.png",pixels.EncodeToPNG());
        }
        [Test] public void GPURendersMoreTightlySpacedHarmonicsForHigherPitch()
        {
            var neutral=Render(Vector4.zero);SaveFrame("PitchNeutral");
            var low=Render(VocalsHologram.EncodeAutomation(0,-12));SaveFrame("PitchLow");
            var high=Render(VocalsHologram.EncodeAutomation(0,12));SaveFrame("PitchHigh");
            int lowCount=Bands(low,neutral),highCount=Bands(high,neutral);
            Assert.GreaterOrEqual(lowCount,3);Assert.Greater(highCount,lowCount*2);
            Assert.Greater(Bands(Render(VocalsHologram.EncodeAutomation(0,1)),neutral),0,"one-semitone changes are visible");
            CollectionAssert.AreEqual(neutral,Render(Vector4.zero),"reset returns pixel-identical neutral appearance");
            Debug.Log($"STEM_HARMONIC_RENDER_PASS stem={stem} lowBands={lowCount} highBands={highCount}");
        }
    }
}
