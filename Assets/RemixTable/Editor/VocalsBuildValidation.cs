using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RemixTable.Editor
{
    public static class VocalsBuildValidation
    {
        public static void Preview()
        {
            Validate();
            RibbonVisualValidation.Preview();
        }
        public static void Validate()
        {
            RemixSceneBuilder.Validate();
            RibbonVisualValidation.Validate();
            RemixUiLayout.Validate();
            RemixWorkspaceLayout.Validate();
            foreach(string path in new[]{VocalsHologram.BodyPath,VocalsHologram.HaloPath,VocalsHologram.StemBodyPath,VocalsHologram.StemHaloPath})
            {
                var material=Resources.Load<Material>(path);
                if(material==null || ShaderUtil.ShaderHasError(material.shader)) throw new BuildFailedException("Vocals shader/material invalid");
                foreach(string property in new[]{"_PrimaryLinear","_DeepLinear","_HighlightLinear","_EdgeLinear","_LowpassLinear","_Contact0","_Contact1","_ContactGeometry","_ContactCoefficients","_ContactHaloCoefficients"})
                    if(!material.HasProperty(property))throw new BuildFailedException("Shared hologram palette property missing: "+property);
            }
            Debug.Log("VOCALS_SCENE_SHADER_VALIDATION_PASS");
            foreach(string path in new[]{SpatialOrbHologram.CorePath,SpatialOrbHologram.HaloPath})
            {
                var material=Resources.Load<Material>(path);
                if(material==null || material.shader.name!="RemixTable/SpatialHolographic" || ShaderUtil.ShaderHasError(material.shader))
                    throw new BuildFailedException("Spatial holographic material/shader invalid");
                foreach(string property in new[]{"_PrimaryLinear","_CoreIntensity","_RimIntensity","_HaloIntensity","_HaloExpansion"})
                    if(!material.HasProperty(property))throw new BuildFailedException("Spatial shader property missing: "+property);
            }
            var field=Object.FindFirstObjectByType<SpatialStemField>();
            if(field==null || field.orbs.Length!=4 || field.orbs.Any(o=>o==null) || field.orbRenderers.Any(r=>r==null))
                throw new BuildFailedException("Spatial orb scene references invalid");
            Debug.Log("SPATIAL_HOLOGRAM_REFERENCE_VALIDATION_PASS");
        }
        public static void Build()
        {
            Validate();
            var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Meta Quest.asset");
            if(profile==null) throw new BuildFailedException("Existing Quest profile missing");
            BuildProfile.SetActiveBuildProfile(profile);
            Directory.CreateDirectory("Builds");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions {
                buildProfile=profile,locationPathName="Builds/RemixTable-VocalsHolographic.apk",options=BuildOptions.DetailedBuildReport });
            if(report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException("Quest APK build failed");
            var packed=report.packedAssets.SelectMany(a=>a.contents).Select(a=>a.sourceAssetPath).ToArray();
            foreach(string path in new[]{"Assets/Resources/VocalsHolographicBody.mat",
                "Assets/Resources/VocalsHolographicHalo.mat","Assets/Resources/StemHolographicBody.mat",
                "Assets/Resources/StemHolographicHalo.mat","Assets/RemixTable/Shaders/VocalsHolographic.shader",
                "Assets/Resources/SpatialHolographicCore.mat","Assets/Resources/SpatialHolographicHalo.mat",
                "Assets/RemixTable/Shaders/SpatialHolographic.shader","Assets/Resources/RemixUiAtlas.png"})
                if(!packed.Contains(path)) throw new BuildFailedException("Vocals asset missing from player: "+path);
            File.WriteAllLines("Logs/VocalsPackedAssets.txt",packed.Where(p=>p.Contains("VocalsHolographic") || p.Contains("StemHolographic") || p.Contains("SpatialHolographic") || p.Contains("RemixUiAtlas")));
            Debug.Log("VOCALS_PACKED_ASSET_VALIDATION_PASS");
            Debug.Log("VOCALS_QUEST_BUILD_PASS bytes="+report.summary.totalSize);
        }
    }
}
