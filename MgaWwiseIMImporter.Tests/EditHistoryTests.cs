using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public sealed class EditHistoryTests
{
    [Fact]
    public void Snapshot_StartsWithOriginAndFollowsRecordAndJump()
    {
        UiStrings.SetLanguage(UiLanguage.Japanese);
        var history = new EditHistory();
        var emptyMarkers = Array.Empty<WaveformMarkerMark>();
        var emptyFades = Array.Empty<RegionEdgeFade>();
        var added = new[] { new WaveformMarkerMark(4800, "-L") };

        history.Record(
            emptyMarkers,
            emptyFades,
            added,
            emptyFades,
            UiStrings.EditHistoryNameAddMarker,
            "マーカー追加");

        var items = history.Snapshot();
        Assert.Equal(2, items.Count);
        Assert.Equal(UiStrings.EditHistoryOrigin, items[0].Name);
        Assert.Equal(UiStrings.EditHistoryNameAddMarker, items[1].Name);
        Assert.Equal(1, history.CurrentIndex);

        Assert.True(history.JumpTo(0, out var origin));
        Assert.Equal(0, history.CurrentIndex);
        Assert.Empty(origin.Markers!);
        Assert.Equal(2, history.Snapshot().Count);

        Assert.True(history.JumpTo(1, out var after));
        Assert.Equal(1, history.CurrentIndex);
        Assert.Equal(4800, after.Markers![0].SampleOffset);
        Assert.False(history.JumpTo(1, out _));
    }

    [Fact]
    public void JumpTo_RestoresEarlierAndLaterStatesAcrossMarkerAndFade()
    {
        var history = new EditHistory();
        var markers0 = Array.Empty<WaveformMarkerMark>();
        var markers1 = new[] { new WaveformMarkerMark(100, "A") };
        var fades0 = Array.Empty<RegionEdgeFade>();
        var fades1 = new[] { new RegionEdgeFade(0, 1000, 200, 800) };

        history.Record(markers0, fades0, markers1, fades0, UiStrings.EditHistoryNameAddMarker, "add");
        history.Record(markers1, fades0, markers1, fades1, UiStrings.EditHistoryNameRegionEdgeFade, "fade");
        Assert.Equal(2, history.TotalCount);

        Assert.True(history.JumpTo(0, out var origin));
        Assert.Empty(origin.Markers!);
        Assert.Empty(origin.Fades);

        Assert.True(history.JumpTo(1, out var mid));
        Assert.Equal("A", mid.Markers![0].Comment);
        Assert.Empty(mid.Fades);

        Assert.True(history.JumpTo(2, out var latest));
        Assert.Equal(200, latest.Fades[0].FadeInEndSample);
    }

    [Fact]
    public void Record_AfterJumpDiscardsRedoBranch()
    {
        var history = new EditHistory();
        var empty = Array.Empty<WaveformMarkerMark>();
        var fades = Array.Empty<RegionEdgeFade>();
        history.Record(empty, fades, [new WaveformMarkerMark(1, "A")], fades, "Add Marker", "A");
        history.Record(
            [new WaveformMarkerMark(1, "A")],
            fades,
            [new WaveformMarkerMark(1, "A"), new WaveformMarkerMark(2, "B")],
            fades,
            "Add Marker",
            "B");
        Assert.True(history.JumpTo(1, out _));

        history.Record(
            [new WaveformMarkerMark(1, "A")],
            fades,
            [new WaveformMarkerMark(1, "A"), new WaveformMarkerMark(9, "C")],
            fades,
            "Add Marker",
            "C");

        Assert.Equal(2, history.TotalCount);
        Assert.Equal(2, history.CurrentIndex);
        Assert.Equal("C", history.Snapshot()[2].Title);
    }

    [Fact]
    public void DescribeMarkerChange_AddMoveAndRename()
    {
        UiStrings.SetLanguage(UiLanguage.Japanese);
        var before = Array.Empty<WaveformMarkerMark>();
        var added = new[] { new WaveformMarkerMark(48000, "-L") };
        var (addName, addSummary) = EditHistory.DescribeMarkerChange(before, added, 48000);
        Assert.Equal(UiStrings.EditHistoryNameAddMarker, addName);
        Assert.Contains("00:01.000", addSummary);
        Assert.Contains("-L", addSummary);

        var moved = new[] { new WaveformMarkerMark(96000, "-L") };
        var (moveName, moveSummary) = EditHistory.DescribeMarkerChange(added, moved, 48000);
        Assert.Equal(UiStrings.EditHistoryNameMoveMarker, moveName);
        Assert.Contains("00:01.000→00:02.000", moveSummary);

        var renamed = new[] { new WaveformMarkerMark(96000, "Loop") };
        var (renameName, renameSummary) = EditHistory.DescribeMarkerChange(moved, renamed, 48000);
        Assert.Equal(UiStrings.EditHistoryNameMarkerComment, renameName);
        Assert.Contains("Loop", renameSummary);
    }
}
