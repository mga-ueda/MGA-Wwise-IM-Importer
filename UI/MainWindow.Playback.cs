using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

/// <summary>再生・トランスポート・メトロノーム・ショートカット。</summary>
public partial class MainWindow
{
    private readonly WaveAudioPlayer _audioPlayer = new();
    private readonly MetronomePlayer? _metronomePlayer = MetronomePlayer.TryCreate();
    private readonly DispatcherTimer _playheadTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private double? _lastPlaybackStartProgress;
    private bool _resumePlaybackAfterBackwardSeek;
    private TransportCommand? _activeTransportShortcutCommand;
    private Key _activeTransportShortcutKey = Key.None;

    private void InitializePlaybackEventWiring()
    {
        _audioPlayer.ApplyOutputSettings(_appSettings.ToAudioOutputSettings());
        ApplyMetronomeVolumeFromSettings();
        _audioPlayer.PlaybackEnded += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            OnPlaybackEndedForPlaylistUi();
            UpdateTransportPlaybackState();
        });
        // Diagnostic は ASIO コールバック等の非 UI スレッドから来る。IsChecked を触らない。
        _audioPlayer.Diagnostic += (_, message) =>
        {
            if (!_developerSettings.DetailedPlaybackLog)
            {
                return;
            }

            Dispatcher.BeginInvoke(() => AppendColoredLine(message));
        };

        InstallMetronomeClicks();
        projectSpectrumView.Player = _audioPlayer;

        transportBar.MetronomeInvoked += (_, _) => TryToggleMetronome();
        _playheadTimer.Tick += (_, _) => OnPlayheadTick();
        _playheadTimer.Start();
        InitializePlaylistPlaybackTimers();
    }

    private void InstallMetronomeClicks()
    {
        if (_metronomePlayer is null)
        {
            return;
        }

        _audioPlayer.SetMetronomeClicks(_metronomePlayer.HighSamples, _metronomePlayer.LowSamples, _metronomePlayer.SampleRate);
    }

    private void ApplyMetronomeVolumeFromSettings()
    {
        var volume = AppSettings.NormalizeMetronomeVolume(_appSettings.MetronomeVolume);
        _appSettings.MetronomeVolume = volume;
        if (_metronomePlayer is not null)
        {
            _metronomePlayer.Volume = volume;
        }

        _audioPlayer.SetMetronomeVolume(volume);
    }

    private void TransportBar_CommandInvoked(object? sender, TransportCommand command)
    {
        if (_uiInteractionLocks != UiInteractionLock.None)
        {
            return;
        }

        var key = command switch
        {
            TransportCommand.TogglePlayback => ResolveTogglePlaybackShortcutKey(),
            TransportCommand.JumpToBar => Key.G,
            TransportCommand.GoToStart => Key.Home, // Ctrl は修飾で表現
            TransportCommand.PreviousPlaylist => Key.Left,
            TransportCommand.PreviousBar => Key.Home,
            TransportCommand.PreviousPage => Key.PageUp,
            TransportCommand.NextPage => Key.PageDown,
            TransportCommand.NextBar => Key.End,
            TransportCommand.NextPlaylist => Key.Right,
            TransportCommand.GoToEnd => Key.End,
            TransportCommand.TimeZoomIn => Key.Up,
            TransportCommand.TimeZoomOut => Key.Down,
            TransportCommand.TimeZoomMax => Key.Up,
            TransportCommand.TimeZoomReset => Key.Down,
            TransportCommand.AmpZoomIn => Key.Up,
            TransportCommand.AmpZoomOut => Key.Down,
            TransportCommand.AmpZoomMax => Key.Up,
            TransportCommand.AmpZoomReset => Key.Down,
            TransportCommand.CycleWaveformHeight => Key.Z,
            _ => Key.None,
        };

        var modifiers = command switch
        {
            TransportCommand.GoToStart or TransportCommand.GoToEnd
                or TransportCommand.PreviousPlaylist or TransportCommand.NextPlaylist
                or TransportCommand.TimeZoomMax or TransportCommand.TimeZoomReset => ModifierKeys.Control,
            TransportCommand.AmpZoomIn or TransportCommand.AmpZoomOut => ModifierKeys.Shift,
            TransportCommand.AmpZoomMax or TransportCommand.AmpZoomReset => ModifierKeys.Control | ModifierKeys.Shift,
            TransportCommand.TogglePlayback when Keyboard.Modifiers.HasFlag(ModifierKeys.Control) => ModifierKeys.Control,
            TransportCommand.TogglePlayback when Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) => ModifierKeys.Alt,
            _ => ModifierKeys.None,
        };

        if (key != Key.None)
        {
            TryProcessWaveformShortcut(key, modifiers, showUiFeedback: false);
        }
        else
        {
            ExecuteTransportCommand(command);
            transportBar.PulseCommandFeedback(command);
        }

        ReleaseFocusToWaveform();

        if (_resumePlaybackAfterBackwardSeek && !transportBar.IsCommandHeld)
        {
            ResumePlaybackAfterBackwardSeek();
        }

        UpdateTransportPlaybackState();
    }

    private static Key ResolveTogglePlaybackShortcutKey()
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            return Key.Enter;
        }

        return Key.Space;
    }

    private void ExecuteTransportCommand(TransportCommand command)
    {
        switch (command)
        {
            case TransportCommand.TogglePlayback:
                TogglePlayback();
                break;
            case TransportCommand.JumpToBar:
                ShowBarJumpDialog();
                break;
            case TransportCommand.GoToStart:
                SeekPlayback(0);
                break;
            case TransportCommand.GoToEnd:
                SeekPlayback(1);
                break;
            case TransportCommand.PreviousBar:
                waveformView.SeekToPreviousBar();
                break;
            case TransportCommand.NextBar:
                waveformView.SeekToNextBar();
                break;
            case TransportCommand.PreviousPage:
                waveformView.SeekToPreviousPage();
                break;
            case TransportCommand.NextPage:
                waveformView.SeekToNextPage();
                break;
            case TransportCommand.PreviousPlaylist:
                waveformView.SeekToPreviousPlaylist();
                break;
            case TransportCommand.NextPlaylist:
                waveformView.SeekToNextPlaylist();
                break;
            case TransportCommand.TimeZoomIn:
                waveformView.ZoomTimeIn();
                break;
            case TransportCommand.TimeZoomOut:
                waveformView.ZoomTimeOut();
                break;
            case TransportCommand.TimeZoomMax:
                waveformView.ZoomTimeToMax();
                break;
            case TransportCommand.TimeZoomReset:
                waveformView.ResetTimeZoom();
                break;
            case TransportCommand.AmpZoomIn:
                waveformView.ZoomAmpIn();
                break;
            case TransportCommand.AmpZoomOut:
                waveformView.ZoomAmpOut();
                break;
            case TransportCommand.AmpZoomMax:
                waveformView.ZoomAmpToMax();
                break;
            case TransportCommand.AmpZoomReset:
                waveformView.ResetAmpZoom();
                break;
            case TransportCommand.CycleWaveformHeight:
                TryCycleWaveformHeightScale();
                break;
        }
    }

    private void EndActiveTransportShortcutFeedback()
    {
        if (_activeTransportShortcutCommand is { } command)
        {
            transportBar.EndShortcutFeedback(command);
        }

        _activeTransportShortcutCommand = null;
        _activeTransportShortcutKey = Key.None;

        if (_resumePlaybackAfterBackwardSeek && !transportBar.IsCommandHeld)
        {
            ResumePlaybackAfterBackwardSeek();
        }
    }

    private void TogglePlayback()
    {
        if (_loadedPreview is null || !_audioPlayer.HasSource)
        {
            return;
        }

        var wasPlaying = _audioPlayer.IsPlaying;
        var hadPendingPlaylistTransition = _pendingPlaylistTransitionGeneration != 0;
        if (!_automaticPlaylistPlayback)
        {
            SetManualPlaylistHighlight(_smoothProgress > 0 ? _smoothProgress : _audioPlayer.Progress);
        }

        if (_audioPlayer.IsPlaying && hadPendingPlaylistTransition)
        {
            OnPlayheadTick();
        }

        if (!wasPlaying)
        {
            ApplyPlayExitLayerForCurrentPlayback();
        }

        if (_audioPlayer.IsPlaying)
        {
            _audioPlayer.Pause();
        }
        else
        {
            _audioPlayer.Play();
        }

        if (_audioPlayer.IsPlaying)
        {
            AnchorPlayhead(hadPendingPlaylistTransition ? _smoothProgress : _audioPlayer.Progress);
            _audioPlayer.ArmLoopAtProgress(_smoothProgress);
            _playheadTimer.Start();
        }
        else
        {
            _playheadTimer.Stop();
            AnchorPlayhead(hadPendingPlaylistTransition ? _smoothProgress : _audioPlayer.Progress);
        }

        OnPlayheadTick();
        if (!wasPlaying && _audioPlayer.IsPlaying)
        {
            _lastPlaybackStartProgress = _smoothProgress;
            StartPlaylistTransitionGlow();
        }
        else
        {
            ApplyPlaylistButtonColors();
        }

        UpdateTransportPlaybackState();
    }

    private void SeekPlayback(double progress, bool ensureVisible = false)
    {
        var clamped = Math.Clamp(progress, 0d, 1d);
        _audioPlayer.Seek(clamped);
        AnchorPlayhead(clamped);
        if (!_automaticPlaylistPlayback && _audioPlayer.IsPlaying)
        {
            SetManualPlaylistHighlight(clamped);
        }

        waveformView.SetPlayhead(clamped, recordTrail: _audioPlayer.IsPlaying, ensureVisible: ensureVisible);
    }

    private void StartPlaybackAt(double progress)
    {
        SeekPlayback(progress);
        _lastPlaybackStartProgress = progress;
        if (!_audioPlayer.IsPlaying)
        {
            _audioPlayer.Play();
        }

        UpdateTransportPlaybackState();
    }

    private void RestartFromLastPlaybackStart()
    {
        if (!_audioPlayer.HasSource)
        {
            return;
        }

        StartPlaybackAt(_lastPlaybackStartProgress ?? _audioPlayer.Progress);
    }

    private void StartPrerollPlayback(double prerollSeconds = 3d)
    {
        if (!_audioPlayer.HasSource)
        {
            return;
        }

        var durationSec = _audioPlayer.Duration.TotalSeconds;
        if (durationSec <= 0)
        {
            return;
        }

        var start = Math.Max(0d, _audioPlayer.Progress - (prerollSeconds / durationSec));
        StartPlaybackAt(start);
    }

    private void PauseForBackwardSeekHold()
    {
        if (!_audioPlayer.HasSource || !_audioPlayer.IsPlaying || _resumePlaybackAfterBackwardSeek)
        {
            return;
        }

        _audioPlayer.Pause();
        UpdateTransportPlaybackState();
        SeekPlayback(_audioPlayer.Progress);
        _resumePlaybackAfterBackwardSeek = true;
    }

    private void ResumePlaybackAfterBackwardSeek()
    {
        if (!_resumePlaybackAfterBackwardSeek)
        {
            return;
        }

        _resumePlaybackAfterBackwardSeek = false;
        if (!_audioPlayer.HasSource)
        {
            return;
        }

        SeekPlayback(_audioPlayer.Progress);
        _audioPlayer.Play();
        UpdateTransportPlaybackState();
    }

    private void ShowBarJumpDialog()
    {
        var currentBar = ResolveBarNumberFromProgress(_audioPlayer.Progress);
        var dialog = new BarJumpDialogWindow(currentBar) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.BarNumber is { } target)
        {
            waveformView.TrySeekToBarNumber(target);
        }
    }

    private void TryToggleMetronome()
    {
        var enabled = !transportBar.IsMetronomeEnabled;
        transportBar.IsMetronomeEnabled = enabled;
        _audioPlayer.SetMetronomeEnabled(enabled);
        transportBar.PulseMetronomeFeedback();
    }

    private bool TryAdjustMetronomeVolume(int wheelDelta)
    {
        if (_uiInteractionLocks != UiInteractionLock.None
            || _metronomePlayer is null
            || !HasTransportBarNavigation())
        {
            return false;
        }

        if (!_metronomePlayer.TryAdjustVolume(wheelDelta))
        {
            return true;
        }

        _appSettings.SaveMetronomeVolume(_metronomePlayer.Volume);
        _audioPlayer.SetMetronomeVolume(_metronomePlayer.Volume);
        transportBar.PulseMetronomeFeedback();
        return true;
    }

    private void UpdateTransportPlaybackState()
    {
        transportBar.IsPlaying = _audioPlayer.IsPlaying;
    }

    private void UpdateNavigationAvailability()
    {
        var hasPreview = _loadedPreview is not null;
        var hasParts = (_previewSession?.EffectiveOutputParts.Count ?? 0) > 1;
        transportBar.SetNavigationAvailability(
            jumpToBarEnabled: hasPreview,
            previousNextBarEnabled: hasPreview,
            playlistNavigationEnabled: hasParts);
    }

    private void OnPlayheadTick()
    {
        if (_loadedPreview is null)
        {
            return;
        }

        if (_audioPlayer.TryCompletePlaybackIfEnded())
        {
            UpdateTransportPlaybackState();
            return;
        }

        var isPlaying = _audioPlayer.IsPlaying;
        var progress = _audioPlayer.Progress;
        if (isPlaying && _anchorTickMs == 0)
        {
            AnchorPlayhead(progress);
        }

        UpdatePlaylistPlaybackOnPlayheadTick(ref progress, isPlaying);
        waveformView.SetPlayhead(progress, recordTrail: isPlaying, ensureVisible: isPlaying);
        waveformView.SetOutputLevel(_audioPlayer.OutputPeak, decay: true);
        transportBar.SetPosition(ResolvePositionInfo(progress));
    }

    private TransportPositionInfo? ResolvePositionInfo(double progress)
    {
        if (_loadedPreview is null || _loadedPreview.Bars.Count == 0)
        {
            return null;
        }

        var totalSamples = Math.Max(1, _loadedPreview.Peaks.FrameCount);
        var sampleOffset = (long)Math.Round(progress * totalSamples);
        var bar = _loadedPreview.Bars[0];
        foreach (var candidate in _loadedPreview.Bars)
        {
            if (candidate.SampleOffset > sampleOffset)
            {
                break;
            }

            bar = candidate;
        }

        var sampleRate = _loadedPreview.WavInfo.SampleRate == 0 ? 48000 : _loadedPreview.WavInfo.SampleRate;
        var secondsPerBeat = bar.Bpm > 0 ? 60d / bar.Bpm : 0d;
        var samplesFromBar = sampleOffset - bar.SampleOffset;
        var secondsFromBar = samplesFromBar / (double)sampleRate;
        var beat = secondsPerBeat > 0 ? (int)(secondsFromBar / secondsPerBeat) % Math.Max(1, bar.Numerator) : 0;
        return new TransportPositionInfo(
            bar.Bpm,
            bar.Numerator,
            bar.Denominator,
            Math.Max(1, bar.BarNumber),
            beat + 1,
            1,
            TimeSpan.FromSeconds(sampleOffset / (double)sampleRate));
    }

    private int ResolveBarNumberFromProgress(double progress)
    {
        if (_loadedPreview is null || _loadedPreview.Bars.Count == 0)
        {
            return 1;
        }

        var totalSamples = Math.Max(1, _loadedPreview.Peaks.FrameCount);
        var sampleOffset = (long)Math.Round(progress * totalSamples);
        var bar = 1;
        foreach (var candidate in _loadedPreview.Bars)
        {
            if (candidate.SampleOffset > sampleOffset || candidate.IsTempoChangeOnly)
            {
                continue;
            }

            bar = Math.Max(bar, candidate.BarNumber);
        }

        return bar;
    }

    private bool TryCycleWaveformHeightScale()
    {
        var next = AppSettings.NormalizeWaveformHeightScale(_appSettings.WaveformHeightScale) switch
        {
            1 => 2,
            2 => 3,
            _ => 1,
        };
        _appSettings.SaveWaveformHeightScale(next);
        ApplyWaveformHeightScale(adjustWindowHeight: true);
        return true;
    }

    /// <summary>
    /// 波形ホスト高さを 1/2/3 倍に合わせる。Z 切替時はウィンドウ高さも差分だけ伸ばす／縮める。
    /// </summary>
    private void ApplyWaveformHeightScale(bool adjustWindowHeight = false)
    {
        var scale = AppSettings.NormalizeWaveformHeightScale(_appSettings.WaveformHeightScale);
        _appSettings.WaveformHeightScale = scale;
        transportBar.SetWaveformHeightScale(scale);

        var previousHeight = waveformHostPanel.Height;
        var desiredHeight = DesignMetrics.WaveformHostHeight * scale;
        if (Math.Abs(desiredHeight - previousHeight) < 0.5)
        {
            if (!adjustWindowHeight)
            {
                UpdateMinimumWindowSize();
            }

            SyncRightSideContentHostHeight();
            return;
        }

        waveformHostPanel.Height = desiredHeight;
        var delta = desiredHeight - previousHeight;

        if (adjustWindowHeight && WindowState == WindowState.Normal && Math.Abs(delta) >= 0.5)
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

        SyncRightSideContentHostHeight();
        waveformView.InvalidateVisual();
    }

    private void PlayMinusECheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents || _populatingPlaylistChoices)
        {
            return;
        }

        StorePlayPostExitForSelectedPart(playMinusECheckBox.IsChecked == true);
        AutosaveCurrentProject();
        SaveLastWaveSessionIfLoaded();
    }

    private void AdditiveLayersCheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_suppressProjectUiEvents || _populatingPlaylistChoices)
        {
            return;
        }

        StoreAdditiveLayersForSelectedPart(additiveLayersCheckBox.IsChecked == true);
        ApplyPlaylistItemTips();
        SaveLastWaveSessionIfLoaded();
    }

    private void TogglePlayPostExitForCurrentPlaylist()
    {
        if (_loadedPreview is null || playMinusECheckBox.IsEnabled != true)
        {
            return;
        }

        playMinusECheckBox.IsChecked = playMinusECheckBox.IsChecked != true;
    }

    private void ConfirmAndExit()
    {
        var confirm = OwnerCenteredMessageBox.Show(
            this,
            UiStrings.DialogExitBody,
            UiStrings.DialogExitTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (confirm == MessageBoxResult.Yes)
        {
            Close();
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_uiInteractionLocks != UiInteractionLock.None)
        {
            return;
        }

        // ログ欄フォーカス中は再生／波形ショートカットを無効（Form1 と同じ）。
        if (editorTextBox.IsKeyboardFocusWithin)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (key == Key.Escape)
        {
            ConfirmAndExit();
            e.Handled = true;
            return;
        }

        if (TrySeekByDigitPercentKey(key, modifiers))
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Delete && modifiers == ModifierKeys.None && TryDeleteSelectedWaveOnlyMarker())
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Insert && modifiers == ModifierKeys.None && TryAddWaveOnlyMarkerAtPlayhead())
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Delete && modifiers == ModifierKeys.Control && TryDeleteSelectedWaveOnlyMarker())
        {
            e.Handled = true;
            return;
        }

        if (key == Key.E && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (exportButton.IsEnabled)
            {
                ExportButton_Click(exportButton, new RoutedEventArgs());
            }

            e.Handled = true;
            return;
        }

        if (key == Key.E && modifiers == ModifierKeys.None && !IsTextEntryFocusActive())
        {
            TogglePlayPostExitForCurrentPlaylist();
            e.Handled = true;
            return;
        }

        if (key is Key.Left or Key.Right
            && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0
            && TryNudgeSeekByArrowKey(key))
        {
            e.Handled = true;
            return;
        }

        if (TryProcessWaveformShortcut(key, modifiers))
        {
            e.Handled = true;
        }
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftShift or Key.RightShift)
        {
            ClearPlaylistGroupPaintStickyId();
        }

        if (_activeTransportShortcutCommand is not null && key == _activeTransportShortcutKey)
        {
            EndActiveTransportShortcutFeedback();
            e.Handled = true;
        }
    }

    private bool HasTransportBarNavigation() =>
        _loadedPreview is { Bars.Count: > 0 };

    private bool HasWaveOnlyViewStepNavigation() =>
        _previewSession is { AllowsSessionMarkerEdit: true }
        && _audioPlayer.HasSource;

    private bool HasWaveOnlyMarkerNavigation() =>
        HasWaveOnlyViewStepNavigation();

    private bool HasTransportPlaylistNavigation() =>
        (_previewSession?.EffectiveOutputParts.Count ?? 0) > 0;

    private bool IsTransportCommandAvailable(TransportCommand command) => command switch
    {
        TransportCommand.JumpToBar => HasTransportBarNavigation(),
        TransportCommand.PreviousBar or TransportCommand.NextBar =>
            HasTransportBarNavigation() || HasWaveOnlyViewStepNavigation(),
        TransportCommand.PreviousPlaylist or TransportCommand.NextPlaylist =>
            HasTransportPlaylistNavigation(),
        _ => true,
    };

    private bool TrySeekByDigitPercentKey(Key key, ModifierKeys modifiers)
    {
        if (!_audioPlayer.HasSource
            || IsTextEntryFocusActive()
            || modifiers != ModifierKeys.None)
        {
            return false;
        }

        if (!TryGetPercentDigit(key, out var digit))
        {
            return false;
        }

        var progress = Math.Clamp(
            waveformView.TimeViewStart + (digit / 10d) * waveformView.TimeViewSpan,
            0d,
            1d);
        SeekPlayback(progress);
        waveformView.SetPlayhead(progress, recordTrail: false, ensureVisible: false);
        return true;
    }

    private static bool TryGetPercentDigit(Key key, out int digit)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            digit = key - Key.D0;
            return true;
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            digit = key - Key.NumPad0;
            return true;
        }

        digit = 0;
        return false;
    }

    private bool TryNudgeSeekByArrowKey(Key key)
    {
        if (key is not (Key.Left or Key.Right))
        {
            return false;
        }

        if (!_audioPlayer.HasSource || _loadedPreview is null)
        {
            return false;
        }

        var timelineWidth = Math.Max(1, waveformView.TimelineContentWidth);
        var progress = _audioPlayer.Progress;
        var progressDelta = (key == Key.Left ? -1 : 1)
            * (waveformView.TimeViewSpan / timelineWidth);
        var next = Math.Clamp(progress + progressDelta, 0d, 1d);
        if (Math.Abs(next - progress) < 1e-15)
        {
            return true;
        }

        SeekPlayback(next);
        return true;
    }

    private bool TrySeekNearActiveLoopEnd()
    {
        if (!_audioPlayer.HasSource
            || _loadedPreview is not { } preview
            || preview.WavInfo.FrameCount <= 0)
        {
            return false;
        }

        if (!_audioPlayer.TryGetActiveLoopSamples(out var loopStartSample, out var loopEndSample)
            && !_audioPlayer.TryGetLoopSamples(_audioPlayer.Progress, out loopStartSample, out loopEndSample))
        {
            return false;
        }

        if (loopEndSample <= loopStartSample)
        {
            return false;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var sampleRate = preview.WavInfo.SampleRate;
        long targetSample;
        if (sampleRate > 0)
        {
            targetSample = loopEndSample - (3L * sampleRate);
        }
        else
        {
            return false;
        }

        targetSample = Math.Clamp(
            targetSample,
            loopStartSample,
            Math.Max(loopStartSample, loopEndSample - 1));
        var targetProgress = Math.Clamp(targetSample / (double)frameCount, 0d, 1d);
        if (targetProgress < _audioPlayer.Progress)
        {
            PauseForBackwardSeekHold();
        }

        SeekPlayback(targetProgress, ensureVisible: true);
        return true;
    }

    private bool TryDeleteSelectedWaveOnlyMarker()
    {
        if (waveformView.SelectedMarkerSampleOffset is not { } sampleOffset)
        {
            return false;
        }

        return TryDeleteWaveOnlyMarker(sampleOffset);
    }

    private bool TryAddWaveOnlyMarkerAtPlayhead()
    {
        if (_previewSession is not { AllowsSessionMarkerEdit: true } session
            || _loadedPreview is null)
        {
            return false;
        }

        var frameCount = _loadedPreview.WavInfo.FrameCount;
        if (frameCount <= 0)
        {
            return false;
        }

        var sampleOffset = (long)Math.Round(Math.Clamp(_audioPlayer.Progress, 0d, 1d) * frameCount);
        sampleOffset = Math.Clamp(sampleOffset, 0L, frameCount - 1);

        if (session.HasWaveOnlyMarkerAt(sampleOffset))
        {
            AppendReport(UiStrings.LogWaveOnlyMarkerDuplicate + Environment.NewLine);
            return true;
        }

        if (!session.TryAddWaveOnlyMarker(sampleOffset, comment: string.Empty))
        {
            return false;
        }

        RefreshMarkersOnWaveform();
        waveformView.SetSelectedMarkerSampleOffset(sampleOffset);
        SaveLastWaveSessionIfLoaded();
        return true;
    }

    private bool IsTextEntryFocusActive()
    {
        if (editorTextBox.IsKeyboardFocusWithin || projectNameComboBox.IsKeyboardFocusWithin)
        {
            return true;
        }

        return Keyboard.FocusedElement switch
        {
            TextBox { IsReadOnly: true } => false,
            TextBox => true,
            RichTextBox => true,
            ComboBox { IsEditable: true } => true,
            _ => false,
        };
    }

    private void ReleaseFocusToWaveform(bool forceTextBoxRelease = false)
    {
        if (!forceTextBoxRelease && IsTextEntryFocusActive() && !projectNameComboBox.IsKeyboardFocusWithin
            && Keyboard.FocusedElement is TextBox { IsReadOnly: false })
        {
            return;
        }

        if (projectOutputPathTextBox.IsKeyboardFocusWithin || projectNameComboBox.IsKeyboardFocusWithin)
        {
            // 読み取り専用パス／コンボは例外的に波形へ戻す
        }
        else if (IsTextEntryFocusActive() && !forceTextBoxRelease)
        {
            return;
        }

        waveformView.Focus();
    }

    private void StopPlaybackForExport()
    {
        _audioPlayer.CancelPlaylistTransition();
        ClearPendingPlaylistUiTransition();
        ClearPlaylistTransitionGlow();
        _playheadTimer.Stop();
        if (_audioPlayer.IsPlaying)
        {
            _audioPlayer.Pause();
        }

        ClearPlaylistPlaybackSelection();
        UpdateTransportPlaybackState();
        OnPlayheadTick();
    }
}
