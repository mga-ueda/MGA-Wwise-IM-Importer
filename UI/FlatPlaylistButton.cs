using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 押下時にも文字位置を動かさない Playlist 専用フラットボタン。
/// 塗り・枠・文字はすべて OnRender で描く（暗黙 Button スタイルを当てない）。
/// </summary>
internal sealed class FlatPlaylistButton : Button
{
    public static readonly DependencyProperty BorderColorProperty =
        DependencyProperty.Register(nameof(BorderColor), typeof(Color), typeof(FlatPlaylistButton),
            new FrameworkPropertyMetadata(
                UiColors.PlaylistButtonBorder,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderSizeProperty =
        DependencyProperty.Register(nameof(BorderSize), typeof(double), typeof(FlatPlaylistButton),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    static FlatPlaylistButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatPlaylistButton),
            new FrameworkPropertyMetadata(typeof(FlatPlaylistButton)));
        FocusableProperty.OverrideMetadata(typeof(FlatPlaylistButton), new FrameworkPropertyMetadata(false));
        FocusVisualStyleProperty.OverrideMetadata(typeof(FlatPlaylistButton), new FrameworkPropertyMetadata(null));
    }

    public FlatPlaylistButton()
    {
        // TargetType=Button の DarkButtonStyle が Template を差し替え、
        // OnRender の再生塗り／遷移枠を隠してしまうため無効化する。
        Style = null;
        Template = CreateTemplate();
        ClipToBounds = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(6, 0, 6, 0);
        HorizontalContentAlignment = HorizontalAlignment.Left;
        VerticalContentAlignment = VerticalAlignment.Center;
        ApplyIdleStyle();
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public double BorderSize
    {
        get => (double)GetValue(BorderSizeProperty);
        set => SetValue(BorderSizeProperty, value);
    }

    public static double MeasureDisplayTextWidth(string text, double fontSize, FontFamily fontFamily)
    {
        var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White,
            1.0);
        return formatted.Width;
    }

    /// <summary>通常時。パネルと同じ地表色（PlaylistAutoBack は再生中のみ）。</summary>
    public void ApplyIdleStyle()
    {
        Background = UiColors.Brush(UiColors.PlaylistBack);
        Foreground = UiColors.Brush(UiColors.PlaylistDefaultFore);
        BorderColor = UiColors.PlaylistButtonBorder;
        BorderSize = 0;
    }

    /// <summary>自動再生中の塗り（紺）。</summary>
    public void ApplyAutoStyle()
    {
        Background = UiColors.Brush(UiColors.PlaylistAutoBack);
        Foreground = UiColors.Brush(UiColors.PlaylistActiveFore);
        BorderColor = UiColors.PlaylistButtonBorder;
        BorderSize = 0;
    }

    /// <summary>手動再生中の塗り。</summary>
    public void ApplyManualStyle()
    {
        Background = UiColors.Brush(UiColors.PlaylistManualBack);
        Foreground = UiColors.Brush(UiColors.PlaylistActiveFore);
        BorderColor = UiColors.PlaylistManualBorder;
        BorderSize = 1;
    }

    public void ApplyTransitionStyle()
    {
        Background = UiColors.Brush(UiColors.PlaylistBack);
        Foreground = UiColors.Brush(UiColors.PlaylistActiveFore);
        BorderColor = UiColors.PlaylistTransitionBorder;
        BorderSize = 1;
    }

    /// <summary>
    /// テンプレート Border が一瞬 0 サイズでも、ボタン全面をヒット対象にする。
    /// （再生中の InvalidateVisual 連打でクリックが隣行へ貫通するのを防ぐ）
    /// </summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (new Rect(RenderSize).Contains(hitTestParameters.HitPoint))
        {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }

        return null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var back = Background is SolidColorBrush brush ? brush.Color : UiColors.PlaylistBack;
        dc.DrawRectangle(WpfControlHelpers.FrozenBrush(back), null, new Rect(0, 0, width, height));

        var borderSize = BorderSize;
        if (borderSize > 0)
        {
            // 枠が外へはみ出して親にクリップされないよう、内側に完全に収める。
            // DPI 丸めで右／下辺が 1px 欠けるのを避けるため、半ピクセル内側へ寄せる。
            var pen = new Pen(WpfControlHelpers.FrozenBrush(BorderColor), borderSize)
            {
                LineJoin = PenLineJoin.Miter,
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat,
            };
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var pixel = dpi > 0 ? 1d / dpi : 1d;
            var inset = (borderSize / 2d) + (pixel * 0.5d);
            var strokeWidth = Math.Max(0d, width - (inset * 2d));
            var strokeHeight = Math.Max(0d, height - (inset * 2d));
            if (strokeWidth > 0 && strokeHeight > 0)
            {
                dc.DrawRectangle(null, pen, new Rect(inset, inset, strokeWidth, strokeHeight));
            }
        }

        var text = Content as string ?? Content?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var fore = IsEnabled
            ? Foreground is SolidColorBrush foreBrush ? foreBrush.Color : UiColors.PlaylistDefaultFore
            : UiColors.ActionButtonDisabledFore;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            FontSize,
            WpfControlHelpers.FrozenBrush(fore),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1d, width - Padding.Left - Padding.Right),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(formatted, new Point(Padding.Left, (height - formatted.Height) / 2d));
    }

    private static ControlTemplate CreateTemplate()
    {
        // ヒットテスト用の透明ルートのみ。見た目は OnRender に任せる。
        var root = new FrameworkElementFactory(typeof(Border));
        root.Name = "Root";
        root.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        root.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        return new ControlTemplate(typeof(Button)) { VisualTree = root };
    }
}
