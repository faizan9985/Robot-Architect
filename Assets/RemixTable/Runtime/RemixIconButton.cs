using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace RemixTable
{
    [DefaultExecutionOrder(600),DisallowMultipleComponent]
    public sealed class RemixIconButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,IPointerClickHandler,ISelectHandler,IDeselectHandler,ISubmitHandler
    {
        public RemixUiPresentation presentation;
        public Button button;
        public TMP_Text sourceLabel,caption;
        public Image icon,outline,selectedMark;
        public string semanticName,iconKey;
        public bool numeric,keepCaption;
        public StemId stem;
        public string role;
        bool hovered,focused,pressed;
        float pressedUntil;
        string shownIcon,shownCaption;
        string recordSource,recordCaption="";
        int presetIndex=-1;
        public bool Selected{get;private set;}
        public void Initialize(RemixUiPresentation owner,Button target)
        {
            presentation=owner;button=target;role=target.name;
            if(sourceLabel==null)
            {
                var label=target.transform.Find("Label");if(label!=null)sourceLabel=label.GetComponent<TMP_Text>();
                if(sourceLabel==null)foreach(var text in target.GetComponentsInChildren<TMP_Text>(true))if(text.name!="Presentation Caption"){sourceLabel=text;break;}
            }
            semanticName=role;iconKey="Help";numeric=role.StartsWith("Preset") || role=="Speed" || role=="Zoom" || role=="Detail" || role=="Normal" || role=="Wide" || role=="Overview";
            keepCaption=role=="EditMode" || role=="LiveMode" || role=="ResetTool";
            if(role.StartsWith("Preset"))int.TryParse(role.Substring(6),out presetIndex);
            if(numeric && role!="Speed" && role!="Zoom" && sourceLabel!=null)semanticName=sourceLabel.text+(presetIndex>=0?" speed":" timeline");
            switch(role)
            {
                case "PreviousSong":iconKey="Previous";semanticName="Previous song";break;
                case "NextSong":iconKey="Next";semanticName="Next song";break;
                case "PlayPause":iconKey="Pause";semanticName="Pause";break;
                case "VolumeMode":iconKey="Volume";semanticName="Gain";break;
                case "FilterMode":iconKey="Filter";semanticName="Filter";break;
                case "PitchMode":iconKey="Pitch";semanticName="Pitch";break;
                case "EditMode":iconKey="Edit";semanticName="Edit: pause while sculpting";break;
                case "LiveMode":iconKey="Live";semanticName="Live: play while sculpting";break;
                case "SnapTool":iconKey="Snap";semanticName="Beat Snap";break;
                case "LoopTool":iconKey="Loop";semanticName="Loop region";break;
                case "SpatialTool":iconKey="Spatial";semanticName="Spatial audio";break;
                case "RecordTool":iconKey="Record";semanticName="Record";break;
                case "RoomTool":iconKey="Room";semanticName="Passthrough";break;
                case "MicTool":iconKey="Voice";semanticName="Voice";break;
                case "ResetTool":iconKey="Reset";semanticName="Reset All (Undo restores)";break;
                case "RecenterTool":iconKey="Recenter";semanticName="Recenter / Drag to move";break;
                case "MasterVolumeTool":iconKey="Volume";semanticName="Master Volume / Help";break;
                case "Undo":case "Redo":iconKey=role;break;
                case "Speed":semanticName="Playback speed";break;
                case "Zoom":semanticName="Timeline zoom";break;
            }
            foreach(StemId id in new[]{StemId.Vocals,StemId.Drums,StemId.Bass,StemId.Melody})
            {
                string key=id==StemId.Melody?"Other":id.ToString();
                if(role==key+"Mute" || role==key+"Solo" || role==key+"Clear")
                {stem=id;iconKey=role.EndsWith("Mute")?"Mute":role.EndsWith("Solo")?"Solo":"Clear";semanticName=iconKey+" "+key;}
            }
            var background=button.targetGraphic as Image;
            if(background!=null){background.sprite=RemixUiPresentation.Sprite("Surface");background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=2.5f;background.color=RemixUiPresentation.Surface;}
            button.transition=Selectable.Transition.None; // This component supplies all visual states; events remain untouched.
            outline=RemixUiPresentation.ChildImage(transform,"Presentation Outline",RemixUiPresentation.Sprite("Border"));RemixUiPresentation.Stretch(outline.rectTransform);
            selectedMark=RemixUiPresentation.ChildImage(transform,"Presentation Selected",RemixUiPresentation.Sprite("Check"));selectedMark.type=Image.Type.Simple;
            selectedMark.rectTransform.anchorMin=selectedMark.rectTransform.anchorMax=new Vector2(1,0);selectedMark.rectTransform.pivot=new Vector2(1,0);selectedMark.rectTransform.anchoredPosition=new Vector2(-4,3);selectedMark.rectTransform.sizeDelta=new Vector2(10,10);
            if(!numeric)
            {
                var existing=transform.Find("Icon");icon=existing!=null?existing.GetComponent<Image>():RemixUiPresentation.ChildImage(transform,"Presentation Icon",RemixUiPresentation.Sprite(iconKey));
                icon.type=Image.Type.Simple;icon.sprite=RemixUiPresentation.Sprite(iconKey);icon.preserveAspect=true;icon.raycastTarget=false;
                icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,.5f);icon.rectTransform.pivot=new Vector2(.5f,.5f);icon.rectTransform.sizeDelta=new Vector2(25,25);icon.rectTransform.anchoredPosition=keepCaption?new Vector2(-28,0):Vector2.zero;
                if(sourceLabel!=null)sourceLabel.enabled=false;
                if(keepCaption || role=="RecordTool")
                {
                    var font=sourceLabel!=null?sourceLabel.font:presentation.manager.workspace.GetComponentInChildren<RemixTransport>(true).songTitle.font;
                    caption=presentation.ChildText(transform,"Presentation Caption",font);caption.rectTransform.sizeDelta=new Vector2(100,32);caption.rectTransform.anchoredPosition=new Vector2(14,0);
                    if(role=="RecordTool" || role=="ResetTool")caption.rectTransform.sizeDelta=new Vector2(48,32);
                    if(role=="RecordTool")caption.fontSize=12;
                }
            }
            else if(sourceLabel!=null)sourceLabel.enabled=true;
            Paint();
        }
        bool Active()
        {
            var m=presentation.manager;var audio=m.audioManager;var toolbox=presentation.toolbox;
            if(role.EndsWith("Mute"))return m.mix.muted[(int)stem];if(role.EndsWith("Solo"))return m.mix.soloed[(int)stem];
            switch(role)
            {
                case "VolumeMode":return m.interactions.Mode==RibbonInteractionManager.ControlMode.Volume;
                case "FilterMode":return m.interactions.Mode==RibbonInteractionManager.ControlMode.Filter;
                case "PitchMode":return m.interactions.Mode==RibbonInteractionManager.ControlMode.Pitch;
                case "EditMode":return audio!=null && !audio.LiveMode;
                case "LiveMode":return audio!=null && audio.LiveMode;
                case "SnapTool":return m.snapToBeat;
                case "LoopTool":return audio!=null && audio.LoopActive;
                case "SpatialTool":return m.mix.spatial;
                case "MasterVolumeTool":return m.workspaceInteraction!=null && m.workspaceInteraction.PanelVisible;
                case "RecordTool":return toolbox!=null && toolbox.recorder!=null && toolbox.recorder.Recording;
                case "RoomTool":return toolbox!=null && toolbox.passthrough is IRemixToggle p && p.IsOn;
                case "MicTool":return toolbox!=null && toolbox.voice is IRemixToggle v && v.IsOn;
                case "Speed":return m.navigationToolbar.ActiveMenu==RemixNavigationToolbar.Menu.Speed;
                case "Zoom":return m.navigationToolbar.ActiveMenu==RemixNavigationToolbar.Menu.Zoom;
            }
            if(presetIndex>=0)return audio!=null && Mathf.Approximately(audio.PlaybackSpeed,RemixAudioManager.SpeedPreset(presetIndex));
            float preset=role=="Detail"?4:role=="Normal"?8:role=="Wide"?16:role=="Overview"?32:0;
            return preset>0 && Mathf.Abs(m.zoom.VisibleSeconds-preset)<.025f;
        }
        void LateUpdate()=>Paint();
        public void Paint()
        {
            if(presentation==null || button==null)return;
            Selected=Active();bool enabled=button.IsInteractable();bool down=pressed || Time.unscaledTime<pressedUntil;
            Color surface=!enabled?new Color(.035f,.03f,.05f,.60f):down?new Color(.24f,.17f,.36f,.96f):Selected?RemixUiPresentation.SelectedSurface:hovered || focused?new Color(.10f,.075f,.17f,.95f):RemixUiPresentation.Surface;
            bool contrast=presentation.manager.accessibility!=null && presentation.manager.accessibility.HighContrast;
            if(contrast)surface=Selected?presentation.manager.accessibility.contrastSelected:Color.black;
            if(button.targetGraphic!=null)button.targetGraphic.color=surface;
            if(outline!=null)outline.color=new Color(.70f,.55f,1,!enabled?.09f:down?1:Selected?.75f:hovered || focused?.6f:.22f);
            if(contrast && outline!=null)outline.color=enabled?Color.white:new Color(1,1,1,.4f);
            if(selectedMark!=null){selectedMark.enabled=Selected;selectedMark.color=enabled?RemixUiPresentation.Violet:new Color(.5f,.5f,.6f,.3f);}
            if(numeric && sourceLabel!=null)sourceLabel.color=enabled?RemixUiPresentation.Ink:new Color(.60f,.60f,.68f,.30f);
            string key=iconKey;
            if(role=="PlayPause"){bool paused=presentation.manager.audioManager!=null && presentation.manager.audioManager.IsManuallyPaused;key=paused?"Play":"Pause";semanticName=paused?"Play":"Pause";}
            if(role=="RecordTool"){key=Selected?"Stop":"Record";semanticName=Selected?"Stop recording":"Record";}
            if(icon!=null)
            {
                if(shownIcon!=key){icon.sprite=RemixUiPresentation.Sprite(key);shownIcon=key;}
                icon.color=!enabled?new Color(.60f,.60f,.68f,.30f):role=="RecordTool"?new Color(1,.34f,.40f,1):Selected || hovered || focused?new Color(.91f,.84f,1,1):RemixUiPresentation.Ink;
            }
            if(caption!=null)
            {
                if(role=="RecordTool" && sourceLabel!=null && recordSource!=sourceLabel.text)
                {recordSource=sourceLabel.text;recordCaption=recordSource.StartsWith("STOP ")?recordSource.Substring(5):"";}
                string text=role=="EditMode"?"Edit":role=="LiveMode"?"Live":role=="ResetTool"?"Reset":Selected?recordCaption:"";
                if(shownCaption!=text){caption.text=text;shownCaption=text;}
                caption.enabled=text.Length>0;caption.color=enabled?RemixUiPresentation.Ink:new Color(.6f,.6f,.7f,.3f);
                if(role=="RecordTool" && icon!=null)icon.rectTransform.anchoredPosition=Selected?new Vector2(-30,0):Vector2.zero;
            }
        }
        void Enter(){if(presentation!=null && button.IsInteractable())presentation.tooltip.Request(this);}
        void Exit(){if(presentation!=null && presentation.tooltip!=null)presentation.tooltip.Dismiss(this);}
        public void OnPointerEnter(PointerEventData e){hovered=true;Enter();}
        public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;Exit();}
        public void OnPointerDown(PointerEventData e){pressed=true;Exit();}
        public void OnPointerUp(PointerEventData e){pressed=false;pressedUntil=Time.unscaledTime+.10f;}
        public void OnPointerClick(PointerEventData e)=>Exit();
        public void OnSelect(BaseEventData e){focused=true;Enter();}
        public void OnDeselect(BaseEventData e){focused=false;Exit();}
        public void OnSubmit(BaseEventData e){pressedUntil=Time.unscaledTime+.10f;Exit();}
        void OnDisable(){hovered=focused=pressed=false;pressedUntil=0;Exit();}
        void OnApplicationFocus(bool focused){if(!focused){hovered=this.focused=pressed=false;pressedUntil=0;Exit();}}
    }
}
