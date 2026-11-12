using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace RemixTable.Tests
{
    public class SeatedWorkspaceTests
    {
        [UnityTest] public IEnumerator SavedWorkspaceTransportAndPalmPreserveAudioAndEditing()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var transport=Object.FindFirstObjectByType<RemixTransport>();
            Assert.IsTrue(manager.anchored);Assert.IsTrue(manager.audioManager.Ready);
            Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<XRUIInputModule>(FindObjectsSortMode.None).Length);
            var position=manager.workspace.position;Camera.main.transform.position+=new Vector3(.1f,0,0);
            yield return null;Assert.AreEqual(position,manager.workspace.position);
            CapturePreview(manager.workspace);
            transport.playPause.onClick.Invoke();Assert.IsTrue(manager.audioManager.IsManuallyPaused);double paused=manager.clock.elapsed;
            yield return new WaitForSeconds(.1f);Assert.AreEqual(paused,manager.clock.elapsed);
            var ribbon=manager.ribbons[0];ribbon.SetSelected(true);
            transport.playPause.onClick.Invoke();Assert.IsFalse(manager.audioManager.IsManuallyPaused);Assert.IsFalse(manager.clock.playing);
            ribbon.SetSelected(false);Assert.IsTrue(manager.clock.playing);
            yield return new WaitForSeconds(.4f);Assert.Greater(manager.clock.elapsed,paused);
            manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=StemId.Bass,parameter=ParameterType.Gain,wholeStem=true,value=.4f});
            transport.nextSong.onClick.Invoke();yield return null;
            Assert.AreEqual(1,manager.library.selectedIndex);Assert.IsFalse(manager.audioManager.Ready);Assert.IsFalse(transport.playPause.interactable);
            Assert.AreEqual(manager.library.Current.title,transport.songTitle.text);
            transport.previousSong.onClick.Invoke();yield return new WaitForSeconds(.5f);
            Assert.AreEqual(0,manager.library.selectedIndex);Assert.IsTrue(manager.audioManager.Ready);
            Assert.That(manager.audioManager.sources[(int)StemId.Bass].volume,Is.EqualTo(manager.audioManager.headroom*.4f).Within(.001));
            transport.playPause.onClick.Invoke();transport.nextSong.onClick.Invoke();transport.previousSong.onClick.Invoke();
            Assert.IsTrue(manager.audioManager.IsManuallyPaused);Assert.IsFalse(manager.clock.playing);
            Assert.AreEqual(1,manager.automation.edits.Count);Assert.IsFalse(transport.palmInstructions.gameObject.activeInHierarchy);
            Assert.IsFalse(transport.palmInstructions.transform.IsChildOf(manager.workspace));
            // Include extreme historic deformations in the geometry-bounds check.
            foreach(var r in manager.ribbons){manager.automation.Upsert(new AutomationEdit{songId=manager.library.Current.id,stem=r.stem,parameter=ParameterType.Pitch,wholeStem=true,value=12});r.wholeStem=true;r.SetSelected(true);}
            yield return null;
            foreach(var r in manager.ribbons)
            {
                foreach(var vertex in r.GetComponent<MeshFilter>().sharedMesh.vertices)Assert.LessOrEqual(Mathf.Abs(vertex.y),manager.design.laneHeight*.5f+.0001f);
                r.SetSelected(false);
            }
            Assert.IsFalse(manager.clock.playing); // Manual pause survives releasing all editing owners.
        }
        static void CapturePreview(Transform workspace)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var go=new GameObject("Seated preview camera");var camera=go.AddComponent<Camera>();camera.stereoTargetEye=StereoTargetEyeMask.None;
            var target=workspace.position+workspace.up*-.058f;go.transform.SetPositionAndRotation(target-workspace.forward*.9f,workspace.rotation);
            camera.orthographic=true;camera.orthographicSize=.31f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.11f,.14f);
            var texture=new RenderTexture(1200,900,24);camera.targetTexture=texture;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=texture;var image=new Texture2D(1200,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,900),0,0);image.Apply();
            Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/RemixSeatedPreview.png",image.EncodeToPNG());RenderTexture.active=previous;
            camera.targetTexture=null;texture.Release();Object.Destroy(texture);Object.Destroy(image);Object.Destroy(go);
        }
    }
}
