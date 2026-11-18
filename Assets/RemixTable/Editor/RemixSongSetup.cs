using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace RemixTable.Editor
{
    // Adds the stem-split songs to the library the same way NIVIROTest is set up: four equal-length WAV stems per
    // song (Demucs drums/bass/other/vocals) imported for streaming, baked waveform caches, and a library entry.
    // To add a song: split it into Audio/<Folder>/Stems/{drums,bass,other,vocals}.wav, add a row below, run
    // Tools/Remix Table/Set Up Songs. NIVIROTest stays first and is left untouched.
    public static class RemixSongSetup
    {
        public struct SongDef { public string folder, id, title; public float bpm, beatOffset; public int seed; }
        public static readonly SongDef[] Songs =
        {
            new SongDef { folder = "OnAndOn", id = "on-and-on", title = "On & On", bpm = 174, beatOffset = .2523f, seed = 13 },
            new SongDef { folder = "HeroesTonight", id = "heroes-tonight", title = "Heroes Tonight", bpm = 128, beatOffset = .0891f, seed = 17 },
            new SongDef { folder = "Mortals", id = "mortals", title = "Mortals", bpm = 90, beatOffset = .2772f, seed = 19 },
        };
        static readonly string[] Files = { "drums", "bass", "other", "vocals" };
        const string FirstSongId = "niviro-test";
        static string StemPath(SongDef d, int i) => $"Assets/RemixTable/Audio/{d.folder}/Stems/{Files[i]}.wav";
        static string WavePath(SongDef d, int i) => $"{WaveformBaker.CacheFolder}/{d.folder}/{Files[i]}.rwave";

        [MenuItem("Tools/Remix Table/Set Up Songs")]
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var d in Songs)
            {
                Directory.CreateDirectory($"{WaveformBaker.CacheFolder}/{d.folder}");
                for (int i = 0; i < 4; i++)
                {
                    string path = StemPath(d, i);
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) throw new Exception("Missing stem " + path + " (split the song first)");
                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = .85f;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    settings.preloadAudioData = false;
                    importer.defaultSampleSettings = settings;
                    importer.SetOverrideSampleSettings("Android", settings);
                    importer.forceToMono = false;
                    importer.SaveAndReimport();
                    WaveformBaker.Bake(path, WavePath(d, i));
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var library = manager.library;
            var songs = new List<SongMetadata>();
            var first = library.songs.FirstOrDefault(s => s.id == FirstSongId);
            if (first != null) songs.Add(first);
            foreach (var d in Songs)
            {
                var stems = new StemMetadata[4];
                AudioClip reference = null;
                for (int i = 0; i < 4; i++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(StemPath(d, i));
                    var wave = AssetDatabase.LoadAssetAtPath<WaveformCache>(WavePath(d, i));
                    RemixSceneBuilder.Require(clip != null && wave != null, d.title + " " + Files[i] + " imported");
                    if (reference == null) reference = clip;
                    RemixSceneBuilder.Require(clip.samples == reference.samples && clip.frequency == reference.frequency, d.title + " stems are the same length");
                    stems[i] = new StemMetadata { id = (StemId)i, label = Files[i], clip = clip, waveform = wave };
                }
                songs.Add(new SongMetadata
                {
                    id = d.id, title = d.title, bpm = d.bpm, beatOffset = d.beatOffset, seed = d.seed,
                    duration = (float)((double)reference.samples / reference.frequency), stems = stems,
                });
            }
            string selected = library.songs.Length > library.selectedIndex ? library.songs[library.selectedIndex].id : FirstSongId;
            library.songs = songs.ToArray();
            library.selectedIndex = Mathf.Max(0, songs.FindIndex(s => s.id == selected));
            EditorUtility.SetDirty(library);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Validate();
        }

        public static void Validate()
        {
            // Checks only what this tool sets up. (The full RemixSceneBuilder.Validate also flags an older, unrelated missing sprite.)
            var scene = EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager = RemixSceneBuilder.All<RemixTableManager>(scene).Single();
            var library = manager.library;
            RemixSceneBuilder.Require(library.songs.Length > 0 && library.songs[0].id == FirstSongId, "NIVIROTest stays first");
            foreach (var d in Songs)
            {
                var song = library.songs.SingleOrDefault(s => s.id == d.id);
                RemixSceneBuilder.Require(song != null && SongLibraryManager.HasAudio(song), d.title + " in library with audio");
                RemixSceneBuilder.Require(song.stems.Length == 4 && song.stems.Select(s => (int)s.id).OrderBy(x => x).SequenceEqual(new[] { 0, 1, 2, 3 }), d.title + " has all four stems");
                RemixSceneBuilder.Require(song.bpm >= 60 && song.bpm <= 200 && song.beatOffset >= 0 && song.beatOffset < 60f / song.bpm, d.title + " tempo");
                foreach (var stem in song.stems)
                {
                    RemixSceneBuilder.Require(stem.clip != null && stem.waveform != null && stem.waveform.SampleFrames == stem.clip.samples && stem.waveform.SampleRate == stem.clip.frequency, d.title + " " + stem.label + " waveform lines up");
                    var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(stem.clip));
                    var s = importer.defaultSampleSettings;
                    RemixSceneBuilder.Require(s.loadType == AudioClipLoadType.Streaming && s.compressionFormat == AudioCompressionFormat.Vorbis, d.title + " " + stem.label + " streams");
                }
            }
            RemixSceneBuilder.Require(library.songs.All(SongLibraryManager.HasAudio), "no silent placeholders left");
            Debug.Log($"REMIX_SONGS_PASS: {library.songs.Length} playable songs: " + string.Join(", ", library.songs.Select(s => $"{s.title} ({s.bpm} BPM, {s.duration:0.0}s)")));
        }
    }
}
