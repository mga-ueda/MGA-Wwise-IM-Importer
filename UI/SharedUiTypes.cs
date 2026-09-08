namespace MgaWwiseIMImporter.UI;

[Flags]
internal enum UiInteractionLock
{
    None = 0,
    SourceNameEdit = 1,
    Export = 2,
    Load = 4,
    MarkerCommentEdit = 8,
}

/// <summary>ログ行の色分けセクション（MainWindow / ExportGlassOverlay 共通）。</summary>
internal enum LogColorSection
{
    Default,
    Warning,
    Error,
}
