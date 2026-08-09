using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>ログ表示（RichTextBox への色分け出力）とログ操作ボタン。</summary>
public partial class MainWindow
{
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

        foreach (var line in report.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (colorize)
            {
                AppendColoredLine(text);
            }
            else
            {
                AppendLogParagraph(text, UiColors.LogDefault);
            }
        }
    }

    /// <summary>1 行をログへ追記する（=== 警告 / エラー === ブロックを跨いで色を引き継ぐ）。</summary>
    private void AppendColoredLine(string line)
    {
        _logColorSection = LogColorHelper.AdvanceLogColorSection(line, _logColorSection);
        var color = LogColorHelper.ColorForLogLine(line, _logColorSection);
        AppendLogParagraph(line, color);
    }

    private void AppendLogParagraph(string line, Color color)
    {
        var paragraph = new Paragraph(new Run(line))
        {
            Margin = new Thickness(0),
            Foreground = UiColors.Brush(color),
            FontFamily = AppFonts.LogTypeface.FontFamily,
        };
        editorTextBox.Document.Blocks.Add(paragraph);
        editorTextBox.ScrollToEnd();
    }
}
