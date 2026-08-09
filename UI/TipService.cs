using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// コントロールホバー時の説明文をログ上部 Tips 枠へ出す。
/// </summary>
internal static class TipService
{
    private static readonly ConditionalWeakTable<FrameworkElement, TipBinding> Bindings = new();
    private static readonly ConditionalWeakTable<FrameworkElement, object> WiredParents = new();

    private const int MinVisibleLines = 5;

    /// <summary>Tips のない領域へ移っても、この時間は直前の Tips を残す。</summary>
    private static readonly TimeSpan ClearHoldDuration = TimeSpan.FromSeconds(1);

    private static TextBlock? _display;
    private static FrameworkElement? _host;
    private static object? _activeSource;
    private static int _suspendCount;
    private static bool _layoutWired;
    private static bool _enabled = true;
    private static DispatcherTimer? _clearTimer;
    private static object? _pendingClearSource;
    private static bool _pendingClearAny;

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            if (!_enabled)
            {
                ClearImmediate();
            }

            RelayoutHost();
        }
    }

    public static void BindDisplay(TextBlock display, FrameworkElement host)
    {
        _display = display;
        _host = host;
        display.TextWrapping = TextWrapping.Wrap;
        display.TextTrimming = TextTrimming.None;

        if (!_layoutWired)
        {
            _layoutWired = true;
            host.SizeChanged += (_, _) => RelayoutHost();
            var fontSizeDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                TextBlock.FontSizeProperty, typeof(TextBlock));
            fontSizeDescriptor?.AddValueChanged(display, (_, _) => RelayoutHost());
        }

        SetDisplayText(null);
    }

    public static void Set(FrameworkElement control, string? tip, bool respectsEnabled = true)
    {
        var binding = Bindings.GetOrCreateValue(control);
        binding.Text = tip ?? string.Empty;
        binding.RespectsEnabled = respectsEnabled;
        EnsureWired(control, binding);
    }

    public static void Show(string? text, object source, bool respectsEnabled = true)
    {
        if (_suspendCount > 0)
        {
            return;
        }

        if (!_enabled && respectsEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Clear(source);
            return;
        }

        CancelPendingClear();
        _activeSource = source;
        SetDisplayText(text);
    }

    public static void Clear()
    {
        ScheduleClear(source: null, clearAny: true);
    }

    public static void Clear(object source)
    {
        if (!ReferenceEquals(_activeSource, source))
        {
            return;
        }

        ScheduleClear(source, clearAny: false);
    }

    public static void Suspend()
    {
        _suspendCount++;
        if (_suspendCount == 1)
        {
            ClearImmediate();
        }
    }

    public static void Resume()
    {
        if (_suspendCount > 0)
        {
            _suspendCount--;
        }
    }

    private static void EnsureWired(FrameworkElement control, TipBinding binding)
    {
        if (!binding.Wired)
        {
            binding.Wired = true;
            control.MouseEnter += (_, _) =>
            {
                if (_suspendCount > 0)
                {
                    return;
                }

                if (!_enabled && binding.RespectsEnabled)
                {
                    return;
                }

                Show(binding.Text, control, binding.RespectsEnabled);
            };
            control.MouseLeave += (_, _) => Clear(control);
            control.IsEnabledChanged += (_, _) =>
            {
                if (!control.IsEnabled && ReferenceEquals(_activeSource, control))
                {
                    ScheduleClear(control, clearAny: false);
                }
            };
            control.Unloaded += (_, _) =>
            {
                if (ReferenceEquals(_activeSource, control))
                {
                    // アンロード時はホバー継続の対象が消えるので即消去。
                    ClearImmediateIfSource(control);
                }
            };
        }

        EnsureParentWired(control);
    }

    private static void ScheduleClear(object? source, bool clearAny)
    {
        if (_display is null && _activeSource is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_display?.Text) && _activeSource is null)
        {
            return;
        }

        _pendingClearAny = clearAny;
        _pendingClearSource = source;
        var timer = EnsureClearTimer();
        timer.Stop();
        timer.Start();
    }

    private static void CancelPendingClear()
    {
        _pendingClearAny = false;
        _pendingClearSource = null;
        _clearTimer?.Stop();
    }

    private static DispatcherTimer EnsureClearTimer()
    {
        if (_clearTimer is not null)
        {
            return _clearTimer;
        }

        _clearTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = ClearHoldDuration,
        };
        _clearTimer.Tick += (_, _) =>
        {
            _clearTimer.Stop();
            var clearAny = _pendingClearAny;
            var source = _pendingClearSource;
            _pendingClearAny = false;
            _pendingClearSource = null;

            if (clearAny)
            {
                ClearImmediate();
                return;
            }

            ClearImmediateIfSource(source);
        };
        return _clearTimer;
    }

    private static void ClearImmediate()
    {
        CancelPendingClear();
        _activeSource = null;
        SetDisplayText(null);
    }

    private static void ClearImmediateIfSource(object? source)
    {
        if (source is null || !ReferenceEquals(_activeSource, source))
        {
            CancelPendingClear();
            return;
        }

        ClearImmediate();
    }

    private static void EnsureParentWired(FrameworkElement control)
    {
        if (control.Parent is not FrameworkElement parent)
        {
            return;
        }

        _ = WiredParents.GetValue(parent, static p =>
        {
            p.PreviewMouseMove += Parent_PreviewMouseMove;
            p.MouseLeave += Parent_MouseLeave;
            return new object();
        });
    }

    private static void Parent_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement parent || _suspendCount > 0)
        {
            return;
        }

        var hit = FindDisabledTipElement(parent, e.GetPosition(parent));
        if (hit is not null && Bindings.TryGetValue(hit, out var binding))
        {
            if (!_enabled && binding.RespectsEnabled)
            {
                return;
            }

            Show(binding.Text, hit, binding.RespectsEnabled);
            return;
        }

        if (_activeSource is FrameworkElement { IsEnabled: false } active
            && ReferenceEquals(GetParent(active), parent))
        {
            Clear(active);
        }
    }

    private static void Parent_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement parent)
        {
            return;
        }

        if (_activeSource is not FrameworkElement { IsEnabled: false } active
            || !ReferenceEquals(GetParent(active), parent))
        {
            return;
        }

        if (!parent.IsMouseOver)
        {
            Clear(active);
        }
    }

    private static FrameworkElement? GetParent(FrameworkElement element) =>
        element.Parent as FrameworkElement;

    private static FrameworkElement? FindDisabledTipElement(FrameworkElement parent, Point parentPoint)
    {
        var hit = parent.InputHitTest(parentPoint) as DependencyObject;
        while (hit is not null)
        {
            if (hit is FrameworkElement element
                && !element.IsEnabled
                && Bindings.TryGetValue(element, out _))
            {
                return element;
            }

            hit = GetVisualOrContentParent(hit);
        }

        return null;
    }

    /// <summary>
    /// InputHitTest は FlowDocument / Run など非 Visual を返すことがある。
    /// VisualTreeHelper.GetParent はそれらで例外になるため分岐する。
    /// </summary>
    private static DependencyObject? GetVisualOrContentParent(DependencyObject current)
    {
        if (current is Visual)
        {
            return VisualTreeHelper.GetParent(current);
        }

        if (current is ContentElement content)
        {
            var parent = ContentOperations.GetParent(content);
            if (parent is not null)
            {
                return parent;
            }

            return LogicalTreeHelper.GetParent(current);
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static void SetDisplayText(string? text)
    {
        if (_display is null)
        {
            return;
        }

        var value = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (!_display.Dispatcher.CheckAccess())
        {
            _display.Dispatcher.BeginInvoke(() => ApplyDisplayText(value));
            return;
        }

        ApplyDisplayText(value);
    }

    private static void ApplyDisplayText(string value)
    {
        if (_display is null)
        {
            return;
        }

        if (!string.Equals(_display.Text, value, StringComparison.Ordinal))
        {
            _display.Text = value;
        }

        RelayoutHost();
    }

    private static void RelayoutHost()
    {
        if (_display is null || _host is null)
        {
            return;
        }

        if (!_enabled)
        {
            SetHostHeight(0);
            return;
        }

        var chromeHeight = MeasureChromeHeight(_host, _display);
        var padding = _display.Padding;
        var contentWidth = Math.Max(1d, _host.ActualWidth - padding.Left - padding.Right);
        // 空でも本文 5 行でも同じ測り方にする（行高×5 と FormattedText 実測で差が出ないように）。
        var minContentHeight = MeasureContentHeight(_display, BuildMinLinesSample(), contentWidth);
        var contentHeight = string.IsNullOrEmpty(_display.Text)
            ? minContentHeight
            : Math.Max(minContentHeight, MeasureContentHeight(_display, _display.Text, contentWidth));

        SetHostHeight(chromeHeight + padding.Top + padding.Bottom + contentHeight);
    }

    private static string BuildMinLinesSample()
    {
        // 実テキスト 5 行と同じく改行で積む（単行高×5 だと行間が合わず空時だけ低くなる）。
        return string.Join("\n", Enumerable.Repeat("Ag", MinVisibleLines));
    }

    /// <summary>WinForms TextRenderer の +1px 余裕に相当。空／本文で同じ値を使う。</summary>
    private static double MeasureContentHeight(TextBlock display, string text, double contentWidth)
    {
        var formatted = CreateMeasureText(display, text, contentWidth);
        return Math.Max(1d, formatted.Height + 1d);
    }

    private static FormattedText CreateMeasureText(TextBlock display, string text, double contentWidth)
    {
        var typeface = new Typeface(display.FontFamily, display.FontStyle, display.FontWeight, display.FontStretch);
        return new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            display.FlowDirection,
            typeface,
            display.FontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(display).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1d, contentWidth),
            Trimming = TextTrimming.None,
        };
    }

    private static double MeasureChromeHeight(FrameworkElement host, FrameworkElement display)
    {
        // tipsPanel は Border → 内側 DockPanel。Panel 以外だと見出し高さが落ちて本文が欠ける。
        var panel = host as Panel
            ?? (host as Decorator)?.Child as Panel;
        if (panel is null)
        {
            return 0d;
        }

        var height = 0d;
        foreach (UIElement child in panel.Children)
        {
            if (ReferenceEquals(child, display))
            {
                continue;
            }

            if (child is not FrameworkElement fe)
            {
                continue;
            }

            var childHeight = fe.ActualHeight;
            if (childHeight <= 0)
            {
                childHeight = fe.DesiredSize.Height;
            }

            if (childHeight <= 0 && fe is SectionHeaderLabel)
            {
                childHeight = DesignMetrics.CompactSectionHeaderHeight;
            }

            height += childHeight + fe.Margin.Top + fe.Margin.Bottom;
        }

        return height;
    }

    private static void SetHostHeight(double height)
    {
        if (_host is null)
        {
            return;
        }

        if (Math.Abs(_host.Height - height) > 0.5)
        {
            _host.Height = height;
        }
    }

    private sealed class TipBinding
    {
        public string Text = string.Empty;
        public bool RespectsEnabled = true;
        public bool Wired;
    }
}
