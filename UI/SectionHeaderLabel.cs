using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// セクション見出しラベル。行の中へ上下左右にマージンを取った
/// 一段低いグレー帯を描き、テキストは帯の縦中央に置く（WinForms 同等）。
/// </summary>
/// <remarks>
/// WinForms は帯を BarMargin でインセットし、テキストはコントロール原点からの
/// <c>Padding.Left</c> に置く（帯の内側 Padding ではない）。
/// WPF の UserControl 既定テンプレートは Padding で Content 全体をインセットするため、
/// 帯まで右へずれて下の選択肢より帯が右になり「内容が帯より左にはみ出す」ように見える。
/// そのため ContentPresenter には Padding を当てず、テキスト位置にだけ使う。
/// </remarks>
internal sealed class SectionHeaderLabel : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty BarColorProperty =
        DependencyProperty.Register(nameof(BarColor), typeof(Color), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(UiColors.SectionHeaderBack, OnBarChromeChanged));

    // Tips/Log 相当の上下余白。WinForms は 96dpi 値を From96 して使う。
    public static readonly DependencyProperty BarMarginTopProperty =
        DependencyProperty.Register(nameof(BarMarginTop), typeof(double), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(DesignMetrics.From96(2), OnBarChromeChanged));

    public static readonly DependencyProperty BarMarginBottomProperty =
        DependencyProperty.Register(nameof(BarMarginBottom), typeof(double), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(DesignMetrics.From96(2), OnBarChromeChanged));

    public static readonly DependencyProperty BarMarginLeftProperty =
        DependencyProperty.Register(nameof(BarMarginLeft), typeof(double), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(DesignMetrics.From96(3), OnBarChromeChanged));

    public static readonly DependencyProperty BarMarginRightProperty =
        DependencyProperty.Register(nameof(BarMarginRight), typeof(double), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(DesignMetrics.From96(3), OnBarChromeChanged));

    public static readonly DependencyProperty BarRightInsetExtraProperty =
        DependencyProperty.Register(nameof(BarRightInsetExtra), typeof(double), typeof(SectionHeaderLabel),
            new FrameworkPropertyMetadata(0d, OnBarChromeChanged));

    private readonly Grid _root = new();
    private readonly Border _bar = new();
    private readonly TextBlock _textBlock = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
    };

    public SectionHeaderLabel()
    {
        Height = DesignMetrics.SectionHeaderHeight;
        MinHeight = DesignMetrics.CompactSectionHeaderHeight;
        Background = Brushes.Transparent;
        // WinForms: Padding はテキスト原点のみ。帯のインセットには使わない。
        Padding = new Thickness(10, 0, 4, 0);
        Foreground = UiColors.Brush(UiColors.PrimaryFore);
        // 既定 UserControl テンプレートは Padding で Content 全体をずらすため使わない。
        Template = CreateTemplate();

        _textBlock.SetBinding(TextBlock.ForegroundProperty,
            new System.Windows.Data.Binding(nameof(Foreground)) { Source = this });
        _textBlock.SetBinding(TextBlock.FontFamilyProperty,
            new System.Windows.Data.Binding(nameof(FontFamily)) { Source = this });
        _textBlock.SetBinding(TextBlock.FontSizeProperty,
            new System.Windows.Data.Binding(nameof(FontSize)) { Source = this });
        _textBlock.SetBinding(TextBlock.FontWeightProperty,
            new System.Windows.Data.Binding(nameof(FontWeight)) { Source = this });
        _textBlock.SetBinding(TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(Text)) { Source = this });

        _root.Children.Add(_bar);
        _root.Children.Add(_textBlock);
        Content = _root;
        ApplyBarChrome();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    public double BarMarginTop
    {
        get => (double)GetValue(BarMarginTopProperty);
        set => SetValue(BarMarginTopProperty, Math.Max(0d, value));
    }

    public double BarMarginBottom
    {
        get => (double)GetValue(BarMarginBottomProperty);
        set => SetValue(BarMarginBottomProperty, Math.Max(0d, value));
    }

    public double BarMarginLeft
    {
        get => (double)GetValue(BarMarginLeftProperty);
        set => SetValue(BarMarginLeftProperty, Math.Max(0d, value));
    }

    public double BarMarginRight
    {
        get => (double)GetValue(BarMarginRightProperty);
        set => SetValue(BarMarginRightProperty, Math.Max(0d, value));
    }

    public double BarRightInsetExtra
    {
        get => (double)GetValue(BarRightInsetExtraProperty);
        set => SetValue(BarRightInsetExtraProperty, Math.Max(0d, value));
    }

    public Rect GetBarBounds() =>
        new(
            BarMarginLeft,
            BarMarginTop,
            Math.Max(0d, ActualWidth - BarMarginLeft - BarMarginRight - BarRightInsetExtra),
            Math.Max(0d, ActualHeight - BarMarginTop - BarMarginBottom));

    private static ControlTemplate CreateTemplate()
    {
        // ContentPresenter に Padding をバインドしない（WinForms 同等）。
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
        return new ControlTemplate(typeof(SectionHeaderLabel))
        {
            VisualTree = presenter,
        };
    }

    private static void OnBarChromeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SectionHeaderLabel label)
        {
            label.ApplyBarChrome();
        }
    }

    private void ApplyBarChrome()
    {
        _bar.Background = WpfControlHelpers.FrozenBrush(BarColor);
        _bar.VerticalAlignment = VerticalAlignment.Stretch;
        _bar.HorizontalAlignment = HorizontalAlignment.Stretch;
        _bar.Margin = new Thickness(
            BarMarginLeft,
            BarMarginTop,
            BarMarginRight + BarRightInsetExtra,
            BarMarginBottom);
        _bar.Padding = new Thickness(0);
        _bar.Child = null;

        // テキスト左端 = コントロール原点からの Padding.Left（帯 Margin の右に重ねない）
        _textBlock.Margin = new Thickness(
            Padding.Left,
            BarMarginTop,
            Math.Max(Padding.Right, BarMarginRight + BarRightInsetExtra),
            BarMarginBottom);
        _textBlock.VerticalAlignment = VerticalAlignment.Center;
        _textBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == PaddingProperty)
        {
            ApplyBarChrome();
        }
    }
}
