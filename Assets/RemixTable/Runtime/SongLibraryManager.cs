using System;
using UnityEngine;
namespace RemixTable
{
    public class SongLibraryManager : MonoBehaviour
    {
        public SongMetadata[] songs;
        public int selectedIndex;
        [Tooltip("Skip songs that have no audio stems assigned (placeholders) when moving to the next / previous song.")]
        public bool skipSilentSongs;
        public SongMetadata Current => songs[selectedIndex];
        public event Action<SongMetadata> SongChanged;
        public static bool HasAudio(SongMetadata song) => song != null && song.stems != null && Array.Exists(song.stems, s => s != null && s.clip != null);
        public int PlayableCount { get { int n = 0; if (songs != null) foreach (var s in songs) if (HasAudio(s)) n++; return n; } }
        public void Select(int index)
        {
            if (songs == null || songs.Length == 0) return;
            int n = songs.Length;
            int target = Wrap(index, n);
            if (skipSilentSongs && PlayableCount > 0)
            {
                // Keep stepping the same way until a song with audio comes up.
                int step = index < selectedIndex ? -1 : 1;
                for (int k = 0; k < n && !HasAudio(songs[target]); k++) target = Wrap(target + step, n);
                // Only one playable song: stepping lands back on it, so leave playback alone.
                if (target == selectedIndex && index != selectedIndex) return;
            }
            selectedIndex = target;
            SongChanged?.Invoke(Current);
        }
        static int Wrap(int index, int n) => ((index % n) + n) % n;
    }
}
