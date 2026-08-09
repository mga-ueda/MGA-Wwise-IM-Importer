using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>FlatOptionRadioButton / FlatOptionCheckBox 共通のグリフ配色・DIP 寸法。</summary>
internal static class FlatOptionGlyph
{
    // WinForms: *Design @144 → DesignMetrics.Dip
    public static double LayoutGlyphSize => DesignMetrics.Dip(21);
    public static double DrawnGlyphSize => DesignMetrics.Dip(15);
    public static double GlyphGap => DesignMetrics.Dip(9);
    public static double TextGap => DesignMetrics.Dip(11);
    public static double RowHeight => DesignMetrics.FlatOptionRowHeight;

    public static Color ResolveBorderColor(bool enabled, bool isChecked, bool hovered)
    {
        if (!enabled)
        {
            return UiColors.OptionGlyphDisabled;
        }

        if (isChecked)
        {
            return hovered
                ? WpfControlHelpers.BlendColor(UiColors.OptionGlyphChecked, Colors.White, 0.22)
                : UiColors.OptionGlyphChecked;
        }

        return hovered ? UiColors.OptionGlyphHover : UiColors.OptionGlyphBorder;
    }

    public static double MeasureContentWidth(Control control, string text)
    {
        var typeface = new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch);
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            control.FlowDirection,
            typeface,
            control.FontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(control).PixelsPerDip);
        return LayoutGlyphSize + TextGap + formatted.Width + DesignMetrics.Dip(3);
    }

    public static bool IsInContentHitArea(Control control, Point point, double contentWidth)
    {
        var width = Math.Clamp(contentWidth, 1d, Math.Max(1d, control.ActualWidth));
        return point.X >= 0
            && point.Y >= 0
            && point.X < width
            && point.Y < control.ActualHeight;
    }
}

internal sealed class FlatOptionRadioButton : RadioButton
{
    private bool _hovered;

    static FlatOptionRadioButton()
    {
        FocusableProperty.OverrideMetadata(typeof(FlatOptionRadioButton), new FrameworkPropertyMetadata(false));
        FocusVisualStyleProperty.OverrideMetadata(typeof(FlatOptionRadioButton), new FrameworkPropertyMetadata(null));
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatOptionRadioButton),
            new FrameworkPropertyMetadata(typeof(FlatOptionRadioButton)));
    }

    public FlatOptionRadioButton()
    {
        // TargetType=RadioButton の暗黙スタイル（中塗り付き）を当てない
        Style = null;
        Height = FlatOptionGlyph.RowHeight;
        Margin = DesignMetrics.FlatOptionControlMargin;
        Cursor = Cursors.Hand;
        Foreground = UiColors.Brush(UiColors.PlaylistOptionFore);
        Background = Brushes.Transparent;
        Template = CreateTemplate();
        IsEnabledChanged += (_, _) => InvalidateVisual();
    }

    public void ApplyColors() => InvalidateVisual();

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        UpdateHover(e.GetPosition(this));
        base.OnMouseEnter(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        UpdateHover(e.GetPosition(this));
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        SetHovered(false);
        base.OnMouseLeave(e);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsInContentHitArea(e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsInContentHitArea(e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        InvalidateVisual();
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var back = Background is SolidColorBrush brush ? brush.Color : Colors.Transparent;
        dc.DrawRectangle(WpfControlHelpers.FrozenBrush(back), null, new Rect(RenderSize));

        var glyphSize = FlatOptionGlyph.LayoutGlyphSize;
        var glyph = new Rect(
            DesignMetrics.Dip(2),
            (ActualHeight - glyphSize) / 2d,
            glyphSize - 1,
            glyphSize - 1);
        var borderColor = FlatOptionGlyph.ResolveBorderColor(IsEnabled, IsChecked == true, _hovered);
        var borderPen = new Pen(WpfControlHelpers.FrozenBrush(borderColor), DesignMetrics.From96(1.4))
        {
            LineJoin = PenLineJoin.Round,
        };
        if (borderPen.CanFreeze)
        {
            borderPen.Freeze();
        }

        // 外円は枠のみ（中は塗らない）。選択時は内側ドットだけ塗る。
        dc.DrawEllipse(null, borderPen, new Point(glyph.X + glyph.Width / 2d, glyph.Y + glyph.Height / 2d),
            glyph.Width / 2d, glyph.Height / 2d);

        if (IsChecked == true)
        {
            var inset = DesignMetrics.From96(4d);
            var dot = new Rect(glyph.X + inset, glyph.Y + inset, glyph.Width - inset * 2, glyph.Height - inset * 2);
            var dotBrush = WpfControlHelpers.FrozenBrush(
                IsEnabled ? UiColors.OptionGlyphChecked : UiColors.OptionGlyphDisabled);
            dc.DrawEllipse(dotBrush, null,
                new Point(dot.X + dot.Width / 2d, dot.Y + dot.Height / 2d), dot.Width / 2d, dot.Height / 2d);
        }

        DrawText(dc, glyphSize);
    }

    private bool IsInContentHitArea(Point point) =>
        FlatOptionGlyph.IsInContentHitArea(this, point, FlatOptionGlyph.MeasureContentWidth(this, Content as string ?? string.Empty));

    private void UpdateHover(Point point) => SetHovered(IsInContentHitArea(point));

    private void SetHovered(bool hovered)
    {
        if (_hovered == hovered)
        {
            return;
        }

        _hovered = hovered;
        InvalidateVisual();
    }

    private void DrawText(DrawingContext dc, double glyphSize)
    {
        var text = Content as string ?? string.Empty;
        var textLeft = glyphSize + FlatOptionGlyph.TextGap;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var fore = IsEnabled
            ? Foreground is SolidColorBrush solid ? solid.Color : UiColors.PrimaryFore
            : UiColors.OptionGlyphDisabled;
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            WpfControlHelpers.FrozenBrush(fore),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1d, ActualWidth - textLeft),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(formatted, new Point(textLeft, (ActualHeight - formatted.Height) / 2d));
    }

    private static ControlTemplate CreateTemplate() =>
        new(typeof(RadioButton))
        {
            VisualTree = new FrameworkElementFactory(typeof(Border))
            {
                Name = "Root",
            },
        };
}

internal sealed class FlatOptionCheckBox : CheckBox
{
    private bool _hovered;

    static FlatOptionCheckBox()
    {
        FocusableProperty.OverrideMetadata(typeof(FlatOptionCheckBox), new FrameworkPropertyMetadata(false));
        FocusVisualStyleProperty.OverrideMetadata(typeof(FlatOptionCheckBox), new FrameworkPropertyMetadata(null));
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatOptionCheckBox),
            new FrameworkPropertyMetadata(typeof(FlatOptionCheckBox)));
    }

    public FlatOptionCheckBox()
    {
        // TargetType=CheckBox の暗黙スタイルを当てず、OnRender 側で描画する
        Style = null;
        // WinForms は AutoSize。固定 Height だと "Additive\nLayer" など複数行が切れる。
        MinHeight = FlatOptionGlyph.RowHeight;
        Margin = DesignMetrics.FlatOptionControlMargin;
        ClipToBounds = false;
        Cursor = Cursors.Hand;
        Foreground = UiColors.Brush(UiColors.PlaylistOptionFore);
        Background = Brushes.Transparent;
        Template = CreateTemplate();
        IsEnabledChanged += (_, _) => InvalidateVisual();
    }

    public void ApplyColors() => InvalidateVisual();

    protected override Size MeasureOverride(Size constraint)
    {
        var text = Content as string ?? string.Empty;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        // \n を複数行として測る（MaxTextWidth 無しだと1行扱いになることがある）
        var maxTextWidth = constraint.Width is > 0 and < double.PositiveInfinity
            ? Math.Max(1d, constraint.Width - Padding.Left - Padding.Right
                - FlatOptionGlyph.LayoutGlyphSize - FlatOptionGlyph.TextGap)
            : 0d;
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if (maxTextWidth > 0d)
        {
            formatted.MaxTextWidth = maxTextWidth;
        }

        var glyph = FlatOptionGlyph.LayoutGlyphSize;
        var width = Padding.Left + Padding.Right + glyph + FlatOptionGlyph.TextGap + formatted.Width + DesignMetrics.Dip(3);
        var height = Math.Max(
            MinHeight,
            Padding.Top + Padding.Bottom + Math.Max(glyph, formatted.Height) + DesignMetrics.Dip(6));
        return new Size(width, height);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        UpdateHover(e.GetPosition(this));
        base.OnMouseEnter(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        UpdateHover(e.GetPosition(this));
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        SetHovered(false);
        base.OnMouseLeave(e);
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsInContentHitArea(e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsInContentHitArea(e.GetPosition(this)))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonUp(e);
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        InvalidateVisual();
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var back = Background is SolidColorBrush brush ? brush.Color : Colors.Transparent;
        dc.DrawRectangle(WpfControlHelpers.FrozenBrush(back), null, new Rect(RenderSize));

        var isChecked = IsChecked == true;
        var glyphSlotSize = FlatOptionGlyph.LayoutGlyphSize;
        var glyphSize = FlatOptionGlyph.DrawnGlyphSize;
        var glyph = new Rect(
            Padding.Left + DesignMetrics.Dip(2) + (glyphSlotSize - glyphSize) / 2d,
            (ActualHeight - glyphSize) / 2d,
            glyphSize - 1,
            glyphSize - 1);
        var borderColor = FlatOptionGlyph.ResolveBorderColor(IsEnabled, isChecked, _hovered);
        var borderPen = new Pen(
            WpfControlHelpers.FrozenBrush(borderColor),
            Math.Max(1d, DesignMetrics.From96(1.4)))
        {
            LineJoin = PenLineJoin.Miter,
        };
        if (borderPen.CanFreeze)
        {
            borderPen.Freeze();
        }

        // WinForms 同等: 選択時はシアン塗り＋暗いチェック印。
        if (isChecked)
        {
            var fill = IsEnabled ? UiColors.OptionGlyphChecked : UiColors.OptionGlyphDisabled;
            dc.DrawRectangle(WpfControlHelpers.FrozenBrush(fill), null, glyph);
        }

        dc.DrawRectangle(null, borderPen, glyph);

        if (isChecked)
        {
            var checkPen = new Pen(
                WpfControlHelpers.FrozenBrush(UiColors.OptionGlyphCheckMark),
                Math.Max(1d, DesignMetrics.From96(1.8)))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            if (checkPen.CanFreeze)
            {
                checkPen.Freeze();
            }

            var p1 = new Point(glyph.Left + glyph.Width * 0.22, glyph.Top + glyph.Height * 0.52);
            var p2 = new Point(glyph.Left + glyph.Width * 0.43, glyph.Top + glyph.Height * 0.73);
            var p3 = new Point(glyph.Left + glyph.Width * 0.80, glyph.Top + glyph.Height * 0.29);
            dc.DrawLine(checkPen, p1, p2);
            dc.DrawLine(checkPen, p2, p3);
        }

        var textLeft = Padding.Left + glyphSlotSize + FlatOptionGlyph.TextGap;
        var text = Content as string ?? string.Empty;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var fore = IsEnabled
            ? Foreground is SolidColorBrush solid ? solid.Color : UiColors.PrimaryFore
            : UiColors.OptionGlyphDisabled;
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            WpfControlHelpers.FrozenBrush(fore),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1d, ActualWidth - textLeft),
        };
        dc.DrawText(formatted, new Point(textLeft, (ActualHeight - formatted.Height) / 2d));
    }

    private bool IsInContentHitArea(Point point)
    {
        var text = Content as string ?? string.Empty;
        return FlatOptionGlyph.IsInContentHitArea(this, point, FlatOptionGlyph.MeasureContentWidth(this, text));
    }

    private void UpdateHover(Point point) => SetHovered(IsInContentHitArea(point));

    private void SetHovered(bool hovered)
    {
        if (_hovered == hovered)
        {
            return;
        }

        _hovered = hovered;
        InvalidateVisual();
    }

    private static ControlTemplate CreateTemplate() =>
        new(typeof(CheckBox))
        {
            VisualTree = new FrameworkElementFactory(typeof(Border))
            {
                Name = "Root",
            },
        };
}
