using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace RemixTable
{
    // Session-only transform authority. Existing hand samples own scaling; XRI owns frame selection.
    [DefaultExecutionOrder(200),DisallowMultipleComponent]
    public sealed class RemixWorkspaceInteraction : MonoBehaviour
    {
        public RemixTableManager manager;
        public Transform waveformRoot;
        public LineRenderer playhead;
        public XRGrabInteractable navigationHandle;
        public RectTransform navigationBackground,handleRect;
        public BoxCollider[] borderColliders;
        public Image borderGraphic;
        public Canvas[] uiCanvases;
        public GameObject masterVolumePanel;
        public Slider masterVolumeSlider;
        public TMP_Text instructions;
        [Min(.1f)] public float minimumHeight=.25f,maximumHeight=.65f;
        [Min(.005f)] public float playheadGrabRadius=.03f;
        [Min(.01f)] public float scaleSmoothing=.035f,dragSmoothing=.025f;
        public enum Ownership { Idle, VerticalScale, NavigationDrag }
        public Ownership State{get;private set;}
        public bool OwnsGesture=>State!=Ownership.Idle;
        public bool ReleaseRequired{get;private set;}
        public bool PanelVisible=>masterVolumePanel!=null && masterVolumePanel.activeInHierarchy;
        public float VerticalScale{get;private set;}=1;
        public float DefaultHeight=>3*manager.design.spacing+manager.design.laneHeight;
        public float Height=>DefaultHeight*VerticalScale;
        float initialSeparation,initialHeight;
        Vector3 dragStart,attachStart;
        IXRSelectInteractor dragInteractor;
        Transform[] labels;
        Vector3[] labelScales,uiPositions;
        CanvasGroup[] groups;
        bool[] raycasts,interactable;
        bool initialized;
        Vector2 lastFrameSize;
        public void Initialize()
        {
            if(initialized)return;
            if(manager==null)manager=GetComponent<RemixTableManager>();
            if(waveformRoot==null || playhead==null || uiCanvases==null)return;
            var texts=waveformRoot.GetComponentsInChildren<TextMesh>(true);var tmp=waveformRoot.GetComponentsInChildren<TMP_Text>(true);
            labels=new Transform[texts.Length+tmp.Length];labelScales=new Vector3[labels.Length];
            for(int i=0;i<texts.Length;i++)labels[i]=texts[i].transform;
            for(int i=0;i<tmp.Length;i++)labels[texts.Length+i]=tmp[i].transform;
            for(int i=0;i<labels.Length;i++)labelScales[i]=labels[i].localScale;
            uiPositions=new Vector3[uiCanvases.Length];groups=new CanvasGroup[uiCanvases.Length];raycasts=new bool[groups.Length];interactable=new bool[groups.Length];
            for(int i=0;i<uiCanvases.Length;i++)
            {
                uiPositions[i]=uiCanvases[i].transform.localPosition;groups[i]=uiCanvases[i].GetComponent<CanvasGroup>();
                if(groups[i]==null)groups[i]=uiCanvases[i].gameObject.AddComponent<CanvasGroup>();
            }
            initialized=true;SetHeight(DefaultHeight);UpdateFrame();
        }
        void Awake()=>Initialize();
        void OnEnable()
        {
            if(manager!=null)manager.WorkspaceAlignmentRequested+=CancelOwnedGesture;
            if(navigationHandle!=null){navigationHandle.selectEntered.AddListener(BeginDrag);navigationHandle.selectExited.AddListener(EndDrag);}
        }
        void OnDisable()
        {
            CancelOwnedGesture();if(manager!=null)manager.WorkspaceAlignmentRequested-=CancelOwnedGesture;
            if(navigationHandle!=null){navigationHandle.selectEntered.RemoveListener(BeginDrag);navigationHandle.selectExited.RemoveListener(EndDrag);}
        }
        public void SetHeight(float height)
        {
            VerticalScale=Mathf.Clamp(height,Mathf.Min(minimumHeight,DefaultHeight),Mathf.Max(maximumHeight,DefaultHeight))/DefaultHeight;
            ApplyLayout();
        }
        public void ApplyLayout()
        {
            if(!initialized)return;
            waveformRoot.localScale=new Vector3(1,VerticalScale,1);
            for(int i=0;i<labels.Length;i++)if(labels[i]!=null){var s=labelScales[i];labels[i].localScale=new Vector3(s.x,s.y/VerticalScale,s.z);}
            float delta=(Height-DefaultHeight)*.5f;
            for(int i=0;i<uiCanvases.Length;i++)
            {
                var p=uiPositions[i];p.y+=p.y>0?delta:-delta;uiCanvases[i].transform.localPosition=p;
            }
        }
        public void ResetHeight(){SetHeight(DefaultHeight);}
        public void ToggleMasterVolume()
        {
            if(masterVolumePanel==null)return;
            manager.interactions.Cancel();masterVolumePanel.SetActive(!masterVolumePanel.activeSelf);
        }
        public bool OnPlayhead(Vector3 world)
        {
            Vector3 a=playhead.transform.TransformPoint(playhead.GetPosition(0)),b=playhead.transform.TransformPoint(playhead.GetPosition(1));
            Vector3 ab=b-a;float t=Mathf.Clamp01(Vector3.Dot(world-a,ab)/Mathf.Max(.000001f,ab.sqrMagnitude));
            return Vector3.Distance(world,a+ab*t)<=playheadGrabRadius;
        }
        public bool ProcessHands(RibbonInteractionManager.HandSample left,RibbonInteractionManager.HandSample right,float dt,bool uiBlocked)
        {
            if(!initialized)return false;
            if(ReleaseRequired)
            {
                if(left.valid && right.valid && !left.pinch && !right.pinch)ReleaseRequired=false;
                return true;
            }
            if(State==Ownership.NavigationDrag)return true;
            if(State==Ownership.VerticalScale)
            {
                if(!left.valid || !right.valid || !left.pinch || !right.pinch || manager.WorkspaceAligning || !manager.WorkspaceTracked)
                {CancelOwnedGesture();return true;}
                float separation=Mathf.Abs(Vector3.Dot(right.position-left.position,manager.workspace.up));
                float wanted=Mathf.Clamp(initialHeight*separation/initialSeparation,minimumHeight,maximumHeight);
                SetHeight(Mathf.Lerp(Height,wanted,1-Mathf.Exp(-Mathf.Max(0,dt)/scaleSmoothing)));return true;
            }
            if(uiBlocked || PanelVisible || manager.ExternalHold || manager.WorkspaceAligning || !manager.anchored || !manager.WorkspaceTracked || manager.interactions.GestureReleaseRequired)return false;
            if(left.valid && right.valid && left.pinch && right.pinch && OnPlayhead(left.position) && OnPlayhead(right.position))
            {
                float separation=Mathf.Abs(Vector3.Dot(right.position-left.position,manager.workspace.up));
                if(separation<.06f)return false;
                // BeginExternalHold closes the old ribbon edit before this new ownership is installed.
                manager.BeginExternalHold(this);initialSeparation=separation;initialHeight=Height;State=Ownership.VerticalScale;BlockUi(true);return true;
            }
            return false;
        }
        void BeginDrag(SelectEnterEventArgs args)
        {
            if(manager.ExternalHold || manager.WorkspaceAligning || !manager.anchored || !manager.WorkspaceTracked)
            {navigationHandle.interactionManager.CancelInteractableSelection((IXRSelectInteractable)navigationHandle);return;}
            var incoming=args.interactorObject;var attach=incoming.GetAttachTransform((IXRInteractable)navigationHandle);
            if(attach==null){navigationHandle.interactionManager.CancelInteractableSelection((IXRSelectInteractable)navigationHandle);return;}
            manager.BeginExternalHold(this);dragInteractor=incoming;
            attachStart=attach.position;dragStart=manager.workspace.position;State=Ownership.NavigationDrag;BlockUi(true);
        }
        void EndDrag(SelectExitEventArgs args){if(State==Ownership.NavigationDrag)CancelOwnedGesture();}
        public void CancelOwnedGesture()
        {
            if(!OwnsGesture)return;
            bool wasDrag=State==Ownership.NavigationDrag;State=Ownership.Idle;dragInteractor=null;ReleaseRequired=true;BlockUi(false);manager.EndExternalHold(this);
            if(wasDrag && navigationHandle!=null && navigationHandle.isSelected && navigationHandle.interactionManager!=null)
                navigationHandle.interactionManager.CancelInteractableSelection((IXRSelectInteractable)navigationHandle);
        }
        void BlockUi(bool block)
        {
            for(int i=0;i<groups.Length;i++)
            {
                if(block){raycasts[i]=groups[i].blocksRaycasts;interactable[i]=groups[i].interactable;groups[i].blocksRaycasts=groups[i].interactable=false;}
                else{groups[i].blocksRaycasts=raycasts[i];groups[i].interactable=interactable[i];}
            }
            var ui=manager.GetComponent<RemixUiPresentation>();if(ui!=null && ui.tooltip!=null)ui.tooltip.Clear();
        }
        public void AdvanceDrag(float dt)
        {
            if(State!=Ownership.NavigationDrag)return;
            if(manager.WorkspaceAligning || !manager.WorkspaceTracked || navigationHandle==null || !navigationHandle.isSelected || !(dragInteractor is Behaviour owner) || owner==null || !owner.isActiveAndEnabled)
            {CancelOwnedGesture();return;}
            var attach=dragInteractor.GetAttachTransform((IXRInteractable)navigationHandle);
            if(attach==null){CancelOwnedGesture();return;}
            Vector3 target=dragStart+attach.position-attachStart;
            manager.MoveWorkspace(Vector3.Lerp(manager.workspace.position,target,1-Mathf.Exp(-Mathf.Max(0,dt)/dragSmoothing)));
        }
        public void UpdateFrame()
        {
            if(handleRect==null || borderColliders==null || borderColliders.Length!=4)return;
            Vector2 size=handleRect.rect.size;if(size==lastFrameSize)return;lastFrameSize=size;
            const float band=10,depth=30;
            borderColliders[0].center=new Vector3(0,(size.y-band)*.5f,0);borderColliders[0].size=new Vector3(size.x,band,depth);
            borderColliders[1].center=-borderColliders[0].center;borderColliders[1].size=borderColliders[0].size;
            borderColliders[2].center=new Vector3((size.x-band)*.5f,0,0);borderColliders[2].size=new Vector3(band,size.y-2*band,depth);
            borderColliders[3].center=-borderColliders[2].center;borderColliders[3].size=borderColliders[2].size;
        }
        void LateUpdate()
        {
            UpdateFrame();AdvanceDrag(Time.unscaledDeltaTime);
            if(borderGraphic!=null)borderGraphic.color=State==Ownership.NavigationDrag?new Color(.86f,.70f,1,1):navigationHandle.isHovered?new Color(.78f,.61f,1,.95f):new Color(.61f,.43f,.92f,.85f);
        }
        void OnApplicationPause(bool paused){if(paused)CancelOwnedGesture();}
        void OnApplicationFocus(bool focused){if(!focused)CancelOwnedGesture();}
    }
}
