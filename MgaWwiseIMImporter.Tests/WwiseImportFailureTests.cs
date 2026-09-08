using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Tests;

public class WwiseImportFailureTests
{
    [Fact]
    public void TryExtractObjectLeafName_ReadsEscapedWaapiPath()
    {
        const string raw =
            """HTTP 500 Server Error. {"details":{"object":"\\States\\Default Work Unit\\1あああ"},"message":"Object \\States\\Default Work Unit\\1あああ is unknown.","uri":"ak.wwise.invalid_arguments"}""";

        Assert.Equal("1あああ", WwiseImportFailure.TryExtractObjectLeafName(raw));
    }

    [Fact]
    public void Format_DigitAndTwoByteUnknownObject_ExplainsAndTellsHowToFix()
    {
        const string raw =
            """HTTP 500 Server Error. {"message":"Object \\States\\Default Work Unit\\1あああ is unknown.","uri":"ak.wwise.invalid_arguments"}""";
        var plan = new WwiseMusicPlan
        {
            ContainerName = "1あああ",
            IsMultiPart = true,
            Playlists = [],
        };

        var text = WwiseImportFailure.Format(raw, plan);

        Assert.Contains("1あああ", text);
        Assert.Contains(UiStrings.ErrWwiseObjectNameRejectedDigit, text);
        Assert.Contains(UiStrings.ErrWwiseObjectNameRejectedTwoByte, text);
        Assert.Contains("Music_01", text);
        Assert.Contains("Segment", text);
        Assert.Contains(UiStrings.ErrWwiseObjectNameRejectedWhatToDo, text);
        Assert.Contains("is unknown", text);
    }

    [Fact]
    public void TryDescribeInvalidPlanName_TrueWhenContainerStartsWithDigit()
    {
        var plan = new WwiseMusicPlan
        {
            ContainerName = "1あああ",
            IsMultiPart = true,
            Playlists = [],
        };

        Assert.True(WwiseImportFailure.TryDescribeInvalidPlanName(plan, out var message));
        Assert.Contains(UiStrings.ErrWwiseObjectNameRejectedDigit, message);
        Assert.DoesNotContain("HTTP 500", message);
    }

    [Fact]
    public void TryDescribeInvalidPlanName_FalseWhenAsciiName()
    {
        var plan = new WwiseMusicPlan
        {
            ContainerName = "bgm_test",
            IsMultiPart = true,
            Playlists = [],
        };

        Assert.False(WwiseImportFailure.TryDescribeInvalidPlanName(plan, out _));
    }

    [Fact]
    public void Format_UnrelatedError_KeepsRawMessage()
    {
        const string raw = "HTTP 500 Server Error. something else";
        var plan = new WwiseMusicPlan
        {
            ContainerName = "bgm_test",
            IsMultiPart = true,
            Playlists = [],
        };

        Assert.Equal(raw, WwiseImportFailure.Format(raw, plan));
    }
}
