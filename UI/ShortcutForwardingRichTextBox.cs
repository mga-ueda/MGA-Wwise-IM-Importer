using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 読み取り専用ログ表示。Space／矢印などのトランスポート用キーは親ウィンドウへ渡す。
/// </summary>
internal sealed class ShortcutForwardingRichTextBox : RichTextBox
{
    public ShortcutForwardingRichTextBox()
    {
        IsReadOnly = true;
        IsDocumentEnabled = false;
        Focusable = true;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        BorderThickness = new Thickness(0);
        Background = UiColors.Brush(UiColors.LogBack);
        Foreground = UiColors.Brush(UiColors.LogDefault);
        CaretBrush = UiColors.Brush(UiColors.AccentCyan);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ShouldForwardToWindow(e.Key))
        {
            return;
        }

        base.OnKeyDown(e);
    }

    private static bool ShouldForwardToWindow(Key key) =>
        key is Key.Space
            or Key.Left
            or Key.Right
            or Key.Up
            or Key.Down
            or Key.PageUp
            or Key.PageDown
            or Key.Home
            or Key.End;
}
