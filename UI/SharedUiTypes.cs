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
