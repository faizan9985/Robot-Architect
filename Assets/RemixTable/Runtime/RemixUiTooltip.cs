using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // A single bubble reparented within existing world-space canvases. Never a raycast or gesture target.
    public sealed class RemixUiTooltip : MonoBehaviour
    {
        public const float Delay=.60f;
        public RemixUiPresentation presentation;
        public TMP_Text label;
        public CanvasGroup group;
        public RemixIconButton Owner{get;private set;}
        public bool Visible=>group!=null && group.alpha>0;
        float elapsed;
        public void Initialize(RemixUiPresentation owner,TMP_FontAsset font)
        {
            presentation=owner;group=GetComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
            gameObject.layer=transform.parent.gameObject.layer;
            var rect=(RectTransform)transform;rect.sizeDelta=new Vector2(240,34);
            var image=GetComponent<Image>();image.sprite=RemixUiPresentation.Sprite("Surface");image.type=Image.Type.Sliced;image.color=new Color(.035f,.025f,.075f,.98f);image.raycastTarget=false;
            label=owner.ChildText(transform,"Tooltip name",font);RemixUiPresentation.Stretch(label.rectTransform);label.fontSize=14;label.raycastTarget=false;
            Clear();
        }
        public void Request(RemixIconButton owner)
        {
            if(owner==null || !owner.button.IsInteractable())return;
            var canvas=owner.GetComponentInParent<Canvas>();if(canvas==null)return;
            // Workspace hints occupy a fixed help strip above its topmost controls, clear of all ribbons.
            if(canvas.transform.IsChildOf(presentation.manager.workspace))
            {
                var tools=presentation.manager.workspace.Find("ToolsCanvas");if(tools!=null)canvas=tools.GetComponent<Canvas>();
            }
            transform.SetParent(canvas.transform,false);transform.SetAsLastSibling();
            gameObject.layer=canvas.gameObject.layer;
            var rect=(RectTransform)transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.localScale=Vector3.one;
            var bounds=((RectTransform)canvas.transform).rect;
            bool above=presentation.manager.workspace.InverseTransformPoint(canvas.transform.position).y>0;
            rect.anchoredPosition=new Vector2(bounds.center.x,above?bounds.yMax+24:bounds.yMin-24);
            group.alpha=0;Owner=owner;elapsed=0;
        }
        public void Dismiss(RemixIconButton owner){if(Owner==owner)Clear();}
        public void Clear(){Owner=null;elapsed=0;if(group!=null)group.alpha=0;}
        public void Advance(float dt)
        {
            if(Owner==null || !Owner.isActiveAndEnabled || !Owner.button.IsInteractable()){Clear();return;}
            elapsed+=Mathf.Max(0,dt);if(elapsed<Delay)return;
            label.text=Owner.semanticName;group.alpha=1;
        }
        void Update()=>Advance(Time.unscaledDeltaTime);
        void OnDisable()=>Clear();
        void OnApplicationFocus(bool focused){if(!focused)Clear();}
        void OnApplicationPause(bool paused){if(paused)Clear();}
    }
}
