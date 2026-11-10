using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

namespace RobotArchitect.Editor
{
    /// <summary>Batch-only smoke checks. Changes exist solely in Play Mode and are never saved.</summary>
    public static class RobotArchitectPlayModeSmoke
    {
        const string Pending = "RobotArchitect.Phase1.SmokePending";
        static double started;
        static int result;
        static bool finishing;

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this smoke check in a separate batch-mode Editor.");
            EditorSceneManager.OpenScene(RobotArchitectPhase1.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(Pending, true);
            RestoreAfterReload();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void RestoreAfterReload()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnState;
            EditorApplication.playModeStateChanged += OnState;
        }

        static void Tick()
        {
            if (finishing) return;
            if (EditorApplication.timeSinceStartup - started > 120)
            {
                Finish(new TimeoutException("Play Mode initialization exceeded 120 seconds."));
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.timeSinceLevelLoad < 3f) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                var roots = scene.GetRootGameObjects();
                T[] All<T>() where T : Component => roots.SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
                Check(All<XROrigin>().Length == 1, "One rig after runtime initialization.");
                Check(All<XRInteractionManager>().Length == 1, "One interaction manager after runtime initialization.");
                var menu = All<HandMenuController>().Single();
                Check(menu.isActiveAndEnabled, "Hand menu initialized without disabling itself.");
                Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1,
                    "One runtime EventSystem.");
                Check(UnityEngine.Object.FindObjectsByType<XRUIInputModule>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled) == 1,
                    "One runtime XRUIInputModule.");
                var root = roots.Single(x => x.name == "RobotArchitect").transform;
                var cube = root.Find("Interactables/TestCube");
                var body = cube.GetComponent<Rigidbody>();
                var zoneTop = root.Find("Workstation/InputZone/Surface").GetComponent<Collider>().bounds.max.y;
                Check(cube.GetComponent<Collider>().bounds.min.y >= zoneTop - 0.012f, "Test cube rests on InputZone rather than falling through it.");
                Check(Mathf.Abs(body.linearVelocity.y) < 0.1f, "Test cube settles under gravity.");
                Check(cube.GetComponent<GrabColorFeedback>().enabled, "GrabColorFeedback initialized with a valid renderer/material.");
                Check(!All<PassthroughController>().Single().IsPassthroughEnabled, "Passthrough remains off on startup.");

                // Synthetic eligibility exercises real button listeners, not physical tracking or pointer input.
                var data = new SerializedObject(menu);
                var open = (Button)data.FindProperty("openButton").objectReferenceValue;
                var close = (Button)data.FindProperty("closeButton").objectReferenceValue;
                typeof(HandMenuController).GetField("eligible", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, true);
                open.onClick.Invoke();
                Check(menu.IsMenuOpen, "Open button listener opens menu with synthetic palm eligibility.");
                var canvas = (Canvas)data.FindProperty("menuCanvas").objectReferenceValue;
                Check(canvas.gameObject.activeInHierarchy, "Expanded menu activates.");
                var slider = canvas.GetComponentInChildren<Slider>(true);
                var volume = AudioListener.volume;
                slider.value = 0.35f;
                Check(Mathf.Abs(AudioListener.volume - 0.35f) < 0.001f, "Volume slider updates AudioListener through original listener.");
                slider.value = volume;
                close.onClick.Invoke();
                Check(!menu.IsMenuOpen && !canvas.gameObject.activeSelf, "Close button hides expanded menu.");
                Debug.Log("ROBOT_ARCHITECT_PLAYMODE_PASS: runtime initialization, cube gravity/support, menu button listeners and volume. Synthetic menu eligibility; no tracked hands, physical UI input, or Quest passthrough tested.");
                Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ROBOT_ARCHITECT_PLAYMODE_FAILED: " + message);
            Debug.Log("ROBOT_ARCHITECT_SMOKE_CHECK: " + message);
        }

        static void Finish(Exception exception)
        {
            finishing = true;
            result = exception == null ? 0 : 1;
            if (exception != null) Debug.LogException(exception);
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else EditorApplication.Exit(result);
        }

        static void OnState(PlayModeStateChange state)
        {
            if (finishing && state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.Exit(result);
        }
    }
}
