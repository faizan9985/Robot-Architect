using System;
using System.IO;
using NUnit.Framework;
namespace RemixTable.Tests
{
    // NUnit fixture setup runs inside Play Mode, after Unity's domain reload. Scene tests
    // never read or write user remixes, and each test starts with its own save folder.
    public abstract class RemixSavedSceneTest
    {
        Func<RemixSaveStore> previous;
        Func<DailyDropStore> previousDaily;
        bool previousMenu;
        string song,stamp; float time; bool hadSong,hadStamp,hadTime;
        void SavePreferences()
        {
            hadSong=UnityEngine.PlayerPrefs.HasKey(RemixMainMenu.ContinueSongKey);
            hadStamp=UnityEngine.PlayerPrefs.HasKey(RemixMainMenu.ContinueStampKey);
            hadTime=UnityEngine.PlayerPrefs.HasKey(RemixMainMenu.ContinueTimeKey);
            song=UnityEngine.PlayerPrefs.GetString(RemixMainMenu.ContinueSongKey);
            stamp=UnityEngine.PlayerPrefs.GetString(RemixMainMenu.ContinueStampKey);
            time=UnityEngine.PlayerPrefs.GetFloat(RemixMainMenu.ContinueTimeKey);
        }
        [SetUp] public void IsolateRemixSaves()
        {
            previous=RemixSaveStore.DefaultFactory;
            previousDaily=DailyDropStore.DefaultFactory; previousMenu=RemixMainMenu.OpenOnStart;
            RemixMainMenu.OpenOnStart=false; SavePreferences();
            string folder=Path.Combine(Path.GetTempPath(),"remix-play-tests",Guid.NewGuid().ToString("N"));
            RemixSaveStore.DefaultFactory=()=>new RemixSaveStore(folder);
            DailyDropStore.DefaultFactory=()=>new DailyDropStore(Path.Combine(folder,"daily-drop.json"));
        }
        [TearDown] public void RestoreRemixSaveFactory()
        {
            RemixSaveStore.DefaultFactory=previous;DailyDropStore.DefaultFactory=previousDaily;RemixMainMenu.OpenOnStart=previousMenu;
            if(hadSong)UnityEngine.PlayerPrefs.SetString(RemixMainMenu.ContinueSongKey,song);else UnityEngine.PlayerPrefs.DeleteKey(RemixMainMenu.ContinueSongKey);
            if(hadStamp)UnityEngine.PlayerPrefs.SetString(RemixMainMenu.ContinueStampKey,stamp);else UnityEngine.PlayerPrefs.DeleteKey(RemixMainMenu.ContinueStampKey);
            if(hadTime)UnityEngine.PlayerPrefs.SetFloat(RemixMainMenu.ContinueTimeKey,time);else UnityEngine.PlayerPrefs.DeleteKey(RemixMainMenu.ContinueTimeKey);
        }
    }
}
