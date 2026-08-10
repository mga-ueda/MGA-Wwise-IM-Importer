using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>ドロップ／読込・波形イベント配線・セッション保存復元・マーカーオプション。</summary>
public partial class MainWindow
{
    private WaveformPreviewData? _loadedPreview;
    private WaveformPreviewSession? _previewSession;
    private readonly WaveOnlyMarkerHistory _waveOnlyMarkerHistory = new();
    private readonly RegionEdgeFadeHistory _regionEdgeFadeHistory = new();
    private bool _pendingWaveOnlySessionPersist;
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
        waveformView.SizeChanged += (_, _) => SyncProjectNameComboWidthToInfoLane();
        waveformView.TimeViewChanged += (_, _) => UpdateWaveformHorizontalScrollBar();
        waveformHorizontalScrollBar.ScrollRequested += (_, viewStart) => waveformView.SetTimeViewStart(viewStart);
    }

    /// <summary>
    /// Info レーン（Measure 列）右端にプロジェクト名コンボ右端を揃える（Form1 同等）。
    /// 波形名でレーン幅が変わるたびに追従する。
    /// </summary>
    private void SyncProjectNameComboWidthToInfoLane()
    {
        if (!IsLoaded || !waveformView.IsLoaded || !projectNameComboBox.IsLoaded)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(waveformView).PixelsPerDip;
        if (dpi <= 0.01)
        {
            dpi = 1d;
        }

        // InfoLaneRightX は WaveformView クライアント原点からのデバイス px。
        // TranslatePoint は DIP なので、差分をそのまま列幅（DIP）に使える。
        // （PointToScreen 差をそのまま渡すとデバイス px を DIP 扱いして高 DPI で広がりすぎる）
        var infoRightInWaveformDip = waveformView.InfoLaneRightX / dpi;
        var infoRightInWindow = waveformView.TranslatePoint(new Point(infoRightInWaveformDip, 0d), this);
        var comboLeftInWindow = projectNameComboBox.TranslatePoint(new Point(0d, 0d), this);
        var minWidth = DesignMetrics.From96(48);
        var widthDip = Math.Max(minWidth, infoRightInWindow.X - comboLeftInWindow.X);
        if (widthDip < minWidth || double.IsNaN(widthDip) || double.IsInfinity(widthDip))
        {
            return;
        }

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
        if (_uiInteractionLocks != UiInteractionLock.None)
        {
            return;
        }

        ClearCurrentProjectToDefaults();
        ReleaseFocusToWaveform();
    }

    /// <summary>
    /// 波形・セッションを卸し、選択中プロジェクトの設定をアプリ既定へ戻して保存する。
    /// Always on Top（アプリ設定）、書き出し先フォルダ、WAAPI Keep Target は変更しない。
    /// プロジェクト名／一覧は消さない。
    /// </summary>
    private void ClearCurrentProjectToDefaults()
    {
        ClearWaveformState();

        var name = _creatingNewProject || string.IsNullOrWhiteSpace(_loadedProjectName)
            ? _projectStore.ActiveName
            : _loadedProjectName;
        _creatingNewProject = false;

        // CLEAR でもパス系は現状を維持する（既定の空文字で潰さない）。
        var preservedOutputDirectory = _projectOutputDirectory;
        var preservedKeepTarget = _keepTarget;
        var preservedKeptTargetPath = _keptTargetPath;
        var preservedKeptTargetProjectFilePath = _keptTargetProjectFilePath;
        // More Options の開閉はユーザー操作のまま残す（既定の展開で上書きしない）。
        var preservedMoreOptionsExpanded = markerOptionsPanel.MoreOptionsExpanded;

        var profile = ProjectSettingsStore.CreateAppDefaults(name);
        profile.OutputDirectory = preservedOutputDirectory;
        profile.KeepTarget = preservedKeepTarget;
        profile.KeptTargetPath = preservedKeptTargetPath;
        profile.KeptTargetProjectFilePath = preservedKeptTargetProjectFilePath;
        profile.MoreOptionsExpanded = preservedMoreOptionsExpanded;

        if (_projectStore.ContainsName(name))
        {
            try
            {
                _projectStore.SaveProfile(name, name, profile, creatingNew: false);
                ProjectSettingsStore.DeleteLastWaveSessionFile(name);
            }
            catch (Exception ex)
            {
                OwnerCenteredMessageBox.Show(
                    this,
                    ex.Message,
                    UiStrings.DialogClearProjectFailedTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ApplyProjectProfile(_projectStore.GetActive(), applyLastSession: false);
                RefreshProjectComboItems(_loadedProjectName);
                return;
            }
        }

        ApplyProjectProfile(profile, applyLastSession: false);
        RefreshProjectComboItems(name);
        ClearLogText();
        AppendReport(UiStrings.LogProjectCleared(name));
    }

    /// <summary>
    /// 読み込み中の波形・再生・Playlist／セッション状態をすべて卸す
    /// （Form1 ClearLoadedWaveAndSession 相当）。
    /// </summary>
    private void ClearWaveformState()
    {
        _exportGeneration++;
        StopPlaybackForExport();
        _audioPlayer.Clear();
        ApplyMetronomeBarsFromPreview(null);
        _loadedPreview = null;
        _previewSession = null;
        _waveOnlyMarkerHistory.Clear();
        _regionEdgeFadeHistory.Clear();
        _pendingWaveOnlySessionPersist = false;
        _sourceBaseNameOverride = null;
        _lastPlaybackStartProgress = null;
        _lastJumpedBarNumber = null;
        _lastInputFiles = [];
        waveformView.SetPreview(WavPeakData.Empty, string.Empty);
        waveformView.SetMarkers([]);
        waveformView.SetRegions([]);
        waveformView.SetOutputParts([]);
        reloadButton.IsEnabled = false;
        markerOptionsPanel.SetMarkerPlacementOptionsEnabled(true);
        UpdateWaveOnlyExitSourceOptionsEnabled();
        ClearPendingPlaylistUiTransition();
        ClearPlaylistChoices(UiStrings.PlaylistNone);
        transportBar.SetPosition(null);
        UpdateNavigationAvailability();
        UpdateTransportPlaybackState();
        UpdateSourceLevelMeter();
        UpdateWaveformHorizontalScrollBar();
        UpdateExportEnabled();
    }

    /// <summary>読み込んだ波形をアプリ全体（波形ビュー・プレイリスト・トランスポート）へ反映する。</summary>
    private void ApplyLoadedPreviewToUi(WaveformPreviewData preview, LastWaveSessionState? capturedSession)
    {
        _loadedPreview = preview;
        _previewSession = new WaveformPreviewSession(preview);
        _previewSession.SetCommentRule(_markerSettings.ToCommentRule());
        waveformView.MarkerGridOverride = _markerSettings.GridOverride;
        _waveOnlyMarkerHistory.Clear();
        _regionEdgeFadeHistory.Clear();
        _pendingWaveOnlySessionPersist = false;
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

        SyncPlaybackRegionsToPlayer(_previewSession.EffectiveRegions);
        ApplyMetronomeBarsFromPreview(preview);
        ApplyWaveformFadeCurveDefaults();
        UpdateWaveOnlyExitSourceOptionsEnabled();
        RefreshPlaylistButtons();
        UpdateNavigationAvailability();
        // SetPreview が ClearPlayhead するため、読み込み直後は冒頭にシークバーを出す（Form1 同等）
        SeekPlayback(0);
        transportBar.SetPosition(ResolvePositionInfo(0));
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
        TryMutateWaveOnlyMarkers(session => e.Mode == MarkerEditMode.Add
            ? session.AddMarkers(e.SampleOffsets)
            : session.RemoveMarkers(e.SampleOffsets));
    }

    private bool TryDeleteWaveOnlyMarker(long sampleOffset) =>
        TryMutateWaveOnlyMarkers(session => session.TryRemoveWaveOnlyMarker(sampleOffset));

    private bool TryMoveWaveOnlyMarker(long fromSampleOffset, long toSampleOffset, bool shiftPreviousMarker) =>
        TryMutateWaveOnlyMarkers(session => shiftPreviousMarker
            ? session.TryMoveWaveOnlyMarkerWithPrevious(fromSampleOffset, toSampleOffset)
            : session.TryMoveWaveOnlyMarker(fromSampleOffset, toSampleOffset));

    private void CommitMarkerComment(long sampleOffset, string comment)
    {
        TryMutateWaveOnlyMarkers(session => session.TrySetWaveOnlyMarkerComment(sampleOffset, comment));
    }

    /// <summary>
    /// Wave 単体マーカーを変更し、成功したら Undo 履歴へ積む（Form1 同等）。
    /// パート構成が変わったときだけ Playlist UI を作り直す。
    /// </summary>
    private bool TryMutateWaveOnlyMarkers(
        Func<WaveformPreviewSession, bool> mutate,
        bool persistSession = true)
    {
        if (_previewSession is not { AllowsSessionMarkerEdit: true } session)
        {
            return false;
        }

        var before = session.GetWaveOnlySessionMarkers();
        if (before is null)
        {
            return false;
        }

        var beforeParts = session.EffectiveOutputParts.ToArray();
        if (!mutate(session))
        {
            return false;
        }

        _waveOnlyMarkerHistory.PushBeforeChange(before);
        ApplyWaveOnlySessionPresentation(
            session,
            refreshPlaylists: !AreOutputPartsEquivalent(beforeParts, session.EffectiveOutputParts));
        if (persistSession)
        {
            SaveLastWaveSessionIfLoaded();
        }

        return true;
    }

    private bool TryUndoWaveOnlyMarkerEdit()
    {
        if (_previewSession is not { AllowsSessionMarkerEdit: true } session)
        {
            return false;
        }

        var current = session.GetWaveOnlySessionMarkers();
        if (current is null
            || !_waveOnlyMarkerHistory.TryUndo(current, out var restored))
        {
            return false;
        }

        var beforeParts = session.EffectiveOutputParts.ToArray();
        if (!session.TryReplaceWaveOnlySessionMarkers(restored))
        {
            return false;
        }

        ApplyWaveOnlySessionPresentation(
            session,
            refreshPlaylists: !AreOutputPartsEquivalent(beforeParts, session.EffectiveOutputParts));
        waveformView.SetSelectedMarkerSampleOffset(null);
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    private bool TryRedoWaveOnlyMarkerEdit()
    {
        if (_previewSession is not { AllowsSessionMarkerEdit: true } session)
        {
            return false;
        }

        var current = session.GetWaveOnlySessionMarkers();
        if (current is null
            || !_waveOnlyMarkerHistory.TryRedo(current, out var restored))
        {
            return false;
        }

        var beforeParts = session.EffectiveOutputParts.ToArray();
        if (!session.TryReplaceWaveOnlySessionMarkers(restored))
        {
            return false;
        }

        ApplyWaveOnlySessionPresentation(
            session,
            refreshPlaylists: !AreOutputPartsEquivalent(beforeParts, session.EffectiveOutputParts));
        waveformView.SetSelectedMarkerSampleOffset(null);
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    /// <summary>セッションの現在状態を波形・エンジン・Playlist UI へ一括反映する（Form1 同等）。</summary>
    private void ApplyWaveOnlySessionPresentation(
        WaveformPreviewSession session,
        bool refreshPlaylists = true)
    {
        waveformView.SuspendPresentationRebuild();
        try
        {
            waveformView.SetMarkers(session.EffectiveMarkers);
            waveformView.SetRegions(session.EffectiveRegions);
            waveformView.SetOutputParts(session.EffectiveOutputParts);
            waveformView.SetRegionEdgeFades(session.RegionEdgeFades);
        }
        finally
        {
            waveformView.ResumePresentationRebuild();
        }

        if (_audioPlayer.HasSource)
        {
            _audioPlayer.SetRegionEdgeFades(session.RegionEdgeFades);
        }

        SyncPlaybackRegionsToPlayer(session.EffectiveRegions);
        if (refreshPlaylists)
        {
            RefreshPlaylistButtons();
        }

        UpdateExportEnabled();
        UpdateNavigationAvailability();
        AppendPendingWaveOnlyMarkerRenameLogs(session);
    }

    private static bool AreOutputPartsEquivalent(
        IReadOnlyList<WaveformOutputPart> left,
        IReadOnlyList<WaveformOutputPart> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Number != right[i].Number
                || left[i].StartSampleOffset != right[i].StartSampleOffset
                || left[i].EndSampleOffset != right[i].EndSampleOffset)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Loop→-L の 2 マーカー化状態や -L/-E 系の可視化など、埋め込みマーカーの自動リネームをログへ出す。
    /// </summary>
    private void AppendPendingWaveOnlyMarkerRenameLogs(WaveformPreviewSession session)
    {
        foreach (var rename in session.TakePendingWaveMarkerRenames())
        {
            AppendReport(
                UiStrings.LogWaveOnlyMarkerRenamed(rename.FromComment, rename.ToComment)
                + Environment.NewLine);
        }
    }

    private void FlushPendingWaveOnlySessionPersist()
    {
        if (!_pendingWaveOnlySessionPersist)
        {
            return;
        }

        _pendingWaveOnlySessionPersist = false;
        SaveLastWaveSessionIfLoaded();
    }

    private void RefreshMarkersOnWaveform()
    {
        if (_previewSession is null)
        {
            return;
        }

        waveformView.SetMarkers(_previewSession.EffectiveMarkers);
        waveformView.SetRegions(_previewSession.EffectiveRegions);
        SyncPlaybackRegionsToPlayer(_previewSession.EffectiveRegions);
    }

    /// <summary>-L プランと -R 無音区間を再生エンジンへ反映する。</summary>
    private void SyncPlaybackRegionsToPlayer(IReadOnlyList<WaveformRegionMark> regions)
    {
        _audioPlayer.SetLoopPlans(WaveAudioPlayer.BuildLoopPlans(regions));
        _audioPlayer.SetExcludedRegions(regions);
    }

    private void UpsertRegionEdgeFade(RegionEdgeFade fade)
    {
        if (_previewSession is null)
        {
            return;
        }

        _regionEdgeFadeHistory.PushBeforeChange(_previewSession.RegionEdgeFades);
        _previewSession.UpsertRegionEdgeFade(fade);
        waveformView.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        _audioPlayer.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        SaveLastWaveSessionIfLoaded();
    }

    private bool TryUndoRegionEdgeFade()
    {
        if (_previewSession is null)
        {
            return false;
        }

        var current = _previewSession.RegionEdgeFades;
        if (!_regionEdgeFadeHistory.TryUndo(current, out var restored))
        {
            return false;
        }

        _previewSession.SetRegionEdgeFades(restored);
        waveformView.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        _audioPlayer.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    private bool TryRedoRegionEdgeFade()
    {
        if (_previewSession is null)
        {
            return false;
        }

        var current = _previewSession.RegionEdgeFades;
        if (!_regionEdgeFadeHistory.TryRedo(current, out var restored))
        {
            return false;
        }

        _previewSession.SetRegionEdgeFades(restored);
        waveformView.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        _audioPlayer.SetRegionEdgeFades(_previewSession.RegionEdgeFades);
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    private void MarkerOptionsPanel_SettingsChanged(object? sender, EventArgs e)
    {
        ApplyMarkerSettings();
        AutosaveCurrentProject();
    }

    /// <summary>マーカーオプションの変更をメモリへ反映する（永続化はプロジェクトへ自動保存）。</summary>
    private void ApplyMarkerSettings()
    {
        waveformView.MarkerGridOverride = _markerSettings.GridOverride;
        if (_previewSession is { } session)
        {
            session.SetCommentRule(_markerSettings.ToCommentRule());
            waveformView.SetMarkers(session.EffectiveMarkers);
        }
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

    /// <summary>WinForms LayoutPlaylistFadeCurveIcon 相当。見出し帯の内側右端・縦中央へアイコンを置く。</summary>
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
        icon.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        // Top+手計算だと描画内容の上寄りや丸めで上に寄って見える。帯と対称な余白になるよう中央揃え。
        icon.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        icon.Stretch = System.Windows.Media.Stretch.Uniform;
        icon.Margin = new Thickness(
            Math.Max(bar.Left, bar.Right - rightInset - iconWidth),
            0,
            0,
            0);
        Panel.SetZIndex(icon, 1);
    }

    /// <summary>
    /// EXPORT ボタン活性を事前検証（Preflight）で常時評価し、結果が変わったときだけログへ出す
    /// （Form1 UpdateExportButtonState 同等）。
    /// </summary>
    private void UpdateExportEnabled()
    {
        var preflight = EvaluateExportPreflight();
        exportButton.IsEnabled = !_exportBusy
            && !_uiInteractionLocks.HasFlag(UiInteractionLock.Export)
            && !_uiInteractionLocks.HasFlag(UiInteractionLock.Load)
            && preflight.CanExport;

        // 読み込み済みのときだけ事前検証の変化をログ（起動直後の空状態は黙る）
        if (_loadedPreview is not null)
        {
            LogExportPreflightIfChanged(preflight);
        }
    }
}
