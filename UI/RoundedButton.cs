using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 角丸ボタン。標準 Button のフォーカス枠／矩形ボーダーを描かず、親背景で角を埋める。
/// </summary>
internal sealed class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(RoundedButton),
            new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverBackColorProperty =
        DependencyProperty.Register(nameof(HoverBackColor), typeof(Color?), typeof(RoundedButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PressedBackColorProperty =
        DependencyProperty.Register(nameof(PressedBackColor), typeof(Color?), typeof(RoundedButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisabledBackColorProperty =
        DependencyProperty.Register(nameof(DisabledBackColor), typeof(Color), typeof(RoundedButton),
            new FrameworkPropertyMetadata(
                UiColors.ForControlBack(UiColors.ActionButtonInnerBack),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisabledForeColorProperty =
        DependencyProperty.Register(nameof(DisabledForeColor), typeof(Color), typeof(RoundedButton),
            new FrameworkPropertyMetadata(
                UiColors.ActionButtonDisabledFore,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderColorProperty =
        DependencyProperty.Register(nameof(BorderColor), typeof(Color?), typeof(RoundedButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HoverBorderColorProperty =
        DependencyProperty.Register(nameof(HoverBorderColor), typeof(Color?), typeof(RoundedButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PressedBorderColorProperty =
        DependencyProperty.Register(nameof(PressedBorderColor), typeof(Color?), typeof(RoundedButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DisabledBorderColorProperty =
        DependencyProperty.Register(nameof(DisabledBorderColor), typeof(Color?), typeof(RoundedButton),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderSizeProperty =
        DependencyProperty.Register(nameof(BorderSize), typeof(double), typeof(RoundedButton),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    static RoundedButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(RoundedButton),
            new FrameworkPropertyMetadata(typeof(RoundedButton)));
        FocusableProperty.OverrideMetadata(typeof(RoundedButton), new FrameworkPropertyMetadata(false));
        FocusVisualStyleProperty.OverrideMetadata(typeof(RoundedButton), new FrameworkPropertyMetadata(null));
    }

    public RoundedButton()
    {
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(8, 2, 8, 2);
        Cursor = Cursors.Hand;
        IsEnabledChanged += (_, _) => InvalidateVisual();
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Color? HoverBackColor
    {
        get => (Color?)GetValue(HoverBackColorProperty);
        set => SetValue(HoverBackColorProperty, value);
    }

    public Color? PressedBackColor
    {
        get => (Color?)GetValue(PressedBackColorProperty);
        set => SetValue(PressedBackColorProperty, value);
    }

    public Color DisabledBackColor
    {
        get => (Color)GetValue(DisabledBackColorProperty);
        set => SetValue(DisabledBackColorProperty, value);
    }

    public Color DisabledForeColor
    {
        get => (Color)GetValue(DisabledForeColorProperty);
        set => SetValue(DisabledForeColorProperty, value);
    }

    public Color? BorderColor
    {
        get => (Color?)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public Color? HoverBorderColor
    {
        get => (Color?)GetValue(HoverBorderColorProperty);
        set => SetValue(HoverBorderColorProperty, value);
    }

    public Color? PressedBorderColor
    {
        get => (Color?)GetValue(PressedBorderColorProperty);
        set => SetValue(PressedBorderColorProperty, value);
    }

    public Color? DisabledBorderColor
    {
        get => (Color?)GetValue(DisabledBorderColorProperty);
        set => SetValue(DisabledBorderColorProperty, value);
    }

    public double BorderSize
    {
        get => (double)GetValue(BorderSizeProperty);
        set => SetValue(BorderSizeProperty, value);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        _hover = true;
        InvalidateVisual();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = false;
        _pressed = false;
        InvalidateVisual();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _pressed = true;
        InvalidateVisual();
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _pressed = false;
        InvalidateVisual();
        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var parentBack = ResolveParentBackColor();
        dc.DrawRectangle(WpfControlHelpers.FrozenBrush(parentBack), null, new Rect(RenderSize));

        var fill = ResolveFillColor();
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var fillGeometry = WpfControlHelpers.RoundedRectGeometry(bounds, CornerRadius);
        dc.DrawGeometry(WpfControlHelpers.FrozenBrush(fill), null, fillGeometry);

        var borderColor = ResolveBorderColor();
        if (BorderSize > 0 && borderColor is Color border)
        {
            var inset = BorderSize / 2d;
            var borderBounds = new Rect(
                inset,
                inset,
                Math.Max(0d, ActualWidth - BorderSize),
                Math.Max(0d, ActualHeight - BorderSize));
            var borderGeometry = WpfControlHelpers.RoundedRectGeometry(
                borderBounds,
                Math.Max(0d, CornerRadius - inset));
            var pen = new Pen(WpfControlHelpers.FrozenBrush(border), BorderSize)
            {
                LineJoin = PenLineJoin.Round,
            };
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            dc.DrawGeometry(null, pen, borderGeometry);
        }

        var text = Content as string ?? Content?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textColor = IsEnabled
            ? Foreground is SolidColorBrush solid ? solid.Color : UiColors.PrimaryFore
            : DisabledForeColor;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            WpfControlHelpers.FrozenBrush(textColor),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1d, ActualWidth - Padding.Left - Padding.Right),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        var textX = Padding.Left + Math.Max(0d, (ActualWidth - Padding.Left - Padding.Right - formatted.Width) / 2d);
        var textY = (ActualHeight - formatted.Height) / 2d;
        dc.DrawText(formatted, new Point(textX, textY));
    }

    private Color ResolveParentBackColor()
    {
        var background = Parent switch
        {
            System.Windows.Controls.Panel panel => panel.Background,
            System.Windows.Controls.Control control => control.Background,
            _ => null,
        };

        if (background is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return UiColors.WindowBack;
    }

    private Color ResolveFillColor()
    {
        if (!IsEnabled)
        {
            return DisabledBackColor;
        }

        if (_pressed && PressedBackColor is Color pressed)
        {
            return pressed;
        }

        if (_hover && HoverBackColor is Color hover)
        {
            return hover;
        }

        if (Background is SolidColorBrush back)
        {
            return back.Color;
        }

        return UiColors.ActionButtonInnerBack;
    }

    private Color? ResolveBorderColor()
    {
        if (!IsEnabled)
        {
            return DisabledBorderColor ?? BorderColor;
        }

        if (_pressed)
        {
            return PressedBorderColor ?? BorderColor;
        }

        if (_hover)
        {
            return HoverBorderColor ?? BorderColor;
        }

        return BorderColor;
    }
}
