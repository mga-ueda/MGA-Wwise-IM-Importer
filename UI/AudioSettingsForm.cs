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
#if DEBUG
    private readonly SectionHeaderLabel _uiScaleHeader;
    private readonly FlatOptionRadioButton _optScaleDisplay;
    private readonly FlatOptionRadioButton _optScale100;
    private readonly FlatOptionRadioButton _optScale150;
#endif

    public AudioOutputSettings SelectedSettings { get; private set; }

    public RegionFadeCurveKind WaveformFadeInCurve => _waveformFadeInRow.Curve;

    public RegionFadeCurveKind WaveformFadeOutCurve => _waveformFadeOutRow.Curve;

    public RegionFadeCurveKind PlaylistFadeInCurve => _playlistFadeInRow.Curve;

    public RegionFadeCurveKind PlaylistFadeOutCurve => _playlistFadeOutRow.Curve;

    public ExpectedWaveformFormat SelectedExpectedFormat { get; private set; }

#if DEBUG
    /// <summary>0 = ディスプレイどおり。96/144 = その DPI 相当のレイアウト寸法。</summary>
    public int SelectedUiScaleSimulateDpi { get; private set; }
#endif

    public AudioSettingsForm(
        AudioOutputSettings current,
        RegionFadeCurveKind waveformFadeIn,
        RegionFadeCurveKind waveformFadeOut,
        RegionFadeCurveKind playlistFadeIn,
        RegionFadeCurveKind playlistFadeOut,
        ExpectedWaveformFormat expectedFormat
#if DEBUG
        ,
        int uiScaleSimulateDpi = 0
#endif
        )
    {
        SelectedSettings = current;
        SelectedExpectedFormat = expectedFormat;
#if DEBUG
        SelectedUiScaleSimulateDpi = NormalizeUiScaleSimulateDpi(uiScaleSimulateDpi);
#endif

        Text = UiStrings.DialogSettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        // 寸法は 150% 設計＋ DesignMetrics。AutoScale は使わない（二重スケール防止）。
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Yu Gothic UI", 9F);
        BackColor = UiColors.ForControlBack(UiColors.WindowBack);
        ForeColor = UiColors.PrimaryFore;
        // 行高・行間・見出しはメイン（More Options／FlatOption）と同じ 150% 設計値。
        // フォントは pt のまま（OS DPI に追従）。
        var left = D(18);
        // 旧 600 はラベルと右端コントロールの間が空きすぎる。長文ラジオが収まる程度に抑える。
        var fieldWidth = D(450);
        var comboHeight = D(FlatOptionGlyph.RowHeightDesign); // 30 — プロジェクトバー行と同系
        var labelToField = D(3);
        var sectionGap = D(12); // More Options 帯間 S(8)@96 = 12@144
        var rowHeight = D(FlatOptionGlyph.RowHeightDesign); // 30
        var rowPitch = D(32); // MarkerOptionsPanel.RowPitchDesign
        var rowGap = Math.Max(0, rowPitch - rowHeight);
        var labelLine = Math.Max(D(21), Font.Height);
        var headerContentGap = D(3);
        var pad = D(18);
        Padding = new Padding(pad);

        _apiLabel = new Label
        {
            AutoSize = true,
            Location = new Point(left, pad),
            Font = Font,
            Text = UiStrings.LabelAudioApi,
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
        };

        _apiCombo = new DarkDropDownComboBox
        {
            Location = new Point(left, _apiLabel.Location.Y + labelLine + labelToField),
            Width = fieldWidth,
            Height = comboHeight,
            Font = Font,
            ItemHeight = Math.Max(1, comboHeight - D(6)),
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

        _deviceLabel.Font = Font;
        _deviceCombo = new DarkDropDownComboBox
        {
            Location = new Point(left, deviceLabelY + labelLine + labelToField),
            Width = fieldWidth,
            Height = comboHeight,
            Font = Font,
            ItemHeight = Math.Max(1, comboHeight - D(6)),
        };
        _deviceCombo.ApplyColors();

        var fadeHeaderY = _deviceCombo.Location.Y + comboHeight + sectionGap;
        // More Options の Stream／Marker Grid と同じ見出し寸法（150% 設計 39）。
        var fadeHeaderHeight = D(39);
        var headerBarMargin = D(3);
        // 見出し帯だけ左右マージンを設計 10 ずつ狭める（帯をコンテンツより左右に伸ばす）。
        var headerSideExtend = D(10);
        var headerLeft = Math.Max(0, left - headerSideExtend);
        var headerWidth = fieldWidth + headerSideExtend * 2;
        _fadeDefaultsHeader = new SectionHeaderLabel
        {
            Location = new Point(headerLeft, fadeHeaderY),
            Size = new Size(headerWidth, fadeHeaderHeight),
            Text = UiStrings.LabelFadeCurveDefaults,
            Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold),
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
            BarColor = UiColors.ForControlBack(UiColors.SectionHeaderBack),
            BarMarginTop = headerBarMargin,
            BarMarginBottom = headerBarMargin,
            Padding = new Padding(D(15), 0, D(6), 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var rowY = fadeHeaderY + fadeHeaderHeight + headerContentGap;
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
            Location = new Point(headerLeft, expectedHeaderY),
            Size = new Size(headerWidth, fadeHeaderHeight),
            Text = UiStrings.LabelExpectedWaveformFormat,
            Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold),
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
            BarColor = UiColors.ForControlBack(UiColors.SectionHeaderBack),
            BarMarginTop = headerBarMargin,
            BarMarginBottom = headerBarMargin,
            Padding = new Padding(D(15), 0, D(6), 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        TipService.Set(_expectedFormatHeader, UiStrings.TipExpectedWaveformFormat);

        var expectedBoxWidth = D(120);
        var expectedLabelGap = D(12);
        var expectedLabelWidth = fieldWidth - expectedBoxWidth - expectedLabelGap;
        var expectedRowY = expectedHeaderY + fadeHeaderHeight + headerContentGap;

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

        var contentBottom = expectedRowY + rowHeight;

#if DEBUG
        var scaleHeaderY = contentBottom + sectionGap;
        var scaleHeaderHeight = fadeHeaderHeight;
        _uiScaleHeader = new SectionHeaderLabel
        {
            Location = new Point(headerLeft, scaleHeaderY),
            Size = new Size(headerWidth, scaleHeaderHeight),
            Text = "表示スケールのプレビュー",
            Font = new Font("Yu Gothic UI", 8.5F, FontStyle.Bold),
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
            BarColor = UiColors.ForControlBack(UiColors.SectionHeaderBack),
            BarMarginTop = headerBarMargin,
            BarMarginBottom = headerBarMargin,
            Padding = new Padding(D(15), 0, D(6), 0),
            TextAlign = ContentAlignment.MiddleLeft,
        };

        // メインの FlatOption ラジオと同じ行高・ピッチ。
        var scaleRowY = scaleHeaderY + scaleHeaderHeight + headerContentGap;
        var scaleRowH = rowHeight;
        var scaleRowPitch = rowPitch;
        _optScaleDisplay = CreateScaleRadio(
            left,
            scaleRowY,
            fieldWidth,
            scaleRowH,
            "ディスプレイ設定に合わせる（DPI 連動）");
        scaleRowY += scaleRowPitch;
        _optScale100 = CreateScaleRadio(
            left,
            scaleRowY,
            fieldWidth,
            scaleRowH,
            "100% 相当の余白感をシミュレート（Ctrl+1）");
        scaleRowY += scaleRowPitch;
        _optScale150 = CreateScaleRadio(
            left,
            scaleRowY,
            fieldWidth,
            scaleRowH,
            "150% 相当（設計どおり）（Ctrl+2）");
        ApplyUiScaleSimulateRadios(SelectedUiScaleSimulateDpi);
        TipService.Set(_optScaleDisplay, "ディスプレイ設定の DPI に合わせてレイアウト寸法を換算する。");
        TipService.Set(_optScale100, "100%（96 DPI）相当の余白・行高をシミュレートする（Ctrl+1）。");
        TipService.Set(_optScale150, "150%（144 DPI）設計どおりの寸法をシミュレートする（Ctrl+2）。");
        contentBottom = scaleRowY + scaleRowH;
#endif

        // CLEAR / EXPORT（Designer 32@96 → 150% 設計 48）と同系。
        var buttonWidth = D(162);
        var buttonHeight = D(48);
        var buttonGap = D(12);
        var buttonY = contentBottom + sectionGap;
        var cancelX = left + fieldWidth - buttonWidth;
        var okX = cancelX - buttonGap - buttonWidth;

        _okButton = CreateDialogButton(UiStrings.ButtonAudioSettingsOk, new Point(okX, buttonY), buttonWidth, buttonHeight);
        _okButton.DialogResult = DialogResult.OK;
        _okButton.Click += OkButton_Click;

        _cancelButton = CreateDialogButton(UiStrings.ButtonAudioSettingsCancel, new Point(cancelX, buttonY), buttonWidth, buttonHeight);
        _cancelButton.DialogResult = DialogResult.Cancel;

        ClientSize = new Size(left * 2 + fieldWidth, buttonY + buttonHeight + pad);

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
#if DEBUG
        Controls.Add(_uiScaleHeader);
        Controls.Add(_optScaleDisplay);
        Controls.Add(_optScale100);
        Controls.Add(_optScale150);
#endif
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

        // メインのフェードカーブアイコン帯に合わせる（アイコンは横長）。
        var iconSide = FadeCurveIcons.WidthFor(height);
        var label = new Label
        {
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(Math.Max(1, width - iconSide - D(9)), height),
            Text = labelText,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
        };

        var icon = new PictureBox
        {
            Cursor = Cursors.Hand,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Size = new Size(iconSide, height),
            Location = new Point(width - iconSide, 0),
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
        // More Options の EditorHeight（設計 28）と同系。行高を超えない。
        var boxH = Math.Min(rowHeight, Math.Max(D(28), Font.Height + D(6)));
        var textBox = new TextBox
        {
            BorderStyle = BorderStyle.FixedSingle,
            Font = Font,
            Size = new Size(width, boxH),
            TextAlign = HorizontalAlignment.Center,
            MaxLength = maxLength,
            BackColor = UiColors.ForControlBack(UiColors.ProjectBarInputBack),
            ForeColor = UiColors.ProjectBarInputFore,
        };
        TextBoxVerticalAlign.Configure(textBox);
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
            pixelSize: DesignMetrics.Px(FadeCurveIcons.IconSize, row.Icon),
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
            CornerRadius = D(6),
            Font = Font,
            Padding = new Padding(D(12), D(3), D(12), D(3)),
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

        // ハンドル生成・初期 Text 反映後に縦中央を確定する。
        TextBoxVerticalAlign.Apply(_expectedSampleRateTextBox);
        TextBoxVerticalAlign.Apply(_expectedBitDepthTextBox);
        TextBoxVerticalAlign.Apply(_expectedChannelsTextBox);
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

    /// <summary>150% 設計 px を現在の LayoutDpi へ換算する。</summary>
    private int D(int design150) => DesignMetrics.Px(design150, this);

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
#if DEBUG
        SelectedUiScaleSimulateDpi = GetUiScaleSimulateTargetDpi();
#endif
    }

#if DEBUG
    private FlatOptionRadioButton CreateScaleRadio(int x, int y, int width, int height, string text)
    {
        var radio = new FlatOptionRadioButton
        {
            AutoSize = false,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Margin = Padding.Empty,
            Text = text,
            ForeColor = UiColors.PrimaryFore,
            BackColor = BackColor,
            Font = Font,
        };
        // ApplyFixedLayout は行高 30 設計に戻すため使わず、ダイアログ行高を維持する。
        radio.ApplyColors();
        return radio;
    }

    private static int NormalizeUiScaleSimulateDpi(int dpi) =>
        dpi is 96 or 144 ? dpi : 0;

    private int GetUiScaleSimulateTargetDpi()
    {
        if (_optScale100.Checked)
        {
            return 96;
        }

        if (_optScale150.Checked)
        {
            return 144;
        }

        return 0;
    }

    private void ApplyUiScaleSimulateRadios(int dpi)
    {
        _optScaleDisplay.Checked = false;
        _optScale100.Checked = false;
        _optScale150.Checked = false;
        if (dpi == 96)
        {
            _optScale100.Checked = true;
        }
        else if (dpi == 144)
        {
            _optScale150.Checked = true;
        }
        else
        {
            _optScaleDisplay.Checked = true;
        }
    }
#endif

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
