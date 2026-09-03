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
    private Services.WaapiConnectionService _waapiConnection = null!;
    private WaapiProbeResult? _waapiLastResult;
    private bool _waapiPollBusy;
    private int _waapiPollFailCount;
    private bool _keepTarget;
    private string _keptTargetPath = string.Empty;
    private string _keptTargetProjectFilePath = string.Empty;
    private string _lastKnownWwiseProjectFilePath = string.Empty;
    private string _lastKnownWwiseProjectName = string.Empty;
    private bool _wwiseProjectActivateBusy;
    private bool _yieldedAlwaysOnTopToWwise;

    private void InitializeWaapiEventWiring()
    {
        _waapiConnection = new Services.WaapiConnectionService(_waapiSettings);
        waapiStatusBar.KeepTargetChanged += WaapiStatusBar_KeepTargetChanged;
        waapiStatusBar.AutoActiveChanged += (_, _) =>
        {
            if (_suppressProjectUiEvents)
            {
                return;
            }

            AutosaveCurrentProject();
            ReleaseFocusToWaveform();
        };
        waapiStatusBar.ProjectNameClick += (_, _) => RequestOpenOrFocusWwiseProject();
        Activated += (_, _) => RestoreAlwaysOnTopAfterWwiseFocus();
        _waapiPollTimer.Tick += async (_, _) => await PollWaapiAsync().ConfigureAwait(true);
    }

    private async Task RunStartupSequenceAsync()
    {
        var lastSessionLoadStarted = false;
        try
        {
            waapiStatusBar.SetPending();
            _waapiLastResult = await _waapiConnection.ProbeAsync().ConfigureAwait(true);
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
            _waapiPollFailCount = 0;
            RememberLiveWwiseProject(result);
        }

        RefreshWaapiStatusDisplay();

        if (logReport && !_exportBusy)
        {
            AppendReport(FormatWaapiLogReport(result));
        }
    }

    /// <summary>
    /// </summary>
    private string FormatWaapiLogReport(WaapiProbeResult result)
    {
        if (!result.Ok)
        {
            return result.FormatLogReport();
        }

        var lines = new List<string>
        {
            UiStrings.LogWaapiHeader,
            $"{UiStrings.KeyStatus} {UiStrings.LogStatusOk}",
        };
        if (result.WwiseVersion.Length > 0)
        {
            lines.Add($"{UiStrings.KeyWwise} {result.WwiseVersion}");
        }

        if (result.Project.Length > 0)
        {
            lines.Add($"{UiStrings.KeyProject} {result.Project}");
        }

        var displayPath = GetDisplayTargetPath();
        if (_keepTarget)
        {
            lines.Add(displayPath.Length > 0
                ? UiStrings.LogTargetKeepOn(displayPath)
                : UiStrings.LogTargetKeepUnset);
        }
        else
        {
            lines.Add(displayPath.Length > 0
                ? $"{UiStrings.KeyTarget} {displayPath}"
                : UiStrings.LogTargetNoneSelected);
            if (result.SelectedType.Length > 0)
            {
                lines.Add($"{UiStrings.KeyType} {result.SelectedType}");
            }
        }

        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private void RememberLiveWwiseProject(WaapiProbeResult result)
    {
        var changed = false;
        if (WaapiJson.LooksLikeProjectFilePath(result.ProjectFilePath)
            && !string.Equals(_lastKnownWwiseProjectFilePath, result.ProjectFilePath, StringComparison.OrdinalIgnoreCase))
        {
            _lastKnownWwiseProjectFilePath = result.ProjectFilePath.Trim().Trim('"');
            changed = true;
        }

        if (result.ProjectName.Length > 0
            && !string.Equals(_lastKnownWwiseProjectName, result.ProjectName, StringComparison.Ordinal))
        {
            _lastKnownWwiseProjectName = result.ProjectName;
            changed = true;
        }

        if (_lastKnownWwiseProjectName.Length == 0)
        {
            var derived = DeriveWwiseProjectDisplayName(_lastKnownWwiseProjectFilePath);
            if (derived.Length > 0)
            {
                _lastKnownWwiseProjectName = derived;
                changed = true;
            }
        }

        if (changed)
        {
            PersistLastKnownWwiseProject();
        }
    }

    /// <returns>Keep Target の .wproj から名前／パスを補完したとき true。</returns>
    private bool RestoreLastKnownWwiseProject(ProjectProfile profile)
    {
        _lastKnownWwiseProjectName = profile.LastKnownWwiseProjectName?.Trim() ?? string.Empty;
        _lastKnownWwiseProjectFilePath = profile.LastKnownWwiseProjectFilePath?.Trim() ?? string.Empty;
        if (_lastKnownWwiseProjectFilePath.Length == 0 && _keptTargetProjectFilePath.Length > 0)
        {
            _lastKnownWwiseProjectFilePath = _keptTargetProjectFilePath;
        }

        if (!WaapiJson.LooksLikeProjectFilePath(_lastKnownWwiseProjectFilePath))
        {
            var nearOutput = WwiseProjectActivator.TryFindProjectFileNearDirectory(profile.OutputDirectory);
            if (nearOutput.Length > 0)
            {
                _lastKnownWwiseProjectFilePath = nearOutput;
            }
        }

        if (_lastKnownWwiseProjectName.Length == 0)
        {
            _lastKnownWwiseProjectName = DeriveWwiseProjectDisplayName(
                _lastKnownWwiseProjectFilePath.Length > 0
                    ? _lastKnownWwiseProjectFilePath
                    : _keptTargetProjectFilePath);
        }

        var seeded = !string.Equals(
                profile.LastKnownWwiseProjectName?.Trim() ?? string.Empty,
                _lastKnownWwiseProjectName,
                StringComparison.Ordinal)
            || !string.Equals(
                profile.LastKnownWwiseProjectFilePath?.Trim() ?? string.Empty,
                _lastKnownWwiseProjectFilePath,
                StringComparison.OrdinalIgnoreCase);
        return seeded && (_lastKnownWwiseProjectName.Length > 0 || _lastKnownWwiseProjectFilePath.Length > 0);
    }

    private void WaapiStatusBar_KeepTargetChanged(object? sender, EventArgs e)
    {
        _keepTarget = waapiStatusBar.KeepTargetChecked;
        if (_keepTarget)
        {
            _keptTargetPath = _waapiLastResult?.SelectedPath ?? _keptTargetPath;
            _keptTargetProjectFilePath = _waapiLastResult?.ProjectFilePath ?? _lastKnownWwiseProjectFilePath;
            if (_waapiLastResult is { ProjectName.Length: > 0 } live)
            {
                _lastKnownWwiseProjectName = live.ProjectName;
            }

            if (_keptTargetProjectFilePath.Length > 0)
            {
                _lastKnownWwiseProjectFilePath = _keptTargetProjectFilePath;
            }

            if (_lastKnownWwiseProjectName.Length == 0)
            {
                _lastKnownWwiseProjectName = DeriveWwiseProjectDisplayName(_lastKnownWwiseProjectFilePath);
            }
        }
        else
        {
            _keptTargetPath = string.Empty;
            _keptTargetProjectFilePath = string.Empty;
        }

        PersistKeepTarget();
        PersistLastKnownWwiseProject();
        RefreshWaapiStatusDisplay();
    }

    private void PersistKeepTarget()
    {
        _projectStore.SaveKeepTarget(_loadedProjectName, _keepTarget, _keptTargetPath, _keptTargetProjectFilePath);
    }

    private void PersistLastKnownWwiseProject()
    {
        if (_suppressProjectUiEvents || _closing || string.IsNullOrWhiteSpace(_loadedProjectName))
        {
            return;
        }

        _projectStore.SaveLastKnownWwiseProject(
            _loadedProjectName,
            _lastKnownWwiseProjectName,
            _lastKnownWwiseProjectFilePath);
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

        var rememberedName = ResolveRememberedWwiseProjectDisplayName(allowUnnamed: false);
        var displayName = result.ProjectName.Length > 0 ? result.ProjectName : rememberedName;
        var launchPath = ResolveWwiseProjectFilePathForLaunch();
        var hasLaunchPath = launchPath.Length > 0;
        var clickable = hasLaunchPath && displayName.Length > 0;

        if (result.Ok)
        {
            waapiStatusBar.UpdateSelection(
                result.WwiseVersion,
                displayName,
                GetDisplayTargetPath(),
                _keepTarget,
                projectNameClickable: clickable);
        }
        else if (_keepTarget)
        {
            waapiStatusBar.UpdateDisconnectedKeepTarget(
                ResolveRememberedWwiseProjectDisplayName(allowUnnamed: true),
                _keptTargetPath,
                projectNameClickable: hasLaunchPath);
        }
        else
        {
            waapiStatusBar.UpdateDisconnectedLastProject(
                rememberedName,
                UiStrings.StatusDisconnected,
                projectNameClickable: clickable);
        }

        UpdateExportEnabled();
    }

    private string ResolveRememberedWwiseProjectDisplayName(bool allowUnnamed)
    {
        if (_lastKnownWwiseProjectName.Length > 0)
        {
            return _lastKnownWwiseProjectName;
        }

        var derived = DeriveWwiseProjectDisplayName(
            _keptTargetProjectFilePath.Length > 0
                ? _keptTargetProjectFilePath
                : _lastKnownWwiseProjectFilePath);
        if (derived.Length > 0)
        {
            return derived;
        }

        return allowUnnamed ? UiStrings.LabelUnnamedProject : string.Empty;
    }

    private static string DeriveWwiseProjectDisplayName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFileNameWithoutExtension(filePath.Trim().Trim('"')) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ResolveWwiseProjectFilePathForLaunch()
    {
        if (WaapiJson.LooksLikeProjectFilePath(_keptTargetProjectFilePath))
        {
            return _keptTargetProjectFilePath.Trim().Trim('"');
        }

        if (WaapiJson.LooksLikeProjectFilePath(_lastKnownWwiseProjectFilePath))
        {
            return _lastKnownWwiseProjectFilePath.Trim().Trim('"');
        }

        var live = _waapiLastResult?.ProjectFilePath ?? string.Empty;
        if (WaapiJson.LooksLikeProjectFilePath(live))
        {
            return live.Trim().Trim('"');
        }

        return WwiseProjectActivator.TryFindProjectFileNearDirectory(_projectOutputDirectory);
    }

    private void RequestOpenOrFocusWwiseProject()
    {
        if (_wwiseProjectActivateBusy)
        {
            return;
        }

        var path = ResolveWwiseProjectFilePathForLaunch();
        if (path.Length == 0)
        {
            AppendColoredLine(UiStrings.LogWwiseProjectPathMissing);
            return;
        }

        RememberResolvedLaunchPath(path);
        YieldAlwaysOnTopToWwise();
        WwiseProjectActivator.TryFocusExistingAuthoring(path);
        _ = OpenOrFocusKeptWwiseProjectAsync(path);
    }

    private void RememberResolvedLaunchPath(string path)
    {
        if (string.Equals(_lastKnownWwiseProjectFilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastKnownWwiseProjectFilePath = path;
        if (_lastKnownWwiseProjectName.Length == 0)
        {
            _lastKnownWwiseProjectName = DeriveWwiseProjectDisplayName(path);
        }

        PersistLastKnownWwiseProject();
    }

    private void YieldAlwaysOnTopToWwise()
    {
        if (!Topmost)
        {
            return;
        }

        _yieldedAlwaysOnTopToWwise = true;
        Topmost = false;
    }

    private void RestoreAlwaysOnTopAfterWwiseFocus()
    {
        if (!_yieldedAlwaysOnTopToWwise || !_appSettings.AlwaysOnTop)
        {
            return;
        }

        _yieldedAlwaysOnTopToWwise = false;
        Topmost = true;
    }

    private async Task OpenOrFocusKeptWwiseProjectAsync(string? projectFilePath = null)
    {
        if (_wwiseProjectActivateBusy)
        {
            return;
        }

        var path = projectFilePath is { Length: > 0 }
            ? projectFilePath
            : ResolveWwiseProjectFilePathForLaunch();
        if (path.Length == 0)
        {
            AppendColoredLine(UiStrings.LogWwiseProjectPathMissing);
            return;
        }

        RememberResolvedLaunchPath(path);
        _wwiseProjectActivateBusy = true;
        try
        {
            YieldAlwaysOnTopToWwise();
            var (ok, message) = await WwiseProjectActivator.OpenOrFocusAsync(_waapiSettings, path)
                .ConfigureAwait(true);
            if (ok)
            {
                YieldAlwaysOnTopToWwise();
                WwiseProjectActivator.TryFocusExistingAuthoring(path);
            }

            if (message.Length > 0)
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
            var (path, type) = await _waapiConnection.RefreshSelectionAsync().ConfigureAwait(true);
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
        var result = await _waapiConnection.ProbeAsync().ConfigureAwait(true);
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
            if (_closing || update is null)
            {
                return;
            }

            var remoteSemVer = update.Value.RemoteSemVer;
            var skipped = AppVersion.NormalizeTag(_appSettings.SkippedUpdateVersion);
            if (skipped.Length > 0
                && string.Equals(skipped, remoteSemVer, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AppendReport(
                UiStrings.LogUpdateAvailable(
                    AppVersion.Current,
                    remoteSemVer)
                + Environment.NewLine);

            var answer = OwnerCenteredMessageBox.Show(
                this,
                UiStrings.DialogUpdateAvailableBody(
                    AppVersion.Current,
                    remoteSemVer,
                    update.Value.IsPrerelease),
                UiStrings.DialogUpdateAvailableTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information,
                MessageBoxResult.Yes);

            if (answer == MessageBoxResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(update.Value.ReleaseUrl)
                        {
                            UseShellExecute = true,
                        });
                }
                catch (Exception ex)
                {
                    OwnerCenteredMessageBox.Show(
                        this,
                        ex.Message,
                        UiStrings.DialogOpenGithubFailed,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                _appSettings.SaveSkippedUpdateVersion(remoteSemVer);
            }
        }
        catch
        {
            // オフライン・API 制限などは起動を妨げない。
        }
    }
}
