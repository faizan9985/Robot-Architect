using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
namespace RemixTable.Editor
{
    public static class RemixSeatedLayout
    {
        static Transform Child(Transform parent,string name)
        {
            var existing=parent.Find(name);if(existing!=null)return existing;
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        static Material Surface(string name,Color color)
        {
            string path="Assets/RemixTable/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);EditorUtility.SetDirty(mat);return mat;
        }
        static void Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat)
        {
            var t=parent.Find(name);
            if(t==null){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());t=go.transform;}
            t.localPosition=pos;t.localScale=size;t.GetComponent<MeshRenderer>().sharedMaterial=mat;
            t.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;t.GetComponent<MeshRenderer>().receiveShadows=false;
        }
        internal static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var t=parent.Find(name) as RectTransform;
            if(t==null){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);t=(RectTransform)go.transform;}
            t.gameObject.layer=5;t.anchorMin=t.anchorMax=t.pivot=new Vector2(.5f,.5f);t.sizeDelta=size;t.anchoredPosition=pos;return t;
        }
        internal static TMP_Text Text(Transform parent,string name,string text,Vector2 pos,Vector2 size,float fontSize,TMP_FontAsset font)
        {
            var rect=Rect(parent,name,pos,size);var label=rect.GetComponent<TextMeshProUGUI>();if(label==null)label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text=text;label.font=font;label.fontSize=fontSize;label.color=new Color(.8f,.88f,.94f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            label.overflowMode=TextOverflowModes.Ellipsis;return label;
        }
        internal static Button Button(Transform parent,string name,string text,float x,float width,TMP_FontAsset font,UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(parent,name,new Vector2(x,-15),new Vector2(width,50));
            var image=rect.GetComponent<Image>();if(image==null)image=rect.gameObject.AddComponent<Image>();image.color=new Color(.07f,.11f,.15f,1);image.raycastTarget=true;
            var button=rect.GetComponent<Button>();if(button==null)button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            button.navigation=new Navigation{mode=Navigation.Mode.None};var colors=button.colors;colors.highlightedColor=new Color(.65f,.85f,1);colors.pressedColor=new Color(.35f,.65f,.85f);button.colors=colors;
            while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);
            UnityEventTools.AddPersistentListener(button.onClick,action);
            Text(rect,"Label",text,Vector2.zero,new Vector2(width-8,42),18,font);return button;
        }
        [MenuItem("Tools/Remix Table/Apply Seated Layout (existing scene)")]
        public static void Apply()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager=RemixSceneBuilder.All<RemixTableManager>(scene).Single();var d=manager.design;
            var sourceRefs=manager.audioManager.sources.ToArray();var clips=sourceRefs.Select(x=>x.clip).ToArray();
            // Reapply the authored design values without resetting the user's ergonomic tuning.
            RemixSceneBuilder.Require(d.panelWidth>d.width && d.spacing>d.laneHeight && d.panelHeight>3*d.spacing+d.laneHeight,"configured layout must fit");
            var workspace=manager.workspace;workspace.name="RemixWorkspaceRoot";
            var viewport=manager.ribbons[0].transform.parent;viewport.name="RibbonViewport";viewport.SetParent(workspace,false);viewport.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);viewport.localScale=Vector3.one;
            var origin=RemixSceneBuilder.All<XROrigin>(scene).Single();var pose=d.SeatedPose(origin.Camera.transform.position,origin.Camera.transform.forward);
            workspace.SetPositionAndRotation(pose.position,pose.rotation);workspace.localScale=Vector3.one;
            var frame=Child(workspace,"WorkspaceFrame");
            var panelMat=Surface("WorkspaceSurface",new Color(.018f,.026f,.04f));var laneMat=Surface("LaneSurface",new Color(.032f,.046f,.064f));
            Box(frame,"Panel",new Vector3(0,0,.022f),new Vector3(d.panelWidth,d.panelHeight,.008f),panelMat);
            StemId[] order={StemId.Vocals,StemId.Drums,StemId.Bass,StemId.Melody};
            for(int row=0;row<4;row++)
            {
                var ribbon=manager.ribbons.Single(x=>x.stem==order[row]);var t=ribbon.transform;
                t.name=order[row]==StemId.Melody?"OtherRibbon":order[row]+"Ribbon";t.SetLocalPositionAndRotation(new Vector3(0,d.LaneY(row),0),Quaternion.identity);t.localScale=Vector3.one;
                Box(frame,t.name+"Lane",new Vector3(0,d.LaneY(row),.014f),new Vector3(d.panelWidth-.04f,d.laneHeight,.002f),laneMat);
                Box(frame,t.name+"Accent",new Vector3(-d.panelWidth*.5f+.012f,d.LaneY(row),.008f),new Vector3(.002f,d.laneHeight*.65f,.001f),d.ribbonMaterial);
                var label=t.GetComponentsInChildren<TextMesh>(true).First();label.text=order[row]==StemId.Melody?"OTHER":order[row].ToString().ToUpperInvariant();label.characterSize=.0018f;label.fontSize=64;label.anchor=TextAnchor.MiddleLeft;label.color=d.colors[(int)ribbon.stem];label.transform.localPosition=new Vector3(-d.width*.5f+.008f,d.spacing*.5f,-.004f);label.transform.localScale=Vector3.one;
                label.font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);
                var labelRenderer=label.GetComponent<Renderer>();float targetHeight=(d.spacing-d.laneHeight)*.7f;
                if(labelRenderer.bounds.size.y>targetHeight)label.transform.localScale*=targetHeight/labelRenderer.bounds.size.y;
                var labelCenter=t.InverseTransformPoint(labelRenderer.bounds.center);label.transform.localPosition+=Vector3.up*(d.spacing*.5f-labelCenter.y);
                foreach(var halo in t.GetComponentsInChildren<LineRenderer>(true)){halo.SetPosition(0,new Vector3(-d.width*.5f,0,.002f));halo.SetPosition(1,new Vector3(d.width*.5f,0,.002f));halo.startWidth=halo.endWidth=.001f;}
            }
            var indicator=workspace.Find("PlaybackIndicator") ?? viewport.Find("FixedPlaybackIndicator");indicator.SetParent(viewport,false);indicator.name="FixedPlaybackIndicator";
            var line=indicator.GetComponent<LineRenderer>();float half=1.5f*d.spacing+d.laneHeight*.5f;
            line.SetPosition(0,new Vector3(0,-half,-.006f));line.SetPosition(1,new Vector3(0,half,-.006f));line.startWidth=line.endWidth=.0015f;line.startColor=line.endColor=new Color(.7f,.86f,1,.8f);
            var legacy=Child(workspace.parent,"LegacyWorkspaceDecor");legacy.gameObject.SetActive(false);
            foreach(var picker in RemixSceneBuilder.All<SpatialSongPicker>(scene)){picker.gameObject.SetActive(false);picker.transform.SetParent(legacy,true);}
            foreach(var text in workspace.GetComponentsInChildren<TextMesh>(true).Where(x=>x.text.Contains("PINCH + LIFT")).ToArray())text.transform.SetParent(legacy,true);
            var particles=workspace.Find("AmbientParticles");if(particles!=null)particles.gameObject.SetActive(false);
            var menu=RemixSceneBuilder.All<HandMenuController>(scene).Single();var menuData=new SerializedObject(menu);
            var menuCanvas=(Canvas)menuData.FindProperty("menuCanvas").objectReferenceValue;
            var font=menuCanvas.GetComponentsInChildren<TMP_Text>(true).First().font;
            var transportRect=Rect(workspace,"TransportCanvas",Vector2.zero,new Vector2(d.panelWidth*1000,d.transportHeight*1000));
            transportRect.localScale=Vector3.one*.001f;transportRect.localPosition=new Vector3(0,d.TransportY,-.006f);
            var canvas=transportRect.GetComponent<Canvas>();if(canvas==null)canvas=transportRect.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=origin.Camera;canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.Normal|AdditionalCanvasShaderChannels.Tangent;
            if(canvas.GetComponent<TrackedDeviceGraphicRaycaster>()==null)canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var background=transportRect.GetComponent<Image>();if(background==null)background=transportRect.gameObject.AddComponent<Image>();background.color=new Color(.018f,.026f,.04f,1);background.raycastTarget=false;
            var transport=transportRect.GetComponent<RemixTransport>();if(transport==null)transport=transportRect.gameObject.AddComponent<RemixTransport>();transport.manager=manager;
            transport.previousSong=Button(transportRect,"PreviousSong","Previous",-175,120,font,transport.PreviousSong);
            transport.playPause=Button(transportRect,"PlayPause","Pause",0,160,font,transport.TogglePlayback);
            transport.nextSong=Button(transportRect,"NextSong","Next",175,120,font,transport.NextSong);
            transport.playPauseLabel=transport.playPause.GetComponentInChildren<TMP_Text>();
            transport.songTitle=Text(transportRect,"SongTitle",manager.library.Current.title,new Vector2(-100,30),new Vector2(400,26),16,font);
            transport.playbackTime=Text(transportRect,"PlaybackTime","00:00",new Vector2(242,30),new Vector2(210,26),14,font);
            // Keep existing menu components and event references; make space for accurate guidance.
            var panel=(RectTransform)menuCanvas.transform.Find("Panel");panel.anchoredPosition=Vector2.zero;panel.sizeDelta=new Vector2(360,430);((RectTransform)menuCanvas.transform).sizeDelta=panel.sizeDelta;
            string[] names={"Title","Passthrough Toggle","Volume Label","Volume Slider","Close Button"};float[] ys={185,128,77,37,-184};
            for(int i=0;i<names.Length;i++){var rect=menuCanvas.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name==names[i]);rect.anchoredPosition=new Vector2(0,ys[i]);}
            transport.palmInstructions=Text(panel,"RemixGestureInstructions",RemixTransport.Guidance,new Vector2(0,-82),new Vector2(316,142),13,font);
            transport.palmInstructions.alignment=TextAlignmentOptions.Left;
            RemixModeLayout.Build(manager,transport);
            for(int i=0;i<4;i++)RemixSceneBuilder.Require(manager.audioManager.sources[i]==sourceRefs[i] && sourceRefs[i].clip==clips[i],"preserved stem source and clip references");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Validate();
        }
        public static void Validate()
        {
            RemixAudioSetup.Validate();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var manager=RemixSceneBuilder.All<RemixTableManager>(scene).Single();var d=manager.design;
            RemixSceneBuilder.Require(d.constrainToLanes && d.width<d.panelWidth && d.spacing>d.laneHeight && 3*d.spacing+d.laneHeight<d.panelHeight,"separated lanes fit panel");
            var order=manager.ribbons.OrderByDescending(x=>x.transform.localPosition.y).ToArray();
            RemixSceneBuilder.Require(order.Select(x=>x.stem).SequenceEqual(new[]{StemId.Vocals,StemId.Drums,StemId.Bass,StemId.Melody}),"seated row order");
            for(int i=0;i<4;i++)RemixSceneBuilder.Require(Mathf.Abs(order[i].transform.localPosition.y-d.LaneY(i))<.0001f && order[i].GetComponentInParent<Canvas>()==null,"3D ribbon row "+i);
            var transport=RemixSceneBuilder.All<RemixTransport>(scene).Single();
            RemixSceneBuilder.Require(transport.GetComponent<Canvas>().renderMode==RenderMode.WorldSpace && transport.GetComponent<TrackedDeviceGraphicRaycaster>()!=null,"world transport with XRI raycaster");
            foreach(var button in new[]{transport.previousSong,transport.playPause,transport.nextSong})RemixSceneBuilder.Require(button.onClick.GetPersistentEventCount()==1 && button.onClick.GetPersistentTarget(0)==transport,"transport binding "+button.name);
            RemixSceneBuilder.Require(!RemixSceneBuilder.All<SpatialSongPicker>(scene).Any(x=>x.isActiveAndEnabled),"no duplicate active song picker");
            RemixSceneBuilder.Require(transport.palmInstructions.text==RemixTransport.Guidance && transport.palmInstructions.GetComponentInParent<Canvas>(true).name=="Hand Menu Canvas","palm guidance and visibility parent");
            Debug.Log("REMIX_SEATED_VALIDATION_PASS");
        }
        public static void Audit()
        {
            var scene=EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager=RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            Debug.Log($"WORKSPACE_AUDIT {manager.workspace.name} position={manager.workspace.position} design width={manager.design.width} spacing={manager.design.spacing}");
            foreach(var r in manager.ribbons)Debug.Log($"RIBBON_AUDIT {r.name} pos={r.transform.position} local={r.transform.localPosition} colliders={r.GetComponents<Collider>().Length} clip={r.GetComponent<AudioSource>().clip.name}");
            foreach(var canvas in RemixSceneBuilder.All<Canvas>(scene))
            {
                Debug.Log($"CANVAS_AUDIT {canvas.name} size={((RectTransform)canvas.transform).sizeDelta} scale={canvas.transform.localScale} raycaster={canvas.GetComponent<TrackedDeviceGraphicRaycaster>()!=null}");
                foreach(var rect in canvas.GetComponentsInChildren<RectTransform>(true))Debug.Log($"UI_RECT {canvas.name}/{rect.name} pos={rect.anchoredPosition} size={rect.sizeDelta} anchors={rect.anchorMin}/{rect.anchorMax}");
            }
        }
    }
}
