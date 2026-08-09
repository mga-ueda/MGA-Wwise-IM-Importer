using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

internal sealed class TransportIconButton : Button
{
    private const double ShortcutFadeDurationMs = 180d;
    private readonly DispatcherTimer _shortcutFadeTimer;
    private bool _isPlaying;
    private int _waveformHeightScale = 1;
    private double _shortcutFeedbackLevel;

    public TransportIconButton(TransportIcon icon)
    {
        Icon = icon;
        Width = DesignMetrics.TransportButtonSide;
        Height = DesignMetrics.TransportButtonSide;
        Padding = new Thickness(0);
        Margin = new Thickness(0, 0, DesignMetrics.TransportButtonGap, 0);
        Focusable = false;
        Cursor = Cursors.Hand;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Template = new ControlTemplate(typeof(Button));

        _shortcutFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _shortcutFadeTimer.Tick += (_, _) => UpdateShortcutFeedbackFade();
        IsEnabledChanged += (_, _) => OnEnabledChanged();
    }

    public TransportIcon Icon { get; private set; }

    public Color HoverBackColor { get; set; }
    public Color PressedBackColor { get; set; }
    public Color AccentColor { get; set; }
    public Color ActiveForeColor { get; set; }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value)
            {
                return;
            }

            _isPlaying = value;
            InvalidateVisual();
        }
    }

    public int WaveformHeightScale
    {
        get => _waveformHeightScale;
        set
        {
            var next = value is >= 1 and <= 3 ? value : 1;
            if (_waveformHeightScale == next)
            {
                return;
            }

            _waveformHeightScale = next;
            InvalidateVisual();
        }
    }

    public void SetIcon(TransportIcon icon)
    {
        if (Icon == icon)
        {
            return;
        }

        Icon = icon;
        InvalidateVisual();
    }

    public void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.TransportBack));
        Foreground = UiColors.Brush(UiColors.TransportFore);
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        AccentColor = Colors.Transparent;
        ActiveForeColor = UiColors.ForControlBack(UiColors.SeekCyan);
        InvalidateVisual();
    }

    public void BeginShortcutFeedback()
    {
        _shortcutFadeTimer.Stop();
        _shortcutFeedbackLevel = 1d;
        InvalidateVisual();
    }

    public void EndShortcutFeedback()
    {
        if (_shortcutFeedbackLevel <= 0d)
        {
            return;
        }

        _shortcutFadeStartMs = Environment.TickCount64;
        _shortcutFadeTimer.Start();
    }

    private long _shortcutFadeStartMs;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // Background を尊重（ログボタンは LogBack、トランスポートは TransportBack）
        var backColor = Background is SolidColorBrush solid
            ? solid.Color
            : UiColors.ForControlBack(UiColors.TransportBack);
        dc.DrawRectangle(UiColors.Brush(backColor), null, bounds);

        var hoverLevel = IsEnabled
            ? (IsMouseOver ? 1d : _shortcutFeedbackLevel)
            : 0d;
        var back = IsPressed
            ? PressedBackColor
            : BlendColor(backColor, HoverBackColor, hoverLevel);
        if (IsPressed || hoverLevel > 0d)
        {
            var inset = 3d;
            var hoverBounds = new Rect(
                inset,
                inset,
                Math.Max(0, bounds.Width - inset * 2),
                Math.Max(0, bounds.Height - inset * 2));
            dc.DrawRectangle(UiColors.Brush(back), null, hoverBounds);
            if (AccentColor.A > 0)
            {
                var accentPen = new Pen(UiColors.Brush(AccentColor), 1);
                accentPen.Freeze();
                dc.DrawRectangle(null, accentPen, hoverBounds);
            }
        }

        var fore = !IsEnabled
            ? UiColors.TransportDisabledFore
            : Icon == TransportIcon.PlayPause && IsPlaying
                ? ActiveForeColor
                : UiColors.TransportFore;

        if (Icon == TransportIcon.WaveformHeight)
        {
            DrawWaveformHeightLabel(dc, fore, WaveformHeightScale, bounds, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            return;
        }

        TransportIconDrawing.DrawIcon(
            dc,
            Icon,
            bounds,
            fore,
            IsEnabled,
            IsPlaying,
            ActiveForeColor);
    }

    private void OnEnabledChanged()
    {
        if (!IsEnabled)
        {
            _shortcutFeedbackLevel = 0d;
            _shortcutFadeTimer.Stop();
        }

        InvalidateVisual();
    }

    private void UpdateShortcutFeedbackFade()
    {
        var elapsed = Math.Max(0L, Environment.TickCount64 - _shortcutFadeStartMs);
        var progress = Math.Clamp(elapsed / ShortcutFadeDurationMs, 0d, 1d);
        _shortcutFeedbackLevel = 1d - progress;
        if (progress >= 1d)
        {
            _shortcutFadeTimer.Stop();
            _shortcutFeedbackLevel = 0d;
        }

        InvalidateVisual();
    }

    private static void DrawWaveformHeightLabel(
        DrawingContext dc,
        Color color,
        int scale,
        Rect bounds,
        double pixelsPerDip)
    {
        scale = scale is >= 1 and <= 3 ? scale : 1;
        var label = "x" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var typeface = new Typeface(AppFonts.AppFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formatted = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            UiColors.Brush(color),
            pixelsPerDip);
        dc.DrawText(
            formatted,
            new Point(
                (bounds.Width - formatted.Width) / 2,
                (bounds.Height - formatted.Height) / 2));
    }

    private static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * amount),
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
    }
}

internal static class TransportIconDrawing
{
    public static void DrawIcon(
        DrawingContext dc,
        TransportIcon icon,
        Rect bounds,
        Color fore,
        bool enabled,
        bool isPlaying,
        Color activeFore)
    {
        const double designW = 34d;
        const double designH = 36d;
        var scale = Math.Min(bounds.Width / designW, bounds.Height / designH);
        if (scale <= 0d)
        {
            return;
        }

        var offsetX = (bounds.Width - designW * scale) * 0.5;
        var offsetY = (bounds.Height - designH * scale) * 0.5;

        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var brush = UiColors.Brush(enabled ? fore : UiColors.TransportDisabledFore);
        var pen = new Pen(UiColors.Brush(enabled ? fore : UiColors.TransportDisabledFore), 1.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();

        const double cx = 17d;
        const double cy = 18d;
        switch (icon)
        {
            case TransportIcon.PlayPause:
                if (isPlaying)
                {
                    dc.DrawRectangle(brush, null, new Rect(12, 11, 4, 14));
                    dc.DrawRectangle(brush, null, new Rect(19, 11, 4, 14));
                }
                else
                {
                    var play = new StreamGeometry();
                    using (var ctx = play.Open())
                    {
                        ctx.BeginFigure(new Point(12, 9), true, true);
                        ctx.LineTo(new Point(25, 18), true, false);
                        ctx.LineTo(new Point(12, 27), true, false);
                    }

                    play.Freeze();
                    dc.DrawGeometry(brush, null, play);
                }

                break;
            case TransportIcon.JumpToBar:
                DrawHash(dc, pen, 10, 10, 19, 16);
                break;
            case TransportIcon.GoToStart:
            case TransportIcon.GoToEnd:
                var start = icon == TransportIcon.GoToStart;
                var lineX = start ? 9d : 25d;
                dc.DrawLine(pen, new Point(lineX, 9), new Point(lineX, 27));
                DrawChevron(dc, pen, cx + (start ? 2 : -2), cy, start);
                break;
            case TransportIcon.PreviousRegion:
            case TransportIcon.NextRegion:
                var previousRegion = icon == TransportIcon.PreviousRegion;
                DrawChevron(dc, pen, cx + (previousRegion ? 3 : -3), cy, previousRegion);
                dc.DrawLine(pen, new Point(previousRegion ? 10 : 24, 10), new Point(previousRegion ? 10 : 24, 26));
                dc.DrawLine(pen, new Point(previousRegion ? 13 : 21, 13), new Point(previousRegion ? 13 : 21, 23));
                break;
            case TransportIcon.PreviousBar:
            case TransportIcon.NextBar:
                var previousBar = icon == TransportIcon.PreviousBar;
                DrawChevron(dc, pen, cx, cy, previousBar);
                dc.DrawLine(pen, new Point(previousBar ? 10 : 24, 10), new Point(previousBar ? 10 : 24, 26));
                break;
            case TransportIcon.PreviousPage:
            case TransportIcon.NextPage:
                var previousPage = icon == TransportIcon.PreviousPage;
                DrawChevron(dc, pen, cx + (previousPage ? -2 : 2), cy, previousPage);
                DrawChevron(dc, pen, cx + (previousPage ? 5 : -5), cy, previousPage);
                break;
            case TransportIcon.TimeZoomIn:
            case TransportIcon.TimeZoomOut:
            case TransportIcon.TimeZoomMax:
            case TransportIcon.TimeZoomReset:
                DrawHorizontalZoomIcon(dc, pen);
                DrawZoomModifier(dc, pen, brush, icon, cx, cy);
                break;
            case TransportIcon.AmpZoomIn:
            case TransportIcon.AmpZoomOut:
            case TransportIcon.AmpZoomMax:
            case TransportIcon.AmpZoomReset:
                DrawVerticalZoomIcon(dc, pen);
                DrawZoomModifier(dc, pen, brush, icon, cx, cy);
                break;
            case TransportIcon.Clear:
                dc.DrawRectangle(null, pen, new Rect(12, 13, 10, 13));
                dc.DrawLine(pen, new Point(10, 11), new Point(24, 11));
                dc.DrawLine(pen, new Point(14, 8), new Point(20, 8));
                dc.DrawLine(pen, new Point(15, 16), new Point(15, 23));
                dc.DrawLine(pen, new Point(19, 16), new Point(19, 23));
                break;
            case TransportIcon.Copy:
                dc.DrawRectangle(null, pen, new Rect(9, 8, 12, 14));
                dc.DrawRectangle(null, pen, new Rect(13, 12, 12, 14));
                break;
            case TransportIcon.Download:
                dc.DrawLine(pen, new Point(17, 7), new Point(17, 20));
                dc.DrawLine(pen, new Point(12, 16), new Point(17, 21));
                dc.DrawLine(pen, new Point(22, 16), new Point(17, 21));
                dc.DrawLine(pen, new Point(9, 26), new Point(25, 26));
                break;
            case TransportIcon.Folder:
                dc.DrawLine(pen, new Point(9, 12), new Point(9, 10));
                dc.DrawLine(pen, new Point(9, 10), new Point(15, 10));
                dc.DrawLine(pen, new Point(15, 10), new Point(17, 12));
                dc.DrawLine(pen, new Point(17, 12), new Point(25, 12));
                dc.DrawLine(pen, new Point(25, 12), new Point(25, 26));
                dc.DrawLine(pen, new Point(25, 26), new Point(9, 26));
                dc.DrawLine(pen, new Point(9, 26), new Point(9, 12));
                dc.DrawLine(pen, new Point(9, 15), new Point(25, 15));
                break;
            case TransportIcon.Delete:
                dc.DrawLine(pen, new Point(9, 13), new Point(25, 13));
                dc.DrawLine(pen, new Point(14, 10), new Point(20, 10));
                dc.DrawLine(pen, new Point(11, 13), new Point(12, 26));
                dc.DrawLine(pen, new Point(12, 26), new Point(22, 26));
                dc.DrawLine(pen, new Point(22, 26), new Point(23, 13));
                dc.DrawLine(pen, new Point(14, 16), new Point(14, 23));
                dc.DrawLine(pen, new Point(17, 16), new Point(17, 23));
                dc.DrawLine(pen, new Point(20, 16), new Point(20, 23));
                break;
            case TransportIcon.Lock:
                DrawPadlockBody(dc, pen);
                dc.DrawLine(pen, new Point(12.5, 16), new Point(12.5, 12.5));
                DrawArc(dc, pen, new Rect(12.5, 7.5, 9, 9), 180, 180);
                dc.DrawLine(pen, new Point(21.5, 12.5), new Point(21.5, 16));
                break;
            case TransportIcon.Unlock:
                DrawPadlockBody(dc, pen);
                dc.DrawLine(pen, new Point(12.5, 16), new Point(12.5, 11.5));
                DrawArc(dc, pen, new Rect(12.5, 6.5, 9.5, 9.5), 180, 180);
                dc.DrawLine(pen, new Point(22, 11.5), new Point(22, 13.5));
                break;
        }

        dc.Pop();
        dc.Pop();
    }

    private static void DrawChevron(DrawingContext dc, Pen pen, double centerX, double centerY, bool left)
    {
        var direction = left ? -1d : 1d;
        dc.DrawLine(pen, new Point(centerX - direction * 4, centerY - 7), new Point(centerX + direction * 3, centerY));
        dc.DrawLine(pen, new Point(centerX + direction * 3, centerY), new Point(centerX - direction * 4, centerY + 7));
    }

    private static void DrawPadlockBody(DrawingContext dc, Pen pen)
    {
        dc.DrawRectangle(null, pen, new Rect(10, 16, 14, 11));
        dc.DrawEllipse(null, pen, new Point(17, 20), 1.5, 1.5);
        dc.DrawLine(pen, new Point(17, 21.5), new Point(17, 24.5));
    }

    private static void DrawHash(DrawingContext dc, Pen pen, double x, double y, double width, double height)
    {
        dc.DrawLine(pen, new Point(x + 4, y), new Point(x + 2, y + height));
        dc.DrawLine(pen, new Point(x + 10, y), new Point(x + 8, y + height));
        dc.DrawLine(pen, new Point(x, y + 5), new Point(x + width - 5, y + 5));
        dc.DrawLine(pen, new Point(x, y + 11), new Point(x + width - 5, y + 11));
    }

    private static void DrawHorizontalZoomIcon(DrawingContext dc, Pen pen)
    {
        dc.DrawLine(pen, new Point(7, 18), new Point(27, 18));
        dc.DrawLine(pen, new Point(11, 14), new Point(7, 18));
        dc.DrawLine(pen, new Point(7, 18), new Point(11, 22));
        dc.DrawLine(pen, new Point(23, 14), new Point(27, 18));
        dc.DrawLine(pen, new Point(27, 18), new Point(23, 22));
    }

    private static void DrawVerticalZoomIcon(DrawingContext dc, Pen pen)
    {
        dc.DrawLine(pen, new Point(17, 8), new Point(17, 28));
        dc.DrawLine(pen, new Point(13, 12), new Point(17, 8));
        dc.DrawLine(pen, new Point(17, 8), new Point(21, 12));
        dc.DrawLine(pen, new Point(13, 24), new Point(17, 28));
        dc.DrawLine(pen, new Point(17, 28), new Point(21, 24));
    }

    private static void DrawArc(DrawingContext dc, Pen pen, Rect rect, double startAngle, double sweepAngle)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(
                PointOnEllipse(rect, startAngle),
                false,
                false);
            ctx.ArcTo(
                PointOnEllipse(rect, startAngle + sweepAngle),
                new Size(rect.Width / 2, rect.Height / 2),
                0,
                sweepAngle >= 180,
                SweepDirection.Clockwise,
                true,
                false);
        }

        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnEllipse(Rect rect, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        return new Point(
            cx + Math.Cos(radians) * rect.Width / 2,
            cy + Math.Sin(radians) * rect.Height / 2);
    }

    private static void DrawZoomModifier(
        DrawingContext dc,
        Pen pen,
        Brush brush,
        TransportIcon icon,
        double cx,
        double cy)
    {
        var isIn = icon is TransportIcon.TimeZoomIn or TransportIcon.AmpZoomIn;
        var isOut = icon is TransportIcon.TimeZoomOut or TransportIcon.AmpZoomOut;
        var isMax = icon is TransportIcon.TimeZoomMax or TransportIcon.AmpZoomMax;
        if (isIn || isOut)
        {
            var badgeBrush = UiColors.Brush(Color.FromArgb(220, UiColors.TransportBadgeBack.R, UiColors.TransportBadgeBack.G, UiColors.TransportBadgeBack.B));
            dc.DrawEllipse(badgeBrush, null, new Point(cx, cy), 5, 5);
            dc.DrawLine(pen, new Point(cx - 3, cy), new Point(cx + 3, cy));
            if (isIn)
            {
                dc.DrawLine(pen, new Point(cx, cy - 3), new Point(cx, cy + 3));
            }
        }
        else if (isMax)
        {
            dc.DrawRectangle(brush, null, new Rect(cx - 3, cy - 3, 6, 6));
        }
        else
        {
            dc.DrawEllipse(null, pen, new Point(cx, cy), 4, 4);
        }
    }
}
