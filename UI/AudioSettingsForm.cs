using System.Globalization;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 再生出力・フェードカーブ既定・波形フォーマット規定の設定ダイアログ。
/// </summary>
internal sealed class AudioSettingsForm : Form
{
    private readonly DarkDropDownComboBox _apiCombo;
    private readonly DarkDropDownComboBox _deviceCombo;
    private readonly Label _apiLabel;
    private readonly Label _deviceLabel;
    private readonly SectionHeaderLabel _fadeDefaultsHeader;
    private readonly FadeCurveRow _waveformFadeInRow;
    private readonly FadeCurveRow _waveformFadeOutRow;
    private readonly FadeCurveRow _playlistFadeInRow;
    private readonly FadeCurveRow _playlistFadeOutRow;
    private readonly SectionHeaderLabel _expectedFormatHeader;
    private readonly Label _expectedSampleRateLabel;
    private readonly Label _expectedBitDepthLabel;
    private readonly Label _expectedChannelsLabel;
    private readonly TextBox _expectedSampleRateTextBox;
    private readonly TextBox _expectedBitDepthTextBox;
    private readonly TextBox _expectedChannelsTextBox;
    private readonly RoundedButton _okButton;
    private readonly RoundedButton _cancelButton;
    private ContextMenuStrip? _fadeCurveMenu;
    private bool _suppressDeviceReload;

    public AudioOutputSettings SelectedSettings { get; private set; }

    public RegionFadeCurveKind WaveformFadeInCurve => _waveformFadeInRow.Curve;

    public RegionFadeCurveKind WaveformFadeOutCurve => _waveformFadeOutRow.Curve;

    public RegionFadeCurveKind PlaylistFadeInCurve => _playlistFadeInRow.Curve;

    public RegionFadeCurveKind PlaylistFadeOutCurve => _playlistFadeOutRow.Curve;

    public ExpectedWaveformFormat SelectedExpectedFormat { get; private set; }

    public AudioSettingsForm(
        AudioOutputSettings current,
        RegionFadeCurveKind waveformFadeIn,
        RegionFadeCurveKind waveformFadeOut,
        RegionFadeCurveKind playlistFadeIn,
        RegionFadeCurveKind playlistFadeOut,
        ExpectedWaveformFormat expectedFormat)
    {
        SelectedSettings = current;
        SelectedExpectedFormat = expectedFormat;

        Text = UiStrings.DialogSettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = UiColors.ForControlBack(UiColors.WindowBack);
        ForeColor = UiColors.PrimaryFore;
        Padding = new Padding(20);

        const int left = 20;
        const int fieldWidth = 400;
        const int comboHeight = 30;
        const int labelToField = 8;
        const int sectionGap = 22;
        const int rowHeight = 28;
        const int rowGap = 6;

        _apiLabel = new Label
        {
            AutoSize = true,
            Location = new Point(left, 20),
            Text = UiStrings.LabelAudioApi,
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
        };

        _apiCombo = new DarkDropDownComboBox
        {
            Location = new Point(left, _apiLabel.Location.Y + 18 + labelToField),
            Width = fieldWidth,
            Height = comboHeight,
            Font = Font,
        };
        _apiCombo.ApplyColors();
        _apiCombo.Items.Add(new ApiItem(AudioOutputApi.WaveOut, UiStrings.LabelAudioApiWaveOut));
        _apiCombo.Items.Add(new ApiItem(AudioOutputApi.Wasapi, UiStrings.LabelAudioApiWasapi));
        _apiCombo.Items.Add(new ApiItem(AudioOutputApi.Asio, UiStrings.LabelAudioApiAsio));
        _apiCombo.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressDeviceReload)
            {
                ReloadDevices(preserveSelection: false);
            }
        };

        var deviceLabelY = _apiCombo.Location.Y + comboHeight + sectionGap;
        _deviceLabel = new Label
        {
            AutoSize = true,
            Location = new Point(left, deviceLabelY),
            Text = UiStrings.LabelAudioDevice,
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
        };

        _deviceCombo = new DarkDropDownComboBox
        {
            Location = new Point(left, deviceLabelY + 18 + labelToField),
            Width = fieldWidth,
            Height = comboHeight,
            Font = Font,
        };
        _deviceCombo.ApplyColors();

        var fadeHeaderY = _deviceCombo.Location.Y + comboHeight + sectionGap;
        // More Options の Stream／Marker Grid と同じ見出し寸法（高さ 26・帯マージン既定）。
        var fadeHeaderHeight = S(26);
        _fadeDefaultsHeader = new SectionHeaderLabel
        {
            Location = new Point(left, fadeHeaderY),
            Size = new Size(fieldWidth, fadeHeaderHeight),
            Text = UiStrings.LabelFadeCurveDefaults,
            Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold),
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
            BarColor = UiColors.ForControlBack(UiColors.SectionHeaderBack),
            BarMarginTop = 3,
            BarMarginBottom = 3,
            Padding = new Padding(S(10), 0, S(4), 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var rowY = fadeHeaderY + fadeHeaderHeight + S(4);
        _waveformFadeInRow = CreateFadeRow(
            left,
            rowY,
            fieldWidth,
            rowHeight,
            UiStrings.LabelDefaultWaveformFadeIn,
            waveformFadeIn,
            isFadeIn: true);
        rowY += rowHeight + rowGap;
        _waveformFadeOutRow = CreateFadeRow(
            left,
            rowY,
            fieldWidth,
            rowHeight,
            UiStrings.LabelDefaultWaveformFadeOut,
            waveformFadeOut,
            isFadeIn: false);
        rowY += rowHeight + rowGap;
        _playlistFadeInRow = CreateFadeRow(
            left,
            rowY,
            fieldWidth,
            rowHeight,
            UiStrings.LabelDefaultPlaylistFadeIn,
            playlistFadeIn,
            isFadeIn: true);
        rowY += rowHeight + rowGap;
        _playlistFadeOutRow = CreateFadeRow(
            left,
            rowY,
            fieldWidth,
            rowHeight,
            UiStrings.LabelDefaultPlaylistFadeOut,
            playlistFadeOut,
            isFadeIn: false);

        var expectedHeaderY = rowY + rowHeight + sectionGap;
        _expectedFormatHeader = new SectionHeaderLabel
        {
            Location = new Point(left, expectedHeaderY),
            Size = new Size(fieldWidth, fadeHeaderHeight),
            Text = UiStrings.LabelExpectedWaveformFormat,
            Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold),
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
            BarColor = UiColors.ForControlBack(UiColors.SectionHeaderBack),
            BarMarginTop = 3,
            BarMarginBottom = 3,
            Padding = new Padding(S(10), 0, S(4), 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        TipService.Set(_expectedFormatHeader, UiStrings.TipExpectedWaveformFormat);

        const int expectedBoxWidth = 108;
        const int expectedLabelGap = 10;
        var expectedLabelWidth = fieldWidth - expectedBoxWidth - expectedLabelGap;
        var expectedRowY = expectedHeaderY + fadeHeaderHeight + S(4);

        _expectedSampleRateLabel = CreateExpectedFieldLabel(
            left,
            expectedRowY,
            expectedLabelWidth,
            rowHeight,
            UiStrings.LabelExpectedSampleRateHz);
        _expectedSampleRateTextBox = CreateExpectedNumberTextBox(
            left + expectedLabelWidth + expectedLabelGap,
            expectedRowY,
            expectedBoxWidth,
            rowHeight,
            maxLength: 6);
        expectedRowY += rowHeight + rowGap;

        _expectedBitDepthLabel = CreateExpectedFieldLabel(
            left,
            expectedRowY,
            expectedLabelWidth,
            rowHeight,
            UiStrings.LabelExpectedBitDepth);
        _expectedBitDepthTextBox = CreateExpectedNumberTextBox(
            left + expectedLabelWidth + expectedLabelGap,
            expectedRowY,
            expectedBoxWidth,
            rowHeight,
            maxLength: 2);
        expectedRowY += rowHeight + rowGap;

        _expectedChannelsLabel = CreateExpectedFieldLabel(
            left,
            expectedRowY,
            expectedLabelWidth,
            rowHeight,
            UiStrings.LabelExpectedChannels);
        _expectedChannelsTextBox = CreateExpectedNumberTextBox(
            left + expectedLabelWidth + expectedLabelGap,
            expectedRowY,
            expectedBoxWidth,
            rowHeight,
            maxLength: 2);

        _expectedSampleRateTextBox.Text = expectedFormat.SampleRateHz.ToString(CultureInfo.InvariantCulture);
        _expectedBitDepthTextBox.Text = expectedFormat.BitsPerSample.ToString(CultureInfo.InvariantCulture);
        _expectedChannelsTextBox.Text = expectedFormat.Channels.ToString(CultureInfo.InvariantCulture);
        TipService.Set(_expectedSampleRateLabel, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(_expectedBitDepthLabel, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(_expectedChannelsLabel, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(_expectedSampleRateTextBox, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(_expectedBitDepthTextBox, UiStrings.TipExpectedWaveformFormat);
        TipService.Set(_expectedChannelsTextBox, UiStrings.TipExpectedWaveformFormat);

        const int buttonWidth = 108;
        const int buttonHeight = 34;
        const int buttonGap = 12;
        var buttonY = expectedRowY + rowHeight + sectionGap;
        var cancelX = left + fieldWidth - buttonWidth;
        var okX = cancelX - buttonGap - buttonWidth;

        _okButton = CreateDialogButton(UiStrings.ButtonAudioSettingsOk, new Point(okX, buttonY), buttonWidth, buttonHeight);
        _okButton.DialogResult = DialogResult.OK;
        _okButton.Click += OkButton_Click;

        _cancelButton = CreateDialogButton(UiStrings.ButtonAudioSettingsCancel, new Point(cancelX, buttonY), buttonWidth, buttonHeight);
        _cancelButton.DialogResult = DialogResult.Cancel;

        ClientSize = new Size(left * 2 + fieldWidth, buttonY + buttonHeight + 20);

        Controls.Add(_apiLabel);
        Controls.Add(_apiCombo);
        Controls.Add(_deviceLabel);
        Controls.Add(_deviceCombo);
        Controls.Add(_fadeDefaultsHeader);
        Controls.Add(_waveformFadeInRow.Host);
        Controls.Add(_waveformFadeOutRow.Host);
        Controls.Add(_playlistFadeInRow.Host);
        Controls.Add(_playlistFadeOutRow.Host);
        Controls.Add(_expectedFormatHeader);
        Controls.Add(_expectedSampleRateLabel);
        Controls.Add(_expectedBitDepthLabel);
        Controls.Add(_expectedChannelsLabel);
        Controls.Add(_expectedSampleRateTextBox);
        Controls.Add(_expectedBitDepthTextBox);
        Controls.Add(_expectedChannelsTextBox);
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        DarkWindowChrome.ApplyImmersiveDarkTitleBar(this);

        SelectApi(current.Api);
        ReloadDevices(preserveSelection: true, preferredDeviceId: current.DeviceId);
    }

    private FadeCurveRow CreateFadeRow(
        int left,
        int top,
        int width,
        int height,
        string labelText,
        RegionFadeCurveKind curve,
        bool isFadeIn)
    {
        var host = new Panel
        {
            Location = new Point(left, top),
            Size = new Size(width, height),
            BackColor = BackColor,
        };

        var label = new Label
        {
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(width - 36, height),
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
        };

        var icon = new PictureBox
        {
            Cursor = Cursors.Hand,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Size = new Size(28, height),
            Location = new Point(width - 28, 0),
            BackColor = UiColors.ForControlBack(UiColors.ProjectBarInputBack),
            TabStop = false,
        };
        icon.Paint += (_, e) =>
        {
            using var pen = new Pen(UiColors.ForControlBack(UiColors.ChromeBorder));
            e.Graphics.DrawRectangle(pen, 0, 0, icon.Width - 1, icon.Height - 1);
        };

        var row = new FadeCurveRow(host, label, icon, curve, isFadeIn);
        RefreshFadeRowIcon(row);
        WireFadeCurveIconHover(icon);
        icon.Click += (_, _) => ShowFadeCurvePicker(row);
        host.Controls.Add(label);
        host.Controls.Add(icon);
        return row;
    }

    private Label CreateExpectedFieldLabel(int x, int y, int width, int height, string text) => new()
    {
        AutoSize = false,
        Location = new Point(x, y),
        Size = new Size(width, height),
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = UiColors.PrimaryFore,
        BackColor = BackColor,
    };

    private TextBox CreateExpectedNumberTextBox(int x, int y, int width, int rowHeight, int maxLength)
    {
        var textBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = Font,
            Size = new Size(width, 26),
            TextAlign = HorizontalAlignment.Center,
            MaxLength = maxLength,
            BackColor = UiColors.ForControlBack(UiColors.ProjectBarInputBack),
            ForeColor = UiColors.ProjectBarInputFore,
        };
        textBox.Location = new Point(x, y + Math.Max(0, (rowHeight - textBox.Height) / 2));
        textBox.KeyPress += ExpectedNumberTextBox_KeyPress;
        textBox.Leave += ExpectedNumberTextBox_Leave;
        return textBox;
    }

    private static void ExpectedNumberTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar))
        {
            return;
        }

        e.Handled = true;
    }

    private void ExpectedNumberTextBox_Leave(object? sender, EventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var format = ReadExpectedFormatFromFields();
        _expectedSampleRateTextBox.Text = format.SampleRateHz.ToString(CultureInfo.InvariantCulture);
        _expectedBitDepthTextBox.Text = format.BitsPerSample.ToString(CultureInfo.InvariantCulture);
        _expectedChannelsTextBox.Text = format.Channels.ToString(CultureInfo.InvariantCulture);
    }

    private ExpectedWaveformFormat ReadExpectedFormatFromFields()
    {
        var rate = TryParsePositiveInt(
            _expectedSampleRateTextBox.Text,
            (int)SelectedExpectedFormat.SampleRateHz);
        var bits = TryParsePositiveInt(
            _expectedBitDepthTextBox.Text,
            SelectedExpectedFormat.BitsPerSample);
        var channels = TryParsePositiveInt(
            _expectedChannelsTextBox.Text,
            SelectedExpectedFormat.Channels);
        return ExpectedWaveformFormat.Normalize(rate, bits, channels);
    }

    private static int TryParsePositiveInt(string? text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;

    private static void WireFadeCurveIconHover(PictureBox icon) =>
        ControlHoverChrome.WireBackColor(
            icon,
            () => UiColors.ForControlBack(UiColors.ProjectBarInputBack));

    private void ShowFadeCurvePicker(FadeCurveRow row)
    {
        FadeCurveIcons.ShowPicker(
            row.Icon,
            new Point(0, row.Icon.Height),
            row.Curve,
            row.IsFadeIn,
            kind =>
            {
                row.Curve = kind;
                RefreshFadeRowIcon(row);
            },
            ref _fadeCurveMenu);
    }

    private void RefreshFadeRowIcon(FadeCurveRow row)
    {
        var old = row.Icon.Image;
        row.Icon.Image = FadeCurveIcons.Create(
            row.Curve,
            row.IsFadeIn,
            selected: false,
            pixelSize: FadeCurveIcons.IconSize,
            leftMargin: 0);
        old?.Dispose();
        TipService.Set(row.Icon, UiStrings.LabelRegionFadeCurve(row.Curve));
        TipService.Set(row.Label, UiStrings.LabelRegionFadeCurve(row.Curve));
    }

    private RoundedButton CreateDialogButton(string text, Point location, int width, int height)
    {
        var button = new RoundedButton
        {
            Text = text,
            Size = new Size(width, height),
            Location = location,
            CornerRadius = 6,
            Font = Font,
            Padding = new Padding(12, 4, 12, 4),
            TabStop = true,
        };
        ApplyDialogButtonColors(button);
        return button;
    }

    private static void ApplyDialogButtonColors(RoundedButton button)
    {
        var fill = UiColors.ForControlBack(UiColors.ProjectBarInputBack);
        var hover = UiColors.ForControlBack(UiColors.TransportHoverBack);
        var pressed = UiColors.ForControlBack(UiColors.TransportPressedBack);
        var border = UiColors.ForControlBack(UiColors.ChromeBorder);

        button.BackColor = fill;
        button.ForeColor = UiColors.ProjectBarInputFore;
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Owner is { TopMost: true })
        {
            TopMost = true;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _fadeCurveMenu?.Dispose();
        _fadeCurveMenu = null;
        foreach (var row in new[]
                 {
                     _waveformFadeInRow,
                     _waveformFadeOutRow,
                     _playlistFadeInRow,
                     _playlistFadeOutRow,
                 })
        {
            var image = row.Icon.Image;
            row.Icon.Image = null;
            image?.Dispose();
        }

        base.OnFormClosed(e);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        return base.ProcessDialogKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private int S(int value) => (int)Math.Round(value * DeviceDpi / 96f);

    private void OkButton_Click(object? sender, EventArgs e)
    {
        var api = GetSelectedApi();
        var deviceId = string.Empty;
        if (_deviceCombo.SelectedItem is DeviceItem device)
        {
            deviceId = device.Id;
        }

        SelectedSettings = new AudioOutputSettings(api, deviceId);
        SelectedExpectedFormat = ReadExpectedFormatFromFields();
    }

    private void SelectApi(AudioOutputApi api)
    {
        _suppressDeviceReload = true;
        try
        {
            for (var i = 0; i < _apiCombo.Items.Count; i++)
            {
                if (_apiCombo.Items[i] is ApiItem item && item.Api == api)
                {
                    _apiCombo.SelectedIndex = i;
                    return;
                }
            }

            _apiCombo.SelectedIndex = 0;
        }
        finally
        {
            _suppressDeviceReload = false;
        }
    }

    private AudioOutputApi GetSelectedApi() =>
        _apiCombo.SelectedItem is ApiItem item ? item.Api : AudioOutputApi.WaveOut;

    private void ReloadDevices(bool preserveSelection, string? preferredDeviceId = null)
    {
        var api = GetSelectedApi();
        var devices = AudioOutputFactory.EnumerateDevices(api);
        var keepId = preferredDeviceId;
        if (preserveSelection
            && keepId is null
            && _deviceCombo.SelectedItem is DeviceItem selected)
        {
            keepId = selected.Id;
        }

        _deviceCombo.BeginUpdate();
        try
        {
            _deviceCombo.Items.Clear();
            foreach (var device in devices)
            {
                _deviceCombo.Items.Add(new DeviceItem(device.Id, device.DisplayName));
            }

            if (_deviceCombo.Items.Count == 0)
            {
                return;
            }

            var index = 0;
            if (!string.IsNullOrEmpty(keepId))
            {
                for (var i = 0; i < _deviceCombo.Items.Count; i++)
                {
                    if (_deviceCombo.Items[i] is DeviceItem item
                        && string.Equals(item.Id, keepId, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }

            _deviceCombo.SelectedIndex = index;
        }
        finally
        {
            _deviceCombo.EndUpdate();
        }
    }

    private sealed class FadeCurveRow(
        Panel host,
        Label label,
        PictureBox icon,
        RegionFadeCurveKind curve,
        bool isFadeIn)
    {
        public Panel Host { get; } = host;

        public Label Label { get; } = label;

        public PictureBox Icon { get; } = icon;

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
