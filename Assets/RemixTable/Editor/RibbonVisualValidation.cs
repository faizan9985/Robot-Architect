using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RemixTable.Editor
{
    public static class RibbonVisualValidation
    {
        [MenuItem("Tools/Remix Table/Validate Holographic Ribbons")]
        public static void Validate()
        {
            WaveformBaker.Validate();
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var d=manager.design;
            RemixSceneBuilder.Require(d.curvedTimeline && d.Radius==.8f && d.arcAngle==180 && d.spacing==.09f,"preserved curved seated geometry");
            RemixSceneBuilder.Require(d.waveformColumns<=512 && d.waveformBaseline>0 && d.waveformPeakPreservation>=.8f,"bounded ribbon detail and preserved peaks");
            RemixSceneBuilder.Require(d.ribbonMaterial.shader.name=="RemixTable/RibbonEnergy" && !ShaderUtil.ShaderHasError(d.ribbonMaterial.shader),"holographic URP shader");
            File.WriteAllText("Logs/RemixHologramSceneValidation.txt","COMPILATION_AND_SCENE_VALIDATION_PASS");
            Debug.Log("REMIX_HOLOGRAM_VALIDATION_PASS");
        }
        // Non-persistent desktop visual inspection. Does not move or save any authored scene nodes.
        public static void Preview()
        {
            Validate();var manager=Object.FindFirstObjectByType<RemixTableManager>();manager.clock.elapsed=65;
            foreach(var ribbon in manager.ribbons)
            {
                if(ribbon.WaveformMesh==null)typeof(RibbonView).GetMethod("Awake",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ribbon,null);
                ribbon.UpdateWaveform();
            }
            var go=new GameObject("temporary ribbon preview camera");var camera=go.AddComponent<Camera>();
            camera.transform.position=manager.workspace.position+new Vector3(0,.08f,-1.7f);
            camera.transform.LookAt(manager.workspace.position);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.018f,.035f);
            camera.fieldOfView=65;camera.nearClipPlane=.01f;camera.aspect=1600f/900;
            // An sRGB target keeps the exported PNG representative of the Game view.
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();
            var request=new RenderPipeline.StandardRequest{destination=target};
            RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;RenderTexture.active=target;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
            File.WriteAllBytes("Logs/RemixHologramPreview.png",pixels.EncodeToPNG());
            RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);Object.DestroyImmediate(go);
            Debug.Log("REMIX_HOLOGRAM_PREVIEW_PASS");
        }
    }
}
