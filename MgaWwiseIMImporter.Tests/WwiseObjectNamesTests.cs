using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Tests;

public class WwiseObjectNamesTests
{
    [Theory]
    [InlineData("intro", false)]
    [InlineData("jingle_04", false)]
    [InlineData("ジングル03", true)]
    [InlineData("jingle（宝箱）", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsUnusableStateNameChars_DetectsNonAscii(string? name, bool expected)
    {
        Assert.Equal(expected, WwiseObjectNames.ContainsUnusableStateNameChars(name));
    }

    [Fact]
    public void ShouldUseFallbackSwitchStateNames_TrueIfAnyNameHasTwoByteChars()
    {
        Assert.False(WwiseObjectNames.ShouldUseFallbackSwitchStateNames(["intro", "loop"]));
        Assert.True(WwiseObjectNames.ShouldUseFallbackSwitchStateNames(["intro", "ジングル04"]));
    }

    [Theory]
    [InlineData(1, 2, "Music_1")]
    [InlineData(2, 9, "Music_2")]
    [InlineData(1, 10, "Music_01")]
    [InlineData(10, 10, "Music_10")]
    [InlineData(1, 100, "Music_001")]
    [InlineData(12, 100, "Music_012")]
    public void BuildFallbackSwitchStateName_PadsToDigitWidthOfCount(
        int index,
        int count,
        string expected)
    {
        Assert.Equal(expected, WwiseObjectNames.BuildFallbackSwitchStateName(index, count));
    }
}
