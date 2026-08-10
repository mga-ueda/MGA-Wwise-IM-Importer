using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public class RegionSuffixTests
{
    [Theory]
    [InlineData("-L")]
    [InlineData("-l")]
    public void LoopLeftSuffix_IsRecognizedCaseInsensitive(string suffix)
    {
        Assert.Equal("-L", WaveformRegionBuilder.LoopLeftSuffix);
        Assert.Equal(0, string.Compare(suffix, WaveformRegionBuilder.LoopLeftSuffix, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void KnownSuffixConstants_MatchMarkerConventions()
    {
        Assert.Equal("-R", WaveformRegionBuilder.ExcludeRangeSuffix);
        Assert.Equal("-L", WaveformRegionBuilder.LoopLeftSuffix);
        Assert.Equal("-E", WaveformRegionBuilder.LoopEndSuffix);
        Assert.Equal("-A", WaveformRegionBuilder.AnacrusisSuffix);
    }

    [Fact]
    public void CycleComment_EndsWithSuffix_MatchesHelpersViaPublicBuildContract()
    {
        Assert.EndsWith(WaveformRegionBuilder.LoopLeftSuffix, "Intro-L", StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(WaveformRegionBuilder.LoopEndSuffix, "Tail-E", StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(WaveformRegionBuilder.ExcludeRangeSuffix, "Skip-R", StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(WaveformRegionBuilder.AnacrusisSuffix, "Pickup-A", StringComparison.OrdinalIgnoreCase);
    }
}
