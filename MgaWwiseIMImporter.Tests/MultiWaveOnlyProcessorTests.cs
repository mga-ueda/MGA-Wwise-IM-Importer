using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public class MultiWaveOnlyProcessorTests
{
    [Fact]
    public void AreCompatible_PcmAndExtensibleSameLayout_ReturnsTrue()
    {
        var pcm = CreateInfo(audioFormat: 1, sampleRate: 48000, channels: 2, bits: 24, blockAlign: 6);
        var extensible = CreateInfo(audioFormat: 65534, sampleRate: 48000, channels: 2, bits: 24, blockAlign: 6);

        Assert.True(MultiWaveOnlyProcessor.AreCompatibleForMultiWave(pcm, extensible));
        Assert.True(MultiWaveOnlyProcessor.AreCompatibleForMultiWave(extensible, pcm));
    }

    [Fact]
    public void AreCompatible_DifferentSampleRate_ReturnsFalse()
    {
        var a = CreateInfo(audioFormat: 1, sampleRate: 48000, channels: 2, bits: 24, blockAlign: 6);
        var b = CreateInfo(audioFormat: 1, sampleRate: 44100, channels: 2, bits: 24, blockAlign: 6);

        Assert.False(MultiWaveOnlyProcessor.AreCompatibleForMultiWave(a, b));
    }

    [Fact]
    public void AreCompatible_PcmAndIeeeFloat_ReturnsFalse()
    {
        var pcm = CreateInfo(audioFormat: 1, sampleRate: 48000, channels: 2, bits: 32, blockAlign: 8);
        var ieee = CreateInfo(audioFormat: 3, sampleRate: 48000, channels: 2, bits: 32, blockAlign: 8);

        Assert.False(MultiWaveOnlyProcessor.AreCompatibleForMultiWave(pcm, ieee));
    }

    [Fact]
    public void AreCompatible_Packed24VsPadded32_ReturnsFalse()
    {
        var packed = CreateInfo(audioFormat: 1, sampleRate: 48000, channels: 2, bits: 24, blockAlign: 6);
        var padded = CreateInfo(audioFormat: 65534, sampleRate: 48000, channels: 2, bits: 24, blockAlign: 8);

        Assert.False(MultiWaveOnlyProcessor.AreCompatibleForMultiWave(packed, padded));
    }

    [Fact]
    public void FormatDetail_IncludesAudioFormatTag()
    {
        var text = UiStrings.LogMultiWaveOnlyFormatDetail(
            48000, 2, 24, 65534,
            48000, 2, 24, 1);

        Assert.Contains("48000 Hz / 2 ch / 24 bit / Extensible (65534)", text);
        Assert.Contains("48000 Hz / 2 ch / 24 bit / PCM (1)", text);
    }

    private static WavFileInfo CreateInfo(
        ushort audioFormat,
        uint sampleRate,
        ushort channels,
        ushort bits,
        ushort blockAlign) =>
        new()
        {
            Path = "test.wav",
            FileSizeBytes = 1,
            AudioFormat = audioFormat,
            Channels = channels,
            SampleRate = sampleRate,
            ByteRate = sampleRate * blockAlign,
            BlockAlign = blockAlign,
            BitsPerSample = bits,
            DataSizeBytes = blockAlign,
            HasIXml = false,
            TimeReferenceSamples = 0,
        };
}
