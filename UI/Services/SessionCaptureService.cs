using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI.Services;

/// <summary>Last Session キャプチャ（ディスク I/O は呼び出し側）。</summary>
internal static class SessionCaptureService
{
    public static LastWaveSessionState? TryCapture(
        WaveformPreviewData? preview,
        WaveformPreviewSession? session,
        IReadOnlyDictionary<int, int> partGroupIds,
        IReadOnlyDictionary<int, int> groupColorIndexes,
        int nextGroupId,
        int nextColorIndex,
        IReadOnlySet<int> disabledPartNumbers,
        IReadOnlyDictionary<int, PlaylistExitSourceMode> partExitSourceModes,
        IReadOnlyDictionary<int, PlaylistExitSourceMode> partChangeOccursAtModes,
        IReadOnlyDictionary<int, double> partFadeInSeconds,
        IReadOnlyDictionary<int, double> partFadeOutSeconds,
        IReadOnlyDictionary<int, RegionFadeCurveKind> partFadeInCurves,
        IReadOnlyDictionary<int, RegionFadeCurveKind> partFadeOutCurves,
        IReadOnlyDictionary<int, double> partGroupFadeSeconds,
        IReadOnlyDictionary<int, bool> partPlayPostExit,
        IReadOnlyDictionary<int, bool> partAdditiveLayers,
        string? sourceBaseNameOverride)
    {
        if (preview is null || session is null)
        {
            return null;
        }

        var wavePaths = preview.IsMultiWaveOnly
            ? preview.SourceSpans.Select(s => s.Path).ToArray()
            : null;
        return LastWaveSessionState.Capture(
            preview.SourcePath,
            session.EffectiveOutputParts,
            partGroupIds,
            groupColorIndexes,
            nextGroupId,
            nextColorIndex,
            session.GetUserMarkerSampleOffsets(),
            disabledPartNumbers,
            partExitSourceModes,
            partChangeOccursAtModes,
            partFadeInSeconds,
            partFadeOutSeconds,
            partFadeInCurves,
            partFadeOutCurves,
            partGroupFadeSeconds,
            partPlayPostExit,
            partAdditiveLayers,
            session.GetWaveOnlySessionMarkers(),
            session.RegionEdgeFades,
            wavePaths,
            sourceBaseNameOverride);
    }
}
