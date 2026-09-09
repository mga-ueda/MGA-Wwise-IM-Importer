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

    /// <summary>WAVE_FORMAT_EXTENSIBLE の 16-bit mono（整数 PCM サブタイプ）。</summary>
    public static string WriteSilentExtensiblePcm16Mono(string path, uint sampleRate, int frameCount)
    {
        var dataBytes = frameCount * 2;
        const uint fmtSize = 40;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(unchecked((uint)(4 + 8 + fmtSize + 8 + dataBytes)));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(fmtSize);
        writer.Write((ushort)65534); // Extensible
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2u);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write((ushort)22); // cbSize
        writer.Write((ushort)16); // valid bits
        writer.Write(0x4u); // SPEAKER_FRONT_CENTER
        writer.Write((uint)1); // KSDATAFORMAT_SUBTYPE_PCM
        writer.Write((ushort)0);
        writer.Write((ushort)0x0010);
        writer.Write((byte)0x80);
        writer.Write((byte)0x00);
        writer.Write((byte)0x00);
        writer.Write((byte)0xAA);
        writer.Write((byte)0x00);
        writer.Write((byte)0x38);
        writer.Write((byte)0x9B);
        writer.Write((byte)0x71);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(unchecked((uint)dataBytes));
        writer.Write(new byte[dataBytes]);

        return path;
    }

    public static string WriteSilentIeeeFloat32Mono(string path, uint sampleRate, int frameCount)
    {
        var dataBytes = frameCount * 4;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(unchecked((uint)(36 + dataBytes)));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16u);
        writer.Write((ushort)3); // IEEE float
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 4u);
        writer.Write((ushort)4);
        writer.Write((ushort)32);

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
