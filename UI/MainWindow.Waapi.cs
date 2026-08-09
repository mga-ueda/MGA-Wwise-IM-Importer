using System.Windows;
using System.Windows.Threading;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.UI;

/// <summary>WAAPI 接続確認・作成先の選択・Keep Target・アプリ更新確認。</summary>
public partial class MainWindow
{
    private const int WaapiConnectedPollMs = 1500;
    private const int WaapiDisconnectedPollMs = 3000;
    private const int WaapiPollFailThreshold = 3;

    private readonly DispatcherTimer _waapiPollTimer = new() { Interval = TimeSpan.FromMilliseconds(WaapiConnectedPollMs) };
    private WaapiProbeResult? _waapiLastResult;
    private bool _waapiPollBusy;
    private int _waapiPollFailCount;
    private bool _keepTarget;
    private string _keptTargetPath = string.Empty;
    private string _keptTargetProjectFilePath = string.Empty;
    private string _lastKnownWwiseProjectFilePath = string.Empty;
    private string _lastKnownWwiseProjectName = string.Empty;
    private bool _wwiseProjectActivateBusy;

    private void InitializeWaapiEventWiring()
    {
        waapiStatusBar.KeepTargetChanged += WaapiStatusBar_KeepTargetChanged;
        waapiStatusBar.AutoActiveChanged += (_, _) => AutosaveCurrentProject();
        waapiStatusBar.ProjectNameClick += (_, _) => _ = OpenOrFocusKeptWwiseProjectAsync();
        _waapiPollTimer.Tick += async (_, _) => await PollWaapiAsync().ConfigureAwait(true);
    }

    private async Task RunStartupSequenceAsync()
    {
        var lastSessionLoadStarted = false;
        try
        {
            waapiStatusBar.SetPending();
            _waapiLastResult = await WaapiStartupProbe.RunAsync(_waapiSettings).ConfigureAwait(true);
            ApplyWaapiProbeResult(_waapiLastResult, logReport: true);

            if (_keepTarget)
            {
                await TryRestoreKeptTargetAsync(logReport: true).ConfigureAwait(true);
            }

            // WinForms 同様: WAAPI 確認のあと Keep Last Session を復元する
            lastSessionLoadStarted = await RestoreKeepLastSessionAsync().ConfigureAwait(true);
        }
        finally
        {
            // Last Session 読み込みへ渡す場合は、そちらがすりガラス解除を継続する
            if (!_closing && !lastSessionLoadStarted)
            {
                SetUiInteractionLocked(UiInteractionLock.Load, locked: false);
            }
        }

        _waapiPollTimer.Interval = TimeSpan.FromMilliseconds(
            _waapiLastResult?.Ok == true ? WaapiConnectedPollMs : WaapiDisconnectedPollMs);
        _waapiPollTimer.Start();
    }

    private void ApplyWaapiProbeResult(WaapiProbeResult result, bool logReport)
    {
        _waapiLastResult = result;
        waapiStatusBar.SetResult(result);
        if (result.Ok)
        {
            RememberLiveWwiseProject(result);
        }

        if (logReport)
        {
            AppendReport(result.FormatLogReport());
        }

        RefreshWaapiStatusDisplay();
    }

    private void RememberLiveWwiseProject(WaapiProbeResult result)
    {
        if (result.ProjectFilePath.Length > 0)
        {
            _lastKnownWwiseProjectFilePath = result.ProjectFilePath;
        }

        if (result.ProjectName.Length > 0)
        {
            _lastKnownWwiseProjectName = result.ProjectName;
        }
    }

    private void WaapiStatusBar_KeepTargetChanged(object? sender, EventArgs e)
    {
        _keepTarget = waapiStatusBar.KeepTargetChecked;
        if (_keepTarget)
        {
            _keptTargetPath = _waapiLastResult?.SelectedPath ?? _keptTargetPath;
            _keptTargetProjectFilePath = _waapiLastResult?.ProjectFilePath ?? _lastKnownWwiseProjectFilePath;
        }
        else
        {
            _keptTargetPath = string.Empty;
            _keptTargetProjectFilePath = string.Empty;
        }

        PersistKeepTarget();
        RefreshWaapiStatusDisplay();
    }

    private void PersistKeepTarget()
    {
        _projectStore.SaveKeepTarget(_loadedProjectName, _keepTarget, _keptTargetPath, _keptTargetProjectFilePath);
    }

    private string GetDisplayTargetPath() => _keepTarget
        ? _keptTargetPath
        : _waapiLastResult?.SelectedPath ?? string.Empty;

    private void RefreshWaapiStatusDisplay()
    {
        if (_waapiLastResult is not { } result)
        {
            return;
        }

        if (result.Ok)
        {
            waapiStatusBar.UpdateSelection(
                result.WwiseVersion,
                result.ProjectName,
                GetDisplayTargetPath(),
                _keepTarget);
        }
        else if (_keepTarget)
        {
            waapiStatusBar.UpdateDisconnectedKeepTarget(GetKeptWwiseProjectDisplayName(_lastKnownWwiseProjectName), _keptTargetPath);
        }
        else
        {
            waapiStatusBar.UpdateDisconnectedLastProject(
                _lastKnownWwiseProjectName,
                UiStrings.StatusDisconnected,
                projectNameClickable: _lastKnownWwiseProjectFilePath.Length > 0);
        }

        UpdateExportEnabled();
    }

    private string GetKeptWwiseProjectDisplayName(string fallback) =>
        fallback.Length > 0 ? fallback : UiStrings.LabelUnnamedProject;

    private string ResolveWwiseProjectFilePathForLaunch() =>
        _keptTargetProjectFilePath.Length > 0 ? _keptTargetProjectFilePath : _lastKnownWwiseProjectFilePath;

    private async Task OpenOrFocusKeptWwiseProjectAsync()
    {
        if (_wwiseProjectActivateBusy)
        {
            return;
        }

        var path = ResolveWwiseProjectFilePathForLaunch();
        if (path.Length == 0)
        {
            return;
        }

        _wwiseProjectActivateBusy = true;
        try
        {
            var (ok, message) = await WwiseProjectActivator.OpenOrFocusAsync(_waapiSettings, path).ConfigureAwait(true);
            if (!ok && message.Length > 0)
            {
                AppendColoredLine(message);
            }
        }
        finally
        {
            _wwiseProjectActivateBusy = false;
        }
    }

    private async Task TryRestoreKeptTargetAsync(bool logReport)
    {
        var (applied, path, message) = await WaapiSelection.TryRestoreKeptTargetAsync(
                _waapiSettings,
                _keptTargetPath,
                _keptTargetProjectFilePath,
                _waapiLastResult?.ProjectFilePath ?? string.Empty)
            .ConfigureAwait(true);

        if (applied)
        {
            _keptTargetPath = path;
        }

        if (logReport && message.Length > 0)
        {
            AppendColoredLine(message);
        }

        RefreshWaapiStatusDisplay();
    }

    private async Task PollWaapiAsync()
    {
        if (_waapiPollBusy || _closing)
        {
            return;
        }

        _waapiPollBusy = true;
        try
        {
            if (_waapiLastResult is { Ok: true })
            {
                await PollWaapiWhileConnectedAsync().ConfigureAwait(true);
            }
            else
            {
                await PollWaapiWhileDisconnectedAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            _waapiPollBusy = false;
        }
    }

    private async Task PollWaapiWhileConnectedAsync()
    {
        try
        {
            var (path, type) = await WaapiStartupProbe.RefreshSelectionAsync(_waapiSettings).ConfigureAwait(true);
            if (_waapiLastResult is { } previous)
            {
                _waapiLastResult = new WaapiProbeResult
                {
                    Ok = true,
                    WwiseVersion = previous.WwiseVersion,
                    Project = previous.Project,
                    ProjectName = previous.ProjectName,
                    ProjectFilePath = previous.ProjectFilePath,
                    SelectedPath = path,
                    SelectedType = type,
                };
            }

            _waapiPollFailCount = 0;
            RefreshWaapiStatusDisplay();
        }
        catch
        {
            RegisterWaapiPollFailure();
        }
    }

    private void RegisterWaapiPollFailure()
    {
        _waapiPollFailCount++;
        if (_waapiPollFailCount < WaapiPollFailThreshold)
        {
            return;
        }

        _waapiLastResult = new WaapiProbeResult { Ok = false, Message = UiStrings.StatusDisconnected };
        _waapiPollTimer.Interval = TimeSpan.FromMilliseconds(WaapiDisconnectedPollMs);
        RefreshWaapiStatusDisplay();
    }

    private async Task PollWaapiWhileDisconnectedAsync()
    {
        var result = await WaapiStartupProbe.RunAsync(_waapiSettings).ConfigureAwait(true);
        if (result.Ok)
        {
            _waapiPollFailCount = 0;
            _waapiPollTimer.Interval = TimeSpan.FromMilliseconds(WaapiConnectedPollMs);
            ApplyWaapiProbeResult(result, logReport: false);
            if (_keepTarget)
            {
                await TryRestoreKeptTargetAsync(logReport: false).ConfigureAwait(true);
            }
        }
    }

    private async Task CheckForAppUpdateAsync()
    {
        try
        {
            var update = await GitHubUpdateChecker.TryGetNewerReleaseAsync().ConfigureAwait(true);
            if (update is not { } info || string.Equals(info.RemoteSemVer, _appSettings.SkippedUpdateVersion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AppendColoredLine(UiStrings.LogUpdateAvailable(info.RemoteSemVer));
            var result = OwnerCenteredMessageBox.Show(
                this,
                UiStrings.UpdateAvailableMessage(info.RemoteSemVer),
                UiStrings.UpdateAvailableTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                TryOpenUrl(info.ReleaseUrl);
            }
            else
            {
                _appSettings.SaveSkippedUpdateVersion(info.RemoteSemVer);
            }
        }
        catch
        {
            // 更新確認の失敗は無視する。
        }
    }
}
