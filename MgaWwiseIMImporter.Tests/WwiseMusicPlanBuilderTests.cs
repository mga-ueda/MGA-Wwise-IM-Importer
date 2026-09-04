using System.IO;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Tests;

public class WwiseMusicPlanBuilderTests
{
    [Fact]
    public void Build_SinglePart_CreatesOnePlaylistAndSegment()
    {
        const uint sampleRate = 48000;
        var parts = new[]
        {
            new WaveformOutputPart(1, 0, 48000, "song_1.wav"),
        };
        var regions = new[]
        {
            new WaveformRegionMark(0, 48000),
        };
        var bars = new[]
        {
            new WaveformBarMark(0, 1, 120, 4, 4),
        };

        var plan = WwiseMusicPlanBuilder.Build(
            sourcePath: @"C:\music\song.wav",
            sampleRate: sampleRate,
            outputParts: parts,
            regions: regions,
            bars: bars,
            markers: Array.Empty<WaveformMarkerMark>());

        Assert.Equal("song", plan.ContainerName);
        Assert.False(plan.IsMultiPart);
        Assert.Single(plan.Playlists);

        var playlist = plan.Playlists[0];
        Assert.Equal("song", playlist.Name);
        Assert.Equal("song", playlist.StateName);
        Assert.False(playlist.UsesFallbackStateName);
        Assert.Equal(PlaylistExitSourceMode.Immediate, playlist.ExitSourceAt);
        Assert.Single(playlist.Segments);
        Assert.False(playlist.Segments[0].LoopInfinite);
        Assert.Equal(120, playlist.Segments[0].TempoBpm);
        Assert.Equal(4, playlist.Segments[0].TimeSignatureUpper);
    }

    [Fact]
    public void Build_LoopSuffix_SetsLoopInfinite()
    {
        const uint sampleRate = 48000;
        var parts = new[]
        {
            new WaveformOutputPart(1, 0, 48000, "loop_1.wav"),
        };
        var regions = new[]
        {
            new WaveformRegionMark(0, 24000, NameSuffix: "-L"),
            new WaveformRegionMark(24000, 48000, NameSuffix: "-E", IsAutoNameSuffix: true),
        };
        var bars = new[]
        {
            new WaveformBarMark(0, 1, 100, 4, 4),
        };

        var plan = WwiseMusicPlanBuilder.Build(
            sourcePath: @"C:\music\loop.wav",
            sampleRate: sampleRate,
            outputParts: parts,
            regions: regions,
            bars: bars,
            markers: Array.Empty<WaveformMarkerMark>());

        var segment = Assert.Single(plan.Playlists[0].Segments);
        Assert.True(segment.LoopInfinite);
        Assert.True(segment.EntryCueMs < segment.ExitCueMs);
    }

    [Fact]
    public void Build_RejectsZeroSampleRate()
    {
        Assert.Throws<ArgumentException>(() =>
            WwiseMusicPlanBuilder.Build(
                sourcePath: "x.wav",
                sampleRate: 0,
                outputParts: [new WaveformOutputPart(1, 0, 100, "x_1.wav")],
                regions: [new WaveformRegionMark(0, 100)],
                bars: [],
                markers: []));
    }

    [Fact]
    public void Build_UsesPartExitSourceOverride()
    {
        const uint sampleRate = 48000;
        var plan = WwiseMusicPlanBuilder.Build(
            sourcePath: @"C:\music\a.wav",
            sampleRate: sampleRate,
            outputParts: [new WaveformOutputPart(1, 0, 1000, "a_1.wav")],
            regions: [new WaveformRegionMark(0, 1000)],
            bars: [new WaveformBarMark(0, 1, 120, 4, 4)],
            markers: [],
            partExitSourceModes: new Dictionary<int, PlaylistExitSourceMode>
            {
                [1] = PlaylistExitSourceMode.ExitCue,
            });

        Assert.Equal(PlaylistExitSourceMode.ExitCue, plan.Playlists[0].ExitSourceAt);
    }

    [Fact]
    public void Build_AsciiFileNames_KeepPlaylistNameAsStateName()
    {
        var plan = BuildTwoPartPlan(
            @"C:\music\song.wav",
            "intro.wav",
            "loop.wav");

        Assert.True(plan.IsMultiPart);
        Assert.Equal("intro", plan.Playlists[0].Name);
        Assert.Equal("intro", plan.Playlists[0].StateName);
        Assert.Equal("loop", plan.Playlists[1].Name);
        Assert.Equal("loop", plan.Playlists[1].StateName);
        Assert.False(plan.Playlists[0].UsesFallbackStateName);
    }

    [Fact]
    public void Build_AnyTwoByteFileName_UsesMusicFallbackStateNames()
    {
        var plan = BuildTwoPartPlan(
            @"C:\music\ジングル03（謎解き）.wav",
            "ジングル03（謎解き）.wav",
            "jingle04.wav");

        Assert.Equal("ジングル03（謎解き）", plan.Playlists[0].Name);
        Assert.Equal("jingle04", plan.Playlists[1].Name);
        Assert.Equal("Music_1", plan.Playlists[0].StateName);
        Assert.Equal("Music_2", plan.Playlists[1].StateName);
        Assert.True(plan.Playlists[0].UsesFallbackStateName);
        Assert.True(plan.Playlists[1].UsesFallbackStateName);
    }

    [Fact]
    public void Build_TenTwoBytePlaylists_PadsStateNamesToTwoDigits()
    {
        var plan = BuildMultiPartPlan(
            @"C:\music\曲.wav",
            Enumerable.Range(1, 10)
                .Select(i => $"曲{i:00}.wav")
                .ToArray());

        Assert.Equal(10, plan.Playlists.Count);
        Assert.Equal("Music_01", plan.Playlists[0].StateName);
        Assert.Equal("Music_10", plan.Playlists[9].StateName);
        Assert.Equal("曲01", plan.Playlists[0].Name);
    }

    private static WwiseMusicPlan BuildTwoPartPlan(
        string sourcePath,
        string fileName1,
        string fileName2) =>
        BuildMultiPartPlan(sourcePath, [fileName1, fileName2]);

    private static WwiseMusicPlan BuildMultiPartPlan(string sourcePath, string[] fileNames)
    {
        const uint sampleRate = 48000;
        var parts = new WaveformOutputPart[fileNames.Length];
        var regions = new WaveformRegionMark[fileNames.Length];
        var overrides = new Dictionary<int, string>();
        for (var i = 0; i < fileNames.Length; i++)
        {
            var start = i * 48000L;
            var end = start + 48000L;
            var number = i + 1;
            parts[i] = new WaveformOutputPart(
                number,
                start,
                end,
                fileNames[i],
                Path.Combine(@"C:\music", fileNames[i]));
            regions[i] = new WaveformRegionMark(start, end);
            overrides[number] = Path.GetFileNameWithoutExtension(fileNames[i]);
        }

        return WwiseMusicPlanBuilder.Build(
            sourcePath: sourcePath,
            sampleRate: sampleRate,
            outputParts: parts,
            regions: regions,
            bars: [new WaveformBarMark(0, 1, 120, 4, 4)],
            markers: [],
            playlistNameOverrides: overrides);
    }
}
