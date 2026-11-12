using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace RemixTable
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SpatialSongPicker : MonoBehaviour
    {
        public SongLibraryManager library;
        public TextMesh label;
        float nextAllowed;
        void OnEnable() { GetComponent<XRSimpleInteractable>().selectEntered.AddListener(Select); library.SongChanged+=Refresh; Refresh(library.Current); }
        void OnDisable() { GetComponent<XRSimpleInteractable>().selectEntered.RemoveListener(Select);library.SongChanged-=Refresh; }
        void Select(SelectEnterEventArgs args) { if(Time.unscaledTime<nextAllowed)return; nextAllowed=Time.unscaledTime+.6f;library.Select(library.selectedIndex+1); }
        void Refresh(SongMetadata song) { label.text="SCULPT SOUND  /  "+song.title+(song.stems == null || System.Array.TrueForAll(song.stems, s=>s.clip==null) ? " (NO AUDIO)" : "")+"\nPINCH HERE  ·  NEXT SONG"; }
    }
}

