using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR;
namespace RemixTable.Tests
{
    public class MergeIntegrationPlayTests : RemixSavedSceneTest
    {
        IEnumerator Load()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity",new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            #endif
            yield return new WaitForSeconds(2);
        }
        [UnityTest] public IEnumerator EverySongLoadsFourSynchronizedStemsAndKeepsItsOwnEdits()
        {
            yield return Load();
            var m=Object.FindFirstObjectByType<RemixTableManager>();
            var p=m.GetComponent<RemixPersistence>();
            Assert.AreEqual(4,m.library.PlayableCount);
            m.audioManager.SetPlaybackSpeed(1.25f);
            for(int i=0;i<m.library.songs.Length;i++)
            {
                m.library.Select(i);Assert.IsTrue(m.audioManager.Ready);
                var song=m.library.Current;
                m.automation.Upsert(new AutomationEdit{songId=song.id,stem=StemId.Drums,parameter=ParameterType.Gain,wholeStem=true,value=1.1f+i*.1f});
                m.mix.SetMuted(StemId.Vocals,i%2==1);p.SaveNow();
                yield return new WaitForSeconds(.8f);
                var first=m.audioManager.ActiveSource(StemId.Drums);
                for(int stem=0;stem<4;stem++)
                {
                    var source=m.audioManager.ActiveSource((StemId)stem);
                    Assert.AreEqual(first.clip.samples,source.clip.samples);Assert.AreEqual(first.clip.frequency,source.clip.frequency);
                    Assert.AreEqual(1.25f,source.pitch);
                    Assert.That(Mathf.Abs(source.time-first.time),Is.LessThan(.08f),song.id+" stem synchronization");
                }
            }
            for(int i=0;i<m.library.songs.Length;i++)
            {
                m.library.Select(i);
                Assert.AreEqual(1.1f+i*.1f,m.automation.EvaluateWholeGain(m.library.Current.id,StemId.Drums,0),.0001f);
                Assert.AreEqual(i%2==1,m.mix.muted[(int)StemId.Vocals]);
            }
        }
        [UnityTest] public IEnumerator RoomRescansDoNotUndoManualDragOrExplicitRecenter()
        {
            yield return Load();var m=Object.FindFirstObjectByType<RemixTableManager>();m.enabled=false;
            var room=Object.FindFirstObjectByType<RemixRoomPlacer>();room.moveSeconds=.05f;
            var eye=new Vector3(0,1.2f,0);var head=new Pose(eye,Quaternion.identity);
            m.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            m.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            var scan=RemixRoomPlacer.SampleRoom(eye,Vector3.forward);room.Place(scan,eye,Vector3.forward);
            m.workspaceInteraction.SetHeight(.5f);
            var moved=m.workspace.position+new Vector3(.2f,.1f,-.1f);m.MoveWorkspace(moved);
            room.Place(scan,eye,Vector3.forward);yield return null;
            Assert.That(Vector3.Distance(moved,m.workspace.position),Is.LessThan(.0001f));Assert.AreEqual(.5f,m.workspaceInteraction.Height,.0001f);
            m.RecenterWorkspace();
            m.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            m.ProcessWorkspacePose(head,true,Pose.identity,TrackingOriginModeFlags.Floor,0,.5f);
            var centered=m.workspace.position;room.Place(scan,eye,Vector3.forward);yield return null;
            Assert.That(Vector3.Distance(centered,m.workspace.position),Is.LessThan(.0001f));
            Assert.AreEqual(m.workspaceInteraction.DefaultHeight,m.workspaceInteraction.Height,.0001f);
            Assert.IsNotNull(m.room);room.Release(eye,Vector3.forward);Assert.IsNull(m.room);
        }
        [UnityTest] public IEnumerator MenuContinueRestoresAnotherSongsSavedEditsAndManualPause()
        {
            yield return Load();var m=Object.FindFirstObjectByType<RemixTableManager>();var menu=Object.FindFirstObjectByType<RemixMainMenu>();
            menu.Play();m.library.Select(1);
            string song=m.library.Current.id;
            m.automation.Upsert(new AutomationEdit{songId=song,stem=StemId.Bass,parameter=ParameterType.Pitch,wholeStem=true,value=3});
            m.GetComponent<RemixPersistence>().SaveNow();m.audioManager.Seek(8);menu.RememberSession();
            m.audioManager.Pause();menu.Open();menu.SelectSong(2);Assert.IsFalse(m.clock.playing);menu.Continue();
            Assert.AreEqual(song,m.library.Current.id);Assert.AreEqual(3,m.PitchAt(StemId.Bass,m.clock.Time),.0001f);
            Assert.AreEqual(8,m.clock.Time,.1f);Assert.IsTrue(m.audioManager.IsManuallyPaused);Assert.IsFalse(m.clock.playing);
            yield return null;
        }
        [UnityTest] public IEnumerator FloatingMenuKeepsReadableLabelsAndOriginalIconsSupportHighContrast()
        {
            yield return Load();var m=Object.FindFirstObjectByType<RemixTableManager>();var menu=Object.FindFirstObjectByType<RemixMainMenu>();
            var ui=m.GetComponent<RemixUiPresentation>();Assert.AreEqual(45,ui.controls.Length);
            foreach(var button in new[]{menu.playButton,menu.songsButton,menu.howToButton,menu.menuButton})
            {
                Assert.IsNull(button.GetComponent<RemixIconButton>(),"floating pills retain their authored labels");
                Assert.IsTrue(button.transform.Find("Label").GetComponent<TMPro.TMP_Text>().enabled);
            }
            m.accessibility.SetContrast(true);
            foreach(var c in ui.controls)if(c.role=="FilterMode")
            {
                c.Paint();Assert.AreEqual(Color.black,c.button.targetGraphic.color);Assert.AreEqual(Color.white,c.outline.color);
            }
            m.accessibility.SetContrast(false);yield return null;
        }
        [UnityTest] public IEnumerator KaraokeAndBreakdownMuteChangesUndoAlongWithAutomation()
        {
            yield return Load();var m=Object.FindFirstObjectByType<RemixTableManager>();var vibes=Object.FindFirstObjectByType<RemixVibes>();
            m.mix.SetSoloed(StemId.Bass,true);vibes.Karaoke();Assert.IsTrue(m.mix.muted[(int)StemId.Vocals]);
            Assert.IsTrue(m.history.Undo(m.automation));Assert.IsFalse(m.mix.muted[(int)StemId.Vocals]);Assert.IsTrue(m.mix.soloed[(int)StemId.Bass]);
            Assert.IsTrue(m.history.Redo(m.automation));Assert.IsTrue(m.mix.muted[(int)StemId.Vocals]);
        }
    }
}
