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
        public void RequestWorkspaceAlignment()
        {
            alignmentPending=true;hasCandidate=false;settleTime=0;
            if(interactions!=null) interactions.CancelForWorkspaceAlignment();
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

