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

    [Fact]
    public void BuildOutputParts_RemoveGap_CreatesOneFilePerSong()
    {
        var regions = new[]
        {
            new WaveformRegionMark(0, 12000),
            new WaveformRegionMark(12000, 18000, IsExcluded: true),
            new WaveformRegionMark(18000, 36000, NameSuffix: "-L"),
            new WaveformRegionMark(36000, 48000, NameSuffix: "-E"),
        };

        var parts = WaveformRegionBuilder.BuildOutputParts(regions, @"C:\music\master.wav");

        Assert.Equal(2, parts.Count);
        Assert.Equal("master_1.wav", parts[0].FileName);
        Assert.Equal("master_2.wav", parts[1].FileName);
        Assert.Equal(0, parts[0].StartSampleOffset);
        Assert.Equal(12000, parts[0].EndSampleOffset);
        Assert.Equal(18000, parts[1].StartSampleOffset);
        Assert.Equal(48000, parts[1].EndSampleOffset);
    }

    [Fact]
    public void BuildOutputParts_HeadAndTailExclude_SingleSong_KeepsOriginalName()
    {
        var regions = new[]
        {
            new WaveformRegionMark(0, 1000, IsExcluded: true),
            new WaveformRegionMark(1000, 12000),
            new WaveformRegionMark(12000, 36000, NameSuffix: "-L"),
            new WaveformRegionMark(36000, 45000, NameSuffix: "-E"),
            new WaveformRegionMark(45000, 48000, IsExcluded: true),
        };

        var parts = WaveformRegionBuilder.BuildOutputParts(regions, @"C:\music\master.wav");

        var part = Assert.Single(parts);
        Assert.Equal("master.wav", part.FileName);
        Assert.Equal(1000, part.StartSampleOffset);
        Assert.Equal(45000, part.EndSampleOffset);
    }

    [Fact]
    public void BuildOutputParts_HeadMidTailExclude_TwoSongs_UsesNumberedNames()
    {
        var regions = new[]
        {
            new WaveformRegionMark(0, 1000, IsExcluded: true),
            new WaveformRegionMark(1000, 12000),
            new WaveformRegionMark(12000, 15000, IsExcluded: true),
            new WaveformRegionMark(15000, 36000, NameSuffix: "-L"),
            new WaveformRegionMark(36000, 45000, NameSuffix: "-E"),
            new WaveformRegionMark(45000, 48000, IsExcluded: true),
        };

        var parts = WaveformRegionBuilder.BuildOutputParts(regions, @"C:\music\master.wav");

        Assert.Equal(2, parts.Count);
        Assert.Equal("master_1.wav", parts[0].FileName);
        Assert.Equal("master_2.wav", parts[1].FileName);
        Assert.Equal(1000, parts[0].StartSampleOffset);
        Assert.Equal(12000, parts[0].EndSampleOffset);
        Assert.Equal(15000, parts[1].StartSampleOffset);
        Assert.Equal(45000, parts[1].EndSampleOffset);
    }

    [Fact]
    public void ProjectExportFileNames_SingleRemainingPart_DropsNumberSuffix()
    {
        var parts = new[]
        {
            new WaveformOutputPart(
                2,
                18000,
                48000,
                "master_2.wav",
                @"C:\music\master.wav",
                18000,
                48000),
        };

        var projected = WaveformRegionBuilder.ProjectExportFileNames(
            parts,
            @"C:\music\master.wav",
            compactFileNumbers: false);

        var part = Assert.Single(projected);
        Assert.Equal("master.wav", part.FileName);
        Assert.Equal(2, part.Number);
        Assert.Equal(18000, part.StartSampleOffset);
    }

    [Fact]
    public void ProjectExportFileNames_TwoParts_CompactRenumbers()
    {
        var parts = new[]
        {
            new WaveformOutputPart(1, 0, 1000, "master_1.wav", @"C:\music\master.wav"),
            new WaveformOutputPart(3, 2000, 4000, "master_3.wav", @"C:\music\master.wav"),
        };

        var compact = WaveformRegionBuilder.ProjectExportFileNames(
            parts,
            @"C:\music\master.wav",
            compactFileNumbers: true);
        Assert.Equal("master_1.wav", compact[0].FileName);
        Assert.Equal("master_2.wav", compact[1].FileName);

        var raw = WaveformRegionBuilder.ProjectExportFileNames(
            parts,
            @"C:\music\master.wav",
            compactFileNumbers: false);
        Assert.Equal("master_1.wav", raw[0].FileName);
        Assert.Equal("master_3.wav", raw[1].FileName);
    }

    [Fact]
    public void SliceSegmentWavs_LeadingExclude_WritesOriginalNameAndSlices()
    {
        var work = Path.Combine(Path.GetTempPath(), "mga-media-headcut-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            const uint sampleRate = 48000;
            const int frameCount = 48000;
            const int headCut = 12000;
            var sourcePath = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(work, "song.wav"),
                sampleRate,
                frameCount);
            var outputDirectory = Path.Combine(work, "originals");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "song.wav"), "stale");
            var regions = new[]
            {
                new WaveformRegionMark(0, headCut, IsExcluded: true),
                new WaveformRegionMark(headCut, 36000),
                new WaveformRegionMark(36000, frameCount, NameSuffix: "-L"),
            };
            var parts = WaveformRegionBuilder.BuildOutputParts(regions, sourcePath);
            var projected = WaveformRegionBuilder.ProjectExportFileNames(
                parts,
                sourcePath,
                compactFileNumbers: false);
            var bars = new[]
            {
                new WaveformBarMark(0, 1, 120, 4, 4),
            };
            var plan = WwiseMusicPlanBuilder.Build(
                sourcePath: sourcePath,
                sampleRate: sampleRate,
                outputParts: projected,
                regions: regions,
                bars: bars,
                markers: Array.Empty<WaveformMarkerMark>(),
                outputDirectory: outputDirectory);
            var wavInfo = WavFileInfo.Read(sourcePath);

            var map = WaapiMusicImporter.SliceSegmentWavs(
                plan,
                sourcePath,
                outputDirectory,
                projected,
                sampleRate,
                blockAlign: 2,
                wavInfo,
                _ => { });

            var dest = Assert.Single(Directory.GetFiles(outputDirectory, "*.wav"));
            Assert.Equal("song.wav", Path.GetFileName(dest));
            Assert.Equal(frameCount - headCut, WavFileInfo.Read(dest).FrameCount);
            Assert.All(map.Values, binding =>
            {
                Assert.False(binding.ReusedOriginal);
                Assert.Equal("song.wav", Path.GetFileName(binding.WavPath));
            });
        }
        finally
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }
        }
    }

    [Fact]
    public void SliceSegmentWavs_XmlStyleTwoSongs_WritesOneWavPerPart()
    {
        var work = Path.Combine(Path.GetTempPath(), "mga-media-split-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            const uint sampleRate = 48000;
            const int frameCount = 48000;
            var sourcePath = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(work, "master.wav"),
                sampleRate,
                frameCount);
            var outputDirectory = Path.Combine(work, "originals");
            var regions = new[]
            {
                new WaveformRegionMark(0, 12000),
                new WaveformRegionMark(12000, 18000, IsExcluded: true),
                new WaveformRegionMark(18000, 24000),
                new WaveformRegionMark(24000, 36000, NameSuffix: "-L"),
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

            Assert.Equal(2, plan.Playlists.Count);
            var written = Directory.GetFiles(outputDirectory, "*.wav");
            Assert.Equal(2, written.Length);
            Assert.Contains(written, path => Path.GetFileName(path) == "master_1.wav");
            Assert.Contains(written, path => Path.GetFileName(path) == "master_2.wav");
            Assert.Equal(12000, WavFileInfo.Read(Path.Combine(outputDirectory, "master_1.wav")).FrameCount);
            Assert.Equal(30000, WavFileInfo.Read(Path.Combine(outputDirectory, "master_2.wav")).FrameCount);

            Assert.All(map.Values, binding => Assert.False(binding.ReusedOriginal));
            var song1 = map.Values.Single(binding =>
                Path.GetFileName(binding.WavPath) == "master_1.wav");
            Assert.Equal(0, song1.SourceStartSample);
            Assert.Equal(12000, song1.SourceEndSample);
            Assert.False(song1.ApplyClipTrim);

            var song2Intro = map.Values.Single(binding =>
                Path.GetFileName(binding.WavPath) == "master_2.wav" && binding.SourceStartSample == 0);
            var song2Loop = map.Values.Single(binding =>
                Path.GetFileName(binding.WavPath) == "master_2.wav" && binding.SourceStartSample == 6000);
            Assert.Equal(6000, song2Intro.SourceEndSample);
            Assert.Equal(30000, song2Loop.SourceEndSample);
            Assert.True(song2Intro.ApplyClipTrim);
            Assert.True(song2Loop.ApplyClipTrim);
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
