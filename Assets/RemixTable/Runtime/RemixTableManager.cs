using UnityEngine;
namespace RemixTable
{
    [DefaultExecutionOrder(-100)]
    public class RemixTableManager : MonoBehaviour
    {
        public RemixAudioManager audioManager; public RemixDesign design;
        public SongLibraryManager library;
        public RibbonInteractionManager interactions;
        public RibbonView[] ribbons;
        public Transform workspace;
        public PlaybackClock clock = new PlaybackClock();
        public AutomationStore automation = new AutomationStore();
        public bool anchored;
        public TimelineZoom zoom = new TimelineZoom();
        public Canvas palmMenuCanvas;
        public bool IsEditing
        {
            get
            {
                if(audioManager!=null && audioManager.EditingPauseCount>0) return true;
                if(ribbons!=null) foreach(var ribbon in ribbons) if(ribbon!=null && ribbon.selected) return true;
                return false;
            }
        }
        void LateUpdate() => zoom.Tick(Time.unscaledDeltaTime,IsEditing,design.zoomSmoothingSeconds);
        void OnEnable() { library.SongChanged += ChangeSong; ChangeSong(library.Current); }
        void OnDisable() { library.SongChanged -= ChangeSong; }
        void ChangeSong(SongMetadata song)
        {
            interactions.Cancel(); clock.Reset(song.duration);
            foreach (var r in ribbons) r.ResetPhase();
            if (audioManager != null) audioManager.Load(song);
        }
        public void ApplyLaneLayout()
        {
            foreach(var ribbon in ribbons)
            {
                int row=ribbon.stem==StemId.Vocals?0:ribbon.stem==StemId.Drums?1:ribbon.stem==StemId.Bass?2:3;
                ribbon.transform.localPosition=new Vector3(0,design.LaneY(row),0);
            }
            var transport=workspace.GetComponentInChildren<RemixTransport>();
            if(transport!=null) transport.transform.localPosition=new Vector3(0,design.TransportY,-.006f);
            var playhead=workspace.GetComponentsInChildren<LineRenderer>();
            foreach(var line in playhead) if(line.name=="FixedPlaybackIndicator")
            {
                float half=1.5f*design.spacing+design.laneHeight*.5f;
                line.SetPosition(0,new Vector3(0,-half,-.006f));line.SetPosition(1,new Vector3(0,half,-.006f));
            }
        }
        void Update()
        {
            if (!anchored && Camera.main != null && Time.timeSinceLevelLoad > 1)
            {
                ApplyLaneLayout();
                var cam = Camera.main.transform;
                var pose=design.SeatedPose(cam.position,cam.forward);
                workspace.SetPositionAndRotation(pose.position,pose.rotation);
                anchored = true;
            }
            // The audio manager owns the DSP timeline.
        }
    }
}

