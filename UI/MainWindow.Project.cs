using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MgaWwiseIMImporter.UI;

/// <summary>プロジェクトバー（プロファイル切替・作成・削除・出力先・言語／Tips／設定）。</summary>
public partial class MainWindow
{
    private void ProjectFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = UiStrings.SelectOutputFolderTitle,
                InitialDirectory = Directory.Exists(_projectOutputDirectory) ? _projectOutputDirectory : string.Empty,
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _projectOutputDirectory = dialog.FolderName;
            projectOutputPathTextBox.Text = _projectOutputDirectory;
            AutosaveCurrentProject();
        }
        catch (Exception ex)
        {
            AppendColoredLine(UiStrings.ErrSelectFolderFailed(ex.Message));
        }
    }

    private void ProjectDeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_projectStore.Names.Count <= 1)
        {
            OwnerCenteredMessageBox.Show(
                this,
                UiStrings.ErrProjectDeleteLastOne,
                UiStrings.LabelDeleteProjectTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var result = OwnerCenteredMessageBox.Show(
            this,
            UiStrings.ConfirmDeleteProject(_loadedProjectName),
            UiStrings.LabelDeleteProjectTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var next = _projectStore.Delete(_loadedProjectName);
            ApplyProjectProfile(next, applyLastSession: true);
            RefreshProjectComboItems(_loadedProjectName);
        }
        catch (InvalidOperationException ex)
        {
            AppendColoredLine(ex.Message);
        }
    }

    private void KeepLastSessionCheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents)
        {
            return;
        }

        AutosaveCurrentProject();
    }

    private void TopMostCheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents)
        {
            return;
        }

        var enabled = topMostCheckBox.IsChecked == true;
        Topmost = enabled;
        _appSettings.SaveAlwaysOnTop(enabled);
    }

    private void DetailedLogCheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents)
        {
            return;
        }

        DeveloperSettings.SaveDetailedPlaybackLog(detailedLogCheckBox.IsChecked == true);
        _developerSettings = DeveloperSettings.Load();
    }

    private void CompactFileNumbersCheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents || _populatingPlaylistChoices)
        {
            return;
        }

        if (_loadedPreview is not null)
        {
            UpdatePlaylistDisplayNames(GetEffectiveOutputParts());
        }

        AutosaveCurrentProject();
        UpdateExportEnabled();
    }

    private void LanguageFlagButton_Click(object? sender, RoutedEventArgs e)
    {
        var next = UiStrings.IsJapanese ? UiLanguage.English : UiLanguage.Japanese;
        UiStrings.SetLanguage(next);
        _appSettings.SaveUiLanguage(next);
        ReleaseFocusToWaveform();
    }

    private void TipsToggleButton_Click(object? sender, RoutedEventArgs e)
    {
        var enabled = !_appSettings.ShowTips;
        _appSettings.SaveShowTips(enabled);
        TipService.Enabled = enabled;
        tipsToggleButton.Checked = enabled;
        ReleaseFocusToWaveform();
    }

    private void SettingsGearButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new AudioSettingsWindow(
            _appSettings.ToAudioOutputSettings(),
            _appSettings.DefaultWaveformFadeInCurve,
            _appSettings.DefaultWaveformFadeOutCurve,
            _appSettings.DefaultPlaylistFadeInCurve,
            _appSettings.DefaultPlaylistFadeOutCurve,
            _appSettings.ToExpectedWaveformFormat())
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _appSettings.SaveAudioOutput(dialog.SelectedSettings.Api, dialog.SelectedSettings.DeviceId);
        _appSettings.SaveDefaultFadeCurves(
            dialog.WaveformFadeInCurve,
            dialog.WaveformFadeOutCurve,
            dialog.PlaylistFadeInCurve,
            dialog.PlaylistFadeOutCurve);
        _appSettings.SaveExpectedWaveformFormat(dialog.SelectedExpectedFormat);
        _audioPlayer.ApplyOutputSettings(_appSettings.ToAudioOutputSettings());
        waveformView.SetExpectedWaveformFormat(_appSettings.ToExpectedWaveformFormat());
    }

    private void ProjectNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressProjectUiEvents || projectNameComboBox.SelectedItem is not string name)
        {
            return;
        }

        if (string.Equals(name, ProjectSettingsStore.NewProjectMenuItem, StringComparison.Ordinal))
        {
            BeginCreateNewProject();
            return;
        }

        if (string.Equals(name, _loadedProjectName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AutosaveCurrentProject();
        try
        {
            _projectStore.SetActive(name);
            ApplyProjectProfile(_projectStore.GetActive(), applyLastSession: true);
        }
        catch (InvalidOperationException ex)
        {
            AppendColoredLine(ex.Message);
        }
    }

    private void BeginCreateNewProject()
    {
        _creatingNewProject = true;
        var suggested = _projectStore.SuggestNewProjectName();
        _suppressProjectUiEvents = true;
        try
        {
            projectNameComboBox.Text = suggested;
        }
        finally
        {
            _suppressProjectUiEvents = false;
        }

        projectNameComboBox.Focus();
    }

    private void ProjectNameComboBox_LostFocus(object sender, RoutedEventArgs e) => CommitProjectNameEdit();

    private void ProjectNameComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitProjectNameEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _creatingNewProject = false;
            RefreshProjectComboItems(_loadedProjectName);
            ReleaseFocusToWaveform();
            e.Handled = true;
        }
    }

    private void CommitProjectNameEdit()
    {
        if (_suppressProjectUiEvents)
        {
            return;
        }

        var text = (projectNameComboBox.Text ?? string.Empty).Trim();
        if (text.Length == 0
            || string.Equals(text, ProjectSettingsStore.NewProjectMenuItem, StringComparison.OrdinalIgnoreCase))
        {
            _creatingNewProject = false;
            RefreshProjectComboItems(_loadedProjectName);
            return;
        }

        if (!_creatingNewProject && string.Equals(text, _loadedProjectName, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var profile = _projectStore.GetActive();
            CaptureProfileFromUi(profile);
            var savedName = _projectStore.SaveProfile(_loadedProjectName, text, profile, _creatingNewProject);
            _creatingNewProject = false;
            ApplyProjectProfile(_projectStore.GetActive(), applyLastSession: savedName != _loadedProjectName);
            RefreshProjectComboItems(savedName);
        }
        catch (InvalidOperationException ex)
        {
            AppendColoredLine(ex.Message);
            RefreshProjectComboItems(_loadedProjectName);
        }
    }

    private void ProjectOutputPathTextBox_GotFocus(object? sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(() => ReleaseFocusToWaveform());

    private void RefreshProjectComboItems(string? selectName)
    {
        _suppressProjectUiEvents = true;
        try
        {
            projectNameComboBox.Items.Clear();
            foreach (var name in _projectStore.Names)
            {
                projectNameComboBox.Items.Add(name);
            }

            projectNameComboBox.Items.Add(ProjectSettingsStore.NewProjectMenuItem);
            projectNameComboBox.SelectedItem = selectName is not null && _projectStore.ContainsName(selectName)
                ? selectName
                : _projectStore.ActiveName;
            projectNameComboBox.Text = selectName ?? _projectStore.ActiveName;
        }
        finally
        {
            _suppressProjectUiEvents = false;
        }
    }

    /// <summary>プロジェクト切替・削除・起動時に、指定プロファイルを UI 全体へ反映する。</summary>
    private void ApplyProjectProfile(ProjectProfile profile, bool applyLastSession)
    {
        _suppressProjectUiEvents = true;
        try
        {
            _loadedProjectName = profile.Name;
            _projectOutputDirectory = profile.OutputDirectory;
            projectOutputPathTextBox.Text = _projectOutputDirectory;
            keepLastSessionCheckBox.IsChecked = profile.KeepLastSession;
            compactFileNumbersCheckBox.IsChecked = profile.CompactFileNumbers;
            _lastWavePath = profile.LastWavePath?.Trim() ?? string.Empty;
            _lastWavePaths = ResolveStoredLastWavePaths(profile.LastWavePath, profile.LastWavePaths);

            SelectFadeRadio(FadeInRadios, profile.FadeInSeconds);
            SelectFadeRadio(TransitionTimeRadios, profile.FadeOutSeconds);
            SelectExitSourceRadio(ExitSourceRadios, profile.ExitSourceAt);
            SelectExitSourceRadio(ChangeOccursRadios, profile.ExitSourceAt);
            playMinusECheckBox.IsChecked = profile.PlayPostExit;
            additiveLayersCheckBox.IsChecked = false;

            profile.CopyMarkerInto(_markerSettings);
            markerOptionsPanel.Bind(_markerSettings);
            ApplyMarkerSettings();
            markerOptionsPanel.BindStreaming(profile.StreamEnabled, profile.LookAheadMs, profile.PrefetchLengthMs);
            markerOptionsPanel.BindLoudness(profile.LoudnessPreserveGroupBalance);
            markerOptionsPanel.BindMoreOptions(profile.MoreOptionsExpanded);

            _keepTarget = profile.KeepTarget;
            _keptTargetPath = profile.KeptTargetPath;
            _keptTargetProjectFilePath = profile.KeptTargetProjectFilePath;
            waapiStatusBar.KeepTargetChecked = _keepTarget;
            waapiStatusBar.AutoActiveChecked = profile.AutoActive;
        }
        finally
        {
            _suppressProjectUiEvents = false;
        }

        exportButton.IsEnabled = false;
        reloadButton.IsEnabled = false;
        ClearWaveformState();

        if (applyLastSession && profile.KeepLastSession)
        {
            _ = RestoreKeepLastSessionAsync();
        }
    }

    /// <summary>現在の UI 状態を渡されたプロファイルへ書き戻す。</summary>
    private void CaptureProfileFromUi(ProjectProfile profile)
    {
        profile.OutputDirectory = _projectOutputDirectory;
        profile.KeepLastSession = keepLastSessionCheckBox.IsChecked == true;
        profile.CompactFileNumbers = compactFileNumbersCheckBox.IsChecked == true;
        profile.LastWavePath = _lastWavePath?.Trim() ?? string.Empty;
        profile.LastWavePaths = _lastWavePaths.Count > 1
            ? LastWaveSessionState.JoinWavePaths(_lastWavePaths)
            : string.Empty;
        profile.FadeInSeconds = ResolveCheckedTag(FadeInRadios) ?? profile.FadeInSeconds;
        profile.FadeOutSeconds = ResolveCheckedTag(TransitionTimeRadios) ?? profile.FadeOutSeconds;
        profile.ExitSourceAt = ResolveCheckedExitSource(ExitSourceRadios) ?? profile.ExitSourceAt;
        profile.PlayPostExit = playMinusECheckBox.IsChecked == true;
        profile.StreamEnabled = markerOptionsPanel.StreamEnabled;
        profile.LookAheadMs = markerOptionsPanel.LookAheadMs;
        profile.PrefetchLengthMs = markerOptionsPanel.PrefetchLengthMs;
        profile.LoudnessPreserveGroupBalance = markerOptionsPanel.LoudnessPreserveGroupBalance;
        profile.MoreOptionsExpanded = markerOptionsPanel.MoreOptionsExpanded;
        profile.CopyMarkerFrom(_markerSettings);
        profile.KeepTarget = _keepTarget;
        profile.KeptTargetPath = _keptTargetPath;
        profile.KeptTargetProjectFilePath = _keptTargetProjectFilePath;
        profile.AutoActive = waapiStatusBar.AutoActiveChecked;
    }

    /// <summary>ロード済みの波形が無くても呼べる、プロジェクト単位の作業設定オートセーブ。</summary>
    private void AutosaveCurrentProject()
    {
        if (_suppressProjectUiEvents || _closing)
        {
            return;
        }

        try
        {
            var profile = _projectStore.GetActive();
            CaptureProfileFromUi(profile);
            _projectStore.SaveProfile(_loadedProjectName, _loadedProjectName, profile, creatingNew: false);
            SaveLastWaveSessionIfLoaded();
        }
        catch
        {
            // オートセーブ失敗は作業を止めない。
        }
    }
}
