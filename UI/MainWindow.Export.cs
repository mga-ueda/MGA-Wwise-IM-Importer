using System.Windows;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.UI;

/// <summary>EXPORT ボタン：Preflight → Wwise Music Plan 構築 → WAAPI インポート。</summary>
public partial class MainWindow
{
    private readonly ExportGlassOverlay _exportOverlay = new();
    private bool _exportBusy;
    private int _exportGeneration;
    private int _loadLockCount;
    private string _busyOverlayMessage = UiStrings.OverlayLoading;
    private string _lastLoggedPreflightKey = string.Empty;
    private bool _restoreTopMostAfterMinimize;

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_exportBusy || _loadedPreview is not { } preview)
        {
            return;
        }

        if (GetEffectiveOutputParts().Count == 0)
        {
            return;
        }

        // 事前検証の await 中に再クリック／Ctrl+Shift+E で二重 EXPORT が始まらないよう、
        // 最初の await より前に busy を立てる（各 return 経路は下の finally で解除）。
        _exportBusy = true;
        try
        {
            // 複数波形＋グループ時、-R 等の投影リージョンが古いと除外区間まで書き出され得る。
            // スナップショット直前に共有／リージョンを確定させる。
            EnsureExportSessionRegionsCurrent();

            // クリック時点で接続・プロジェクト・選択・書き出し先を再検証（失敗時は WAV を書き始めない）
            ExportPreflightResult preflight;
            try
            {
                var result = await _waapiConnection.ProbeAsync().ConfigureAwait(true);
                if (!_closing)
                {
                    ApplyWaapiProbeResult(result, logReport: false);
                    await TryRestoreKeptTargetIfEnabledAsync(logReport: true).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                AppendReport(
                    $"{UiStrings.LogExportPreflightHeader}{Environment.NewLine}"
                    + $"{UiStrings.KeyStatus} {UiStrings.LogStatusNg}{Environment.NewLine}"
                    + UiStrings.LogWaapiStateFailed(ex.Message)
                    + Environment.NewLine
                    + Environment.NewLine);
                UpdateExportEnabled();
                ReleaseFocusToWaveform();
                return;
            }

            if (_closing)
            {
                return;
            }

            // 事前検証の await 中に読み込み直し等でプレビューが差し替わっていたら中止する。
            if (!ReferenceEquals(_loadedPreview, preview))
            {
                return;
            }

            preflight = EvaluateExportPreflight();
            UpdateExportEnabled();
            if (!preflight.CanExport)
            {
                AppendReport(preflight.FormatLogMessage());
                _lastLoggedPreflightKey = BuildPreflightLogKey(preflight);
                OwnerCenteredMessageBox.Show(
                    this,
                    preflight.Reason,
                    UiStrings.DialogExportTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                ReleaseFocusToWaveform();
                return;
            }

            var outputDirectory = preflight.OutputDirectory;
            var targetPath = preflight.TargetPath;

            var exportGeneration = _exportGeneration;
            var wwiseMarkers = _previewSession is { } session
                ? session.WwiseMarkers.ToArray()
                : preview.AllowsSessionMarkerEdit
                    ? []
                    : preview.Markers.ToArray();
            var wwiseSnapshot = BuildPlaylistExportSnapshot(preview, wwiseMarkers);
            if (wwiseSnapshot.Parts.Count == 0)
            {
                return;
            }

            StopPlaybackForExport();

            SetUiInteractionLocked(UiInteractionLock.Export, locked: true, UiStrings.OverlayExporting);
            UpdateExportEnabled();

            var exportSucceeded = false;
            string? importErrorMessage = null;
            try
            {
                (exportSucceeded, importErrorMessage) = await RunWwiseImportAsync(
                    preview,
                    wwiseSnapshot,
                    exportGeneration,
                    outputDirectory,
                    targetPath);
            }
            finally
            {
                if (!_closing)
                {
                    _exportBusy = false;
                    SetUiInteractionLocked(UiInteractionLock.Export, locked: false);
                    UpdateExportEnabled();
                    ReleaseFocusToWaveform();
                }
            }

            // エラーはログに加えてダイアログでも通知する（スキップ／キャンセルは対象外）。
            if (!_closing && importErrorMessage is not null)
            {
                OwnerCenteredMessageBox.Show(
                    this,
                    importErrorMessage,
                    UiStrings.DialogWwiseImportFailedTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            if (!_closing && exportSucceeded && waapiStatusBar.AutoActiveChecked)
            {
                try
                {
                    await ApplyAutoActiveAfterExportAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    // async void の Export クリック経路へ例外を漏らすとプロセスが落ちる。
                    if (!_closing)
                    {
                        AppendReport(
                            $"{UiStrings.LogWwiseBringToFrontFailed(ex.Message)}{Environment.NewLine}");
                    }
                }
            }
        }
        finally
        {
            // 事前検証段階での return／例外時に busy が残らないようにする
            // （本編は内側の finally で解除済み。ここは残っている場合のみ）。
            if (_exportBusy)
            {
                _exportBusy = false;
                UpdateExportEnabled();
            }
        }
    }

    /// <summary>Keep Target がオンのときだけ記憶パスを再選択する（Form1 と同じガード）。</summary>
    private async Task TryRestoreKeptTargetIfEnabledAsync(bool logReport)
    {
        if (!_keepTarget || _waapiLastResult is not { Ok: true })
        {
            return;
        }

        await TryRestoreKeptTargetAsync(logReport).ConfigureAwait(true);
    }

    private bool HasEnabledExportParts()
    {
        var parts = GetEffectiveOutputParts();
        return parts.Count > 0
            && parts.Any(part => !_disabledPartNumbers.Contains(part.Number));
    }

    private ExportPreflightResult EvaluateExportPreflight()
    {
        var fallbackProject = _keptTargetProjectFilePath.Trim();
        if (fallbackProject.Length == 0)
        {
            fallbackProject = _lastKnownWwiseProjectFilePath.Trim();
        }

        return ExportPreflight.Evaluate(
            _projectOutputDirectory,
            _waapiLastResult,
            HasEnabledExportParts(),
            keepTarget: _keepTarget,
            keptTargetPath: _keptTargetPath,
            fallbackProjectFilePath: fallbackProject);
    }

    /// <summary>事前検証ログの重複抑止キー（結果が変わったときだけログする）。</summary>
    private static string BuildPreflightLogKey(ExportPreflightResult preflight) =>
        $"{preflight.CanExport}|{preflight.Reason}|{preflight.OutputDirectory}"
        + $"|{preflight.TargetPath}|{preflight.ProjectFilePath}";

    /// <summary>
    /// 事前検証の結果が変わったときだけログへ出す（ポーリングで連打しない）。
    /// Wave 単体モードは条件達成／未達の両方を出す。それ以外は未達時のみ。
    /// EXPORT 中（WWU 直編集のプロジェクトクローズ含む）は誤った NG を出さない。
    /// </summary>
    private void LogExportPreflightIfChanged(ExportPreflightResult preflight)
    {
        if (_exportBusy)
        {
            return;
        }

        var key = BuildPreflightLogKey(preflight);
        if (string.Equals(key, _lastLoggedPreflightKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedPreflightKey = key;

        var waveOnly = _previewSession is { AllowsSessionMarkerEdit: true };
        if (waveOnly || !preflight.CanExport)
        {
            AppendReport(preflight.FormatLogMessage());
        }
    }

    /// <summary>
    /// EXPORT 直前に Wave 単体／複数波形のリージョンを最新マーカー・グループ投影へ揃える。
    /// </summary>
    private void EnsureExportSessionRegionsCurrent()
    {
        if (_previewSession is not { AllowsSessionMarkerEdit: true } session)
        {
            return;
        }

        session.SetDisabledPartNumbers(_disabledPartNumbers);
        if (_loadedPreview is { IsMultiWaveOnly: true })
        {
            session.SetPlaylistGroups(BuildEnabledPartGroupIds());
            ApplyWaveOnlySessionPresentation(session, refreshPlaylists: false);
            return;
        }

        // 単体 Wave もグループ共有マーカーを確定してから書き出す。
        session.SetPlaylistGroups(BuildEnabledPartGroupIds());
        waveformView.SetMarkers(session.EffectiveMarkers);
        waveformView.SetRegions(session.EffectiveRegions);
        waveformView.SetOutputParts(session.EffectiveOutputParts);
    }

    private PlaylistExportSnapshot BuildPlaylistExportSnapshot(
        WaveformPreviewData preview,
        IReadOnlyList<WaveformMarkerMark> markers)
    {
        var parts = BuildProjectedEnabledParts(
            GetEffectiveOutputParts(),
            BuildNamingSourcePath(preview.SourcePath));
        var enabledNumbers = parts.Select(part => part.Number).ToHashSet();
        var groups = _partGroupIds
            .Where(pair => enabledNumbers.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var filteredMarkers = markers
            .Where(marker => parts.Any(part =>
                marker.SampleOffset >= part.StartSampleOffset
                && marker.SampleOffset < part.EndSampleOffset))
            .ToArray();

        return new PlaylistExportSnapshot(
            parts,
            groups,
            filteredMarkers,
            BuildPlaylistNameOverrides(parts),
            BuildExportModes(enabledNumbers, ResolveExitSourceMode),
            BuildExportModes(enabledNumbers, ResolveChangeOccursAtMode),
            BuildExportValues(enabledNumbers, ResolveFadeInSeconds),
            BuildExportValues(enabledNumbers, ResolveFadeOutSeconds),
            BuildExportValues(enabledNumbers, ResolveFadeInCurve),
            BuildExportValues(enabledNumbers, ResolveFadeOutCurve),
            BuildExportValues(enabledNumbers, ResolveGroupFadeSeconds),
            BuildExportValues(enabledNumbers, ResolvePlayPostExit),
            BuildExportValues(enabledNumbers, ResolveAdditiveLayers));
    }

    private static IReadOnlyDictionary<int, T> BuildExportValues<T>(
        IReadOnlySet<int> enabledNumbers,
        Func<int, T> resolver) =>
        Services.ExportValueBuilder.Build(enabledNumbers, resolver);

    private static IReadOnlyDictionary<int, PlaylistExitSourceMode> BuildExportModes(
        IReadOnlySet<int> enabledNumbers,
        Func<int, PlaylistExitSourceMode> resolver) =>
        BuildExportValues(enabledNumbers, resolver);

    private readonly record struct PlaylistExportSnapshot(
        IReadOnlyList<WaveformOutputPart> Parts,
        IReadOnlyDictionary<int, int> PartGroupIds,
        IReadOnlyList<WaveformMarkerMark> Markers,
        IReadOnlyDictionary<int, string> PlaylistNameOverrides,
        IReadOnlyDictionary<int, PlaylistExitSourceMode> PartExitSourceModes,
        IReadOnlyDictionary<int, PlaylistExitSourceMode> PartChangeOccursAtModes,
        IReadOnlyDictionary<int, double> PartFadeInSeconds,
        IReadOnlyDictionary<int, double> PartFadeOutSeconds,
        IReadOnlyDictionary<int, RegionFadeCurveKind> PartFadeInCurves,
        IReadOnlyDictionary<int, RegionFadeCurveKind> PartFadeOutCurves,
        IReadOnlyDictionary<int, double> PartGroupFadeSeconds,
        IReadOnlyDictionary<int, bool> PartPlayPostExit,
        IReadOnlyDictionary<int, bool> PartAdditiveLayers);

    /// <summary>
    /// エクスポート済み WAV を Wwise の選択位置へ Music 構造として流し込む。
    /// キャンセル時はログを残してスキップする。作成先は EXPORT 開始時に固定したパスを使う。
    /// 戻り値: 成否と、エラー時のみダイアログ表示用メッセージ（スキップ／キャンセルは null）。
    /// </summary>
    private async Task<(bool Succeeded, string? ErrorMessage)> RunWwiseImportAsync(
        WaveformPreviewData preview,
        PlaylistExportSnapshot snapshot,
        int exportGeneration,
        string outputDirectory,
        string targetPath)
    {
        void ReportProgress(string message)
        {
            // すりガラス中もログエディタへ残す（AppendReport がオーバーレイへもミラーする）。
            var text = message.EndsWith('\n') || message.EndsWith("\r\n", StringComparison.Ordinal)
                ? message
                : message + Environment.NewLine;
            AppendReport(text);
        }

        if (targetPath.Length == 0)
        {
            AppendReport(
                $"{UiStrings.LogWwiseImportHeader}{Environment.NewLine}"
                + UiStrings.LogImportSkippedNoSelection
                + Environment.NewLine
                + Environment.NewLine);
            return (false, null);
        }

        var importSettings = WwiseImportSettings.Load()
            .WithStreaming(
                markerOptionsPanel.StreamEnabled,
                markerOptionsPanel.LookAheadMs,
                markerOptionsPanel.PrefetchLengthMs);

        WwiseMusicPlan plan;
        try
        {
            ReportProgress(UiStrings.LogBuildingImportPlan);
            var containerNameOverride = preview.IsMultiWaveOnly
                ? WwiseObjectNames.MultiWaveContainerName
                : null;
            plan = WwiseMusicPlanBuilder.Build(
                BuildNamingSourcePath(preview.SourcePath),
                preview.WavInfo.SampleRate,
                snapshot.Parts,
                _previewSession?.EffectiveRegions ?? preview.Regions,
                preview.Bars,
                snapshot.Markers,
                snapshot.PartGroupIds,
                snapshot.PlaylistNameOverrides,
                outputDirectory,
                snapshot.PartExitSourceModes,
                PlaylistExitSourceMode.Immediate,
                snapshot.PartFadeInSeconds,
                snapshot.PartFadeOutSeconds,
                defaultFadeInSeconds: 0,
                defaultFadeOutSeconds: 0,
                snapshot.PartFadeInCurves,
                snapshot.PartFadeOutCurves,
                _appSettings.DefaultPlaylistFadeInCurve,
                _appSettings.DefaultPlaylistFadeOutCurve,
                snapshot.PartGroupFadeSeconds,
                defaultGroupFadeSeconds: 0,
                snapshot.PartChangeOccursAtModes,
                PlaylistExitSourceMode.Immediate,
                snapshot.PartPlayPostExit,
                defaultPlayPostExit: false,
                snapshot.PartAdditiveLayers,
                containerNameOverride);
            ReportProgress(UiStrings.LogPlanReady(plan.Playlists.Count));
            AppendReport(WaapiMusicImporter.FormatPlanSummary(plan) + Environment.NewLine);
            var exportRegions = _previewSession?.EffectiveRegions ?? preview.Regions;
            AppendReport(
                FormatExportRegionSummary(exportRegions, snapshot.Markers) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            AppendReport(
                $"{UiStrings.LogWwiseImportHeader}{Environment.NewLine}"
                + UiStrings.LogImportPlanFailed(ex.Message)
                + Environment.NewLine
                + Environment.NewLine);
            return (false, ex.Message);
        }

        var updateExistingStateGroup = false;
        if (plan.IsMultiPart)
        {
            var stateGroupPath = importSettings.ResolveStateGroupPath(plan.ContainerName);
            bool exists;
            try
            {
                ReportProgress(UiStrings.LogCheckingStateGroup);
                exists = await WaapiObjectUtil.ExistsAsync(_waapiSettings, stateGroupPath);
                ReportProgress(exists
                    ? UiStrings.LogStateGroupExistingFound
                    : UiStrings.LogStateGroupAvailable);
            }
            catch (Exception ex)
            {
                AppendReport(
                    $"{UiStrings.LogWwiseImportHeader}{Environment.NewLine}"
                    + $"{UiStrings.KeyStatus} {UiStrings.LogStatusNg}{Environment.NewLine}"
                    + UiStrings.LogStateGroupCheckFailed(ex.Message)
                    + Environment.NewLine
                    + Environment.NewLine);
                return (false, ex.Message);
            }

            if (_closing || exportGeneration != _exportGeneration)
            {
                return (false, null);
            }

            // 既存 State Group は削除せず、object.set の merge で同一オブジェクトを更新する。
            updateExistingStateGroup = exists;
        }

        if (exportGeneration != _exportGeneration)
        {
            return (false, null);
        }

        try
        {
            // 進行ログは Progress → AppendReport でエディタ／オーバーレイへ逐次出す。
            // 完了後にまとめて再出力すると二重になるため、戻り値の全文は捨てる。
            var progress = new Progress<string>(ReportProgress);
            _ = await Task.Run(() => WaapiMusicImporter.ImportAsync(
                _waapiSettings,
                importSettings,
                plan,
                targetPath,
                preview.SourcePath,
                outputDirectory,
                snapshot.Parts,
                preview.WavInfo,
                snapshot.PartGroupIds,
                markerOptionsPanel.LoudnessPreserveGroupBalance,
                updateExistingStateGroup,
                _previewSession?.RegionEdgeFades,
                progress));
            return (!_closing && exportGeneration == _exportGeneration, null);
        }
        catch (Exception ex)
        {
            if (!_closing)
            {
                AppendReport(
                    $"{UiStrings.LogWwiseImportHeader}{Environment.NewLine}"
                    + $"{UiStrings.KeyStatus} {UiStrings.LogStatusNg}{Environment.NewLine}"
                    + $"{UiStrings.KeyMessage} {ex.Message}{Environment.NewLine}{Environment.NewLine}");
            }

            return (false, _closing ? null : ex.Message);
        }
    }

    /// <summary>
    /// EXPORT 成功後: Wwise を前面化。Always on Top なら、前面化のあとにこのアプリを最小化する。
    /// </summary>
    private async Task ApplyAutoActiveAfterExportAsync()
    {
        // フェード中のすりガラスと最小化が重なると描画経路で例外になり得るため、先に消す。
        _exportOverlay.HideOverlay();

        var shouldMinimize = topMostCheckBox.IsChecked == true || Topmost;

        // 先に Wwise を前面化し、その後で最小化（Minimize → WAAPI の順序を避ける）。
        var (_, message) = await WwiseProjectActivator.BringToForegroundAsync(_waapiSettings)
            .ConfigureAwait(true);

        if (_closing)
        {
            return;
        }

        if (message.Length > 0)
        {
            try
            {
                AppendReport(message + Environment.NewLine);
            }
            catch
            {
                // 最小化直後などで描画できない場合は無視する。
            }
        }

        if (!shouldMinimize || _closing)
        {
            return;
        }

        // TopMost のまま Minimize すると前面固定の影響で挙動が不安定なことがある。
        // 復帰時は RestoreTopMostAfterMinimizeIfNeeded で戻す。
        Topmost = false;
        _restoreTopMostAfterMinimize = true;
        if (WindowState != WindowState.Minimized)
        {
            WindowState = WindowState.Minimized;
        }
    }

    /// <summary>
    /// Auto Active で最小化時に外した TopMost を、タスクバーから復帰したときに戻す。
    /// </summary>
    private void RestoreTopMostAfterMinimizeIfNeeded()
    {
        if (!_restoreTopMostAfterMinimize
            || WindowState == WindowState.Minimized
            || topMostCheckBox.IsChecked != true
            || Topmost)
        {
            return;
        }

        _restoreTopMostAfterMinimize = false;
        Topmost = true;
    }

    private static string FormatExportRegionSummary(
        IReadOnlyList<WaveformRegionMark> regions,
        IReadOnlyList<WaveformMarkerMark> markers)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(UiStrings.LogExportRegionHeader);
        var excluded = 0;
        var included = 0;
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (region.IsExcluded)
            {
                excluded++;
                sb.AppendLine(
                    UiStrings.LogExportRegionExcluded(
                        i + 1,
                        region.StartSampleOffset,
                        region.EndSampleOffset));
                continue;
            }

            included++;
            var suffix = string.IsNullOrEmpty(region.NameSuffix)
                ? "-"
                : region.NameSuffix;
            sb.AppendLine(
                UiStrings.LogExportRegionIncluded(
                    i + 1,
                    suffix,
                    region.StartSampleOffset,
                    region.EndSampleOffset));
        }

        sb.AppendLine(UiStrings.LogExportRegionTotals(included, excluded));
        if (markers.Count > 0)
        {
            sb.AppendLine(UiStrings.LogExportMarkerHeader(markers.Count));
            foreach (var marker in markers.OrderBy(m => m.SampleOffset))
            {
                sb.AppendLine(
                    UiStrings.LogExportMarkerLine(
                        marker.SampleOffset,
                        marker.Comment,
                        marker.IsFromWaveEmbedded));
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 複数の理由が重なっても、最後のロックが外れるまでショートカットは再開しない。
    /// Export / Load 時はすりガラスオーバーレイも更新する。
    /// </summary>
    private void SetUiInteractionLocked(
        UiInteractionLock reason,
        bool locked,
        string? overlayMessage = null)
    {
        var messageChanged = false;
        if (locked
            && reason is UiInteractionLock.Export or UiInteractionLock.Load
            && !string.IsNullOrWhiteSpace(overlayMessage))
        {
            var trimmed = overlayMessage.Trim();
            if (!string.Equals(_busyOverlayMessage, trimmed, StringComparison.Ordinal))
            {
                _busyOverlayMessage = trimmed;
                messageChanged = true;
            }
        }

        var wasBusy = IsExportOrLoadBusy;
        var next = locked
            ? _uiInteractionLocks | reason
            : _uiInteractionLocks & ~reason;
        if (next == _uiInteractionLocks)
        {
            // ロック継続中のメッセージ差し替え（Starting → Loading Last Session など）。
            if (messageChanged && next.HasFlag(reason))
            {
                UpdateBusyGlassOverlay();
            }

            return;
        }

        _uiInteractionLocks = next;
        EndActiveTransportShortcutFeedback();
        _resumePlaybackAfterBackwardSeek = false;

        UpdateBusyGlassOverlay();
        RefreshUiInteractionEnabled();

        // すりガラス解除後、無効化でログ等へ逃げたフォーカスを波形へ戻す。
        if (wasBusy && !IsExportOrLoadBusy)
        {
            ReleaseFocusToWaveform(forceTextBoxRelease: true);
        }
    }

    /// <summary>書き出し／読み込みロック中（すりガラス表示対象）。</summary>
    private bool IsExportOrLoadBusy =>
        _uiInteractionLocks.HasFlag(UiInteractionLock.Export)
        || _uiInteractionLocks.HasFlag(UiInteractionLock.Load);

    /// <summary>クライアント領域全体（WAAPI ステータスバー含む）。</summary>
    private Rect GetBusyGlassCoverBounds()
    {
        var host = rootChromeGrid;
        return new Rect(
            0,
            0,
            Math.Max(0, host.ActualWidth),
            Math.Max(0, host.ActualHeight));
    }

    /// <summary>
    /// 書き出し／読み込み中はコントロールを無効化し、クライアント全体（ステータスバー含む）を
    /// すりガラスで覆ってマウス操作を遮断する。解除は短いフェードで行う。
    /// </summary>
    private void UpdateBusyGlassOverlay()
    {
        string? message = null;
        if (_uiInteractionLocks.HasFlag(UiInteractionLock.Export))
        {
            message = UiStrings.OverlayExporting;
        }
        else if (_uiInteractionLocks.HasFlag(UiInteractionLock.Load))
        {
            message = _busyOverlayMessage;
        }

        if (message is not null)
        {
            if (_exportOverlay.IsShowingBusy)
            {
                _exportOverlay.SetMessage(message);
                _exportOverlay.SyncBounds(GetBusyGlassCoverBounds());
            }
            else
            {
                // フロスト取り込み前に右余白を確定（ボタンは畳まない）
                rootChromeGrid.UpdateLayout();
                PositionLogButtons();
                rootDockPanel.UpdateLayout();
                _exportOverlay.ShowOverlay(
                    rootChromeGrid,
                    rootDockPanel,
                    GetBusyGlassCoverBounds(),
                    message);
            }

            return;
        }

        _exportOverlay.BeginFadeOut();
    }

    private void SyncBusyGlassOverlayBounds()
    {
        if (!_exportOverlay.IsShowingBusy)
        {
            return;
        }

        _exportOverlay.SyncBounds(GetBusyGlassCoverBounds());
    }

    private void RefreshUiInteractionEnabled()
    {
        var busy = IsExportOrLoadBusy;
        UpdateExportEnabled();
        reloadButton.IsEnabled = !busy && _lastInputFiles.Count > 0;
        clearButton.IsEnabled = !busy;
        projectNameComboBox.IsEnabled = !busy;
        transportBar.IsEnabled = !busy;
        SyncLogButtonsForBusy(busy);
    }
}
