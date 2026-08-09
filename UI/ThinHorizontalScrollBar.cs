using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 波形表示範囲専用の、常時表示する細い水平スクロールバー。
/// </summary>
internal sealed class ThinHorizontalScrollBar : FrameworkElement
{
    // WinForms はデバイス px 固定。DIP のままだと 150% で太く見える。
    private static double HorizontalInset => DesignMetrics.Dip(3);
    private static double MinimumThumbWidth => DesignMetrics.Dip(24);
    private static double ThumbHeight => DesignMetrics.Dip(8);

    private double _viewStart;
    private double _viewSpan = 1d;
    private bool _hovered;
    private bool _dragging;
    private double _dragOffsetX;

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(0d));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(1d));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(0.1d));

    public static readonly DependencyProperty ViewportSizeProperty =
        DependencyProperty.Register(nameof(ViewportSize), typeof(double), typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(1d, OnViewportChanged));

    static ThinHorizontalScrollBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(typeof(ThinHorizontalScrollBar)));
        FocusableProperty.OverrideMetadata(typeof(ThinHorizontalScrollBar), new FrameworkPropertyMetadata(false));
        HeightProperty.OverrideMetadata(
            typeof(ThinHorizontalScrollBar),
            new FrameworkPropertyMetadata(DesignMetrics.WaveformScrollBarHeight));
    }

    public ThinHorizontalScrollBar()
    {
        Height = DesignMetrics.WaveformScrollBarHeight;
        Cursor = Cursors.Arrow;
    }

    public event EventHandler<double>? ScrollRequested;
    public event EventHandler? ScrollCompleted;
    public event EventHandler? ValueChanged;

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public double ViewportSize
    {
        get => (double)GetValue(ViewportSizeProperty);
        set => SetValue(ViewportSizeProperty, value);
    }

    public void SetViewport(double viewStart, double viewSpan)
    {
        _viewSpan = Math.Clamp(viewSpan, 0d, 1d);
        if (!_dragging)
        {
            _viewStart = Math.Clamp(viewStart, 0d, Math.Max(0d, 1d - _viewSpan));
            Value = _viewStart;
        }

        InvalidateVisual();
    }

    public void ApplyColors() => InvalidateVisual();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var thumb = GetThumbBounds();
        if (thumb.Width <= 0 || thumb.Height <= 0)
        {
            base.OnMouseLeftButtonDown(e);
            return;
        }

        var point = e.GetPosition(this);
        if (thumb.Contains(point))
        {
            _dragging = true;
            _dragOffsetX = point.X - thumb.Left;
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var page = _viewSpan;
        RequestScroll(point.X < thumb.Left ? _viewStart - page : _viewStart + page);
        ScrollCompleted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging && IsMouseCaptured)
        {
            RequestScroll(StartFromThumbLeft(e.GetPosition(this).X - _dragOffsetX));
            e.Handled = true;
            return;
        }

        var hovered = GetThumbBounds().Contains(e.GetPosition(this));
        if (_hovered != hovered)
        {
            _hovered = hovered;
            InvalidateVisual();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            _hovered = GetThumbBounds().Contains(e.GetPosition(this));
            InvalidateVisual();
            ScrollCompleted?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (!_dragging && _hovered)
        {
            _hovered = false;
            InvalidateVisual();
        }

        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var notches = Math.Max(1d, Math.Abs(e.Delta) / 120d);
        var distance = _viewSpan * 0.1d * notches;
        RequestScroll(_viewStart + (e.Delta < 0 ? distance : -distance));
        ScrollCompleted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(
            UiColors.Brush(UiColors.ForControlBack(UiColors.WaveformScrollTrack)),
            null,
            new Rect(RenderSize));

        var thumb = GetThumbBounds();
        if (thumb.Width <= 0 || thumb.Height <= 0)
        {
            return;
        }

        var color = _hovered ? UiColors.WaveformScrollThumbHover : UiColors.WaveformScrollThumb;
        var brush = UiColors.Brush(UiColors.ForControlBack(color));
        var capSize = Math.Min(thumb.Height, thumb.Width);
        if (thumb.Width <= capSize)
        {
            dc.DrawEllipse(brush, null, new Point(thumb.X + capSize / 2d, thumb.Y + capSize / 2d), capSize / 2d, capSize / 2d);
            return;
        }

        dc.DrawRectangle(brush, null, new Rect(thumb.X + capSize / 2d, thumb.Y, thumb.Width - capSize, thumb.Height));
        dc.DrawEllipse(brush, null, new Point(thumb.X + capSize / 2d, thumb.Y + capSize / 2d), capSize / 2d, capSize / 2d);
        dc.DrawEllipse(brush, null, new Point(thumb.Right - capSize / 2d, thumb.Y + capSize / 2d), capSize / 2d, capSize / 2d);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThinHorizontalScrollBar bar && !bar._dragging)
        {
            bar._viewStart = Math.Clamp((double)e.NewValue, 0d, Math.Max(0d, 1d - bar._viewSpan));
            bar.InvalidateVisual();
            bar.ValueChanged?.Invoke(bar, EventArgs.Empty);
        }
    }

    private static void OnViewportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThinHorizontalScrollBar bar)
        {
            bar._viewSpan = Math.Clamp((double)e.NewValue, 0d, 1d);
            bar.InvalidateVisual();
        }
    }

    private Rect GetTrackBounds()
    {
        return new Rect(
            HorizontalInset,
            Math.Max(0d, (ActualHeight - ThumbHeight) / 2d),
            Math.Max(0d, ActualWidth - HorizontalInset * 2),
            Math.Min(ThumbHeight, ActualHeight));
    }

    private Rect GetThumbBounds()
    {
        var track = GetTrackBounds();
        if (track.Width <= 0 || track.Height <= 0)
        {
            return Rect.Empty;
        }

        if (_viewSpan >= 1d - 1e-9)
        {
            return track;
        }

        var thumbWidth = Math.Clamp(
            track.Width * _viewSpan,
            Math.Min(MinimumThumbWidth, track.Width),
            track.Width);
        var travel = Math.Max(0d, track.Width - thumbWidth);
        var maxStart = Math.Max(0d, 1d - _viewSpan);
        var ratio = maxStart > 1e-12 ? _viewStart / maxStart : 0d;
        var left = track.Left + travel * ratio;
        return new Rect(left, track.Top, thumbWidth, track.Height);
    }

    private double StartFromThumbLeft(double thumbLeft)
    {
        var track = GetTrackBounds();
        var thumb = GetThumbBounds();
        var travel = Math.Max(0d, track.Width - thumb.Width);
        if (travel == 0)
        {
            return 0d;
        }

        var pixel = Math.Clamp(thumbLeft - track.Left, 0d, travel);
        return pixel / travel * Math.Max(0d, 1d - _viewSpan);
    }

    private void RequestScroll(double viewStart)
    {
        var clamped = Math.Clamp(viewStart, 0d, Math.Max(0d, 1d - _viewSpan));
        if (Math.Abs(clamped - _viewStart) < 1e-12)
        {
            return;
        }

        _viewStart = clamped;
        Value = clamped;
        InvalidateVisual();
        ScrollRequested?.Invoke(this, clamped);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
}
