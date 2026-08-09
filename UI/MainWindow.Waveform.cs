using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>ドロップ／読込・波形イベント配線・セッション保存復元・マーカーオプション。</summary>
public partial class MainWindow
{
    private WaveformPreviewData? _loadedPreview;
    private WaveformPreviewSession? _previewSession;
    private IReadOnlyList<string> _lastInputFiles = [];
    private string? _sourceBaseNameOverride;
    private int _nextGroupId = 1;
    private int _nextColorIndex;

    private void InitializeWaveformEventWiring()
    {
        waveformView.SeekRequested += (_, progress) => SeekPlayback(progress);
        waveformView.MarkerEditRequested += WaveformView_MarkerEditRequested;
        waveformView.SourceNameEditCommitted += (_, e) => CommitSourceNameOverride(e.Name);
        waveformView.SourceNameEditStateChanged += (_, e) =>
            SetUiInteractionLocked(UiInteractionLock.SourceNameEdit, e.IsEditing);
        waveformView.MarkerCommentEditCommitted += (_, e) => CommitMarkerComment(e.SampleOffset, e.Comment);
        waveformView.MarkerCommentEditStateChanged += (_, e) =>
            SetUiInteractionLocked(UiInteractionLock.MarkerCommentEdit, e.IsEditing);
        waveformView.MarkerSessionDeleteRequested += (_, e) => TryDeleteWaveOnlyMarker(e.SampleOffset);
        waveformView.MarkerSessionMoveRequested += (_, e) =>
            TryMoveWaveOnlyMarker(e.FromSampleOffset, e.ToSampleOffset, e.ShiftPreviousMarker);
        waveformView.RegionFadeChanged += (_, e) => UpsertRegionEdgeFade(e.Fade);
        // 波形内プレイリストレーン上のホバー → 一覧側の文字色更新（枠は一覧ホバー専用）
        waveformView.PlaylistHoverChanged += (_, partNumber) =>
        {
            _hoveredPlaylistPartNumber = partNumber;
            QueuePlaylistHoverColorRefresh();
        };
        waveformView.TransportFeedbackRequested += (_, command) =>
            transportBar.PulseCommandFeedback(command);
        waveformView.InfoLaneWidthChanged += (_, _) => SyncProjectNameComboWidthToInfoLane();
        waveformView.TimeViewChanged += (_, _) => UpdateWaveformHorizontalScrollBar();
        waveformHorizontalScrollBar.ScrollRequested += (_, viewStart) => waveformView.SetTimeViewStart(viewStart);
    }

    /// <summary>Info レーン幅に合わせてプロジェクト名コンボ幅を揃える（Form1 同等）。</summary>
    private void SyncProjectNameComboWidthToInfoLane()
    {
        if (!IsLoaded || !waveformView.IsLoaded)
        {
            return;
        }

        // InfoLaneRightX はデバイス px → DIP にしてから画面座標へ。
        var dpi = VisualTreeHelper.GetDpi(waveformView).PixelsPerDip;
        if (dpi <= 0.01)
        {
            dpi = 1d;
        }

        var infoRightDip = waveformView.InfoLaneRightX / dpi;
        var infoRightScreen = waveformView.PointToScreen(new Point(infoRightDip, 0d)).X;
        var comboLeftScreen = projectNameComboBox.PointToScreen(new Point(0d, 0d)).X;
        var minWidth = DesignMetrics.From96(48);
        var widthDip = Math.Max(minWidth, infoRightScreen - comboLeftScreen);
        if (Math.Abs(projectNameColumn.Width.Value - widthDip) < 0.5)
        {
            return;
        }

        projectNameColumn.Width = new GridLength(widthDip);
    }

    private void UpdateWaveformHorizontalScrollBar() =>
        waveformHorizontalScrollBar.SetViewport(waveformView.TimeViewStart, waveformView.TimeViewSpan);

    private async void HandleDroppedFiles(string[] paths)
    {
        if ((_uiInteractionLocks & UiInteractionLock.Load) != 0)
        {
            return;
        }

        var preview = await ProcessDroppedFilesAsync(paths, isLastSessionLoad: false).ConfigureAwait(true);
        if (preview is not null)
        {
            AutosaveCurrentProject();
        }
    }

    private async void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastInputFiles.Count == 0 || (_uiInteractionLocks & UiInteractionLock.Load) != 0)
        {
            return;
        }

        await ProcessDroppedFilesAsync(_lastInputFiles, isLastSessionLoad: false).ConfigureAwait(true);
    }

    /// <summary>
    /// ピーク解析を背景スレッドで行い、その間すりガラスで UI を覆う（Form1 ProcessDroppedFiles 相当）。
    /// </summary>
    private async Task<WaveformPreviewData?> ProcessDroppedFilesAsync(
        IReadOnlyList<string> paths,
        bool isLastSessionLoad,
        LastWaveSessionState? capturedSession = null)
    {
        _loadLockCount++;
        var loadMessage = isLastSessionLoad
            ? UiStrings.OverlayLoadingLastSession
            : UiStrings.OverlayLoading;
        SetUiInteractionLocked(UiInteractionLock.Load, locked: true, loadMessage);

        WaveformPreviewData? preview = null;
        try
        {
            StopPlaybackForExport();
            _audioPlayer.Clear();
            waveformView.CommitDarkFrame();

            var expectedFormat = _appSettings.ToExpectedWaveformFormat();
            var fileList = paths.ToArray();
            var (report, previewData) = await Task.Run(() =>
                {
                    var text = DroppedFilesProcessor.Process(fileList, out var processed, expectedFormat);
                    return (text, processed);
                })
                .ConfigureAwait(true);

            if (_closing)
            {
                return null;
            }

            AppendReport(report);
            preview = previewData;
            if (preview is null)
            {
                return null;
            }

            _lastInputFiles = fileList;
            if (!isLastSessionLoad)
            {
                _sourceBaseNameOverride = null;
            }

            if (capturedSession is not null && !capturedSession.MatchesLoadedWave(preview))
            {
                capturedSession = null;
            }

            ApplyLoadedPreviewToUi(preview, capturedSession);
            return preview;
        }
        finally
        {
            _loadLockCount = Math.Max(0, _loadLockCount - 1);
            if (_loadLockCount == 0)
            {
                SetUiInteractionLocked(UiInteractionLock.Load, locked: false);
            }
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        ClearWaveformState();
        ClearLogText();
        ProjectSettingsStore.DeleteLastWaveSessionFile(_loadedProjectName);
    }

    private void ClearWaveformState()
    {
        StopPlaybackForExport();
        _audioPlayer.Clear();
        _loadedPreview = null;
        _previewSession = null;
        _lastInputFiles = [];
        waveformView.SetPreview(WavPeakData.Empty, string.Empty);
        waveformView.SetMarkers([]);
        waveformView.SetRegions([]);
        waveformView.SetOutputParts([]);
        ClearPlaylistChoices();
        exportButton.IsEnabled = false;
        reloadButton.IsEnabled = false;
    }

    /// <summary>読み込んだ波形をアプリ全体（波形ビュー・プレイリスト・トランスポート）へ反映する。</summary>
    private void ApplyLoadedPreviewToUi(WaveformPreviewData preview, LastWaveSessionState? capturedSession)
    {
        _loadedPreview = preview;
        _previewSession = new WaveformPreviewSession(preview);
        _previewSession.SetCommentRule(_markerSettings.ToCommentRule());
        RememberLoadedWavePaths(preview);

        if (capturedSession is not null)
        {
            RestoreSessionIntoPreview(capturedSession);
        }

        waveformView.SetExpectedWaveformFormat(_appSettings.ToExpectedWaveformFormat());
        waveformView.SuspendPresentationRebuild();
        try
        {
            // Bars を渡さないと Tempo / 拍子 / 小節線が空のまま（WinForms SetPreview と同等）
            waveformView.SetPreview(
                preview.Peaks,
                preview.SourcePath,
                preview.WavInfo,
                preview.Bars,
                _previewSession.EffectiveMarkers,
                _previewSession.EffectiveRegions,
                _previewSession.EffectiveOutputParts,
                preview.AllowsSessionMarkerEdit,
                preview.SourceSpans,
                sourceNameEditable: !preview.IsMultiWaveOnly);
            waveformView.SetSourceDisplayName(ResolveSourceDisplayName());
            waveformView.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
            waveformView.SetDisabledPlaylistParts(GetDisabledPartNumbers());
        }
        finally
        {
            waveformView.ResumePresentationRebuild();
        }

        _audioPlayer.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        if (preview.IsMultiWaveOnly)
        {
            _audioPlayer.LoadVirtualConcat(preview.SourceSpans);
        }
        else
        {
            _audioPlayer.Load(preview.SourcePath);
        }

        _audioPlayer.SetLoopPlans(WaveAudioPlayer.BuildLoopPlans(_previewSession.EffectiveRegions));
        _audioPlayer.SetMetronomeBars(preview.Bars);
        ApplyWaveformFadeCurveDefaults();
        RefreshPlaylistButtons();
        UpdateNavigationAvailability();
        UpdateExportEnabled();
        reloadButton.IsEnabled = _lastInputFiles.Count > 0 || _lastWavePaths.Count > 0;
        // パスとセッションをプロジェクト設定へ残す（Keep Last Session）
        AutosaveCurrentProject();
    }

    private string ResolveSourceDisplayName() =>
        !string.IsNullOrWhiteSpace(_sourceBaseNameOverride)
            ? _sourceBaseNameOverride!
            : Path.GetFileNameWithoutExtension(_loadedPreview?.SourcePath ?? string.Empty);

    private void CommitSourceNameOverride(string name)
    {
        var trimmed = name.Trim();
        var defaultName = Path.GetFileNameWithoutExtension(_loadedPreview?.SourcePath ?? string.Empty);
        _sourceBaseNameOverride = string.Equals(trimmed, defaultName, StringComparison.Ordinal) || trimmed.Length == 0
            ? null
            : trimmed;
        waveformView.SetSourceDisplayName(ResolveSourceDisplayName());
        if (_loadedPreview is not null)
        {
            UpdatePlaylistDisplayNames(GetEffectiveOutputParts());
        }

        SaveLastWaveSessionIfLoaded();
    }

    private void WaveformView_MarkerEditRequested(object? sender, MarkerEditRequestedEventArgs e)
    {
        if (_previewSession is null)
        {
            return;
        }

        var changed = e.Mode == MarkerEditMode.Add
            ? _previewSession.AddMarkers(e.SampleOffsets)
            : _previewSession.RemoveMarkers(e.SampleOffsets);

        if (changed)
        {
            RefreshMarkersOnWaveform();
            SaveLastWaveSessionIfLoaded();
        }
    }

    private bool TryDeleteWaveOnlyMarker(long sampleOffset)
    {
        if (_previewSession is null || !_previewSession.TryRemoveWaveOnlyMarker(sampleOffset))
        {
            return false;
        }

        RefreshMarkersOnWaveform();
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    private bool TryMoveWaveOnlyMarker(long fromSampleOffset, long toSampleOffset, bool shiftPreviousMarker)
    {
        if (_previewSession is null)
        {
            return false;
        }

        var moved = shiftPreviousMarker
            ? _previewSession.TryMoveWaveOnlyMarkerWithPrevious(fromSampleOffset, toSampleOffset)
            : _previewSession.TryMoveWaveOnlyMarker(fromSampleOffset, toSampleOffset);
        if (!moved)
        {
            return false;
        }

        RefreshMarkersOnWaveform();
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    private void CommitMarkerComment(long sampleOffset, string comment)
    {
        if (_previewSession is null || !_previewSession.TrySetWaveOnlyMarkerComment(sampleOffset, comment))
        {
            return;
        }

        RefreshMarkersOnWaveform();
        SaveLastWaveSessionIfLoaded();
    }

    private void RefreshMarkersOnWaveform()
    {
        if (_previewSession is null)
        {
            return;
        }

        waveformView.SetMarkers(_previewSession.EffectiveMarkers);
    }

    private void UpsertRegionEdgeFade(RegionEdgeFade fade)
    {
        _previewSession?.UpsertRegionEdgeFade(fade);
        if (_previewSession is null)
        {
            return;
        }

        waveformView.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        _audioPlayer.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        SaveLastWaveSessionIfLoaded();
    }

    private void MarkerOptionsPanel_SettingsChanged(object? sender, EventArgs e)
    {
        markerOptionsPanel.Bind(_markerSettings);
        _previewSession?.SetCommentRule(_markerSettings.ToCommentRule());
        AutosaveCurrentProject();
    }

    private void ApplyWaveformFadeCurveDefaults()
    {
        waveformView.SetRegionEdgeFades(_previewSession?.RegionEdgeFades ?? []);
    }

    private HashSet<int> GetDisabledPartNumbers() => _disabledPartNumbers;

    private void ShowFadeCurvePicker(bool isFadeIn)
    {
        var current = isFadeIn ? _appSettings.DefaultWaveformFadeInCurve : _appSettings.DefaultWaveformFadeOutCurve;
        var icon = isFadeIn ? fadeInCurveIcon : fadeOutCurveIcon;
        ContextMenu? menu = null;
        FadeCurveIcons.ShowPicker(
            icon,
            new Point(0, icon.ActualHeight),
            current,
            isFadeIn,
            selected =>
            {
                if (isFadeIn)
                {
                    _appSettings.SaveDefaultFadeCurves(
                        selected,
                        _appSettings.DefaultWaveformFadeOutCurve,
                        _appSettings.DefaultPlaylistFadeInCurve,
                        _appSettings.DefaultPlaylistFadeOutCurve);
                }
                else
                {
                    _appSettings.SaveDefaultFadeCurves(
                        _appSettings.DefaultWaveformFadeInCurve,
                        selected,
                        _appSettings.DefaultPlaylistFadeInCurve,
                        _appSettings.DefaultPlaylistFadeOutCurve);
                }

                RefreshFadeCurveIcons();
            },
            ref menu);
    }

    private void RefreshFadeCurveIcons()
    {
        LayoutPlaylistFadeCurveIcon(fadeInHeaderLabel, fadeInCurveIcon);
        LayoutPlaylistFadeCurveIcon(transitionTimeHeaderLabel, fadeOutCurveIcon);

        var inSize = Math.Max(8, (int)Math.Round(fadeInCurveIcon.Height));
        var outSize = Math.Max(8, (int)Math.Round(fadeOutCurveIcon.Height));
        fadeInCurveIcon.Source = FadeCurveIcons.Create(
            _appSettings.DefaultWaveformFadeInCurve,
            isFadeIn: true,
            selected: false,
            pixelSize: inSize,
            leftMargin: 0);
        fadeOutCurveIcon.Source = FadeCurveIcons.Create(
            _appSettings.DefaultWaveformFadeOutCurve,
            isFadeIn: false,
            selected: false,
            pixelSize: outSize,
            leftMargin: 0);
        TipService.Set(fadeInCurveIcon, UiStrings.LabelRegionFadeCurve(_appSettings.DefaultWaveformFadeInCurve));
        TipService.Set(fadeOutCurveIcon, UiStrings.LabelRegionFadeCurve(_appSettings.DefaultWaveformFadeOutCurve));
    }

    /// <summary>WinForms LayoutPlaylistFadeCurveIcon 相当。見出し帯の内側右端へアイコンを置く。</summary>
    private void LayoutPlaylistFadeCurveIcon(SectionHeaderLabel header, System.Windows.Controls.Image icon)
    {
        if (header.ActualWidth <= 0 || header.ActualHeight <= 0)
        {
            return;
        }

        var bar = header.GetBarBounds();
        var size = Math.Max(8, (int)Math.Round(bar.Height * 0.75));
        var iconWidth = FadeCurveIcons.WidthFor(size);
        var rightInset = Math.Max(DesignMetrics.Dip(4), DesignMetrics.From96(6));
        icon.Width = iconWidth;
        icon.Height = size;
        icon.Margin = new Thickness(
            Math.Max(bar.Left, bar.Right - rightInset - iconWidth),
            bar.Top + Math.Max(0, (bar.Height - size) / 2),
            0,
            0);
        Panel.SetZIndex(icon, 1);
        icon.Stretch = System.Windows.Media.Stretch.None;
    }

    private void UpdateExportEnabled()
    {
        exportButton.IsEnabled = _loadedPreview is not null
            && _previewSession is not null
            && _previewSession.EffectiveOutputParts.Any(p => !_disabledPartNumbers.Contains(p.Number));
    }
}
