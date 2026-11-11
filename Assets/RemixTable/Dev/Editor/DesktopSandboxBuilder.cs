using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemixTable.Dev.Editor
{
    /// <summary>
    /// Tools > Remix Table > Dev > Build Desktop Sandbox. Creates (or rebuilds) Assets/RemixTable/Dev/DesktopSandbox.unity:
    /// a seated camera (the listener) at a desk, a light, the desk, DeskFrame, the engine (StemPlayer +
    /// MusicClockDriver + StemAnalyzer + DropController) with the test song, one coloured sphere per stem, and
    /// DesktopDevInput. The scene is built additively and closed again, so whatever scene you have open is never
    /// modified or saved. It is not added to the build settings.
    /// </summary>
    public static class DesktopSandboxBuilder
    {
        public const string ScenePath = "Assets/RemixTable/Dev/DesktopSandbox.unity";
        public const string TuningPath = "Assets/RemixTable/Dev/DevMixTuning.asset";

        const float DeskHeight = 0.75f;
        const float DeskThickness = 0.04f;
        static readonly Vector2 DeskSize = new Vector2(1.4f, 0.8f);
        static readonly Vector3 DeskCentre = new Vector3(0f, DeskHeight, 0.55f);
        static readonly Vector3 CameraPosition = new Vector3(0f, 1.2f, 0f);
        const float CameraPitch = 38f;
        const float OrbRadius = 0.045f;
        const float OrbArcRadius = 0.4f;
        const float OrbArcDegrees = 50f;

        [MenuItem("Tools/Remix Table/Dev/Build Desktop Sandbox")]
        static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Remix Table", "Leave Play mode first.", "OK");
                return;
            }

            var song = AssetDatabase.LoadAssetAtPath<SongDefinition>(TestSongGenerator.SongAssetPath);
            if (song == null)
            {
                if (!EditorUtility.DisplayDialog("Remix Table", "The test song doesn't exist yet. Generate it now?", "Generate", "Cancel"))
                    return;
                song = TestSongGenerator.Generate();
                if (song == null) return;
            }

            if (Build(song))
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                EditorGUIUtility.PingObject(sceneAsset);
                RemixLog.Info($"Desktop sandbox saved to {ScenePath}. Open it from the Project window and press Play.", sceneAsset);
            }
        }

        public static bool Build(SongDefinition song)
        {
            MixTuning tuning = GetOrCreateTuning();
            var riser = AssetDatabase.LoadAssetAtPath<AudioClip>(TestSongGenerator.RiserPath);
            var hit = AssetDatabase.LoadAssetAtPath<AudioClip>(TestSongGenerator.HitPath);

            Scene previousActive = SceneManager.GetActiveScene();
            bool rebuildingOpenSandbox = previousActive.path == ScenePath;
            Scene scene;
            try
            {
                // Rebuilding while the sandbox itself is open replaces it; otherwise build alongside the open scene.
                scene = rebuildingOpenSandbox
                    ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            catch (Exception e)
            {
                RemixLog.Error("Couldn't create the sandbox scene (an untitled scene may be open; save or close it first): " + e.Message);
                return false;
            }

            try
            {
                SceneManager.SetActiveScene(scene);
                Populate(song, tuning, riser, hit);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    RemixLog.Error($"Saving {ScenePath} failed.");
                    return false;
                }
                return true;
            }
            finally
            {
                if (!rebuildingOpenSandbox)
                {
                    if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static void Populate(SongDefinition song, MixTuning tuning, AudioClip riser, AudioClip hit)
        {
            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            // Seated camera = listener
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.07f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraPitch, 0f, 0f));

            // Desk
            GameObject deskMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deskMesh.name = "Desk";
            deskMesh.transform.position = DeskCentre - new Vector3(0f, DeskThickness * 0.5f, 0f);
            deskMesh.transform.localScale = new Vector3(DeskSize.x, DeskThickness, DeskSize.y);

            var deskFrameGo = new GameObject("DeskFrame");
            deskFrameGo.transform.position = DeskCentre;
            var desk = deskFrameGo.AddComponent<DeskFrame>();
            desk.Configure(cameraGo.transform, DeskSize);

            // Engine
            var engine = new GameObject("RemixTable Engine");
            var player = engine.AddComponent<StemPlayer>();
            player.EditorConfigure(song, tuning);
            engine.AddComponent<MusicClockDriver>().EditorConfigure(player);
            var analyzer = engine.AddComponent<StemAnalyzer>();
            analyzer.EditorConfigure(player);
            var drop = engine.AddComponent<DropController>();
            drop.EditorConfigure(player, riser, hit);
            // Off by default; tick it in Play mode to hear the hands-free headset test on desktop.
            engine.AddComponent<AudioSelfTest>().enabled = false;

            // One orb per stem, on an arc in front of the listener
            int count = song.StemCount;
            var orbs = new StemOrb[count];
            var orbsRoot = new GameObject("Orbs");
            for (int i = 0; i < count; i++)
            {
                StemDefinition stem = song.GetStem(i);
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float angle = Mathf.Lerp(-OrbArcDegrees, OrbArcDegrees, t) * Mathf.Deg2Rad;
                var position = new Vector3(
                    CameraPosition.x + Mathf.Sin(angle) * OrbArcRadius,
                    DeskHeight + OrbRadius,
                    CameraPosition.z + Mathf.Cos(angle) * OrbArcRadius);

                var orbGo = new GameObject($"Orb {i} {stem.Name}");
                orbGo.transform.SetParent(orbsRoot.transform, false);
                orbGo.transform.position = position;
                orbGo.AddComponent<SphereCollider>().radius = OrbRadius;
                var orb = orbGo.AddComponent<StemOrb>();
                orb.Configure(player, desk, i, OrbRadius);
                orbs[i] = orb;

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Visual";
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.transform.SetParent(orbGo.transform, false);
                visual.transform.localScale = Vector3.one * (OrbRadius * 2f);
                visual.AddComponent<DevOrbVisual>().Configure(analyzer, orb, visual.GetComponent<Renderer>(), stem.Color);
            }

            // Desktop controls + readout
            var devGo = new GameObject("DesktopDevInput");
            devGo.AddComponent<DesktopDevInput>().Configure(cam, player, drop, desk, analyzer, orbs);
        }

        static MixTuning GetOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MixTuning>(TuningPath);
            if (tuning != null) return tuning;
            tuning = ScriptableObject.CreateInstance<MixTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
            AssetDatabase.SaveAssets();
            return tuning;
        }
    }
}
