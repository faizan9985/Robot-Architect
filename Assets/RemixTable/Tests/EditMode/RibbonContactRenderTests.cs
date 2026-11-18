using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    [TestFixture(StemId.Drums)] [TestFixture(StemId.Bass)] [TestFixture(StemId.Melody)] [TestFixture(StemId.Vocals)]
    public class RibbonContactRenderTests
    {
        readonly StemId stem;public RibbonContactRenderTests(StemId id){stem=id;}
        GameObject surface,eye;Mesh mesh;Camera camera;Renderer renderer;RenderTexture target;Texture2D pixels;MaterialPropertyBlock block;
        [SetUp] public void Setup()
        {
            surface=new GameObject("contact render fixture",typeof(MeshFilter),typeof(MeshRenderer));surface.layer=31;surface.transform.position=new Vector3(10,10,10);
            const int n=192;var points=new Vector3[(n+1)*2];var uv=new Vector2[points.Length];var edge=new Vector4[points.Length];var colors=new Color[points.Length];var triangles=new int[n*6];
            for(int i=0;i<=n;i++)
            {
                int v=i*2;float x=(i/(float)n-.5f)*.6f;
                points[v]=new Vector3(x,-.008f,0);points[v+1]=new Vector3(x,.008f,0);uv[v]=new Vector2(i/(float)n,0);uv[v+1]=new Vector2(i/(float)n,1);
                edge[v]=edge[v+1]=new Vector4(0,1,0,.016f);colors[v]=colors[v+1]=Color.white;
                if(i==n)continue;int k=i*6;triangles[k]=v;triangles[k+1]=v+1;triangles[k+2]=v+2;triangles[k+3]=v+2;triangles[k+4]=v+1;triangles[k+5]=v+3;
            }
            mesh=new Mesh();mesh.vertices=points;mesh.triangles=triangles;mesh.uv=uv;mesh.colors=colors;mesh.SetUVs(1,edge);surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            renderer=surface.GetComponent<Renderer>();renderer.sharedMaterial=Resources.Load<Material>(VocalsHologram.BodyFor(stem));
            block=new MaterialPropertyBlock();if(stem!=StemId.Vocals)StemHologramPalette.For(stem).Apply(block);
            eye=new GameObject("contact render camera");camera=eye.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(10,10,9);
            camera.orthographic=true;camera.orthographicSize=.05f;camera.aspect=6;camera.nearClipPlane=.01f;camera.farClipPlane=2;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            target=new RenderTexture(768,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();pixels=new Texture2D(768,128,TextureFormat.RGBA32,false);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(surface);Object.DestroyImmediate(eye);Object.DestroyImmediate(mesh);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        Color[] Render(Vector4 first,Vector4 second=default)
        {
            block.SetVector("_Contact0",first);block.SetVector("_Contact1",second);renderer.SetPropertyBlock(block);
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            try{pixels.ReadPixels(new Rect(0,0,768,128),0,0);pixels.Apply();return pixels.GetPixels();}finally{RenderTexture.active=previous;}
        }
        static float Light(Color c)=>c.r*.2126f+c.g*.7152f+c.b*.0722f;
        static float Difference(Color[] a,Color[] b,int center,int y=64)
        {
            // Sample the full interior, including bright and dark harmonics; y=0 alone is always a pitch line.
            float sum=0;int count=0;int low=y==64?56:y,high=y==64?72:y+1;
            for(int row=low;row<high;row++)for(int x=center-5;x<center+5;x++){sum+=Light(a[row*768+x])-Light(b[row*768+x]);count++;}
            return sum/count;
        }
        [TestCase(0,0)] [TestCase(-1,0)] [TestCase(1,0)] [TestCase(0,-12)] [TestCase(0,12)]
        public void LocalHoverContactAndRestoredIdlePreserveAllAutomationStates(float filter,float pitch)
        {
            var states=new Vector4[mesh.vertexCount];for(int i=0;i<states.Length;i++)states[i]=VocalsHologram.EncodeAutomation(filter,pitch);mesh.SetUVs(2,states);
            var idle=Render(Vector4.zero);var hover=Render(new Vector4(0,.09f,1,0));var pinch=Render(new Vector4(0,.09f,0,1));
            Assert.Greater(Difference(hover,idle,384),.025f,"hover must be noticeable on a seated-scale narrow filled waveform");
            Assert.Greater(Difference(pinch,hover,384),.025f,"pinch must be clearly stronger than hover");
            Assert.Greater(Difference(pinch,idle,384,72),0,"contour light must increase");
            for(int y=55;y<74;y++)for(int x=30;x<738;x++)if(x<260 || x>508)Assert.AreEqual(idle[y*768+x],pinch[y*768+x],"outside the 9cm contact radius stays unchanged");
            var both=Render(new Vector4(-.16f,.06f,0,1),new Vector4(.16f,.06f,0,1));
            Assert.Greater(Difference(both,idle,179),.02f);Assert.Greater(Difference(both,idle,589),.02f);
            Assert.That(Difference(both,idle,384),Is.EqualTo(0).Within(.0001));
            CollectionAssert.AreEqual(idle,Render(Vector4.zero));
            Render(new Vector4(0,.09f,0,1));System.IO.File.WriteAllBytes($"Logs/Contact_{stem}_{filter}_{pitch}.png",pixels.EncodeToPNG());
            Debug.Log($"CONTACT_GPU_PASS stem={stem} filter={filter} pitch={pitch} hoverDelta={Difference(hover,idle,384):F3} pinchDelta={Difference(pinch,idle,384):F3}");
        }
        [Test] public void ExistingHaloGetsLocalizedOpacityWithoutExtraGeometry()
        {
            renderer.sharedMaterial=Resources.Load<Material>(VocalsHologram.HaloFor(stem));
            var idle=Render(Vector4.zero);var hover=Render(new Vector4(0,.09f,1,0));var pinch=Render(new Vector4(0,.09f,0,1));
            Assert.Greater(Difference(hover,idle,384,60),.01f);Assert.Greater(Difference(pinch,hover,384,60),.01f);
            Assert.AreEqual(idle[60*768+100],pinch[60*768+100]);CollectionAssert.AreEqual(idle,Render(Vector4.zero));
        }
    }
}
