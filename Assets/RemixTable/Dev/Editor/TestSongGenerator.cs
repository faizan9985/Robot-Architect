using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RemixTable.Dev.Editor
{
    /// <summary>
    /// Tools > Remix Table > Dev > Generate Test Song. Synthesises a 120 BPM, 4/4, 8-bar song as 48 kHz 16-bit
    /// mono WAVs (Drums, Bass, Keys, Lead, FX, all exactly the same sample count) plus a 2-bar Riser and a Hit
    /// one-shot, then creates or updates its SongDefinition. Only ever writes inside <see cref="Folder"/>; re-running
    /// overwrites those files in place (asset GUIDs are kept, so scenes keep their references).
    /// </summary>
    public static class TestSongGenerator
    {
        public const string Folder = "Assets/RemixTable/Dev/TestSong";
        public const string SongAssetPath = Folder + "/TestSong.asset";
        public const string RiserPath = Folder + "/Riser.wav";
        public const string HitPath = Folder + "/Hit.wav";

        public const int SampleRate = 48000;
        public const int Bpm = 120;
        public const int BeatsPerBar = 4;
        public const int Bars = 8;

        [MenuItem("Tools/Remix Table/Dev/Generate Test Song")]
        static void GenerateMenu()
        {
            SongDefinition song = Generate();
            if (song == null) return;
            Selection.activeObject = song;
            EditorGUIUtility.PingObject(song);
        }

        public static SongDefinition Generate()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Remix Table", "Synthesising test song…", 0.1f);
                EnsureFolder();

                var layout = new TestSongSynth.Layout
                {
                    SampleRate = SampleRate,
                    SamplesPerBeat = SampleRate * 60 / Bpm,
                    BeatsPerBar = BeatsPerBar,
                    Bars = Bars,
                };

                var stems = new (string name, StemRole role, Color color, Func<TestSongSynth.Layout, float[]> synth)[]
                {
                    ("Drums", StemRole.Drums, new Color(1f, 0.38f, 0.25f), TestSongSynth.Drums),
                    ("Bass", StemRole.Bass, new Color(0.55f, 0.38f, 1f), TestSongSynth.Bass),
                    ("Keys", StemRole.Keys, new Color(0.2f, 0.85f, 0.75f), TestSongSynth.Keys),
                    ("Lead", StemRole.Lead, new Color(1f, 0.85f, 0.25f), TestSongSynth.Lead),
                    ("FX", StemRole.FX, new Color(1f, 0.42f, 0.8f), TestSongSynth.Fx),
                };

                var paths = new List<string>();
                for (int i = 0; i < stems.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Remix Table", $"Synthesising {stems[i].name}…", 0.1f + 0.12f * i);
                    float[] data = stems[i].synth(layout);
                    if (data.Length != layout.TotalSamples)
                        throw new InvalidOperationException($"{stems[i].name} has {data.Length} samples, expected {layout.TotalSamples}.");
                    string path = $"{Folder}/{stems[i].name}.wav";
                    WavFile.WriteMono16(path, data, SampleRate);
                    paths.Add(path);
                }

                EditorUtility.DisplayProgressBar("Remix Table", "Synthesising riser and hit…", 0.75f);
                WavFile.WriteMono16(RiserPath, TestSongSynth.Riser(layout.SamplesPerBar * 2, SampleRate), SampleRate);
                WavFile.WriteMono16(HitPath, TestSongSynth.Hit((int)(1.6 * SampleRate), SampleRate), SampleRate);
                paths.Add(RiserPath);
                paths.Add(HitPath);

                EditorUtility.DisplayProgressBar("Remix Table", "Importing…", 0.85f);
                foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var definitions = new List<StemDefinition>();
                for (int i = 0; i < stems.Length; i++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
                    if (clip == null) throw new InvalidOperationException($"Couldn't load {paths[i]} after import.");
                    definitions.Add(new StemDefinition(stems[i].name, stems[i].role, clip, stems[i].color));
                }

                var song = AssetDatabase.LoadAssetAtPath<SongDefinition>(SongAssetPath);
                if (song == null)
                {
                    song = ScriptableObject.CreateInstance<SongDefinition>();
                    AssetDatabase.CreateAsset(song, SongAssetPath);
                }
                song.EditorConfigure("Remix Table Test Song", "Remix Table (generated)", "Generated test content", "",
                    Bpm, BeatsPerBar, Bars, definitions);
                EditorUtility.SetDirty(song);
                AssetDatabase.SaveAssets();

                if (!song.TryValidate(out string errors, out string warnings) || warnings.Length > 0)
                    RemixLog.Warn($"Test song generated with problems:\n{errors}{warnings}", song);
                else
                    RemixLog.Info($"Test song generated: {stems.Length} stems × {layout.TotalSamples} samples " +
                                  $"({layout.TotalSamples / (double)SampleRate:F1} s) in {Folder}.", song);
                return song;
            }
            catch (Exception e)
            {
                RemixLog.Error($"Generating the test song failed: {e}");
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void EnsureFolder()
        {
            const string parent = "Assets/RemixTable/Dev";
            if (!AssetDatabase.IsValidFolder(parent)) throw new InvalidOperationException($"{parent} is missing.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(parent, "TestSong");
            Directory.CreateDirectory(Folder);
        }
    }

    /// <summary>Import settings for the generated clips only (nothing outside the TestSong folder is touched):
    /// Force To Mono, Decompress On Load, PCM (lossless, so the dev reference is exact).</summary>
    sealed class TestSongAudioPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(TestSongGenerator.Folder + "/", StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.ambisonic = false;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;

            importer.ClearSampleSettingOverride("Android");
            importer.ClearSampleSettingOverride("Standalone");
        }
    }
}
