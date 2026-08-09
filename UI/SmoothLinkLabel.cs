using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// フッタ権利表記向け。本文はミュート色、指定リンクだけ青。行間を詰めて 3 行を収める。
/// </summary>
internal sealed class SmoothLinkLabel : TextBlock
{
    public static readonly DependencyProperty LinkTextProperty =
        DependencyProperty.Register(
            nameof(LinkText),
            typeof(string),
            typeof(SmoothLinkLabel),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure, OnContentChanged));

    public static readonly DependencyProperty LineHeightScaleProperty =
        DependencyProperty.Register(
            nameof(LineHeightScale),
            typeof(double),
            typeof(SmoothLinkLabel),
            new FrameworkPropertyMetadata(0.78d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnContentChanged));

    public static readonly RoutedEvent LinkClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(LinkClick),
            RoutingStrategy.Bubble,
            typeof(EventHandler<SmoothLinkClickEventArgs>),
            typeof(SmoothLinkLabel));

    private readonly List<Hyperlink> _links = [];
    private Hyperlink? _hoveredLink;

    static SmoothLinkLabel()
    {
        FontSizeProperty.OverrideMetadata(
            typeof(SmoothLinkLabel),
            new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsMeasure, OnFontSizeChanged));
    }

    public SmoothLinkLabel()
    {
        // 著作権は NoWrap + LineBreak で 3 行固定。汎用時は呼び出し側で Wrap を指定可。
        TextWrapping = TextWrapping.NoWrap;
        Foreground = UiColors.Brush(UiColors.ActionCopyrightFore);
        Cursor = Cursors.Arrow;
        Focusable = false;
        FontFamily = AppFonts.UiFamily;
        RebuildContent();
    }

    public event EventHandler<SmoothLinkClickEventArgs> LinkClick
    {
        add => AddHandler(LinkClickEvent, value);
        remove => RemoveHandler(LinkClickEvent, value);
    }

    public string LinkText
    {
        get => (string)GetValue(LinkTextProperty);
        set => SetValue(LinkTextProperty, value);
    }

    /// <summary>1 未満で行間を詰める（Form1 LineHeightScale=0.78）。</summary>
    public double LineHeightScale
    {
        get => (double)GetValue(LineHeightScaleProperty);
        set => SetValue(LineHeightScaleProperty, Math.Clamp(value, 0.5d, 1.5d));
    }

    public void ApplyColors()
    {
        Foreground = UiColors.Brush(UiColors.ActionCopyrightFore);
        UpdateLinkBrushes();
        UpdatePlainRunBrushes();
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SmoothLinkLabel label)
        {
            label.RebuildContent();
        }
    }

    private static void OnFontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SmoothLinkLabel label)
        {
            label.ApplyLineMetrics();
        }
    }

    private void ApplyLineMetrics()
    {
        var scale = LineHeightScale;
        if (scale < 0.999d)
        {
            // WinForms SmoothLinkLabel: Font.GetHeight(g) * LineHeightScale。
            // GDI GetHeight は FontFamily.LineSpacing * em と一致（1.25 近似だと低すぎる）。
            var lineSpacing = FontFamily?.LineSpacing > 0d ? FontFamily.LineSpacing : 1.33d;
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            LineHeight = Math.Max(1d, FontSize * lineSpacing * scale);
        }
        else
        {
            ClearValue(LineHeightProperty);
            LineStackingStrategy = LineStackingStrategy.MaxHeight;
        }
    }

    private void RebuildContent()
    {
        Inlines.Clear();
        _links.Clear();
        ApplyLineMetrics();

        var text = LinkText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var bodyBrush = UiColors.Brush(UiColors.ActionCopyrightFore);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                Inlines.Add(new LineBreak());
            }

            AppendLineWithLinks(lines[i], bodyBrush);
        }
    }

    private void AppendLineWithLinks(string line, Brush bodyBrush)
    {
        if (string.IsNullOrEmpty(line))
        {
            Inlines.Add(new Run(" ") { Foreground = bodyBrush });
            return;
        }

        var remaining = line;
        while (remaining.Length > 0)
        {
            var githubAt = remaining.IndexOf("GitHub", StringComparison.Ordinal);
            var licenseAt = remaining.IndexOf(UiStrings.CopyrightLicenseLinkText, StringComparison.Ordinal);
            var nextAt = -1;
            string? nextId = null;
            var nextLen = 0;
            if (githubAt >= 0 && (licenseAt < 0 || githubAt <= licenseAt))
            {
                nextAt = githubAt;
                nextId = "github";
                nextLen = "GitHub".Length;
            }
            else if (licenseAt >= 0)
            {
                nextAt = licenseAt;
                nextId = "license";
                nextLen = UiStrings.CopyrightLicenseLinkText.Length;
            }

            if (nextAt < 0 || nextId is null)
            {
                Inlines.Add(new Run(remaining) { Foreground = bodyBrush });
                break;
            }

            if (nextAt > 0)
            {
                Inlines.Add(new Run(remaining[..nextAt]) { Foreground = bodyBrush });
            }

            var segment = remaining.Substring(nextAt, nextLen);
            var link = new Hyperlink(new Run(segment))
            {
                TextDecorations = null,
                Foreground = UiColors.Brush(UiColors.ActionLinkFore),
                Cursor = Cursors.Hand,
                Tag = nextId,
            };
            var capturedId = nextId;
            link.Click += (_, _) =>
                RaiseEvent(new SmoothLinkClickEventArgs(LinkClickEvent, this, capturedId));
            link.MouseEnter += (_, _) =>
            {
                _hoveredLink = link;
                UpdateLinkBrushes();
            };
            link.MouseLeave += (_, _) =>
            {
                if (ReferenceEquals(_hoveredLink, link))
                {
                    _hoveredLink = null;
                }

                UpdateLinkBrushes();
            };
            _links.Add(link);
            Inlines.Add(link);
            remaining = remaining[(nextAt + nextLen)..];
        }
    }

    private void UpdatePlainRunBrushes()
    {
        var body = Foreground;
        foreach (var inline in Inlines)
        {
            if (inline is Run run)
            {
                run.Foreground = body;
            }
        }
    }

    private void UpdateLinkBrushes()
    {
        var normal = UiColors.Brush(UiColors.ActionLinkFore);
        var hover = UiColors.Brush(UiColors.ActionLinkHoverFore);
        foreach (var link in _links)
        {
            link.Foreground = ReferenceEquals(link, _hoveredLink) ? hover : normal;
        }
    }
}

internal sealed class SmoothLinkClickEventArgs : RoutedEventArgs
{
    public SmoothLinkClickEventArgs(RoutedEvent routedEvent, object source, string linkId)
        : base(routedEvent, source)
    {
        LinkId = linkId;
    }

    public string LinkId { get; }
}
