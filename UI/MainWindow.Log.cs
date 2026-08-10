using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>ログ表示（RichTextBox への色分け出力）とログ操作ボタン。</summary>
public partial class MainWindow
{
    /// <summary>Form1 LogLineSpacingTwips=200（10pt）相当。</summary>
    private static double LogLineHeightDip => AppFonts.DipFromPoints(10);

    private void LogClearButton_Click(object? sender, RoutedEventArgs e) => ClearLogText();

    private void LogCopyButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var text = new TextRange(editorTextBox.Document.ContentStart, editorTextBox.Document.ContentEnd).Text;
            Clipboard.SetText(text);
        }
        catch
        {
            // クリップボード失敗は無視する。
        }
    }

    private void LogDownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"MGA-Wwise-IM-Importer-Log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*",
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var text = new TextRange(editorTextBox.Document.ContentStart, editorTextBox.Document.ContentEnd).Text;
            TextFileUtf8.WriteAllText(dialog.FileName, text, emitBom: false);
        }
        catch (Exception ex)
        {
            AppendColoredLine(UiStrings.ErrLogDownloadFailed(ex.Message));
        }
    }

    private void ClearLogText()
    {
        editorTextBox.Document.Blocks.Clear();
        _logColorSection = LogColorSection.Default;
        _logLastLineWasBlank = false;
    }

    /// <summary>複数行のレポート文字列を、行ごとに色分けしてログへ追記する。</summary>
    private void AppendReport(string report) => AppendReport(report, colorize: true);

    /// <summary>複数行をログへ追記する。colorize=false のときは既定色のまま。</summary>
    private void AppendReport(string report, bool colorize)
    {
        if (string.IsNullOrEmpty(report))
        {
            return;
        }

        // Form1 同様: 末尾 NewLine は「行の終端」であり空行ではない（Split の末尾 "" を捨てる）。
        var normalized = report
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var endsWithNewline = normalized.EndsWith('\n');
        var lines = normalized.Split('\n');
        var count = endsWithNewline ? lines.Length - 1 : lines.Length;

        for (var i = 0; i < count; i++)
        {
            if (colorize)
            {
                AppendColoredLine(lines[i]);
            }
            else
            {
                AppendLogParagraph(lines[i], UiColors.LogDefault);
            }
        }

        if (_uiInteractionLocks.HasFlag(UiInteractionLock.Export)
            || _uiInteractionLocks.HasFlag(UiInteractionLock.Load))
        {
            _exportOverlay.AppendLog(report);
        }
    }

    /// <summary>1 行をログへ追記する（=== 警告 / エラー === ブロックを跨いで色を引き継ぐ）。</summary>
    private void AppendColoredLine(string line)
    {
        if (ShouldSkipBlankLogLine(line))
        {
            return;
        }

        _logColorSection = LogColorHelper.AdvanceLogColorSection(line, _logColorSection);
        var color = LogColorHelper.ColorForLogLine(line, _logColorSection);
        AppendLogParagraph(line, color);
    }

    private void AppendLogParagraph(string line, Color color)
    {
        if (ShouldSkipBlankLogLine(line))
        {
            return;
        }

        var isBlank = string.IsNullOrWhiteSpace(line);
        var paragraph = new Paragraph(new Run(isBlank ? string.Empty : line))
        {
            Margin = new Thickness(0),
            Foreground = UiColors.Brush(color),
            FontFamily = AppFonts.LogTypeface.FontFamily,
            FontSize = editorTextBox.FontSize,
            LineHeight = LogLineHeightDip,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
        };
        editorTextBox.Document.Blocks.Add(paragraph);
        _logLastLineWasBlank = isBlank;
        editorTextBox.ScrollToEnd();
    }

    /// <summary>連続する空行は 1 行までに抑える（本文中の \n\n\n や二重 NewLine 対策）。</summary>
    private bool ShouldSkipBlankLogLine(string line) =>
        string.IsNullOrWhiteSpace(line) && _logLastLineWasBlank;
}
