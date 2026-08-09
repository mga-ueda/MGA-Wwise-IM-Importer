using System.Drawing;
using WpfColor = System.Windows.Media.Color;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// UiColors の色を GDI+ (<see cref="System.Drawing.Color"/>) として直接使うための薄いラッパー。
/// WaveformView の描画コードは GDI+ を使い続けるため、このファイル内では
/// <c>WaveformGdiColors.XXX</c> と書くだけで自動的に System.Drawing.Color へ変換される。
/// </summary>
internal static class WaveformGdiColors
{
    private static Color D(WpfColor c) => MgaWwiseIMImporter.UI.UiColors.ToDrawing(c);

    public static Color ForControlBack(Color c) => Color.FromArgb(255, c.R, c.G, c.B);

    public static Color ToDrawing(WpfColor c) => D(c);

    public static Color WaveformBack => D(MgaWwiseIMImporter.UI.UiColors.WaveformBack);
    public static Color EmptyHint => D(MgaWwiseIMImporter.UI.UiColors.EmptyHint);
    public static Color BarNumberBg => D(MgaWwiseIMImporter.UI.UiColors.BarNumberBg);
    public static Color TempoBg => D(MgaWwiseIMImporter.UI.UiColors.TempoBg);
    public static Color SignatureBg => D(MgaWwiseIMImporter.UI.UiColors.SignatureBg);
    public static Color MarkerRowBg => D(MgaWwiseIMImporter.UI.UiColors.MarkerRowBg);
    public static Color WaveformInfoFg => D(MgaWwiseIMImporter.UI.UiColors.WaveformInfoFg);
    public static Color MarkerTriangle => D(MgaWwiseIMImporter.UI.UiColors.MarkerTriangle);
    public static Color MarkerTriangleSelected => D(MgaWwiseIMImporter.UI.UiColors.MarkerTriangleSelected);
    public static Color MarkerCommentSelected => D(MgaWwiseIMImporter.UI.UiColors.MarkerCommentSelected);
    public static Color BarLine => D(MgaWwiseIMImporter.UI.UiColors.BarLine);
    public static Color BeatLine => D(MgaWwiseIMImporter.UI.UiColors.BeatLine);
    public static Color TempoChangeLine => D(MgaWwiseIMImporter.UI.UiColors.TempoChangeLine);
    public static Color WaveFill => D(MgaWwiseIMImporter.UI.UiColors.WaveFill);
    public static Color WaveZeroDbLine => D(MgaWwiseIMImporter.UI.UiColors.WaveZeroDbLine);
    public static Color WaveformSourceMeterTrack => D(MgaWwiseIMImporter.UI.UiColors.WaveformSourceMeterTrack);
    public static Color WaveformSourceMeterMinimum => D(MgaWwiseIMImporter.UI.UiColors.WaveformSourceMeterMinimum);
    public static Color WaveformSourceMeterMaximum => D(MgaWwiseIMImporter.UI.UiColors.WaveformSourceMeterMaximum);
    public static Color RegionWaveFillGray => D(MgaWwiseIMImporter.UI.UiColors.RegionWaveFillGray);
    public static Color RegionWaveFillExcluded => D(MgaWwiseIMImporter.UI.UiColors.RegionWaveFillExcluded);
    public static Color RegionWaveFillLoop => D(MgaWwiseIMImporter.UI.UiColors.RegionWaveFillLoop);
    public static Color RegionWaveFillAnacrusis => D(MgaWwiseIMImporter.UI.UiColors.RegionWaveFillAnacrusis);
    public static Color RegionWaveFillExit => D(MgaWwiseIMImporter.UI.UiColors.RegionWaveFillExit);
    public static Color RegionBoundaryMarker => D(MgaWwiseIMImporter.UI.UiColors.RegionBoundaryMarker);
    public static Color EntryCueMarker => D(MgaWwiseIMImporter.UI.UiColors.EntryCueMarker);
    public static Color ExitCueMarker => D(MgaWwiseIMImporter.UI.UiColors.ExitCueMarker);
    public static Color OutputPartFg => D(MgaWwiseIMImporter.UI.UiColors.OutputPartFg);
    public static Color MusicSegmentLaneBg => D(MgaWwiseIMImporter.UI.UiColors.MusicSegmentLaneBg);
    public static Color MusicPlaylistLaneBg => D(MgaWwiseIMImporter.UI.UiColors.MusicPlaylistLaneBg);
    public static Color ExportPartGlow => D(MgaWwiseIMImporter.UI.UiColors.ExportPartGlow);
    public static Color SeekCyan => D(MgaWwiseIMImporter.UI.UiColors.SeekCyan);
    public static Color RegionFadeCurve => D(MgaWwiseIMImporter.UI.UiColors.RegionFadeCurve);
    public static Color SeekExit => D(MgaWwiseIMImporter.UI.UiColors.SeekExit);
    public static Color SeekAnacrusis => D(MgaWwiseIMImporter.UI.UiColors.SeekAnacrusis);
    public static Color SeekFadeOut => D(MgaWwiseIMImporter.UI.UiColors.SeekFadeOut);
    public static Color MouseGuide => D(MgaWwiseIMImporter.UI.UiColors.MouseGuide);
    public static Color ChromeMid => D(MgaWwiseIMImporter.UI.UiColors.ChromeMid);
    public static Color ChromeBorder => D(MgaWwiseIMImporter.UI.UiColors.ChromeBorder);
    public static Color TransportDisabledFore => D(MgaWwiseIMImporter.UI.UiColors.TransportDisabledFore);
    public static Color DialogFore => D(MgaWwiseIMImporter.UI.UiColors.DialogFore);
    public static Color DialogInputBack => D(MgaWwiseIMImporter.UI.UiColors.DialogInputBack);
    public static Color LogWarning => D(MgaWwiseIMImporter.UI.UiColors.LogWarning);
    public static Color PlaylistHoverBorder => D(MgaWwiseIMImporter.UI.UiColors.PlaylistHoverBorder);
}

internal enum MarkerEditMode
{
    Add,
    Remove,
}

internal sealed class MarkerEditRequestedEventArgs(
    MarkerEditMode mode,
    IReadOnlyList<long> sampleOffsets) : EventArgs
{
    public MarkerEditMode Mode { get; } = mode;
    public IReadOnlyList<long> SampleOffsets { get; } = sampleOffsets;
}

internal sealed class SourceNameEditCommittedEventArgs(string name) : EventArgs
{
    public string Name { get; } = name;
}

internal sealed class SourceNameEditStateChangedEventArgs(bool isEditing) : EventArgs
{
    public bool IsEditing { get; } = isEditing;
}

internal sealed class MarkerCommentEditCommittedEventArgs(long sampleOffset, string comment) : EventArgs
{
    public long SampleOffset { get; } = sampleOffset;
    public string Comment { get; } = comment;
}

internal sealed class MarkerCommentEditStateChangedEventArgs(bool isEditing) : EventArgs
{
    public bool IsEditing { get; } = isEditing;
}

internal sealed class MarkerSessionDeleteRequestedEventArgs(long sampleOffset) : EventArgs
{
    public long SampleOffset { get; } = sampleOffset;
}

internal sealed class MarkerSessionMoveRequestedEventArgs(
    long fromSampleOffset,
    long toSampleOffset,
    bool shiftPreviousMarker = false) : EventArgs
{
    public long FromSampleOffset { get; } = fromSampleOffset;
    public long ToSampleOffset { get; } = toSampleOffset;

    /// <summary>true のとき、一つ前のマーカーも同じサンプル差分だけ移動する。</summary>
    public bool ShiftPreviousMarker { get; } = shiftPreviousMarker;
}

internal sealed class RegionFadeChangedEventArgs(RegionEdgeFade fade) : EventArgs
{
    public RegionEdgeFade Fade { get; } = fade;
}
