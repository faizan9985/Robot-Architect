using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RobotArchitect.Editor
{
    // Meta SDK 209 injects an AgentBridge token at order 1, even with DevAgent off.
    // Run afterwards so optional development credentials never enter player Resources.
    public sealed class DevAgentCredentialBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            Sanitize();
        }

        public static void Sanitize()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/Resources/DevAgentSettings.asset");
            if (asset == null)
                throw new BuildFailedException("DevAgent settings could not be inspected for credentials.");

            var settings = new SerializedObject(asset);
            var token = settings.FindProperty("accessToken");
            var enabled = settings.FindProperty("enabled");
            var voiceToken = settings.FindProperty("witClientAccessToken");
            if (token == null || enabled == null || voiceToken == null)
                throw new BuildFailedException("DevAgent settings schema changed; review credential protection.");

            token.stringValue = string.Empty;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            if (enabled.boolValue || !string.IsNullOrEmpty(voiceToken.stringValue))
                throw new BuildFailedException(
                    "DevAgent must remain disabled and voice credentials empty for Robot Architect builds.");
        }
    }
}
