using System.IO;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public class WavRiffTests
{
    [Fact]
    public void WavFileInfo_Read_ValidPcmWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mga-riff-{Guid.NewGuid():N}.wav");
        try
        {
            TestWavFactory.WriteSilentPcm16Mono(path, sampleRate: 48000, frameCount: 480);
            var info = WavFileInfo.Read(path);

            Assert.Equal(path, info.Path);
            Assert.Equal((ushort)1, info.AudioFormat);
            Assert.Equal((ushort)1, info.Channels);
            Assert.Equal(48000u, info.SampleRate);
            Assert.Equal((ushort)16, info.BitsPerSample);
            Assert.Equal(960u, info.DataSizeBytes);
            Assert.Equal(480, info.FrameCount);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void WavFileInfo_Read_RejectsNonRiff()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mga-bad-{Guid.NewGuid():N}.bin");
        try
        {
            TestWavFactory.WriteInvalidRiff(path);
            var ex = Assert.Throws<InvalidDataException>(() => WavFileInfo.Read(path));
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void WavRiff_ReadFourCc_ReadsAscii()
    {
        using var stream = new MemoryStream("RIFF"u8.ToArray());
        using var reader = new BinaryReader(stream);
        Assert.Equal("RIFF", WavRiff.ReadFourCc(reader));
    }
}
