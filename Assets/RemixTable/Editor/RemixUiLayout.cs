using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace RemixTable.Editor
{
    public static class RemixUiLayout
    {
        public const string AtlasPath="Assets/Resources/RemixUiAtlas.png";
        static readonly string[] Names={"Play","Pause","Stop","Record","Previous","Next","Undo","Redo","Volume","Filter","Pitch","Mute","Solo","Loop","Snap","Clear","Reset","Save","Voice","Help","Tools","Room","Spatial","Close","Edit","Live","Check","Surface","Border","Recenter","HandleBorder"};
        public static void ImportAtlas()
        {
            var importer=AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("UI atlas missing");
            #pragma warning disable CS0618
            if(importer.textureShape==TextureImporterShape.Texture2D && importer.textureType==TextureImporterType.Sprite && importer.spriteImportMode==SpriteImportMode.Multiple && AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().Count()==Names.Length)return;
            importer.textureShape=TextureImporterShape.Texture2D;importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.isReadable=false;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.spritesheet=Names.Select((name,i)=>new SpriteMetaData{name=name,rect=new Rect(i%8*128,512-(i/8+1)*128,128,128),pivot=new Vector2(.5f,.5f),alignment=(int)SpriteAlignment.Center,border=name=="Surface" || name=="Border" || name=="HandleBorder"?Vector4.one*32:Vector4.zero}).ToArray();
            importer.SaveAndReimport();
            #pragma warning restore CS0618
        }
        [MenuItem("Tools/Remix Table/Apply Minimal Icon UI")]
        public static void Apply()
        {
            ImportAtlas();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("ICON_ATLAS_IMPORT_PASS sprites="+Resources.LoadAll<Sprite>("RemixUiAtlas").Length);
            EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager=Object.FindFirstObjectByType<RemixTableManager>();
            var buttons=Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var rects=buttons.Select(b=>TargetStamp(b.GetComponent<RectTransform>())).ToArray();
            var counts=buttons.Select(b=>b.onClick.GetPersistentEventCount()).ToArray();
            var view=manager.GetComponent<RemixUiPresentation>();if(view==null)view=manager.gameObject.AddComponent<RemixUiPresentation>();view.manager=manager;view.Initialize();
            for(int i=0;i<buttons.Length;i++)
            {
                // Compare the actual target layout properties; children are decorative only.
                if(TargetStamp(buttons[i].GetComponent<RectTransform>())!=rects[i] || buttons[i].onClick.GetPersistentEventCount()!=counts[i])throw new InvalidOperationException("UI target or action changed");
            }
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);EditorSceneManager.SaveScene(manager.gameObject.scene);AssetDatabase.SaveAssets();Validate();
        }
        static string TargetStamp(RectTransform r)=>r.anchoredPosition3D.ToString("R")+r.sizeDelta.ToString("R")+r.anchorMin.ToString("R")+r.anchorMax.ToString("R")+r.pivot.ToString("R")+r.localScale.ToString("R")+r.localRotation.ToString("R");
        public static void Validate()
        {
            var view=Object.FindFirstObjectByType<RemixUiPresentation>();
            if(view==null || view.controls.Length!=45)throw new InvalidOperationException("Expected all 45 original buttons");
            foreach(var control in view.controls)
            {
                if(control==null || control.button==null || control.outline==null || control.selectedMark==null || string.IsNullOrWhiteSpace(control.semanticName))throw new InvalidOperationException("Missing UI reference");
                if(!control.numeric && (control.icon==null || control.icon.sprite==null))throw new InvalidOperationException("Missing icon");
                if(control.numeric && (control.sourceLabel==null || !control.sourceLabel.enabled))throw new InvalidOperationException("Numeric label hidden");
                var canvas=control.GetComponentInParent<Canvas>(true);if(canvas==null || canvas.renderMode!=RenderMode.WorldSpace || canvas.GetComponent<TrackedDeviceGraphicRaycaster>()==null)throw new InvalidOperationException("XR UI raycaster missing: "+control.role);
                foreach(var graphic in control.GetComponentsInChildren<Graphic>(true))if(graphic!=control.button.targetGraphic && graphic.name.StartsWith("Presentation") && graphic.raycastTarget)throw new InvalidOperationException("Decoration intercepts input");
            }
            if(Resources.LoadAll<Sprite>("RemixUiAtlas").Length!=Names.Length)throw new InvalidOperationException("Missing atlas sprites");
            if(Graphic.defaultGraphicMaterial==null || ShaderUtil.ShaderHasError(Graphic.defaultGraphicMaterial.shader))throw new InvalidOperationException("UI material/shader invalid");
            foreach(var root in view.gameObject.scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.GetComponents<Component>().Any(c=>c==null))throw new InvalidOperationException("Missing scene script: "+t.name);
            Debug.Log("ICON_UI_REFERENCE_VALIDATION_PASS buttons=45 sprites="+Names.Length);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/IconUiInventory.txt",view.controls.OrderBy(c=>c.role).Select(c=>$"{c.role} | {c.semanticName} | {c.iconKey} | numeric={c.numeric} | persistentActions={c.button.onClick.GetPersistentEventCount()}"));
        }
        public static void Preview()
        {
            EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);var manager=Object.FindFirstObjectByType<RemixTableManager>();var ui=manager.GetComponent<RemixUiPresentation>();ui.Initialize();Validate();
            manager.clock.elapsed=65;
            foreach(var ribbon in manager.ribbons)
            {
                typeof(RibbonView).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(ribbon,null);ribbon.UpdateWaveform();
            }
            var go=new GameObject("temporary UI preview camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=manager.workspace.TransformPoint(new Vector3(0,-.08f,-1.25f));camera.transform.LookAt(manager.workspace.TransformPoint(new Vector3(0,-.08f,0)));
            camera.orthographic=true;camera.orthographicSize=.55f;camera.aspect=1.6f;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.020f,.035f);
            Canvas.ForceUpdateCanvases();Capture(camera,"Logs/IconUiWorkspace.png");
            var filter=ui.controls.Single(c=>c.role=="FilterMode");filter.OnPointerEnter(null);filter.Paint();ui.tooltip.Advance(.61f);Canvas.ForceUpdateCanvases();Capture(camera,"Logs/IconUiHoverTooltip.png");filter.OnPointerExit(null);filter.Paint();
            var panel=manager.workspaceInteraction.masterVolumePanel;panel.SetActive(true);Canvas.ForceUpdateCanvases();Capture(camera,"Logs/WorkspaceControlsPreview.png");
            Object.DestroyImmediate(go);
        }
        public static void ApplyAndPreview(){Apply();Preview();}
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);target.Create();RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}finally{RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
