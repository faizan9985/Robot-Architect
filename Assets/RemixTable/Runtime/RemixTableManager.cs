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
        public readonly RemixEditHistory history = new RemixEditHistory();
        // Mute / solo / spatial placement for the current song (saved with the remix).
        public readonly StemMix mix = new StemMix();
        // SNAP: region edges land on the song's beats.
        public bool snapToBeat;
        // Raised after a song has been loaded and its history cleared (RemixPersistence restores the save here).
        public event System.Action<SongMetadata> SongReady;
        // Grabs that are not ribbon edits (e.g. a spatial stem orb). While any is held, ribbon pinches and
        // the two-hand zoom stay off so moving an orb never edits the song.
        readonly System.Collections.Generic.HashSet<Object> externalHolds = new System.Collections.Generic.HashSet<Object>();
        public bool ExternalHold { get { externalHolds.RemoveWhere(x => x == null); return externalHolds.Count > 0; } }
        public void BeginExternalHold(Object owner) { if (owner != null && externalHolds.Add(owner) && interactions != null) interactions.Cancel(); }
        public void EndExternalHold(Object owner) => externalHolds.Remove(owner);
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
            interactions.Cancel(); history.Clear(); clock.Reset(song.duration);
            foreach (var r in ribbons) r.ResetPhase();
            if (audioManager != null) { audioManager.ClearLoop(); audioManager.Load(song); }
            SongReady?.Invoke(song);
        }
        // CLR on a stem: removes all of that stem's edits for this song as one undoable step.
        public void ResetStem(StemId stem) => UndoableChange(() => automation.ResetStem(library.Current.id, stem));
        // RESET ALL: removes every edit for this song (undoable) and clears mute / solo.
        public void ResetSong()
        {
            UndoableChange(() => automation.ResetSong(library.Current.id));
            mix.ClearMuteSolo();
        }
        void UndoableChange(System.Action change)
        {
            if (interactions != null) interactions.Cancel(); // Close any open hold first so it is its own step.
            history.BeginGesture(automation);
            change();
            history.EndGesture(automation);
        }
        // SPATIAL stem placement is stored relative to the seated head anchor (eye level, at the center of the
        // ribbon arc) in the workspace's orientation, so it follows the table when it is re-anchored.
        public Vector3 HeadAnchorLocal => design == null ? Vector3.zero :
            new Vector3(0, design.workspaceEyeDrop, -(design.curvedTimeline ? design.Radius : design.workspaceDistance));
        public Vector3 SpatialWorld(Vector3 local) => workspace == null ? local : workspace.position + workspace.rotation * (HeadAnchorLocal + local);
        public Vector3 SpatialLocal(Vector3 world) => workspace == null ? world : Quaternion.Inverse(workspace.rotation) * (world - workspace.position) - HeadAnchorLocal;
        // Stem pitch in semitones at the playhead (PITCH automation).
        public float PitchAt(StemId stem, float time) =>
            Mathf.Clamp(automation.Evaluate(library.Current.id, stem, ParameterType.Pitch, time, clock.duration), -PitchShiftDsp.MaxSemitones, PitchShiftDsp.MaxSemitones);
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

