using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

internal sealed partial class MarkerOptionsPanel : UserControl
{
    private const int StreamMsMin = 0;
    private const int StreamMsMax = 9999;
    private const int StreamMsDefault = 500;
    private static readonly Regex DigitsOnlyRegex = new("[0-9]");

    private MarkerSettings? _settings;
    private bool _updating;
    private bool _markerPlacementOptionsEnabled = true;
    private bool _layerMusicOptionEnabled;
    private bool _streamEnabled = true;
    private int _lookAheadMs = StreamMsDefault;
    private int _prefetchLengthMs = StreamMsDefault;
    private bool _loudnessPreserveGroupBalance;
    private bool _moreOptionsExpanded = true;
    private readonly EventHandler _languageChangedHandler;

    public MarkerOptionsPanel()
    {
        InitializeComponent();
        WireTextEditingFocus(LookAheadTextBox);
        WireTextEditingFocus(PrefetchTextBox);
        WireTextEditingFocus(DigitsTextBox);
        WireTextEditingFocus(PrefixTextBox);
        WireTextEditingFocus(SuffixTextBox);
        WireTextEditingFocus(JoinerTextBox);

        LookAheadTextBox.LostFocus += StreamMsTextBox_LostFocus;
        PrefetchTextBox.LostFocus += StreamMsTextBox_LostFocus;

        ApplyLocalizedLabels();
        ApplyColors();
        ApplyMoreOptionsVisibility();

        _languageChangedHandler = (_, _) => ApplyLocalizedLabels();
        UiStrings.LanguageChanged += _languageChangedHandler;
        SizeChanged += (_, _) => UpdateRequiredWidth();
        UpdateRequiredWidth();
    }

    public event EventHandler? SettingsChanged;
    public event EventHandler<bool>? TextEditingChanged;
    public event EventHandler? RequiredHeightChanged;

    public int RequiredWidth { get; private set; }
    public int RequiredHeight
    {
        get
        {
            var header = DesignMetrics.SectionHeaderHeight;
            return _moreOptionsExpanded
                ? (int)Math.Ceiling(header + BodyGrid.DesiredSize.Height)
                : (int)Math.Ceiling(header);
        }
    }

    public bool StreamEnabled => _streamEnabled;
    public int LookAheadMs => _lookAheadMs;
    public int PrefetchLengthMs => _prefetchLengthMs;
    public bool LoudnessPreserveGroupBalance => _loudnessPreserveGroupBalance;
    public bool MoreOptionsExpanded => _moreOptionsExpanded;

    public bool HasEditableTextBoxFocus => HasFocusedTextBox();

    public bool IsPointerOverEditableTextBox()
    {
        foreach (var textBox in EnumerateEditableTextBoxes())
        {
            if (!textBox.IsVisible)
            {
                continue;
            }

            var pt = Mouse.GetPosition(textBox);
            if (pt.X >= 0 && pt.Y >= 0 && pt.X < textBox.ActualWidth && pt.Y < textBox.ActualHeight)
            {
                return true;
            }
        }

        return false;
    }

    public void SetMarkerPlacementOptionsEnabled(bool enabled)
    {
        if (_markerPlacementOptionsEnabled == enabled)
        {
            return;
        }

        _markerPlacementOptionsEnabled = enabled;
        UpdateDependentStates();
    }

    public void SetLayerMusicOptionEnabled(bool enabled)
    {
        if (_layerMusicOptionEnabled == enabled)
        {
            return;
        }

        _layerMusicOptionEnabled = enabled;
        UpdateDependentStates();
    }

    public void Bind(MarkerSettings settings)
    {
        _settings = settings;
        _updating = true;
        try
        {
            var gridRadio = settings.GridOverride switch
            {
                MarkerGridOverrideMode.Bar => GridBarRadio,
                MarkerGridOverrideMode.Beat => GridBeatRadio,
                _ => GridDefaultRadio,
            };
            gridRadio.IsChecked = true;
            DigitsTextBox.Text = settings.CommentDigits <= 0
                ? string.Empty
                : Math.Clamp(
                    settings.CommentDigits,
                    MarkerSettings.CommentDigitsMin,
                    MarkerSettings.CommentDigitsMax).ToString();
            ZeroPadCheckBox.IsChecked = settings.CommentZeroPad;
            ResetPerPartCheckBox.IsChecked = settings.CommentResetPerPart;
            PrefixTextBox.Text = settings.CommentPrefix;
            SuffixTextBox.Text = settings.CommentSuffix;
            JoinerTextBox.Text = settings.CommentJoiner;
        }
        finally
        {
            _updating = false;
        }

        UpdateDependentStates();
        UpdatePreview();
    }

    public void BindStreaming(bool streamEnabled, int lookAheadMs, int prefetchLengthMs)
    {
        _updating = true;
        try
        {
            _streamEnabled = streamEnabled;
            StreamEnabledCheckBox.IsChecked = streamEnabled;
            _lookAheadMs = Math.Clamp(lookAheadMs, StreamMsMin, StreamMsMax);
            _prefetchLengthMs = Math.Clamp(prefetchLengthMs, StreamMsMin, StreamMsMax);
            LookAheadTextBox.Text = _lookAheadMs.ToString();
            PrefetchTextBox.Text = _prefetchLengthMs.ToString();
        }
        finally
        {
            _updating = false;
        }

        UpdateDependentStates();
    }

    public void BindLoudness(bool preserveGroupBalance)
    {
        _updating = true;
        try
        {
            _loudnessPreserveGroupBalance = preserveGroupBalance;
            LoudnessGroupBalanceCheckBox.IsChecked = preserveGroupBalance;
        }
        finally
        {
            _updating = false;
        }

        UpdateDependentStates();
    }

    public void BindMoreOptions(bool expanded)
    {
        if (_moreOptionsExpanded == expanded)
        {
            return;
        }

        _moreOptionsExpanded = expanded;
        ApplyMoreOptionsVisibility();
        RequiredHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.PlaylistBack));
        var optionFore = UiColors.Brush(UiColors.PlaylistOptionFore);
        PrefetchLabel.Foreground = optionFore;
        PrefetchUnitLabel.Foreground = optionFore;
        LookAheadLabel.Foreground = optionFore;
        LookAheadUnitLabel.Foreground = optionFore;
        DigitsLabel.Foreground = optionFore;
        PrefixLabel.Foreground = optionFore;
        SuffixLabel.Foreground = optionFore;
        JoinerLabel.Foreground = optionFore;
        PreviewLabel.Foreground = optionFore;
        ApplyHeaderColors(StreamHeader, UiColors.PlaylistDefaultFore);
        ApplyHeaderColors(LoudnessHeader, UiColors.PlaylistDefaultFore);
        ApplyHeaderColors(GridHeader, UiColors.PlaylistDefaultFore);
        ApplyHeaderColors(CommentHeader, UiColors.PlaylistDefaultFore);
        ApplyHeaderColors(MoreOptionsHeader, UiColors.PlaylistDefaultFore);

        ApplyFlatOptionColors(StreamEnabledCheckBox, optionFore);
        ApplyFlatOptionColors(LoudnessGroupBalanceCheckBox, optionFore);
        ApplyFlatOptionColors(ZeroPadCheckBox, optionFore);
        ApplyFlatOptionColors(ResetPerPartCheckBox, optionFore);
        ApplyFlatOptionColors(GridBarRadio, optionFore);
        ApplyFlatOptionColors(GridBeatRadio, optionFore);
        ApplyFlatOptionColors(GridDefaultRadio, optionFore);

        UpdateDependentStates();
        UpdatePreview();
    }

    private static void ApplyFlatOptionColors(Control control, System.Windows.Media.Brush fore)
    {
        control.Foreground = fore;
        control.Background = Brushes.Transparent;
        switch (control)
        {
            case FlatOptionCheckBox check:
                check.ApplyColors();
                break;
            case FlatOptionRadioButton radio:
                radio.ApplyColors();
                break;
        }
    }

    private static void ApplyHeaderColors(SectionHeaderLabel header, Color fore)
    {
        header.Foreground = UiColors.Brush(fore);
        header.BarColor = UiColors.SectionHeaderBack;
    }

    public void ApplyLocalizedLabels()
    {
        StreamHeader.Text = UiStrings.LabelStream;
        StreamEnabledCheckBox.Content = UiStrings.LabelStream;
        PrefetchLabel.Text = UiStrings.LabelPrefetchLength;
        PrefetchUnitLabel.Text = UiStrings.LabelMsUnit;
        LookAheadLabel.Text = UiStrings.LabelLookAheadTime;
        LookAheadUnitLabel.Text = UiStrings.LabelMsUnit;
        LoudnessHeader.Text = UiStrings.LabelLayerMusicOption;
        LoudnessGroupBalanceCheckBox.Content = UiStrings.LabelKeepLayerBalance;
        GridHeader.Text = UiStrings.LabelMarkerGridHeader;
        GridBarRadio.Content = UiStrings.LabelBar;
        GridBeatRadio.Content = UiStrings.LabelBeat;
        GridDefaultRadio.Content = UiStrings.LabelTimeline;
        CommentHeader.Text = UiStrings.LabelMarkerComment;
        DigitsLabel.Text = UiStrings.LabelDigits;
        ZeroPadCheckBox.Content = UiStrings.LabelZeroPad;
        ResetPerPartCheckBox.Content = UiStrings.LabelResetPerPart;
        PrefixLabel.Text = UiStrings.LabelPrefix;
        SuffixLabel.Text = UiStrings.LabelSuffix;
        JoinerLabel.Text = UiStrings.LabelSeparator;
        MoreOptionsHeader.Text = FormatMoreOptionsHeader(_moreOptionsExpanded);
        ApplyTips();
        UpdatePreview();
    }

    private void MoreOptionsHeader_Click(object sender, MouseButtonEventArgs e)
    {
        _moreOptionsExpanded = !_moreOptionsExpanded;
        ApplyMoreOptionsVisibility();
        RequiredHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyMoreOptionsVisibility()
    {
        MoreOptionsHeader.Text = FormatMoreOptionsHeader(_moreOptionsExpanded);
        BodyGrid.Visibility = _moreOptionsExpanded ? Visibility.Visible : Visibility.Collapsed;
        UpdateRequiredWidth();
    }

    private void UpdateRequiredWidth()
    {
        RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        RequiredWidth = Math.Max(460, (int)Math.Ceiling(RootGrid.DesiredSize.Width));
    }

    private static string FormatMoreOptionsHeader(bool expanded) => UiStrings.LabelMoreOptions(expanded);

    private void MarkerUiChanged(object sender, RoutedEventArgs e) => OnUiChanged();

    private void GridRadioChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true })
        {
            OnUiChanged();
        }
    }

    private void StreamUiChanged(object sender, RoutedEventArgs e) => OnStreamUiChanged();

    private void LoudnessUiChanged(object sender, RoutedEventArgs e) => OnLoudnessUiChanged();

    private void OnUiChanged()
    {
        if (_updating || _settings is null)
        {
            return;
        }

        _settings.GridOverride = GridBarRadio.IsChecked == true
            ? MarkerGridOverrideMode.Bar
            : GridBeatRadio.IsChecked == true
                ? MarkerGridOverrideMode.Beat
                : MarkerGridOverrideMode.Default;
        if (TryGetDigits(out var digits))
        {
            _settings.CommentDigits = digits;
        }

        _settings.CommentZeroPad = ZeroPadCheckBox.IsChecked == true;
        _settings.CommentResetPerPart = ResetPerPartCheckBox.IsChecked == true;
        _settings.CommentPrefix = PrefixTextBox.Text;
        _settings.CommentSuffix = SuffixTextBox.Text;
        _settings.CommentJoiner = JoinerTextBox.Text;
        _settings.SyncCommentOptionalEnabledFlags();

        UpdateDependentStates();
        UpdatePreview();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateDependentStates()
    {
        var placementEnabled = _markerPlacementOptionsEnabled;
        GridDefaultRadio.IsEnabled = placementEnabled;
        GridBarRadio.IsEnabled = placementEnabled;
        GridBeatRadio.IsEnabled = placementEnabled;
        ZeroPadCheckBox.IsEnabled = placementEnabled;
        ResetPerPartCheckBox.IsEnabled = placementEnabled;

        DigitsTextBox.IsReadOnly = !placementEnabled;
        LookAheadTextBox.IsReadOnly = !_streamEnabled;
        PrefetchTextBox.IsReadOnly = !_streamEnabled;
        LoudnessGroupBalanceCheckBox.IsEnabled = _layerMusicOptionEnabled;
        PrefixTextBox.IsReadOnly = !placementEnabled;
        SuffixTextBox.IsReadOnly = !placementEnabled;
        JoinerTextBox.IsReadOnly = !placementEnabled;
        ApplyDependentColors();
    }

    private void ApplyDependentColors()
    {
        var headerFore = UiColors.PlaylistDefaultFore;
        var optionFore = UiColors.PlaylistOptionFore;
        var disabledFore = UiColors.OptionGlyphDisabled;
        var inputBack = UiColors.ForControlBack(UiColors.DialogInputBack);
        var placementEnabled = _markerPlacementOptionsEnabled;
        var layerMusicEnabled = _layerMusicOptionEnabled;

        ApplyHeaderColors(LoudnessHeader, layerMusicEnabled ? headerFore : disabledFore);
        ApplyHeaderColors(GridHeader, placementEnabled ? headerFore : disabledFore);
        ApplyHeaderColors(CommentHeader, placementEnabled ? headerFore : disabledFore);
        PreviewLabel.Foreground = UiColors.Brush(placementEnabled ? optionFore : disabledFore);

        ApplyInputAppearance(LookAheadTextBox, _streamEnabled, optionFore, disabledFore, inputBack);
        ApplyInputAppearance(PrefetchTextBox, _streamEnabled, optionFore, disabledFore, inputBack);
        ApplyInputAppearance(DigitsTextBox, placementEnabled, optionFore, disabledFore, inputBack);
        ApplyInputAppearance(PrefixTextBox, placementEnabled, optionFore, disabledFore, inputBack);
        ApplyInputAppearance(SuffixTextBox, placementEnabled, optionFore, disabledFore, inputBack);
        ApplyInputAppearance(JoinerTextBox, placementEnabled, optionFore, disabledFore, inputBack);

        var optionBrush = UiColors.Brush(optionFore);
        ApplyFlatOptionColors(StreamEnabledCheckBox, optionBrush);
        ApplyFlatOptionColors(LoudnessGroupBalanceCheckBox, optionBrush);
        ApplyFlatOptionColors(ZeroPadCheckBox, optionBrush);
        ApplyFlatOptionColors(ResetPerPartCheckBox, optionBrush);
        ApplyFlatOptionColors(GridBarRadio, optionBrush);
        ApplyFlatOptionColors(GridBeatRadio, optionBrush);
        ApplyFlatOptionColors(GridDefaultRadio, optionBrush);
    }

    private static void ApplyInputAppearance(
        TextBox textBox,
        bool enabled,
        Color optionFore,
        Color disabledFore,
        Color inputBack)
    {
        textBox.Background = UiColors.Brush(inputBack);
        textBox.Foreground = UiColors.Brush(enabled ? optionFore : disabledFore);
        textBox.Cursor = enabled ? Cursors.IBeam : Cursors.Arrow;
    }

    private void StreamMsPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !DigitsOnlyRegex.IsMatch(e.Text);

    private void DigitsPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Length > 0 && (e.Text[0] < '0' || e.Text[0] > '6');

    private void StreamMsTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        if (sender == LookAheadTextBox)
        {
            LookAheadTextBox.Text = _lookAheadMs.ToString();
        }
        else if (sender == PrefetchTextBox)
        {
            PrefetchTextBox.Text = _prefetchLengthMs.ToString();
        }
    }

    private void OnStreamUiChanged()
    {
        if (_updating)
        {
            return;
        }

        var streamEnabled = StreamEnabledCheckBox.IsChecked == true;
        var lookAheadOk = TryParseStreamMs(LookAheadTextBox.Text, out var lookAhead);
        var prefetchOk = TryParseStreamMs(PrefetchTextBox.Text, out var prefetch);
        if (!lookAheadOk || !prefetchOk)
        {
            if (streamEnabled == _streamEnabled)
            {
                return;
            }

            _streamEnabled = streamEnabled;
            UpdateDependentStates();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (streamEnabled == _streamEnabled
            && lookAhead == _lookAheadMs
            && prefetch == _prefetchLengthMs)
        {
            return;
        }

        _streamEnabled = streamEnabled;
        _lookAheadMs = lookAhead;
        _prefetchLengthMs = prefetch;
        UpdateDependentStates();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoudnessUiChanged()
    {
        if (_updating || !_layerMusicOptionEnabled)
        {
            return;
        }

        var groupBalance = LoudnessGroupBalanceCheckBox.IsChecked == true;
        if (groupBalance == _loudnessPreserveGroupBalance)
        {
            return;
        }

        _loudnessPreserveGroupBalance = groupBalance;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryParseStreamMs(string text, out int milliseconds)
    {
        if (int.TryParse(
                text.Trim(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out milliseconds)
            && milliseconds >= StreamMsMin
            && milliseconds <= StreamMsMax)
        {
            return true;
        }

        milliseconds = 0;
        return false;
    }

    private bool TryGetDigits(out int digits)
    {
        if (string.IsNullOrWhiteSpace(DigitsTextBox.Text))
        {
            digits = 0;
            return true;
        }

        return int.TryParse(
                DigitsTextBox.Text,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out digits)
            && digits >= MarkerSettings.CommentDigitsMin
            && digits <= MarkerSettings.CommentDigitsMax;
    }

    private void ApplyTips()
    {
        TipService.Set(StreamHeader, UiStrings.TipStreamHeader);
        TipService.Set(StreamEnabledCheckBox, UiStrings.TipStreamEnabled);
        TipService.Set(LookAheadLabel, UiStrings.TipLookAheadLabel);
        TipService.Set(LookAheadTextBox, UiStrings.TipLookAheadBox);
        TipService.Set(LookAheadUnitLabel, UiStrings.TipLookAheadUnit);
        TipService.Set(PrefetchLabel, UiStrings.TipPrefetchLabel);
        TipService.Set(PrefetchTextBox, UiStrings.TipPrefetchBox);
        TipService.Set(PrefetchUnitLabel, UiStrings.TipPrefetchUnit);
        TipService.Set(LoudnessHeader, UiStrings.TipLoudnessHeader);
        TipService.Set(LoudnessGroupBalanceCheckBox, UiStrings.TipLoudnessGroupBalance);
        TipService.Set(MoreOptionsHeader, UiStrings.TipMoreOptionsHeader);
        TipService.Set(GridHeader, UiStrings.TipMarkerGridHeader);
        TipService.Set(GridDefaultRadio, UiStrings.TipMarkerGridTimeline);
        TipService.Set(GridBarRadio, UiStrings.TipMarkerGridBar);
        TipService.Set(GridBeatRadio, UiStrings.TipMarkerGridBeat);
        TipService.Set(CommentHeader, UiStrings.TipMarkerCommentHeader);
        TipService.Set(DigitsLabel, UiStrings.TipCommentDigits);
        TipService.Set(DigitsTextBox, UiStrings.TipCommentDigitsBox);
        TipService.Set(ZeroPadCheckBox, UiStrings.TipCommentZeroPad);
        TipService.Set(ResetPerPartCheckBox, UiStrings.TipCommentResetPerPart);
        TipService.Set(PrefixLabel, UiStrings.TipCommentPrefix);
        TipService.Set(PrefixTextBox, UiStrings.TipCommentPrefixBox);
        TipService.Set(SuffixLabel, UiStrings.TipCommentSuffix);
        TipService.Set(SuffixTextBox, UiStrings.TipCommentSuffixBox);
        TipService.Set(JoinerLabel, UiStrings.TipCommentSeparator);
        TipService.Set(JoinerTextBox, UiStrings.TipCommentSeparatorBox);
        TipService.Set(PreviewLabel, UiStrings.TipCommentPreview);
    }

    private void UpdatePreview()
    {
        if (_settings is null)
        {
            PreviewLabel.Text = string.Empty;
            return;
        }

        var rule = _settings.ToCommentRule();
        var example = rule.Format(1);
        var validationError = ValidateWwiseCustomCueName(_settings, example);
        if (validationError is null)
        {
            PreviewLabel.Text = UiStrings.LabelPreviewExample(example);
            PreviewLabel.Foreground = UiColors.Brush(UiColors.PlaylistDefaultFore);
        }
        else
        {
            PreviewLabel.Text = validationError;
            PreviewLabel.Foreground = UiColors.Brush(UiColors.MarkerCommentErrorFore);
        }
    }

    private static string? ValidateWwiseCustomCueName(MarkerSettings settings, string name)
    {
        if (settings.CommentDigits <= 0
            && string.IsNullOrWhiteSpace(settings.CommentPrefix))
        {
            return UiStrings.MarkerCommentNeedPrefix;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return UiStrings.MarkerCommentEmptyName;
        }

        if (name.Any(char.IsControl))
        {
            return UiStrings.MarkerCommentControlChars;
        }

        return null;
    }

    private void WireTextEditingFocus(TextBox textBox)
    {
        textBox.GotFocus += (_, _) => TextEditingChanged?.Invoke(this, true);
        textBox.LostFocus += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!HasFocusedTextBox())
                {
                    TextEditingChanged?.Invoke(this, false);
                }
            });
        };
    }

    private bool HasFocusedTextBox()
    {
        foreach (var textBox in EnumerateEditableTextBoxes())
        {
            if (textBox.IsFocused)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<TextBox> EnumerateEditableTextBoxes()
    {
        yield return LookAheadTextBox;
        yield return PrefetchTextBox;
        yield return DigitsTextBox;
        yield return PrefixTextBox;
        yield return SuffixTextBox;
        yield return JoinerTextBox;
    }
}
