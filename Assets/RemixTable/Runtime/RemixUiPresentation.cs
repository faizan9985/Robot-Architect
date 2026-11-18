using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RemixTable
{
    // Presentation only. Never edits listeners, RectTransform hit targets, gestures, or audio state.
    [DefaultExecutionOrder(500),DisallowMultipleComponent]
    public sealed class RemixUiPresentation : MonoBehaviour
    {
        public RemixTableManager manager;
        public RemixUiTooltip tooltip;
        public RemixIconButton[] controls;
        public RemixToolbox toolbox;
        static Dictionary<string,Sprite> atlas;
        public static readonly Color Surface=new Color(.025f,.022f,.052f,.92f),Panel=new Color(.015f,.014f,.032f,.91f),Violet=new Color(.70f,.60f,1,1),Ink=new Color(.84f,.86f,.96f,1);
        public static readonly Color SelectedSurface=new Color(.18f,.10f,.31f,.95f);
        public static Sprite Sprite(string key)
        {
            if(atlas==null){atlas=new Dictionary<string,Sprite>();foreach(var s in Resources.LoadAll<Sprite>("RemixUiAtlas"))atlas[s.name]=s;}
            if(!atlas.TryGetValue(key,out var sprite))throw new InvalidOperationException("UI atlas sprite missing: "+key);
            return sprite;
        }
        void Awake()=>Initialize();
        public void Initialize()
        {
            if(manager==null)manager=GetComponent<RemixTableManager>();
            var transport=manager.workspace.GetComponentInChildren<RemixTransport>(true);
            toolbox=manager.workspace.GetComponentInChildren<RemixToolbox>(true);
            var mode=manager.workspace.GetComponentInChildren<RemixModeSelector>(true);if(mode!=null){mode.idleColor=Surface;mode.selectedColor=SelectedSurface;}
            var playback=manager.workspace.GetComponentInChildren<RemixPlaybackModeSelector>(true);if(playback!=null){playback.idleColor=Surface;playback.selectedColor=SelectedSurface;}
            var mix=manager.workspace.GetComponentInChildren<RemixMixControls>(true);if(mix!=null){mix.idleColor=Surface;mix.muteColor=mix.soloColor=SelectedSurface;}
            if(toolbox!=null){toolbox.idleColor=Surface;toolbox.selectedColor=SelectedSurface;}
            if(manager.speedSelector!=null){manager.speedSelector.idleColor=Surface;manager.speedSelector.selectedColor=SelectedSurface;}
            var zoom=transport.GetComponent<TimelineZoomUI>();if(zoom!=null){zoom.idleColor=Surface;zoom.selectedColor=SelectedSurface;}
            if(tooltip==null)
            {
                var go=new GameObject("Context Tooltip",typeof(RectTransform),typeof(Image),typeof(CanvasGroup),typeof(RemixUiTooltip));
                go.transform.SetParent(transport.transform,false);tooltip=go.GetComponent<RemixUiTooltip>();
            }
            tooltip.Initialize(this,transport.songTitle.font);
            var buttons=new List<RemixIconButton>();
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var b in root.GetComponentsInChildren<Button>(true))
            {
                var view=b.GetComponent<RemixIconButton>();if(view==null)view=b.gameObject.AddComponent<RemixIconButton>();
                view.Initialize(this,b);buttons.Add(view);
            }
            controls=buttons.ToArray();
            foreach(var slider in manager.workspace.GetComponentsInChildren<Slider>(true))
            {
                if(slider.fillRect!=null && slider.fillRect.TryGetComponent<Image>(out var fill))fill.color=Violet;
                if(slider.handleRect!=null && slider.handleRect.TryGetComponent<Image>(out var handle)){handle.color=Ink;handle.sprite=Sprite("Surface");handle.type=Image.Type.Sliced;handle.pixelsPerUnitMultiplier=2.5f;}
            }
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var image in root.GetComponentsInChildren<Image>(true))
            {
                if(image.GetComponent<Button>()!=null || image.GetComponentInParent<RemixIconButton>()!=null || image.GetComponentInParent<RemixUiTooltip>()!=null)continue;
                if(image.GetComponent<Canvas>()!=null || image.name=="Panel" || image.name=="Content" || image.name=="Background")
                {
                    image.sprite=Sprite("Surface");image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=2.5f;image.color=Panel;
                    // Existing toggle/slider graphics retain their raycast flags; panel surfaces were already non-targets.
                    var edge=ChildImage(image.transform,"Presentation Border",Sprite("Border"));Stretch(edge.rectTransform);edge.color=new Color(.55f,.40f,.85f,.22f);edge.enabled=image.enabled;
                }
            }
        }
        public static Image ChildImage(Transform parent,string name,Sprite sprite)
        {
            var child=parent.Find(name);if(child==null){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);child=go.transform;}
            child.gameObject.layer=parent.gameObject.layer;var image=child.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.preserveAspect=false;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=2.5f;return image;
        }
        public static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        public TMP_Text ChildText(Transform parent,string name,TMP_FontAsset font)
        {
            var child=parent.Find(name);if(child==null){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);child=go.transform;}
            child.gameObject.layer=parent.gameObject.layer;var text=child.GetComponent<TMP_Text>();text.font=font;text.fontSize=14;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.color=Ink;return text;
        }
    }
}
