using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// マーカー編集とリージョン端フェードを時系列で持つ Undo / Redo。
/// Sonic Anvil の編集履歴と同じく、初期状態を 0、最後の編集を <see cref="TotalCount"/> とする。
/// </summary>
internal sealed class EditHistory
{
    private readonly List<EditHistoryFrame> _frames = [];
    private int _index;

    public bool CanUndo => _frames.Count > 0 && _index > 0;

    public bool CanRedo => _frames.Count > 0 && _index < _frames.Count - 1;

    /// <summary>初期状態を 0、最後に実行した編集を Undo 相当の位置。</summary>
    public int CurrentIndex => _frames.Count == 0 ? 0 : _index;

    public int TotalCount => Math.Max(0, _frames.Count - 1);

    public void Clear()
    {
        _frames.Clear();
        _index = 0;
    }

    public IReadOnlyList<EditHistoryEntry> Snapshot()
    {
        if (_frames.Count == 0)
        {
            return [new(0, UiStrings.EditHistoryOrigin, UiStrings.EditHistoryOrigin)];
        }

        var items = new List<EditHistoryEntry>(_frames.Count);
        for (var i = 0; i < _frames.Count; i++)
        {
            var frame = _frames[i];
            items.Add(new(i, frame.Name, frame.Title));
        }

        return items;
    }

    public void Record(
        IReadOnlyList<WaveformMarkerMark>? markersBefore,
        IReadOnlyList<RegionEdgeFade> fadesBefore,
        IReadOnlyList<WaveformMarkerMark>? markersAfter,
        IReadOnlyList<RegionEdgeFade> fadesAfter,
        string name,
        string summary)
    {
        if (_frames.Count == 0)
        {
            _frames.Add(new EditHistoryFrame(
                UiStrings.EditHistoryOrigin,
                UiStrings.EditHistoryOrigin,
                CloneMarkers(markersBefore),
                CloneFades(fadesBefore)));
            _index = 0;
        }
        else if (_index < _frames.Count - 1)
        {
            _frames.RemoveRange(_index + 1, _frames.Count - _index - 1);
        }

        _frames.Add(new EditHistoryFrame(
            name,
            summary,
            CloneMarkers(markersAfter),
            CloneFades(fadesAfter)));
        _index = _frames.Count - 1;
    }

    public bool JumpTo(int index, out EditHistoryFrame frame)
    {
        if (_frames.Count == 0)
        {
            frame = default;
            return false;
        }

        var next = Math.Clamp(index, 0, _frames.Count - 1);
        if (next == _index)
        {
            frame = _frames[next];
            return false;
        }

        _index = next;
        frame = _frames[next];
        return true;
    }

    public static (string Name, string Summary) DescribeMarkerChange(
        IReadOnlyList<WaveformMarkerMark> before,
        IReadOnlyList<WaveformMarkerMark> after,
        int sampleRate)
    {
        var beforeFrames = before.Select(marker => marker.SampleOffset).ToHashSet();
        var afterFrames = after.Select(marker => marker.SampleOffset).ToHashSet();
        var removed = before.Where(marker => !afterFrames.Contains(marker.SampleOffset)).ToArray();
        var added = after.Where(marker => !beforeFrames.Contains(marker.SampleOffset)).ToArray();

        if (removed.Length == 0 && added.Length == 1)
        {
            return (
                UiStrings.EditHistoryNameAddMarker,
                UiStrings.EditHistoryPoint(
                    UiStrings.EditHistoryName(UiStrings.EditHistoryNameAddMarker),
                    sampleRate,
                    added[0].SampleOffset,
                    UiStrings.EditHistoryQuote(added[0].Comment)));
        }

        if (removed.Length == 1 && added.Length == 0)
        {
            return (
                UiStrings.EditHistoryNameDeleteMarkers,
                UiStrings.EditHistoryPoint(
                    UiStrings.EditHistoryName(UiStrings.EditHistoryNameDeleteMarkers),
                    sampleRate,
                    removed[0].SampleOffset,
                    UiStrings.EditHistoryQuote(removed[0].Comment)));
        }

        if (removed.Length == added.Length && removed.Length > 0)
        {
            Array.Sort(removed, static (left, right) => left.SampleOffset.CompareTo(right.SampleOffset));
            Array.Sort(added, static (left, right) => left.SampleOffset.CompareTo(right.SampleOffset));
            var comment = removed.Length == 1 && removed[0].Comment.Length > 0
                ? UiStrings.EditHistoryQuote(removed[0].Comment)
                : null;
            if (removed.Length == 1)
            {
                return (
                    UiStrings.EditHistoryNameMoveMarker,
                    UiStrings.EditHistoryShift(
                        UiStrings.EditHistoryName(UiStrings.EditHistoryNameMoveMarker),
                        sampleRate,
                        removed[0].SampleOffset,
                        added[0].SampleOffset,
                        comment));
            }

            return (
                UiStrings.EditHistoryNameMoveMarker,
                $"{UiStrings.EditHistoryName(UiStrings.EditHistoryNameMoveMarker)}  {removed.Length}");
        }

        if (removed.Length == 0 && added.Length == 0)
        {
            var renamed = FindRenamed(before, after);
            if (renamed is { } pair)
            {
                return (
                    UiStrings.EditHistoryNameMarkerComment,
                    UiStrings.EditHistoryPoint(
                        UiStrings.EditHistoryName(UiStrings.EditHistoryNameMarkerComment),
                        sampleRate,
                        pair.SampleOffset,
                        $"{UiStrings.EditHistoryQuote(pair.Before)}→{UiStrings.EditHistoryQuote(pair.After)}"));
            }
        }

        return (
            UiStrings.EditHistoryNameEditMarkers,
            UiStrings.EditHistoryName(UiStrings.EditHistoryNameEditMarkers));
    }

    public static (string Name, string Summary) DescribeFadeChange() =>
        (
            UiStrings.EditHistoryNameRegionEdgeFade,
            UiStrings.EditHistoryName(UiStrings.EditHistoryNameRegionEdgeFade));

    private static (long SampleOffset, string Before, string After)? FindRenamed(
        IReadOnlyList<WaveformMarkerMark> before,
        IReadOnlyList<WaveformMarkerMark> after)
    {
        var afterByOffset = after.ToDictionary(marker => marker.SampleOffset);
        foreach (var marker in before)
        {
            if (afterByOffset.TryGetValue(marker.SampleOffset, out var next)
                && !string.Equals(marker.Comment, next.Comment, StringComparison.Ordinal))
            {
                return (marker.SampleOffset, marker.Comment, next.Comment);
            }
        }

        return null;
    }

    private static IReadOnlyList<WaveformMarkerMark>? CloneMarkers(
        IReadOnlyList<WaveformMarkerMark>? source) =>
        source?.Select(marker => new WaveformMarkerMark(
            marker.SampleOffset,
            marker.Comment,
            marker.IsSharedProjection,
            marker.IsFromWaveEmbedded)).ToArray();

    private static IReadOnlyList<RegionEdgeFade> CloneFades(IReadOnlyList<RegionEdgeFade> source) =>
        source.ToArray();
}

internal readonly record struct EditHistoryEntry(int Index, string Name, string Title);

internal readonly record struct EditHistoryFrame(
    string Name,
    string Title,
    IReadOnlyList<WaveformMarkerMark>? Markers,
    IReadOnlyList<RegionEdgeFade> Fades);
