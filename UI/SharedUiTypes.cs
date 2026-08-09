namespace MgaWwiseIMImporter.UI;

internal enum PlaylistExitSourceMode
{
    Immediate,
    NextBar,
    NextBeat,
    NextCue,
    ExitCue,
}

internal static class PlaylistUiNames
{
    /// <summary>Exit Source At ラジオの表示名。</summary>
    public static string ToUiName(this PlaylistExitSourceMode mode) => UiStrings.LabelExitSource(mode);

    /// <summary>遷移先同期モードの表示名（ログ・診断用）。</summary>
    public static string ToUiName(this PlaylistDestinationSyncMode mode) => UiStrings.LabelDestinationSync(mode);

    /// <summary>Marker Grid ラジオの表示名。</summary>
    public static string ToUiName(this MarkerGridOverrideMode mode) => UiStrings.LabelMarkerGrid(mode);

    /// <summary>Fade In / Fade Out の秒数に対応する表示名。</summary>
    public static string ToFadeUiName(double seconds, bool isFadeIn) => UiStrings.LabelFadeSeconds(seconds);
}

[Flags]
internal enum UiInteractionLock
{
    None = 0,
    SourceNameEdit = 1,
    Export = 2,
    Load = 4,
    MarkerOptionsEdit = 8,
    MarkerCommentEdit = 16,
}

/// <summary>ログ行の色分けセクション（MainWindow / ExportGlassOverlay 共通）。</summary>
internal enum LogColorSection
{
    Default,
    Header,
    Warning,
    Error,
}
