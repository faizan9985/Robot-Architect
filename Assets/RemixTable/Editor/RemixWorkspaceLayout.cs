using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object=UnityEngine.Object;
namespace RemixTable.Editor
{
    public static class RemixWorkspaceLayout
    {
        public static void Apply()
        {
            RemixUiLayout.ImportAtlas();EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            Configure(Object.FindFirstObjectByType<RemixTableManager>());
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Validate();
        }
        public static void Configure(RemixTableManager manager)
        {
            var transport=manager.workspace.GetComponentInChildren<RemixTransport>(true);
            var controller=manager.GetComponent<RemixWorkspaceInteraction>();if(controller==null)controller=manager.gameObject.AddComponent<RemixWorkspaceInteraction>();
            controller.manager=manager;manager.workspaceInteraction=controller;controller.waveformRoot=manager.ribbons[0].transform.parent;
            controller.playhead=manager.workspace.GetComponentsInChildren<LineRenderer>(true).Single(l=>l.name=="FixedPlaybackIndicator");
            controller.playhead.transform.SetParent(controller.waveformRoot,true);
            var tools=manager.workspace.GetComponentInChildren<RemixToolbox>(true);var font=transport.songTitle.font;
            var panel=RemixSeatedLayout.Rect(tools.transform,"Master Volume Panel",new Vector2(96,180),new Vector2(360,260));controller.masterVolumePanel=panel.gameObject;
            var bg=panel.GetComponent<Image>();if(bg==null)bg=panel.gameObject.AddComponent<Image>();bg.sprite=RemixUiPresentation.Sprite("Surface");bg.type=Image.Type.Sliced;bg.raycastTarget=false;bg.color=RemixUiPresentation.Panel;
            Slider slider=panel.GetComponentInChildren<Slider>(true);TMP_Text volumeLabel=null;
            foreach(var menu in RemixSceneBuilder.All<HandMenuController>(manager.gameObject.scene))
            {
                var data=new SerializedObject(menu);var old=(PrototypeVolumeControl)data.FindProperty("volumeControl").objectReferenceValue;
                if(old!=null)
                {
                    var fields=new SerializedObject(old);slider=(Slider)fields.FindProperty("slider").objectReferenceValue;volumeLabel=(TMP_Text)fields.FindProperty("volumeLabel").objectReferenceValue;
                    slider.transform.SetParent(panel,false);volumeLabel.transform.SetParent(panel,false);
                    Object.DestroyImmediate(old);
                }
                var launcher=(Canvas)data.FindProperty("launcherCanvas").objectReferenceValue;var palm=(Canvas)data.FindProperty("menuCanvas").objectReferenceValue;
                var services=menu.gameObject;Object.DestroyImmediate(menu);
                // The old menu root also owns the shared passthrough controller. Keep its
                // serialized camera and Tools references while retiring all palm activation.
                if(services.name=="Hand Menu Root"){services.name="Workspace Services";services.transform.SetParent(manager.transform,true);}
                if(launcher!=null)Object.DestroyImmediate(launcher.gameObject);if(palm!=null)Object.DestroyImmediate(palm.gameObject);
            }
            if(slider==null)throw new InvalidOperationException("Master volume must be migrated, never dropped");
            var volume=panel.GetComponent<PrototypeVolumeControl>();if(volume==null)volume=panel.gameObject.AddComponent<PrototypeVolumeControl>();
            if(volumeLabel==null)volumeLabel=panel.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Volume Label");
            var volumeFields=new SerializedObject(volume);volumeFields.FindProperty("slider").objectReferenceValue=slider;volumeFields.FindProperty("volumeLabel").objectReferenceValue=volumeLabel;volumeFields.ApplyModifiedPropertiesWithoutUndo();
            ((RectTransform)slider.transform).anchoredPosition=new Vector2(0,70);((RectTransform)volumeLabel.transform).anchoredPosition=new Vector2(0,108);
            controller.masterVolumeSlider=slider;
            transport.workspaceInstructions=RemixSeatedLayout.Text(panel,"Workspace Instructions",RemixTransport.WorkspaceGuidance,new Vector2(0,-35),new Vector2(320,150),12,font);
            transport.workspaceInstructions.alignment=TextAlignmentOptions.Left;controller.instructions=transport.workspaceInstructions;
            panel.gameObject.SetActive(false);
            var recenter=RemixSeatedLayout.Button(tools.transform,"RecenterTool","",414,80,font,manager.RecenterWorkspace);((RectTransform)recenter.transform).anchoredPosition=new Vector2(414,0);
            var move=recenter.GetComponent<RecenterWorkspaceButton>();if(move==null)move=recenter.gameObject.AddComponent<RecenterWorkspaceButton>();move.workspace=controller;move.button=recenter;controller.recenterControl=move;
            recenter.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(recenter.onClick,move.Click);
            var master=RemixSeatedLayout.Button(tools.transform,"MasterVolumeTool","",502,80,font,controller.ToggleMasterVolume);((RectTransform)master.transform).anchoredPosition=new Vector2(502,0);
            var oldBg=tools.GetComponent<Image>();if(oldBg!=null)oldBg.enabled=false;
            var backdrop=RemixUiPresentation.ChildImage(tools.transform,"Workspace Tools Background",RemixUiPresentation.Sprite("Surface"));backdrop.rectTransform.sizeDelta=new Vector2(932,55);backdrop.rectTransform.anchoredPosition=new Vector2(96,0);backdrop.color=RemixUiPresentation.Panel;backdrop.transform.SetAsFirstSibling();
            var oldHandle=manager.navigationToolbar.background.Find("Grab Border");if(oldHandle!=null)Object.DestroyImmediate(oldHandle.gameObject);
            var spatial=RemixSceneBuilder.All<SpatialStemField>(manager.gameObject.scene).Single();spatial.transform.SetParent(manager.workspace,true);
            controller.uiCanvases=manager.workspace.GetComponentsInChildren<Canvas>(true);controller.Initialize();
            manager.GetComponent<RemixUiPresentation>().Initialize();
            EditorUtility.SetDirty(manager);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(transport);
        }
        public static void Validate()
        {
            var manager=Object.FindFirstObjectByType<RemixTableManager>();var c=manager.workspaceInteraction;
            if(c==null || c.recenterControl==null || c.recenterControl.button==null || c.masterVolumeSlider==null)throw new InvalidOperationException("Workspace references missing");
            if(manager.navigationToolbar.background.Find("Grab Border")!=null || manager.navigationToolbar.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Navigation grab border must be removed");
            if(RemixSceneBuilder.All<HandMenuController>(manager.gameObject.scene).Length!=0)throw new InvalidOperationException("Palm activation must be removed");
            var pass=RemixSceneBuilder.All<PassthroughController>(manager.gameObject.scene).Single();
            var room=RemixSceneBuilder.All<RemixPassthroughToggle>(manager.gameObject.scene).Single();
            if(new SerializedObject(room).FindProperty("passthrough").objectReferenceValue!=pass)throw new InvalidOperationException("Tools passthrough controller must survive palm removal");
            if(!c.playhead.transform.IsChildOf(c.waveformRoot) || manager.ribbons.Any(r=>r.transform.parent!=c.waveformRoot))throw new InvalidOperationException("Vertical geometry root invalid");
            if(c.uiCanvases.Length!=6 || c.uiCanvases.Any(canvas=>canvas.transform.IsChildOf(c.waveformRoot)))throw new InvalidOperationException("UI must stay unscaled");
            if(!RemixSceneBuilder.All<SpatialStemField>(manager.gameObject.scene).Single().transform.IsChildOf(manager.workspace))throw new InvalidOperationException("Spatial field must move with root");
            RemixUiLayout.Validate();Debug.Log("WORKSPACE_INTERACTION_VALIDATION_PASS");
        }
    }
}
