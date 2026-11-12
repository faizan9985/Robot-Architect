using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RemixTable.Editor
{
    public static class RemixSceneBuilder
    {
        public const string ScenePath="Assets/RemixTable/Scenes/RemixTable.unity";
        static Transform Child(string name,Transform parent) { var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform; }
        [MenuItem("Tools/Remix Table/Generate Scene (replaces Remix scene)")]
        public static void Build()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Assets/RemixTable/Scenes");Directory.CreateDirectory("Assets/RemixTable/Materials");
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene("Assets/RobotArchitect/Scenes/RobotArchitect.unity",OpenSceneMode.Single);
            // Save As through Unity retains prefab instances and every shared serialized reference.
            if(!EditorSceneManager.SaveScene(scene,ScenePath,true)) throw new Exception("Could not duplicate scene");
            scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var gameplay=scene.GetRootGameObjects().Single(x=>x.name=="RobotArchitect");
            UnityEngine.Object.DestroyImmediate(gameplay);
            var design=AssetDatabase.LoadAssetAtPath<RemixDesign>("Assets/RemixTable/Materials/RemixDesign.asset");
            if(design==null) {design=ScriptableObject.CreateInstance<RemixDesign>();AssetDatabase.CreateAsset(design,"Assets/RemixTable/Materials/RemixDesign.asset");}
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/RemixTable/Shaders/RibbonEnergy.shader");if(shader==null)throw new Exception("Energy shader missing");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/RemixTable/Materials/RibbonEnergy.mat");
            if(material==null) {material=new Material(shader);AssetDatabase.CreateAsset(material,"Assets/RemixTable/Materials/RibbonEnergy.mat");}
            design.ribbonMaterial=material;EditorUtility.SetDirty(design);
            var root=Child("RemixTable",null);
            var manager=Child("RemixTableManager",root).gameObject.AddComponent<RemixTableManager>();manager.design=design;
            var library=manager.gameObject.AddComponent<SongLibraryManager>();manager.library=library;
            library.songs=new[]{Song("neon-tide","NEON TIDE",32,110,3),Song("glass-orbit","GLASS ORBIT",40,96,7),Song("violet-dawn","VIOLET DAWN",48,124,11)};
            var workspace=Child("MusicWorkspace",root);manager.workspace=workspace;
            var origin=All<XROrigin>(scene).Single();
            var forward=Vector3.ProjectOnPlane(origin.Camera.transform.forward,Vector3.up).normalized;
            workspace.SetPositionAndRotation(origin.Camera.transform.position+forward*.7f+Vector3.down*.35f,Quaternion.LookRotation(forward));
            Child("WorkspaceAnchor",workspace);
            var container=Child("RibbonContainer",workspace);
            manager.ribbons=new RibbonView[4];
            for(int i=0;i<4;i++)
            {
                var t=Child(((StemId)i)+"Ribbon",container);t.localPosition=new Vector3(0,(1.5f-i)*design.spacing,0);
                var view=t.gameObject.AddComponent<RibbonView>();view.manager=manager;view.stem=(StemId)i;manager.ribbons[i]=view;
                t.GetComponent<MeshRenderer>().sharedMaterial=material;
                Label(((StemId)i).ToString().ToUpperInvariant(),t,new Vector3(-.5f,0,0),design.colors[i],.018f);
                // Static energy line is visible before entering Play Mode. Runtime mesh supplies deformation.
                Line("EnergyHalo",t,new Vector3(-design.width/2,0,.004f),new Vector3(design.width/2,0,.004f),.002f,design.colors[i]*.3f,material);
            }
            Line("PlaybackIndicator",workspace,new Vector3(0,-.23f,-.012f),new Vector3(0,.23f,-.012f),.0025f,new Color(.5f,.8f,1,.5f),material);
            var particles=Child("AmbientParticles",workspace).gameObject.AddComponent<ParticleSystem>();
            var main=particles.main;main.maxParticles=design.particleBudget;main.startLifetime=3;main.startSpeed=.008f;main.startSize=.002f;main.startColor=new Color(.15f,.6f,1,.15f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=particles.emission;emission.rateOverTime=6;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(.85f,.45f,.08f);
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
            var systems=Child("InteractionSystems",root);
            var interaction=Child("RibbonInteractionManager",systems).gameObject.AddComponent<RibbonInteractionManager>();interaction.manager=manager;manager.interactions=interaction;
            Child("RibbonSelectionManager",systems);Child("RibbonAutomationManager",systems);
            var visuals=Child("VisualSystems",root);Child("RibbonVisualizationManager",visuals);Child("AudioReactiveVisualManager",visuals);
            var ui=Child("UI",root);var picker=Child("SongSelectionPlaceholder",ui);picker.SetParent(workspace,false);picker.localPosition=new Vector3(0,-.28f,0);
            var collider=picker.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.5f,.055f,.025f);
            var interactable=picker.gameObject.AddComponent<XRSimpleInteractable>();interactable.interactionManager=All<XRInteractionManager>(scene).Single();
            var songPicker=picker.gameObject.AddComponent<SpatialSongPicker>();songPicker.library=library;
            songPicker.label=Label("SCULPT SOUND / NEON TIDE\nPINCH HERE · NEXT SONG",picker,Vector3.zero,new Color(.5f,.8f,1),.015f);
            Label("PINCH + LIFT  /  GAIN     TWO HANDS  /  REGION\nTWIST  /  EFFECT     HOLD ENDPOINT  /  WHOLE STEM",workspace,new Vector3(0,-.34f,0),new Color(.35f,.55f,.65f),.009f);
            // Preserve default passthrough state and existing palm toggle wiring exactly.
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Validate();
        }
        static SongMetadata Song(string id,string title,float duration,float bpm,int seed) => new SongMetadata {id=id,title=title,duration=duration,bpm=bpm,seed=seed,stems=Enum.GetValues(typeof(StemId)).Cast<StemId>().Select(s=>new StemMetadata{id=s,label=s.ToString()}).ToArray()};
        static TextMesh Label(string text,Transform parent,Vector3 position,Color color,float size)
        {
            var t=Child("Label",parent);t.localPosition=position;var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.characterSize=size;label.fontSize=48;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;return label;
        }
        static void Line(string name,Transform parent,Vector3 a,Vector3 b,float width,Color color,Material mat)
        {
            var line=Child(name,parent).gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.SetPosition(0,a);line.SetPosition(1,b);line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.sharedMaterial=mat;
        }
        public static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<T>(true)).ToArray();
        [MenuItem("Tools/Remix Table/Validate Saved Scene")]
        public static void Validate()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            Require(All<XROrigin>(scene).Count(x=>x.isActiveAndEnabled)==1,"one active XR Origin");
            Require(All<XRInteractionManager>(scene).Count(x=>x.isActiveAndEnabled)==1,"one active interaction manager");
            var origin=All<XROrigin>(scene).Single();
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(origin.gameObject).EndsWith("XR Origin Hands (XR Rig).prefab"),"hands prefab connection");
            var menu=All<HandMenuController>(scene).Single();var data=new SerializedObject(menu);
            foreach(var field in new[]{"xrCamera","trackingSpace","passthrough","launcherCanvas","menuCanvas","openButton","closeButton","passthroughToggle","passthroughLabel","volumeControl"}) Require(data.FindProperty(field).objectReferenceValue!=null,"palm reference "+field);
            Require(All<UnityEngine.XR.ARFoundation.ARSession>(scene).Length==1,"one AR session");
            var pass=new SerializedObject(All<PassthroughController>(scene).Single());
            Require(pass.FindProperty("xrCamera").objectReferenceValue==origin.Camera,"passthrough camera");
            Require(pass.FindProperty("cameraManager").objectReferenceValue!=null,"passthrough AR manager");
            var manager=All<RemixTableManager>(scene).Single();
            Require(manager.ribbons.Length==4 && manager.ribbons.All(x=>x!=null && x.manager==manager),"four ribbons share timeline");
            Require(manager.ribbons.Select(x=>x.stem).Distinct().Count()==4,"four unique stems");
            Require(manager.design.colors.Length==4 && manager.library.songs.Length==3,"palette and songs");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/RemixTable/Shaders/RibbonEnergy.shader");
            Require(shader!=null && !ShaderUtil.ShaderHasError(shader),"energy shader import");
            Require(All<UnityEngine.EventSystems.EventSystem>(scene).Length==0,"inherited runtime UI registration");
            foreach(var transform in All<Transform>(scene))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)==0,"scripts "+transform.name);
                foreach(var component in transform.GetComponents<Component>())
                {
                    if(component==null)continue;
                    var property=new SerializedObject(component).GetIterator();
                    while(property.Next(true)) if(property.propertyType==SerializedPropertyType.ObjectReference)
                        Require(property.objectReferenceValue!=null || property.objectReferenceInstanceIDValue==0,"reference "+transform.name+"/"+property.propertyPath);
                }
            }
            Require(!scene.GetRootGameObjects().Any(x=>x.name=="RobotArchitect"),"gameplay removed from duplicate");
            Debug.Log("REMIX_VALIDATION_PASS: scene, XR prefab, palm, passthrough, ribbons, songs and serialized references.");
        }
        public static void Require(bool condition,string message) {if(!condition)throw new Exception("REMIX_CHECK_FAILED: "+message);}
    }
}

