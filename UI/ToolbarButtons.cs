using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

internal abstract class SquareToolbarButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public static readonly DependencyProperty HoverBackColorProperty =
        DependencyProperty.Register(nameof(HoverBackColor), typeof(Color), typeof(SquareToolbarButton),
            new FrameworkPropertyMetadata(
                UiColors.ForControlBack(UiColors.TransportHoverBack),
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PressedBackColorProperty =
        DependencyProperty.Register(nameof(PressedBackColor), typeof(Color), typeof(SquareToolbarButton),
            new FrameworkPropertyMetadata(
                UiColors.ForControlBack(UiColors.TransportPressedBack),
                FrameworkPropertyMetadataOptions.AffectsRender));

    static SquareToolbarButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SquareToolbarButton),
            new FrameworkPropertyMetadata(typeof(SquareToolbarButton)));
        FocusableProperty.OverrideMetadata(typeof(SquareToolbarButton), new FrameworkPropertyMetadata(false));
        FocusVisualStyleProperty.OverrideMetadata(typeof(SquareToolbarButton), new FrameworkPropertyMetadata(null));
    }

    protected SquareToolbarButton()
    {
        Width = DesignMetrics.ToolbarButtonSide;
        Height = DesignMetrics.ToolbarButtonSide;
        MinWidth = DesignMetrics.ToolbarButtonSide;
        MinHeight = DesignMetrics.ToolbarButtonSide;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.ProjectBarBack));
        Foreground = UiColors.Brush(UiColors.LogButtonFore);
        Cursor = Cursors.Hand;
    }

    public Color HoverBackColor
    {
        get => (Color)GetValue(HoverBackColorProperty);
        set => SetValue(HoverBackColorProperty, value);
    }

    public Color PressedBackColor
    {
        get => (Color)GetValue(PressedBackColorProperty);
        set => SetValue(PressedBackColorProperty, value);
    }

    protected bool IsHovered => _hovered;
    protected bool IsPressing => _pressed;

    public virtual void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.ProjectBarBack));
        Foreground = UiColors.Brush(UiColors.LogButtonFore);
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        InvalidateVisual();
    }

    protected Color ResolveFillColor() =>
        _pressed ? PressedBackColor : _hovered ? HoverBackColor :
        Background is SolidColorBrush brush ? brush.Color : UiColors.ProjectBarBack;

    protected Color ResolveForeColor() =>
        Foreground is SolidColorBrush brush ? brush.Color : UiColors.LogButtonFore;

    protected void PaintFillBackground(DrawingContext dc)
    {
        var back = Background is SolidColorBrush brush ? brush.Color : UiColors.ProjectBarBack;
        dc.DrawRectangle(WpfControlHelpers.FrozenBrush(back), null, new Rect(RenderSize));
        dc.DrawRectangle(WpfControlHelpers.FrozenBrush(ResolveFillColor()), null, new Rect(RenderSize));
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        _hovered = true;
        InvalidateVisual();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hovered = false;
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
}

/// <summary>音声出力設定（言語切替の左）。歯車を描画し、薄い枠付きの正方形。</summary>
internal sealed class SettingsGearButton : SquareToolbarButton
{
    public SettingsGearButton()
    {
        Margin = new Thickness(0, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public void RefreshAppearance() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        PaintFillBackground(dc);
        DrawGear(dc, ResolveForeColor(), ResolveFillColor());
    }

    private static void DrawGear(DrawingContext dc, Color color, Color holeColor)
    {
        const int teeth = 8;
        const double side = 24;
        var cx = side * 0.5;
        var cy = side * 0.5;
        var outer = side * 0.30;
        var inner = side * 0.19;
        var hub = side * 0.09;
        var points = new Point[teeth * 4];
        for (var i = 0; i < teeth; i++)
        {
            var baseAngle = i / (double)teeth * Math.PI * 2d - Math.PI / teeth;
            var step = Math.PI * 2d / teeth;
            points[i * 4] = Polar(cx, cy, inner, baseAngle);
            points[i * 4 + 1] = Polar(cx, cy, outer, baseAngle + step * 0.28);
            points[i * 4 + 2] = Polar(cx, cy, outer, baseAngle + step * 0.72);
            points[i * 4 + 3] = Polar(cx, cy, inner, baseAngle + step);
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: true, isClosed: true);
            for (var i = 1; i < points.Length; i++)
            {
                ctx.LineTo(points[i], true, false);
            }
        }

        geometry.Freeze();
        dc.DrawGeometry(WpfControlHelpers.FrozenBrush(color), null, geometry);
        dc.DrawEllipse(WpfControlHelpers.FrozenBrush(holeColor), null, new Point(cx, cy), hub, hub);
        var ringPen = new Pen(WpfControlHelpers.FrozenBrush(color), Math.Max(1d, side * 0.05))
        {
            LineJoin = PenLineJoin.Round,
        };
        if (ringPen.CanFreeze)
        {
            ringPen.Freeze();
        }

        dc.DrawEllipse(null, ringPen, new Point(cx, cy), hub * 1.7, hub * 1.7);
    }

    private static Point Polar(double cx, double cy, double radius, double angle) =>
        new(cx + Math.Cos(angle) * radius, cy + Math.Sin(angle) * radius);
}

/// <summary>表示言語切替（スペクトラム左）。JP／EN を描画し、薄い枠付きの正方形。</summary>
internal sealed class LanguageFlagButton : SquareToolbarButton
{
    public LanguageFlagButton()
    {
        Margin = new Thickness(8, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public void RefreshAppearance() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        PaintFillBackground(dc);
        var label = UiStrings.IsJapanese
            ? UiStrings.LanguageBadgeJapanese
            : UiStrings.LanguageBadgeEnglish;
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formatted = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            10,
            WpfControlHelpers.FrozenBrush(ResolveForeColor()),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(
            formatted,
            new Point((ActualWidth - formatted.Width) / 2d, (ActualHeight - formatted.Height) / 2d));
    }
}

/// <summary>ユーザーマニュアルを開くボタン（歯車の左）。「?」を描画する。</summary>
internal sealed class ManualHelpButton : SquareToolbarButton
{
    public ManualHelpButton()
    {
        Margin = new Thickness(0, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public void RefreshAppearance() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        PaintFillBackground(dc);
        var typeface = new Typeface(new FontFamily("Segoe UI Semibold"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formatted = new FormattedText(
            "?",
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            Math.Max(12d, Math.Min(ActualWidth, ActualHeight) * 0.52),
            WpfControlHelpers.FrozenBrush(ResolveForeColor()),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(
            formatted,
            new Point((ActualWidth - formatted.Width) / 2d, (ActualHeight - formatted.Height) / 2d));
    }
}

/// <summary>Tips 枠表示のオン／オフ切替。吹き出しを描画し、オフ時はグレーアウトする。</summary>
internal sealed class TipsToggleButton : SquareToolbarButton
{
    private bool _checked = true;

    public TipsToggleButton()
    {
        Margin = new Thickness(0, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            RefreshAppearance();
        }
    }

    public void RefreshAppearance() => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        PaintFillBackground(dc);
        var fill = ResolveFillColor();
        var iconColor = _checked
            ? ResolveForeColor()
            : Color.FromArgb(128, ResolveForeColor().R, ResolveForeColor().G, ResolveForeColor().B);
        DrawBalloon(dc, iconColor, fill);
    }

    private static void DrawBalloon(DrawingContext dc, Color color, Color holeColor)
    {
        const double side = 24;
        var w = side * 0.62;
        var h = side * 0.42;
        var x = (side - w) / 2d;
        var y = side * 0.24;
        var radius = h * 0.36;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            WpfControlHelpers.AddRoundedRect(ctx, new Rect(x, y, w, h), radius);
            var tailTopX = x + w * 0.28;
            ctx.BeginFigure(new Point(tailTopX, y + h - 1), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(tailTopX + w * 0.18, y + h - 1), true, false);
            ctx.LineTo(new Point(tailTopX, y + h + side * 0.14), true, false);
        }

        geometry.FillRule = FillRule.Nonzero;
        geometry.Freeze();
        dc.DrawGeometry(WpfControlHelpers.FrozenBrush(color), null, geometry);

        var dot = Math.Max(1.5, side * 0.06);
        var dotY = y + h / 2d - dot / 2d;
        for (var i = 0; i < 3; i++)
        {
            var dotX = x + w * (0.26 + 0.24 * i) - dot / 2d;
            dc.DrawEllipse(WpfControlHelpers.FrozenBrush(holeColor), null,
                new Point(dotX + dot / 2d, dotY + dot / 2d), dot / 2d, dot / 2d);
        }
    }
}
