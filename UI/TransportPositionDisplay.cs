using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

internal sealed class TransportPositionDisplay : UserControl
{
    private readonly TransportMetronomeButton _metronomeButton;
    private readonly TextBlock _signatureText;
    private readonly TextBlock _musicalText;
    private readonly TextBlock _timeText;
    private TransportPositionInfo? _position;

    public TransportPositionDisplay()
    {
        Width = DesignMetrics.TransportPositionWidth;
        Height = DesignMetrics.TransportPositionHeight;
        Focusable = false;

        var grid = new Grid
        {
            Background = UiColors.Brush(UiColors.ForControlBack(UiColors.TransportBack)),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(DesignMetrics.TransportMetronomeHitWidth),
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _metronomeButton = new TransportMetronomeButton
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _metronomeButton.Click += (_, _) => MetronomeInvoked?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(_metronomeButton, 0);
        grid.Children.Add(_metronomeButton);

        var textPanel = new Grid { Margin = new Thickness(0) };
        Grid.SetColumn(textPanel, 1);
        _signatureText = CreatePositionTextBlock();
        _signatureText.Margin = new Thickness(DesignMetrics.TransportSignatureLeftInText, 0, 0, 0);
        _signatureText.HorizontalAlignment = HorizontalAlignment.Left;
        _musicalText = CreatePositionTextBlock();
        _musicalText.Margin = new Thickness(DesignMetrics.TransportMusicalLeftInText, 0, 0, 0);
        _musicalText.HorizontalAlignment = HorizontalAlignment.Left;
        _timeText = CreatePositionTextBlock();
        _timeText.Margin = new Thickness(DesignMetrics.TransportTimeLeftInText, 0, DesignMetrics.Dip(8), 0);
        _timeText.HorizontalAlignment = HorizontalAlignment.Left;
        textPanel.Children.Add(_signatureText);
        textPanel.Children.Add(_musicalText);
        textPanel.Children.Add(_timeText);
        grid.Children.Add(textPanel);

        Content = grid;
        ApplyColors();
        ApplyLocalizedTips();
        SyncMetronomeButtonContent();
        UpdatePositionText();
    }

    public event EventHandler? MetronomeInvoked;

    public bool IsMetronomeEnabled
    {
        get => _metronomeButton.IsActive;
        set => _metronomeButton.IsActive = value && _metronomeButton.IsEnabled;
    }

    public TransportPositionInfo? Position
    {
        get => _position;
        set
        {
            if (_position == value)
            {
                return;
            }

            _position = value;
            var available = value is { HasMusicalPosition: true };
            _metronomeButton.IsEnabled = available;
            if (!available)
            {
                IsMetronomeEnabled = false;
            }

            SyncMetronomeButtonContent();
            UpdatePositionText();
        }
    }

    public void ApplyColors()
    {
        if (Content is Grid grid)
        {
            grid.Background = UiColors.Brush(UiColors.ForControlBack(UiColors.TransportBack));
        }

        _metronomeButton.ApplyColors();
        SyncMetronomeButtonContent();
        UpdatePositionText();
    }

    public void ApplyLocalizedTips() =>
        TipService.Set(_metronomeButton, UiStrings.TipTransportMetronome);

    public void PulseMetronomeFeedback()
    {
        _metronomeButton.BeginShortcutFeedback();
        _metronomeButton.EndShortcutFeedback();
    }

    public bool IsMetronomeHitAtScreenPoint(Point screenPoint)
    {
        if (!_metronomeButton.IsEnabled || !_metronomeButton.IsVisible)
        {
            return false;
        }

        var topLeft = _metronomeButton.PointToScreen(new Point(0, 0));
        var rect = new Rect(topLeft, new Size(_metronomeButton.ActualWidth, _metronomeButton.ActualHeight));
        return rect.Contains(screenPoint);
    }

    public void RestoreMetronomeTipIfHovered()
    {
        if (!_metronomeButton.IsMouseOver)
        {
            return;
        }

        TipService.Show(UiStrings.TipTransportMetronome, _metronomeButton);
    }

    private static TextBlock CreatePositionTextBlock() => new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        FontWeight = FontWeights.Bold,
        FontSize = AppFonts.DipFromPoints(9.5),
    };

    private void SyncMetronomeButtonContent()
    {
        var hasMusical = _position is { HasMusicalPosition: true };
        _metronomeButton.BpmText = hasMusical && _position is { } p
            ? Math.Round(p.Bpm).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "---";
    }

    private void UpdatePositionText()
    {
        var position = Position;
        var hasMusical = position is { HasMusicalPosition: true };
        var musicalFore = hasMusical ? UiColors.TransportFore : UiColors.TransportDisabledFore;
        var timeFore = position is not null ? UiColors.TransportFore : UiColors.TransportDisabledFore;

        _signatureText.Text = hasMusical && position is { } signaturePosition
            ? $"{signaturePosition.Numerator}/{signaturePosition.Denominator}"
            : "--/--";
        _musicalText.Text = hasMusical && position is { } musical
            ? $"{Math.Max(0, musical.Bar):000}:{musical.Beat}:{musical.Subdivision}"
            : "000:1:1";
        var elapsed = position?.Time ?? TimeSpan.Zero;
        var hours = Math.Max(0L, (long)elapsed.TotalHours);
        _timeText.Text = $"{hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}";

        _signatureText.Foreground = UiColors.Brush(musicalFore);
        _musicalText.Foreground = UiColors.Brush(musicalFore);
        _timeText.Foreground = UiColors.Brush(timeFore);
    }
}

internal sealed class TransportMetronomeButton : Button
{
    private const double ShortcutFadeDurationMs = 180d;
    private const int BpmTextLeftDesign = 25;
    private const int NoteGlyphDesign = 30;
    private const float NoteGlyphScale = 0.75f;

    private readonly DispatcherTimer _shortcutFadeTimer;
    private string _bpmText = "---";
    private bool _isActive;
    private double _shortcutFeedbackLevel;
    private long _shortcutFadeStartMs;

    public TransportMetronomeButton()
    {
        Focusable = false;
        Cursor = Cursors.Hand;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Template = new ControlTemplate(typeof(Button));
        IsEnabled = false;

        _shortcutFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _shortcutFadeTimer.Tick += (_, _) => UpdateShortcutFeedbackFade();
        IsEnabledChanged += (_, _) => OnEnabledChanged();
        ApplyColors();
    }

    public Color HoverBackColor { get; set; }
    public Color PressedBackColor { get; set; }
    public Color ActiveForeColor { get; set; }

    public string BpmText
    {
        get => _bpmText;
        set
        {
            var next = value ?? "---";
            if (_bpmText == next)
            {
                return;
            }

            _bpmText = next;
            InvalidateVisual();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            InvalidateVisual();
        }
    }

    public void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.TransportBack));
        Foreground = UiColors.Brush(UiColors.TransportFore);
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        ActiveForeColor = UiColors.ForControlBack(UiColors.SeekCyan);
        InvalidateVisual();
    }

    public void BeginShortcutFeedback()
    {
        if (!IsEnabled)
        {
            return;
        }

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

    // TransportIconButton と同様、ホバー／押下の変化で再描画する。
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateVisual();
    }

    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        InvalidateVisual();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        var backColor = UiColors.ForControlBack(UiColors.TransportBack);
        dc.DrawRectangle(UiColors.Brush(backColor), null, bounds);

        var hoverLevel = IsEnabled
            ? (IsMouseOver ? 1d : _shortcutFeedbackLevel)
            : 0d;
        var back = IsPressed
            ? PressedBackColor
            : BlendColor(backColor, HoverBackColor, hoverLevel);
        if (IsPressed || hoverLevel > 0d)
        {
            var hoverBounds = new Rect(2, 5, Math.Max(0, bounds.Width - 4), Math.Max(0, bounds.Height - 10));
            dc.DrawRectangle(UiColors.Brush(back), null, hoverBounds);
        }

        var fore = !IsEnabled
            ? UiColors.TransportDisabledFore
            : _isActive
                ? ActiveForeColor
                : UiColors.TransportFore;

        var noteScale = bounds.Height > 0
            ? bounds.Height / NoteGlyphDesign * NoteGlyphScale
            : 0d;
        if (noteScale > 0d)
        {
            dc.PushTransform(new TranslateTransform(0, (bounds.Height - NoteGlyphDesign * noteScale) / 2));
            dc.PushTransform(new ScaleTransform(noteScale, noteScale));
            var pen = new Pen(UiColors.Brush(fore), 1.6 / NoteGlyphScale)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();
            var brush = UiColors.Brush(fore);
            // WinForms DrawQuarterNote(x=5, y=7): 符頭 (5,18,7×5) + 幹 (11,19)→(11,7)
            const double noteX = 5d;
            const double noteY = 7d;
            dc.DrawEllipse(brush, null, new Point(noteX + 3.5, noteY + 11 + 2.5), 3.5, 2.5);
            dc.DrawLine(pen, new Point(noteX + 6, noteY + 12), new Point(noteX + 6, noteY));
            dc.Pop();
            dc.Pop();
        }

        var typeface = new Typeface(AppFonts.AppFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formatted = new FormattedText(
            _bpmText,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            AppFonts.DipFromPoints(9.5),
            UiColors.Brush(fore),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(
            formatted,
            new Point(DesignMetrics.Dip(BpmTextLeftDesign), (bounds.Height - formatted.Height) / 2));
    }

    private void OnEnabledChanged()
    {
        if (!IsEnabled)
        {
            _shortcutFeedbackLevel = 0d;
            _shortcutFadeTimer.Stop();
            Cursor = Cursors.Arrow;
        }
        else
        {
            Cursor = Cursors.Hand;
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
