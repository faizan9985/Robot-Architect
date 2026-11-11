using System;
using System.IO;
using System.Text;

namespace RemixTable.Dev.Editor
{
    /// <summary>Writes 16-bit PCM mono WAV files.</summary>
    static class WavFile
    {
        public static void WriteMono16(string path, float[] samples, int sampleRate)
        {
            int dataBytes = samples.Length * 2;
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);                 // fmt chunk size
            writer.Write((short)1);           // PCM
            writer.Write((short)1);           // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);     // byte rate
            writer.Write((short)2);           // block align
            writer.Write((short)16);          // bits per sample

            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                float s = Math.Max(-1f, Math.Min(1f, samples[i]));
                writer.Write((short)Math.Round(s * short.MaxValue));
            }
        }
    }
}
