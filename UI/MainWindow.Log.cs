using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>ログ表示（RichTextBox への色分け出力）とログ操作ボタン。</summary>
public partial class MainWindow
{
    /// <summary>DarkScrollBarStyle Width fallback.</summary>
    private const double LogScrollBarFallbackWidth = 8;

    /// <summary>Form1 LogLineSpacingTwips=200（10pt）相当。</summary>
    private static double LogLineHeightDip => AppFonts.DipFromPoints(10);

    private ScrollBar? _logVerticalScrollBar;

    private void WireLogButtonLayout()
    {
        // 右余白は縦スクロール幅が本命。host サイズ変化とスクロールバー可視化で足りる。
        logEditorHost.SizeChanged += LogEditorHost_SizeChanged;
    }

    private void LogEditorHost_SizeChanged(object sender, SizeChangedEventArgs e) => PositionLogButtons();

    /// <summary>
    /// ログ本文へ重ねたボタン行の右余白を、縦スクロールバー幅に合わせる（Form1 相当）。
    /// 位置そのものは logEditorHost 内の右下寄せ（XAML）に任せる。
    /// </summary>
    private void PositionLogButtons()
    {
        if (!IsLoaded)
        {
            return;
        }

        var scrollBar = ResolveLogVerticalScrollBar();
        var rightInset = scrollBar switch
        {
            { IsVisible: true, ActualWidth: > 0 } => scrollBar.ActualWidth,
            not null when !double.IsNaN(scrollBar.Width) && scrollBar.Width > 0 => scrollBar.Width,
            _ => LogScrollBarFallbackWidth,
        };

        logButtonPanel.Margin = new Thickness(0, 0, rightInset, 0);
    }

    private ScrollBar? ResolveLogVerticalScrollBar()
    {
        if (_logVerticalScrollBar is not null)
        {
            return _logVerticalScrollBar;
        }

        editorTextBox.ApplyTemplate();
        var scrollViewer = editorTextBox.Template?.FindName("PART_ContentHost", editorTextBox) as ScrollViewer
            ?? VisualTreeUtil.FindVisualDescendant<ScrollViewer>(editorTextBox);
        scrollViewer?.ApplyTemplate();
        var scrollBar = scrollViewer?.Template?.FindName("PART_VerticalScrollBar", scrollViewer) as ScrollBar
            ?? VisualTreeUtil.FindVisualDescendant<ScrollBar>(
                editorTextBox,
                sb => sb.Orientation == Orientation.Vertical);
        if (scrollBar is null)
        {
            return null;
        }

        _logVerticalScrollBar = scrollBar;
        scrollBar.IsVisibleChanged += (_, _) => PositionLogButtons();
        scrollBar.SizeChanged += (_, _) => PositionLogButtons();
        return scrollBar;
    }

    /// <summary>すりガラス下のログボタン。busy 中はヒットだけ切る（非表示にしない）。</summary>
    private void SyncLogButtonsForBusy(bool busy)
    {
        logButtonPanel.IsHitTestVisible = !busy;
        if (!busy)
        {
            PositionLogButtons();
        }
    }

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

        if (IsExportOrLoadBusy)
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
