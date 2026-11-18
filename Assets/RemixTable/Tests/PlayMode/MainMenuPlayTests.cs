using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace RemixTable.Tests
{
    public class MainMenuPlayTests
    {
        #if UNITY_EDITOR
        [UnityTest] public IEnumerator SavedSceneOpensMenuHidesTableAndPlayReturnsToIt()
        {
            RemixMainMenu.OpenOnStart = true;
            RemixMainMenu.ForgetSession(); // First launch: nothing to continue.
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/RemixTable/Scenes/RemixTable.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                yield return new WaitForSeconds(2);
                var manager = Object.FindFirstObjectByType<RemixTableManager>();
                var menu = Object.FindFirstObjectByType<RemixMainMenu>();
                Assert.NotNull(menu);
                Assert.IsTrue(menu.IsOpen, "menu opens on start");
                Assert.IsTrue(menu.welcomePage.gameObject.activeSelf);
                Assert.IsTrue(manager.audioManager.MenuHold);
                Assert.IsFalse(manager.clock.playing, "song is held while the menu is open");
                Assert.IsFalse(manager.interactions.enabled, "ribbon pinches are off behind the menu");
                foreach (var ribbon in manager.ribbons) Assert.IsFalse(ribbon.GetComponent<MeshRenderer>().enabled, ribbon.name + " hidden");
                Assert.IsTrue(menu.GetComponent<Canvas>().enabled);
                Assert.Greater(menu.group.alpha, 0, "menu faded in after the table was placed");
                Assert.IsTrue(menu.background.isActiveAndEnabled, "living ribbons drift behind the menu");
                Assert.IsFalse(menu.continueButton.gameObject.activeSelf, "no CONTINUE on first launch");
                Assert.AreEqual("PLAY", menu.playButton.GetComponentInChildren<TMPro.TMP_Text>().text);

                // SELECT SONG: one row per library song; silent placeholders cannot be picked.
                menu.ShowSongs();
                Assert.IsTrue(menu.songsPage.gameObject.activeSelf);
                var rows = menu.songList.GetComponentsInChildren<UnityEngine.UI.Button>(false);
                Assert.AreEqual(manager.library.songs.Length, rows.Length);
                for (int i = 0; i < rows.Length; i++) Assert.AreEqual(SongLibraryManager.HasAudio(manager.library.songs[i]), rows[i].interactable);
                menu.SelectSong(manager.library.selectedIndex);
                Assert.IsTrue(menu.welcomePage.gameObject.activeSelf, "picking a song returns to the welcome page");

                // HOW TO USE pages step and clamp.
                menu.ShowHowTo();
                Assert.AreEqual(menu.howToPages[0].title, menu.howToTitle.text);
                Assert.IsFalse(menu.howToPrevButton.interactable);
                for (int i = 0; i < menu.howToPages.Length + 2; i++) menu.NextHowTo();
                Assert.AreEqual(menu.howToPages[menu.howToPages.Length - 1].title, menu.howToTitle.text);
                Assert.IsFalse(menu.howToNextButton.interactable);
                menu.ShowWelcome();

                // PLAY shows the table and starts the song.
                menu.playButton.onClick.Invoke();
                yield return new WaitForSeconds(.6f);
                Assert.IsFalse(menu.IsOpen);
                Assert.IsFalse(menu.GetComponent<Canvas>().enabled);
                Assert.IsFalse(manager.audioManager.MenuHold);
                Assert.IsTrue(manager.clock.playing, "song plays after PLAY");
                Assert.IsTrue(manager.interactions.enabled);
                foreach (var ribbon in manager.ribbons) Assert.IsTrue(ribbon.GetComponent<MeshRenderer>().enabled, ribbon.name + " visible");
                Assert.IsTrue(menu.menuButton.gameObject.activeInHierarchy && menu.menuButton.GetComponentInParent<Canvas>().enabled, "MENU button visible on the table");

                Assert.IsFalse(menu.background.enabled, "ribbon background idles while the table is in use");

                // MENU brings it back and remembers the spot: CONTINUE becomes the main button.
                yield return new WaitForSeconds(1);
                float left = manager.clock.Time;
                menu.menuButton.onClick.Invoke();
                Assert.IsTrue(menu.IsOpen);
                Assert.IsTrue(menu.HasContinue(out int index, out float time));
                Assert.AreEqual(manager.library.selectedIndex, index);
                Assert.AreEqual(left, time, .1f);
                Assert.IsTrue(menu.continueButton.gameObject.activeSelf);
                StringAssert.Contains(manager.library.Current.title, menu.continueDetail.text);
                Assert.AreEqual("PLAY FROM START", menu.playButton.GetComponentInChildren<TMPro.TMP_Text>().text);

                // CONTINUE resumes at the remembered spot.
                menu.continueButton.onClick.Invoke();
                yield return new WaitForSeconds(.5f);
                Assert.IsFalse(menu.IsOpen);
                Assert.IsTrue(manager.clock.playing);
                Assert.AreEqual(left + .5f, manager.clock.Time, .35f, "picked up where the listener left");

                // PLAY FROM START goes back to the top.
                menu.Open();
                menu.playButton.onClick.Invoke();
                yield return new WaitForSeconds(.3f);
                Assert.Less(manager.clock.Time, .6f, "PLAY FROM START restarts the song");

                // A manual pause chosen before opening the menu survives the round trip.
                manager.audioManager.Pause();
                menu.Open();
                menu.Play();
                yield return null;
                Assert.IsTrue(manager.audioManager.IsManuallyPaused, "the listener's PAUSE is kept");
                manager.audioManager.Resume();
            }
            finally { RemixMainMenu.OpenOnStart = true; RemixMainMenu.ForgetSession(); }
        }
        #endif
    }
}
