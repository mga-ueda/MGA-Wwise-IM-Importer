using System.Windows.Media;
using System.Windows.Threading;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

public partial class MainWindow
{
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
        if (span <= 1e-12)
        {
            return progress;
        }

        // ループ開始より前なら、そこに至るまで通常再生
        if (progress < start)
        {
            return progress;
        }

        var relative = progress - start;
        var wrapped = start + (relative - Math.Floor(relative / span) * span);
        if (wrapped >= end)
        {
            wrapped = start;
        }

        // 折り返しで滑らかアンカーを付け替える。
        if (Math.Abs(wrapped - progress) > 1e-9)
        {
            _anchorProgress = wrapped;
            _anchorTickMs = Environment.TickCount64;
        }

        return wrapped;
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

                if (progress + 1e-12 < _smoothProgress)
                {
                    waveformView.ClearPlayheadTrail();
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
        _resumePlaybackAfterBackwardSeek = false;
        _playheadTimer.Stop();
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
        waveformView.SetOverlayFadeOutPlayheads([]);
        ApplyPlaylistButtonColors();
        UpdateSourceLevelMeter();
    }
}
