using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace RemixTable
{
    public class RibbonInteractionManager : MonoBehaviour
    {
        public RemixTableManager manager;
        readonly List<XRHandSubsystem> systems = new List<XRHandSubsystem>();
        RibbonView active;
        int owner;
        float startY, initialGain, initialEffect, endpointHold;
        bool endpoint;
        public enum GestureState { Idle, OneHandGain, TwoHandRegion, ZoomPending, Zooming }
        // Which control a one-hand hold edits; both use the same lift up/down motion, and only the selected
        // one moves. Picked with the VOLUME / FILTER buttons.
        // Volume: up = louder, down = quieter. Filter: up = thinner (high-pass), down = muffled (low-pass).
        public enum ControlMode { Volume, Filter }
        [SerializeField] ControlMode mode = ControlMode.Volume;
        public ControlMode Mode
        {
            get => mode;
            set
            {
                if (mode == value) return;
                mode = value;
                // Switching mid-hold keeps what was already set and continues from the hand's current pose.
                if (active != null && State == GestureState.OneHandGain) RebaseGain();
            }
        }
        public GestureState State { get; private set; }
        // LIVE playback: the song keeps playing while a ribbon is held (see RemixAudioManager.LiveMode).
        bool Live => manager.audioManager != null && manager.audioManager.LiveMode;
        // How far ahead of the playhead a live hold keeps its region, and how far behind it may lag and still catch up.
        public const float LiveLead = .3f, LiveCatchUp = .5f;
        public RibbonView ActiveRibbon => active;
        bool zoomReleaseRequired;
        float zoomStartDistance, zoomStartWindow;
        public bool ZoomReleaseRequired => zoomReleaseRequired;
        // Live filter readout for the transport UI: -1 = full low-pass, +1 = full high-pass.
        public bool IsFiltering => active!=null && effectEdit!=null;
        public float FilterEffect => effectEdit!=null ? effectEdit.value : 0;
        // Live lift readout: the gain being written by this hold.
        public bool IsLifting => active!=null && gainEdit!=null;
        public float LiftGain => gainEdit!=null ? gainEdit.value : 1;
        Vector3 firstPosition;
        AutomationEdit gainEdit, effectEdit;
        readonly HandSample[] hands = new HandSample[2];
        // position = pinch point (selection, regions). wrist = lift reference: it is steadier than the
        // fingertips, which wobble as the hand rotates slightly while lifting.
        public struct HandSample { public bool valid, pinch; public Vector3 position; public Vector3 wrist; public bool wristValid; }
        static Vector3 LiftPoint(HandSample hand) => hand.wristValid ? hand.wrist : hand.position;
        void Update()
        {
            SubsystemManager.GetSubsystems(systems);
            XRHandSubsystem system = null;
            foreach (var s in systems) if (s.running) { system=s; break; }
            if (system == null) { Cancel(); return; }
            ProcessHands(Read(system.leftHand,hands[0].pinch),Read(system.rightHand,hands[1].pinch),Time.unscaledDeltaTime);
        }
        // The XR adapter and deterministic tests feed the same gesture path. Positions are world space.
        public void ProcessHands(HandSample left, HandSample right, float deltaTime)
        {
            hands[0]=left; hands[1]=right;
            if(zoomReleaseRequired)
            {
                // Unknown tracking is never evidence that the user released a pinch.
                if(left.valid && right.valid && !left.pinch && !right.pinch) zoomReleaseRequired=false;
                return;
            }
            if(State==GestureState.ZoomPending || State==GestureState.Zooming)
            {
                if(!left.valid || !right.valid || !left.pinch || !right.pinch || manager.IsEditing ||
                    (manager.palmMenuCanvas!=null && manager.palmMenuCanvas.gameObject.activeInHierarchy)) { Cancel(); return; }
                float distance=Vector3.Distance(left.position,right.position);
                if(State==GestureState.ZoomPending && Mathf.Abs(distance-zoomStartDistance)<manager.design.zoomMovementThreshold) return;
                State=GestureState.Zooming;
                manager.zoom.Request(TimelineZoom.GestureWindow(zoomStartWindow,zoomStartDistance,distance,manager.design.zoomGestureSensitivity));
                return; // A zoom session can never acquire a ribbon, even if a hand enters one.
            }
            if (active == null)
            {
                for(int h=0;h<2;h++) if(hands[h].valid && hands[h].pinch)
                {
                    RibbonView nearest=null;float best=manager.design.interactionRadius;
                    foreach(var r in manager.ribbons)
                    {
                        if (r == null || !r.isActiveAndEnabled) continue;
                        if(r.CanSelectAt(hands[h].position,out float distance) && distance<best) {nearest=r;best=distance;}
                    }
                    if(nearest!=null) { Begin(nearest,h);break; }
                }
                if (active == null)
                {
                    if(manager.design.curvedTimeline && !manager.IsEditing && left.valid && right.valid && left.pinch && right.pinch &&
                        (manager.palmMenuCanvas==null || !manager.palmMenuCanvas.gameObject.activeInHierarchy) && InZoomWorkspace(left.position) && InZoomWorkspace(right.position))
                    {
                        zoomStartDistance=Vector3.Distance(left.position,right.position);
                        if(zoomStartDistance>=.12f)
                        {
                            zoomStartWindow=manager.zoom.VisibleSeconds;
                            State=GestureState.ZoomPending;
                        }
                    }
                    return;
                }
            }
            var primary=hands[owner];var secondary=hands[1-owner];
            if(!active.isActiveAndEnabled || !active.selected) { Cancel(); return; }
            if (!primary.valid || !primary.pinch)
            {
                if (State==GestureState.TwoHandRegion && secondary.valid && secondary.pinch)
                {
                    owner=1-owner; RebaseGain(); endpoint=false;
                    return;
                }
                Cancel(); return;
            }
            bool second=secondary.valid && secondary.pinch;
            if(second)
            {
                if(active.wholeStem) { active.wholeStem=false; gainEdit=effectEdit=null; }
                State=GestureState.TwoHandRegion;endpoint=false;
                float x1=active.LocalX(primary.position),x2=active.LocalX(secondary.position);
                active.regionStart=active.Timestamp(Mathf.Min(x1,x2));active.regionEnd=active.Timestamp(Mathf.Max(x1,x2));
                // Resize an edit already authored in this session without changing its gain.
                if(gainEdit!=null)
                {
                    gainEdit.start=active.regionStart;gainEdit.end=active.regionEnd;manager.automation.Upsert(gainEdit);
                }
                if(effectEdit!=null)
                {
                    effectEdit.start=active.regionStart;effectEdit.end=active.regionEnd;
                    if(effectEdit.curve!=null)
                    {
                        float length=Mathf.Max(.01f,AutomationStore.RegionLength(active.regionStart,active.regionEnd,manager.clock.duration));
                        effectEdit.curve=new AnimationCurve(new Keyframe(0,0),new Keyframe(length*.5f,effectEdit.value),new Keyframe(length,0));
                    }
                    manager.automation.Upsert(effectEdit);
                }
                return;
            }
            if(State==GestureState.TwoHandRegion) { RebaseGain(); return; }
            Vector3 local=active.transform.InverseTransformPoint(primary.position);
            if(endpoint && Vector3.Distance(local,firstPosition)<.025f)
            {
                endpointHold+=Mathf.Max(0,deltaTime);
                if(endpointHold>.65f && !active.wholeStem) {active.wholeStem=true;gainEdit=effectEdit=null;RebaseGain();}
            }
            else endpoint=false;
            if(Live && !active.wholeStem) FollowPlayhead();
            // Only the selected control is written. It wakes up past its dead zone and then follows the hand
            // for the rest of the hold, so it can be pushed one way and brought back (or past neutral) freely.
            float displacement=active.transform.InverseTransformPoint(LiftPoint(primary)).y-startY;
            bool moved=Mathf.Abs(displacement)>manager.design.gainDragDeadzone;
            if(mode==ControlMode.Volume && (gainEdit!=null || moved))
                Write(ref gainEdit,ParameterType.Gain,manager.design.MapDragGain(initialGain,displacement));
            if(mode==ControlMode.Filter && (effectEdit!=null || moved))
                Write(ref effectEdit,ParameterType.Effect,manager.design.MapDragFilter(initialEffect,displacement));
        }
        bool InZoomWorkspace(Vector3 world)
        {
            if(manager.workspace==null) return false;
            var d=manager.design;var local=manager.workspace.InverseTransformPoint(world);
            // A forward seated volume excludes hands behind the initial seated center and extreme reaches.
            return Mathf.Abs(local.x)<=.65f && local.z>=-d.Radius+.12f && local.z<=.18f && Mathf.Abs(local.y)<=.5f;
        }
        HandSample Read(XRHand hand,bool wasPinching)
        {
            if(!hand.isTracked || Camera.main==null) return default;
            if(!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index) || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb)) return default;
            Transform tracking=Camera.main.transform.parent;
            var sample=SamplePinch(index.position,thumb.position,tracking,wasPinching);
            if(hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist))
            {
                sample.wrist=tracking!=null?tracking.TransformPoint(wrist.position):wrist.position;
                sample.wristValid=true;
            }
            return sample;
        }
        public static HandSample SamplePinch(Vector3 index, Vector3 thumb, Transform tracking, bool wasPinching)
        {
            Vector3 local=(index+thumb)*.5f;
            return new HandSample{valid=true,pinch=Vector3.Distance(index,thumb)<(wasPinching?.035f:.024f),position=tracking!=null?tracking.TransformPoint(local):local};
        }
        // LIVE: a one-hand region is only ~0.6 s of song, which would scroll past the playhead almost at once.
        // Once the playhead reaches the held region, keep stretching its end just ahead of the playhead so the
        // change is heard (and recorded) for as long as the hold lasts. A region still ahead is left alone.
        void FollowPlayhead()
        {
            float duration=manager.clock.duration;
            if(duration<=LiveLead*2) return;
            float played=Mathf.Repeat(manager.clock.Time-active.regionStart,duration);
            float length=AutomationStore.RegionLength(active.regionStart,active.regionEnd,duration);
            float wanted=played+LiveLead;
            if(played>length+LiveCatchUp || wanted<=length || wanted>duration-LiveLead) return;
            active.regionEnd=Mathf.Repeat(manager.clock.Time+LiveLead,duration);
            foreach(var edit in new[]{gainEdit,effectEdit})
                if(edit!=null && !edit.wholeStem && edit.curve==null) { edit.start=active.regionStart; edit.end=active.regionEnd; manager.automation.Upsert(edit); }
        }
        void RebaseGain()
        {
            State=GestureState.OneHandGain;
            startY=active.transform.InverseTransformPoint(LiftPoint(hands[owner])).y;
            initialGain=gainEdit!=null ? AutomationStore.SafeGain(gainEdit.value) : Value(ParameterType.Gain);
            initialEffect=effectEdit!=null ? effectEdit.value : Value(ParameterType.Effect);
        }
        void Begin(RibbonView ribbon,int h)
        {
            manager.history.BeginGesture(manager.automation); // One undo step per hold.
            active=ribbon;owner=h;active.SetSelected(true);active.wholeStem=false;
            float t=active.Timestamp(active.LocalX(hands[h].position));
            active.regionStart=Mathf.Repeat(t-.3f,manager.clock.duration);active.regionEnd=Mathf.Repeat(t+.3f,manager.clock.duration);
            firstPosition=active.transform.InverseTransformPoint(hands[h].position);endpoint=Mathf.Abs(active.LocalX(hands[h].position))>manager.design.width*(manager.design.curvedTimeline ? manager.design.interactiveAngle/manager.design.arcAngle*.43f : .43f);endpointHold=0;
            gainEdit=effectEdit=null;RebaseGain();
        }
        float Value(ParameterType type)
        {
            float duration=manager.clock.duration;
            float midpoint=Mathf.Repeat(active.regionStart+AutomationStore.RegionLength(active.regionStart,active.regionEnd,duration)*.5f,duration);
            if(type==ParameterType.Gain)
                return active.wholeStem ? AutomationStore.SafeGain(manager.automation.EvaluateWholeGain(manager.library.Current.id,active.stem,midpoint)) :
                    manager.automation.EvaluateRegionGain(manager.library.Current.id,active.stem,midpoint,duration,manager.design.gainBoundaryFade);
            return manager.automation.Evaluate(manager.library.Current.id,active.stem,type,midpoint,duration);
        }
        void Write(ref AutomationEdit edit,ParameterType type,float value)
        {
            if(type==ParameterType.Gain)
            {
                if(edit==null) edit=manager.automation.FindGainEdit(manager.library.Current.id,active.stem,active.wholeStem,active.regionStart,active.regionEnd);
                if(edit!=null && !edit.wholeStem) { edit.start=active.regionStart; edit.end=active.regionEnd; }
                value=AutomationStore.SafeGain(value);
            }
            if(edit==null)
            {
                edit=new AutomationEdit {songId=manager.library.Current.id,stem=active.stem,start=active.regionStart,end=active.regionEnd,wholeStem=active.wholeStem,parameter=type};
                // Live holds stretch over time, so their filter is a flat plateau like gain instead of a peak.
                if(!edit.wholeStem && type!=ParameterType.Gain && !Live)
                {
                    float baseline=type==ParameterType.Gain?1:0;
                    float length=Mathf.Max(.01f,AutomationStore.RegionLength(edit.start,edit.end,manager.clock.duration));
                    edit.curve=new AnimationCurve(new Keyframe(0,baseline),new Keyframe(length*.5f,value),new Keyframe(length,baseline));
                }
            }
            if(type==ParameterType.Gain) edit.curve=null; // Scalar region plateau with shared boundary fades.
            else if(edit.curve!=null) edit.curve.MoveKey(1,new Keyframe(AutomationStore.RegionLength(edit.start,edit.end,manager.clock.duration)*.5f,value));
            edit.value=value;manager.automation.Upsert(edit);
        }
        public void Cancel() { if(State==GestureState.ZoomPending || State==GestureState.Zooming) zoomReleaseRequired=true; if(active!=null) { active.SetSelected(false); if(manager!=null) manager.history.EndGesture(manager.automation); }active=null;State=GestureState.Idle;gainEdit=effectEdit=null;hands[0]=hands[1]=default; }
        void OnDisable() => Cancel();
        void OnApplicationPause(bool paused) { if(paused) Cancel(); }
        void OnApplicationFocus(bool focused) { if(!focused) Cancel(); }
    }
}
