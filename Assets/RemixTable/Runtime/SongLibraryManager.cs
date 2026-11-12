using System;
using UnityEngine;
namespace RemixTable
{
    public class SongLibraryManager : MonoBehaviour
    {
        public SongMetadata[] songs;
        public int selectedIndex;
        public SongMetadata Current => songs[selectedIndex];
        public event Action<SongMetadata> SongChanged;
        public void Select(int index)
        {
            if (songs == null || songs.Length == 0) return;
            selectedIndex = ((index % songs.Length) + songs.Length) % songs.Length;
            SongChanged?.Invoke(Current);
        }
    }
}
