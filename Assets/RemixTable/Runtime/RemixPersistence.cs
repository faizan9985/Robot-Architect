using UnityEngine;
namespace RemixTable
{
    // Keeps each song's remix across sessions: loads the saved remix when a song is selected and saves shortly
    // after any change (end of a pinch hold, undo / redo, reset, mute / solo, moving a spatial stem), plus
    // whenever the app is paused or closed.
    public sealed class RemixPersistence : MonoBehaviour
    {
        public RemixTableManager manager;
        [Min(0)] public float saveDelaySeconds = 1;
        public RemixSaveStore Store { get; set; }
        string loadedSong;
        int seenHistory = -1, seenMix = -1;
        float dueTime = -1;
        public bool Dirty => dueTime >= 0;
        void OnEnable()
        {
            if (manager == null) return;
            Store ??= RemixSaveStore.Default();
            manager.SongReady += Load;
            if (manager.library != null && manager.library.songs != null && manager.library.songs.Length > 0) Load(manager.library.Current);
        }
        void OnDisable()
        {
            SaveNow();
            if (manager != null) manager.SongReady -= Load;
        }
        public void Load(SongMetadata song)
        {
            if (song == null) return;
            if (loadedSong != null && loadedSong != song.id) SaveNow(); // Leaving a song: flush its pending edits.
            var data = Store.Load(song.id);
            RemixSaveStore.Apply(data, song.id, manager.automation, manager.mix);
            loadedSong = song.id;
            seenHistory = manager.history.Version; seenMix = manager.mix.Version; dueTime = -1;
        }
        void Update()
        {
            if (loadedSong == null) return;
            // While a hold is open the edit is still changing; the save happens after release.
            if (manager.history.GestureOpen) return;
            if (manager.history.Version != seenHistory || manager.mix.Version != seenMix)
            {
                seenHistory = manager.history.Version; seenMix = manager.mix.Version;
                dueTime = Time.unscaledTime + saveDelaySeconds;
            }
            if (dueTime >= 0 && Time.unscaledTime >= dueTime) SaveNow();
        }
        public void SaveNow()
        {
            if (loadedSong == null || manager == null || Store == null) return;
            dueTime = -1;
            try { Store.Save(RemixSaveStore.Capture(loadedSong, manager.automation, manager.mix)); }
            catch (System.Exception exception) { Debug.LogWarning("Remix could not be saved: " + exception.Message); }
        }
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationQuit() => SaveNow();
    }
}
