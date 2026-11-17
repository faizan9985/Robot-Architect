using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace RemixTable.Tests
{
    public class VocalsAutomationRenderTests
    {
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
            surface.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>(VocalsHologram.BodyPath);
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
            mesh.SetUVs(2,new[]{state,state,state,state});
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            try {pixels.ReadPixels(new Rect(0,0,128,256),0,0);pixels.Apply();return pixels.GetPixels();}
            finally {RenderTexture.active=previous;}
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
            var neutral=Render(Vector4.zero);
            var low=Render(VocalsHologram.EncodeAutomation(0,-12));var high=Render(VocalsHologram.EncodeAutomation(0,12));
            int lowCount=Bands(low,neutral),highCount=Bands(high,neutral);
            Assert.GreaterOrEqual(lowCount,3);Assert.Greater(highCount,lowCount*2);
            Assert.Greater(Bands(Render(VocalsHologram.EncodeAutomation(0,1)),neutral),0,"one-semitone changes are visible");
            CollectionAssert.AreEqual(neutral,Render(Vector4.zero),"reset returns pixel-identical neutral appearance");
            Debug.Log($"VOCALS_HARMONIC_RENDER_PASS lowBands={lowCount} highBands={highCount}");
        }
    }
}
