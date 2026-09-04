using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MgaWwiseIMImporter.UI;

/// <summary>再生・トランスポート・メトロノーム・ショートカット。</summary>
public partial class MainWindow
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VkLeft = 0x25;
    private const int VkRight = 0x27;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;

    private readonly WaveAudioPlayer _audioPlayer = new();
    private readonly MetronomePlayer? _metronomePlayer = MetronomePlayer.TryCreate();
    private readonly object _metronomeVolumeTipSource = new();
    private readonly DispatcherTimer _metronomeVolumeTipTimer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private readonly DispatcherTimer _playheadTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _markerNudgeTimer = new();
    private Key _markerNudgeDirection = Key.None;
    private bool _markerNudgeRepeatStarted;
    private double? _lastPlaybackStartProgress;
    private bool _resumePlaybackAfterBackwardSeek;
    private int? _lastJumpedBarNumber;
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
            UpdateSourceLevelMeter();
        });
        // Diagnostic は ASIO コールバック等の非 UI スレッドから来る。IsChecked を触らない。
        _audioPlayer.Diagnostic += (_, message) =>
        {
#if DEBUG
            if (!_developerSettings.DetailedPlaybackLog)
            {
                return;
            }

            Dispatcher.BeginInvoke(() => AppendColoredLine(message));
#endif
        };

        InstallMetronomeClicks();
        projectSpectrumView.Player = _audioPlayer;

        transportBar.MetronomeInvoked += (_, _) => TryToggleMetronome();
        _metronomeVolumeTipTimer.Tick += (_, _) =>
        {
            _metronomeVolumeTipTimer.Stop();
            TipService.Clear(_metronomeVolumeTipSource);
            transportBar.RestoreMetronomeTipIfHovered();
        };
        _playheadTimer.Tick += (_, _) => OnPlayheadTick();
        _playheadTimer.Start();
        _markerNudgeTimer.Tick += (_, _) => TickWaveOnlyMarkerNudgeHold();
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
        if (!_audioPlayer.HasSource)
        {
            return;
        }

        // 多重波形 + Additive 重ね中はレイヤー相対シークを優先
        if (TrySeekPreservingAdditiveLayers(progress, ensureVisible))
        {
            return;
        }

        var clamped = Math.Clamp(progress, 0d, 1d);
        _audioPlayer.CancelPlaylistTransition();
        ClearPendingPlaylistUiTransition();
        ClearPlaylistOverlayState();
        SetManualPlaylistHighlight(clamped);
        _audioPlayer.Seek(clamped);
        // ジャンプ先が -L 内ならその区間に付け替え、外ならループ解除
        _audioPlayer.ArmLoopAtProgress(clamped);
        AnchorPlayhead(clamped);
        waveformView.SetPlayhead(clamped, recordTrail: false, ensureVisible: ensureVisible);
        waveformView.SetExitPlayhead(null);
        waveformView.SetFadeOutPlayhead(null);
        UpdateSourceLevelMeter();
    }

    private void StartPlaybackAt(double progress)
    {
        _resumePlaybackAfterBackwardSeek = false;
        var wasPlaying = _audioPlayer.IsPlaying;
        var clamped = Math.Clamp(progress, 0d, 1d);
        SeekPlayback(clamped);
        _lastPlaybackStartProgress = clamped;
        ApplyPlayExitLayerForCurrentPlayback();

        if (!wasPlaying)
        {
            _audioPlayer.Play();
        }

        AnchorPlayhead(clamped);
        _audioPlayer.ArmLoopAtProgress(clamped);
        _playheadTimer.Start();
        OnPlayheadTick();

        if (!wasPlaying && _audioPlayer.IsPlaying)
        {
            StartPlaylistTransitionGlow();
        }
        else
        {
            ApplyPlaylistButtonColors();
        }

        UpdateTransportPlaybackState();
    }

    private void RestartFromLastPlaybackStart()
    {
        if (!_audioPlayer.HasSource)
        {
            return;
        }

        StartPlaybackAt(_lastPlaybackStartProgress ?? _smoothProgress);
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

        var start = Math.Max(0d, _smoothProgress - (prerollSeconds / durationSec));
        StartPlaybackAt(start);
    }

    private void PauseForBackwardSeekHold()
    {
        if (!_audioPlayer.HasSource || !_audioPlayer.IsPlaying || _resumePlaybackAfterBackwardSeek)
        {
            return;
        }

        // タイマー基準の現在位置を確定してから止める
        var durationSec = _audioPlayer.Duration.TotalSeconds;
        if (durationSec > 0)
        {
            var elapsedSec = (Environment.TickCount64 - _anchorTickMs) / 1000d;
            _smoothProgress = Math.Clamp(_anchorProgress + elapsedSec / durationSec, 0d, 1d);
        }

        _playheadTimer.Stop();
        _audioPlayer.Pause();
        UpdateTransportPlaybackState();
        SeekPlayback(_smoothProgress);
        _resumePlaybackAfterBackwardSeek = true;
        OnPlayheadTick();
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

        SeekPlayback(_smoothProgress);
        _audioPlayer.Play();
        AnchorPlayhead(_smoothProgress);
        _playheadTimer.Start();
        OnPlayheadTick();
        UpdateTransportPlaybackState();
    }

    private void ShowBarJumpDialog()
    {
        if (!HasTransportBarNavigation())
        {
            return;
        }

        // 初回は現在位置の最近傍小節。一度ジャンプしたあとはその値を初期表示する。
        var dialog = new BarJumpDialogWindow(
            _lastJumpedBarNumber ?? waveformView.GetNearestBarNumber())
        {
            Owner = this,
        };
        if (dialog.ShowDialog() != true || dialog.BarNumber is not int barNumber)
        {
            return;
        }

        _lastJumpedBarNumber = barNumber;
        if (!waveformView.TrySeekToBarNumber(barNumber))
        {
            AppendReport(
                $"{UiStrings.LogGoToMeasureHeader}{Environment.NewLine}"
                + UiStrings.LogBarNotFound(barNumber)
                + Environment.NewLine
                + Environment.NewLine);
        }
    }

    private bool TryToggleMetronome()
    {
        if (_uiInteractionLocks != UiInteractionLock.None
            || _metronomePlayer is null
            || !HasTransportBarNavigation())
        {
            return false;
        }

        transportBar.IsMetronomeEnabled = !transportBar.IsMetronomeEnabled;
        _audioPlayer.SetMetronomeEnabled(transportBar.IsMetronomeEnabled);
        transportBar.PulseMetronomeFeedback();
        ReleaseFocusToWaveform();
        return true;
    }

    /// <summary>
    /// 音符＋テンポ上のホイールでメトロノーム音量を変える（下限 10%〜最大、10% 刻み。アプリ設定に保存）。
    /// </summary>
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
            ShowMetronomeVolumeTip();
            return true;
        }

        _appSettings.SaveMetronomeVolume(_metronomePlayer.Volume);
        _audioPlayer.SetMetronomeVolume(_metronomePlayer.Volume);
        transportBar.PulseMetronomeFeedback();
        ShowMetronomeVolumeTip();
        return true;
    }

    private void ShowMetronomeVolumeTip()
    {
        if (_metronomePlayer is null)
        {
            return;
        }

        var percent = (int)Math.Round(_metronomePlayer.Volume * 100d);
        TipService.Show(
            UiStrings.TipMetronomeVolume(percent),
            _metronomeVolumeTipSource);
        _metronomeVolumeTipTimer.Stop();
        _metronomeVolumeTipTimer.Start();
    }

    /// <summary>
    /// </summary>
    private void ApplyMetronomeBarsFromPreview(WaveformPreviewData? preview)
    {
        if (preview is { Bars.Count: > 0 })
        {
            _audioPlayer.SetMetronomeBars(preview.Bars);
            return;
        }

        _audioPlayer.SetMetronomeBars([]);
        if (transportBar.IsMetronomeEnabled)
        {
            transportBar.IsMetronomeEnabled = false;
        }

        _audioPlayer.SetMetronomeEnabled(false);
    }

    private void UpdateTransportPlaybackState()
    {
        transportBar.IsPlaying = _audioPlayer.IsPlaying;
    }

    private void UpdateSourceLevelMeter()
    {
        // 停止中はピークを 0・減衰なしで即リセット（タイマー停止後もメーターが残らないようにする）。
        var peak = _audioPlayer.IsPlaying ? _audioPlayer.OutputPeak : 0f;
        var targetLevel = peak <= 0.001f
            ? 0f
            : (float)Math.Clamp((20d * Math.Log10(peak) + 60d) / 60d, 0d, 1d);
        waveformView.SetOutputLevel(targetLevel, decay: _audioPlayer.IsPlaying);
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
            UpdateSourceLevelMeter();
            return;
        }

        if (!_audioPlayer.HasSource)
        {
            waveformView.SetPlayhead(null);
            waveformView.SetFadeOutPlayhead(null);
            waveformView.SetExitPlayhead(null);
            UpdateSourceLevelMeter();
            transportBar.SetPosition(null);
            return;
        }

        var isPlaying = _audioPlayer.IsPlaying;
        var progress = _audioPlayer.Progress;
        if (isPlaying && _anchorTickMs == 0)
        {
            AnchorPlayhead(progress);
        }

        UpdatePlaylistPlaybackOnPlayheadTick(ref progress, isPlaying);

        if (isPlaying)
        {
            var clockFadeOutActive = _audioPlayer.TryGetClockFadeOutPlaybackProgress(
                out var clockFadeProgress);
            if (clockFadeOutActive)
            {
                waveformView.SetPlayhead(null);
                waveformView.SetFadeOutPlayhead(
                    clockFadeProgress,
                    recordTrail: true,
                    isExit: false);
            }
            else
            {
                waveformView.SetPlayhead(progress, recordTrail: true, ensureVisible: true);
            }

            double? targetExitProgress = null;
            if (_audioPlayer.TryGetExitPlaybackProgress(out var exitProgress))
            {
                targetExitProgress = exitProgress;
            }

            if (!clockFadeOutActive
                && _audioPlayer.TryGetPlaylistFadePlaybackProgress(
                    out var fadeProgress,
                    out var fadeReachedExit))
            {
                waveformView.SetFadeOutPlayhead(
                    fadeProgress,
                    recordTrail: true,
                    isExit: fadeReachedExit);
            }
            else if (!clockFadeOutActive)
            {
                waveformView.SetFadeOutPlayhead(null);
            }

            waveformView.SetExitPlayhead(
                targetExitProgress,
                recordTrail: targetExitProgress is not null);
        }
        else
        {
            waveformView.SetPlayhead(progress, recordTrail: false, ensureVisible: false);
            waveformView.SetExitPlayhead(null);
            waveformView.SetFadeOutPlayhead(null);
        }

        UpdateSourceLevelMeter();
        transportBar.SetPosition(ResolvePositionInfo(progress));
    }

    private TransportPositionInfo? ResolvePositionInfo(double progress)
    {
        if (_loadedPreview is null)
        {
            return null;
        }

        var totalSamples = Math.Max(1, _loadedPreview.Peaks.FrameCount);
        var sampleOffset = (long)Math.Round(progress * totalSamples);
        var sampleRate = _loadedPreview.WavInfo.SampleRate == 0 ? 48000 : _loadedPreview.WavInfo.SampleRate;
        var time = TimeSpan.FromSeconds(sampleOffset / (double)sampleRate);

        // XML 無し（小節線なし）でもタイムコードは出す。拍子／小節位置だけ無効。
        if (_loadedPreview.Bars.Count == 0)
        {
            return new TransportPositionInfo(
                Bpm: 0,
                Numerator: 0,
                Denominator: 0,
                Bar: 0,
                Beat: 1,
                Subdivision: 1,
                Time: time,
                HasMusicalPosition: false);
        }

        var bar = _loadedPreview.Bars[0];
        foreach (var candidate in _loadedPreview.Bars)
        {
            if (candidate.SampleOffset > sampleOffset)
            {
                break;
            }

            bar = candidate;
        }

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
            time);
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
        ApplyPlayExitLayerForCurrentPlayback();
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
        if (_loadedPreview is null)
        {
            return;
        }

        // 選択中ではなく、実際に再生中／シーク位置の Playlist のみ対象。
        var partNumber = ResolveClockPlaylistPart()?.Number
            ?? TryGetOutputPartAtProgress(_smoothProgress)?.Number;
        if (partNumber is not int number)
        {
            return;
        }

        var enabled = !ResolvePlayPostExit(number);
        StorePlayPostExit(number, enabled);
        SaveLastWaveSessionIfLoaded();
        SelectPlaylistPart(number, seekAndPlay: false);
        ApplyPlayExitLayerForCurrentPlayback();
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

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        // Esc はログ欄フォーカス中でも終了確認を出す（Form1 と同じ）。
        // プロジェクト名編集中はコンボ側の編集キャンセルに委ねる。
        if (key == Key.Escape)
        {
            if (projectNameComboBox.IsKeyboardFocusWithin)
            {
                return;
            }

            ConfirmAndExit();
            e.Handled = true;
            return;
        }

        if (TryHandleLogTipsFontShortcut(key, modifiers))
        {
            e.Handled = true;
            return;
        }

        // ログ欄フォーカス中は再生／波形ショートカットを無効（Form1 と同じ）。
        if (editorTextBox.IsKeyboardFocusWithin)
        {
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

        if (key == Key.R
            && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && TryRenameWaveOnlyMarkerAtPlayhead())
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Delete && modifiers == ModifierKeys.Control && TryDeleteWaveOnlyMarkerAtPlayhead())
        {
            e.Handled = true;
            return;
        }

        if (key == Key.W && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            waapiStatusBar.TryInvokeProjectNameClick();
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

#if DEBUG
        if (key == Key.F8 && modifiers == ModifierKeys.None)
        {
            ShowWwiseImportFailedDialogForTest();
            e.Handled = true;
            return;
        }
#endif

        if (key == Key.E && modifiers == ModifierKeys.None && !IsTextEntryFocusActive())
        {
            TogglePlayPostExitForCurrentPlaylist();
            e.Handled = true;
            return;
        }

        // Alt+←/→ は押下中をタイマーで扱う（途中で Shift を足してもオートリピートが途切れない）。
        if (key is Key.Left or Key.Right
            && modifiers.HasFlag(ModifierKeys.Alt)
            && TryBeginWaveOnlyMarkerNudgeHold(key))
        {
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

        if (key == Key.Z
            && modifiers == ModifierKeys.Control
            && (TryUndoRegionEdgeFade() || TryUndoWaveOnlyMarkerEdit()))
        {
            e.Handled = true;
            return;
        }

        if (((key == Key.Z && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                || (key == Key.Y && modifiers == ModifierKeys.Control))
            && (TryRedoRegionEdgeFade() || TryRedoWaveOnlyMarkerEdit()))
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

        if (_resumePlaybackAfterBackwardSeek && IsBackwardSeekKey(key))
        {
            ResumePlaybackAfterBackwardSeek();
            e.Handled = true;
        }

        if (key is Key.Left or Key.Right or Key.LeftAlt or Key.RightAlt)
        {
            // Shift 追加などで OS が一時的に KeyUp を出しても、実キーが押されていれば継続する。
            if ((!IsAsyncKeyDown(VkLeft) && !IsAsyncKeyDown(VkRight))
                || !IsAsyncKeyDown(VkMenu))
            {
                StopWaveOnlyMarkerNudgeHold(flushPersist: true);
            }
        }
    }

    private static bool IsBackwardSeekKey(Key key) =>
        key is Key.Home or Key.Left or Key.PageUp or Key.L;

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

        double next;
        // サンプル点が描かれる密度では 1px ではなく 1 サンプル単位で動かす（Sonic Anvil と同じ）。
        if (waveformView.SamplePointsVisible)
        {
            var frameCount = _loadedPreview.WavInfo.FrameCount;
            if (frameCount <= 0)
            {
                return false;
            }

            var currentSample = (long)Math.Round(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount);
            currentSample = Math.Clamp(currentSample, 0L, frameCount - 1);
            var nextSample = Math.Clamp(
                currentSample + (key == Key.Left ? -1L : 1L),
                0L,
                frameCount - 1);
            if (nextSample == currentSample)
            {
                return true;
            }

            next = nextSample / (double)frameCount;
        }
        else
        {
            var timelineWidth = Math.Max(1, waveformView.TimelineContentWidth);
            // 表示中の再生ヘッド基準（エンジン Progress はバッファ遅れがあり、右キーが進まないように見える）
            var progressDelta = (key == Key.Left ? -1 : 1)
                * (waveformView.TimeViewSpan / timelineWidth);
            next = Math.Clamp(_smoothProgress + progressDelta, 0d, 1d);
        }

        if (Math.Abs(next - _smoothProgress) < 1e-15)
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

        if (!TryResolveLoopEndSamples(out var loopStartSample, out var loopEndSample)
            || loopEndSample <= loopStartSample)
        {
            return false;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var sampleRate = preview.WavInfo.SampleRate;

        // (1) 小節管理あり → ループエンドの 1 小節前
        // (2) 小節管理なし／小節に届かない → ループエンドの 3 秒前
        long? oneBarBefore = HasTransportBarNavigation()
            ? FindSampleOneBarBefore(preview.Bars, loopEndSample, sampleRate)
            : null;
        long targetSample;
        if (oneBarBefore is long barSample)
        {
            targetSample = barSample;
        }
        else
        {
            if (sampleRate <= 0)
            {
                return false;
            }

            targetSample = loopEndSample - (3L * sampleRate);
        }

        targetSample = Math.Clamp(
            targetSample,
            loopStartSample,
            Math.Max(loopStartSample, loopEndSample - 1));
        var targetProgress = Math.Clamp(targetSample / (double)frameCount, 0d, 1d);
        if (targetProgress < _smoothProgress)
        {
            PauseForBackwardSeekHold();
        }

        SeekPlayback(targetProgress, ensureVisible: true);
        return true;
    }

    /// <summary>
    /// ジャンプ先のループ範囲（-L の折り返し）を解決する。
    /// Playlist オール（-E 含む）ではなく、実際にループする -L エンドを使う。
    /// </summary>
    private bool TryResolveLoopEndSamples(out long loopStartSample, out long loopEndSample)
    {
        loopStartSample = 0;
        loopEndSample = 0;

        if (_audioPlayer.TryGetActiveLoopSamples(out loopStartSample, out loopEndSample)
            || _audioPlayer.TryGetLoopSamples(_smoothProgress, out loopStartSample, out loopEndSample))
        {
            return true;
        }

        if (ResolveClockPlaylistPart() is { } part
            && TryFindLoopSamplesInRange(
                part.StartSampleOffset,
                part.EndSampleOffset,
                out loopStartSample,
                out loopEndSample))
        {
            return true;
        }

        return false;
    }

    private bool TryFindLoopSamplesInRange(
        long rangeStart,
        long rangeEnd,
        out long loopStartSample,
        out long loopEndSample)
    {
        loopStartSample = 0;
        loopEndSample = 0;
        if (_loadedPreview is not { } preview || rangeEnd <= rangeStart)
        {
            return false;
        }

        var plans = WaveAudioPlayer.BuildLoopPlans(GetEffectiveRegions());
        if (plans.Length == 0)
        {
            return false;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var playheadSample = frameCount > 0
            ? (long)Math.Round(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount)
            : rangeStart;

        LoopPlaybackPlan? best = null;
        var bestDistance = long.MaxValue;
        foreach (var plan in plans)
        {
            if (plan.LoopEndSample <= plan.LoopStartSample
                || plan.LoopStartSample < rangeStart
                || plan.LoopEndSample > rangeEnd)
            {
                continue;
            }

            var distance = playheadSample < plan.LoopStartSample
                ? plan.LoopStartSample - playheadSample
                : playheadSample >= plan.LoopEndSample
                    ? playheadSample - plan.LoopEndSample
                    : 0L;
            if (best is null
                || distance < bestDistance
                || (distance == bestDistance && plan.LoopEndSample > best.Value.LoopEndSample))
            {
                best = plan;
                bestDistance = distance;
            }
        }

        if (best is not { } chosen)
        {
            return false;
        }

        loopStartSample = chosen.LoopStartSample;
        loopEndSample = chosen.LoopEndSample;
        return true;
    }

    /// <summary>
    /// <paramref name="loopEndSample"/> の 1 小節前の小節線。無ければ null（呼び出し側は 3 秒前へ）。
    /// </summary>
    private static long? FindSampleOneBarBefore(
        IReadOnlyList<WaveformBarMark> bars,
        long loopEndSample,
        uint sampleRate)
    {
        var endTolerance = sampleRate > 0
            ? Math.Max(2L, sampleRate / 100L)
            : 2L;
        var endThreshold = loopEndSample - endTolerance;

        long? previous = null;
        foreach (var mark in bars)
        {
            if (mark.IsTempoChangeOnly)
            {
                continue;
            }

            if (mark.SampleOffset >= endThreshold)
            {
                break;
            }

            previous = mark.SampleOffset;
        }

        return previous;
    }

    private bool TryDeleteSelectedWaveOnlyMarker()
    {
        if (waveformView.SelectedMarkerSampleOffset is not { } sampleOffset)
        {
            return false;
        }

        return TryDeleteWaveOnlyMarker(sampleOffset);
    }

    private bool TryRenameWaveOnlyMarkerAtPlayhead()
    {
        if (_previewSession is not { AllowsSessionMarkerEdit: true }
            || _loadedPreview is null)
        {
            return false;
        }

        var frameCount = _loadedPreview.WavInfo.FrameCount;
        if (frameCount <= 0)
        {
            return false;
        }

        var sampleOffset = (long)Math.Round(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount);
        sampleOffset = Math.Clamp(sampleOffset, 0L, frameCount - 1);
        return waveformView.TryBeginMarkerCommentEditAtSample(sampleOffset);
    }

    private bool TryDeleteWaveOnlyMarkerAtPlayhead()
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

        var sampleOffset = (long)Math.Round(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount);
        sampleOffset = Math.Clamp(sampleOffset, 0L, frameCount - 1);
        if (!session.HasWaveOnlyMarkerAt(sampleOffset))
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

        var sampleOffset = (long)Math.Round(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount);
        sampleOffset = Math.Clamp(sampleOffset, 0L, frameCount - 1);

        if (session.HasWaveOnlyMarkerAt(sampleOffset))
        {
            AppendReport(UiStrings.LogWaveOnlyMarkerDuplicate + Environment.NewLine);
            return true;
        }

        if (!TryMutateWaveOnlyMarkers(
                current => current.TryAddWaveOnlyMarker(sampleOffset, comment: string.Empty)))
        {
            return false;
        }

        waveformView.SetSelectedMarkerSampleOffset(sampleOffset);
        return true;
    }

    /// <summary>
    /// 再生ヘッド位置のマーカーを 1 ピクセル相当（Shift で 3 ピクセル）だけ左右へ動かす。
    /// Ctrl（shiftPrevious）で直前マーカーとペア移動。
    /// </summary>
    private bool TryNudgeWaveOnlyMarkerAtPlayheadByPixel(
        Key keyCode,
        bool shiftPrevious = false,
        int pixelStep = 1)
    {
        if (keyCode is not (Key.Left or Key.Right))
        {
            return false;
        }

        if (pixelStep < 1)
        {
            pixelStep = 1;
        }

        if (_uiInteractionLocks != UiInteractionLock.None
            || _previewSession is not { AllowsSessionMarkerEdit: true } session
            || _loadedPreview is null
            || !_audioPlayer.HasSource)
        {
            return false;
        }

        var frameCount = _loadedPreview.WavInfo.FrameCount;
        if (frameCount <= 0)
        {
            return false;
        }

        var fromSample = (long)Math.Round(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount);
        fromSample = Math.Clamp(fromSample, 0L, frameCount - 1);
        if (!session.HasWaveOnlyMarkerAt(fromSample))
        {
            return false;
        }

        var timelineWidth = Math.Max(1, waveformView.TimelineContentWidth);
        var progressDelta = (keyCode == Key.Left ? -1 : 1)
            * pixelStep
            * (waveformView.TimeViewSpan / timelineWidth);
        var nextProgress = Math.Clamp(_smoothProgress + progressDelta, 0d, 1d);
        var toSample = (long)Math.Round(nextProgress * frameCount);
        toSample = Math.Clamp(toSample, 0L, frameCount - 1);

        // ズームインで 1 ステップが 1 サンプル未満のときは最低 pixelStep サンプル動かす。
        if (toSample == fromSample)
        {
            var step = (keyCode == Key.Left ? -1L : 1L) * pixelStep;
            toSample = Math.Clamp(fromSample + step, 0L, frameCount - 1);
            if (toSample == fromSample)
            {
                return true;
            }
        }

        if (shiftPrevious)
        {
            toSample = session.ClampWaveOnlyMarkerMoveWithPrevious(fromSample, toSample);
            if (toSample == fromSample)
            {
                return true;
            }

            if (!TryMutateWaveOnlyMarkers(
                    current => current.TryMoveWaveOnlyMarkerWithPrevious(fromSample, toSample),
                    persistSession: false))
            {
                if (session.HasWaveOnlyMarkerAt(toSample))
                {
                    AppendReport(UiStrings.LogWaveOnlyMarkerDuplicate + Environment.NewLine);
                }

                return true;
            }
        }
        else
        {
            toSample = session.ClampWaveOnlyMarkerMove(fromSample, toSample);
            if (toSample == fromSample)
            {
                return true;
            }

            if (session.HasWaveOnlyMarkerAt(toSample))
            {
                AppendReport(UiStrings.LogWaveOnlyMarkerDuplicate + Environment.NewLine);
                return true;
            }

            if (!TryMutateWaveOnlyMarkers(
                    current => current.TryMoveWaveOnlyMarker(fromSample, toSample),
                    persistSession: false))
            {
                return true;
            }
        }

        _pendingWaveOnlySessionPersist = true;
        waveformView.SetSelectedMarkerSampleOffset(toSample);
        // 次のキー入力でも「ちょうどマーカー上」と判定できるようサンプル位置へ合わせる。
        // -R 直後へ出たときも表示位置を追従させ、キーリピート中に位置が定まるようにする。
        SeekPlayback((double)toSample / frameCount, ensureVisible: true);
        waveformView.InvalidateVisual();
        return true;
    }

    /// <summary>
    /// Alt+←/→ 押しっぱなし開始。OS のキーリピートに頼らずタイマーで連続移動し、
    /// 途中の Shift／Ctrl 追加でも途切れず倍速・ペア移動を切り替える。
    /// </summary>
    private bool TryBeginWaveOnlyMarkerNudgeHold(Key keyCode)
    {
        if (keyCode is not (Key.Left or Key.Right))
        {
            return false;
        }

        if (_markerNudgeTimer.IsEnabled && _markerNudgeDirection == keyCode)
        {
            // OS からのリピートは握りつぶし、タイマー側の間隔だけ使う。
            return true;
        }

        if (_uiInteractionLocks != UiInteractionLock.None
            || _previewSession is not { AllowsSessionMarkerEdit: true }
            || _loadedPreview is null
            || !_audioPlayer.HasSource)
        {
            return false;
        }

        StopWaveOnlyMarkerNudgeHold(flushPersist: false);
        _markerNudgeDirection = keyCode;
        _markerNudgeRepeatStarted = false;
        _markerNudgeTimer.Interval =
            TimeSpan.FromMilliseconds((SystemParameters.KeyboardDelay + 1) * 250);

        if (!TryNudgeWaveOnlyMarkerAtPlayheadFromHeldKeys())
        {
            _markerNudgeDirection = Key.None;
            return false;
        }

        _markerNudgeTimer.Start();
        return true;
    }

    private void TickWaveOnlyMarkerNudgeHold()
    {
        if (_markerNudgeDirection is not (Key.Left or Key.Right))
        {
            StopWaveOnlyMarkerNudgeHold(flushPersist: true);
            return;
        }

        var directionVk = _markerNudgeDirection == Key.Left ? VkLeft : VkRight;
        if (!IsAsyncKeyDown(directionVk) || !IsAsyncKeyDown(VkMenu))
        {
            StopWaveOnlyMarkerNudgeHold(flushPersist: true);
            return;
        }

        if (!_markerNudgeRepeatStarted)
        {
            _markerNudgeRepeatStarted = true;
            // Windows の KeyboardSpeed: 0=約2.5回/秒、31=約30回/秒。
            var repeatsPerSecond =
                2.5d + SystemParameters.KeyboardSpeed * (30d - 2.5d) / 31d;
            _markerNudgeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(
                20,
                (int)Math.Round(1000d / repeatsPerSecond)));
        }

        TryNudgeWaveOnlyMarkerAtPlayheadFromHeldKeys();
    }

    private bool TryNudgeWaveOnlyMarkerAtPlayheadFromHeldKeys()
    {
        if (_markerNudgeDirection is not (Key.Left or Key.Right))
        {
            return false;
        }

        var shiftPrevious = IsAsyncKeyDown(VkControl);
        var pixelStep = IsAsyncKeyDown(VkShift) ? 3 : 1;
        return TryNudgeWaveOnlyMarkerAtPlayheadByPixel(
            _markerNudgeDirection,
            shiftPrevious,
            pixelStep);
    }

    private void StopWaveOnlyMarkerNudgeHold(bool flushPersist)
    {
        _markerNudgeTimer.Stop();
        _markerNudgeDirection = Key.None;
        _markerNudgeRepeatStarted = false;
        if (flushPersist)
        {
            FlushPendingWaveOnlySessionPersist();
        }
    }

    private static bool IsAsyncKeyDown(int vKey) =>
        (GetAsyncKeyState(vKey) & 0x8000) != 0;

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
