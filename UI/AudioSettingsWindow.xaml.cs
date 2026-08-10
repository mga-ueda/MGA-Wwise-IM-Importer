using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

internal partial class AudioSettingsWindow : Window
{
    private static readonly Regex DigitsOnly = new("[^0-9]+");

    private readonly FadeCurveRow _waveformFadeInRow;
    private readonly FadeCurveRow _waveformFadeOutRow;
    private readonly FadeCurveRow _playlistFadeInRow;
    private readonly FadeCurveRow _playlistFadeOutRow;
    private ContextMenu? _fadeCurveMenu;
    private bool _suppressDeviceReload;

    public AudioOutputSettings SelectedSettings { get; private set; }

    public RegionFadeCurveKind WaveformFadeInCurve => _waveformFadeInRow.Curve;

    public RegionFadeCurveKind WaveformFadeOutCurve => _waveformFadeOutRow.Curve;

    public RegionFadeCurveKind PlaylistFadeInCurve => _playlistFadeInRow.Curve;

    public RegionFadeCurveKind PlaylistFadeOutCurve => _playlistFadeOutRow.Curve;

    public ExpectedWaveformFormat SelectedExpectedFormat { get; private set; }

    public AudioSettingsWindow(
        AudioOutputSettings current,
        RegionFadeCurveKind waveformFadeIn,
        RegionFadeCurveKind waveformFadeOut,
        RegionFadeCurveKind playlistFadeIn,
        RegionFadeCurveKind playlistFadeOut,
        ExpectedWaveformFormat expectedFormat)
    {
        SelectedSettings = current;
        SelectedExpectedFormat = expectedFormat;

        InitializeComponent();
        WindowIconHelper.Apply(this);
        Title = UiStrings.DialogSettingsTitle;
        SourceInitialized += (_, _) =>
        {
            DarkWindowChrome.ApplyImmersiveDarkTitleBar(this);
            // メインが最前面でもダイアログが背面に回らないようにする（WinForms 同等）。
            if (Owner is { Topmost: true })
            {
                Topmost = true;
            }
        };
        // テキストボックス外をクリックしたらフォーカス（キャレット）を解放する（WinForms 同等）。
        PreviewMouseDown += (_, e) =>
        {
            if (Keyboard.FocusedElement is TextBox
                && e.OriginalSource is DependencyObject origin
                && FindAncestorTextBox(origin) is null)
            {
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, this);
            }
        };

        ApiCombo.Items.Add(new ApiItem(AudioOutputApi.WaveOut, UiStrings.LabelAudioApiWaveOut));
        ApiCombo.Items.Add(new ApiItem(AudioOutputApi.Wasapi, UiStrings.LabelAudioApiWasapi));
        ApiCombo.Items.Add(new ApiItem(AudioOutputApi.Asio, UiStrings.LabelAudioApiAsio));

        _waveformFadeInRow = CreateFadeRow(UiStrings.LabelDefaultWaveformFadeIn, waveformFadeIn, isFadeIn: true);
        _waveformFadeOutRow = CreateFadeRow(UiStrings.LabelDefaultWaveformFadeOut, waveformFadeOut, isFadeIn: false);
        _playlistFadeInRow = CreateFadeRow(UiStrings.LabelDefaultPlaylistFadeIn, playlistFadeIn, isFadeIn: true);
        _playlistFadeOutRow = CreateFadeRow(UiStrings.LabelDefaultPlaylistFadeOut, playlistFadeOut, isFadeIn: false);

        FadeRowsHost.Children.Add(_waveformFadeInRow.Host);
        FadeRowsHost.Children.Add(_waveformFadeOutRow.Host);
        FadeRowsHost.Children.Add(_playlistFadeInRow.Host);
        FadeRowsHost.Children.Add(_playlistFadeOutRow.Host);

        ExpectedSampleRateTextBox.Text = expectedFormat.SampleRateHz.ToString(CultureInfo.InvariantCulture);
        ExpectedBitDepthTextBox.Text = expectedFormat.BitsPerSample.ToString(CultureInfo.InvariantCulture);
        ExpectedChannelsTextBox.Text = expectedFormat.Channels.ToString(CultureInfo.InvariantCulture);

        TipService.Set(ExpectedFormatHeader, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(ExpectedSampleRateLabel, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(ExpectedBitDepthLabel, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(ExpectedChannelsLabel, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(ExpectedSampleRateTextBox, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(ExpectedBitDepthTextBox, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(ExpectedChannelsTextBox, UiStrings.TipExpectedWaveformFormat);

        ApplyDialogButtonColors(OkButton);
        ApplyDialogButtonColors(CancelButton);

        SelectApi(current.Api);
        ReloadDevices(preserveSelection: true, preferredDeviceId: current.DeviceId);
    }

    private static TextBox? FindAncestorTextBox(DependencyObject origin)
    {
        for (var current = origin; current is not null;)
        {
            if (current is TextBox textBox)
            {
                return textBox;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private FadeCurveRow CreateFadeRow(string labelText, RegionFadeCurveKind curve, bool isFadeIn)
    {
        var rowHeight = DesignMetrics.FlatOptionRowHeight;
        var iconSide = FadeCurveIcons.WidthFor((int)Math.Round(rowHeight));

        var host = new Grid
        {
            Height = rowHeight,
            Margin = new Thickness(0, 0, 0, 2),
        };
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconSide) });

        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = UiColors.Brush(UiColors.PrimaryFore),
        };

        var iconHost = new Border
        {
            Width = iconSide,
            Height = rowHeight,
            Background = UiColors.Brush(UiColors.ForControlBack(UiColors.ProjectBarInputBack)),
            BorderBrush = UiColors.Brush(UiColors.ForControlBack(UiColors.ChromeBorder)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
        };

        var icon = new Image
        {
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconHost.Child = icon;
        iconHost.MouseEnter += (_, _) =>
            iconHost.Background = UiColors.Brush(UiColors.ForControlBack(UiColors.TransportHoverBack));
        iconHost.MouseLeave += (_, _) =>
            iconHost.Background = UiColors.Brush(UiColors.ForControlBack(UiColors.ProjectBarInputBack));

        host.Children.Add(label);
        host.Children.Add(iconHost);
        Grid.SetColumn(iconHost, 1);

        var row = new FadeCurveRow(host, label, icon, iconHost, curve, isFadeIn);
        RefreshFadeRowIcon(row);
        iconHost.MouseLeftButtonUp += (_, _) => ShowFadeCurvePicker(row);
        return row;
    }

    private void ShowFadeCurvePicker(FadeCurveRow row)
    {
        FadeCurveIcons.ShowPicker(
            row.IconHost,
            new Point(0, row.IconHost.ActualHeight),
            row.Curve,
            row.IsFadeIn,
            kind =>
            {
                row.Curve = kind;
                RefreshFadeRowIcon(row);
            },
            ref _fadeCurveMenu);
    }

    private static void RefreshFadeRowIcon(FadeCurveRow row)
    {
        row.Icon.Source = FadeCurveIcons.Create(
            row.Curve,
            row.IsFadeIn,
            selected: false,
            pixelSize: FadeCurveIcons.IconSize,
            leftMargin: 0);
        TipService.Set(row.IconHost, UiStrings.LabelRegionFadeCurve(row.Curve));
        TipService.Set(row.Label, UiStrings.LabelRegionFadeCurve(row.Curve));
    }

    private static void ApplyDialogButtonColors(RoundedButton button)
    {
        var fill = UiColors.ForControlBack(UiColors.ProjectBarInputBack);
        var hover = UiColors.ForControlBack(UiColors.TransportHoverBack);
        var pressed = UiColors.ForControlBack(UiColors.TransportPressedBack);
        var border = UiColors.ForControlBack(UiColors.ChromeBorder);

        button.Background = UiColors.Brush(fill);
        button.Foreground = UiColors.Brush(UiColors.ProjectBarInputFore);
        button.HoverBackColor = hover;
        button.PressedBackColor = pressed;
        button.DisabledBackColor = fill;
        button.DisabledForeColor = UiColors.ActionButtonDisabledFore;
        button.BorderColor = border;
        button.HoverBorderColor = border;
        button.PressedBorderColor = border;
        button.DisabledBorderColor = UiColors.ForControlBack(UiColors.ActionButtonDisabledBorder);
        button.BorderSize = 1;
    }

    private void ApiCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressDeviceReload)
        {
            ReloadDevices(preserveSelection: false);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var api = GetSelectedApi();
        var deviceId = DeviceCombo.SelectedItem is DeviceItem device ? device.Id : string.Empty;
        SelectedSettings = new AudioOutputSettings(api, deviceId);
        SelectedExpectedFormat = ReadExpectedFormatFromFields();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
        }
    }

    private void ExpectedNumber_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !DigitsOnly.IsMatch(e.Text);

    private void ExpectedNumber_LostFocus(object sender, RoutedEventArgs e)
    {
        var format = ReadExpectedFormatFromFields();
        ExpectedSampleRateTextBox.Text = format.SampleRateHz.ToString(CultureInfo.InvariantCulture);
        ExpectedBitDepthTextBox.Text = format.BitsPerSample.ToString(CultureInfo.InvariantCulture);
        ExpectedChannelsTextBox.Text = format.Channels.ToString(CultureInfo.InvariantCulture);
    }

    private ExpectedWaveformFormat ReadExpectedFormatFromFields()
    {
        var rate = TryParsePositiveInt(
            ExpectedSampleRateTextBox.Text,
            (int)SelectedExpectedFormat.SampleRateHz);
        var bits = TryParsePositiveInt(
            ExpectedBitDepthTextBox.Text,
            SelectedExpectedFormat.BitsPerSample);
        var channels = TryParsePositiveInt(
            ExpectedChannelsTextBox.Text,
            SelectedExpectedFormat.Channels);
        return ExpectedWaveformFormat.Normalize(rate, bits, channels);
    }

    private static int TryParsePositiveInt(string? text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;

    private void SelectApi(AudioOutputApi api)
    {
        _suppressDeviceReload = true;
        try
        {
            for (var i = 0; i < ApiCombo.Items.Count; i++)
            {
                if (ApiCombo.Items[i] is ApiItem item && item.Api == api)
                {
                    ApiCombo.SelectedIndex = i;
                    return;
                }
            }

            ApiCombo.SelectedIndex = 0;
        }
        finally
        {
            _suppressDeviceReload = false;
        }
    }

    private AudioOutputApi GetSelectedApi() =>
        ApiCombo.SelectedItem is ApiItem item ? item.Api : AudioOutputApi.WaveOut;

    private void ReloadDevices(bool preserveSelection, string? preferredDeviceId = null)
    {
        var api = GetSelectedApi();
        var devices = AudioOutputFactory.EnumerateDevices(api);
        var keepId = preferredDeviceId;
        if (preserveSelection
            && keepId is null
            && DeviceCombo.SelectedItem is DeviceItem selected)
        {
            keepId = selected.Id;
        }

        DeviceCombo.Items.Clear();
        foreach (var device in devices)
        {
            DeviceCombo.Items.Add(new DeviceItem(device.Id, device.DisplayName));
        }

        if (DeviceCombo.Items.Count == 0)
        {
            return;
        }

        var index = 0;
        if (!string.IsNullOrEmpty(keepId))
        {
            for (var i = 0; i < DeviceCombo.Items.Count; i++)
            {
                if (DeviceCombo.Items[i] is DeviceItem item
                    && string.Equals(item.Id, keepId, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
        }

        DeviceCombo.SelectedIndex = index;
    }

    protected override void OnClosed(EventArgs e)
    {
        _fadeCurveMenu = null;
        base.OnClosed(e);
    }

    private sealed class FadeCurveRow(
        Grid host,
        TextBlock label,
        Image icon,
        Border iconHost,
        RegionFadeCurveKind curve,
        bool isFadeIn)
    {
        public Grid Host { get; } = host;

        public TextBlock Label { get; } = label;

        public Image Icon { get; } = icon;

        public Border IconHost { get; } = iconHost;

        public RegionFadeCurveKind Curve { get; set; } = curve;

        public bool IsFadeIn { get; } = isFadeIn;
    }

    private sealed class ApiItem(AudioOutputApi api, string label)
    {
        public AudioOutputApi Api { get; } = api;

        public override string ToString() => label;
    }

    private sealed class DeviceItem(string id, string displayName)
    {
        public string Id { get; } = id;

        public override string ToString() => displayName;
    }
}
