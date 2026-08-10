using System.Windows.Media;
using System.Windows.Threading;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

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
    private readonly Dictionary<int, (double AnchorProgress, long AnchorTickMs)> _overlayPlayheadAnchors = [];
    private readonly Dictionary<int, (double AnchorProgress, long AnchorTickMs)> _overlayFadeOutPlayheadAnchors = [];
    private readonly Dictionary<int, (double AnchorProgress, long AnchorTickMs)> _overlayExitPlayheadAnchors = [];
    private readonly HashSet<int> _overlayAnchorLiveIdsScratch = [];
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

    private void ClearPlaylistOverlayState()
    {
        ClearPendingOverlay();
        _playingPlaylistPartNumbers.Clear();
        _audioPlayer.ClearOverlayPlaylistVoices();
        ClearOverlayPlayheadUi();
    }

    private void ClearOverlayPlayheadUi()
    {
        _overlayPlayheadProgresses.Clear();
        _overlayFadeOutPlayheadProgresses.Clear();
        _overlayExitPlayheadProgresses.Clear();
        _overlayPlayheadAnchors.Clear();
        _overlayFadeOutPlayheadAnchors.Clear();
        _overlayExitPlayheadAnchors.Clear();
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

    private double ResolveFadeInSeconds(int partNumber) =>
        _partFadeInSeconds.GetValueOrDefault(partNumber);

    private double ResolveFadeOutSeconds(int partNumber) =>
        _partFadeOutSeconds.GetValueOrDefault(partNumber);

    private RegionFadeCurveKind ResolveFadeInCurve(int partNumber) =>
        _partFadeInCurves.GetValueOrDefault(partNumber, _appSettings.DefaultPlaylistFadeInCurve);

    private RegionFadeCurveKind ResolveFadeOutCurve(int partNumber) =>
        _partFadeOutCurves.GetValueOrDefault(partNumber, _appSettings.DefaultPlaylistFadeOutCurve);

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
}
