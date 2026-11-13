using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RemixTable.Editor
{
    public static class WaveformBaker
    {
        public const int BlockFrames=128;
        public const string CacheFolder="Assets/RemixTable/Waveforms";
        struct Bin { public float min,max; public double energy; public int samples; }
        // Streaming RIFF reader supports the project's IEEE float32 stereo WAVs and PCM16/24/32.
        // Only the small base envelope is retained, never the complete song's samples.
        public static void Bake(string source,string destination)
        {
            var bins=new List<Bin>(); int rate=0,channels=0,bits=0,format=0,frames=0;
            using(var r=new BinaryReader(File.OpenRead(source)))
            {
                if(new string(r.ReadChars(4))!="RIFF") throw new InvalidDataException("Expected RIFF");
                r.ReadInt32(); if(new string(r.ReadChars(4))!="WAVE") throw new InvalidDataException("Expected WAVE");
                while(r.BaseStream.Position+8<=r.BaseStream.Length)
                {
                    string id=new string(r.ReadChars(4)); uint size=r.ReadUInt32(); long next=r.BaseStream.Position+size+(size&1);
                    if(id=="fmt ")
                    {
                        format=r.ReadUInt16();channels=r.ReadUInt16();rate=r.ReadInt32();r.ReadInt32();r.ReadUInt16();bits=r.ReadUInt16();
                    }
                    else if(id=="data")
                    {
                        if(rate<=0 || channels<=0 || !((format==3 && bits==32) || (format==1 && (bits==16 || bits==24 || bits==32))))
                            throw new InvalidDataException("Unsupported WAV sample format");
                        frames=checked((int)(size/(channels*(bits/8))));
                        for(int frame=0;frame<frames;)
                        {
                            var bin=new Bin { min=float.PositiveInfinity,max=float.NegativeInfinity }; int count=Math.Min(BlockFrames,frames-frame);
                            for(int j=0;j<count*channels;j++)
                            {
                                float value;
                                if(format==3) value=r.ReadSingle();
                                else if(bits==16) value=r.ReadInt16()/32768f;
                                else if(bits==32) value=(float)(r.ReadInt32()/2147483648.0);
                                else { int v=r.ReadByte() | r.ReadByte()<<8 | r.ReadByte()<<16; value=((v<<8)>>8)/8388608f; }
                                if(float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Nonfinite WAV sample");
                                bin.min=Math.Min(bin.min,value);bin.max=Math.Max(bin.max,value);bin.energy+=(double)value*value;
                            }
                            bin.samples=count*channels; bins.Add(bin);frame+=count;
                        }
                    }
                    r.BaseStream.Position=next;
                }
            }
            if(bins.Count==0) throw new InvalidDataException("No samples");
            float peak=0;foreach(var bin in bins) peak=Math.Max(peak,Math.Max(-bin.min,bin.max));
            // Global encoding scale is reversed on lookup; this is NOT display normalization.
            float scale=Math.Max(peak,1e-9f)/32767;
            var levels=new List<List<Bin>>{bins};
            while(bins.Count>1)
            {
                var parent=new List<Bin>((bins.Count+1)/2);
                for(int i=0;i<bins.Count;i+=2)
                {
                    var a=bins[i];
                    if(i+1<bins.Count) { var b=bins[i+1];a.min=Math.Min(a.min,b.min);a.max=Math.Max(a.max,b.max);a.energy+=b.energy;a.samples+=b.samples; }
                    parent.Add(a);
                }
                levels.Add(parent);bins=parent;
            }
            byte[] hash;using(var sha=SHA256.Create()) using(var stream=File.OpenRead(source)) hash=sha.ComputeHash(stream);
            using(var w=new BinaryWriter(File.Create(destination)))
            {
                w.Write(0x31565752);w.Write(rate);w.Write(frames);w.Write(BlockFrames);w.Write(scale);w.Write(levels.Count);
                foreach(var level in levels) w.Write(level.Count);w.Write(hash);
                foreach(var level in levels) foreach(var bin in level)
                {
                    w.Write((short)Math.Max(-32767,Math.Floor(bin.min/scale)));
                    w.Write((short)Math.Min(32767,Math.Ceiling(bin.max/scale)));
                    w.Write((ushort)Math.Round(Math.Sqrt(bin.energy/bin.samples)/scale));
                }
            }
            Debug.Log($"WAVEFORM_BAKED {source}: {frames} frames at {rate}Hz, {levels.Count} levels, {new FileInfo(destination).Length} bytes");
        }
        [MenuItem("Tools/Remix Table/Bake Audio Waveforms")]
        public static void BakeAll()
        {
            RequireSavedScenes();
            Directory.CreateDirectory(CacheFolder);
            foreach(string file in new[]{"drums","bass","other","vocals"})
                Bake("Assets/RemixTable/Audio/NIVIROTest/Stems/"+file+".wav",CacheFolder+"/"+file+".rwave");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene=EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            var manager=UnityEngine.Object.FindFirstObjectByType<RemixTableManager>();
            string[] names={"drums","bass","other","vocals"};
            foreach(var stem in manager.library.songs[0].stems)
                stem.waveform=AssetDatabase.LoadAssetAtPath<WaveformCache>(CacheFolder+"/"+names[(int)stem.id]+".rwave");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Validate();
        }
        public static void Validate()
        {
            RequireSavedScenes();
            var scene=EditorSceneManager.OpenScene(RemixSceneBuilder.ScenePath);
            RemixSeatedLayout.Validate();RemixSeatedLayout.ValidateZoom();
            var manager=UnityEngine.Object.FindFirstObjectByType<RemixTableManager>();
            foreach(var stem in manager.library.songs[0].stems)
                RemixSceneBuilder.Require(stem.waveform!=null && stem.waveform.SampleFrames==stem.clip.samples && stem.waveform.SampleRate==stem.clip.frequency,"waveform aligned to "+stem.id);
            File.WriteAllText("Logs/RemixWaveformValidation.txt","COMPILATION_AND_SCENE_VALIDATION_PASS");
            Debug.Log("REMIX_WAVEFORM_VALIDATION_PASS");
        }
        static void RequireSavedScenes()
        {
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before baking/validating; unsaved scenes have been preserved.");
        }
    }
}
