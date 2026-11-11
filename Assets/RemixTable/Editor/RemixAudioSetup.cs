using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RemixTable.Editor
{
    public static class RemixAudioSetup
    {
        public static void Configure()
        {
            string[] files = { "drums", "bass", "other", "vocals" };
            var clips = new AudioClip[4];
            for (int i=0;i<4;i++)
            {
                string path="Assets/RemixTable/Audio/NIVIROTest/Stems/"+files[i]+".wav";
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.Streaming;
                settings.compressionFormat=AudioCompressionFormat.Vorbis;
                settings.quality=.85f;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;
                importer.SetOverrideSampleSettings("Android",settings);
                importer.forceToMono=false;
                importer.SaveAndReimport();
                clips[i]=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            var scene=EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager=RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var audio=manager.GetComponent<RemixAudioManager>();
            if(audio==null) audio=manager.gameObject.AddComponent<RemixAudioManager>();
            audio.manager=manager; manager.audioManager=audio;
            audio.sources=new AudioSource[4];
            for(int i=0;i<4;i++)
            {
                var ribbon=manager.ribbons.Single(r=>(int)r.stem==i);
                var source=ribbon.GetComponent<AudioSource>();
                if(source==null)source=ribbon.gameObject.AddComponent<AudioSource>();
                source.clip=clips[i];source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.volume=audio.headroom;
                audio.sources[i]=source;
            }
            var song=manager.library.songs[0];song.id="niviro-test";song.title="NIVIROTest";song.duration=clips[0].length;
            song.stems=Enumerable.Range(0,4).Select(i=>new StemMetadata{id=(StemId)i,label=files[i],clip=clips[i]}).ToArray();
            manager.library.selectedIndex=0;
            foreach(var picker in RemixSceneBuilder.All<SpatialSongPicker>(scene))picker.label.text="SCULPT SOUND / NIVIROTest\nPINCH HERE · NEXT SONG";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Validate();
        }
        public static void Validate()
        {
            RemixSceneBuilder.Validate();
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            RemixSceneBuilder.Require(RemixSceneBuilder.All<AudioListener>(scene).Count(x=>x.isActiveAndEnabled)==1,"one active audio listener");
            var sources=RemixSceneBuilder.All<AudioSource>(scene);
            RemixSceneBuilder.Require(sources.Count(x=>x.GetComponent<RibbonView>()!=null)==4 && sources.All(x=>!x.playOnAwake) && sources.Where(x=>x.GetComponent<RibbonView>()!=null).All(x=>x.clip!=null && x.loop),"four assigned scheduled sources only");
            Debug.Log("REMIX_AUDIO_VALIDATION_PASS");
        }
    }
}

