using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 書き出し／読み込み中にフォームのクライアント領域（WAAPI ステータスバー含む）を覆うすりガラス。
/// </summary>
internal sealed class ExportGlassOverlay : FrameworkElement
{
    private const int MaxDots = 3;
    private const int FadeOutDelayMs = 1000;
    private const int FadeOutDurationMs = 300;
    private const int LogMargin = 18;

    private readonly DispatcherTimer _dotsTimer;
    private readonly DispatcherTimer _fadeDelayTimer;
    private readonly DispatcherTimer _fadeTimer;
    private readonly List<string> _logLines = [];
    private ImageBrush? _frostedBrush;
    private string _baseText = UiStrings.OverlayExporting;
    private int _dotCount = 1;
    private bool _fadePending;
    private bool _fading;
    private long _fadeStartTickMs;
    private float _fadeStartOpacity = 1f;
    private float _paintOpacity = 1f;
    private Panel? _host;

    public ExportGlassOverlay()
    {
        Focusable = false;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = true;

        _dotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _dotsTimer.Tick += (_, _) =>
        {
            _dotCount = _dotCount % MaxDots + 1;
            InvalidateVisual();
        };
        _fadeDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(FadeOutDelayMs) };
        _fadeDelayTimer.Tick += (_, _) => StartFadeOut();
        _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _fadeTimer.Tick += (_, _) => AdvanceFade();
    }

    public bool IsShowingBusy => Visibility == Visibility.Visible && !_fading && !_fadePending;

    public void ShowOverlay(Panel host, FrameworkElement captureSource, Rect coverBounds, string baseText)
    {
        CancelFade();
        EnsureParent(host, coverBounds);

        _frostedBrush = CaptureFrostedBrush(captureSource, coverBounds);
        _baseText = NormalizeMessage(baseText);
        _dotCount = 1;
        _logLines.Clear();
        _paintOpacity = 1f;

        Visibility = Visibility.Visible;
        InvalidateVisual();
        Panel.SetZIndex(this, int.MaxValue);
        _dotsTimer.Start();
    }

    public void SyncBounds(Rect coverBounds)
    {
        if (!IsShowingBusy || _host is null)
        {
            return;
        }

        ApplyBounds(coverBounds);
        InvalidateVisual();
    }

    public void SetMessage(string baseText)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetMessage(baseText));
            return;
        }

        var next = NormalizeMessage(baseText);
        if (string.Equals(_baseText, next, StringComparison.Ordinal))
        {
            return;
        }

        _baseText = next;
        InvalidateVisual();
    }

    public void AppendLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => AppendLog(text));
            return;
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                _logLines.Add(line.TrimEnd());
            }
        }

        InvalidateVisual();
    }

    public void BeginFadeOut()
    {
        if (Visibility != Visibility.Visible || _fadePending || _fading)
        {
            return;
        }

        _dotsTimer.Stop();
        _fadePending = true;
        _fadeDelayTimer.Start();
    }

    public void HideOverlay()
    {
        CancelFade();
        _dotsTimer.Stop();
        _paintOpacity = 1f;
        Visibility = Visibility.Collapsed;
        _frostedBrush = null;
        if (_host?.Children.Contains(this) == true)
        {
            _host.Children.Remove(this);
        }

        _host = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var opacity = Math.Clamp(_paintOpacity, 0f, 1f);

        if (_frostedBrush is not null)
        {
            dc.PushOpacity(opacity);
            dc.DrawRectangle(_frostedBrush, null, bounds);
            dc.Pop();
        }
        else
        {
            var tint = UiColors.ForControlBack(UiColors.SurfaceBack);
            dc.DrawRectangle(
                UiColors.Brush(Color.FromArgb((byte)Math.Round(255 * opacity), tint.R, tint.G, tint.B)),
                null,
                bounds);
        }

        var tintOverlay = UiColors.SurfaceBack;
        dc.DrawRectangle(
            UiColors.Brush(Color.FromArgb((byte)Math.Round(140 * opacity), tintOverlay.R, tintOverlay.G, tintOverlay.B)),
            null,
            bounds);

        DrawLog(dc, opacity);
        DrawMessage(dc, opacity);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        double.IsInfinity(availableSize.Width) || double.IsInfinity(availableSize.Height)
            ? new Size(0, 0)
            : availableSize;

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    private void EnsureParent(Panel host, Rect coverBounds)
    {
        if (!ReferenceEquals(_host, host))
        {
            _host?.Children.Remove(this);
            _host = host;
        }

        // 常に末尾へ載せ直し、他子より前面に描画する
        if (host.Children.Contains(this))
        {
            host.Children.Remove(this);
        }

        host.Children.Add(this);
        Panel.SetZIndex(this, int.MaxValue);
        ApplyBounds(coverBounds);
    }

    private void ApplyBounds(Rect coverBounds)
    {
        Width = coverBounds.Width;
        Height = coverBounds.Height;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(coverBounds.Left, coverBounds.Top, 0, 0);
    }

    private static string NormalizeMessage(string baseText) =>
        string.IsNullOrWhiteSpace(baseText) ? UiStrings.OverlayLoading : baseText.Trim();

    private void StartFadeOut()
    {
        _fadeDelayTimer.Stop();
        _fadePending = false;
        if (Visibility != Visibility.Visible)
        {
            return;
        }

        _logLines.Clear();
        InvalidateVisual();
        _fading = true;
        _fadeStartOpacity = _paintOpacity;
        _fadeStartTickMs = Environment.TickCount64;
        _fadeTimer.Start();
    }

    private void AdvanceFade()
    {
        var progress = Math.Clamp((Environment.TickCount64 - _fadeStartTickMs) / (float)FadeOutDurationMs, 0f, 1f);
        _paintOpacity = _fadeStartOpacity * (1f - progress);
        InvalidateVisual();
        if (progress >= 1f)
        {
            HideOverlay();
        }
    }

    private void CancelFade()
    {
        _fadeDelayTimer.Stop();
        _fadeTimer.Stop();
        _fadePending = false;
        _fading = false;
        _paintOpacity = 1f;
    }

    private void DrawMessage(DrawingContext dc, float opacity)
    {
        var typeface = new Typeface(AppFonts.AppFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var fore = UiColors.Brush(UiColors.PrimaryFore);
        var baseFormatted = new FormattedText(
            _baseText,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            15,
            fore,
            dpi);
        // ドット数で本文が横に動かないよう、常に MaxDots 分の幅で中央寄せする。
        var dotsReserve = new FormattedText(
            " " + new string('.', MaxDots),
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            15,
            fore,
            dpi);
        var dotsText = " " + new string('.', _dotCount);
        var dotsFormatted = new FormattedText(
            dotsText,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            15,
            fore,
            dpi);
        var x = (ActualWidth - (baseFormatted.Width + dotsReserve.Width)) / 2;
        var y = (ActualHeight - baseFormatted.Height) / 2;
        dc.PushOpacity(opacity);
        DrawTextWithOutline(dc, baseFormatted, new Point(x, y), opacity, _baseText, typeface, 15);
        DrawTextWithOutline(dc, dotsFormatted, new Point(x + baseFormatted.Width, y), opacity, dotsText, typeface, 15);
        dc.Pop();
    }

    private void DrawLog(DrawingContext dc, float opacity)
    {
        if (_logLines.Count == 0)
        {
            return;
        }

        var typeface = AppFonts.LogTypeface;
        var sample = new FormattedText(
            "Ag",
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var lineHeight = sample.Height + 3;
        var maximumBottom = ActualHeight - LogMargin;
        var maximumWidth = Math.Max(1, ActualWidth - LogMargin * 2);
        var availableHeight = Math.Max(0, maximumBottom - LogMargin);
        var visibleCount = Math.Min(_logLines.Count, (int)(availableHeight / lineHeight));
        var first = _logLines.Count - visibleCount;
        var y = maximumBottom - visibleCount * lineHeight;
        var section = LogColorSection.Default;

        dc.PushOpacity(opacity);
        for (var i = 0; i < _logLines.Count; i++)
        {
            section = LogColorHelper.AdvanceLogColorSection(_logLines[i], section);
            if (i < first)
            {
                continue;
            }

            var fore = LogColorHelper.ColorForLogLine(_logLines[i], section);
            var formatted = new FormattedText(
                _logLines[i],
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                UiColors.Brush(fore),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = maximumWidth,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            var shadow = new FormattedText(
                _logLines[i],
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                UiColors.Brush(Color.FromArgb(210, 0, 0, 0)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = maximumWidth,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            dc.DrawText(shadow, new Point(LogMargin + 1, y + 1));
            dc.DrawText(formatted, new Point(LogMargin, y));
            y += lineHeight;
        }

        dc.Pop();
    }

    private void DrawTextWithOutline(
        DrawingContext dc,
        FormattedText text,
        Point location,
        float opacity,
        string rawText,
        Typeface typeface,
        double fontSize)
    {
        var outlineBrush = UiColors.Brush(Color.FromArgb((byte)Math.Round(255 * opacity), 0, 0, 0));
        foreach (var (dx, dy) in OutlineOffsets)
        {
            var outline = new FormattedText(
                rawText,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                outlineBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(outline, new Point(location.X + dx, location.Y + dy));
        }

        dc.DrawText(text, location);
    }

    private static readonly (int Dx, int Dy)[] OutlineOffsets =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1),
    ];

    /// <summary>
    /// ホスト（オーバーレイ含む）ではなく、キャプチャ対象ビジュアルを直接 Render する。
    /// DockPanel 子を Margin 基準で合成する旧実装は WPF レイアウトと合わずフロストが空になっていた。
    /// </summary>
    private static ImageBrush? CaptureFrostedBrush(FrameworkElement captureSource, Rect coverBounds)
    {
        if (coverBounds.Width <= 0 || coverBounds.Height <= 0)
        {
            return null;
        }

        try
        {
            captureSource.UpdateLayout();
            var fullW = Math.Max(1, (int)Math.Ceiling(captureSource.ActualWidth));
            var fullH = Math.Max(1, (int)Math.Ceiling(captureSource.ActualHeight));
            if (fullW <= 1 || fullH <= 1)
            {
                return null;
            }

            var rtb = new RenderTargetBitmap(fullW, fullH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(captureSource);

            var cropX = Math.Clamp((int)Math.Floor(coverBounds.X), 0, fullW - 1);
            var cropY = Math.Clamp((int)Math.Floor(coverBounds.Y), 0, fullH - 1);
            var cropW = Math.Clamp((int)Math.Ceiling(coverBounds.Width), 1, fullW - cropX);
            var cropH = Math.Clamp((int)Math.Ceiling(coverBounds.Height), 1, fullH - cropY);

            BitmapSource source = rtb;
            if (cropX != 0 || cropY != 0 || cropW != fullW || cropH != fullH)
            {
                var cropped = new CroppedBitmap(rtb, new Int32Rect(cropX, cropY, cropW, cropH));
                cropped.Freeze();
                source = cropped;
            }

            var scaled = ScaleBitmap(source, Math.Max(1, cropW / 6), Math.Max(1, cropH / 6));
            var tiny = ScaleBitmap(scaled, Math.Max(1, cropW / 20), Math.Max(1, cropH / 20));
            var brush = new ImageBrush(tiny)
            {
                Stretch = Stretch.Fill,
                Opacity = 1,
            };
            brush.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource ScaleBitmap(BitmapSource source, int width, int height)
    {
        var scaled = new TransformedBitmap(
            source,
            new ScaleTransform(
                width / (double)source.PixelWidth,
                height / (double)source.PixelHeight));
        scaled.Freeze();
        return scaled;
    }
}
