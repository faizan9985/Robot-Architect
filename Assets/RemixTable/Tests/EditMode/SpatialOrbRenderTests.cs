using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    [TestFixture(StemId.Drums)] [TestFixture(StemId.Bass)] [TestFixture(StemId.Melody)] [TestFixture(StemId.Vocals)]
    public class SpatialOrbRenderTests
    {
        readonly StemId stem;public SpatialOrbRenderTests(StemId id){stem=id;}
        GameObject orb,eye;SpatialOrbHologram visual;Camera camera;RenderTexture target;Texture2D texture;
        [SetUp] public void Setup()
        {
            orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.layer=31;orb.transform.position=new Vector3(10,10,10);orb.transform.localScale=Vector3.one*.09f;
            visual=orb.AddComponent<SpatialOrbHologram>();visual.Initialize(stem,orb.GetComponent<Renderer>());
            eye=new GameObject("spatial material render test camera");camera=eye.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(10,10,8.9f);camera.fieldOfView=16;camera.nearClipPlane=.01f;camera.farClipPlane=3;camera.aspect=1;
            camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();texture=new Texture2D(256,256,TextureFormat.RGBA32,false);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(orb);Object.DestroyImmediate(eye);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);}
        Color[] Render(string name)
        {
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;
            try {texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();System.IO.File.WriteAllBytes("Logs/Spatial_"+stem+"_"+name+".png",texture.EncodeToPNG());return texture.GetPixels();}
            finally {RenderTexture.active=old;}
        }
        static Color Core(Color[] frame)
        {Color mean=Color.black;int count=0;for(int y=108;y<148;y++)for(int x=108;x<148;x++){mean+=frame[y*256+x];count++;}return mean/count;}
        static float Light(Color color)=>color.r*.2126f+color.g*.7152f+color.b*.0722f;
        static int HaloPixels(Color[] frame)
        {int count=0;for(int y=70;y<186;y++)for(int x=70;x<186;x++){int dx=x-128,dy=y-128;if(dx*dx+dy*dy>=40*40 && Light(frame[y*256+x])>.015f)count++;}return count;}
        [Test] public void ActualSphereRenderHasColoredIdleHaloAndRecognizablyBrighterGrab()
        {
            var idle=Render("Idle");var color=Core(idle);Assert.Greater(Light(color),.1f);Assert.Greater(HaloPixels(idle),20,"standalone halo must render without bloom");
            if(stem==StemId.Drums){Assert.Greater(color.r,color.g);Assert.Greater(color.g,color.b);}
            else if(stem==StemId.Bass){Assert.Greater(color.g,color.b);Assert.Greater(color.b,color.r);}
            else if(stem==StemId.Melody){Assert.Greater(color.b,color.g);Assert.Greater(color.g,color.r);}
            else {Assert.Greater(color.b,color.r);Assert.Greater(color.r,color.g);}
            visual.Advance(.1f,true,false,true,false);var hover=Render("Hover");Assert.Greater(Light(Core(hover)),Light(color));
            visual.Advance(.1f,true,true,true,false);var grab=Render("Grab");
            Assert.Greater(Light(Core(grab)),Light(color)+.015f);Assert.Greater(HaloPixels(grab),HaloPixels(idle));
            visual.Advance(.25f,false,false,true,false);CollectionAssert.AreEqual(idle,Render("Released"));
            visual.Advance(.1f,true,true,false,false);var muted=Render("MutedGrab");Assert.Less(Light(Core(muted)),Light(Core(grab)),"mute attenuates the equivalent approved grab state");
            Debug.Log($"SPATIAL_GPU_PASS stem={stem} idleHalo={HaloPixels(idle)} grabHalo={HaloPixels(grab)} idleCore={Light(color):F3} grabCore={Light(Core(grab)):F3}");
        }
    }
}
