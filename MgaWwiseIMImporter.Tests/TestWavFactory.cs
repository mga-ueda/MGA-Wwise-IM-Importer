using System.IO;
using System.Text;

namespace MgaWwiseIMImporter.Tests;

/// <summary>最小の PCM WAV を一時ファイルとして作る。</summary>
internal static class TestWavFactory
{
    public static string WriteSilentPcm16Mono(string path, uint sampleRate, int frameCount)
    {
        var dataBytes = frameCount * 2; // 16-bit mono
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(unchecked((uint)(36 + dataBytes)));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16u);
        writer.Write((ushort)1); // PCM
        writer.Write((ushort)1); // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2u); // byte rate
        writer.Write((ushort)2); // block align
        writer.Write((ushort)16); // bits

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(unchecked((uint)dataBytes));
        writer.Write(new byte[dataBytes]);

        return path;
    }

    public static string WritePcm16Mono(string path, uint sampleRate, short[] frames)
    {
        var dataBytes = frames.Length * 2;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(unchecked((uint)(36 + dataBytes)));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16u);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2u);
        writer.Write((ushort)2);
        writer.Write((ushort)16);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(unchecked((uint)dataBytes));
        foreach (var sample in frames)
        {
            writer.Write(sample);
        }

        return path;
    }

    public static string WritePcm16Stereo(string path, uint sampleRate, (short Left, short Right)[] frames)
    {
        var dataBytes = frames.Length * 4;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(unchecked((uint)(36 + dataBytes)));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16u);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 4u);
        writer.Write((ushort)4);
        writer.Write((ushort)16);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(unchecked((uint)dataBytes));
        foreach (var (left, right) in frames)
        {
            writer.Write(left);
            writer.Write(right);
        }

        return path;
    }

    public static string WriteInvalidRiff(string path)
    {
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("XXXX....NOTWAVE"));
        return path;
    }
}
