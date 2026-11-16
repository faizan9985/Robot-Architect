using System;
using System.IO;
using UnityEngine;
namespace RemixTable
{
    // Streams 16-bit PCM WAV to disk. The header is written with placeholder sizes and patched on Close, so
    // the file is valid as soon as it is closed, however long the recording was.
    public sealed class WavWriter : IDisposable
    {
        readonly FileStream stream;
        readonly BinaryWriter writer;
        byte[] buffer = new byte[0];
        public int SampleRate { get; }
        public int Channels { get; }
        public long SamplesWritten { get; private set; } // Per channel (frames).
        public float Seconds => SampleRate > 0 ? (float)SamplesWritten / SampleRate : 0;
        public string Path { get; }
        public WavWriter(string path, int sampleRate, int channels)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            Path = path; SampleRate = sampleRate; Channels = channels;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            writer = new BinaryWriter(stream);
            WriteHeader(0);
        }
        void WriteHeader(long dataBytes)
        {
            int data = (int)Math.Min(dataBytes, int.MaxValue - 36);
            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + data);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);                          // PCM fmt chunk size
            writer.Write((short)1);                    // PCM
            writer.Write((short)Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * Channels * 2);   // Byte rate
            writer.Write((short)(Channels * 2));       // Block align
            writer.Write((short)16);                   // Bits per sample
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(data);
        }
        // Interleaved float samples in [-1, 1]; values outside are clipped.
        public void Write(float[] interleaved, int count)
        {
            count = Math.Min(count, interleaved.Length);
            count -= count % Channels;
            if (count <= 0) return;
            if (buffer.Length < count * 2) buffer = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                short s = (short)Mathf.RoundToInt(Mathf.Clamp(interleaved[i], -1, 1) * 32767);
                buffer[i * 2] = (byte)s; buffer[i * 2 + 1] = (byte)(s >> 8);
            }
            writer.Write(buffer, 0, count * 2);
            SamplesWritten += count / Channels;
        }
        public void Dispose()
        {
            if (!stream.CanWrite) return;
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            WriteHeader(SamplesWritten * Channels * 2);
            writer.Flush();
            writer.Dispose();
        }
    }
}
