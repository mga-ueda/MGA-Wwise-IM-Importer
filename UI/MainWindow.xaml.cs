using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.UI;

public partial class MainWindow : Window
{
    // --- Code-created controls (no default ctor / ctor args in XAML) ---
    private TransportIconButton projectFolderButton = null!;
    private TransportIconButton projectDeleteButton = null!;
    private TransportIconButton logClearButton = null!;
    private TransportIconButton logCopyButton = null!;
    private TransportIconButton logDownloadButton = null!;

    // --- App-level state (ported from Form1) ---
    private DeveloperSettings _developerSettings = DeveloperSettings.Load();
    private WaapiSettings _waapiSettings = WaapiSettings.Load();
    private WwiseImportSettings _importSettings = WwiseImportSettings.Load();
    private ProjectSettingsStore _projectStore = ProjectSettingsStore.Load();
    private AppSettings _appSettings = AppSettings.Load();

    private string _loadedProjectName = ProjectSettingsStore.DefaultName;
    private bool _creatingNewProject;
    private bool _suppressProjectUiEvents;
    private string _projectOutputDirectory = string.Empty;
    private string _lastWavePath = string.Empty;
    private IReadOnlyList<string> _lastWavePaths = [];

    private readonly MarkerSettings _markerSettings = new();

    private LogColorSection _logColorSection;
    private UiInteractionLock _uiInteractionLocks;

    private bool _closing;

    public MainWindow()
    {
        InitializeComponent();
        WindowIconHelper.Apply(this);
        InitializeCodeCreatedControls();
        InitializeLocalizedText();
        InitializeEventWiring();
        InitializeWaveformEventWiring();
        InitializePlaybackEventWiring();
        InitializeWaapiEventWiring();
        ApplyAllowDropHandlers();
        ApplyUiColors();
        AlignCompactFileNumbersCheckBox();

        TipService.BindDisplay(tipsLabel, tipsPanel);

        UiStrings.LanguageChanged += (_, _) => Dispatcher.BeginInvoke(RefreshLocalizedText);

        SourceInitialized += (_, _) => DarkWindowChrome.ApplyImmersiveDarkTitleBar(this);
        Loaded += OnLoaded;
        Closing += OnClosing;
        Deactivated += (_, _) => ClearPlaylistGroupPaintStickyId();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
        PreviewMouseWheel += MainWindow_PreviewMouseWheel;
        PreviewMouseDown += MainWindow_PreviewMouseDown;
        PreviewMouseMove += MainWindow_PreviewMouseMoveForPlaylistPaint;
        PreviewMouseLeftButtonUp += MainWindow_PreviewMouseLeftButtonUpForPlaylistPaint;
        SizeChanged += (_, _) =>
        {
            SyncRightSideContentHostHeight();
            UpdatePlaylistSelectorWidth();
            UpdateMinimumWindowSize();
            RefreshFadeCurveIcons();
            PositionLogButtons();
            SyncBusyGlassOverlayBounds();
        };
        logAreaPanel.SizeChanged += (_, _) => UpdatePlaylistSelectorWidth();
        fadeInHeaderPanel.SizeChanged += (_, _) => RefreshFadeCurveIcons();
        fadeOutHeaderPanel.SizeChanged += (_, _) => RefreshFadeCurveIcons();
        logEditorPanel.SizeChanged += (_, _) => PositionLogButtons();
        editorTextBox.SizeChanged += (_, _) => PositionLogButtons();
    }

    /// <summary>WinForms PositionLogButtons 相当。縦スクロールバー幅を空けて右下へ。</summary>
    private void PositionLogButtons()
    {
        var scrollbarWidth = SystemParameters.VerticalScrollBarWidth;
        logButtonPanel.Margin = new Thickness(0, 0, scrollbarWidth, 0);
        Panel.SetZIndex(logButtonPanel, 1);
    }

    private void InitializeCodeCreatedControls()
    {
        projectFolderButton = CreateProjectBarIconButton(TransportIcon.Folder);
        projectDeleteButton = CreateProjectBarIconButton(TransportIcon.Delete);
        projectActionPanel.Children.Insert(0, projectDeleteButton);
        projectActionPanel.Children.Insert(0, projectFolderButton);

        logDownloadButton = CreateLogIconButton(TransportIcon.Download);
        logCopyButton = CreateLogIconButton(TransportIcon.Copy);
        logClearButton = CreateLogIconButton(TransportIcon.Clear);
        logButtonHost.Children.Add(logDownloadButton);
        logButtonHost.Children.Add(logCopyButton);
        logButtonHost.Children.Add(logClearButton);

        try
        {
            using var logoStream = AppEmbeddedResources.OpenLogo();
            if (logoStream is not null)
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = logoStream;
                bitmap.EndInit();
                bitmap.Freeze();
                brandLogoPictureBox.Source = bitmap;
            }
        }
        catch
        {
            // ブランドロゴが読めなくても起動は継続する。
        }
    }

    private static TransportIconButton CreateProjectBarIconButton(TransportIcon icon) =>
        new(icon)
        {
            Width = DesignMetrics.ToolbarButtonSide,
            Height = DesignMetrics.ToolbarButtonSide,
            Margin = icon == TransportIcon.Delete ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 4, 0),
        };

    private static TransportIconButton CreateLogIconButton(TransportIcon icon) =>
        new(icon)
        {
            Width = DesignMetrics.ToolbarButtonSide,
            Height = DesignMetrics.ToolbarButtonSide,
            Margin = new Thickness(2, 0, 0, 0),
            Background = UiColors.Brush(UiColors.LogBack),
        };

    private void InitializeLocalizedText()
    {
        Title = AppVersion.FormTitle;

        keepLastSessionCheckBox.Content = UiStrings.LabelKeepLastSession;
        topMostCheckBox.Content = UiStrings.LabelAlwaysOnTop;
        detailedLogCheckBox.Content = UiStrings.LabelDebugLog;
        compactFileNumbersCheckBox.Content = UiStrings.LabelCompactFileNumbers;

        clearButton.Content = UiStrings.LabelClear;
        reloadButton.Content = UiStrings.LabelReload;
        exportButton.Content = UiStrings.LabelExport;

        tipsHeaderLabel.Text = UiStrings.LabelTips;
        logHeaderLabel.Text = UiStrings.LabelLog;
        playlistHeaderLabel.Text = UiStrings.LabelMusicPlaylist;

        fadeInHeaderLabel.Text = UiStrings.LabelFadeIn;
        transitionTimeHeaderLabel.Text = UiStrings.LabelFadeOut;
        optionsHeaderLabel.Text = UiStrings.LabelOptions;
        fadeInGroupDividerLabel.Text = UiStrings.LabelGroup;
        changeOccursAtHeaderLabel.Text = UiStrings.LabelChangeOccursAt;
        exitSourceAtHeaderLabel.Text = UiStrings.LabelExitSourceAt;

        SetFadeRadioLabels(fadeInNoneRadio, 0);
        SetFadeRadioLabels(fadeInHalfSecondRadio, 0.5);
        SetFadeRadioLabels(fadeInOneSecondRadio, 1);
        SetFadeRadioLabels(fadeInThreeSecondsRadio, 3);
        SetFadeRadioLabels(fadeInSixSecondsRadio, 6);

        SetFadeRadioLabels(transitionTimeNoneRadio, 0);
        SetFadeRadioLabels(transitionTimeHalfSecondRadio, 0.5);
        SetFadeRadioLabels(transitionTimeOneSecondRadio, 1);
        SetFadeRadioLabels(transitionTimeThreeSecondsRadio, 3);
        SetFadeRadioLabels(transitionTimeSixSecondsRadio, 6);

        SetFadeRadioLabels(fadeInGroupNoneRadio, 0);
        SetFadeRadioLabels(fadeInGroupOneSecondRadio, 1);
        SetFadeRadioLabels(fadeInGroupThreeSecondsRadio, 3);
        SetFadeRadioLabels(fadeInGroupSixSecondsRadio, 6);
        SetFadeRadioLabels(fadeInGroupNineSecondsRadio, 9);

        playMinusECheckBox.Content = UiStrings.LabelPlayMinusE;
        additiveLayersCheckBox.Content = UiStrings.LabelAdditiveLayers;

        SetExitSourceRadioLabel(changeOccursImmediateRadio, PlaylistExitSourceMode.Immediate);
        SetExitSourceRadioLabel(changeOccursNextBarRadio, PlaylistExitSourceMode.NextBar);
        SetExitSourceRadioLabel(changeOccursNextBeatRadio, PlaylistExitSourceMode.NextBeat);
        SetExitSourceRadioLabel(changeOccursNextCueRadio, PlaylistExitSourceMode.NextCue);
        SetExitSourceRadioLabel(changeOccursExitCueRadio, PlaylistExitSourceMode.ExitCue);

        SetExitSourceRadioLabel(exitSourceImmediateRadio, PlaylistExitSourceMode.Immediate);
        SetExitSourceRadioLabel(exitSourceNextBarRadio, PlaylistExitSourceMode.NextBar);
        SetExitSourceRadioLabel(exitSourceNextBeatRadio, PlaylistExitSourceMode.NextBeat);
        SetExitSourceRadioLabel(exitSourceNextCueRadio, PlaylistExitSourceMode.NextCue);
        SetExitSourceRadioLabel(exitSourceExitCueRadio, PlaylistExitSourceMode.ExitCue);

        copyrightLinkLabel.LinkText = UiStrings.CopyrightText;

        editorTextBox.FontFamily = AppFonts.LogTypeface.FontFamily;
        editorTextBox.FontSize = AppFonts.DipFromPoints(7); // Form1: CreateLogFont(7F)

        RefreshFadeCurveIcons();
        ApplyActionBarTips();
        ApplyProjectBarTips();
        ApplyTransitionTips();
        ApplyLogAreaTips();
        ApplyPlaylistItemTips();
        transportBar.ApplyLocalizedTips();
        waveformView.RefreshLocalizedTips();
        markerOptionsPanel.ApplyLocalizedLabels();
        waapiStatusBar.ApplyColors();
        RefreshPlaylistLocalizedText();
    }

    /// <summary>言語切替時に固定ラベル・タイトル等を再適用する。</summary>
    private void RefreshLocalizedText()
    {
        InitializeLocalizedText();
        RefreshProjectComboItems(_loadedProjectName);
        RefreshWaapiStatusDisplay();
    }

    private static void SetFadeRadioLabels(FlatOptionRadioButton radio, double seconds) =>
        radio.Content = UiStrings.LabelFadeSeconds(seconds);

    private static void SetExitSourceRadioLabel(FlatOptionRadioButton radio, PlaylistExitSourceMode mode) =>
        radio.Content = UiStrings.LabelExitSource(mode);

    private void ApplyActionBarTips()
    {
        TipService.Set(detailedLogCheckBox, UiStrings.TipDebugLog);
        TipService.Set(languageFlagButton, UiStrings.IsJapanese
            ? UiStrings.TipLanguageJapanese
            : UiStrings.TipLanguageEnglish);
        TipService.Set(settingsGearButton, UiStrings.TipAudioSettings);
        TipService.Set(manualHelpButton, UiStrings.TipManualHelp);
        TipService.Set(compactFileNumbersCheckBox, UiStrings.TipCompactFileNumbers);
        TipService.Set(keepLastSessionCheckBox, UiStrings.TipKeepLastSession);
        TipService.Set(topMostCheckBox, UiStrings.TipAlwaysOnTop);
        TipService.Set(clearButton, UiStrings.TipClear);
        TipService.Set(reloadButton, UiStrings.TipReload);
        TipService.Set(exportButton, UiStrings.TipExport);
        TipService.Set(copyrightLinkLabel, UiStrings.TipCopyright);
        TipService.Set(brandLogoPictureBox, UiStrings.TipBrandLogo);
    }

    private void ApplyProjectBarTips()
    {
        TipService.Set(projectNameComboBox, UiStrings.TipProjectName);
        TipService.Set(projectOutputPathTextBox, UiStrings.TipProjectOutputPath);
        TipService.Set(projectFolderButton, UiStrings.TipProjectFolder);
        TipService.Set(projectDeleteButton, UiStrings.TipProjectDelete);
        TipService.Set(projectSpectrumView, UiStrings.TipSpectrum);
    }

    private void ApplyLogAreaTips()
    {
        TipService.Set(editorTextBox, UiStrings.TipLogEditor);
        TipService.Set(logClearButton, UiStrings.TipLogClear);
        TipService.Set(logCopyButton, UiStrings.TipLogCopy);
        TipService.Set(logDownloadButton, UiStrings.TipLogDownload);
        TipService.Set(playlistHeaderLabel, UiStrings.TipPlaylistHeader);
    }

    private void ApplyTransitionTips()
    {
        TipService.Set(fadeInHeaderLabel, UiStrings.TipFadeInHeader);
        TipService.Set(transitionTimeHeaderLabel, UiStrings.TipFadeOutHeader);
        TipService.Set(exitSourceAtHeaderLabel, UiStrings.TipExitSourceHeader);
        TipService.Set(fadeInGroupDividerLabel, UiStrings.TipGroupFadeHeader);
        TipService.Set(optionsHeaderLabel, UiStrings.TipOptionsHeader);
        TipService.Set(playMinusECheckBox, UiStrings.TipPlayMinusE);
        TipService.Set(additiveLayersCheckBox, UiStrings.TipAdditiveLayers);
        TipService.Set(changeOccursAtHeaderLabel, UiStrings.TipChangeOccursAtHeader);

        var (fadeIn, fadeOut) = (_appSettings.DefaultWaveformFadeInCurve, _appSettings.DefaultWaveformFadeOutCurve);
        TipService.Set(fadeInCurveIcon, UiStrings.LabelRegionFadeCurve(fadeIn));
        TipService.Set(fadeOutCurveIcon, UiStrings.LabelRegionFadeCurve(fadeOut));

        foreach (var radio in FadeInRadios.Concat(TransitionTimeRadios).Concat(FadeInGroupRadios))
        {
            ApplyFadeRadioTip(radio);
        }

        TipService.Set(exitSourceImmediateRadio, UiStrings.TipExitImmediate);
        TipService.Set(exitSourceNextBarRadio, UiStrings.TipExitNextBar);
        TipService.Set(exitSourceNextBeatRadio, UiStrings.TipExitNextBeat);
        TipService.Set(exitSourceNextCueRadio, UiStrings.TipExitNextCue);
        TipService.Set(exitSourceExitCueRadio, UiStrings.TipExitExitCue);
        TipService.Set(changeOccursImmediateRadio, UiStrings.TipExitImmediate);
        TipService.Set(changeOccursNextBarRadio, UiStrings.TipExitNextBar);
        TipService.Set(changeOccursNextBeatRadio, UiStrings.TipExitNextBeat);
        TipService.Set(changeOccursNextCueRadio, UiStrings.TipExitNextCue);
        TipService.Set(changeOccursExitCueRadio, UiStrings.TipExitExitCue);
    }

    private static void ApplyFadeRadioTip(FlatOptionRadioButton radio)
    {
        var seconds = radio.Tag switch
        {
            double value => value,
            string text when double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => 0d,
        };
        var tip = seconds <= 0
            ? UiStrings.TipFadeNone
            : UiStrings.TipFadeSeconds(seconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
        TipService.Set(radio, tip);
    }

    private void InitializeEventWiring()
    {
        // Project bar
        projectFolderButton.Click += ProjectFolderButton_Click;
        projectDeleteButton.Click += ProjectDeleteButton_Click;
        keepLastSessionCheckBox.Checked += KeepLastSessionCheckBox_CheckedChanged;
        keepLastSessionCheckBox.Unchecked += KeepLastSessionCheckBox_CheckedChanged;
        topMostCheckBox.Checked += TopMostCheckBox_CheckedChanged;
        topMostCheckBox.Unchecked += TopMostCheckBox_CheckedChanged;
        languageFlagButton.Click += LanguageFlagButton_Click;
        tipsToggleButton.Click += TipsToggleButton_Click;
        manualHelpButton.Click += (_, _) => ManualViewer.Open(this);
        settingsGearButton.Click += SettingsGearButton_Click;

        projectNameComboBox.SelectionChanged += ProjectNameComboBox_SelectionChanged;
        projectNameComboBox.LostFocus += ProjectNameComboBox_LostFocus;
        projectNameComboBox.PreviewKeyDown += ProjectNameComboBox_PreviewKeyDown;
        projectOutputPathTextBox.GotFocus += ProjectOutputPathTextBox_GotFocus;

        // Transport
        transportBar.CommandInvoked += TransportBar_CommandInvoked;
        transportBar.CommandHoldEnded += (_, _) =>
        {
            EndActiveTransportShortcutFeedback();
            if (_resumePlaybackAfterBackwardSeek)
            {
                ResumePlaybackAfterBackwardSeek();
            }

            UpdateTransportPlaybackState();
        };

        // Log buttons
        logClearButton.Click += LogClearButton_Click;
        logCopyButton.Click += LogCopyButton_Click;
        logDownloadButton.Click += LogDownloadButton_Click;

        // Action bar
        brandLogoPictureBox.MouseLeftButtonUp += (_, _) => TryOpenUrl(AppVersion.CompanyUrl);
        copyrightLinkLabel.LinkClick += CopyrightLinkLabel_LinkClick;
        detailedLogCheckBox.Checked += DetailedLogCheckBox_CheckedChanged;
        detailedLogCheckBox.Unchecked += DetailedLogCheckBox_CheckedChanged;
        clearButton.Click += ClearButton_Click;
        reloadButton.Click += ReloadButton_Click;
        exportButton.Click += ExportButton_Click;
        compactFileNumbersCheckBox.Checked += CompactFileNumbersCheckBox_CheckedChanged;
        compactFileNumbersCheckBox.Unchecked += CompactFileNumbersCheckBox_CheckedChanged;

        // Fade curve icons
        fadeInCurveIcon.MouseLeftButtonUp += (_, _) => ShowFadeCurvePicker(isFadeIn: true);
        fadeOutCurveIcon.MouseLeftButtonUp += (_, _) => ShowFadeCurvePicker(isFadeIn: false);

        // Fade / transition radios
        WireFadeRadio(fadeInNoneRadio);
        WireFadeRadio(fadeInHalfSecondRadio);
        WireFadeRadio(fadeInOneSecondRadio);
        WireFadeRadio(fadeInThreeSecondsRadio);
        WireFadeRadio(fadeInSixSecondsRadio);
        WireFadeRadio(transitionTimeNoneRadio);
        WireFadeRadio(transitionTimeHalfSecondRadio);
        WireFadeRadio(transitionTimeOneSecondRadio);
        WireFadeRadio(transitionTimeThreeSecondsRadio);
        WireFadeRadio(transitionTimeSixSecondsRadio);
        WireFadeRadio(fadeInGroupNoneRadio);
        WireFadeRadio(fadeInGroupOneSecondRadio);
        WireFadeRadio(fadeInGroupThreeSecondsRadio);
        WireFadeRadio(fadeInGroupSixSecondsRadio);
        WireFadeRadio(fadeInGroupNineSecondsRadio);

        playMinusECheckBox.Checked += PlayMinusECheckBox_CheckedChanged;
        playMinusECheckBox.Unchecked += PlayMinusECheckBox_CheckedChanged;
        additiveLayersCheckBox.Checked += AdditiveLayersCheckBox_CheckedChanged;
        additiveLayersCheckBox.Unchecked += AdditiveLayersCheckBox_CheckedChanged;

        WireExitSourceRadio(changeOccursImmediateRadio, isChangeOccursAt: true);
        WireExitSourceRadio(changeOccursNextBarRadio, isChangeOccursAt: true);
        WireExitSourceRadio(changeOccursNextBeatRadio, isChangeOccursAt: true);
        WireExitSourceRadio(changeOccursNextCueRadio, isChangeOccursAt: true);
        WireExitSourceRadio(changeOccursExitCueRadio, isChangeOccursAt: true);
        WireExitSourceRadio(exitSourceImmediateRadio, isChangeOccursAt: false);
        WireExitSourceRadio(exitSourceNextBarRadio, isChangeOccursAt: false);
        WireExitSourceRadio(exitSourceNextBeatRadio, isChangeOccursAt: false);
        WireExitSourceRadio(exitSourceNextCueRadio, isChangeOccursAt: false);
        WireExitSourceRadio(exitSourceExitCueRadio, isChangeOccursAt: false);

        markerOptionsPanel.SettingsChanged += MarkerOptionsPanel_SettingsChanged;
        markerOptionsPanel.RequiredHeightChanged += MarkerOptionsPanel_RequiredHeightChanged;
    }

    private void MarkerOptionsPanel_RequiredHeightChanged(object? sender, EventArgs e)
    {
        // Form1: More Options 開閉はウィンドウ高さへ転嫁し、Music Playlist の高さを保つ。
        var previousPanelHeight = markerOptionsPanel.Height;
        var desiredPanelHeight = markerOptionsPanel.RequiredHeight;
        var delta = desiredPanelHeight - previousPanelHeight;
        markerOptionsPanel.Height = desiredPanelHeight;
        SyncRightSideContentHostHeight();
        UpdatePlaylistSelectorWidth();

        if (Math.Abs(delta) >= 0.5 && WindowState == WindowState.Normal)
        {
            if (delta < 0)
            {
                UpdateMinimumWindowSize();
            }

            var targetHeight = Height + delta;
            if (delta > 0)
            {
                Height = targetHeight;
                UpdateMinimumWindowSize();
            }
            else
            {
                Height = Math.Max(MinHeight, targetHeight);
            }
        }
        else
        {
            UpdateMinimumWindowSize();
        }
    }

    /// <summary>
    /// ホスト高さを「Fade 行高＋ More Options 高」に固定する。
    /// Playlist（Compact Num. 含む）の下端が Fade In セクション下端と一致する。
    /// </summary>
    private void SyncRightSideContentHostHeight()
    {
        fadeInSectionPanel.UpdateLayout();
        fadeOutSectionPanel.UpdateLayout();
        exitSourceAtSectionPanel.UpdateLayout();

        var transitionRowsHeight = Math.Max(
            Math.Max(fadeInSectionPanel.ActualHeight, fadeOutSectionPanel.ActualHeight),
            exitSourceAtSectionPanel.ActualHeight);
        if (transitionRowsHeight < 1)
        {
            transitionRowsHeight = Math.Max(
                Math.Max(fadeInSectionPanel.DesiredSize.Height, fadeOutSectionPanel.DesiredSize.Height),
                exitSourceAtSectionPanel.DesiredSize.Height);
        }

        var desired = transitionRowsHeight + markerOptionsPanel.Height;
        if (desired < 1)
        {
            return;
        }

        if (Math.Abs(rightSideContentHost.Height - desired) > 0.5)
        {
            rightSideContentHost.Height = desired;
        }
    }

    /// <summary>
    /// Music Playlist 幅と、Fade×3 列が Wrap せず並ぶ右側全体幅を Form1 同様に決める。
    /// 幅不足だと Exit Source At / Chg Occ At が下へ折り返され見切れる。
    /// </summary>
    private void UpdatePlaylistSelectorWidth()
    {
        if (!IsLoaded || logAreaPanel.ActualWidth <= 0)
        {
            return;
        }

        var fontSize = playlistHeaderLabel.FontSize > 0 ? playlistHeaderLabel.FontSize : 12;
        var fontFamily = playlistHeaderLabel.FontFamily;
        var textWidth = FlatPlaylistButton.MeasureDisplayTextWidth(
            playlistHeaderLabel.Text ?? string.Empty,
            fontSize,
            fontFamily);
        foreach (var button in _playlistButtons.Values)
        {
            textWidth = Math.Max(
                textWidth,
                FlatPlaylistButton.MeasureDisplayTextWidth(
                    button.Content as string ?? string.Empty,
                    button.FontSize > 0 ? button.FontSize : fontSize,
                    button.FontFamily));
        }

        var sampleButton = _playlistButtons.Values.FirstOrDefault();
        var sampleMargin = sampleButton is null
            ? DesignMetrics.From96(6)
            : sampleButton.Margin.Left + sampleButton.Margin.Right;
        var samplePadding = sampleButton is null
            ? DesignMetrics.From96(4)
            : sampleButton.Padding.Left + sampleButton.Padding.Right;
        var sampleSwatch = playlistListLayout.Children
            .OfType<Panel>()
            .SelectMany(row => row.Children.OfType<PlaylistGroupSwatch>())
            .FirstOrDefault();
        var swatchColumnWidth = sampleSwatch is null
            ? 0d
            : sampleSwatch.Width + sampleSwatch.Margin.Left + sampleSwatch.Margin.Right;
        var chromeWidth = playlistScrollViewer.Padding.Left
            + playlistScrollViewer.Padding.Right
            + SystemParameters.VerticalScrollBarWidth
            + swatchColumnWidth
            + sampleMargin
            + samplePadding
            + DesignMetrics.From96(4);
        const double minimumWidth = 132;
        var desiredPlaylistWidth = Math.Max(minimumWidth, textWidth + chromeWidth);

        var transitionWidth = GetTransitionColumnWidth();
        desiredPlaylistWidth = Math.Max(
            desiredPlaylistWidth,
            Math.Max(0, markerOptionsPanel.RequiredWidth - transitionWidth));

        if (Math.Abs(playlistSelectorPanel.Width - desiredPlaylistWidth) > 0.5)
        {
            playlistSelectorPanel.Width = desiredPlaylistWidth;
        }

        var desiredRightWidth = transitionWidth + desiredPlaylistWidth;
        if (Math.Abs(rightSidePanel.Width - desiredRightWidth) > 0.5)
        {
            rightSidePanel.Width = desiredRightWidth;
        }
    }

    /// <summary>Fade In / Fade Out / Exit Source At の3列が横並びになる必要幅。</summary>
    private double GetTransitionColumnWidth()
    {
        static double Horiz(FrameworkElement e) =>
            e.Width + e.Margin.Left + e.Margin.Right;

        return transitionSettingsPanel.Margin.Left
            + transitionSettingsPanel.Margin.Right
            + Horiz(fadeInSectionPanel)
            + Horiz(fadeOutSectionPanel)
            + Horiz(exitSourceAtSectionPanel);
    }

    private void QueuePlaylistSelectorWidthUpdate()
    {
        if (!IsLoaded)
        {
            UpdatePlaylistSelectorWidth();
            return;
        }

        Dispatcher.BeginInvoke(UpdatePlaylistSelectorWidth, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void WireFadeRadio(FlatOptionRadioButton radio) =>
        radio.Checked += FadeRadio_CheckedChanged;

    private void WireExitSourceRadio(FlatOptionRadioButton radio, bool isChangeOccursAt) =>
        radio.Checked += (_, _) => ExitSourceRadio_CheckedChanged(radio, isChangeOccursAt);

    private void ApplyAllowDropHandlers()
    {
        void HandleDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        void HandleDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            {
                return;
            }

            HandleDroppedFiles(paths);
        }

        foreach (var target in new UIElement[]
        {
            waveformHostPanel,
            waveformView,
            logAreaPanel,
            editorTextBox,
            transitionTimePanel,
            markerOptionsPanel,
            playlistSelectorPanel,
            playlistHeaderLabel,
            playlistScrollPanel,
            playlistListLayout,
        })
        {
            target.AllowDrop = true;
            target.DragOver += HandleDragOver;
            target.Drop += HandleDrop;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowSettings.Load()?.TryApply(this);

        Opacity = 0;
        InvalidateVisual();
        UpdateLayout();

        ApplyProjectProfile(_projectStore.GetActive(), applyLastSession: false);
        RefreshProjectComboItems(_loadedProjectName);
        topMostCheckBox.IsChecked = _appSettings.AlwaysOnTop;
        Topmost = _appSettings.AlwaysOnTop;
        detailedLogCheckBox.IsChecked = _developerSettings.DetailedPlaybackLog;
        TipService.Enabled = _appSettings.ShowTips;
        tipsToggleButton.Checked = _appSettings.ShowTips;
        UiStrings.SetLanguage(_appSettings.UiLanguage);
        UpdatePlaylistSelectorWidth();
        SyncRightSideContentHostHeight();
        RefreshLocalizedText();
        ApplyWaveformHeightScale();
        markerOptionsPanel.Height = markerOptionsPanel.RequiredHeight;
        SyncRightSideContentHostHeight();
        RefreshFadeCurveIcons();
        UpdateMinimumWindowSize();
        PositionLogButtons();

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            // フォームが不透明になる前にすりガラスを載せ、素の UI が一瞬出ないようにする
            SyncRightSideContentHostHeight();
            UpdateMinimumWindowSize();
            PositionLogButtons();
            UpdateLayout();
            SetUiInteractionLocked(UiInteractionLock.Load, locked: true, UiStrings.OverlayStarting);
            Opacity = 1;
            await RunStartupSequenceAsync().ConfigureAwait(true);
            _ = CheckForAppUpdateAsync();
        }), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Form1 UpdateMinimumWindowSize 相当。
    /// 最小幅＝著作権／Transport、最小高＝固定 chrome＋ログエリア必須高。
    /// </summary>
    private void UpdateMinimumWindowSize()
    {
        var safety = DesignMetrics.From96(8);
        var content = Content as FrameworkElement;
        var contentWidth = content?.ActualWidth ?? 0;
        var contentHeight = content?.ActualHeight ?? 0;
        var nonClientWidth = contentWidth > 1 ? Math.Max(0, ActualWidth - contentWidth) : 0;
        var nonClientHeight = contentHeight > 1 ? Math.Max(0, ActualHeight - contentHeight) : 0;

        fadeInSectionPanel.UpdateLayout();
        fadeOutSectionPanel.UpdateLayout();
        exitSourceAtSectionPanel.UpdateLayout();
        var transitionRowsHeight = Math.Max(
            Math.Max(
                ResolveElementHeight(fadeInSectionPanel, DesignMetrics.From96(120)),
                ResolveElementHeight(fadeOutSectionPanel, DesignMetrics.From96(120))),
            ResolveElementHeight(exitSourceAtSectionPanel, DesignMetrics.From96(120)));
        var requiredLogAreaHeight = transitionRowsHeight + markerOptionsPanel.RequiredHeight;

        var fixedChromeHeight =
            ResolveElementHeight(projectBar, DesignMetrics.ProjectBarHeight)
            + ResolveElementHeight(waveformHostPanel, waveformHostPanel.Height > 1
                ? waveformHostPanel.Height
                : DesignMetrics.WaveformHostHeight)
            + ResolveElementHeight(transportBar, DesignMetrics.TransportBarHeight)
            + ResolveElementHeight(waapiStatusBar, DesignMetrics.WaapiBarHeight)
            + ResolveElementHeight(actionBar, DesignMetrics.ActionBarHeight);

        var copyrightW = MeasureCopyrightPreferredWidth();
        var logoW = brandLogoPictureBox.ActualWidth > 0 ? brandLogoPictureBox.ActualWidth : 214;
        var controlsW = actionControlsPanel.ActualWidth > 0
            ? actionControlsPanel.ActualWidth
            : actionControlsPanel.DesiredSize.Width;
        var gap = DesignMetrics.Dip(18);
        var actionMin = actionBar.Padding.Left
            + logoW
            + 8
            + copyrightW
            + gap
            + controlsW
            + actionBar.Padding.Right;
        var transportMin = transportBar.RequiredWidth;
        var minClientWidth = Math.Max(actionMin, transportMin);
        var nextMinWidth = Math.Ceiling(minClientWidth + nonClientWidth + safety);
        var nextMinHeight = Math.Ceiling(
            fixedChromeHeight + requiredLogAreaHeight + nonClientHeight + safety);

        if (Math.Abs(MinWidth - nextMinWidth) > 0.5)
        {
            MinWidth = nextMinWidth;
        }

        if (Math.Abs(MinHeight - nextMinHeight) > 0.5)
        {
            MinHeight = nextMinHeight;
        }

        if (WindowState == WindowState.Normal && Width < MinWidth)
        {
            Width = MinWidth;
        }

        if (WindowState == WindowState.Normal && Height < MinHeight)
        {
            Height = MinHeight;
        }
    }

    private static double ResolveElementHeight(FrameworkElement element, double fallback)
    {
        if (element.ActualHeight > 1)
        {
            return element.ActualHeight;
        }

        if (element.Height > 1 && !double.IsNaN(element.Height) && !double.IsInfinity(element.Height))
        {
            return element.Height;
        }

        return fallback;
    }

    private double MeasureCopyrightPreferredWidth()
    {
        var text = copyrightLinkLabel.LinkText;
        if (string.IsNullOrEmpty(text))
        {
            return DesignMetrics.Dip(120);
        }

        var typeface = new Typeface(
            copyrightLinkLabel.FontFamily,
            copyrightLinkLabel.FontStyle,
            copyrightLinkLabel.FontWeight,
            copyrightLinkLabel.FontStretch);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double max = 0;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var formatted = new FormattedText(
                string.IsNullOrEmpty(line) ? " " : line,
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                copyrightLinkLabel.FontSize,
                Brushes.White,
                dpi);
            max = Math.Max(max, formatted.Width);
        }

        return max + DesignMetrics.Dip(12);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        StopPlaybackForExport();
        _waapiPollTimer.Stop();
        _playheadTimer.Stop();
        DisposePlaylistPlaybackTimers();
        // _closing より先に保存する（Autosave / Last Session が _closing でスキップされないように）
        AutosaveCurrentProject();
        _projectStore.SaveActiveNameOnly();
        WindowSettings.FromWindow(this).Save();
        _closing = true;
        _audioPlayer.Dispose();
    }

    private void CopyrightLinkLabel_LinkClick(object? sender, SmoothLinkClickEventArgs e)
    {
        if (string.Equals(e.LinkId, "license", StringComparison.Ordinal))
        {
            ShowEmbeddedLicenseInLog();
            return;
        }

        TryOpenUrl(AppVersion.RepositoryUrl);
    }

    private void ShowEmbeddedLicenseInLog()
    {
        var body = AppEmbeddedResources.ReadUdevGothicLicenseText();
        ClearLogText();
        if (string.IsNullOrWhiteSpace(body))
        {
            AppendReport(UiStrings.DialogLicenseMissing + Environment.NewLine, colorize: false);
            return;
        }

        AppendReport(UiStrings.DialogLicenseTitle + Environment.NewLine + Environment.NewLine, colorize: false);
        AppendReport(body.TrimEnd() + Environment.NewLine, colorize: false);
        editorTextBox.CaretPosition = editorTextBox.Document.ContentStart;
        editorTextBox.ScrollToHome();
    }

    private static void TryOpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ブラウザ起動失敗は無視する。
        }
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        TryReleaseLogFocusOnOutsideMouseDown();
        TryReleaseMarkerOptionsFocusOnOutsideMouseDown();
    }

    private void TryReleaseLogFocusOnOutsideMouseDown()
    {
        if (!editorTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        var pt = Mouse.GetPosition(editorTextBox);
        if (pt.X >= 0 && pt.Y >= 0 && pt.X < editorTextBox.ActualWidth && pt.Y < editorTextBox.ActualHeight)
        {
            return;
        }

        ReleaseFocusToWaveform();
    }

    private void TryReleaseMarkerOptionsFocusOnOutsideMouseDown()
    {
        if (markerOptionsPanel.IsPointerOverEditableTextBox())
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (!markerOptionsPanel.HasEditableTextBoxFocus)
            {
                return;
            }

            ReleaseFocusToWaveform(forceTextBoxRelease: true);
        }, DispatcherPriority.Input);
    }

    private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        if (transportBar.IsMetronomeHitAtScreenPoint(screenPoint)
            && TryAdjustMetronomeVolume(e.Delta))
        {
            e.Handled = true;
            return;
        }

        // Form1: 波形上でのみズーム／パン。ログや Playlist のスクロールは奪わない。
        if (!waveformView.IsMouseOver)
        {
            return;
        }

        var position = e.GetPosition(waveformView);
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            waveformView.ZoomAmpByWheel(e.Delta);
            transportBar.PulseCommandFeedback(
                e.Delta > 0 ? TransportCommand.AmpZoomIn : TransportCommand.AmpZoomOut);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            waveformView.PanTimeByWheel(e.Delta);
            transportBar.PulseCommandFeedback(
                e.Delta > 0 ? TransportCommand.PreviousPage : TransportCommand.NextPage);
        }
        else
        {
            waveformView.ZoomTimeByWheel(e.Delta, (int)position.X);
            transportBar.PulseCommandFeedback(
                e.Delta > 0 ? TransportCommand.TimeZoomIn : TransportCommand.TimeZoomOut);
        }

        e.Handled = true;
    }
}
