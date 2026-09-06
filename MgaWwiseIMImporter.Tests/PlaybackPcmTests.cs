using System.IO;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public class PlaybackPcmTests
{
    [Fact]
    public void FromWave_DecodesMonoToStereo()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mga-pcm-mono-{Guid.NewGuid():N}.wav");
        try
        {
            TestWavFactory.WritePcm16Mono(path, 48000, [16384, -16384]);
            var pcm = PlaybackPcm.FromWave(WavFileInfo.Read(path));

            Assert.Equal(48000, pcm.SampleRate);
            Assert.Equal(2, pcm.FrameCount);
            Assert.Equal(4, pcm.InterleavedStereo.Length);
            Assert.Equal(0.5f, pcm.InterleavedStereo[0], 4);
            Assert.Equal(0.5f, pcm.InterleavedStereo[1], 4);
            Assert.Equal(-0.5f, pcm.InterleavedStereo[2], 4);
            Assert.Equal(-0.5f, pcm.InterleavedStereo[3], 4);
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
    public void FromWave_KeepsStereoChannels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mga-pcm-stereo-{Guid.NewGuid():N}.wav");
        try
        {
            TestWavFactory.WritePcm16Stereo(path, 48000, [(16384, -16384)]);
            var pcm = PlaybackPcm.FromWave(WavFileInfo.Read(path));

            Assert.Equal(1, pcm.FrameCount);
            Assert.Equal(0.5f, pcm.InterleavedStereo[0], 4);
            Assert.Equal(-0.5f, pcm.InterleavedStereo[1], 4);
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
    public void FromSpans_ConcatenatesInOrder()
    {
        var first = Path.Combine(Path.GetTempPath(), $"mga-pcm-a-{Guid.NewGuid():N}.wav");
        var second = Path.Combine(Path.GetTempPath(), $"mga-pcm-b-{Guid.NewGuid():N}.wav");
        try
        {
            TestWavFactory.WritePcm16Mono(first, 48000, [16384]);
            TestWavFactory.WritePcm16Mono(second, 48000, [-16384]);
            var infoA = WavFileInfo.Read(first);
            var infoB = WavFileInfo.Read(second);
            var pcm = PlaybackPcm.FromSpans(
            [
                new WaveformSourceSpan(first, infoA, 0, infoA.FrameCount),
                new WaveformSourceSpan(second, infoB, infoA.FrameCount, infoB.FrameCount),
            ]);

            Assert.Equal(2, pcm.FrameCount);
            Assert.Equal(0.5f, pcm.InterleavedStereo[0], 4);
            Assert.Equal(-0.5f, pcm.InterleavedStereo[2], 4);
        }
        finally
        {
            if (File.Exists(first))
            {
                File.Delete(first);
            }

            if (File.Exists(second))
            {
                File.Delete(second);
            }
        }
    }

    [Fact]
    public void Playhead_ReadsThenAdvances()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mga-pcm-head-{Guid.NewGuid():N}.wav");
        try
        {
            TestWavFactory.WritePcm16Mono(path, 48000, [16384, -16384]);
            var pcm = PlaybackPcm.FromWave(WavFileInfo.Read(path));
            var head = new PcmPlayhead(pcm);
            var buffer = new byte[16];

            Assert.Equal(1, head.ReadFrames(buffer, 0, 1));
            Assert.Equal(1, head.Sample);
            Assert.Equal(0.5f, BitConverter.ToSingle(buffer, 0), 4);

            head.SeekToSample(0);
            Assert.Equal(0, head.Sample);
            Assert.Equal(2, head.ReadFrames(buffer, 0, 4));
            Assert.Equal(2, head.Sample);
            Assert.Equal(0, head.ReadFrames(buffer, 0, 1));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
