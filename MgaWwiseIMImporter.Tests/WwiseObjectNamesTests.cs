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

    [Theory]
    [InlineData("Battle", "Battle")]
    [InlineData("jingle_04", "jingle_04")]
    [InlineData("_intro", "_intro")]
    [InlineData("My Song", "My_Song")]
    [InlineData("  loop  ", "loop")]
    [InlineData("a b c", "a_b_c")]
    [InlineData("song(loop)", "song_loop")]
    [InlineData("song (loop)", "song_loop")]
    [InlineData("bgm_field(battle)", "bgm_field_battle")]
    [InlineData("(intro)", "_intro")]
    [InlineData("boss-final", "boss_final")]
    [InlineData("BGM [Loop]", "BGM_Loop")]
    [InlineData("take-02", "take_02")]
    [InlineData("boss!", "boss")]
    [InlineData("king's", "king_s")]
    [InlineData("a+b&c", "a_b_c")]
    [InlineData("take.2", "take_2")]
    [InlineData("ジングル03", "ジングル03")]
    [InlineData("荒廃したタカマガハラ", "荒廃したタカマガハラ")]
    [InlineData("荒廃したタカマガハラ_a (aaa)", "荒廃したタカマガハラ_a_aaa")]
    [InlineData("荒廃したタカマガハラ_b (bbb)", "荒廃したタカマガハラ_b_bbb")]
    [InlineData("jingle（宝箱）", "jingle（宝箱）")]
    public void TryNormalizeRenameName_AcceptsAsciiWordChars(string input, string expected)
    {
        Assert.True(WwiseObjectNames.TryNormalizeRenameName(input, out var normalized, out var reason));
        Assert.Equal(expected, normalized);
        Assert.Equal(WwiseBaseNameRejectReason.None, reason);
    }

    [Theory]
    [InlineData("ﾎﾞｽ戦", nameof(WwiseBaseNameRejectReason.NonAsciiChars))] // 半角カナ
    [InlineData("ﾊﾞﾄﾙ", nameof(WwiseBaseNameRejectReason.NonAsciiChars))] // 半角カナのみ
    [InlineData("1battle", nameof(WwiseBaseNameRejectReason.StartsWithDigit))]
    [InlineData("boss2", nameof(WwiseBaseNameRejectReason.None))] // 数字は先頭以外 OK
    [InlineData("CON", nameof(WwiseBaseNameRejectReason.ReservedWindowsName))]
    [InlineData("", nameof(WwiseBaseNameRejectReason.Empty))]
    [InlineData("   ", nameof(WwiseBaseNameRejectReason.Empty))]
    [InlineData(null, nameof(WwiseBaseNameRejectReason.Empty))]
    public void TryNormalizeRenameName_RejectsUnusableNames(
        string? input,
        string expectedReasonName)
    {
        var expectedReason = Enum.Parse<WwiseBaseNameRejectReason>(expectedReasonName);
        var ok = WwiseObjectNames.TryNormalizeRenameName(input, out _, out var reason);
        Assert.Equal(expectedReason == WwiseBaseNameRejectReason.None, ok);
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    [InlineData("song.v2")] // 基底名の . は不可
    [InlineData("song%take")] // % は不可
    public void TryValidateBaseName_RejectsDotAndPercent(string name)
    {
        Assert.False(WwiseObjectNames.TryValidateBaseName(name, out var reason));
        Assert.Equal(WwiseBaseNameRejectReason.InvalidFileNameChars, reason);
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

    [Theory]
    [InlineData("intro", 1, 1, "intro")]
    [InlineData("ジングル03", 1, 1, "Music_1")]
    [InlineData("荒廃したタカマガハラ戦闘", 1, 2, "Music_1")]
    [InlineData("荒廃したタカマガハラ戦闘", 2, 2, "Music_2")]
    [InlineData("battle", 2, 2, "battle")]
    public void ResolveUsableStateObjectName_FallsBackWhenNonAscii(
        string name,
        int index,
        int count,
        string expected)
    {
        Assert.Equal(
            expected,
            WwiseObjectNames.ResolveUsableStateObjectName(name, index, count));
    }

    [Theory]
    [InlineData("Music_1", 1, 1)]
    [InlineData("Music_01", 1, 2)]
    [InlineData("Music_10", 10, 2)]
    [InlineData("intro", 0, 0)]
    [InlineData("Music_", 0, 0)]
    public void TryParseFallbackSwitchStateName_ParsesMusicN(
        string name,
        int expectedIndex,
        int expectedWidth)
    {
        var parsed = WwiseObjectNames.TryParseFallbackSwitchStateName(
            name,
            out var index,
            out var width);
        if (expectedIndex == 0)
        {
            Assert.False(parsed);
            return;
        }

        Assert.True(parsed);
        Assert.Equal(expectedIndex, index);
        Assert.Equal(expectedWidth, width);
    }

    [Theory]
    [InlineData("Music_1", "Music_2")]
    [InlineData("Music_9", "Music_10")]
    [InlineData("Music_09", "Music_10")]
    [InlineData("Music_99", "Music_100")]
    public void NextFallbackSwitchStateName_IncrementsAndExpandsWidth(
        string current,
        string expected)
    {
        Assert.Equal(expected, WwiseObjectNames.NextFallbackSwitchStateName(current));
    }

    [Fact]
    public void AllocateUnusedFallbackName_KeepsNameIfFree()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "Music_2" };
        Assert.Equal("Music_1", WwiseObjectNames.AllocateUnusedFallbackName("Music_1", taken));
    }

    [Fact]
    public void AllocateUnusedFallbackName_IncrementsWhileTaken()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "Music_1", "Music_2" };
        Assert.Equal("Music_3", WwiseObjectNames.AllocateUnusedFallbackName("Music_1", taken));
    }

    [Theory]
    [InlineData("Battle", "Battle")]
    [InlineData("jingle_04", "jingle_04")]
    [InlineData("Multi_Wave", "Multi_Wave")]
    [InlineData("ジングル03", "Multi_Wave")]
    [InlineData("荒廃したタカマガハラ戦闘", "Multi_Wave")]
    [InlineData("jingle（宝箱）", "Multi_Wave")]
    [InlineData("  Battle  ", "Battle")]
    [InlineData("", "Multi_Wave")]
    [InlineData("   ", "Multi_Wave")]
    [InlineData(null, "Multi_Wave")]
    public void ResolveMultiWaveContainerName_FallsBackWhenUnusableForStateGroup(
        string? desiredName,
        string expected)
    {
        Assert.Equal(expected, WwiseObjectNames.ResolveMultiWaveContainerName(desiredName));
    }

    [Theory]
    [InlineData(1, "Multi_Wave")]
    [InlineData(2, "Multi_Wave_2")]
    [InlineData(10, "Multi_Wave_10")]
    public void BuildMultiWaveContainerName_SkipsNumberOnFirst(int index, string expected)
    {
        Assert.Equal(expected, WwiseObjectNames.BuildMultiWaveContainerName(index));
    }

    [Theory]
    [InlineData("Multi_Wave", 1)]
    [InlineData("Multi_Wave_2", 2)]
    [InlineData("Multi_Wave_10", 10)]
    [InlineData("Multi_Wave_1", 0)]
    [InlineData("intro", 0)]
    public void TryParseMultiWaveContainerName_ParsesIndexedNames(string name, int expectedIndex)
    {
        var parsed = WwiseObjectNames.TryParseMultiWaveContainerName(name, out var index);
        if (expectedIndex == 0)
        {
            Assert.False(parsed);
            return;
        }

        Assert.True(parsed);
        Assert.Equal(expectedIndex, index);
    }

    [Theory]
    [InlineData("Multi_Wave", "Multi_Wave_2")]
    [InlineData("Multi_Wave_2", "Multi_Wave_3")]
    [InlineData("Multi_Wave_9", "Multi_Wave_10")]
    public void NextMultiWaveContainerName_IncrementsFromFirstAsTwo(
        string current,
        string expected)
    {
        Assert.Equal(expected, WwiseObjectNames.NextMultiWaveContainerName(current));
    }

    [Fact]
    public void AllocateUnusedMultiWaveName_KeepsNameIfFree()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "Multi_Wave_2" };
        Assert.Equal(
            "Multi_Wave",
            WwiseObjectNames.AllocateUnusedMultiWaveName("Multi_Wave", taken));
    }

    [Fact]
    public void AllocateUnusedMultiWaveName_IncrementsWhileTaken()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal)
        {
            "Multi_Wave",
            "Multi_Wave_2",
        };
        Assert.Equal(
            "Multi_Wave_3",
            WwiseObjectNames.AllocateUnusedMultiWaveName("Multi_Wave", taken));
    }
}
