using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace RemixTable
{
    // What is saved for one song: its automation edits and its mixer state (mute / solo / spatial placement).
    [Serializable] public class RemixSaveData
    {
        public int version = 1;
        public string songId;
        public List<AutomationEdit> edits = new List<AutomationEdit>();
        public StemMix mix = new StemMix();
    }

    // Saves one JSON file per song under Application.persistentDataPath/Remixes. Writes go to a temporary file
    // first and are then moved into place, so a crash mid-write never corrupts an existing save.
    public sealed class RemixSaveStore
    {
        public string Folder { get; }
        public RemixSaveStore(string folder) { Folder = folder; }
        public static RemixSaveStore Default() => new RemixSaveStore(Path.Combine(Application.persistentDataPath, "Remixes"));
        public string PathFor(string songId)
        {
            var safe = string.IsNullOrEmpty(songId) ? "untitled" : songId;
            foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return Path.Combine(Folder, safe + ".json");
        }
        public static RemixSaveData Capture(string songId, AutomationStore store, StemMix mix)
        {
            var data = new RemixSaveData { songId = songId };
            foreach (var e in store.edits) if (e.songId == songId) data.edits.Add(e);
            data.mix.CopyFrom(mix);
            return data;
        }
        public void Save(RemixSaveData data)
        {
            Directory.CreateDirectory(Folder);
            string path = PathFor(data.songId), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        // Returns null when there is no save or it cannot be read; a broken file is never fatal.
        public RemixSaveData Load(string songId)
        {
            string path = PathFor(songId);
            if (!File.Exists(path)) return null;
            try
            {
                var data = JsonUtility.FromJson<RemixSaveData>(File.ReadAllText(path));
                if (data == null || data.songId != songId) return null;
                data.edits ??= new List<AutomationEdit>();
                data.edits.RemoveAll(e => e == null || e.songId != songId || float.IsNaN(e.start) || float.IsNaN(e.end) || float.IsNaN(e.value));
                foreach (var e in data.edits) if (e.curve != null && e.curve.length == 0) e.curve = null;
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Remix save for '{songId}' could not be read and was ignored: {exception.Message}");
                return null;
            }
        }
        public void Delete(string songId) { var path = PathFor(songId); if (File.Exists(path)) File.Delete(path); }
        // Replaces this song's edits in the store with the saved ones and applies the saved mix.
        public static void Apply(RemixSaveData data, string songId, AutomationStore store, StemMix mix)
        {
            store.ResetSong(songId);
            if (data == null) { mix.CopyFrom(null); return; }
            foreach (var e in data.edits) store.Upsert(e);
            mix.CopyFrom(data.mix);
        }
    }
}
