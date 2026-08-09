using System.Windows.Media;
using System.Windows.Threading;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>Music Playlist 再生中のボタン着色・遷移点滅・遷移グロー（Form1 相当）。</summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _playlistBlinkTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer _playlistTransitionGlowTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    private double _smoothProgress;
    private double _anchorProgress;
    private long _anchorTickMs;

    private bool _automaticPlaylistPlayback;
    private int? _activeAutomaticPlaylistPartNumber;
    private int? _requestedPlaylistPartNumber;
    private int? _manualPlaylistPartNumber;

    private readonly HashSet<int> _playingPlaylistPartNumbers = [];
    private readonly int[] _overlayVoiceIdScratch = new int[WaveAudioPlayer.MaxPlaylistVoices];
    private readonly int[] _overlayFadeOutVoiceIdScratch = new int[WaveAudioPlayer.MaxPlaylistVoices];
    private readonly int[] _overlayExitVoiceIdScratch = new int[WaveAudioPlayer.MaxPlaylistVoices];
    private readonly double[] _overlayProgressScratch = new double[WaveAudioPlayer.MaxPlaylistVoices];
    private readonly double[] _overlayFadeOutProgressScratch = new double[WaveAudioPlayer.MaxPlaylistVoices];
    private readonly double[] _overlayExitProgressScratch = new double[WaveAudioPlayer.MaxPlaylistVoices];
    private readonly List<double> _overlayPlayheadProgresses = [];
    private readonly List<double> _overlayFadeOutPlayheadProgresses = [];
    private readonly List<double> _overlayExitPlayheadProgresses = [];
    private readonly HashSet<int> _playingPlaylistPartNumbersSyncScratch = [];
    private readonly Dictionary<int, (long StartTickMs, double DurationMs, bool FadeIn)> _playlistHighlightFades = [];

    private int? _pendingOverlayPartNumber;
    private bool _pendingOverlayFadeOut;
    private long _pendingOverlayAtSample;

    private long _pendingPlaylistTransitionGeneration;
    private long _pendingPlaylistBoundarySample;
    private long _pendingPlaylistSyncBoundarySample;
    private long _pendingPlaylistTargetSample;
    private long _pendingPlaylistTargetEntrySample;
    private bool _pendingPlaylistAudioStarted;
    private double? _pendingSourceLoopStart;
    private double? _pendingSourceLoopEnd;
    private double _pendingPlaylistBlinkLevel;

    private int? _playlistTransitionGlowPartNumber;
    private long _playlistTransitionGlowStartTickMs;
    private double _playlistTransitionGlowDurationMs;
    private double _playlistTransitionGlowLevel;

    private void InitializePlaylistPlaybackTimers()
    {
        _playlistBlinkTimer.Tick += (_, _) => UpdatePendingPlaylistBlink();
        _playlistTransitionGlowTimer.Tick += (_, _) => UpdatePlaylistTransitionGlow();
    }

    private void DisposePlaylistPlaybackTimers()
    {
        _playlistBlinkTimer.Stop();
        _playlistTransitionGlowTimer.Stop();
    }

    private IReadOnlyList<WaveformOutputPart> GetEffectiveOutputParts() =>
        _previewSession?.EffectiveOutputParts ?? [];

    private IReadOnlyList<WaveformRegionMark> GetEffectiveRegions() =>
        _previewSession?.EffectiveRegions ?? _loadedPreview?.Regions ?? [];

    private void AnchorPlayhead(double progress)
    {
        _anchorProgress = Math.Clamp(progress, 0d, 1d);
        _anchorTickMs = Environment.TickCount64;
        _smoothProgress = _anchorProgress;
    }

    private void ApplyPlaylistButtonColors()
    {
        if (_playlistButtons.Count == 0)
        {
            return;
        }

        // グループ塗り中は全ボタン Invalidate がスウォッチ色の Present を阻害する。
        // 無効化塗りは Form1 同様、ここで LogError を即時載せる必要がある。
        if (_playlistGroupPaintActive)
        {
            return;
        }

        var isPlaying = _audioPlayer.IsPlaying;

        foreach (var (partNumber, button) in _playlistButtons)
        {
            // 通常時は枠なし。遷移待ち点滅と遷移完了グローだけ枠を描く（Form1 同等）。
            button.ApplyIdleStyle();

            var isAutomatic = _automaticPlaylistPlayback
                && (_playingPlaylistPartNumbers.Contains(partNumber)
                    || _activeAutomaticPlaylistPartNumber == partNumber);
            var isManual = !_automaticPlaylistPlayback
                && _manualPlaylistPartNumber == partNumber;
            var isPending = _pendingPlaylistTransitionGeneration != 0
                && _requestedPlaylistPartNumber == partNumber;

            if (isPlaying && isPending)
            {
                button.BorderSize = 2;
                button.BorderColor = WpfControlHelpers.BlendColor(
                    UiColors.PlaylistButtonBorder,
                    UiColors.PlaylistTransitionBorder,
                    _pendingPlaylistBlinkLevel);
                button.Foreground = UiColors.Brush(UiColors.PlaylistActiveFore);
                button.Background = UiColors.Brush(UiColors.PlaylistBack);
            }
            else if (TryGetPlaylistHighlightFadeLevel(partNumber, out var highlightFadeLevel))
            {
                button.Background = WpfControlHelpers.FrozenBrush(
                    WpfControlHelpers.BlendColor(
                        UiColors.PlaylistBack,
                        UiColors.PlaylistAutoBack,
                        highlightFadeLevel));
                button.Foreground = UiColors.Brush(
                    highlightFadeLevel > 0.2d
                        ? UiColors.PlaylistActiveFore
                        : UiColors.PlaylistDefaultFore);
            }
            else if (isPlaying && (isAutomatic || isManual))
            {
                if (isManual)
                {
                    button.ApplyManualStyle();
                }
                else
                {
                    button.ApplyAutoStyle();
                }
            }
            else if (_hoveredPlaylistPartNumber == partNumber
                || _hoveredPlaylistListPartNumber == partNumber)
            {
                button.Foreground = UiColors.Brush(UiColors.PlaylistHoverFore);
            }

            if (_playlistTransitionGlowPartNumber == partNumber
                && _playlistTransitionGlowLevel > 0d)
            {
                button.BorderSize = 2;
                button.BorderColor = WpfControlHelpers.BlendColor(
                    UiColors.PlaylistButtonBorder,
                    isManual ? UiColors.PlaylistManualBorder : UiColors.PlaylistTransitionBorder,
                    _playlistTransitionGlowLevel);
            }

            if (_disabledPartNumbers.Contains(partNumber))
            {
                button.Foreground = UiColors.Brush(UiColors.LogError);
            }

            button.InvalidateVisual();
        }

        EnsureHighlightedPlaylistVisible();
    }

    /// <summary>波形レーン上の高速ホバーで色更新をまとめ、UI スレッドを詰まらせない（Form1 同等）。</summary>
    private void QueuePlaylistHoverColorRefresh()
    {
        if (_playlistHoverColorRefreshQueued || !IsLoaded)
        {
            return;
        }

        _playlistHoverColorRefreshQueued = true;
        Dispatcher.BeginInvoke(() =>
        {
            _playlistHoverColorRefreshQueued = false;
            if (IsLoaded)
            {
                ApplyPlaylistButtonColors();
            }
        });
    }

    /// <summary>再生中／ホバー中パートを Music Playlist スクロール内に見える位置へ寄せる。</summary>
    private void EnsureHighlightedPlaylistVisible()
    {
        // ドラッグ塗り中、またはポインタが一覧上にあるときは自動スクロールしない。
        // BringIntoView が入ると「見えている行」とヒット行がずれ、起点パート（再生中の A など）が塗から外れる。
        if (_playlistGroupPaintActive
            || _playlistDisablePaintActive
            || playlistScrollViewer.IsMouseOver
            || playlistListLayout.IsMouseOver)
        {
            return;
        }

        int? targetPartNumber = null;
        if (_pendingPlaylistTransitionGeneration != 0)
        {
            targetPartNumber = _requestedPlaylistPartNumber;
        }

        targetPartNumber ??= _hoveredPlaylistPartNumber;
        if (targetPartNumber is null && _audioPlayer.IsPlaying)
        {
            targetPartNumber = _automaticPlaylistPlayback
                ? _activeAutomaticPlaylistPartNumber
                : _manualPlaylistPartNumber;
        }

        targetPartNumber ??= _playlistTransitionGlowLevel > 0d
            ? _playlistTransitionGlowPartNumber
            : null;

        if (_lastAutoScrolledPlaylistPartNumber == targetPartNumber)
        {
            return;
        }

        _lastAutoScrolledPlaylistPartNumber = targetPartNumber;
        if (targetPartNumber is not int partNumber
            || !_playlistButtons.TryGetValue(partNumber, out var targetButton))
        {
            return;
        }

        targetButton.BringIntoView();
    }

    private void ClearPlaylistPlaybackSelection()
    {
        _automaticPlaylistPlayback = false;
        _activeAutomaticPlaylistPartNumber = null;
        _requestedPlaylistPartNumber = null;
        _manualPlaylistPartNumber = null;
        _playlistHighlightFades.Clear();
        ClearPendingOverlay();
        _playingPlaylistPartNumbers.Clear();
        _audioPlayer.ClearOverlayPlaylistVoices();
        ClearOverlayPlayheadUi();
        ApplyPlaylistButtonColors();
        UpdateGroupFadeRadioEnabled();
    }

    private void ClearPendingOverlay()
    {
        _pendingOverlayPartNumber = null;
        _pendingOverlayFadeOut = false;
        _pendingOverlayAtSample = 0;
    }

    private void ClearOverlayPlayheadUi()
    {
        _overlayPlayheadProgresses.Clear();
        _overlayFadeOutPlayheadProgresses.Clear();
        _overlayExitPlayheadProgresses.Clear();
        waveformView.SetOverlayPlayheads([]);
        waveformView.SetOverlayFadeOutPlayheads([]);
        waveformView.SetOverlayExitPlayheads([]);
    }

    private void ClearPendingPlaylistUiTransition()
    {
        var wasPending = _pendingPlaylistTransitionGeneration != 0;
        _pendingPlaylistTransitionGeneration = 0;
        _pendingPlaylistBoundarySample = 0;
        _pendingPlaylistSyncBoundarySample = 0;
        _pendingPlaylistTargetSample = 0;
        _pendingPlaylistTargetEntrySample = 0;
        _pendingPlaylistAudioStarted = false;
        _pendingSourceLoopStart = null;
        _pendingSourceLoopEnd = null;
        _requestedPlaylistPartNumber = null;
        _pendingPlaylistBlinkLevel = 0d;
        _playlistBlinkTimer.Stop();
        waveformView.SetAnacrusisPlayhead(null);
        if (wasPending)
        {
            ApplyPlaylistButtonColors();
        }
    }

    private void ClearPlaylistTransitionGlow()
    {
        _playlistTransitionGlowTimer.Stop();
        _playlistTransitionGlowPartNumber = null;
        _playlistTransitionGlowStartTickMs = 0;
        _playlistTransitionGlowDurationMs = 0d;
        _playlistTransitionGlowLevel = 0d;
        ApplyPlaylistButtonColors();
    }

    private bool SyncPlayingPlaylistPartNumbersFromPlayer()
    {
        _playingPlaylistPartNumbersSyncScratch.Clear();
        if (_audioPlayer.HasClockPlaylistRange)
        {
            var clockId = _audioPlayer.GetClockPlaylistVoiceId();
            if (clockId != 0)
            {
                _playingPlaylistPartNumbersSyncScratch.Add(clockId);
            }
            else if (_automaticPlaylistPlayback
                     && _activeAutomaticPlaylistPartNumber is int activeFallback)
            {
                // クロック範囲はあるが voiceId 未反映の瞬間でも塗りを落とさない
                _playingPlaylistPartNumbersSyncScratch.Add(activeFallback);
            }
        }
        else if (_automaticPlaylistPlayback
                 && _audioPlayer.IsPlaying
                 && _activeAutomaticPlaylistPartNumber is int activePart)
        {
            // 遷移直後など HasClockPlaylistRange が一瞬 false でも着色を維持
            _playingPlaylistPartNumbersSyncScratch.Add(activePart);
        }

        var overlayCount = _audioPlayer.CopyActiveOverlayPlaylistVoiceIds(_overlayVoiceIdScratch);
        for (var i = 0; i < overlayCount; i++)
        {
            _playingPlaylistPartNumbersSyncScratch.Add(_overlayVoiceIdScratch[i]);
        }

        if (_playingPlaylistPartNumbers.SetEquals(_playingPlaylistPartNumbersSyncScratch))
        {
            return false;
        }

        _playingPlaylistPartNumbers.Clear();
        foreach (var id in _playingPlaylistPartNumbersSyncScratch)
        {
            _playingPlaylistPartNumbers.Add(id);
        }

        return true;
    }

    private WaveformOutputPart? TryGetOutputPart(int partNumber)
    {
        var part = GetEffectiveOutputParts().FirstOrDefault(p => p.Number == partNumber);
        return part.Number == partNumber ? part : null;
    }

    private WaveformOutputPart? TryGetOutputPartAtProgress(double progress)
    {
        if (_loadedPreview is not { } preview || preview.WavInfo.FrameCount <= 0)
        {
            return null;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var sample = (long)Math.Clamp(
            Math.Floor(Math.Clamp(progress, 0d, 1d) * frameCount),
            0d,
            Math.Max(0L, frameCount - 1));
        var part = GetEffectiveOutputParts()
            .FirstOrDefault(p => sample >= p.StartSampleOffset && sample < p.EndSampleOffset);
        return sample >= part.StartSampleOffset && sample < part.EndSampleOffset ? part : null;
    }

    private WaveformOutputPart? ResolveClockPlaylistPart()
    {
        var trackedNumber = _automaticPlaylistPlayback
            ? _activeAutomaticPlaylistPartNumber
            : _manualPlaylistPartNumber;
        if (trackedNumber is int number && TryGetOutputPart(number) is { } trackedPart)
        {
            return trackedPart;
        }

        return TryGetOutputPartAtProgress(_smoothProgress);
    }

    private void SetManualPlaylistHighlight(double progress)
    {
        if (_loadedPreview is not { } preview || preview.WavInfo.FrameCount <= 0)
        {
            return;
        }

        var partNumber = TryGetOutputPartAtProgress(progress)?.Number;
        if (!_automaticPlaylistPlayback && _manualPlaylistPartNumber == partNumber)
        {
            return;
        }

        if (_audioPlayer.ActiveOverlayPlaylistVoiceCount > 0
            || _playingPlaylistPartNumbers.Count > 1)
        {
            return;
        }

        _automaticPlaylistPlayback = false;
        _activeAutomaticPlaylistPartNumber = null;
        _requestedPlaylistPartNumber = null;
        _manualPlaylistPartNumber = partNumber;
        _playingPlaylistPartNumbers.Clear();
        _audioPlayer.ClearOverlayPlaylistVoices();
        ApplyPlaylistButtonColors();
        UpdateGroupFadeRadioEnabled();
    }

    private PlaylistExitSourceMode ResolveExitSourceMode(int partNumber)
    {
        var mode = _partExitSourceModes.GetValueOrDefault(partNumber, PlaylistExitSourceMode.Immediate);
        return NormalizeExitSourceModeForCurrentWave(mode);
    }

    private PlaylistExitSourceMode ResolveChangeOccursAtMode(int partNumber)
    {
        var mode = _partChangeOccursAtModes.GetValueOrDefault(partNumber, PlaylistExitSourceMode.Immediate);
        return NormalizeExitSourceModeForCurrentWave(mode);
    }

    private static PlaylistExitSourceMode NormalizeExitSourceModeForCurrentWave(
        PlaylistExitSourceMode mode,
        bool waveOnly)
    {
        if (waveOnly && mode is PlaylistExitSourceMode.NextBar or PlaylistExitSourceMode.NextBeat)
        {
            return PlaylistExitSourceMode.Immediate;
        }

        return mode;
    }

    private PlaylistExitSourceMode NormalizeExitSourceModeForCurrentWave(PlaylistExitSourceMode mode) =>
        NormalizeExitSourceModeForCurrentWave(mode, _previewSession?.AllowsSessionMarkerEdit == true);

    private bool ResolvePlayPostExit(int partNumber) =>
        _partPlayPostExit.GetValueOrDefault(partNumber, true);

    private bool ResolveAdditiveLayers(int partNumber) =>
        _partAdditiveLayers.GetValueOrDefault(partNumber, false);

    private double ResolveGroupFadeSeconds(int partNumber) =>
        _partGroupFadeSeconds.GetValueOrDefault(partNumber);

    private (double FadeInSeconds, double FadeOutSeconds) ResolveTransitionFadeSeconds(
        int targetPartNumber,
        PlaylistDestinationSyncMode destinationSyncMode)
    {
        if (destinationSyncMode == PlaylistDestinationSyncMode.SameTime)
        {
            var groupFade = _partGroupFadeSeconds.GetValueOrDefault(targetPartNumber);
            return (groupFade, groupFade);
        }

        return (
            _partFadeInSeconds.GetValueOrDefault(targetPartNumber),
            _partFadeOutSeconds.GetValueOrDefault(targetPartNumber));
    }

    private void ApplyPlayExitLayerForCurrentPlayback()
    {
        var part = ResolveClockPlaylistPart()?.Number
            ?? TryGetOutputPartAtProgress(_smoothProgress)?.Number
            ?? _selectedPlaylistPartNumber
            ?? _activeAutomaticPlaylistPartNumber
            ?? _manualPlaylistPartNumber;
        _audioPlayer.PlayExitLayer = part is int number
            ? ResolvePlayPostExit(number)
            : true;
    }

    private void FadeOutPlayingGroupOverlays(double fadeOutSeconds)
    {
        var count = _audioPlayer.CopyActiveOverlayPlaylistVoiceIds(_overlayVoiceIdScratch);
        if (count == 0)
        {
            return;
        }

        _audioPlayer.FadeOutAllOverlayPlaylistVoices(fadeOutSeconds);
        if (SyncPlayingPlaylistPartNumbersFromPlayer())
        {
            ApplyPlaylistButtonColors();
        }
    }

    /// <summary>
    /// Additive Layers が有効な同一グループ内クリックは、通常遷移ではなく重ね再生にする。
    /// グループ外クリックや Alt+クリック強制は <see cref="RequestPlaylistOverlayToggle"/> を直接呼ぶ。
    /// </summary>
    private bool ShouldUseAdditiveLayerClick(WaveformOutputPart target)
    {
        if (!ResolveAdditiveLayers(target.Number)
            || !_audioPlayer.IsPlaying
            || _disabledPartNumbers.Contains(target.Number)
            || !_partGroupIds.TryGetValue(target.Number, out var targetGroupId))
        {
            return false;
        }

        foreach (var playingPartNumber in _playingPlaylistPartNumbers)
        {
            if (_partGroupIds.TryGetValue(playingPartNumber, out var playingGroupId)
                && playingGroupId == targetGroupId)
            {
                return true;
            }
        }

        if (_manualPlaylistPartNumber is int manualPart
            && !_disabledPartNumbers.Contains(manualPart)
            && _partGroupIds.TryGetValue(manualPart, out var manualGroupId)
            && manualGroupId == targetGroupId)
        {
            return true;
        }

        if (_automaticPlaylistPlayback
            && _activeAutomaticPlaylistPartNumber is int activePart
            && !_disabledPartNumbers.Contains(activePart)
            && _partGroupIds.TryGetValue(activePart, out var activeGroupId)
            && activeGroupId == targetGroupId)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// グループ重ね再生（Additive / Alt+クリック）。再生中の同一グループへ上乗せ／フェードアウトする。
    /// Change Occurs At が Immediate 以外なら境界まで予約する。
    /// </summary>
    private void RequestPlaylistOverlayToggle(WaveformOutputPart target)
    {
        if (_loadedPreview is not { } preview
            || !_audioPlayer.HasSource
            || !_audioPlayer.IsPlaying
            || preview.WavInfo.FrameCount <= 0
            || _disabledPartNumbers.Contains(target.Number))
        {
            return;
        }

        if (IsPlaylistLayerVoiceActive(target.Number)
            || _playingPlaylistPartNumbers.Contains(target.Number))
        {
            RequestPlaylistOverlayFadeOut(target);
            return;
        }

        if (!_partGroupIds.TryGetValue(target.Number, out var targetGroupId)
            || !TryEnsureOverlayClockContext(targetGroupId, out var clockPartNumber))
        {
            return;
        }

        if (clockPartNumber == target.Number)
        {
            ApplyPlaylistButtonColors();
            UpdateGroupFadeRadioEnabled();
            return;
        }

        // 最初の上乗せは Immediate、2本目以降は Change Occurs At に従う。
        var changeOccursMode = _audioPlayer.ActiveOverlayPlaylistVoiceCount == 0
            ? PlaylistExitSourceMode.Immediate
            : ResolveChangeOccursAtMode(target.Number);
        if (changeOccursMode != PlaylistExitSourceMode.Immediate
            && TrySchedulePendingOverlay(target.Number, fadeOut: false, clockPartNumber, changeOccursMode))
        {
            return;
        }

        CommitPlaylistOverlayAdd(target, clockPartNumber);
    }

    private void RequestPlaylistOverlayFadeOut(WaveformOutputPart target)
    {
        var changeOccursMode = ResolveChangeOccursAtMode(target.Number);
        if (changeOccursMode != PlaylistExitSourceMode.Immediate
            && _partGroupIds.TryGetValue(target.Number, out var targetGroupId)
            && TryEnsureOverlayClockContext(targetGroupId, out var clockPartNumber)
            && TrySchedulePendingOverlay(target.Number, fadeOut: true, clockPartNumber, changeOccursMode))
        {
            return;
        }

        CommitPlaylistOverlayFadeOut(target);
    }

    private bool TrySchedulePendingOverlay(
        int targetPartNumber,
        bool fadeOut,
        int clockPartNumber,
        PlaylistExitSourceMode changeOccursMode)
    {
        if (_loadedPreview is not { } preview
            || preview.WavInfo.FrameCount <= 0
            || TryGetOutputPart(clockPartNumber) is not { } clockPart)
        {
            return false;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var currentSample = Math.Clamp(
            (long)Math.Floor(_smoothProgress * frameCount),
            0L,
            Math.Max(0L, frameCount - 1));
        var markers = _previewSession?.EffectiveMarkers ?? preview.Markers;
        var regions = GetEffectiveRegions();
        var transitionLimit = clockPart.EndSampleOffset;
        var hasActiveLoop = _audioPlayer.TryGetActiveLoopProgress(
            out _,
            out var loopEndProgress);
        if (hasActiveLoop)
        {
            var loopEnd = (long)Math.Round(loopEndProgress * frameCount);
            if (loopEnd > currentSample)
            {
                transitionLimit = Math.Min(transitionLimit, loopEnd);
            }
        }

        var boundaries = GetPlaylistExitBoundaries(
            preview,
            markers,
            regions,
            changeOccursMode,
            currentSample,
            clockPart.StartSampleOffset,
            clockPart.EndSampleOffset,
            transitionLimit,
            hasActiveLoop);
        if (boundaries.Count == 0)
        {
            return false;
        }

        _pendingOverlayPartNumber = targetPartNumber;
        _pendingOverlayFadeOut = fadeOut;
        _pendingOverlayAtSample = boundaries[0];
        return true;
    }

    private void TryCommitPendingOverlay()
    {
        if (_pendingOverlayPartNumber is not int partNumber
            || _loadedPreview is not { } preview
            || preview.WavInfo.FrameCount <= 0)
        {
            return;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var currentSample = Math.Clamp(
            (long)Math.Floor(_smoothProgress * frameCount),
            0L,
            Math.Max(0L, frameCount - 1));
        if (currentSample < _pendingOverlayAtSample)
        {
            return;
        }

        var fadeOut = _pendingOverlayFadeOut;
        ClearPendingOverlay();
        if (TryGetOutputPart(partNumber) is not { } target)
        {
            return;
        }

        if (fadeOut)
        {
            CommitPlaylistOverlayFadeOut(target);
            return;
        }

        if (!_partGroupIds.TryGetValue(target.Number, out var targetGroupId)
            || !TryEnsureOverlayClockContext(targetGroupId, out var clockPartNumber)
            || clockPartNumber == target.Number)
        {
            return;
        }

        CommitPlaylistOverlayAdd(target, clockPartNumber);
    }

    private void CommitPlaylistOverlayAdd(WaveformOutputPart target, int clockPartNumber)
    {
        ClearPendingOverlay();
        var fadeInSeconds = ResolveGroupFadeSeconds(target.Number);
        if (!_audioPlayer.TryAddOverlayPlaylistVoice(
                target.Number,
                target.StartSampleOffset,
                target.EndSampleOffset,
                fadeInSeconds,
                out _))
        {
            return;
        }

        _automaticPlaylistPlayback = true;
        _manualPlaylistPartNumber = null;
        _activeAutomaticPlaylistPartNumber = clockPartNumber;
        _playingPlaylistPartNumbers.Add(clockPartNumber);
        _playingPlaylistPartNumbers.Add(target.Number);
        if (fadeInSeconds > 0d)
        {
            StartPlaylistHighlightFade(target.Number, fadeInSeconds, fadeIn: true);
        }
        else
        {
            _playlistHighlightFades.Remove(target.Number);
        }

        UpdateOverlayPlayheads(recordTrail: false);
        ApplyPlaylistButtonColors();
        UpdateGroupFadeRadioEnabled();
        StartPlaylistTransitionGlow(target.Number);
    }

    private void CommitPlaylistOverlayFadeOut(WaveformOutputPart target)
    {
        ClearPendingOverlay();
        var fadeOutSeconds = ResolveGroupFadeSeconds(target.Number);
        if (_audioPlayer.HasClockPlaylistRange
            && _audioPlayer.GetClockPlaylistVoiceId() == target.Number)
        {
            if (!_audioPlayer.TryFadeOutClockPlaylistVoice(
                    fadeOutSeconds,
                    out var promotedVoiceId,
                    out _))
            {
                return;
            }

            if (promotedVoiceId is int promoted)
            {
                _automaticPlaylistPlayback = true;
                _activeAutomaticPlaylistPartNumber = promoted;
                _manualPlaylistPartNumber = null;
                SyncUiPlayheadToCurrentMainSample();
            }

            SyncPlayingPlaylistPartNumbersFromPlayer();
        }
        else
        {
            if (!_audioPlayer.TryFadeOutOverlayPlaylistVoice(target.Number, fadeOutSeconds))
            {
                return;
            }

            SyncPlayingPlaylistPartNumbersFromPlayer();
        }

        if (fadeOutSeconds > 0d)
        {
            StartPlaylistHighlightFade(target.Number, fadeOutSeconds, fadeIn: false);
        }
        else
        {
            _playlistHighlightFades.Remove(target.Number);
        }

        ApplyPlaylistButtonColors();
        UpdateGroupFadeRadioEnabled();
        StartPlaylistTransitionGlow(target.Number);
    }

    private void StartPlaylistHighlightFade(int partNumber, double seconds, bool fadeIn)
    {
        if (seconds <= 0d)
        {
            _playlistHighlightFades.Remove(partNumber);
            return;
        }

        _playlistHighlightFades[partNumber] = (
            Environment.TickCount64,
            seconds * 1000d,
            fadeIn);
    }

    private bool TryGetPlaylistHighlightFadeLevel(int partNumber, out double level)
    {
        level = 0d;
        if (!_playlistHighlightFades.TryGetValue(partNumber, out var fade))
        {
            return false;
        }

        var elapsed = Math.Max(0L, Environment.TickCount64 - fade.StartTickMs);
        var t = Math.Clamp(elapsed / Math.Max(1d, fade.DurationMs), 0d, 1d);
        if (t >= 1d)
        {
            _playlistHighlightFades.Remove(partNumber);
            return false;
        }

        level = fade.FadeIn ? t : 1d - t;
        return true;
    }

    private void UpdatePlaylistHighlightFades()
    {
        if (_playlistHighlightFades.Count == 0)
        {
            return;
        }

        var now = Environment.TickCount64;
        List<int>? completed = null;
        foreach (var (partNumber, fade) in _playlistHighlightFades)
        {
            if (now - fade.StartTickMs < fade.DurationMs)
            {
                continue;
            }

            completed ??= [];
            completed.Add(partNumber);
        }

        if (completed is not null)
        {
            foreach (var partNumber in completed)
            {
                _playlistHighlightFades.Remove(partNumber);
            }
        }

        ApplyPlaylistButtonColors();
    }

    private void SyncUiPlayheadToCurrentMainSample()
    {
        if (_loadedPreview is not { } preview || preview.WavInfo.FrameCount <= 0)
        {
            return;
        }

        var progress = Math.Clamp(
            _audioPlayer.CurrentMainSample / (double)preview.WavInfo.FrameCount,
            0d,
            1d);
        AnchorPlayhead(progress);
        _smoothProgress = progress;
        waveformView.ClearPlayheadTrail();
        waveformView.SetPlayhead(progress, recordTrail: false, ensureVisible: true);
        _audioPlayer.ArmLoopAtProgress(progress);
    }

    private bool IsPlaylistLayerVoiceActive(int partNumber)
    {
        if (_audioPlayer.HasClockPlaylistRange
            && _audioPlayer.GetClockPlaylistVoiceId() == partNumber)
        {
            return true;
        }

        return _audioPlayer.HasOverlayPlaylistVoice(partNumber);
    }

    /// <summary>
    /// 上乗せのためのクロック Playlist 範囲を確保する。
    /// 既に Provider にクロックがあればそれを使い、なければ Space 再生中パート等を adopt する。
    /// </summary>
    private bool TryEnsureOverlayClockContext(int groupId, out int clockPartNumber)
    {
        clockPartNumber = 0;
        if (!_audioPlayer.HasSource || !_audioPlayer.IsPlaying)
        {
            return false;
        }

        if (_audioPlayer.HasClockPlaylistRange)
        {
            var existingVoiceId = _audioPlayer.GetClockPlaylistVoiceId();
            WaveformOutputPart? existingPart = existingVoiceId != 0
                ? TryGetOutputPart(existingVoiceId)
                : null;
            existingPart ??= _activeAutomaticPlaylistPartNumber is int active
                ? TryGetOutputPart(active)
                : null;
            existingPart ??= ResolveClockPlaylistPart();

            if (existingPart is { } clock
                && _partGroupIds.TryGetValue(clock.Number, out var existingGroupId)
                && existingGroupId == groupId)
            {
                if (existingVoiceId == 0)
                {
                    _audioPlayer.SetClockPlaylistVoiceId(clock.Number);
                }

                clockPartNumber = clock.Number;
                _automaticPlaylistPlayback = true;
                _activeAutomaticPlaylistPartNumber = clock.Number;
                _manualPlaylistPartNumber = null;
                _playingPlaylistPartNumbers.Add(clock.Number);
                return true;
            }
        }

        WaveformOutputPart? sourcePart = null;
        if (_manualPlaylistPartNumber is int manualPart
            && !_disabledPartNumbers.Contains(manualPart)
            && _partGroupIds.TryGetValue(manualPart, out var manualGroupId)
            && manualGroupId == groupId)
        {
            sourcePart = TryGetOutputPart(manualPart);
        }

        if (sourcePart is null
            && _automaticPlaylistPlayback
            && _activeAutomaticPlaylistPartNumber is int activePart
            && !_disabledPartNumbers.Contains(activePart)
            && _partGroupIds.TryGetValue(activePart, out var activeGroupId)
            && activeGroupId == groupId)
        {
            sourcePart = TryGetOutputPart(activePart);
        }

        sourcePart ??= ResolveClockPlaylistPart();
        if (sourcePart is not { } adopted
            || !_partGroupIds.TryGetValue(adopted.Number, out var clockGroupId)
            || clockGroupId != groupId)
        {
            return false;
        }

        if (!_audioPlayer.TryAdoptClockPlaylistRange(
                adopted.StartSampleOffset,
                adopted.EndSampleOffset,
                adopted.Number))
        {
            if (_activeAutomaticPlaylistPartNumber == adopted.Number
                || _manualPlaylistPartNumber == adopted.Number)
            {
                _audioPlayer.SetClockPlaylistVoiceId(adopted.Number);
                if (!_audioPlayer.TryAdoptClockPlaylistRange(
                        adopted.StartSampleOffset,
                        adopted.EndSampleOffset,
                        adopted.Number))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        _automaticPlaylistPlayback = true;
        _activeAutomaticPlaylistPartNumber = adopted.Number;
        _manualPlaylistPartNumber = null;
        _playingPlaylistPartNumbers.Add(adopted.Number);
        clockPartNumber = adopted.Number;
        return true;
    }

    private void UpdateOverlayPlayheads(bool recordTrail)
    {
        if (SyncPlayingPlaylistPartNumbersFromPlayer())
        {
            ApplyPlaylistButtonColors();
        }

        FillOverlayProgressList(
            _audioPlayer.CopyOverlayPlaylistVoiceProgresses(
                _overlayProgressScratch,
                _overlayVoiceIdScratch),
            _overlayProgressScratch,
            _overlayPlayheadProgresses);
        waveformView.SetOverlayPlayheads(_overlayPlayheadProgresses, recordTrail);

        FillOverlayProgressList(
            _audioPlayer.CopyOverlayFadeOutProgresses(
                _overlayFadeOutProgressScratch,
                _overlayFadeOutVoiceIdScratch),
            _overlayFadeOutProgressScratch,
            _overlayFadeOutPlayheadProgresses);
        waveformView.SetOverlayFadeOutPlayheads(_overlayFadeOutPlayheadProgresses, recordTrail);

        FillOverlayProgressList(
            _audioPlayer.CopyOverlayExitProgresses(
                _overlayExitProgressScratch,
                _overlayExitVoiceIdScratch),
            _overlayExitProgressScratch,
            _overlayExitPlayheadProgresses);
        waveformView.SetOverlayExitPlayheads(_overlayExitPlayheadProgresses, recordTrail);
    }

    private static void FillOverlayProgressList(
        int count,
        double[] source,
        List<double> destination)
    {
        destination.Clear();
        for (var i = 0; i < count; i++)
        {
            destination.Add(source[i]);
        }
    }

    private void RequestPlaylistPlayback(WaveformOutputPart target)
    {
        if (_loadedPreview is not { } preview
            || !_audioPlayer.HasSource
            || preview.WavInfo.FrameCount <= 0
            || _disabledPartNumbers.Contains(target.Number))
        {
            return;
        }

        var frameCount = preview.WavInfo.FrameCount;
        if (!_audioPlayer.IsPlaying)
        {
            ClearPendingOverlay();
            ClearPendingPlaylistUiTransition();
            _audioPlayer.CancelPlaylistTransition();
            _audioPlayer.ClearOverlayPlaylistVoices();
            ClearOverlayPlayheadUi();
            _audioPlayer.PlayExitLayer = ResolvePlayPostExit(target.Number);
            if (!_audioPlayer.StartPlaylistRange(target.StartSampleOffset, target.EndSampleOffset, target.Number))
            {
                ClearPlaylistPlaybackSelection();
                return;
            }

            _automaticPlaylistPlayback = true;
            _activeAutomaticPlaylistPartNumber = target.Number;
            _requestedPlaylistPartNumber = null;
            _manualPlaylistPartNumber = null;
            _playingPlaylistPartNumbers.Clear();
            _playingPlaylistPartNumbers.Add(target.Number);
            var progress = target.StartSampleOffset / (double)frameCount;
            _lastPlaybackStartProgress = progress;
            AnchorPlayhead(progress);
            SeekPlayback(progress, ensureVisible: true);
            waveformView.SetExitPlayhead(null);
            waveformView.SetFadeOutPlayhead(null);
            if (!_audioPlayer.IsPlaying)
            {
                _audioPlayer.Play();
            }

            _playheadTimer.Start();
            UpdateTransportPlaybackState();
            StartPlaylistTransitionGlow();
            ApplyPlaylistButtonColors();
            UpdateGroupFadeRadioEnabled();
            return;
        }

        _requestedPlaylistPartNumber = target.Number;
        ApplyPlaylistButtonColors();
        UpdateGroupFadeRadioEnabled();

        var currentSample = Math.Clamp(
            (long)Math.Floor(_smoothProgress * frameCount),
            0L,
            Math.Max(0L, frameCount - 1));
        var outputParts = GetEffectiveOutputParts();
        var regions = GetEffectiveRegions();
        var markers = _previewSession?.EffectiveMarkers ?? preview.Markers;
        var currentPart = ResolveClockPlaylistPart()
            ?? GetEffectiveOutputParts()
                .Where(p => currentSample >= p.StartSampleOffset && currentSample < p.EndSampleOffset)
                .Select(p => (WaveformOutputPart?)p)
                .FirstOrDefault();
        var currentPartStart = currentPart?.StartSampleOffset ?? 0L;
        var currentPartEnd = currentPart?.EndSampleOffset ?? frameCount;

        var clockVoiceId = _audioPlayer.GetClockPlaylistVoiceId();
        var targetIsCurrentClock = clockVoiceId != 0
            ? clockVoiceId == target.Number
            : currentPart?.Number == target.Number;

        if (targetIsCurrentClock)
        {
            var destinationSyncModeForCollapse =
                ResolvePlaylistDestinationSyncMode(currentPart, target);
            var hadOverlays = _audioPlayer.ActiveOverlayPlaylistVoiceCount > 0;
            if (hadOverlays)
            {
                FadeOutPlayingGroupOverlays(
                    ResolveTransitionFadeSeconds(target.Number, destinationSyncModeForCollapse).FadeOutSeconds);
                _automaticPlaylistPlayback = true;
                _activeAutomaticPlaylistPartNumber = target.Number;
                _manualPlaylistPartNumber = null;
                _requestedPlaylistPartNumber = null;
                SyncPlayingPlaylistPartNumbersFromPlayer();
                ApplyPlaylistButtonColors();
                UpdateGroupFadeRadioEnabled();
                return;
            }

            _requestedPlaylistPartNumber = null;
            ApplyPlaylistButtonColors();
            UpdateGroupFadeRadioEnabled();
            return;
        }

        var destinationSyncMode = ResolvePlaylistDestinationSyncMode(currentPart, target);
        FadeOutPlayingGroupOverlays(
            ResolveTransitionFadeSeconds(target.Number, destinationSyncMode).FadeOutSeconds);

        var transitionLimit = currentPartEnd;
        var hasActiveLoop = _audioPlayer.TryGetActiveLoopProgress(out _, out var loopEndProgress);
        if (hasActiveLoop)
        {
            var loopEnd = (long)Math.Round(loopEndProgress * frameCount);
            if (loopEnd > currentSample)
            {
                transitionLimit = Math.Min(transitionLimit, loopEnd);
            }
        }

        var anacrusisFrames =
            destinationSyncMode == PlaylistDestinationSyncMode.EntryCue
                ? GetLeadingAnacrusisFrameCount(regions, target)
                : 0L;
        var exitSourceMode = destinationSyncMode == PlaylistDestinationSyncMode.SameTime
            ? ResolveChangeOccursAtMode(target.Number)
            : ResolveExitSourceMode(target.Number);
        var boundaries = GetPlaylistExitBoundaries(
            preview,
            markers,
            regions,
            exitSourceMode,
            currentSample,
            currentPartStart,
            currentPartEnd,
            transitionLimit,
            hasActiveLoop);

        if (exitSourceMode == PlaylistExitSourceMode.Immediate)
        {
            if (TrySchedulePlaylistTransition(
                    target,
                    currentPartStart,
                    currentPartEnd,
                    anacrusisFrames,
                    sourceExitSample: null,
                    allowShortPreRoll: true,
                    exitSourceMode,
                    destinationSyncMode,
                    out var terminalFailure))
            {
                return;
            }

            if (terminalFailure)
            {
                _requestedPlaylistPartNumber = null;
                ApplyPlaylistButtonColors();
            }
        }
        else
        {
            var allowShortPreRoll =
                exitSourceMode is PlaylistExitSourceMode.NextCue
                    or PlaylistExitSourceMode.ExitCue;
            foreach (var boundary in boundaries)
            {
                if (TrySchedulePlaylistTransition(
                        target,
                        currentPartStart,
                        currentPartEnd,
                        anacrusisFrames,
                        boundary,
                        allowShortPreRoll,
                        exitSourceMode,
                        destinationSyncMode,
                        out var candidateTerminalFailure))
                {
                    return;
                }

                if (candidateTerminalFailure)
                {
                    _requestedPlaylistPartNumber = null;
                    ApplyPlaylistButtonColors();
                    return;
                }
            }

            var fallbackTerminalFailure = false;
            if (exitSourceMode == PlaylistExitSourceMode.ExitCue
                && boundaries.Count > 0
                && boundaries[0] <= _audioPlayer.CurrentMainSample
                && TrySchedulePlaylistTransition(
                    target,
                    currentPartStart,
                    currentPartEnd,
                    anacrusisFrames,
                    sourceExitSample: null,
                    allowShortPreRoll: true,
                    exitSourceMode,
                    destinationSyncMode,
                    out fallbackTerminalFailure))
            {
                return;
            }

            if (fallbackTerminalFailure)
            {
                _requestedPlaylistPartNumber = null;
                ApplyPlaylistButtonColors();
                return;
            }
        }

        AppendReport(UiStrings.LogPlaylistScheduleFailed(target.FileName) + Environment.NewLine);
        _requestedPlaylistPartNumber = null;
        ApplyPlaylistButtonColors();
    }

    private PlaylistDestinationSyncMode ResolvePlaylistDestinationSyncMode(
        WaveformOutputPart? current,
        WaveformOutputPart target)
    {
        if (current is not { } currentPart
            || !_partGroupIds.TryGetValue(currentPart.Number, out var currentGroupId)
            || !_partGroupIds.TryGetValue(target.Number, out var targetGroupId))
        {
            return PlaylistDestinationSyncMode.EntryCue;
        }

        return currentGroupId == targetGroupId
            ? PlaylistDestinationSyncMode.SameTime
            : PlaylistDestinationSyncMode.EntryCue;
    }

    private bool TrySchedulePlaylistTransition(
        WaveformOutputPart target,
        long currentPartStart,
        long currentPartEnd,
        long anacrusisFrames,
        long? sourceExitSample,
        bool allowShortPreRoll,
        PlaylistExitSourceMode exitSourceMode,
        PlaylistDestinationSyncMode destinationSyncMode,
        out bool terminalFailure)
    {
        terminalFailure = false;
        var (fadeInSeconds, fadeOutSeconds) = ResolveTransitionFadeSeconds(
            target.Number,
            destinationSyncMode);
        if (!_audioPlayer.TrySchedulePlaylistTransition(
                target.StartSampleOffset,
                target.EndSampleOffset,
                sourceExitSample,
                currentPartStart,
                destinationSyncMode,
                anacrusisFrames,
                allowShortPreRoll,
                currentPartEnd,
                fadeInSeconds,
                fadeOutSeconds,
                out var schedule))
        {
            if (schedule.RejectionReason == "same-time-out-of-range")
            {
                terminalFailure = true;
                var targetDuration = target.EndSampleOffset - target.StartSampleOffset;
                AppendReport(
                    UiStrings.LogSameTimeOutOfRange(
                        target.FileName,
                        schedule.SourceRelativeSample,
                        targetDuration)
                    + Environment.NewLine);
            }

            return false;
        }

        SetPendingPlaylistUiTransition(
            schedule.Generation,
            schedule.TriggerSample,
            schedule.SyncBoundarySample,
            target.StartSampleOffset,
            schedule.TargetSwitchSample);

        if (schedule.StartedImmediately
            && schedule.TriggerSample == schedule.SyncBoundarySample)
        {
            CommitPendingPlaylistUiTransition(
                schedule.SyncBoundarySample,
                schedule.TargetSwitchSample,
                "immediate");
        }

        return true;
    }

    private void SetPendingPlaylistUiTransition(
        long generation,
        long triggerSample,
        long syncBoundarySample,
        long targetSample,
        long targetEntrySample)
    {
        _pendingPlaylistTransitionGeneration = generation;
        _pendingPlaylistBoundarySample = triggerSample;
        _pendingPlaylistSyncBoundarySample = syncBoundarySample;
        _pendingPlaylistTargetSample = targetSample;
        _pendingPlaylistTargetEntrySample = targetEntrySample;
        _pendingPlaylistAudioStarted = false;
        if (_audioPlayer.TryGetActiveLoopProgress(out var loopStart, out var loopEnd))
        {
            _pendingSourceLoopStart = loopStart;
            _pendingSourceLoopEnd = loopEnd;
        }
        else
        {
            _pendingSourceLoopStart = null;
            _pendingSourceLoopEnd = null;
        }

        AnchorPlayhead(_smoothProgress);
        waveformView.SetAnacrusisPlayhead(null);
        _pendingPlaylistBlinkLevel = GetPlaylistBeatBlinkLevel();
        ApplyPlaylistButtonColors();
        _playlistBlinkTimer.Start();
    }

    private double CommitPendingPlaylistUiTransition(
        long oldTimelineSample,
        long targetTimelineSample,
        string reason)
    {
        if (_loadedPreview is not { } preview
            || preview.WavInfo.FrameCount <= 0
            || _pendingPlaylistTransitionGeneration == 0)
        {
            return _smoothProgress;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var progress = Math.Clamp(targetTimelineSample / (double)frameCount, 0d, 1d);
        AnchorPlayhead(progress);
        waveformView.ClearPlayheadTrail();
        waveformView.SetPlayhead(progress, recordTrail: false, ensureVisible: true);
        _activeAutomaticPlaylistPartNumber =
            _requestedPlaylistPartNumber
            ?? GetEffectiveOutputParts()
                .Where(part =>
                    _pendingPlaylistTargetSample >= part.StartSampleOffset
                    && _pendingPlaylistTargetSample < part.EndSampleOffset)
                .Select(part => (int?)part.Number)
                .FirstOrDefault();
        _automaticPlaylistPlayback = true;
        _manualPlaylistPartNumber = null;
        _playingPlaylistPartNumbers.Clear();
        if (_activeAutomaticPlaylistPartNumber is int committedPartNumber)
        {
            _playingPlaylistPartNumbers.Add(committedPartNumber);
            _audioPlayer.SetClockPlaylistVoiceId(committedPartNumber);
            ApplyPlayExitLayerForCurrentPlayback();
        }

        _audioPlayer.ClearOverlayPlaylistVoices();
        ApplyPlaylistButtonColors();
        StartPlaylistTransitionGlow();
        ClearPendingPlaylistUiTransition();
        UpdateGroupFadeRadioEnabled();
        return progress;
    }

    private void UpdatePendingPlaylistBlink()
    {
        if (_pendingPlaylistTransitionGeneration == 0)
        {
            return;
        }

        var level = GetPlaylistBeatBlinkLevel();
        if (Math.Abs(level - _pendingPlaylistBlinkLevel) < 0.005d)
        {
            return;
        }

        _pendingPlaylistBlinkLevel = level;
        ApplyPlaylistButtonColors();
    }

    private double GetPlaylistBeatBlinkLevel()
    {
        if (!TryGetPlaylistBeatTiming(out var beatPhase, out _))
        {
            return 0.5d;
        }

        return 0.75d + 0.25d * Math.Cos(beatPhase * Math.PI * 2d);
    }

    private bool TryGetPlaylistBeatTiming(out double beatPhase, out double beatDurationMs)
    {
        beatPhase = 0d;
        beatDurationMs = 0d;
        if (_loadedPreview is not { } preview
            || preview.WavInfo.FrameCount <= 0
            || preview.WavInfo.SampleRate == 0)
        {
            return false;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var sample = (long)Math.Clamp(
            Math.Floor(Math.Clamp(_smoothProgress, 0d, 1d) * frameCount),
            0d,
            Math.Max(0L, frameCount - 1));
        var bar = preview.Bars
            .Where(mark => !mark.IsTempoChangeOnly && mark.SampleOffset <= sample)
            .OrderBy(mark => mark.SampleOffset)
            .LastOrDefault();
        var tempo = preview.Bars
            .Where(mark => mark.SampleOffset <= sample)
            .OrderBy(mark => mark.SampleOffset)
            .LastOrDefault();

        var bpm = tempo.Bpm > 0d ? tempo.Bpm : bar.Bpm;
        var denominator = tempo.Denominator > 0 ? tempo.Denominator : bar.Denominator;
        if (bpm <= 0d || denominator <= 0)
        {
            return false;
        }

        var beatSamples = preview.WavInfo.SampleRate * 60d / bpm * 4d / denominator;
        if (beatSamples <= 1d)
        {
            return false;
        }

        var relativeBeats = Math.Max(0d, sample - bar.SampleOffset) / beatSamples;
        beatPhase = relativeBeats - Math.Floor(relativeBeats);
        beatDurationMs = beatSamples / preview.WavInfo.SampleRate * 1000d;
        return true;
    }

    private void StartPlaylistTransitionGlow(int? partNumberOverride = null)
    {
        var activePartNumber = partNumberOverride
            ?? (_automaticPlaylistPlayback
                ? _activeAutomaticPlaylistPartNumber
                : _manualPlaylistPartNumber);
        if (activePartNumber is not int partNumber)
        {
            ApplyPlaylistButtonColors();
            return;
        }

        _playlistTransitionGlowPartNumber = partNumber;
        _playlistTransitionGlowStartTickMs = Environment.TickCount64;
        if (TryGetPlaylistBeatTiming(out var beatPhase, out var beatDurationMs))
        {
            var remainingBeat = beatPhase <= 1e-3 ? 1d : 1d - beatPhase;
            _playlistTransitionGlowDurationMs = Math.Clamp(
                beatDurationMs * remainingBeat,
                50d,
                5000d);
        }
        else
        {
            _playlistTransitionGlowDurationMs = 1000d;
        }

        _playlistTransitionGlowLevel = 1d;
        _playlistTransitionGlowTimer.Start();
        ApplyPlaylistButtonColors();
    }

    private void UpdatePlaylistTransitionGlow()
    {
        var elapsed = Math.Max(0L, Environment.TickCount64 - _playlistTransitionGlowStartTickMs);
        var t = Math.Clamp(elapsed / Math.Max(1d, _playlistTransitionGlowDurationMs), 0d, 1d);
        if (t >= 1d)
        {
            ClearPlaylistTransitionGlow();
            return;
        }

        _playlistTransitionGlowLevel = (1d + Math.Cos(t * Math.PI)) * 0.5d;
        ApplyPlaylistButtonColors();
    }

    private void UpdateGroupFadeRadioEnabled()
    {
        IEnumerable<int> playingParts = _playingPlaylistPartNumbers.Count > 0
            ? _playingPlaylistPartNumbers
            : _manualPlaylistPartNumber is int manualPart
                ? [manualPart]
                : [];
        var enabled = playingParts.Any(part =>
            _partGroupIds.ContainsKey(part)
            && !_disabledPartNumbers.Contains(part));
        var waveOnly = _previewSession?.AllowsSessionMarkerEdit == true;

        foreach (var radio in FadeInGroupRadios)
        {
            radio.IsEnabled = enabled;
        }

        foreach (var radio in ChangeOccursRadios)
        {
            var radioEnabled = enabled;
            if (radioEnabled
                && waveOnly
                && TagToExitSource(radio) is PlaylistExitSourceMode mode
                && mode is PlaylistExitSourceMode.NextBar or PlaylistExitSourceMode.NextBeat)
            {
                radioEnabled = false;
            }

            radio.IsEnabled = radioEnabled;
        }
    }

    private static IReadOnlyList<long> GetPlaylistExitBoundaries(
        WaveformPreviewData preview,
        IReadOnlyList<WaveformMarkerMark> markers,
        IReadOnlyList<WaveformRegionMark> regions,
        PlaylistExitSourceMode mode,
        long currentSample,
        long currentPartStart,
        long currentPartEnd,
        long transitionLimit,
        bool hasActiveLoop)
    {
        IEnumerable<long> candidates = mode switch
        {
            PlaylistExitSourceMode.Immediate => [],
            PlaylistExitSourceMode.NextBar => preview.Bars
                .Where(mark => !mark.IsTempoChangeOnly)
                .Select(mark => mark.SampleOffset)
                .Append(transitionLimit),
            PlaylistExitSourceMode.NextBeat => EnumerateBeatBoundaries(preview.Bars, transitionLimit)
                .Append(transitionLimit),
            PlaylistExitSourceMode.NextCue => markers
                .Where(marker =>
                    marker.SampleOffset >= currentPartStart
                    && marker.SampleOffset < currentPartEnd)
                .Select(marker => marker.SampleOffset),
            PlaylistExitSourceMode.ExitCue =>
            [
                GetPlaylistExitCueSample(
                    regions,
                    currentPartStart,
                    currentPartEnd,
                    transitionLimit,
                    hasActiveLoop),
            ],
            _ => [],
        };

        return candidates
            .Where(sample =>
                (mode == PlaylistExitSourceMode.ExitCue || sample > currentSample)
                && sample <= (mode == PlaylistExitSourceMode.ExitCue
                    ? currentPartEnd
                    : transitionLimit))
            .Distinct()
            .Order()
            .ToArray();
    }

    private static IEnumerable<long> EnumerateBeatBoundaries(
        IReadOnlyList<WaveformBarMark> bars,
        long limit)
    {
        var barLines = bars
            .Where(mark => !mark.IsTempoChangeOnly)
            .OrderBy(mark => mark.SampleOffset)
            .ToArray();
        for (var i = 0; i + 1 < barLines.Length; i++)
        {
            var bar = barLines[i];
            var next = barLines[i + 1];
            if (bar.SampleOffset >= limit)
            {
                yield break;
            }

            var end = Math.Min(next.SampleOffset, limit);
            var span = next.SampleOffset - bar.SampleOffset;
            var beatCount = Math.Max(1, bar.Numerator);
            if (span <= 0)
            {
                continue;
            }

            for (var beat = 0; beat < beatCount; beat++)
            {
                var sample = bar.SampleOffset
                    + (long)Math.Round(span * beat / (double)beatCount, MidpointRounding.AwayFromZero);
                if (sample < end)
                {
                    yield return sample;
                }
            }
        }
    }

    private static long GetPlaylistExitCueSample(
        IReadOnlyList<WaveformRegionMark> regions,
        long currentPartStart,
        long currentPartEnd,
        long transitionLimit,
        bool hasActiveLoop)
    {
        var lastRegion = regions
            .Where(region =>
                !region.IsExcluded
                && region.StartSampleOffset < currentPartEnd
                && region.EndSampleOffset > currentPartStart)
            .OrderBy(region => region.StartSampleOffset)
            .LastOrDefault();
        var exitCue = lastRegion is { } last
            && last.NameSuffix.Equals(
                WaveformRegionBuilder.LoopEndSuffix,
                StringComparison.OrdinalIgnoreCase)
            ? Math.Max(currentPartStart, last.StartSampleOffset)
            : currentPartEnd;
        return hasActiveLoop
            ? Math.Min(exitCue, transitionLimit)
            : exitCue;
    }

    private static long GetLeadingAnacrusisFrameCount(
        IReadOnlyList<WaveformRegionMark> regions,
        WaveformOutputPart target)
    {
        var expectedStart = target.StartSampleOffset;
        foreach (var region in regions.OrderBy(region => region.StartSampleOffset))
        {
            if (region.EndSampleOffset <= expectedStart)
            {
                continue;
            }

            if (region.StartSampleOffset != expectedStart
                || region.IsExcluded
                || !region.NameSuffix.Equals(
                    WaveformRegionBuilder.AnacrusisSuffix,
                    StringComparison.OrdinalIgnoreCase)
                || region.EndSampleOffset > target.EndSampleOffset)
            {
                break;
            }

            expectedStart = region.EndSampleOffset;
        }

        return Math.Max(0L, expectedStart - target.StartSampleOffset);
    }

    private double WrapProgressForLoopRange(double progress, double? startNullable, double? endNullable)
    {
        if (startNullable is not double start || endNullable is not double end)
        {
            return progress;
        }

        var span = end - start;
        if (span <= 1e-15)
        {
            return progress;
        }

        while (progress > end + 1e-12)
        {
            progress -= span;
        }

        return Math.Clamp(progress, 0d, 1d);
    }

    private double WrapProgressForLoop(double progress)
    {
        if (!_audioPlayer.TryGetActiveLoopProgress(out var start, out var end))
        {
            return progress;
        }

        return WrapProgressForLoopRange(progress, start, end);
    }

    /// <summary>再生中のプレイリスト UI 状態を更新する（OnPlayheadTick から呼ぶ）。</summary>
    private void UpdatePlaylistPlaybackOnPlayheadTick(ref double progress, bool isPlaying)
    {
        if (!_audioPlayer.HasSource)
        {
            return;
        }

        if (isPlaying)
        {
            TryCommitPendingOverlay();
            var durationSec = _audioPlayer.Duration.TotalSeconds;
            if (durationSec > 0)
            {
                var elapsedSec = (Environment.TickCount64 - _anchorTickMs) / 1000d;
                progress = _anchorProgress + elapsedSec / durationSec;

                if (_pendingPlaylistTransitionGeneration != 0
                    && _loadedPreview is { } preview
                    && preview.WavInfo.FrameCount > 0)
                {
                    var frameCount = preview.WavInfo.FrameCount;
                    var oldTimelineSample = (long)Math.Floor(progress * frameCount);
                    if (!_pendingPlaylistAudioStarted
                        && _audioPlayer.TryGetPlaylistTransitionState(out var transition)
                        && transition.StartedGeneration >= _pendingPlaylistTransitionGeneration)
                    {
                        if (oldTimelineSample >= _pendingPlaylistBoundarySample)
                        {
                            _pendingPlaylistAudioStarted = true;
                        }
                    }

                    if (_pendingPlaylistAudioStarted
                        && oldTimelineSample < _pendingPlaylistSyncBoundarySample)
                    {
                        var preRollElapsed = Math.Max(
                            0L,
                            oldTimelineSample - _pendingPlaylistBoundarySample);
                        var anacrusisSample = Math.Min(
                            _pendingPlaylistTargetEntrySample,
                            _pendingPlaylistTargetSample + preRollElapsed);
                        waveformView.SetAnacrusisPlayhead(
                            anacrusisSample / (double)frameCount,
                            recordTrail: true);
                    }

                    if (_pendingPlaylistAudioStarted
                        && oldTimelineSample >= _pendingPlaylistSyncBoundarySample)
                    {
                        var overshoot = Math.Max(
                            0L,
                            oldTimelineSample - _pendingPlaylistSyncBoundarySample);
                        var targetTimelineSample =
                            _pendingPlaylistTargetEntrySample + overshoot;
                        progress = CommitPendingPlaylistUiTransition(
                            oldTimelineSample,
                            targetTimelineSample,
                            "scheduled");
                    }
                }

                if (_pendingPlaylistTransitionGeneration == 0)
                {
                    if (!_audioPlayer.TryGetActiveLoopProgress(out _, out _)
                        && _audioPlayer.TryGetLoopProgress(progress, out _, out _))
                    {
                        _audioPlayer.ArmLoopAtProgress(progress);
                    }

                    progress = WrapProgressForLoop(progress);
                }
                else
                {
                    progress = WrapProgressForLoopRange(
                        progress,
                        _pendingSourceLoopStart,
                        _pendingSourceLoopEnd);
                }

                _smoothProgress = Math.Clamp(progress, 0d, 1d);
                progress = _smoothProgress;
            }

            if (!_automaticPlaylistPlayback)
            {
                SetManualPlaylistHighlight(_smoothProgress);
            }
        }
        else
        {
            _smoothProgress = progress;
        }

        if (isPlaying
            || _audioPlayer.ActiveOverlayPlaylistVoiceCount > 0
            || _overlayPlayheadProgresses.Count > 0
            || _overlayFadeOutPlayheadProgresses.Count > 0
            || _overlayExitPlayheadProgresses.Count > 0)
        {
            UpdateOverlayPlayheads(recordTrail: isPlaying);
        }
        else if (SyncPlayingPlaylistPartNumbersFromPlayer())
        {
            ApplyPlaylistButtonColors();
        }

        UpdatePlaylistHighlightFades();
    }

    private void OnPlaybackEndedForPlaylistUi()
    {
        ClearPendingPlaylistUiTransition();
        ClearPlaylistTransitionGlow();
        ClearPlaylistPlaybackSelection();
        var resetProgress = Math.Clamp(_lastPlaybackStartProgress ?? 0d, 0d, 1d);
        if (_audioPlayer.HasSource)
        {
            _audioPlayer.Seek(resetProgress);
            _audioPlayer.ArmLoopAtProgress(resetProgress);
        }

        AnchorPlayhead(resetProgress);
        waveformView.SetPlayhead(resetProgress, recordTrail: false, ensureVisible: true);
        waveformView.SetExitPlayhead(null);
        waveformView.SetFadeOutPlayhead(null);
        waveformView.SetAnacrusisPlayhead(null);
        ApplyPlaylistButtonColors();
    }
}
