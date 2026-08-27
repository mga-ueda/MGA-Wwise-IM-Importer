using System.IO;
using MgaWwiseIMImporter.Wave;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Tests;

public class WaapiMediaReuseTests
{
    [Fact]
    public void BuildOutputParts_SingleRun_AttachesDedicatedSourceAndKeepsOriginalName()
    {
        var regions = new[]
        {
            new WaveformRegionMark(0, 24000),
            new WaveformRegionMark(24000, 48000, NameSuffix: "-L"),
            new WaveformRegionMark(48000, 60000, NameSuffix: "-E"),
        };

        var parts = WaveformRegionBuilder.BuildOutputParts(regions, @"C:\music\春の街.wav");

        var part = Assert.Single(parts);
        Assert.Equal("春の街.wav", part.FileName);
        Assert.True(part.HasDedicatedSource);
        Assert.Equal(@"C:\music\春の街.wav", part.SourcePath);
        Assert.Equal(0, part.LocalStartSample);
        Assert.Equal(60000, part.LocalEndSample);
    }

    [Fact]
    public void SliceSegmentWavs_IntroAndLoop_CopiesOneMasterAndAppliesClipTrim()
    {
        var work = Path.Combine(Path.GetTempPath(), "mga-media-reuse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            const uint sampleRate = 48000;
            const int frameCount = 48000;
            var sourcePath = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(work, "song.wav"),
                sampleRate,
                frameCount);
            var outputDirectory = Path.Combine(work, "originals");
            var regions = new[]
            {
                new WaveformRegionMark(0, 12000),
                new WaveformRegionMark(12000, 36000, NameSuffix: "-L"),
                new WaveformRegionMark(36000, frameCount, NameSuffix: "-E"),
            };
            var parts = WaveformRegionBuilder.BuildOutputParts(regions, sourcePath);
            var bars = new[]
            {
                new WaveformBarMark(0, 1, 120, 4, 4),
            };
            var plan = WwiseMusicPlanBuilder.Build(
                sourcePath: sourcePath,
                sampleRate: sampleRate,
                outputParts: parts,
                regions: regions,
                bars: bars,
                markers: Array.Empty<WaveformMarkerMark>(),
                outputDirectory: outputDirectory);
            var wavInfo = WavFileInfo.Read(sourcePath);

            var map = WaapiMusicImporter.SliceSegmentWavs(
                plan,
                sourcePath,
                outputDirectory,
                parts,
                sampleRate,
                blockAlign: 2,
                wavInfo,
                _ => { });

            Assert.Equal(2, plan.Playlists[0].Segments.Count);
            Assert.Equal(2, map.Count);
            Assert.Single(
                map.Values.Select(binding => binding.WavPath).Distinct(StringComparer.OrdinalIgnoreCase));
            Assert.All(map.Values, binding =>
            {
                Assert.True(binding.ReusedOriginal);
                Assert.True(binding.ApplyClipTrim);
                Assert.Equal(frameCount, binding.SourceFrameCount);
            });

            var dest = map.Values.First().WavPath;
            Assert.Equal("song.wav", Path.GetFileName(dest));
            Assert.True(File.Exists(dest));
            Assert.Equal(new FileInfo(sourcePath).Length, new FileInfo(dest).Length);
            Assert.Single(Directory.GetFiles(outputDirectory, "*.wav"));

            var intro = map.Values.Single(binding => binding.SourceStartSample == 0);
            var loop = map.Values.Single(binding => binding.SourceStartSample == 12000);
            Assert.Equal(12000, intro.SourceEndSample);
            Assert.Equal(frameCount, loop.SourceEndSample);
        }
        finally
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }
        }
    }
}
