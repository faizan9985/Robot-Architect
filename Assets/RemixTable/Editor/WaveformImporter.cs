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
    [ScriptedImporter(1,"rwave")]
    public sealed class WaveformImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            using(var r=new BinaryReader(File.OpenRead(ctx.assetPath)))
            {
                if(r.ReadInt32()!=0x31565752) throw new InvalidDataException("Unsupported waveform cache");
                int rate=r.ReadInt32(), frames=r.ReadInt32(), block=r.ReadInt32(); float scale=r.ReadSingle();
                int levels=r.ReadInt32(); var offsets=new int[levels]; var counts=new int[levels];
                int size=0;
                for(int i=0;i<levels;i++) { offsets[i]=size; counts[i]=r.ReadInt32(); size+=counts[i]*6; }
                // SHA256 is provenance for the complete original WAV (including its header).
                r.ReadBytes(32);
                var data=r.ReadBytes(size);
                if(data.Length!=size || r.BaseStream.Position!=r.BaseStream.Length) throw new InvalidDataException("Truncated waveform cache");
                var asset=ScriptableObject.CreateInstance<WaveformCache>();
                asset.Initialize(rate,frames,block,scale,data,offsets,counts);
                ctx.AddObjectToAsset("waveform",asset);ctx.SetMainObject(asset);
            }
        }
    }


}