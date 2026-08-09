using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>ダークテーマの枠付き TextBox。</summary>
internal sealed class DarkBorderTextBox : UserControl
{
    public static readonly DependencyProperty TextProperty =
        TextBox.TextProperty.AddOwner(typeof(DarkBorderTextBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty BorderColorProperty =
        DependencyProperty.Register(nameof(BorderColor), typeof(Color), typeof(DarkBorderTextBox),
            new FrameworkPropertyMetadata(UiColors.ChromeBorder, OnChromeChanged));

    public static readonly DependencyProperty InputBackProperty =
        DependencyProperty.Register(nameof(InputBack), typeof(Color), typeof(DarkBorderTextBox),
            new FrameworkPropertyMetadata(UiColors.DialogInputBack, OnChromeChanged));

    private readonly Border _border = new()
    {
        BorderThickness = new Thickness(1),
        Padding = new Thickness(6, 3, 6, 3),
    };
    private readonly TextBox _textBox = new()
    {
        BorderThickness = new Thickness(0),
        Background = Brushes.Transparent,
        Padding = new Thickness(0),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    public DarkBorderTextBox()
    {
        ApplyChrome();
        _textBox.SetBinding(TextBox.TextProperty,
            new System.Windows.Data.Binding(nameof(Text)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
        _textBox.SetBinding(TextBox.ForegroundProperty,
            new System.Windows.Data.Binding(nameof(Foreground)) { Source = this });
        _textBox.SetBinding(TextBox.FontFamilyProperty,
            new System.Windows.Data.Binding(nameof(FontFamily)) { Source = this });
        _textBox.SetBinding(TextBox.FontSizeProperty,
            new System.Windows.Data.Binding(nameof(FontSize)) { Source = this });
        _textBox.SetBinding(TextBox.IsReadOnlyProperty,
            new System.Windows.Data.Binding(nameof(IsReadOnly)) { Source = this });
        _textBox.SetBinding(TextBox.IsEnabledProperty,
            new System.Windows.Data.Binding(nameof(IsEnabled)) { Source = this });
        _border.Child = _textBox;
        Content = _border;
        Foreground = UiColors.Brush(UiColors.PrimaryFore);
        CaretBrush = UiColors.AccentCyan;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public Color InputBack
    {
        get => (Color)GetValue(InputBackProperty);
        set => SetValue(InputBackProperty, value);
    }

    public bool IsReadOnly
    {
        get => _textBox.IsReadOnly;
        set => _textBox.IsReadOnly = value;
    }

    public Color CaretBrush
    {
        get => _textBox.CaretBrush is SolidColorBrush brush ? brush.Color : UiColors.AccentCyan;
        set => _textBox.CaretBrush = UiColors.Brush(value);
    }

    public TextBox InnerTextBox => _textBox;

    public void ApplyColors()
    {
        InputBack = UiColors.DialogInputBack;
        BorderColor = UiColors.ChromeBorder;
        Foreground = UiColors.Brush(UiColors.PrimaryFore);
    }

    private static void OnChromeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DarkBorderTextBox control)
        {
            control.ApplyChrome();
        }
    }

    private void ApplyChrome()
    {
        _border.Background = UiColors.Brush(UiColors.ForControlBack(InputBack));
        _border.BorderBrush = UiColors.Brush(BorderColor);
        _textBox.SelectionBrush = UiColors.Brush(UiColors.AccentCyan);
        _textBox.SelectionOpacity = 0.35;
    }
}

/// <summary>DropDownList 専用のダークテーマ ComboBox。</summary>
internal sealed class DarkDropDownComboBox : ComboBox
{
    public DarkDropDownComboBox()
    {
        IsEditable = false;
        ApplyColors();
    }

    public void ApplyListItemHeight() =>
        ItemContainerStyle = DarkComboBoxHelpers.CreateItemContainerStyle(FontSize);

    public void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.DialogInputBack));
        Foreground = UiColors.Brush(UiColors.ProjectBarInputFore);
        BorderBrush = UiColors.Brush(UiColors.ChromeBorder);
        BorderThickness = new Thickness(1);
        ApplyListItemHeight();
    }
}

/// <summary>プロジェクト名の編集と選択に使うダークテーマ ComboBox（編集可）。</summary>
internal sealed class DarkProjectComboBox : ComboBox
{
    public DarkProjectComboBox()
    {
        IsEditable = true;
        ApplyColors();
    }

    public void ApplyListItemHeight() =>
        ItemContainerStyle = DarkComboBoxHelpers.CreateItemContainerStyle(FontSize);

    public void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.DialogInputBack));
        Foreground = UiColors.Brush(UiColors.ProjectBarInputFore);
        BorderBrush = UiColors.Brush(UiColors.ChromeBorder);
        BorderThickness = new Thickness(1);
        ApplyListItemHeight();
    }

    /// <summary>編集欄のテキスト選択ハイライトを解除する。</summary>
    public void ClearTextSelection()
    {
        if (Template?.FindName("PART_EditableTextBox", this) is not TextBox edit)
        {
            return;
        }

        edit.SelectionLength = 0;
        edit.CaretIndex = Math.Min(edit.CaretIndex, edit.Text?.Length ?? 0);
    }

    public void DismissTransientSelection() => ClearTextSelection();

    public void SetControlHeight(int targetHeight)
    {
        if (targetHeight > 0)
        {
            Height = targetHeight;
        }
    }

    public void RefreshEditAlignment()
    {
        if (Template?.FindName("PART_EditableTextBox", this) is TextBox edit)
        {
            edit.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }
}

internal static class DarkComboBoxHelpers
{
    public static Style CreateItemContainerStyle(double fontSize)
    {
        var height = Math.Max(1, Math.Ceiling(fontSize + 10));
        var basedOn = System.Windows.Application.Current?.TryFindResource("DarkComboBoxItemStyle") as Style;
        var style = basedOn is null
            ? new Style(typeof(ComboBoxItem))
            : new Style(typeof(ComboBoxItem), basedOn);
        style.Setters.Add(new Setter(FrameworkElement.HeightProperty, height));
        if (basedOn is null)
        {
            style.Setters.Add(new Setter(Control.ForegroundProperty, UiColors.Brush(UiColors.PrimaryFore)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        }

        return style;
    }
}
