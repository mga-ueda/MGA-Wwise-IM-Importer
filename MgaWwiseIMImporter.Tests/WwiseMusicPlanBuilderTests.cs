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
}
