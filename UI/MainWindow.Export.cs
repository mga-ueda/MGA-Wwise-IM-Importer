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

    private async void ExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_exportBusy || _loadedPreview is null || _previewSession is null)
        {
            return;
        }

        var enabledParts = _previewSession.EffectiveOutputParts
            .Where(p => !_disabledPartNumbers.Contains(p.Number))
            .ToList();

        var preflight = ExportPreflight.Evaluate(
            _projectOutputDirectory,
            _waapiLastResult,
            hasEnabledParts: enabledParts.Count > 0,
            keepTarget: _keepTarget,
            keptTargetPath: _keptTargetPath,
            fallbackProjectFilePath: _keptTargetProjectFilePath);
        AppendReport(preflight.FormatLogMessage());
        if (!preflight.CanExport)
        {
            return;
        }

        _exportBusy = true;
        var generation = ++_exportGeneration;
        SetUiInteractionLocked(UiInteractionLock.Export, locked: true, UiStrings.OverlayExporting);
        StopPlaybackForExport();

        try
        {
            var plan = WwiseMusicPlanBuilder.Build(
                _loadedPreview.SourcePath,
                _loadedPreview.WavInfo.SampleRate,
                enabledParts,
                _previewSession.EffectiveRegions,
                _loadedPreview.Bars,
                _previewSession.EffectiveMarkers,
                _partGroupIds,
                outputDirectory: preflight.OutputDirectory,
                partExitSourceModes: _partExitSourceModes,
                partFadeInSeconds: _partFadeInSeconds,
                partFadeOutSeconds: _partFadeOutSeconds);

            var progress = new Progress<string>(line =>
            {
                if (generation != _exportGeneration)
                {
                    return;
                }

                AppendColoredLine(line);
                _exportOverlay.AppendLog(line);
            });

            var report = await WaapiMusicImporter.ImportAsync(
                    _waapiSettings,
                    _importSettings.WithStreaming(
                        markerOptionsPanel.StreamEnabled,
                        markerOptionsPanel.LookAheadMs,
                        markerOptionsPanel.PrefetchLengthMs),
                    plan,
                    preflight.TargetPath,
                    _loadedPreview.SourcePath,
                    preflight.OutputDirectory,
                    enabledParts,
                    _loadedPreview.WavInfo,
                    _partGroupIds,
                    loudnessPreserveGroupBalance: markerOptionsPanel.LoudnessPreserveGroupBalance,
                    regionEdgeFades: _previewSession.RegionEdgeFades,
                    progress: progress)
                .ConfigureAwait(true);

            if (generation == _exportGeneration)
            {
                AppendReport(report);
            }

            if (waapiStatusBar.AutoActiveChecked)
            {
                await WwiseProjectActivator.BringToForegroundAsync(_waapiSettings).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            AppendColoredLine(UiStrings.ErrExportFailed(ex.Message));
        }
        finally
        {
            SetUiInteractionLocked(UiInteractionLock.Export, locked: false);
            _exportBusy = false;
        }
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
    }

    /// <summary>WAAPI ステータスバーを除いたクライアント領域。</summary>
    private Rect GetBusyGlassCoverBounds()
    {
        var host = rootChromeGrid;
        var width = Math.Max(0, host.ActualWidth);
        var statusHeight = waapiStatusBar.ActualHeight;
        if (statusHeight <= 0)
        {
            statusHeight = waapiStatusBar.DesiredSize.Height;
        }

        var height = host.ActualHeight - statusHeight;
        if (height <= 0)
        {
            height = Math.Max(0, host.ActualHeight);
        }

        return new Rect(0, 0, width, height);
    }

    /// <summary>
    /// 書き出し／読み込み中はコントロールを無効化し、WAAPI ステータスバーを除く全体を
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
                rootChromeGrid.UpdateLayout();
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
        var busy = _uiInteractionLocks.HasFlag(UiInteractionLock.Export)
            || _uiInteractionLocks.HasFlag(UiInteractionLock.Load);
        exportButton.IsEnabled = !busy && _loadedPreview is not null;
        reloadButton.IsEnabled = !busy && _lastInputFiles.Count > 0;
        clearButton.IsEnabled = !busy;
        projectNameComboBox.IsEnabled = !busy;
        transportBar.IsEnabled = !busy;
    }
}
