using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // Sole owner of menu width, opacity and sibling positions. No LayoutGroup or scale animation.
    public sealed class RemixNavigationToolbar : MonoBehaviour
    {
        public enum Menu { None, Speed, Zoom }
        public RemixTableManager manager;
        public RectTransform content,background,zoomRoot;
        public RemixSpeedSelector speed;
        public TimelineZoomUI zoom;
        public float openingSeconds=.28f,closingSeconds=.25f;
        public const float ButtonStep=56,MenuWidth=230,MenuGap=6,CollapsedWidth=230,LeftEdge=-59;
        sealed class Motion
        {
            public float value,start,elapsed;public bool target;
            public void Set(bool next) { if(next==target) return;start=value;elapsed=0;target=next; }
            public void Tick(float dt,float opening,float closing)
            {
                elapsed+=Mathf.Max(0,dt);float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,target?opening:closing));
                float ease=target?1-Mathf.Pow(1-t,3):t*t*(3-2*t);
                value=Mathf.Lerp(start,target?1:0,ease);
            }
            public void Reset() {value=start=elapsed=0;target=false;}
        }
        readonly Motion speedMotion=new Motion(),zoomMotion=new Motion();
        public Menu ActiveMenu { get; private set; }
        public float SpeedReveal => speedMotion.value;
        public float ZoomReveal => zoomMotion.value;
        public bool MenusVisible => ActiveMenu!=Menu.None || speedMotion.value>0 || zoomMotion.value>0;
        public float RenderedWidth => CollapsedWidth+(MenuWidth+MenuGap)*(SpeedReveal+ZoomReveal);
        public void Toggle(Menu menu) => SetMenu(menu,ActiveMenu!=menu);
        public void SetMenu(Menu menu,bool open)
        {
            if(open)
            {
                ActiveMenu=menu;
                var gestures=manager!=null?manager.interactions:null;
                if(gestures!=null && (gestures.State==RibbonInteractionManager.GestureState.ZoomPending || gestures.State==RibbonInteractionManager.GestureState.Zooming)) gestures.Cancel();
                speedMotion.Set(menu==Menu.Speed);zoomMotion.Set(menu==Menu.Zoom);
            }
            else
            {
                if(ActiveMenu==menu) ActiveMenu=Menu.None;
                if(menu==Menu.Speed) speedMotion.Set(false);
                if(menu==Menu.Zoom) zoomMotion.Set(false);
            }
            ApplyLayout(); // Target changes input immediately; rendered positions remain continuous.
        }
        void OnEnable() => ResetClosed();
        void OnDisable() => ResetClosed();
        void Update() => AdvanceAnimation(Time.unscaledDeltaTime);
        public void ResetClosed()
        {
            speedMotion.Reset();zoomMotion.Reset();ActiveMenu=Menu.None;ApplyLayout();
        }
        public void AdvanceAnimation(float dt)
        {
            speedMotion.Tick(dt,openingSeconds,closingSeconds);zoomMotion.Tick(dt,openingSeconds,closingSeconds);ApplyLayout();
        }
        void ApplyLayout()
        {
            if(content==null || background==null || zoomRoot==null || speed==null || zoom==null) return;
            float width=RenderedWidth;
            float half=manager!=null?manager.design.panelWidth*500:370;
            // Preserve the centered collapsed position, shifting the group only if a transition
            // would exceed seated bounds. Use rendered widths, not destination widths.
            float min=-half-LeftEdge,max=half-(LeftEdge+width);
            float offset=min<=max?Mathf.Clamp(-56,min,max):-(LeftEdge+width*.5f);
            content.anchoredPosition=new Vector2(offset,0);
            background.anchoredPosition=new Vector2(LeftEdge,0);background.sizeDelta=new Vector2(width,52);
            zoomRoot.anchoredPosition=new Vector2(140+(MenuWidth+MenuGap)*SpeedReveal,0);
            ApplyMenu(speed.optionsPanel,speed.optionsGroup,speedMotion,98);
            ApplyMenu(zoom.options!=null?(RectTransform)zoom.options.transform:null,zoom.optionsGroup,zoomMotion,52);
        }
        static void ApplyMenu(RectTransform viewport,CanvasGroup group,Motion motion,float height)
        {
            if(viewport==null || group==null) return;
            viewport.anchoredPosition=new Vector2(31,0);
            viewport.sizeDelta=new Vector2(MenuWidth*motion.value,height);
            group.alpha=motion.value;
            group.blocksRaycasts=group.interactable=motion.target && motion.value>=1;
            viewport.gameObject.SetActive(motion.target || motion.value>0);
        }
    }
}
