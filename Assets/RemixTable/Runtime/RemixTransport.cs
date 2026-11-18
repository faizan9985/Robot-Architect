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
        public TMP_Text workspaceInstructions;
        public const string Guidance="PICK VOLUME, FILTER OR PITCH BELOW\nPINCH + MOVE UP / DOWN\nFILTER: UP THIN, DOWN MUFFLED\nTWO HANDS / REGION\nHOLD ENDPOINT / WHOLE STEM\nEMPTY SPACE / SPREAD TO ZOOM";
        public const string WorkspaceGuidance=Guidance+"\nTWO HANDS ON PLAYHEAD / HEIGHT\nDRAG RECENTER / MOVE WORKSPACE";
        public RemixToolbox toolbox; // Optional: its short feedback messages replace the clock briefly.
        public static string GestureLabel(int? volumePercent,int? filterPercent,int? semitones)
        {
            string label=GestureLabel(volumePercent,filterPercent);
            if(!semitones.HasValue) return label;
            string pitch=semitones.Value==0 ? "PITCH 0" : $"PITCH {(semitones.Value>0?"+":"")}{semitones.Value}";
            return label.Length>0 ? label+"  "+pitch : pitch;
        }
        int displayedSecond=-1;
        int? displayedVolume, displayedFilter, displayedPitch;
        string displayedMessage="";
        bool displayedLoop;
        // Live readout of what the current hold is doing, e.g. "VOL 120%  HIGH 40%". Empty when idle.
        public static string GestureLabel(int? volumePercent,int? filterPercent)
        {
            string volume=volumePercent.HasValue ? $"VOL {volumePercent.Value}%" : null;
            string filter=!filterPercent.HasValue ? null : filterPercent.Value<0 ? $"LOW {-filterPercent.Value}%" : filterPercent.Value>0 ? $"HIGH {filterPercent.Value}%" : "FLAT";
            return volume!=null && filter!=null ? volume+"  "+filter : volume ?? filter ?? "";
        }
        bool displayedManual, displayedEditing, displayedReady, displayedLive;
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
            bool editing=audio.EditingPauseCount>0, manual=audio.IsManuallyPaused, live=audio.LiveMode;
            int second=Mathf.FloorToInt(manager.clock.Time);
            var gestures=manager.interactions;
            int? volume=gestures!=null && gestures.IsLifting ? Mathf.RoundToInt(gestures.LiftGain*100) : (int?)null;
            int? filter=gestures!=null && gestures.IsFiltering ? Mathf.RoundToInt(gestures.FilterEffect*100) : (int?)null;
            int? pitch=gestures!=null && gestures.IsPitching ? Mathf.RoundToInt(gestures.PitchValue) : (int?)null;
            string message=toolbox!=null ? toolbox.Message : "";
            bool looping=audio.LoopActive;
            if(!dirty && second==displayedSecond && manual==displayedManual && editing==displayedEditing && audio.Ready==displayedReady && live==displayedLive && volume==displayedVolume && filter==displayedFilter && pitch==displayedPitch && message==displayedMessage && looping==displayedLoop) return;
            displayedVolume=volume;displayedFilter=filter;displayedPitch=pitch;displayedMessage=message;displayedLoop=looping;
            string gesture=GestureLabel(volume,filter,pitch);
            if(gesture.Length==0 && message.Length>0) gesture=message;
            if(dirty) songTitle.text=manager.library.Current.title;
            playPause.interactable=audio.Ready;
            previousSong.interactable=nextSong.interactable=manager.library.songs.Length>1;
            playPauseLabel.text=!audio.Ready ? "No audio" : manual ? "Play" : "Pause";
            int total=Mathf.FloorToInt(manager.clock.duration);
            // While holding a ribbon the time slot shows the gesture readout (in EDIT mode playback is paused anyway).
            playbackTime.text=!audio.Ready ? "AUDIO UNAVAILABLE" : gesture.Length>0 ? (live ? "LIVE  "+gesture : gesture) :
                $"{(live ? "LIVE  " : editing ? "EDIT  " : "")}{(looping ? "LOOP  " : "")}{second/60:00}:{second%60:00} / {total/60:00}:{total%60:00}";
            displayedSecond=second;displayedManual=manual;displayedEditing=editing;displayedReady=audio.Ready;displayedLive=live;dirty=false;
        }
    }
}
