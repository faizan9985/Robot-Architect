using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
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
        public Camera workspaceCamera;
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
        readonly List<XRInputSubsystem> inputSubsystems=new List<XRInputSubsystem>();
        XRInputSubsystem inputSubsystem;
        float nextInputSearch, settleTime;
        bool alignmentPending, hasCandidate, hasOriginSample, hasRecenterCount;
        Pose candidatePose, lastOffsetPose;
        TrackingOriginModeFlags lastOriginMode;
        int lastRecenterCount;
        public bool WorkspaceAligning => alignmentPending;
        public event System.Action WorkspaceAlignmentRequested;
        public RemixSpeedSelector speedSelector;
        public RemixNavigationToolbar navigationToolbar;
        public bool IsEditing
        {
            get
            {
                if(audioManager!=null && audioManager.EditingPauseCount>0) return true;
                if(ribbons!=null) foreach(var ribbon in ribbons) if(ribbon!=null && ribbon.selected) return true;
                return false;
            }
        }
        void LateUpdate()
        {
            UpdateWorkspaceAlignment();
            zoom.Tick(Time.unscaledDeltaTime,IsEditing,design.zoomSmoothingSeconds);
        }
        void OnEnable()
        {
            history.Manager=this;
            library.SongChanged += ChangeSong; ChangeSong(library.Current);
            RequestWorkspaceAlignment();
        }
        void OnDisable()
        {
            library.SongChanged -= ChangeSong;
            SetInputSubsystem(null);
        }
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
            UndoableChange(() => { automation.ResetSong(library.Current.id);mix.ClearMuteSolo();if(audioManager!=null) audioManager.ClearLoop(); },true);
        }
        void UndoableChange(System.Action change, bool includeMixAndLoop=false)
        {
            if (interactions != null) interactions.Cancel(); // Close any open hold first so it is its own step.
            history.Manager=this;
            history.BeginGesture(automation,includeMixAndLoop);
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
        public void RequestWorkspaceAlignment()
        {
            alignmentPending=true;hasCandidate=false;settleTime=0;
            if(interactions!=null) interactions.CancelForWorkspaceAlignment();
            WorkspaceAlignmentRequested?.Invoke();
            externalHolds.Clear();
            // Release any independently held ribbon owners as well; manual pause and automation survive.
            if(ribbons!=null) foreach(var ribbon in ribbons) if(ribbon!=null && ribbon.selected) ribbon.SetSelected(false);
        }
        void TrackingOriginUpdated(XRInputSubsystem source)
        {
            if(source==inputSubsystem && source.running) RequestWorkspaceAlignment();
        }
        void SetInputSubsystem(XRInputSubsystem source)
        {
            if(inputSubsystem==source) return;
            if(inputSubsystem!=null) inputSubsystem.trackingOriginUpdated-=TrackingOriginUpdated;
            inputSubsystem=source;
            hasOriginSample=hasRecenterCount=false;
            if(source!=null)
            {
                source.trackingOriginUpdated+=TrackingOriginUpdated;
                RequestWorkspaceAlignment();
            }
        }
        void UpdateWorkspaceAlignment()
        {
            if(workspaceCamera==null) workspaceCamera=Camera.main;
            if(workspaceCamera==null || workspace==null) return;
            if(Time.unscaledTime>=nextInputSearch)
            {
                nextInputSearch=Time.unscaledTime+1;
                SubsystemManager.GetSubsystems(inputSubsystems);
                XRInputSubsystem running=null;
                foreach(var source in inputSubsystems) if(source.running) { running=source;break; }
                SetInputSubsystem(running);
            }
            var cameraTransform=workspaceCamera.transform;
            var trackingSpace=cameraTransform.parent;
            // Local Camera Offset changes identify device/floor-height changes without treating rig
            // locomotion or ordinary head movement as a recenter.
            Pose offset=trackingSpace!=null ? new Pose(trackingSpace.localPosition,trackingSpace.localRotation) : Pose.identity;
            var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            bool tracked=inputSubsystem!=null && inputSubsystem.running &&
                device.TryGetFeatureValue(CommonUsages.isTracked,out bool isTracked) && isTracked &&
                device.TryGetFeatureValue(CommonUsages.trackingState,out InputTrackingState state) &&
                (state&(InputTrackingState.Position|InputTrackingState.Rotation))==(InputTrackingState.Position|InputTrackingState.Rotation);
            // Desktop scene tests/previews have no HMD. This path is never enabled in a Quest build.
            if(Application.isEditor && inputSubsystem==null) tracked=true;
            int? recenterCount=null;
            #if UNITY_ANDROID && !UNITY_EDITOR
            if(inputSubsystem!=null && inputSubsystem.running && OVRPlugin.initialized)
                recenterCount=OVRPlugin.GetLocalTrackingSpaceRecenterCount();
            #endif
            ProcessWorkspacePose(new Pose(cameraTransform.position,cameraTransform.rotation),tracked,offset,
                inputSubsystem!=null ? inputSubsystem.GetTrackingOriginMode() : TrackingOriginModeFlags.Unknown,
                recenterCount,Time.unscaledDeltaTime);
        }
        // Runtime sampling and deterministic tests share the same alignment path. head is world space;
        // cameraOffset is local to the XR Origin, never an extra world-space height to add to head.
        public void ProcessWorkspacePose(Pose head,bool tracked,Pose cameraOffset,TrackingOriginModeFlags originMode,int? recenterCount,float deltaTime)
        {
            if(hasOriginSample && (originMode!=lastOriginMode || Vector3.Distance(cameraOffset.position,lastOffsetPose.position)>.001f ||
                Quaternion.Angle(cameraOffset.rotation,lastOffsetPose.rotation)>.1f)) RequestWorkspaceAlignment();
            hasOriginSample=true;lastOffsetPose=cameraOffset;lastOriginMode=originMode;
            if(recenterCount.HasValue)
            {
                if(hasRecenterCount && recenterCount.Value!=lastRecenterCount) RequestWorkspaceAlignment();
                hasRecenterCount=true;lastRecenterCount=recenterCount.Value;
            }
            if(!anchored && !alignmentPending) RequestWorkspaceAlignment();
            if(!alignmentPending) return; // A settled workspace never follows head motion.
            var forward=Vector3.ProjectOnPlane(head.rotation*Vector3.forward,Vector3.up);
            if(!tracked || !Finite(head.position) || !Finite(head.rotation*Vector3.forward) || forward.sqrMagnitude<.0001f)
            {
                hasCandidate=false;settleTime=0;return;
            }
            var pose=design.SeatedPose(head.position,forward);
            if(!hasCandidate || Vector3.Distance(pose.position,candidatePose.position)>design.recenterPositionTolerance ||
                Quaternion.Angle(pose.rotation,candidatePose.rotation)>design.recenterYawTolerance)
            {
                hasCandidate=true;candidatePose=pose;settleTime=0;return;
            }
            settleTime+=Mathf.Max(0,deltaTime);
            if(settleTime<Mathf.Max(.01f,design.recenterStabilizationSeconds)) return;
            ApplyLaneLayout();
            workspace.SetPositionAndRotation(pose.position,pose.rotation);
            anchored=true;alignmentPending=false;hasCandidate=false;
        }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
