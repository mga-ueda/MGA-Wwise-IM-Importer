using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>Sonic Anvil と同じ編集履歴オーバーレイ（U で開閉）。</summary>
internal sealed class EditHistoryOverlay : Border
{
    private readonly TextBlock _title;
    private readonly TextBlock _hint;
    private readonly StackPanel _items = new();
    private readonly ScrollViewer _scroll;

    public event EventHandler<int>? ItemChosen;
    public event EventHandler<int>? ItemCommitted;

    public EditHistoryOverlay()
    {
        Width = 340;
        MaxHeight = 360;
        Padding = new Thickness(0, 0, 0, 6);
        Background = (Brush)Application.Current.FindResource("ColorPanelBackBrush");
        BorderBrush = (Brush)Application.Current.FindResource("ChromeBorderBrush");
        BorderThickness = new Thickness(1);
        SnapsToDevicePixels = true;
        Focusable = false;

        _title = new TextBlock
        {
            Margin = new Thickness(10, 8, 10, 2),
            FontSize = 11,
            FontFamily = AppFonts.UiFamily,
            Foreground = (Brush)Application.Current.FindResource("MutedForeBrush"),
        };
        _hint = new TextBlock
        {
            Margin = new Thickness(10, 0, 10, 6),
            FontSize = 10,
            FontFamily = AppFonts.UiFamily,
            Foreground = (Brush)Application.Current.FindResource("MutedForeBrush"),
        };
        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 300,
            Content = _items,
        };

        var root = new DockPanel();
        DockPanel.SetDock(_title, Dock.Top);
        DockPanel.SetDock(_hint, Dock.Top);
        root.Children.Add(_title);
        root.Children.Add(_hint);
        root.Children.Add(_scroll);
        Child = root;
        ApplyLocalizedChrome();
    }

    public void SetItems(IReadOnlyList<EditHistoryEntry> items, int selectedIndex)
    {
        ApplyLocalizedChrome();
        _items.Children.Clear();
        var selectedBack = (Brush)Application.Current.FindResource("PrimaryForeBrush");
        var selectedFore = (Brush)Application.Current.FindResource("SurfaceBackBrush");
        var idleFore = (Brush)Application.Current.FindResource("PrimaryForeBrush");
        var futureFore = (Brush)Application.Current.FindResource("MutedForeBrush");
        FrameworkElement? selectedRow = null;
        foreach (var item in items)
        {
            var selected = item.Index == selectedIndex;
            var future = item.Index > selectedIndex;
            var row = new Border
            {
                Tag = item.Index,
                Padding = new Thickness(10, 4, 10, 4),
                Background = selected ? selectedBack : Brushes.Transparent,
                Cursor = Cursors.Hand,
            };
            var label = new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                FontFamily = AppFonts.UiFamily,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = selected ? selectedFore : future ? futureFore : idleFore,
            };
            row.Child = label;
            row.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount != 2 || row.Tag is not int index)
                {
                    return;
                }

                e.Handled = true;
                ItemCommitted?.Invoke(this, index);
            };
            row.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (row.Tag is int index)
                {
                    ItemChosen?.Invoke(this, index);
                }
            };
            _items.Children.Add(row);
            if (selected)
            {
                selectedRow = row;
            }
        }

        selectedRow?.BringIntoView();
    }

    private void ApplyLocalizedChrome()
    {
        _title.Text = UiStrings.EditHistoryTitle;
        _hint.Text = UiStrings.EditHistoryHint;
    }
}
