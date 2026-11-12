using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class PlaybackTests
    {
        #if UNITY_EDITOR
        [UnityTest] public IEnumerator SavedSceneStartsWithInheritedXrAndFourAnimatedRibbons()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return new WaitForSeconds(2);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();
            Assert.NotNull(manager);Assert.AreEqual(4,manager.ribbons.Length);Assert.Greater(manager.clock.elapsed,1);
            foreach(var ribbon in manager.ribbons)
            {
                Assert.Greater(ribbon.visualTime,0);
                var mesh=ribbon.GetComponent<MeshFilter>().sharedMesh;
                Assert.NotNull(mesh);Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);
            }
            var all=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            int origins=0,interactions=0,menus=0,events=0;
            foreach(var component in all)
            {
                switch(component.GetType().Name)
                {
                    case "XROrigin":origins++;break;
                    case "XRInteractionManager":interactions++;break;
                    case "HandMenuController":menus++;break;
                    case "EventSystem":events++;break;
                }
            }
            Assert.AreEqual(1,origins);Assert.AreEqual(1,interactions);Assert.AreEqual(1,menus);Assert.AreEqual(1,events);
            manager.library.Select(1);Assert.Less(manager.clock.Time,.1f);
            foreach(var ribbon in manager.ribbons) Assert.IsFalse(ribbon.selected);
        }
        #endif
        [Test] public void SongSwitchRestoresSongSpecificAutomation()
        {
            var root=new GameObject("library");var library=root.AddComponent<SongLibraryManager>();
            library.songs=new[]{new SongMetadata{id="a"},new SongMetadata{id="b"}};
            var store=new AutomationStore();store.Upsert(new AutomationEdit{songId="a",stem=StemId.Drums,wholeStem=true,parameter=ParameterType.Gain,value=2});
            int changes=0;library.SongChanged+=_=>changes++;
            library.Select(1);Assert.AreEqual(1,store.Evaluate(library.Current.id,StemId.Drums,ParameterType.Gain,10));
            library.Select(0);Assert.AreEqual(2,store.Evaluate(library.Current.id,StemId.Drums,ParameterType.Gain,10));Assert.AreEqual(2,changes);
            Object.DestroyImmediate(root);
        }
    }
}

