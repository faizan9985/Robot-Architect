using System;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;
using Object = UnityEngine.Object;
namespace RemixTable.Editor
{
    // Final feature set: QUICK START onboarding (card + ghost pinch), Meta XR Audio 3D stems with room-sized
    // reverb, FoV-aware side panels, gaze focus / audition, and environment-depth occlusion in ROOM mode.
    // Idempotent; batch: -executeMethod RemixTable.Editor.RemixFinalLayout.Apply
    public static class RemixFinalLayout
    {
        public const string CardName = "QuickStartCanvas", GhostName = "QuickStartGhost", AudioName = "RemixAudio3D", FovName = "RemixFov", GazeName = "RemixGaze";
        public const float CardY = .43f;
        public static readonly Vector2 CardSize = new Vector2(.40f, .085f);

        [MenuItem("Tools/Remix Table/Add Quick Start, 3D Audio, Gaze, FoV and Occlusion")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnableOcclusionFeature();
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var transport = RemixSceneBuilder.All<RemixTransport>(scene).Single();
            var toolbox = RemixSceneBuilder.All<RemixToolbox>(scene).Single();
            var vibes = RemixSceneBuilder.All<RemixVibes>(scene).Single();
            var daily = RemixSceneBuilder.All<RemixDailyDrop>(scene).Single();
            var scanner = RemixSceneBuilder.All<RemixRoomScanner>(scene).Single();
            var origin = RemixSceneBuilder.All<XROrigin>(scene).Single(o => o.gameObject.activeInHierarchy);
            var font = transport.songTitle.font;

            // ---- Quick start card + ghost pinch ----
            var card = RemixModeLayout.Row(manager, transport, CardName, CardY, CardSize);
            var onboarding = Get<RemixOnboarding>(Root(scene, "RemixQuickStart"));
            onboarding.manager = manager; onboarding.toolbox = toolbox; onboarding.vibes = vibes;
            onboarding.card = card.gameObject;
            onboarding.stepLabel = Label(card, "StepLabel", "QUICK START", new Vector2(-110, 24), new Vector2(170, 24), 13, font, new Color(1f, .62f, .2f));
            onboarding.instruction = Label(card, "Instruction", RemixOnboarding.Text(RemixOnboarding.Step.Pull), new Vector2(-40, -10), new Vector2(300, 52), 15, font, Color.white);
            onboarding.instruction.textWrappingMode = TextWrappingModes.Normal; onboarding.instruction.alignment = TextAlignmentOptions.Left;
            onboarding.stepLabel.alignment = TextAlignmentOptions.Left;
            var skip = RemixSeatedLayout.Button(card, "Skip", "SKIP", 150, 80, font, Action(onboarding, nameof(RemixOnboarding.Skip)));
            var sr = (RectTransform)skip.transform; sr.anchoredPosition = new Vector2(150, 0); sr.sizeDelta = new Vector2(80, 44);
            onboarding.skipButton = skip;
            onboarding.vibesHighlight = vibes.GetComponent<Image>();
            card.gameObject.SetActive(false); // Shown by the onboarding when it starts.
            BuildGhost(scene, onboarding);
            toolbox.onboarding = onboarding;
            EditorUtility.SetDirty(onboarding); EditorUtility.SetDirty(toolbox);

            // ---- 3D audio (Meta XR Audio) ----
            manager.audioManager.use3DAudio = true; EditorUtility.SetDirty(manager.audioManager);
            var audioRoot = Root(scene, AudioName);
            var acoustics = Get<MetaXRAudioRoomAcousticProperties>(audioRoot);
            var meta = Get<RemixMetaAudio>(audioRoot);
            var data = new SerializedObject(meta);
            data.FindProperty("manager").objectReferenceValue = manager;
            data.FindProperty("room").objectReferenceValue = acoustics;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(acoustics);

            // ---- FoV-aware side panels ----
            var fov = Get<RemixFovAdapter>(Root(scene, FovName));
            fov.manager = manager;
            fov.sidePanels = new[] { (RectTransform)daily.transform, (RectTransform)vibes.transform };
            fov.designAngle = RemixSidePanelsLayout.PanelAngle;
            EditorUtility.SetDirty(fov);

            // ---- Gaze focus ----
            var gazeRoot = Root(scene, GazeName);
            var gaze = Get<RemixGazeFocus>(gazeRoot);
            gaze.manager = manager;
            var labelTransform = gazeRoot.transform.Find("GazeLabel");
            var labelGo = labelTransform != null ? labelTransform.gameObject : new GameObject("GazeLabel", typeof(RectTransform));
            labelGo.transform.SetParent(gazeRoot.transform, false);
            var text = Get<TextMeshPro>(labelGo);
            text.font = font; text.fontSize = .32f; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(.6f, .06f); text.textWrappingMode = TextWrappingModes.NoWrap;
            labelGo.SetActive(false);
            gaze.label = text;
            EditorUtility.SetDirty(gaze); EditorUtility.SetDirty(text);

            // ---- Occlusion (ROOM mode) ----
            var cam = origin.Camera;
            var occlusion = Get<AROcclusionManager>(cam.gameObject);
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            occlusion.enabled = false;
            var shaderOcclusion = Get<ARShaderOcclusion>(cam.gameObject);
            shaderOcclusion.enabled = false;
            EditorUtility.SetDirty(occlusion); EditorUtility.SetDirty(shaderOcclusion);
            var scan = new SerializedObject(scanner);
            scan.FindProperty("occlusionManager").objectReferenceValue = occlusion;
            scan.FindProperty("shaderOcclusion").objectReferenceValue = shaderOcclusion;
            scan.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        static GameObject Root(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
            if (go == null) { go = new GameObject(name); EditorSceneManager.MoveGameObjectToScene(go, scene); }
            return go;
        }
        static T Get<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c != null ? c : go.AddComponent<T>(); }
        static TMP_Text Label(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, TMP_FontAsset font, Color color)
        {
            var t = RemixSeatedLayout.Text(parent, name, text, pos, size, fontSize, font); t.color = color; EditorUtility.SetDirty(t); return t;
        }
        static UnityEngine.Events.UnityAction Action<T>(T target, string method) where T : Object =>
            (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, method);

        // Two glowing fingertips (thumb and index) that pinch and drag.
        static void BuildGhost(UnityEngine.SceneManagement.Scene scene, RemixOnboarding onboarding)
        {
            var root = Root(scene, GhostName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(RemixFeatureLayout.OrbMaterialPath);
            Transform Tip(string name)
            {
                var t = root.transform.Find(name);
                var go = t != null ? t.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name; go.transform.SetParent(root.transform, false);
                var collider = go.GetComponent<Collider>(); if (collider != null) Object.DestroyImmediate(collider);
                go.transform.localScale = Vector3.one * .018f;
                var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                return go.transform;
            }
            onboarding.ghost = root.transform;
            onboarding.ghostThumb = Tip("Thumb"); onboarding.ghostIndex = Tip("Index");
            onboarding.ghostRenderers = new Renderer[] { onboarding.ghostThumb.GetComponent<Renderer>(), onboarding.ghostIndex.GetComponent<Renderer>() };
            root.SetActive(false);
        }

        static void EnableOcclusionFeature()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            RemixSceneBuilder.Require(settings != null, "Android OpenXR settings exist");
            foreach (var feature in settings.GetFeatures())
                if (feature is AROcclusionFeature) { feature.enabled = true; EditorUtility.SetDirty(feature); }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var toolbox = RemixSceneBuilder.All<RemixToolbox>(scene).Single();
            var onboarding = RemixSceneBuilder.All<RemixOnboarding>(scene).Single();
            RemixSceneBuilder.Require(toolbox.onboarding == onboarding && onboarding.card != null && onboarding.instruction != null && onboarding.ghost != null && onboarding.ghostRenderers.All(r => r != null), "quick start wired");
            RemixSceneBuilder.Require(onboarding.skipButton.onClick.GetPersistentEventCount() == 1 && onboarding.skipButton.onClick.GetPersistentMethodName(0) == nameof(RemixOnboarding.Skip), "skip bound");
            RemixSceneBuilder.Require(onboarding.ghost.GetComponentsInChildren<Collider>(true).Length == 0, "ghost has no colliders");
            var meta = RemixSceneBuilder.All<RemixMetaAudio>(scene).Single();
            var metaData = new SerializedObject(meta);
            RemixSceneBuilder.Require(metaData.FindProperty("manager").objectReferenceValue != null && metaData.FindProperty("room").objectReferenceValue != null, "3D audio wired");
            RemixSceneBuilder.Require(RemixSceneBuilder.All<MetaXRAudioRoomAcousticProperties>(scene).Length == 1, "exactly one room acoustics component");
            var fov = RemixSceneBuilder.All<RemixFovAdapter>(scene).Single();
            RemixSceneBuilder.Require(fov.sidePanels.Length == 2 && fov.sidePanels.All(p => p != null), "FoV adapter has both side panels");
            var gaze = RemixSceneBuilder.All<RemixGazeFocus>(scene).Single();
            RemixSceneBuilder.Require(gaze.manager != null && gaze.label != null, "gaze focus wired");
            var scanner = new SerializedObject(RemixSceneBuilder.All<RemixRoomScanner>(scene).Single());
            var occlusion = (AROcclusionManager)scanner.FindProperty("occlusionManager").objectReferenceValue;
            RemixSceneBuilder.Require(occlusion != null && !occlusion.enabled && scanner.FindProperty("shaderOcclusion").objectReferenceValue != null, "occlusion wired, off until ROOM");
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            RemixSceneBuilder.Require(settings.GetFeature<AROcclusionFeature>() != null && settings.GetFeature<AROcclusionFeature>().enabled, "Meta Quest: Occlusion enabled for Android");
            var ribbon = Shader.Find("RemixTable/RibbonEnergy");
            RemixSceneBuilder.Require(ribbon != null && ribbon.isSupported, "ribbon shader (with occlusion) compiles");
            Debug.Log("REMIX_FINAL_LAYOUT_PASS: quick start, 3D audio, FoV, gaze and occlusion configured.");
        }
    }
}
