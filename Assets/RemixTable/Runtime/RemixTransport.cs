using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    public sealed class RemixTransport : MonoBehaviour
    {
        public RemixTableManager manager;
        public Button previousSong, playPause, nextSong;
        public TMP_Text songTitle, playbackTime, playPauseLabel;
        public TMP_Text palmInstructions;
        public const string Guidance="PINCH + LIFT / GAIN\nTWO HANDS / REGION\nTWIST / FILTER SWEEP\nHOLD ENDPOINT / WHOLE STEM";
        int displayedSecond=-1, displayedFilter;
        public static string FilterLabel(int percent,bool editing) =>
            percent<0 ? $"LOW {-percent}%  " : percent>0 ? $"HIGH {percent}%  " : editing ? "EDIT  " : "";
        bool displayedManual, displayedEditing, displayedReady;
        bool dirty=true;
        void OnEnable() { manager.library.SongChanged+=SongChanged;dirty=true; }
        void OnDisable() { if(manager!=null && manager.library!=null) manager.library.SongChanged-=SongChanged; }
        void SongChanged(SongMetadata song) { dirty=true; }
        public void PreviousSong() => Navigate(-1);
        public void NextSong() => Navigate(1);
        void Navigate(int step)
        {
            bool paused=manager.audioManager.IsManuallyPaused;
            manager.library.Select(manager.library.selectedIndex+step);
            if(paused) manager.audioManager.Pause();
        }
        public void TogglePlayback()
        {
            if(!manager.audioManager.Ready) return;
            if(manager.audioManager.IsManuallyPaused) manager.audioManager.Resume(); else manager.audioManager.Pause();
            dirty=true;
        }
        void Update()
        {
            var audio=manager.audioManager;
            bool editing=audio.EditingPauseCount>0, manual=audio.IsManuallyPaused;
            int second=Mathf.FloorToInt(manager.clock.Time);
            var gestures=manager.interactions;
            int filter=gestures!=null && gestures.IsTwisting ? Mathf.RoundToInt(gestures.TwistEffect*100) : 0;
            if(!dirty && second==displayedSecond && manual==displayedManual && editing==displayedEditing && audio.Ready==displayedReady && filter==displayedFilter) return;
            displayedFilter=filter;
            if(dirty) songTitle.text=manager.library.Current.title;
            playPause.interactable=audio.Ready;
            previousSong.interactable=nextSong.interactable=manager.library.songs.Length>1;
            playPauseLabel.text=!audio.Ready ? "No audio" : manual ? "Play" : "Pause";
            int total=Mathf.FloorToInt(manager.clock.duration);
            playbackTime.text=!audio.Ready ? "AUDIO UNAVAILABLE" : $"{FilterLabel(filter,editing)}{second/60:00}:{second%60:00} / {total/60:00}:{total%60:00}";
            displayedSecond=second;displayedManual=manual;displayedEditing=editing;displayedReady=audio.Ready;dirty=false;
        }
    }
}
