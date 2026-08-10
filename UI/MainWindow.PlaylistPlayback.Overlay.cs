using System.Windows.Media;
using System.Windows.Threading;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

public partial class MainWindow
{
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
    /// 多重波形モードかつ Additive Layer 有効・重ね再生中に、
    /// 再生中のいずれかの Playlist 区間へタイムラインクリックしたとき、
    /// 重ねを崩さず同一相対オフセットへシークする。
    /// </summary>
    private bool TrySeekPreservingAdditiveLayers(double progress, bool ensureVisible)
    {
        if (_loadedPreview is not { IsMultiWaveOnly: true } preview
            || preview.WavInfo.FrameCount <= 0
            || !_audioPlayer.IsPlaying
            || !_audioPlayer.HasClockPlaylistRange
            || _audioPlayer.ActiveOverlayPlaylistVoiceCount <= 0)
        {
            return false;
        }

        var clockPartNumber = _audioPlayer.GetClockPlaylistVoiceId();
        if (clockPartNumber == 0 || !ResolveAdditiveLayers(clockPartNumber))
        {
            return false;
        }

        if (TryGetOutputPartAtProgress(progress) is not { } clickedPart
            || !IsPlaylistLayerVoiceActive(clickedPart.Number)
            || clickedPart.EndSampleOffset <= clickedPart.StartSampleOffset)
        {
            return false;
        }

        var frameCount = preview.WavInfo.FrameCount;
        var clickSample = (long)Math.Clamp(
            Math.Floor(Math.Clamp(progress, 0d, 1d) * frameCount),
            0d,
            Math.Max(0L, frameCount - 1));
        var relativeSample = Math.Max(0L, clickSample - clickedPart.StartSampleOffset);

        _audioPlayer.CancelPlaylistTransition();
        ClearPendingPlaylistUiTransition();
        ClearPendingOverlay();

        if (!_audioPlayer.TrySeekPlaylistLayersToRelative(relativeSample, out var clockProgress))
        {
            return false;
        }

        _audioPlayer.ArmLoopAtProgress(clockProgress);
        AnchorPlayhead(clockProgress);
        waveformView.SetPlayhead(clockProgress, recordTrail: false, ensureVisible: ensureVisible);
        waveformView.SetExitPlayhead(null);
        waveformView.SetFadeOutPlayhead(null);
        UpdateOverlayPlayheads(recordTrail: false);
        UpdateSourceLevelMeter();
        return true;
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

        var durationSec = _audioPlayer.Duration.TotalSeconds;
        var now = Environment.TickCount64;

        var voiceCount = _audioPlayer.CopyOverlayPlaylistVoiceProgresses(
            _overlayProgressScratch,
            _overlayVoiceIdScratch);
        FillSmoothedOverlayProgresses(
            voiceCount,
            _overlayVoiceIdScratch,
            _overlayProgressScratch,
            _overlayPlayheadProgresses,
            _overlayPlayheadAnchors,
            durationSec,
            now,
            recordTrail);
        waveformView.SetOverlayPlayheads(_overlayPlayheadProgresses, recordTrail);

        var fadeOutCount = _audioPlayer.CopyOverlayFadeOutProgresses(
            _overlayFadeOutProgressScratch,
            _overlayFadeOutVoiceIdScratch);
        FillSmoothedOverlayProgresses(
            fadeOutCount,
            _overlayFadeOutVoiceIdScratch,
            _overlayFadeOutProgressScratch,
            _overlayFadeOutPlayheadProgresses,
            _overlayFadeOutPlayheadAnchors,
            durationSec,
            now,
            recordTrail);
        waveformView.SetOverlayFadeOutPlayheads(_overlayFadeOutPlayheadProgresses, recordTrail);

        var exitCount = _audioPlayer.CopyOverlayExitProgresses(
            _overlayExitProgressScratch,
            _overlayExitVoiceIdScratch);
        FillSmoothedOverlayProgresses(
            exitCount,
            _overlayExitVoiceIdScratch,
            _overlayExitProgressScratch,
            _overlayExitPlayheadProgresses,
            _overlayExitPlayheadAnchors,
            durationSec,
            now,
            recordTrail);
        waveformView.SetOverlayExitPlayheads(_overlayExitPlayheadProgresses, recordTrail);
    }

    /// <summary>
    /// 生バッファ位置を壁時計で滑らかにし、主シークと同じ進み方で残像が伸びるようにする。
    /// ループ等で生位置が大きく飛んだらアンカーを張り直す。
    /// </summary>
    private void FillSmoothedOverlayProgresses(
        int count,
        int[] voiceIds,
        double[] rawProgresses,
        List<double> destination,
        Dictionary<int, (double AnchorProgress, long AnchorTickMs)> anchors,
        double durationSec,
        long now,
        bool recordTrail)
    {
        destination.Clear();
        _overlayAnchorLiveIdsScratch.Clear();
        if (!recordTrail || count <= 0)
        {
            anchors.Clear();
            for (var i = 0; i < count; i++)
            {
                destination.Add(Math.Clamp(rawProgresses[i], 0d, 1d));
            }

            return;
        }

        for (var i = 0; i < count; i++)
        {
            var voiceId = voiceIds[i];
            var raw = Math.Clamp(rawProgresses[i], 0d, 1d);
            _overlayAnchorLiveIdsScratch.Add(voiceId);
            destination.Add(SmoothOverlayProgress(voiceId, raw, durationSec, now, anchors));
        }

        if (anchors.Count == _overlayAnchorLiveIdsScratch.Count)
        {
            return;
        }

        List<int>? stale = null;
        foreach (var id in anchors.Keys)
        {
            if (_overlayAnchorLiveIdsScratch.Contains(id))
            {
                continue;
            }

            stale ??= [];
            stale.Add(id);
        }

        if (stale is null)
        {
            return;
        }

        foreach (var id in stale)
        {
            anchors.Remove(id);
        }
    }

    private static double SmoothOverlayProgress(
        int voiceId,
        double rawProgress,
        double durationSec,
        long now,
        Dictionary<int, (double AnchorProgress, long AnchorTickMs)> anchors)
    {
        if (durationSec <= 0d)
        {
            anchors[voiceId] = (rawProgress, now);
            return rawProgress;
        }

        if (!anchors.TryGetValue(voiceId, out var anchor))
        {
            anchors[voiceId] = (rawProgress, now);
            return rawProgress;
        }

        var elapsedSec = (now - anchor.AnchorTickMs) / 1000d;
        var smooth = anchor.AnchorProgress + elapsedSec / durationSec;
        var driftSec = Math.Abs(rawProgress - smooth) * durationSec;
        // WaveformView の残像不連続許容（約 1.25s）に合わせ、ループ等で飛んだら張り直す
        if (driftSec >= 1.25d)
        {
            anchors[voiceId] = (rawProgress, now);
            return rawProgress;
        }

        return Math.Clamp(smooth, 0d, 1d);
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
            // StartPlaylistRange が既に位置とループプランをセット済み。
            // SeekPlayback は ClearPlaylistPlayback するためここでは呼ばない（Form1 と同じ）。
            AnchorPlayhead(progress);
            waveformView.SetPlayhead(progress, recordTrail: false, ensureVisible: true);
            waveformView.SetExitPlayhead(null);
            waveformView.SetFadeOutPlayhead(null);
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

}
