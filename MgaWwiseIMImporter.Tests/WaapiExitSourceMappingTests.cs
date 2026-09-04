using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Tests;

public class WaapiExitSourceMappingTests
{
    [Theory]
    [InlineData(nameof(PlaylistExitSourceMode.Immediate), 0)]
    [InlineData(nameof(PlaylistExitSourceMode.NextBar), 2)]
    [InlineData(nameof(PlaylistExitSourceMode.NextBeat), 3)]
    [InlineData(nameof(PlaylistExitSourceMode.NextCue), 4)]
    [InlineData(nameof(PlaylistExitSourceMode.ExitCue), 7)]
    public void ToWaapiExitSourceAt_MatchesKnownWwiseValues(string modeName, int expected)
    {
        var mode = Enum.Parse<PlaylistExitSourceMode>(modeName);
        Assert.Equal(expected, WaapiMusicTransitionDefaults.ToWaapiExitSourceAt(mode));
        Assert.Equal(expected, WaapiMusicTransitionDefaults.ToWaapiMusicSyncType(mode));
    }

    [Fact]
    public void BuildTransitionRoot_DefaultAnyToAnyUsesImmediate()
    {
        var playlists = new[]
        {
            new WwisePlaylistPlan
            {
                Name = "P1",
                StateName = "P1",
                SourceWavPath = @"C:\tmp\a.wav",
                SourcePartNumbers = [1],
                ExitSourceAt = PlaylistExitSourceMode.NextBar,
                FadeInSeconds = 0,
                FadeOutSeconds = 0,
                FadeInCurve = RegionFadeCurveKind.SCurve,
                FadeOutCurve = RegionFadeCurveKind.SCurve,
                PlayPostExit = true,
                Segments = [],
            },
        };

        var root = WaapiMusicTransitionDefaults.BuildTransitionRoot(@"\\Container", playlists);
        var children = Assert.IsType<List<object>>(root["children"]);
        Assert.True(children.Count >= 2);

        var anyToAny = Assert.IsType<Dictionary<string, object?>>(children[0]);
        Assert.Equal(0, anyToAny["@ExitSourceAt"]);

        var anyToPlaylist = Assert.IsType<Dictionary<string, object?>>(children[1]);
        Assert.Equal(2, anyToPlaylist["@ExitSourceAt"]);
        Assert.Equal(@"\\Container\P1", anyToPlaylist["@DestinationContextObject"]);
    }
}
