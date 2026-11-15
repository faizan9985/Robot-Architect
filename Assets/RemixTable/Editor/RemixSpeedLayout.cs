using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace RemixTable.Editor
{
    // Reuses the existing history canvas, speed selector and authoritative TimelineZoomUI adapter.
    public static class RemixSpeedLayout
    {
        public const float ButtonWidth=50,ButtonHeight=40,Pad=6;
        public static Vector2 PanelSize => new Vector2(230,98);
        [MenuItem("Tools/Remix Table/Apply Navigation Toolbar")]
        public static void Apply()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();
            Build(manager,manager.workspace.GetComponentInChildren<RemixHistoryControls>(),manager.workspace.GetComponentInChildren<RemixTransport>().songTitle.font);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            RemixZoomValidation.ValidateScene();
        }
        public static RemixSpeedSelector Build(RemixTableManager manager,RemixHistoryControls history,TMP_FontAsset font)
        {
            var transport=manager.workspace.GetComponentInChildren<RemixTransport>();
            var ui=transport.GetComponent<TimelineZoomUI>();if(ui==null) ui=transport.gameObject.AddComponent<TimelineZoomUI>();
            ui.manager=manager;
            var toolbar=history.GetComponent<RemixNavigationToolbar>();if(toolbar==null) toolbar=history.gameObject.AddComponent<RemixNavigationToolbar>();
            toolbar.manager=manager;manager.navigationToolbar=toolbar;
            var content=RemixSeatedLayout.Rect(history.transform,"NavigationContent",Vector2.zero,Vector2.zero);toolbar.content=content;
            foreach(var button in new[]{history.undoButton,history.redoButton})
            {
                var rect=(RectTransform)button.transform;var position=rect.anchoredPosition;
                rect.SetParent(content,false);rect.anchoredPosition=position;
            }
            var oldBackground=history.GetComponent<Image>();if(oldBackground!=null) oldBackground.enabled=false;
            var background=RemixSeatedLayout.Rect(content,"Background",Vector2.zero,new Vector2(230,52));
            background.pivot=new Vector2(0,.5f);Fill(background);background.SetAsFirstSibling();toolbar.background=background;
            var existing=history.GetComponentInChildren<RemixSpeedSelector>(true);
            if(existing!=null) existing.transform.SetParent(content,false);
            var root=RemixSeatedLayout.Rect(content,"SpeedSelector",new Vector2(84,0),new Vector2(50,40));
            var selector=root.GetComponent<RemixSpeedSelector>();if(selector==null) selector=root.gameObject.AddComponent<RemixSpeedSelector>();
            selector.manager=manager;selector.toolbar=toolbar;manager.speedSelector=selector;
            selector.speedButton=Button(root,"Speed","1\u00d7",font);
            selector.speedLabel=selector.speedButton.GetComponentInChildren<TMP_Text>();
            UnityEventTools.AddPersistentListener(selector.speedButton.onClick,selector.Toggle);
            selector.optionsPanel=Viewport(root,98,out var speedGroup,out var speedContent);
            selector.optionsGroup=speedGroup;selector.optionsContent=speedContent;
            selector.optionButtons=new Button[8];
            for(int i=0;i<8;i++)
            {
                // Reparent existing preset objects before rebuilding bindings; preserve their fileIDs.
                var old=selector.optionsPanel.Find("Preset"+i);if(old!=null) old.SetParent(speedContent,false);
                var button=Button(speedContent,"Preset"+i,RemixSpeedSelector.Label(RemixAudioManager.SpeedPreset(i)),font);
                Position(button,i%4,i<4?23:-23);
                UnityEventTools.AddIntPersistentListener(button.onClick,selector.SelectPreset,i);selector.optionButtons[i]=button;
            }
            var zoomRoot=RemixSeatedLayout.Rect(content,"ZoomSelector",new Vector2(140,0),new Vector2(50,40));toolbar.zoomRoot=zoomRoot;
            ui.toolbar=toolbar;ui.expand=Button(zoomRoot,"Zoom","8s",font);ui.buttonLabel=ui.expand.GetComponentInChildren<TMP_Text>();
            UnityEventTools.AddPersistentListener(ui.expand.onClick,ui.ToggleOptions);
            var zoomViewport=Viewport(zoomRoot,52,out var zoomGroup,out var zoomContent);
            ui.options=zoomViewport.gameObject;ui.optionsContent=zoomContent;ui.optionsGroup=zoomGroup;
            ui.detail=Button(zoomContent,"Detail","4s",font);UnityEventTools.AddPersistentListener(ui.detail.onClick,ui.Detail);
            ui.normal=Button(zoomContent,"Normal","8s",font);UnityEventTools.AddPersistentListener(ui.normal.onClick,ui.Normal);
            ui.wide=Button(zoomContent,"Wide","16s",font);UnityEventTools.AddPersistentListener(ui.wide.onClick,ui.Wide);
            ui.overview=Button(zoomContent,"Overview","32s",font);UnityEventTools.AddPersistentListener(ui.overview.onClick,ui.Overview);
            var zoomButtons=new[]{ui.detail,ui.normal,ui.wide,ui.overview};for(int i=0;i<4;i++) Position(zoomButtons[i],i,0);
            if(ui.workspaceRange==null) ui.workspaceRange=RemixSeatedLayout.Text(transport.transform,"TimelineZoomRange","8s visible",new Vector2(58,30),new Vector2(150,26),11,font);
            toolbar.speed=selector;toolbar.zoom=ui;
            RemovePalmZoom(manager,transport);
            toolbar.ResetClosed();selector.Refresh();ui.RefreshNow();
            foreach(var item in new Object[]{toolbar,selector,ui,manager}) EditorUtility.SetDirty(item);
            return selector;
        }
        static void RemovePalmZoom(RemixTableManager manager,RemixTransport transport)
        {
            var panel=manager.palmMenuCanvas.transform.Find("Panel");
            foreach(string name in new[]{"TimelineZoom","TimelineZoomOptions"})
            {
                var obsolete=panel.Find(name);if(obsolete!=null) Object.DestroyImmediate(obsolete.gameObject);
            }
            // Restore the original instruction area rather than leaving the removed section's gap.
            var guidance=(RectTransform)transport.palmInstructions.transform;
            guidance.anchoredPosition=new Vector2(0,-82);guidance.sizeDelta=new Vector2(316,142);
            transport.palmInstructions.text=RemixTransport.Guidance;transport.palmInstructions.fontSize=13;
            guidance.gameObject.SetActive(true);
        }
        static RectTransform Viewport(Transform owner,float height,out CanvasGroup group,out RectTransform content)
        {
            var viewport=RemixSeatedLayout.Rect(owner,"Options",new Vector2(31,0),new Vector2(0,height));viewport.pivot=new Vector2(0,.5f);
            var oldImage=viewport.GetComponent<Image>();if(oldImage!=null) oldImage.enabled=false;
            if(viewport.GetComponent<RectMask2D>()==null) viewport.gameObject.AddComponent<RectMask2D>();
            group=viewport.GetComponent<CanvasGroup>();if(group==null) group=viewport.gameObject.AddComponent<CanvasGroup>();
            content=RemixSeatedLayout.Rect(viewport,"Content",Vector2.zero,new Vector2(230,height));
            content.anchorMin=content.anchorMax=new Vector2(0,.5f);content.pivot=new Vector2(0,.5f);content.anchoredPosition=Vector2.zero;Fill(content);
            return viewport;
        }
        static void Fill(RectTransform rect)
        {
            var image=rect.GetComponent<Image>();if(image==null) image=rect.gameObject.AddComponent<Image>();
            image.color=new Color(.018f,.026f,.04f,1);image.raycastTarget=false;
        }
        static void Position(Button button,int column,float y)
        {
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);
            rect.anchoredPosition=new Vector2(31+column*56,y);
        }
        static Button Button(Transform parent,string name,string label,TMP_FontAsset font)
        {
            var rect=RemixSeatedLayout.Rect(parent,name,Vector2.zero,new Vector2(50,40));
            var image=rect.GetComponent<Image>();if(image==null) image=rect.gameObject.AddComponent<Image>();
            image.color=new Color(.07f,.11f,.15f,1);image.raycastTarget=true;
            var button=rect.GetComponent<Button>();if(button==null) button=rect.gameObject.AddComponent<Button>();
            button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.highlightedColor=new Color(.65f,.85f,1);colors.pressedColor=new Color(.35f,.65f,.85f);button.colors=colors;
            while(button.onClick.GetPersistentEventCount()>0) UnityEventTools.RemovePersistentListener(button.onClick,0);
            RemixSeatedLayout.Text(rect,"Label",label,Vector2.zero,new Vector2(42,34),15,font);return button;
        }
        public static void Validate()
        {
            var selector=Object.FindFirstObjectByType<RemixSpeedSelector>(FindObjectsInactive.Include);
            RemixSceneBuilder.Require(selector!=null && selector.manager.speedSelector==selector && selector.optionButtons.Length==8,"speed selector and eight presets");
            var toolbar=selector.toolbar;var ui=toolbar.zoom;var manager=selector.manager;
            RemixSceneBuilder.Require(toolbar==manager.navigationToolbar && toolbar.GetComponent<Canvas>()==selector.GetComponentInParent<Canvas>(),"one existing navigation canvas");
            RemixSceneBuilder.Require(toolbar.openingSeconds==.28f && toolbar.closingSeconds==.25f,"toolbar animation timing");
            RemixSceneBuilder.Require(toolbar.content.GetComponentsInChildren<LayoutGroup>(true).Length==0 && toolbar.content.GetComponentsInChildren<ContentSizeFitter>(true).Length==0,"explicit animated positions");
            RemixSceneBuilder.Require(selector.optionsPanel.GetComponent<RectMask2D>()!=null && ui.options.GetComponent<RectMask2D>()!=null,"inline reveal masks");
            RemixSceneBuilder.Require(selector.optionsContent.sizeDelta==PanelSize && ui.optionsContent.sizeDelta==new Vector2(230,52),"fixed full-size menu content");
            var speedButtons=selector.optionButtons;
            for(int i=0;i<8;i++) Binding(speedButtons[i],selector,nameof(RemixSpeedSelector.SelectPreset));
            Binding(selector.speedButton,selector,nameof(RemixSpeedSelector.Toggle));
            var zoomButtons=new[]{ui.detail,ui.normal,ui.wide,ui.overview};var methods=new[]{nameof(TimelineZoomUI.Detail),nameof(TimelineZoomUI.Normal),nameof(TimelineZoomUI.Wide),nameof(TimelineZoomUI.Overview)};
            Binding(ui.expand,ui,nameof(TimelineZoomUI.ToggleOptions));for(int i=0;i<4;i++) Binding(zoomButtons[i],ui,methods[i]);
            var panel=manager.palmMenuCanvas.transform.Find("Panel");
            RemixSceneBuilder.Require(panel.Find("TimelineZoom")==null && panel.Find("TimelineZoomOptions")==null,"obsolete palm zoom UI removed");
            float y=toolbar.transform.localPosition.y;
            try
            {
                foreach(var menu in new[]{RemixNavigationToolbar.Menu.Speed,RemixNavigationToolbar.Menu.Zoom})
                {
                    toolbar.SetMenu(menu,true);toolbar.AdvanceAnimation(1);
                    var corners=new Vector3[4];toolbar.background.GetWorldCorners(corners);
                    RemixSceneBuilder.Require(corners.All(p=>Mathf.Abs(manager.workspace.InverseTransformPoint(p).x)<=manager.design.panelWidth*.5f+.0001f),"toolbar inside seated width");
                    var viewport=menu==RemixNavigationToolbar.Menu.Speed?selector.optionsPanel:(RectTransform)ui.options.transform;
                    viewport.GetWorldCorners(corners);
                    RemixSceneBuilder.Require(corners.Min(p=>manager.workspace.InverseTransformPoint(p).y)>1.5f*manager.design.spacing+manager.design.laneHeight*.5f,"inline options above ribbon geometry");
                    RemixSceneBuilder.Require(toolbar.transform.localPosition.y==y && toolbar.content.anchoredPosition.y==0,"no vertical toolbar animation");
                }
            }
            finally { toolbar.ResetClosed(); }
            Debug.Log("REMIX_NAVIGATION_VALIDATION_PASS");
        }
        static void Binding(Button button,Object target,string method)
        {
            RemixSceneBuilder.Require(button!=null && ((RectTransform)button.transform).sizeDelta==new Vector2(50,40),"fixed navigation touch target");
            RemixSceneBuilder.Require(button.onClick.GetPersistentEventCount()==1 && button.onClick.GetPersistentTarget(0)==target && button.onClick.GetPersistentMethodName(0)==method,"toolbar binding "+method);
        }
    }
}
