using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace RemixTable
{
    // The existing XR UI module owns the press. Sample a fixed distance along its
    // world ray, never intersect the moving workspace's plane (which would feed back).
    [DisallowMultipleComponent]
    public sealed class RecenterWorkspaceButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,
        IInitializePotentialDragHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,ICancelHandler
    {
        public RemixWorkspaceInteraction workspace;
        public Button button;
        PointerEventData captured;
        IUIInteractor source;
        float distance,started;
        int blockedClickFrame=-1;
        public bool Captured=>captured!=null;
        public void OnInitializePotentialDrag(PointerEventData data){data.useDragThreshold=false;}
        public void OnPointerDown(PointerEventData data)
        {
            blockedClickFrame=Time.frameCount;
            if(Captured || data.button!=PointerEventData.InputButton.Left || !button.IsInteractable())return;
            var tracked=data as TrackedDeviceEventData;source=tracked!=null?tracked.interactor:null;
            var hit=data.pointerCurrentRaycast;var world=hit.gameObject!=null?hit.worldPosition:transform.position;
            if(tracked!=null)
            {
                if(tracked.rayPoints==null || tracked.rayPoints.Count<2)return;
                distance=DistanceAlong(tracked.rayPoints,world);
            }
            else
            {
                if(data.pressEventCamera==null)return;
                distance=Vector3.Distance(data.pressEventCamera.ScreenPointToRay(data.position).origin,world);
            }
            captured=data;
            if(!TryPoint(out var point,true) || !workspace.BeginMove(point)){ClearCapture();return;}
            started=Time.unscaledTime;data.eligibleForClick=false;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if(!Captured || data.pointerId!=captured.pointerId)return;
            captured=data;
            bool valid=TryPoint(out var point,true);
            if(valid)workspace.UpdateMove(point,0); // Catch movement on the release frame too.
            data.eligibleForClick=false;
            workspace.FinishMove(Time.unscaledTime-started,valid);
        }
        public void OnBeginDrag(PointerEventData data){data.eligibleForClick=false;}
        public void OnDrag(PointerEventData data)
        {
            if(Captured && data.pointerId==captured.pointerId){captured=data;data.eligibleForClick=false;}
        }
        public void OnEndDrag(PointerEventData data){}
        public void OnCancel(BaseEventData data)=>Cancel();
        // Pointer taps are dispatched once from release, including poke click-on-down.
        // Keyboard/explicit Button invocations still use the same recenter command.
        public void Click(){if(!Captured && blockedClickFrame!=Time.frameCount)workspace.manager.RecenterWorkspace();}
        public void ClearCapture(){captured=null;source=null;blockedClickFrame=Time.frameCount;}
        public void Advance(float dt)
        {
            if(!Captured)return;
            if(!TryPoint(out var point)){Cancel();return;}
            workspace.UpdateMove(point,dt);
        }
        void Cancel(){ClearCapture();blockedClickFrame=Time.frameCount;if(workspace!=null)workspace.CancelOwnedGesture();}
        bool TryPoint(out Vector3 point,bool eventSample=false)
        {
            point=default;
            if(source!=null && (!(source is Behaviour owner) || owner==null || !owner.isActiveAndEnabled))return false;
            // XRI stores the new model after dispatching pointer callbacks. Those callbacks
            // must read the fresh event; LateUpdate can then read the committed model.
            if(source!=null && !eventSample)
            {
                if(!source.TryGetUIModel(out var model) || !model.select || model.raycastPoints==null || model.raycastPoints.Count<2)return false;
                point=PointAlong(model.raycastPoints,distance);return Finite(point);
            }
            if(captured is TrackedDeviceEventData tracked)
            {
                if(tracked.rayPoints==null || tracked.rayPoints.Count<2)return false;
                point=PointAlong(tracked.rayPoints,distance);return Finite(point);
            }
            if(captured==null || captured.pressEventCamera==null)return false;
            point=captured.pressEventCamera.ScreenPointToRay(captured.position).GetPoint(distance);return Finite(point);
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static float DistanceAlong(List<Vector3> ray,Vector3 hit)
        {
            float best=float.PositiveInfinity,result=0,total=0;
            for(int i=1;i<ray.Count;i++)
            {
                var delta=ray[i]-ray[i-1];float length=delta.magnitude;
                float t=Mathf.Clamp01(Vector3.Dot(hit-ray[i-1],delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                float square=(hit-ray[i-1]-delta*t).sqrMagnitude;
                if(square<best){best=square;result=total+length*t;}total+=length;
            }
            return result;
        }
        static Vector3 PointAlong(List<Vector3> ray,float remaining)
        {
            for(int i=1;i<ray.Count;i++)
            {
                var delta=ray[i]-ray[i-1];float length=delta.magnitude;
                if(length<.000001f)continue;
                if(remaining<=length || i==ray.Count-1)return ray[i-1]+delta*(remaining/length);
                remaining-=length;
            }
            return ray[0];
        }
        void OnDisable()=>Cancel();
        void OnApplicationPause(bool paused){if(paused)Cancel();}
        void OnApplicationFocus(bool focus){if(!focus)Cancel();}
    }
}
