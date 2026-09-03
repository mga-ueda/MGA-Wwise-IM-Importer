using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.UI;

internal sealed partial class WaapiStatusBar : UserControl
{
    private readonly TransportIconButton _keepLockButton;
    private string _badgeText = "—";
    private Color _badgeBack = Colors.Transparent;
    private Color _badgeFore = Colors.Gray;
    private bool _badgeFilled;
    private bool _selectionMissing;
    private bool _keepTargetChecked;
    private bool _keepLockHovered;
    private bool _keepLockEnabled = true;
    private bool _showKeepLock;
    private bool _projectNameClickable;
    private bool _projectNameHovered;
    private readonly EventHandler _languageChangedHandler;

    public WaapiStatusBar()
    {
        InitializeComponent();
        Height = DesignMetrics.WaapiBarHeight;
        RootGrid.Margin = new Thickness(DesignMetrics.Dip(10), 0, DesignMetrics.Dip(10), 0);

        var keepLockSide = Math.Min(
            DesignMetrics.Dip(24),
            Math.Max(1, DesignMetrics.WaapiBarHeight - DesignMetrics.Dip(2)));
        _keepLockButton = new TransportIconButton(TransportIcon.Unlock)
        {
            Width = keepLockSide,
            Height = keepLockSide,
            Margin = new Thickness(0),
        };
        _keepLockButton.Click += KeepLockButton_Click;
        _keepLockButton.MouseEnter += (_, _) =>
        {
            _keepLockHovered = true;
            ApplyKeepLockColors();
        };
        _keepLockButton.MouseLeave += (_, _) =>
        {
            _keepLockHovered = false;
            ApplyKeepLockColors();
        };
        KeepLockButtonHost.Content = _keepLockButton;

        TitleLabel.Text = UiStrings.WaapiTitle;
        AutoActiveCheckBox.Content = UiStrings.LabelAutoActive;
        AutoActiveCheckBox.IsChecked = true;
        ApplyColors();
        ApplyTips();
        SetPending();

        BadgeCanvas.Loaded += (_, _) => DrawBadge();
        _languageChangedHandler = (_, _) =>
        {
            UpdateKeepLockAppearance();
            TitleLabel.Text = UiStrings.WaapiTitle;
            AutoActiveCheckBox.Content = UiStrings.LabelAutoActive;
            ApplyTips();
            DrawBadge();
        };
        UiStrings.LanguageChanged += _languageChangedHandler;
    }

    public event EventHandler? KeepTargetChanged;
    public event EventHandler? AutoActiveChanged;
    public event EventHandler? ProjectNameClick;

    public bool KeepTargetChecked
    {
        get => _keepTargetChecked;
        set
        {
            if (_keepTargetChecked == value)
            {
                return;
            }

            _keepTargetChecked = value;
            UpdateKeepLockAppearance();
        }
    }

    private bool _suppressAutoActiveEvent;

    /// <summary>プログラムからの設定では AutoActiveChanged を発火しない（WinForms 同等）。</summary>
    public bool AutoActiveChecked
    {
        get => AutoActiveCheckBox.IsChecked == true;
        set
        {
            if (AutoActiveCheckBox.IsChecked == value)
            {
                return;
            }

            _suppressAutoActiveEvent = true;
            try
            {
                AutoActiveCheckBox.IsChecked = value;
            }
            finally
            {
                _suppressAutoActiveEvent = false;
            }
        }
    }

    public bool TryInvokeProjectNameClick()
    {
        if (!_projectNameClickable)
        {
            return false;
        }

        ProjectNameClick?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ApplyColors()
    {
        Background = UiColors.Brush(UiColors.ForControlBack(UiColors.StatusBarBack));
        TitleLabel.Foreground = UiColors.Brush(UiColors.StatusBarTitleFore);
        KeepStateLabel.Foreground = UiColors.Brush(UiColors.StatusBarTitleFore);
        AutoActiveCheckBox.Foreground = UiColors.Brush(UiColors.ActionOptionFore);
        AutoActiveCheckBox.ApplyColors();
        ApplyKeepLockColors();
        ApplyProjectNameColors();

        if (_badgeText == UiStrings.WaapiBadgeConnect)
        {
            SetBadgeConnected();
            ApplyPathForeColor(connected: true);
        }
        else if (_badgeText == UiStrings.WaapiBadgeDisconnect)
        {
            SetBadgeDisconnected();
            ApplyPathForeColor(connected: false);
        }
        else
        {
            SetBadgeNeutral();
            PathLabel.Foreground = UiColors.Brush(UiColors.StatusBarTitleFore);
            VersionLabel.Foreground = UiColors.Brush(UiColors.StatusBarTitleFore);
            SepAfterVersion.Foreground = UiColors.Brush(UiColors.StatusBarTitleFore);
            SepAfterProject.Foreground = UiColors.Brush(UiColors.StatusBarTitleFore);
        }

        DrawBadge();
    }

    public void SetPending()
    {
        _selectionMissing = false;
        _keepLockEnabled = false;
        _showKeepLock = false;
        SetProjectNameClickable(false);
        _badgeText = "…";
        SetBadgeNeutral();
        SetPlainDetail(UiStrings.StatusChecking, UiColors.StatusBarTitleFore);
    }

    public void SetSkipped()
    {
        _selectionMissing = false;
        _keepLockEnabled = false;
        _showKeepLock = false;
        SetProjectNameClickable(false);
        _badgeText = "—";
        SetBadgeNeutral();
        SetPlainDetail(UiStrings.StatusStartupCheckOff, UiColors.StatusBarTitleFore);
    }

    public void SetResult(WaapiProbeResult result)
    {
        _keepLockEnabled = result.Ok;
        if (result.Ok)
        {
            _selectionMissing = !result.HasSelection;
            SetBadgeConnected();
            ApplyPathForeColor(connected: true);
            SetStructuredDetail(
                result.WwiseVersion,
                result.ProjectName,
                result.HasSelection ? result.SelectedPath : UiStrings.StatusNoneSelected,
                projectNameClickable: false);
        }
        else
        {
            _selectionMissing = false;
            _showKeepLock = false;
            SetProjectNameClickable(false);
            SetBadgeDisconnected();
            ApplyPathForeColor(connected: false);
            SetPlainDetail(
                result.Message.Length > 0 ? result.Message : UiStrings.StatusDisconnected,
                UiColors.StatusBarErrorDetailFore);
        }
    }

    public void UpdateSelection(
        string wwiseVersion,
        string projectName,
        string selectedPath,
        bool keepTarget = false,
        bool? projectNameClickable = null)
    {
        _keepLockEnabled = true;
        if (keepTarget != _keepTargetChecked)
        {
            _keepTargetChecked = keepTarget;
            UpdateKeepLockAppearance();
        }

        _selectionMissing = keepTarget
            ? selectedPath.Length == 0
            : string.IsNullOrEmpty(selectedPath);
        SetBadgeConnected();
        ApplyPathForeColor(connected: true);
        SetStructuredDetail(
            wwiseVersion,
            projectName,
            string.IsNullOrEmpty(selectedPath) ? UiStrings.StatusNoneSelected : selectedPath,
            projectNameClickable: projectNameClickable ?? (keepTarget && projectName.Length > 0));
    }

    public void UpdateDisconnectedKeepTarget(
        string projectName,
        string keptPath,
        bool? projectNameClickable = null) =>
        UpdateDisconnectedStatus(
            projectName,
            string.IsNullOrEmpty(keptPath) ? UiStrings.StatusNoneSelected : keptPath,
            keepTargetChecked: true,
            projectNameClickable: projectNameClickable ?? projectName.Length > 0);

    public void UpdateDisconnectedLastProject(
        string projectName,
        string detailText,
        bool projectNameClickable) =>
        UpdateDisconnectedStatus(
            projectName,
            string.IsNullOrEmpty(detailText) ? UiStrings.StatusDisconnected : detailText,
            keepTargetChecked: false,
            projectNameClickable: projectNameClickable && projectName.Length > 0);

    private void UpdateDisconnectedStatus(
        string projectName,
        string detailText,
        bool keepTargetChecked,
        bool projectNameClickable)
    {
        _keepLockEnabled = true;
        if (keepTargetChecked != _keepTargetChecked)
        {
            _keepTargetChecked = keepTargetChecked;
            UpdateKeepLockAppearance();
        }

        _selectionMissing = keepTargetChecked && detailText == UiStrings.StatusNoneSelected;
        _showKeepLock = true;
        SetBadgeDisconnected();
        PathLabel.Foreground = UiColors.Brush(UiColors.StatusBarDetailFore);
        SepAfterVersion.Foreground = UiColors.Brush(UiColors.StatusBarDetailFore);
        SepAfterProject.Foreground = UiColors.Brush(UiColors.StatusBarDetailFore);
        VersionLabel.Foreground = UiColors.Brush(UiColors.StatusBarDetailFore);
        SetStructuredDetail(
            wwiseVersion: string.Empty,
            projectName,
            detailText,
            projectNameClickable: projectNameClickable);
    }

    private void SetPlainDetail(string text, Color foreColor)
    {
        _showKeepLock = false;
        VersionLabel.Visibility = Visibility.Collapsed;
        SepAfterVersion.Visibility = Visibility.Collapsed;
        ProjectNameLabel.Visibility = Visibility.Collapsed;
        SepAfterProject.Visibility = Visibility.Collapsed;
        PathLabel.Text = text;
        PathLabel.Visibility = Visibility.Visible;
        PathLabel.Foreground = UiColors.Brush(foreColor);
        UpdateKeepLockVisibility();
        DrawBadge();
    }

    private void SetStructuredDetail(
        string wwiseVersion,
        string projectName,
        string pathText,
        bool projectNameClickable)
    {
        _showKeepLock = true;
        var hasVersion = !string.IsNullOrWhiteSpace(wwiseVersion);
        VersionLabel.Text = hasVersion ? FormatDisplayVersion(wwiseVersion) : string.Empty;
        VersionLabel.Visibility = hasVersion ? Visibility.Visible : Visibility.Collapsed;

        var hasProject = projectName.Length > 0;
        ProjectNameLabel.Text = projectName;
        ProjectNameLabel.Visibility = hasProject ? Visibility.Visible : Visibility.Collapsed;
        SepAfterVersion.Visibility = hasVersion ? Visibility.Visible : Visibility.Collapsed;
        SepAfterProject.Visibility = hasProject ? Visibility.Visible : Visibility.Collapsed;
        PathLabel.Text = pathText;
        PathLabel.Visibility = Visibility.Visible;
        SetProjectNameClickable(projectNameClickable && hasProject);
        UpdateKeepLockVisibility();
        DrawBadge();
    }

    private void SetProjectNameClickable(bool clickable)
    {
        _projectNameClickable = clickable;
        if (!clickable)
        {
            _projectNameHovered = false;
        }

        ApplyProjectNameColors();
        ApplyTips();
    }

    private void UpdateKeepLockVisibility()
    {
        KeepLockPanel.Visibility = _showKeepLock ? Visibility.Visible : Visibility.Collapsed;
        _keepLockButton.IsEnabled = _keepLockEnabled;
        KeepStateLabel.Text = _keepTargetChecked
            ? UiStrings.KeepTargetOnLabel
            : UiStrings.KeepTargetOffLabel;
    }

    private void UpdateKeepLockAppearance()
    {
        _keepLockButton.SetIcon(_keepTargetChecked ? TransportIcon.Lock : TransportIcon.Unlock);
        KeepStateLabel.Text = _keepTargetChecked
            ? UiStrings.KeepTargetOnLabel
            : UiStrings.KeepTargetOffLabel;
        ApplyKeepLockColors();
        ApplyTips();
    }

    private void ApplyKeepLockColors()
    {
        var barBack = UiColors.ForControlBack(UiColors.StatusBarBack);
        _keepLockButton.Background = UiColors.Brush(barBack);
        _keepLockButton.HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        _keepLockButton.PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        _keepLockButton.AccentColor = Colors.Transparent;

        if (_keepTargetChecked)
        {
            var fore = _keepLockHovered
                ? UiColors.KeepTargetLockHoverFore
                : UiColors.KeepTargetLockFore;
            _keepLockButton.Foreground = UiColors.Brush(fore);
            _keepLockButton.ActiveForeColor = UiColors.KeepTargetLockFore;
        }
        else
        {
            var fore = _keepLockHovered
                ? UiColors.KeepTargetUnlockHoverFore
                : UiColors.KeepTargetUnlockFore;
            _keepLockButton.Foreground = UiColors.Brush(fore);
            _keepLockButton.ActiveForeColor = UiColors.KeepTargetUnlockFore;
        }

        _keepLockButton.InvalidateVisual();
    }

    private void ApplyProjectNameColors()
    {
        if (_projectNameClickable)
        {
            ProjectNameLabel.Foreground = UiColors.Brush(
                _projectNameHovered ? UiColors.ActionLinkHoverFore : UiColors.ActionLinkFore);
            ProjectNameLabel.Cursor = Cursors.Hand;
            return;
        }

        ProjectNameLabel.Foreground = UiColors.Brush(UiColors.StatusBarDetailFore);
        ProjectNameLabel.Cursor = Cursors.Arrow;
    }

    private void ApplyPathForeColor(bool connected)
    {
        var error = !connected || _selectionMissing;
        var fore = error ? UiColors.StatusBarErrorDetailFore : UiColors.StatusBarDetailFore;
        PathLabel.Foreground = UiColors.Brush(fore);
        if (!_projectNameClickable)
        {
            var detailFore = connected ? UiColors.StatusBarDetailFore : UiColors.StatusBarErrorDetailFore;
            VersionLabel.Foreground = UiColors.Brush(detailFore);
            SepAfterVersion.Foreground = UiColors.Brush(detailFore);
            SepAfterProject.Foreground = UiColors.Brush(detailFore);
        }
        else
        {
            VersionLabel.Foreground = UiColors.Brush(
                connected ? UiColors.StatusBarDetailFore : UiColors.StatusBarErrorDetailFore);
            SepAfterVersion.Foreground = VersionLabel.Foreground;
            SepAfterProject.Foreground = UiColors.Brush(
                connected ? UiColors.StatusBarDetailFore : UiColors.StatusBarErrorDetailFore);
        }

        ApplyProjectNameColors();
    }

    private void SetBadgeConnected()
    {
        _badgeText = UiStrings.WaapiBadgeConnect;
        _badgeBack = UiColors.StatusBarConnectedBadgeBack;
        _badgeFore = Colors.White;
        _badgeFilled = true;
    }

    private void SetBadgeDisconnected()
    {
        _badgeText = UiStrings.WaapiBadgeDisconnect;
        _badgeBack = UiColors.StatusBarDisconnectedBadgeBack;
        _badgeFore = Colors.White;
        _badgeFilled = true;
    }

    private void SetBadgeNeutral()
    {
        _badgeBack = UiColors.ForControlBack(UiColors.StatusBarBack);
        _badgeFore = UiColors.StatusBarTitleFore;
        _badgeFilled = false;
    }

    private void DrawBadge()
    {
        BadgeCanvas.Children.Clear();
        if (BadgeCanvas.ActualWidth <= 0 || BadgeCanvas.ActualHeight <= 0)
        {
            return;
        }

        var typeface = new Typeface(AppFonts.AppFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var formatted = new FormattedText(
            _badgeText,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            UiColors.Brush(_badgeFore),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var padX = 8d;
        var padY = 3d;
        var width = formatted.Width + padX * 2;
        var height = formatted.Height + padY * 2;
        BadgeCanvas.Width = Math.Max(40, width);

        if (_badgeFilled)
        {
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = width,
                Height = height,
                Fill = UiColors.Brush(_badgeBack),
            };
            Canvas.SetTop(rect, (BadgeCanvas.ActualHeight - height) / 2 + 1);
            BadgeCanvas.Children.Add(rect);
        }

        var text = new TextBlock
        {
            Text = _badgeText,
            Foreground = UiColors.Brush(_badgeFore),
            FontWeight = FontWeights.Bold,
            FontSize = 12,
        };
        Canvas.SetLeft(text, padX);
        Canvas.SetTop(text, (BadgeCanvas.ActualHeight - formatted.Height) / 2);
        BadgeCanvas.Children.Add(text);
    }

    private void ApplyTips()
    {
        TipService.Set(
            _keepLockButton,
            _keepTargetChecked ? UiStrings.TipKeepTargetLock : UiStrings.TipKeepTargetUnlock);
        TipService.Set(
            ProjectNameLabel,
            _projectNameClickable ? UiStrings.TipWwiseProjectNameOpen : string.Empty);
        TipService.Set(AutoActiveCheckBox, UiStrings.TipAutoActive);
    }

    private static string FormatDisplayVersion(string wwiseVersion)
    {
        var wwise = UiStrings.LabelWwise;
        if (string.IsNullOrWhiteSpace(wwiseVersion))
        {
            return wwise;
        }

        var text = wwiseVersion.Trim();
        if (text.Equals(wwise, StringComparison.OrdinalIgnoreCase)
            || text.Equals("Wwise", StringComparison.OrdinalIgnoreCase))
        {
            return wwise;
        }

        if (text.StartsWith("Wwise v", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("Wwise V", StringComparison.OrdinalIgnoreCase))
        {
            var ver = text["Wwise ".Length..].TrimStart('v', 'V', ' ');
            if (ver.Length == 0
                || ver.Equals(wwise, StringComparison.OrdinalIgnoreCase)
                || ver.Equals("Wwise", StringComparison.OrdinalIgnoreCase))
            {
                return wwise;
            }

            return $"{wwise} v{ver}";
        }

        if (text.StartsWith("Wwise ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = text["Wwise ".Length..].Trim();
            if (rest.StartsWith('v') || rest.StartsWith('V'))
            {
                rest = rest[1..].TrimStart();
            }

            if (rest.Length == 0
                || rest.Equals(wwise, StringComparison.OrdinalIgnoreCase)
                || rest.Equals("Wwise", StringComparison.OrdinalIgnoreCase))
            {
                return wwise;
            }

            return $"{wwise} v{rest}";
        }

        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            var ver = text[1..].TrimStart();
            return ver.Length > 0 ? $"{wwise} v{ver}" : wwise;
        }

        return $"{wwise} v{text}";
    }

    private void AutoActiveCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressAutoActiveEvent)
        {
            return;
        }

        AutoActiveChanged?.Invoke(this, EventArgs.Empty);
    }

    private void KeepLockButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_keepLockEnabled)
        {
            return;
        }

        KeepTargetChecked = !KeepTargetChecked;
        KeepTargetChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ProjectNameLabel_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_projectNameClickable)
        {
            return;
        }

        ProjectNameClick?.Invoke(this, EventArgs.Empty);
    }

    private void ProjectNameLabel_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_projectNameClickable)
        {
            return;
        }

        _projectNameHovered = true;
        ApplyProjectNameColors();
    }

    private void ProjectNameLabel_MouseLeave(object sender, MouseEventArgs e)
    {
        _projectNameHovered = false;
        ApplyProjectNameColors();
    }
}
