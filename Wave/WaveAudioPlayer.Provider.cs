using NAudio.Wave;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wave;

internal sealed partial class WaveAudioPlayer
{
    private sealed class StereoFloatWaveProvider : IWaveProvider
    {
        private const float FoldGain = 0.7071f;
        private readonly WaveFileReader _source;
        private readonly WaveFileReader _exitSource;
        private readonly WaveFileReader _playlistFadeSource;
        private readonly WaveFileReader _playlistExitFadeSource;
        private readonly WaveFileReader _playlistPreRollSource;
        private readonly OverlayPlaylistVoice[] _overlayVoices;
        private readonly Action<string> _diagnostic;
        private readonly Func<byte[], int, float> _sampleReader;
        private readonly int _sourceBlockAlign;
        private readonly int _channels;
        private readonly int _bytesPerSample;
        private readonly float _normalize;
        private byte[] _pcmScratch = [];
        private byte[] _mainFloat = [];
        private byte[] _exitFloat = [];
        private byte[] _playlistFadeFloat = [];
        private byte[] _playlistExitFadeFloat = [];
        private byte[] _playlistPreRollFloat = [];
        private byte[] _overlayMixFloat = [];
        private byte[] _overlayExitFloat = [];
        private readonly object _gate = new();
        private readonly object _readGate = new();
        private LoopPlaybackPlan? _activePlan;
        private bool _playExitLayer = true;
        private bool _exitPlaying;
        private long _exitStartSample;
        private long _exitEndSample;
        private long _exitStartTickMs;
        private PlaylistTransitionRequest? _pendingPlaylistTransition;
        private long? _playlistStartSample;
        private long? _playlistEndSample;
        private int _clockPlaylistVoiceId;
        private bool _clockFadeOutPlaying;
        private long _clockFadeOutFramesRead;
        private int _clockFadeOutFrameCount;
        private bool _stopAfterClockFadeOut;
        /// <summary>最終クロック FO 完了後、Read が 0 を返し続け再生終了させる。</summary>
        private bool _forceEndAfterClockFadeOut;
        private bool _playlistFadePlaying;
        private long _playlistFadeStartSample;
        private long _playlistFadeEndSample;
        private long _playlistFadeStartTickMs;
        private long _playlistFadeExitStartSample;
        private long _playlistFadeExitEndSample;
        private bool _playlistExitFadePlaying;
        private long _playlistExitFadeEndSample;
        private bool _playlistPreRollPlaying;
        private bool _playlistMainFadeInPlaying;
        private long _playlistMainFadeInFramesRead;
        private int _playlistMainFadeInFrameCount;
        private bool _playlistPreRollFadeInPlaying;
        private long _playlistPreRollFadeInFramesRead;
        private int _playlistPreRollFadeInFrameCount;
        private long _playlistFadeIncomingFramesRead;
        private int _playlistFadeIncomingFrameCount;
        private long _playlistFadeFramesRead;
        private int _playlistFadeFrameCount;
        private long _playlistRequestGeneration;
        private long _playlistStartedGeneration;
        private long _playlistStartedTargetSample;
        private IReadOnlyList<RegionEdgeFade> _regionEdgeFades = [];
        private IReadOnlyList<(long Start, long End)> _excludedRanges = [];
        private float _outputPeak;
        /// <summary>スペアナ用の直近出力モノラルサンプル（リングバッファ）。</summary>
        private readonly float[] _monitorRing = new float[8192];
        private long _monitorWriteCount;
        private readonly object _monitorGate = new();

        private bool _metronomeEnabled;
        private float _metronomeVolume = MetronomePlayer.DefaultVolume;
        private IReadOnlyList<WaveformBarMark> _metronomeBars = [];
        private float[] _metronomeHigh = [];
        private float[] _metronomeLow = [];
        private float[]? _metronomeActiveClick;
        private int _metronomeClickPos = -1;
        private long _metronomeLastAbsSample = -1;
        private long? _metronomeArmedBeatKey;
        private long _metronomeCachedBarStart = -1;
        private long _metronomeCachedBarEnd = -1;
        private int _metronomeCachedBarNumber;
        private double _metronomeCachedBpm;
        private int _metronomeCachedNumerator;
        private int _metronomeCachedDenominator;
        private long[] _metronomeCachedBeatStarts = [];
        /// <summary>現在拍（0 始まり）。ホットパスで境界内なら解決を省略する。</summary>
        private int _metronomeCurrentBeatZeroBased = -1;
        private long _metronomeCurrentBeatStart;
        private long _metronomeCurrentNextBeat;

        public StereoFloatWaveProvider(
            WaveFileReader source,
            WaveFileReader exitSource,
            WaveFileReader playlistFadeSource,
            WaveFileReader playlistExitFadeSource,
            WaveFileReader playlistPreRollSource,
            WaveFileReader[] overlaySources,
            WaveFileReader[] overlayExitSources,
            WavFileInfo info,
            Action<string> diagnostic)
        {
            if (info.Channels == 0 || info.BlockAlign == 0 || info.SampleRate == 0)
            {
                throw new InvalidDataException(UiStrings.ErrWaveFormatInvalid);
            }

            if (overlaySources.Length != MaxPlaylistVoices - 1
                || overlayExitSources.Length != MaxPlaylistVoices - 1)
            {
                throw new ArgumentException(
                    $"Overlay readers must be {MaxPlaylistVoices - 1}.",
                    nameof(overlaySources));
            }

            _source = source;
            _exitSource = exitSource;
            _playlistFadeSource = playlistFadeSource;
            _playlistExitFadeSource = playlistExitFadeSource;
            _playlistPreRollSource = playlistPreRollSource;
            _overlayVoices = new OverlayPlaylistVoice[overlaySources.Length];
            for (var i = 0; i < overlaySources.Length; i++)
            {
                _overlayVoices[i] = new OverlayPlaylistVoice(
                    overlaySources[i],
                    overlayExitSources[i]);
            }

            _diagnostic = diagnostic;
            _sampleReader = WavPeakReader.CreateSampleReader(info.AudioFormat, info.BitsPerSample);
            _channels = info.Channels;
            _sourceBlockAlign = info.BlockAlign;
            _bytesPerSample = info.BitsPerSample / 8;
            var extraChannels = Math.Max(0, _channels - 2);
            _normalize = 1f / (1f + extraChannels * FoldGain);
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)info.SampleRate, 2);
            _playlistFadeFrameCount = Math.Max(1, (int)Math.Round(info.SampleRate * 0.5d));
        }

        public WaveFormat WaveFormat { get; }

        public float OutputPeak => Volatile.Read(ref _outputPeak);

        public bool HasClockPlaylistRange
        {
            get
            {
                lock (_gate)
                {
                    return _playlistStartSample is not null && _playlistEndSample is not null;
                }
            }
        }

        public int ActiveOverlayPlaylistVoiceCount
        {
            get
            {
                lock (_gate)
                {
                    var count = 0;
                    foreach (var voice in _overlayVoices)
                    {
                        if (voice.Active)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
        }

        public void ResetOutputPeak() => Volatile.Write(ref _outputPeak, 0f);

        public void SetMetronomeClicks(float[] high, float[] low)
        {
            lock (_readGate)
            {
                lock (_gate)
                {
                    _metronomeHigh = high ?? [];
                    _metronomeLow = low ?? [];
                    _metronomeActiveClick = null;
                    _metronomeClickPos = -1;
                }
            }
        }

        public void SetMetronomeEnabled(bool enabled)
        {
            lock (_readGate)
            {
                lock (_gate)
                {
                    _metronomeEnabled = enabled;
                    ResetMetronomeScheduleNoLock();
                }
            }
        }

        /// <summary>シーク等の不連続後に、着地拍をサイレントアームし直す。</summary>
        public void ResetMetronomeSchedule()
        {
            lock (_readGate)
            {
                lock (_gate)
                {
                    ResetMetronomeScheduleNoLock();
                }
            }
        }

        private void ResetMetronomeScheduleNoLock()
        {
            _metronomeActiveClick = null;
            _metronomeClickPos = -1;
            _metronomeLastAbsSample = -1;
            _metronomeArmedBeatKey = null;
            _metronomeCachedBarStart = -1;
            _metronomeCachedBarEnd = -1;
            _metronomeCachedBeatStarts = [];
            _metronomeCurrentBeatZeroBased = -1;
            _metronomeCurrentBeatStart = 0;
            _metronomeCurrentNextBeat = 0;
        }

        public void SetMetronomeVolume(float volume)
        {
            lock (_gate)
            {
                _metronomeVolume = Math.Clamp(
                    volume,
                    MetronomePlayer.MinVolume,
                    MetronomePlayer.MaxVolume);
            }
        }

        public void SetMetronomeBars(IReadOnlyList<WaveformBarMark> bars)
        {
            lock (_readGate)
            {
                lock (_gate)
                {
                    _metronomeBars = bars ?? [];
                    ResetMetronomeScheduleNoLock();
                }
            }
        }

        public long CurrentMainSample
        {
            get
            {
                lock (_readGate)
                {
                    return CurrentSample(_source);
                }
            }
        }

        public void SetPlayExitLayer(bool enabled)
        {
            lock (_gate)
            {
                _playExitLayer = enabled;
                if (enabled)
                {
                    return;
                }

                _exitPlaying = false;
                foreach (var voice in _overlayVoices)
                {
                    voice.ExitPlaying = false;
                    voice.ExitStartSample = 0;
                    voice.ExitEndSample = 0;
                }
            }
        }

        public void SetActivePlan(LoopPlaybackPlan? plan)
        {
            lock (_gate)
            {
                _activePlan = plan;
                // アーム／解除だけでは Exit を始めない（ループ折り返しで開始）
                _exitPlaying = false;
            }
        }

        public void SetRegionEdgeFades(IReadOnlyList<RegionEdgeFade>? fades)
        {
            lock (_gate)
            {
                _regionEdgeFades = fades is null || fades.Count == 0
                    ? []
                    : fades.ToArray();
            }
        }

        public void SetExcludedRegions(IReadOnlyList<WaveformRegionMark>? regions)
        {
            lock (_gate)
            {
                if (regions is null || regions.Count == 0)
                {
                    _excludedRanges = [];
                    return;
                }

                var list = new List<(long Start, long End)>();
                foreach (var region in regions)
                {
                    if (!region.IsExcluded || region.EndSampleOffset <= region.StartSampleOffset)
                    {
                        continue;
                    }

                    list.Add((region.StartSampleOffset, region.EndSampleOffset));
                }

                list.Sort((a, b) => a.Start.CompareTo(b.Start));
                _excludedRanges = list;
            }
        }

        public LoopPlaybackPlan? GetActivePlan()
        {
            lock (_gate)
            {
                return _activePlan;
            }
        }

        public void StartPlaylistRange(
            long startSample,
            long endSample,
            LoopPlaybackPlan? plan,
            int clockVoiceId)
        {
            lock (_readGate)
            {
                var generation = NextPlaylistGeneration();
                SeekToSample(_source, startSample);
                ResetMetronomeScheduleNoLock();
                lock (_gate)
                {
                    _pendingPlaylistTransition = null;
                    _playlistFadePlaying = false;
                    _playlistExitFadePlaying = false;
                    _playlistPreRollPlaying = false;
                    ResetMainFadeInNoLock();
                    ResetPreRollFadeInNoLock();
                    ResetClockFadeOutNoLock();
                    ClearOverlayPlaylistVoicesNoLock();
                    _playlistStartSample = startSample;
                    _playlistEndSample = endSample;
                    _clockPlaylistVoiceId = clockVoiceId;
                    _activePlan = plan;
                    _exitPlaying = false;
                    _playlistStartedGeneration = generation;
                    _playlistStartedTargetSample = startSample;
                }
                _diagnostic($"provider.playlist-range-start generation={generation} voice={clockVoiceId} start={startSample} end={endSample} loopPlan={plan?.ToString() ?? "none"}");
            }
        }

        public bool TrySchedulePlaylistTransition(
            long startSample,
            long endSample,
            long? sourceExitSample,
            long sourcePartStartSample,
            PlaylistDestinationSyncMode destinationSyncMode,
            long preRollFrameCount,
            bool allowShortPreRoll,
            long fadeSourceEndSample,
            int fadeInFrameCount,
            int fadeFrameCount,
            Func<long, LoopPlaybackPlan?> findPlan,
            out PlaylistTransitionSchedule schedule)
        {
            lock (_readGate)
            {
                var currentSample = CurrentSample(_source);
                var syncBoundarySample = sourceExitSample ?? currentSample;
                var sourceRelativeSample = Math.Max(
                    0L,
                    syncBoundarySample - sourcePartStartSample);
                if (syncBoundarySample < currentSample
                    || fadeSourceEndSample < syncBoundarySample)
                {
                    schedule = default;
                    _diagnostic($"provider.playlist-schedule-rejected current={currentSample} sourceExit={sourceExitSample?.ToString() ?? "immediate"} sync={syncBoundarySample} fadeEnd={fadeSourceEndSample}");
                    return false;
                }

                var effectivePreRollFrameCount =
                    destinationSyncMode == PlaylistDestinationSyncMode.EntryCue
                        ? preRollFrameCount
                        : 0L;
                var desiredTriggerSample =
                    syncBoundarySample - effectivePreRollFrameCount;
                var startsImmediately = desiredTriggerSample <= currentSample;
                if (startsImmediately
                    && sourceExitSample.HasValue
                    && !allowShortPreRoll)
                {
                    schedule = default;
                    _diagnostic($"provider.playlist-schedule-rejected-short-preroll current={currentSample} desiredTrigger={desiredTriggerSample} sync={syncBoundarySample} preRoll={preRollFrameCount}");
                    return false;
                }

                var triggerSample = startsImmediately
                    ? currentSample
                    : desiredTriggerSample;
                var targetEntrySample = destinationSyncMode switch
                {
                    PlaylistDestinationSyncMode.SameTime =>
                        startSample + sourceRelativeSample,
                    _ => startSample + (syncBoundarySample - triggerSample),
                };
                if (destinationSyncMode == PlaylistDestinationSyncMode.SameTime
                    && targetEntrySample >= endSample)
                {
                    schedule = new PlaylistTransitionSchedule(
                        0,
                        triggerSample,
                        syncBoundarySample,
                        targetEntrySample,
                        startsImmediately,
                        sourceRelativeSample,
                        "same-time-out-of-range");
                    _diagnostic($"provider.playlist-schedule-rejected-same-time current={currentSample} sourcePartStart={sourcePartStartSample} sourceRelative={sourceRelativeSample} sync={syncBoundarySample} targetStart={startSample} targetSwitch={targetEntrySample} targetEnd={endSample}");
                    return false;
                }

                if (triggerSample < 0
                    || targetEntrySample < startSample
                    || targetEntrySample > endSample)
                {
                    schedule = default;
                    _diagnostic($"provider.playlist-schedule-rejected-range current={currentSample} trigger={triggerSample} sync={syncBoundarySample} targetStart={startSample} targetEntry={targetEntrySample} targetEnd={endSample}");
                    return false;
                }

                var generation = NextPlaylistGeneration();
                PlaylistTransitionRequest transition;
                lock (_gate)
                {
                    _pendingPlaylistTransition = null;
                    _playlistPreRollPlaying = false;
                    ResetPreRollFadeInNoLock();
                    transition = new PlaylistTransitionRequest(
                        startSample,
                        targetEntrySample,
                        endSample,
                        triggerSample,
                        syncBoundarySample,
                        Math.Max(syncBoundarySample, fadeSourceEndSample),
                        Math.Max(0, fadeInFrameCount),
                        Math.Max(0, fadeFrameCount),
                        findPlan(targetEntrySample),
                        generation);
                    _pendingPlaylistTransition = transition;
                }

                if (startsImmediately)
                {
                    if (triggerSample < syncBoundarySample)
                    {
                        BeginPlaylistPreRoll(transition);
                    }
                    else
                    {
                        BeginPlaylistTransition(transition);
                    }
                }

                schedule = new PlaylistTransitionSchedule(
                    generation,
                    triggerSample,
                    syncBoundarySample,
                    targetEntrySample,
                    startsImmediately,
                    sourceRelativeSample,
                    null);
                _diagnostic($"provider.playlist-schedule generation={generation} current={currentSample} destinationSync={destinationSyncMode} sourcePartStart={sourcePartStartSample} sourceRelative={sourceRelativeSample} trigger={triggerSample} sync={syncBoundarySample} targetStart={startSample} targetEntry={targetEntrySample} targetEnd={endSample} fadeEnd={fadeSourceEndSample} fadeInFrames={fadeInFrameCount} fadeOutFrames={fadeFrameCount} startedImmediately={startsImmediately}");
                return true;
            }
        }

        public void CancelPlaylistTransition()
        {
            lock (_readGate)
            {
                var currentSample = CurrentSample(_source);
                var hadPending = false;
                var hadFade = false;
                lock (_gate)
                {
                    hadPending = _pendingPlaylistTransition is not null;
                    hadFade = _playlistFadePlaying
                        || _playlistExitFadePlaying
                        || _playlistPreRollPlaying;
                    _pendingPlaylistTransition = null;
                    _playlistFadePlaying = false;
                    _playlistExitFadePlaying = false;
                    _playlistPreRollPlaying = false;
                    ResetPreRollFadeInNoLock();
                }
                _diagnostic($"provider.playlist-cancel current={currentSample} hadPending={hadPending} hadFade={hadFade}");
            }
        }

        /// <summary>
        /// クロック／上乗せの相対位置をそろえたままシークする。ボイスは維持する。
        /// </summary>
        public bool TrySeekPlaylistLayersToRelative(
            long relativeSample,
            Func<long, LoopPlaybackPlan?> findPlan,
            out long clockSample)
        {
            clockSample = 0;
            lock (_readGate)
            {
                long clockStart;
                long clockEnd;
                lock (_gate)
                {
                    if (_playlistStartSample is not long start
                        || _playlistEndSample is not long end
                        || end <= start)
                    {
                        return false;
                    }

                    clockStart = start;
                    clockEnd = end;

                    // 遷移／Exit は位置ジャンプと両立しないので止める。上乗せ本体は残す。
                    _pendingPlaylistTransition = null;
                    _playlistFadePlaying = false;
                    _playlistExitFadePlaying = false;
                    _playlistPreRollPlaying = false;
                    ResetPreRollFadeInNoLock();
                    ResetMainFadeInNoLock();
                    _exitPlaying = false;
                }

                var safeRelative = Math.Max(0L, relativeSample);
                var clockSpan = clockEnd - clockStart;
                var clockOffset = Math.Min(safeRelative, Math.Max(0L, clockSpan - 1));
                clockSample = clockStart + clockOffset;
                SeekToSample(_source, clockSample);
                var clockPlan = findPlan(clockSample);

                lock (_gate)
                {
                    _activePlan = clockPlan;
                    foreach (var voice in _overlayVoices)
                    {
                        if (!voice.Active)
                        {
                            continue;
                        }

                        var partSpan = voice.PartEndSample - voice.PartStartSample;
                        if (partSpan <= 0)
                        {
                            continue;
                        }

                        var overlayOffset = Math.Min(safeRelative, Math.Max(0L, partSpan - 1));
                        var targetSample = voice.PartStartSample + overlayOffset;
                        SeekToSample(voice.Reader, targetSample);
                        voice.LoopPlan = findPlan(targetSample);
                        voice.ExitPlaying = false;
                        voice.ExitStartSample = 0;
                        voice.ExitEndSample = 0;
                    }
                }

                _diagnostic(
                    $"provider.layer-seek-relative relative={safeRelative}"
                    + $" clock={clockSample} start={clockStart} end={clockEnd}");
                return true;
            }
        }

        public void ClearPlaylistPlayback()
        {
            lock (_readGate)
            {
                lock (_gate)
                {
                    _pendingPlaylistTransition = null;
                    _playlistFadePlaying = false;
                    _playlistExitFadePlaying = false;
                    _playlistPreRollPlaying = false;
                    ResetMainFadeInNoLock();
                    ResetPreRollFadeInNoLock();
                    ResetClockFadeOutNoLock();
                    ClearOverlayPlaylistVoicesNoLock();
                    _playlistStartSample = null;
                    _playlistEndSample = null;
                    _clockPlaylistVoiceId = 0;
                    _playlistRequestGeneration = 0;
                    _playlistStartedGeneration = 0;
                    _playlistStartedTargetSample = 0;
                }
            }
        }

        public bool HasOverlayPlaylistVoice(int voiceId)
        {
            lock (_gate)
            {
                foreach (var voice in _overlayVoices)
                {
                    if (voice.Active && voice.VoiceId == voiceId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TryAdoptClockPlaylistRange(
            long startSample,
            long endSample,
            int clockVoiceId,
            LoopPlaybackPlan? plan)
        {
            lock (_readGate)
            {
                var currentSample = CurrentSample(_source);
                var inRange = currentSample >= startSample && currentSample < endSample;
                if (!inRange)
                {
                    // Soft adopt: UI と実サンプルが僅かにずれていてもクロック範囲は載せる。
                    // 上乗せの relative は 0 未満にならないよう呼び出し側／Add 側で clamp する。
                    _diagnostic(
                        $"provider.adopt-clock-soft sample={currentSample}"
                        + $" start={startSample} end={endSample} voice={clockVoiceId}");
                }

                lock (_gate)
                {
                    _playlistStartSample = startSample;
                    _playlistEndSample = endSample;
                    _clockPlaylistVoiceId = clockVoiceId;
                    _activePlan = plan ?? _activePlan;
                    ResetClockFadeOutNoLock();
                }

                _diagnostic(
                    $"provider.adopt-clock voice={clockVoiceId} start={startSample}"
                    + $" end={endSample} sample={currentSample} inRange={inRange}");
                return true;
            }
        }

        public bool TryAddOverlayPlaylistVoice(
            int voiceId,
            long startSample,
            long endSample,
            int fadeInFrameCount,
            Func<long, LoopPlaybackPlan?> findPlan,
            out string? rejectReason)
        {
            lock (_readGate)
            {
                lock (_gate)
                {
                    if (_playlistStartSample is not long clockStart
                        || _playlistEndSample is not long)
                    {
                        rejectReason = "no-clock-playlist";
                        return false;
                    }

                    if (_clockPlaylistVoiceId == voiceId)
                    {
                        rejectReason = "already-clock";
                        return false;
                    }

                    var activeCount = 0;
                    OverlayPlaylistVoice? free = null;
                    foreach (var voice in _overlayVoices)
                    {
                        if (voice.Active)
                        {
                            activeCount++;
                            if (voice.VoiceId == voiceId)
                            {
                                rejectReason = "already-active";
                                return false;
                            }
                        }
                        else if (free is null)
                        {
                            free = voice;
                        }
                    }

                    // クロック1本 + 上乗せ
                    if (activeCount + 1 >= MaxPlaylistVoices || free is null)
                    {
                        rejectReason = "voice-limit";
                        return false;
                    }

                    var currentSample = CurrentSample(_source);
                    var relative = Math.Max(0L, currentSample - clockStart);
                    var targetSample = startSample + relative;
                    if (targetSample >= endSample)
                    {
                        rejectReason = "same-time-out-of-range";
                        return false;
                    }

                    SeekToSample(free.Reader, targetSample);
                    free.Active = true;
                    free.VoiceId = voiceId;
                    free.PartStartSample = startSample;
                    free.PartEndSample = endSample;
                    free.LoopPlan = findPlan(targetSample);
                    free.ExitPlaying = false;
                    free.FadeInPlaying = fadeInFrameCount > 0;
                    free.FadeOutPlaying = false;
                    free.FadeFramesRead = 0;
                    free.FadeFrameCount = Math.Max(0, fadeInFrameCount);
                    rejectReason = null;
                    _diagnostic(
                        $"provider.overlay-add voice={voiceId} start={startSample} end={endSample}"
                        + $" target={targetSample} relative={relative} fadeInFrames={fadeInFrameCount}"
                        + $" loopPlan={free.LoopPlan?.ToString() ?? "none"}");
                    return true;
                }
            }
        }

        public bool TryFadeOutOverlayPlaylistVoice(int voiceId, int fadeOutFrameCount)
        {
            lock (_gate)
            {
                foreach (var voice in _overlayVoices)
                {
                    if (!voice.Active || voice.VoiceId != voiceId)
                    {
                        continue;
                    }

                    if (fadeOutFrameCount <= 0)
                    {
                        voice.Active = false;
                        voice.FadeInPlaying = false;
                        voice.FadeOutPlaying = false;
                        voice.ExitPlaying = false;
                        _diagnostic($"provider.overlay-stop-immediate voice={voiceId}");
                        return true;
                    }

                    voice.FadeInPlaying = false;
                    voice.FadeOutPlaying = true;
                    voice.FadeFramesRead = 0;
                    voice.FadeFrameCount = fadeOutFrameCount;
                    _diagnostic(
                        $"provider.overlay-fade-out voice={voiceId} fadeOutFrames={fadeOutFrameCount}");
                    return true;
                }

                return false;
            }
        }

        public void FadeOutAllOverlayPlaylistVoices(int fadeOutFrameCount)
        {
            lock (_gate)
            {
                foreach (var voice in _overlayVoices)
                {
                    if (!voice.Active)
                    {
                        continue;
                    }

                    if (fadeOutFrameCount <= 0)
                    {
                        voice.Active = false;
                        voice.FadeInPlaying = false;
                        voice.FadeOutPlaying = false;
                        continue;
                    }

                    voice.FadeInPlaying = false;
                    voice.FadeOutPlaying = true;
                    voice.FadeFramesRead = 0;
                    voice.FadeFrameCount = fadeOutFrameCount;
                }
            }
        }

        public void ClearOverlayPlaylistVoices()
        {
            lock (_gate)
            {
                ClearOverlayPlaylistVoicesNoLock();
            }
        }

        public bool TryFadeOutClockPlaylistVoice(
            int fadeOutFrameCount,
            Func<long, LoopPlaybackPlan?> findPlan,
            out int? promotedVoiceId,
            out bool playbackWillEnd)
        {
            promotedVoiceId = null;
            playbackWillEnd = false;
            lock (_readGate)
            {
                lock (_gate)
                {
                    if (_playlistStartSample is null || _playlistEndSample is null)
                    {
                        return false;
                    }

                    OverlayPlaylistVoice? promote = null;
                    foreach (var voice in _overlayVoices)
                    {
                        if (!voice.Active || voice.FadeOutPlaying)
                        {
                            continue;
                        }

                        if (promote is null || voice.VoiceId < promote.VoiceId)
                        {
                            promote = voice;
                        }
                    }

                    if (promote is null)
                    {
                        playbackWillEnd = true;
                        if (fadeOutFrameCount <= 0)
                        {
                            _playlistStartSample = null;
                            _playlistEndSample = null;
                            _clockPlaylistVoiceId = 0;
                            ResetClockFadeOutNoLock();
                            _stopAfterClockFadeOut = true;
                            _forceEndAfterClockFadeOut = true;
                            return true;
                        }

                        _clockFadeOutPlaying = true;
                        _clockFadeOutFramesRead = 0;
                        _clockFadeOutFrameCount = fadeOutFrameCount;
                        _diagnostic(
                            $"provider.clock-fade-out-last fadeOutFrames={fadeOutFrameCount}");
                        return true;
                    }

                    // 旧クロックをフェードリーダーへ移し、上乗せをクロックへ昇格。
                    var oldSample = CurrentSample(_source);
                    if (fadeOutFrameCount > 0)
                    {
                        SeekToSample(_playlistFadeSource, oldSample);
                        _playlistFadePlaying = true;
                        _playlistFadeStartSample = oldSample;
                        _playlistFadeEndSample = _playlistEndSample.Value;
                        _playlistFadeStartTickMs = Environment.TickCount64;
                        _playlistFadeExitStartSample = 0;
                        _playlistFadeExitEndSample = 0;
                        _playlistExitFadePlaying = false;
                        _playlistFadeIncomingFramesRead = 0;
                        _playlistFadeIncomingFrameCount = 0;
                        _playlistFadeFramesRead = 0;
                        _playlistFadeFrameCount = fadeOutFrameCount;
                    }

                    var promoteSample = CurrentSample(promote.Reader);
                    SeekToSample(_source, promoteSample);
                    _playlistStartSample = promote.PartStartSample;
                    _playlistEndSample = promote.PartEndSample;
                    _clockPlaylistVoiceId = promote.VoiceId;
                    _activePlan = findPlan(promoteSample);
                    _exitPlaying = false;
                    ResetMainFadeInNoLock();
                    ResetClockFadeOutNoLock();
                    promotedVoiceId = promote.VoiceId;
                    promote.Active = false;
                    promote.FadeInPlaying = false;
                    promote.FadeOutPlaying = false;
                    _diagnostic(
                        $"provider.clock-promote fromSample={oldSample} toVoice={promote.VoiceId}"
                        + $" toSample={promoteSample} fadeOutFrames={fadeOutFrameCount}");
                    return true;
                }
            }
        }

        public int CopyOverlayPlaylistVoiceProgresses(
            double[] destination,
            int[]? voiceIds,
            long frameCount)
        {
            if (frameCount <= 0 || destination.Length == 0)
            {
                return 0;
            }

            lock (_readGate)
            {
                var count = 0;
                foreach (var voice in _overlayVoices)
                {
                    if (!voice.Active || voice.FadeOutPlaying || count >= destination.Length)
                    {
                        continue;
                    }

                    var sample = CurrentSample(voice.Reader);
                    destination[count] = Math.Clamp(sample / (double)frameCount, 0d, 1d);
                    if (voiceIds is not null && count < voiceIds.Length)
                    {
                        voiceIds[count] = voice.VoiceId;
                    }

                    count++;
                }

                return count;
            }
        }

        public int CopyOverlayFadeOutProgresses(
            double[] destination,
            int[]? voiceIds,
            long frameCount)
        {
            if (frameCount <= 0 || destination.Length == 0)
            {
                return 0;
            }

            lock (_readGate)
            {
                var count = 0;
                foreach (var voice in _overlayVoices)
                {
                    if (!voice.Active || !voice.FadeOutPlaying || count >= destination.Length)
                    {
                        continue;
                    }

                    var sample = CurrentSample(voice.Reader);
                    destination[count] = Math.Clamp(sample / (double)frameCount, 0d, 1d);
                    if (voiceIds is not null && count < voiceIds.Length)
                    {
                        voiceIds[count] = voice.VoiceId;
                    }

                    count++;
                }

                return count;
            }
        }

        public bool TryGetClockFadeOutPlaybackProgress(long frameCount, out double progress)
        {
            progress = 0d;
            if (frameCount <= 0)
            {
                return false;
            }

            lock (_readGate)
            {
                lock (_gate)
                {
                    if (!_clockFadeOutPlaying)
                    {
                        return false;
                    }
                }

                var sample = CurrentSample(_source);
                progress = Math.Clamp(sample / (double)frameCount, 0d, 1d);
                return true;
            }
        }

        public int CopyOverlayExitProgresses(
            double[] destination,
            int[]? voiceIds,
            long frameCount)
        {
            if (frameCount <= 0 || destination.Length == 0)
            {
                return 0;
            }

            lock (_readGate)
            {
                var count = 0;
                foreach (var voice in _overlayVoices)
                {
                    if (!voice.Active || !voice.ExitPlaying || count >= destination.Length)
                    {
                        continue;
                    }

                    var sample = CurrentSample(voice.ExitReader);
                    if (sample < voice.ExitStartSample || sample >= voice.ExitEndSample)
                    {
                        continue;
                    }

                    destination[count] = Math.Clamp(sample / (double)frameCount, 0d, 1d);
                    if (voiceIds is not null && count < voiceIds.Length)
                    {
                        voiceIds[count] = voice.VoiceId;
                    }

                    count++;
                }

                return count;
            }
        }

        public int CopyActiveOverlayPlaylistVoiceIds(int[] destination)
        {
            lock (_gate)
            {
                var count = 0;
                foreach (var voice in _overlayVoices)
                {
                    if (!voice.Active || count >= destination.Length)
                    {
                        continue;
                    }

                    destination[count++] = voice.VoiceId;
                }

                return count;
            }
        }

        private void ClearOverlayPlaylistVoicesNoLock()
        {
            foreach (var voice in _overlayVoices)
            {
                voice.Active = false;
                voice.FadeInPlaying = false;
                voice.FadeOutPlaying = false;
                voice.FadeFramesRead = 0;
                voice.FadeFrameCount = 0;
                voice.VoiceId = 0;
                voice.LoopPlan = null;
                voice.ExitPlaying = false;
                voice.ExitStartSample = 0;
                voice.ExitEndSample = 0;
            }
        }

        private void ResetClockFadeOutNoLock()
        {
            _clockFadeOutPlaying = false;
            _clockFadeOutFramesRead = 0;
            _clockFadeOutFrameCount = 0;
            _stopAfterClockFadeOut = false;
            _forceEndAfterClockFadeOut = false;
        }

        /// <summary>
        /// 最終クロック Group Fade Out 完了による強制終了フラグを消費する。
        /// 立っていた場合は true（Read が 0 を返して停止した要因の判定用）。
        /// </summary>
        public bool ConsumeForceEndAfterClockFadeOut()
        {
            lock (_gate)
            {
                if (!_forceEndAfterClockFadeOut && !_stopAfterClockFadeOut)
                {
                    return false;
                }

                _stopAfterClockFadeOut = false;
                _forceEndAfterClockFadeOut = false;
                return true;
            }
        }

        public bool TryResetPlaylistAfterEnd()
        {
            lock (_readGate)
            {
                long? start;
                long? end;
                lock (_gate)
                {
                    start = _playlistStartSample;
                    end = _playlistEndSample;
                }

                if (start is not long rangeStart
                    || end is not long rangeEnd
                    || CurrentSample(_source) < rangeEnd)
                {
                    return false;
                }

                SeekToSample(_source, rangeStart);
                lock (_gate)
                {
                    _pendingPlaylistTransition = null;
                    _playlistFadePlaying = false;
                    _playlistExitFadePlaying = false;
                    _playlistPreRollPlaying = false;
                    ResetMainFadeInNoLock();
                    ResetPreRollFadeInNoLock();
                    _exitPlaying = false;
                }

                _diagnostic($"provider.playlist-ended resetTo={rangeStart} end={rangeEnd}");
                return true;
            }
        }

        public bool TryGetPlaylistTransitionState(out PlaylistTransitionState state)
        {
            lock (_gate)
            {
                var pending = _pendingPlaylistTransition;
                if (pending is null && _playlistStartedGeneration == 0)
                {
                    state = default;
                    return false;
                }

                state = new PlaylistTransitionState(
                    pending?.TargetStartSample ?? _playlistStartedTargetSample,
                    pending?.TargetEndSample ?? _playlistEndSample ?? 0,
                    pending?.TriggerSample,
                    pending?.Generation ?? _playlistRequestGeneration,
                    _playlistStartedGeneration,
                    _playlistFadePlaying);
                return true;
            }
        }

        private long NextPlaylistGeneration()
        {
            lock (_gate)
            {
                return ++_playlistRequestGeneration;
            }
        }

        public void StopExitLayer()
        {
            lock (_gate)
            {
                _exitPlaying = false;
            }
        }

        /// <summary>
        /// ループ末端→頭の折り返しと同時に Exit 二重再生を開始／頭から再開する。
        /// </summary>
        private void BeginExitOnLoopWrap(LoopPlaybackPlan loop)
        {
            if (!_playExitLayer || !loop.HasExit)
            {
                return;
            }

            lock (_gate)
            {
                _exitStartSample = loop.LoopEndSample;
                _exitEndSample = loop.ExitEndSample!.Value;
                _exitPlaying = true;
                _exitStartTickMs = Environment.TickCount64;
                SeekExitToSample(_exitStartSample);
            }
            _diagnostic($"provider.exit-start start={loop.LoopEndSample} end={loop.ExitEndSample}");
        }

        private void WrapMainToLoopStart(LoopPlaybackPlan loop)
        {
            SeekToSample(_source, loop.LoopStartSample);
            BeginExitOnLoopWrap(loop);
            _diagnostic($"provider.loop-wrap start={loop.LoopStartSample} end={loop.LoopEndSample}");
        }

        /// <summary>
        /// Exit 二重再生の現在位置（ファイル全体の 0〜1）。再生中でなければ false。
        /// 壁時計ベース（メイン再生ヘッドと同様にバッファ位置の揺れを避ける）。
        /// </summary>
        public bool TryGetExitPlaybackProgress(long frameCount, int sampleRate, out double progress)
        {
            progress = 0;
            if (frameCount <= 0 || sampleRate <= 0)
            {
                return false;
            }

            long exitStart;
            long exitEnd;
            long startTick;
            lock (_gate)
            {
                if (!_exitPlaying)
                {
                    return false;
                }

                exitStart = _exitStartSample;
                exitEnd = _exitEndSample;
                startTick = _exitStartTickMs;
            }

            if (exitEnd <= exitStart)
            {
                return false;
            }

            var elapsedSec = Math.Max(0, (Environment.TickCount64 - startTick) / 1000d);
            var sample = exitStart + (long)(elapsedSec * sampleRate);
            if (sample >= exitEnd)
            {
                return false;
            }

            progress = sample / (double)frameCount;
            return true;
        }

        public bool TryGetPlaylistFadePlaybackProgress(
            long frameCount,
            int sampleRate,
            out double progress,
            out bool isExit)
        {
            progress = 0d;
            isExit = false;
            if (frameCount <= 0 || sampleRate <= 0)
            {
                return false;
            }

            long start;
            long end;
            long startTick;
            long exitStart;
            long exitEnd;
            lock (_gate)
            {
                if (!_playlistFadePlaying)
                {
                    return false;
                }

                start = _playlistFadeStartSample;
                end = Math.Min(
                    _playlistFadeEndSample,
                    start + _playlistFadeFrameCount);
                startTick = _playlistFadeStartTickMs;
                exitStart = _playlistFadeExitStartSample;
                exitEnd = _playlistFadeExitEndSample;
            }

            var elapsedSeconds = Math.Max(
                0d,
                (Environment.TickCount64 - startTick) / 1000d);
            var sample = start + (long)Math.Floor(elapsedSeconds * sampleRate);
            if (sample >= end)
            {
                return false;
            }

            progress = Math.Clamp(sample / (double)frameCount, 0d, 1d);
            isExit = exitEnd > exitStart
                && sample >= exitStart
                && sample < exitEnd;
            return true;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            lock (_readGate)
            {
                return ReadCore(buffer, offset, count);
            }
        }

        private int ReadCore(byte[] buffer, int offset, int count)
        {
            var framesNeeded = count / 8;
            if (framesNeeded <= 0)
            {
                Volatile.Write(ref _outputPeak, 0f);
                return 0;
            }

            var outIndex = offset;
            var totalFrames = 0;
            var outputPeak = 0f;
            while (totalFrames < framesNeeded)
            {
                LoopPlaybackPlan? plan;
                var exitPlaying = false;
                long exitStart = 0;
                long exitEnd = 0;
                PlaylistTransitionRequest? transition;
                long? playlistEnd;
                var playlistFadePlaying = false;
                var playlistPreRollPlaying = false;
                var stopAfterClockFade = false;
                var forceEndAfterClockFade = false;
                IReadOnlyList<(long Start, long End)> excludedRanges;
                lock (_gate)
                {
                    plan = _activePlan;
                    exitPlaying = _exitPlaying;
                    exitStart = _exitStartSample;
                    exitEnd = _exitEndSample;
                    transition = _pendingPlaylistTransition;
                    playlistEnd = _playlistEndSample;
                    playlistFadePlaying = _playlistFadePlaying;
                    playlistPreRollPlaying = _playlistPreRollPlaying;
                    stopAfterClockFade = _stopAfterClockFadeOut;
                    forceEndAfterClockFade = _forceEndAfterClockFadeOut;
                    excludedRanges = _excludedRanges;
                }

                if (forceEndAfterClockFade || stopAfterClockFade)
                {
                    lock (_gate)
                    {
                        _stopAfterClockFadeOut = false;
                        _forceEndAfterClockFadeOut = true;
                    }

                    // この Read で既に書いた分は返し、以降は 0 で終了させる。
                    if (totalFrames == 0)
                    {
                        Volatile.Write(ref _outputPeak, 0f);
                        return 0;
                    }

                    break;
                }

                var samplePos = CurrentSample(_source);
                var framesThis = framesNeeded - totalFrames;

                // Playlist 遷移はループ折り返しより優先する。境界まででチャンクを厳密に分割する。
                if (transition is { } pending)
                {
                    if (!playlistPreRollPlaying)
                    {
                        if (samplePos >= pending.TriggerSample)
                        {
                            if (pending.TriggerSample < pending.SyncBoundarySample)
                            {
                                BeginPlaylistPreRoll(pending);
                            }
                            else
                            {
                                BeginPlaylistTransition(pending);
                            }

                            continue;
                        }

                        framesThis = (int)Math.Min(
                            framesThis,
                            pending.TriggerSample - samplePos);
                    }
                    else
                    {
                        if (samplePos >= pending.SyncBoundarySample)
                        {
                            BeginPlaylistTransition(pending);
                            continue;
                        }

                        framesThis = (int)Math.Min(
                            framesThis,
                            pending.SyncBoundarySample - samplePos);
                    }
                }

                if (plan is { } loop)
                {
                    if (samplePos >= loop.LoopEndSample)
                    {
                        WrapMainToLoopStart(loop);
                        continue;
                    }

                    var untilEnd = loop.LoopEndSample - samplePos;
                    if (untilEnd <= 0)
                    {
                        WrapMainToLoopStart(loop);
                        continue;
                    }

                    framesThis = (int)Math.Min(framesThis, untilEnd);
                }

                if (playlistEnd is long rangeEnd)
                {
                    if (samplePos >= rangeEnd)
                    {
                        break;
                    }

                    framesThis = (int)Math.Min(framesThis, rangeEnd - samplePos);
                }

                // メインを float バッファへ
                EnsureBuffer(ref _mainFloat, framesThis * 8);
                var gotFrames = ReadDecodedFrames(_source, _mainFloat, 0, framesThis);
                if (gotFrames <= 0)
                {
                    break;
                }

                ApplyRegionEdgeGain(_mainFloat, gotFrames, samplePos);

                // Exit レイヤ（同時長）。停止／終了後は 0。
                // このチャンク読了後に折り返す場合は、折り返し後の続きで Exit が乗る。
                EnsureBuffer(ref _exitFloat, gotFrames * 8);
                Array.Clear(_exitFloat, 0, gotFrames * 8);
                if (exitPlaying)
                {
                    var exitPos = CurrentSample(_exitSource);
                    MixExitLayer(_exitFloat, 0, gotFrames, exitStart, exitEnd);
                    ApplyRegionEdgeGain(_exitFloat, gotFrames, exitPos);
                }

                EnsureBuffer(ref _playlistFadeFloat, gotFrames * 8);
                Array.Clear(_playlistFadeFloat, 0, gotFrames * 8);
                if (playlistFadePlaying)
                {
                    var fadePos = CurrentSample(_playlistFadeSource);
                    MixPlaylistFade(_playlistFadeFloat, 0, gotFrames);
                    ApplyRegionEdgeGain(_playlistFadeFloat, gotFrames, fadePos);
                }

                EnsureBuffer(ref _playlistPreRollFloat, gotFrames * 8);
                Array.Clear(_playlistPreRollFloat, 0, gotFrames * 8);
                if (playlistPreRollPlaying)
                {
                    var preRollPos = CurrentSample(_playlistPreRollSource);
                    _ = ReadDecodedFrames(
                        _playlistPreRollSource,
                        _playlistPreRollFloat,
                        0,
                        gotFrames);
                    ApplyRegionEdgeGain(_playlistPreRollFloat, gotFrames, preRollPos);
                }

                ApplyFadeIn(
                    _mainFloat,
                    gotFrames,
                    ref _playlistMainFadeInPlaying,
                    ref _playlistMainFadeInFramesRead,
                    _playlistMainFadeInFrameCount,
                    "main");
                ApplyFadeIn(
                    _playlistPreRollFloat,
                    gotFrames,
                    ref _playlistPreRollFadeInPlaying,
                    ref _playlistPreRollFadeInFramesRead,
                    _playlistPreRollFadeInFrameCount,
                    "pre-roll");
                ApplyClockFadeOut(_mainFloat, gotFrames);

                EnsureBuffer(ref _overlayMixFloat, gotFrames * 8);
                Array.Clear(_overlayMixFloat, 0, gotFrames * 8);
                MixOverlayPlaylistVoices(_overlayMixFloat, gotFrames);

                bool metronomeEnabled;
                float metronomeVolume;
                IReadOnlyList<WaveformBarMark> metronomeBars;
                float[] metronomeHigh;
                float[] metronomeLow;
                lock (_gate)
                {
                    metronomeEnabled = _metronomeEnabled;
                    metronomeVolume = _metronomeVolume;
                    metronomeBars = _metronomeBars;
                    metronomeHigh = _metronomeHigh;
                    metronomeLow = _metronomeLow;
                }

                // 加算ミックス（簡易クリップ）。-R 区間はタイムラインを進めつつ無音にする。
                // メトロノームは除外区間でも再生サンプル位置に同期して重ねる。
                for (var i = 0; i < gotFrames; i++)
                {
                    var absSample = samplePos + i;
                    var metro = NextMetronomeSample(
                        absSample,
                        metronomeEnabled,
                        metronomeVolume,
                        metronomeBars,
                        metronomeHigh,
                        metronomeLow);

                    float outputL;
                    float outputR;
                    if (IsExcludedSample(absSample, excludedRanges))
                    {
                        outputL = metro;
                        outputR = metro;
                    }
                    else
                    {
                        var mainL = BitConverter.ToSingle(_mainFloat, i * 8);
                        var mainR = BitConverter.ToSingle(_mainFloat, i * 8 + 4);
                        var exitL = BitConverter.ToSingle(_exitFloat, i * 8);
                        var exitR = BitConverter.ToSingle(_exitFloat, i * 8 + 4);
                        var fadeL = BitConverter.ToSingle(_playlistFadeFloat, i * 8);
                        var fadeR = BitConverter.ToSingle(_playlistFadeFloat, i * 8 + 4);
                        var preRollL = BitConverter.ToSingle(_playlistPreRollFloat, i * 8);
                        var preRollR = BitConverter.ToSingle(_playlistPreRollFloat, i * 8 + 4);
                        var overlayL = BitConverter.ToSingle(_overlayMixFloat, i * 8);
                        var overlayR = BitConverter.ToSingle(_overlayMixFloat, i * 8 + 4);
                        outputL = ClampSample(mainL + exitL + fadeL + preRollL + overlayL + metro);
                        outputR = ClampSample(mainR + exitR + fadeR + preRollR + overlayR + metro);
                    }

                    outputPeak = Math.Max(
                        outputPeak,
                        Math.Max(Math.Abs(outputL), Math.Abs(outputR)));
                    BitConverter.TryWriteBytes(
                        buffer.AsSpan(outIndex + i * 8, 4),
                        outputL);
                    BitConverter.TryWriteBytes(
                        buffer.AsSpan(outIndex + i * 8 + 4, 4),
                        outputR);
                }

                PushMonitorSamples(buffer, outIndex, gotFrames);
                outIndex += gotFrames * 8;
                totalFrames += gotFrames;
            }

            Volatile.Write(ref _outputPeak, outputPeak);
            return totalFrames * 8;
        }

        /// <summary>
        /// 再生サンプル位置の拍境界で High／Low をアームし、進行中クリックの 1 サンプルを返す。
        /// 呼び出しは <see cref="ReadCore"/>（<_readGate>）上のみ。
        /// 同一拍内は O(1)。小節跨ぎ／拍境界でのみグリッドを進める（毎サンプルの全マーク走査はしない）。
        /// </summary>
        private float NextMetronomeSample(
            long absSample,
            bool enabled,
            float volume,
            IReadOnlyList<WaveformBarMark> bars,
            float[] high,
            float[] low)
        {
            if (!enabled || bars.Count == 0 || high.Length == 0 || low.Length == 0)
            {
                _metronomeActiveClick = null;
                _metronomeClickPos = -1;
                _metronomeArmedBeatKey = null;
                _metronomeLastAbsSample = -1;
                _metronomeCurrentBeatZeroBased = -1;
                return 0f;
            }

            // 同一拍内ホットパス: 解決・マーク走査なし。
            if (_metronomeArmedBeatKey is not null
                && _metronomeCurrentBeatZeroBased >= 0
                && absSample >= _metronomeCurrentBeatStart
                && absSample < _metronomeCurrentNextBeat
                && absSample >= _metronomeCachedBarStart
                && absSample < _metronomeCachedBarEnd)
            {
                _metronomeLastAbsSample = absSample;
                return ReadMetronomeClickSample(volume);
            }

            if (TryResolveMusicalBeatAtSample(
                    bars,
                    absSample,
                    out var barNumber,
                    out var beat,
                    out var beatStartSample,
                    out var nextBeatSample)
                && beat >= 1)
            {
                var beatKey = ((long)barNumber << 16) | (uint)beat;
                if (_metronomeArmedBeatKey is not long lastKey)
                {
                    _metronomeArmedBeatKey = beatKey;
                    _metronomeLastAbsSample = absSample;
                }
                else if (beatKey != lastKey)
                {
                    var sampleDelta = absSample - _metronomeLastAbsSample;
                    // 到着拍の実長のみ使う（BPM 再走査しない）。
                    var beatSamples = Math.Max(1L, nextBeatSample - beatStartSample);
                    _metronomeLastAbsSample = absSample;
                    _metronomeArmedBeatKey = beatKey;
                    // 前方への大きなジャンプ（シーク等）はクリックしない。ループ折り返しは鳴らす。
                    if (sampleDelta <= beatSamples * 2L)
                    {
                        _metronomeActiveClick = beat == 1 ? high : low;
                        _metronomeClickPos = 0;
                    }
                }
                else
                {
                    _metronomeLastAbsSample = absSample;
                }
            }

            return ReadMetronomeClickSample(volume);
        }

        private float ReadMetronomeClickSample(float volume)
        {
            if (_metronomeActiveClick is not { Length: > 0 } click
                || _metronomeClickPos < 0
                || _metronomeClickPos >= click.Length)
            {
                _metronomeActiveClick = null;
                _metronomeClickPos = -1;
                return 0f;
            }

            var sample = click[_metronomeClickPos] * volume;
            _metronomeClickPos++;
            if (_metronomeClickPos >= click.Length)
            {
                _metronomeActiveClick = null;
                _metronomeClickPos = -1;
            }

            return sample;
        }

        private bool TryResolveMusicalBeatAtSample(
            IReadOnlyList<WaveformBarMark> bars,
            long positionSample,
            out int barNumber,
            out int beat,
            out long beatStartSample,
            out long nextBeatSample)
        {
            barNumber = 0;
            beat = 1;
            beatStartSample = 0;
            nextBeatSample = 0;

            var frameCount = _sourceBlockAlign <= 0 ? 0L : _source.Length / _sourceBlockAlign;
            if (frameCount <= 0 || bars.Count == 0)
            {
                return false;
            }

            positionSample = Math.Clamp(positionSample, 0L, frameCount - 1);
            var sampleRate = WaveFormat.SampleRate;

            if (_metronomeCachedBarStart >= 0
                && positionSample >= _metronomeCachedBarStart
                && positionSample < _metronomeCachedBarEnd
                && _metronomeCachedNumerator > 0
                && _metronomeCachedDenominator > 0
                && _metronomeCachedBeatStarts.Length == _metronomeCachedNumerator)
            {
                // 連続再生は前方へしか進まないので、現在拍から前方スキャン。
                var beatZeroBased = _metronomeCurrentBeatZeroBased;
                if (beatZeroBased < 0
                    || beatZeroBased >= _metronomeCachedBeatStarts.Length
                    || positionSample < _metronomeCachedBeatStarts[beatZeroBased])
                {
                    beatZeroBased = 0;
                }

                while (beatZeroBased + 1 < _metronomeCachedBeatStarts.Length
                    && positionSample >= _metronomeCachedBeatStarts[beatZeroBased + 1])
                {
                    beatZeroBased++;
                }

                ApplyCurrentBeat(beatZeroBased);
                barNumber = _metronomeCachedBarNumber;
                beat = beatZeroBased + 1;
                beatStartSample = _metronomeCurrentBeatStart;
                nextBeatSample = _metronomeCurrentNextBeat;
                return true;
            }

            if (!MetronomeBeatGrid.TryFindBarContext(
                    bars,
                    positionSample,
                    frameCount,
                    sampleRate,
                    out var bar,
                    out var state,
                    out var barStartSample,
                    out var barEndSample))
            {
                return false;
            }

            var numerator = Math.Max(1, state.Numerator > 0 ? state.Numerator : bar.Numerator);
            var denominator = state.Denominator > 0 ? state.Denominator : bar.Denominator;
            var startBpm = bar.Bpm > 0d ? bar.Bpm : state.Bpm;
            if (denominator <= 0 || startBpm <= 0d)
            {
                return false;
            }

            var beatStarts = MetronomeBeatGrid.BuildBeatStarts(
                bars,
                barStartSample,
                barEndSample,
                startBpm,
                numerator,
                denominator,
                sampleRate);

            var resolvedBeatZeroBased = 0;
            while (resolvedBeatZeroBased + 1 < beatStarts.Length
                && positionSample >= beatStarts[resolvedBeatZeroBased + 1])
            {
                resolvedBeatZeroBased++;
            }

            _metronomeCachedBarStart = barStartSample;
            _metronomeCachedBarEnd = barEndSample;
            _metronomeCachedBarNumber = Math.Max(0, bar.BarNumber);
            _metronomeCachedBpm = state.Bpm > 0d ? state.Bpm : startBpm;
            _metronomeCachedNumerator = numerator;
            _metronomeCachedDenominator = denominator;
            _metronomeCachedBeatStarts = beatStarts;
            ApplyCurrentBeat(resolvedBeatZeroBased);

            barNumber = _metronomeCachedBarNumber;
            beat = resolvedBeatZeroBased + 1;
            beatStartSample = _metronomeCurrentBeatStart;
            nextBeatSample = _metronomeCurrentNextBeat;
            return true;
        }

        private void ApplyCurrentBeat(int beatZeroBased)
        {
            _metronomeCurrentBeatZeroBased = beatZeroBased;
            _metronomeCurrentBeatStart = _metronomeCachedBeatStarts[beatZeroBased];
            _metronomeCurrentNextBeat = beatZeroBased + 1 < _metronomeCachedBeatStarts.Length
                ? _metronomeCachedBeatStarts[beatZeroBased + 1]
                : _metronomeCachedBarEnd;
        }

        private void PushMonitorSamples(byte[] buffer, int offset, int frames)
        {
            lock (_monitorGate)
            {
                for (var i = 0; i < frames; i++)
                {
                    var left = BitConverter.ToSingle(buffer, offset + i * 8);
                    var right = BitConverter.ToSingle(buffer, offset + i * 8 + 4);
                    _monitorRing[(int)(_monitorWriteCount % _monitorRing.Length)] =
                        (left + right) * 0.5f;
                    _monitorWriteCount++;
                }
            }
        }

        /// <summary>直近サンプルを destination の末尾詰めでコピー（不足分は先頭を 0 埋め）。</summary>
        public int CopyRecentOutputSamples(float[] destination)
        {
            lock (_monitorGate)
            {
                var available = (int)Math.Min(
                    _monitorWriteCount,
                    Math.Min(destination.Length, _monitorRing.Length));
                var start = _monitorWriteCount - available;
                for (var i = 0; i < available; i++)
                {
                    destination[destination.Length - available + i] =
                        _monitorRing[(int)((start + i) % _monitorRing.Length)];
                }

                if (available < destination.Length)
                {
                    Array.Clear(destination, 0, destination.Length - available);
                }

                return available;
            }
        }

        private void BeginPlaylistPreRoll(PlaylistTransitionRequest transition)
        {
            SeekToSample(_playlistPreRollSource, transition.TargetStartSample);
            lock (_gate)
            {
                _playlistPreRollPlaying = true;
                _playlistPreRollFadeInFrameCount = transition.FadeInFrameCount;
                _playlistPreRollFadeInFramesRead = 0;
                _playlistPreRollFadeInPlaying =
                    transition.FadeInFrameCount > 0;
                _playlistStartedGeneration = transition.Generation;
                _playlistStartedTargetSample = transition.TargetStartSample;
            }

            _diagnostic(
                $"provider.playlist-preroll-start generation={transition.Generation}"
                + $" trigger={transition.TriggerSample}"
                + $" sync={transition.SyncBoundarySample}"
                + $" targetStart={transition.TargetStartSample}"
                + $" targetEntry={transition.TargetEntrySample}"
                + $" fadeInFrames={transition.FadeInFrameCount}");
        }

        private void BeginPlaylistTransition(PlaylistTransitionRequest transition)
        {
            // 同期境界までは旧 Playlist をメインで維持し、ここから専用リーダーでフェードする。
            SeekToSample(_playlistFadeSource, transition.SyncBoundarySample);
            long exitFadeStart = 0;
            long exitFadeEnd = 0;
            lock (_gate)
            {
                if (_exitPlaying)
                {
                    exitFadeStart = CurrentSample(_exitSource);
                    exitFadeEnd = _exitEndSample;
                }
            }

            var carryExitFade = exitFadeEnd > exitFadeStart;
            if (carryExitFade)
            {
                SeekToSample(_playlistExitFadeSource, exitFadeStart);
            }

            SeekToSample(_source, transition.TargetEntrySample);
            ResetMetronomeScheduleNoLock();
            var continuedFromPreRoll = false;
            var sourceExitWillBeMaintained = false;
            lock (_gate)
            {
                var oldPlan = _activePlan;
                continuedFromPreRoll = _playlistPreRollPlaying;
                sourceExitWillBeMaintained = carryExitFade
                    || (oldPlan is { HasExit: true } sourcePlan
                        && transition.FadeFrameCount
                            > Math.Max(
                                0L,
                                sourcePlan.LoopEndSample
                                - transition.SyncBoundarySample));
                // 同時に保持できる旧フェードはこの1本だけ。再遷移時は上書きして先頭から開始。
                // Fade Out=None（0フレーム）のときは旧ソースを重ねず即切り替え。
                _playlistFadePlaying = transition.FadeFrameCount > 0;
                _playlistFadeStartSample = transition.SyncBoundarySample;
                _playlistFadeEndSample = transition.FadeSourceEndSample;
                _playlistFadeStartTickMs = Environment.TickCount64;
                _playlistFadeExitStartSample = oldPlan is { HasExit: true }
                    ? oldPlan.Value.LoopEndSample
                    : 0;
                _playlistFadeExitEndSample = oldPlan is { HasExit: true }
                    ? oldPlan.Value.ExitEndSample!.Value
                    : 0;
                _playlistFadeIncomingFramesRead =
                    _playlistMainFadeInPlaying
                        ? _playlistMainFadeInFramesRead
                        : 0;
                _playlistFadeIncomingFrameCount =
                    _playlistMainFadeInPlaying
                        ? _playlistMainFadeInFrameCount
                        : 0;
                _playlistExitFadePlaying = carryExitFade;
                _playlistExitFadeEndSample = exitFadeEnd;
                _playlistFadeFramesRead = 0;
                _playlistFadeFrameCount = transition.FadeFrameCount;
                _playlistPreRollPlaying = false;
                if (continuedFromPreRoll)
                {
                    _playlistMainFadeInPlaying =
                        _playlistPreRollFadeInPlaying;
                    _playlistMainFadeInFramesRead =
                        _playlistPreRollFadeInFramesRead;
                    _playlistMainFadeInFrameCount =
                        _playlistPreRollFadeInFrameCount;
                }
                else
                {
                    _playlistMainFadeInFrameCount =
                        transition.FadeInFrameCount;
                    _playlistMainFadeInFramesRead = 0;
                    _playlistMainFadeInPlaying =
                        transition.FadeInFrameCount > 0;
                }
                ResetPreRollFadeInNoLock();
                _playlistStartSample = transition.TargetStartSample;
                _playlistEndSample = transition.TargetEndSample;
                _activePlan = transition.TargetPlan;
                _exitPlaying = false;
                _pendingPlaylistTransition = null;
                _playlistStartedGeneration = transition.Generation;
                _playlistStartedTargetSample = transition.TargetStartSample;
            }
            _diagnostic(
                $"provider.playlist-transition-start generation={transition.Generation}"
                + $" trigger={transition.TriggerSample}"
                + $" sync={transition.SyncBoundarySample}"
                + $" targetStart={transition.TargetStartSample}"
                + $" targetEntry={transition.TargetEntrySample}"
                + $" targetEnd={transition.TargetEndSample}"
                + $" fadeInFrames={transition.FadeInFrameCount}"
                + $" fadeInContinuedFromPreRoll={continuedFromPreRoll}"
                + $" sourceExitWillBeMaintained={sourceExitWillBeMaintained}"
                + $" oldExitCarried={carryExitFade}"
                + $" oldExitStart={exitFadeStart}"
                + $" oldExitEnd={exitFadeEnd}");
        }

        private void MixPlaylistFade(byte[] dest, int destOffset, int frames)
        {
            var framesRemaining = _playlistFadeFrameCount - _playlistFadeFramesRead;
            if (framesRemaining <= 0)
            {
                StopPlaylistFade();
                return;
            }

            var chunk = (int)Math.Min(frames, framesRemaining);
            var sourceRemaining = Math.Max(
                0L,
                _playlistFadeEndSample - CurrentSample(_playlistFadeSource));
            var mainFrames = (int)Math.Min(chunk, sourceRemaining);
            var mainGot = mainFrames > 0
                ? ReadDecodedFrames(_playlistFadeSource, dest, destOffset, mainFrames)
                : 0;

            var exitGot = 0;
            if (_playlistExitFadePlaying)
            {
                var exitRemaining = Math.Max(
                    0L,
                    _playlistExitFadeEndSample - CurrentSample(_playlistExitFadeSource));
                var exitFrames = (int)Math.Min(chunk, exitRemaining);
                if (exitFrames > 0)
                {
                    EnsureBuffer(ref _playlistExitFadeFloat, exitFrames * 8);
                    exitGot = ReadDecodedFrames(
                        _playlistExitFadeSource,
                        _playlistExitFadeFloat,
                        0,
                        exitFrames);
                    for (var i = 0; i < exitGot; i++)
                    {
                        var at = destOffset + i * 8;
                        BitConverter.TryWriteBytes(
                            dest.AsSpan(at, 4),
                            ClampSample(
                                BitConverter.ToSingle(dest, at)
                                + BitConverter.ToSingle(_playlistExitFadeFloat, i * 8)));
                        BitConverter.TryWriteBytes(
                            dest.AsSpan(at + 4, 4),
                            ClampSample(
                                BitConverter.ToSingle(dest, at + 4)
                                + BitConverter.ToSingle(_playlistExitFadeFloat, i * 8 + 4)));
                    }
                }
            }

            var got = Math.Max(mainGot, exitGot);
            for (var i = 0; i < got; i++)
            {
                var fadeIndex = _playlistFadeFramesRead + i;
                var fadeOutGain = _playlistFadeFrameCount <= 1
                    ? 0f
                    : 1f - fadeIndex / (float)(_playlistFadeFrameCount - 1);
                var fadeInGain = _playlistFadeIncomingFrameCount <= 0
                    ? 1f
                    : _playlistFadeIncomingFrameCount <= 1
                        ? 1f
                        : Math.Min(
                            1f,
                            (_playlistFadeIncomingFramesRead + fadeIndex)
                            / (float)(_playlistFadeIncomingFrameCount - 1));
                var gain = fadeOutGain * fadeInGain;
                var at = destOffset + i * 8;
                BitConverter.TryWriteBytes(
                    dest.AsSpan(at, 4),
                    BitConverter.ToSingle(dest, at) * gain);
                BitConverter.TryWriteBytes(
                    dest.AsSpan(at + 4, 4),
                    BitConverter.ToSingle(dest, at + 4) * gain);
            }

            _playlistFadeFramesRead += got;
            if (got <= 0 || _playlistFadeFramesRead >= _playlistFadeFrameCount)
            {
                StopPlaylistFade();
            }
        }

        private void StopPlaylistFade()
        {
            lock (_gate)
            {
                _playlistFadePlaying = false;
                _playlistFadeStartSample = 0;
                _playlistFadeStartTickMs = 0;
                _playlistFadeExitStartSample = 0;
                _playlistFadeExitEndSample = 0;
                _playlistFadeIncomingFramesRead = 0;
                _playlistFadeIncomingFrameCount = 0;
                _playlistExitFadePlaying = false;
            }
        }

        private void ApplyRegionEdgeGain(byte[] buffer, int frames, long startSample)
        {
            IReadOnlyList<RegionEdgeFade> fades;
            lock (_gate)
            {
                fades = _regionEdgeFades;
            }

            if (fades.Count == 0 || frames <= 0)
            {
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var gain = RegionEdgeFade.GainAt(startSample + i, fades);
                if (Math.Abs(gain - 1f) < 1e-6f)
                {
                    continue;
                }

                var at = i * 8;
                BitConverter.TryWriteBytes(
                    buffer.AsSpan(at, 4),
                    BitConverter.ToSingle(buffer, at) * gain);
                BitConverter.TryWriteBytes(
                    buffer.AsSpan(at + 4, 4),
                    BitConverter.ToSingle(buffer, at + 4) * gain);
            }
        }

        private void ApplyClockFadeOut(byte[] buffer, int frames)
        {
            bool playing;
            long framesRead;
            int frameCount;
            lock (_gate)
            {
                playing = _clockFadeOutPlaying;
                framesRead = _clockFadeOutFramesRead;
                frameCount = _clockFadeOutFrameCount;
            }

            if (!playing || frameCount <= 0 || frames <= 0)
            {
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var fadeIndex = framesRead + i;
                var gain = frameCount <= 1
                    ? 0f
                    : Math.Max(
                        0f,
                        1f - fadeIndex / (float)(frameCount - 1));
                var at = i * 8;
                BitConverter.TryWriteBytes(
                    buffer.AsSpan(at, 4),
                    BitConverter.ToSingle(buffer, at) * gain);
                BitConverter.TryWriteBytes(
                    buffer.AsSpan(at + 4, 4),
                    BitConverter.ToSingle(buffer, at + 4) * gain);
            }

            lock (_gate)
            {
                if (!_clockFadeOutPlaying)
                {
                    return;
                }

                _clockFadeOutFramesRead += frames;
                if (_clockFadeOutFramesRead < _clockFadeOutFrameCount)
                {
                    return;
                }

                _clockFadeOutPlaying = false;
                _playlistStartSample = null;
                _playlistEndSample = null;
                _clockPlaylistVoiceId = 0;
                _stopAfterClockFadeOut = true;
                _forceEndAfterClockFadeOut = true;
                _diagnostic("provider.clock-fade-out-complete");
            }
        }

        private void MixOverlayPlaylistVoices(byte[] dest, int frames)
        {
            OverlayPlaylistVoice[] snapshot;
            lock (_gate)
            {
                var active = 0;
                foreach (var voice in _overlayVoices)
                {
                    if (voice.Active)
                    {
                        active++;
                    }
                }

                if (active == 0)
                {
                    return;
                }

                snapshot = _overlayVoices;
            }

            EnsureBuffer(ref _overlayExitFloat, frames * 8);

            foreach (var voice in snapshot)
            {
                bool active;
                bool fadeIn;
                bool fadeOut;
                long fadeRead;
                int fadeCount;
                long partEnd;
                LoopPlaybackPlan? loopPlan;
                bool exitPlaying;
                long exitStart;
                long exitEnd;
                lock (_gate)
                {
                    active = voice.Active;
                    fadeIn = voice.FadeInPlaying;
                    fadeOut = voice.FadeOutPlaying;
                    fadeRead = voice.FadeFramesRead;
                    fadeCount = voice.FadeFrameCount;
                    partEnd = voice.PartEndSample;
                    loopPlan = voice.LoopPlan;
                    exitPlaying = voice.ExitPlaying;
                    exitStart = voice.ExitStartSample;
                    exitEnd = voice.ExitEndSample;
                    if (!active)
                    {
                        continue;
                    }
                }

                var pos = CurrentSample(voice.Reader);
                if (loopPlan is { } loop)
                {
                    if (pos >= loop.LoopEndSample)
                    {
                        SeekToSample(voice.Reader, loop.LoopStartSample);
                        BeginOverlayExitOnLoopWrap(voice, loop);
                        pos = loop.LoopStartSample;
                        lock (_gate)
                        {
                            exitPlaying = voice.ExitPlaying;
                            exitStart = voice.ExitStartSample;
                            exitEnd = voice.ExitEndSample;
                        }
                    }
                }
                else if (pos >= partEnd)
                {
                    lock (_gate)
                    {
                        voice.Active = false;
                        voice.FadeInPlaying = false;
                        voice.FadeOutPlaying = false;
                        voice.ExitPlaying = false;
                    }

                    continue;
                }

                var limit = partEnd;
                if (loopPlan is { } activeLoop)
                {
                    limit = Math.Min(limit, activeLoop.LoopEndSample);
                }

                var framesThis = (int)Math.Min(frames, Math.Max(0L, limit - pos));
                EnsureBuffer(ref voice.FloatBuffer, frames * 8);
                Array.Clear(voice.FloatBuffer, 0, frames * 8);
                var got = framesThis > 0
                    ? ReadDecodedFrames(voice.Reader, voice.FloatBuffer, 0, framesThis)
                    : 0;
                if (got <= 0 && !exitPlaying)
                {
                    lock (_gate)
                    {
                        voice.Active = false;
                        voice.ExitPlaying = false;
                    }

                    continue;
                }

                if (got > 0)
                {
                    ApplyRegionEdgeGain(voice.FloatBuffer, got, pos);
                }

                Array.Clear(_overlayExitFloat, 0, frames * 8);
                var exitGot = 0;
                if (exitPlaying)
                {
                    exitGot = MixOverlayExitLayer(
                        voice,
                        _overlayExitFloat,
                        Math.Max(got, frames),
                        exitStart,
                        exitEnd);
                }

                var mixFrames = Math.Max(got, exitGot);
                for (var i = 0; i < mixFrames; i++)
                {
                    var gain = 1f;
                    if (i < got && (fadeIn || fadeOut) && fadeCount > 0)
                    {
                        var fadeIndex = fadeRead + i;
                        if (fadeIn)
                        {
                            gain = fadeCount <= 1
                                ? 1f
                                : Math.Min(1f, fadeIndex / (float)(fadeCount - 1));
                        }
                        else
                        {
                            gain = fadeCount <= 1
                                ? 0f
                                : Math.Max(0f, 1f - fadeIndex / (float)(fadeCount - 1));
                        }
                    }

                    var at = i * 8;
                    var left = i < got
                        ? BitConverter.ToSingle(voice.FloatBuffer, at) * gain
                        : 0f;
                    var right = i < got
                        ? BitConverter.ToSingle(voice.FloatBuffer, at + 4) * gain
                        : 0f;
                    if (i < exitGot)
                    {
                        left = ClampSample(
                            left + BitConverter.ToSingle(_overlayExitFloat, at));
                        right = ClampSample(
                            right + BitConverter.ToSingle(_overlayExitFloat, at + 4));
                    }

                    BitConverter.TryWriteBytes(
                        dest.AsSpan(at, 4),
                        ClampSample(BitConverter.ToSingle(dest, at) + left));
                    BitConverter.TryWriteBytes(
                        dest.AsSpan(at + 4, 4),
                        ClampSample(BitConverter.ToSingle(dest, at + 4) + right));
                }

                lock (_gate)
                {
                    if (!voice.Active)
                    {
                        continue;
                    }

                    if ((fadeIn || fadeOut) && got > 0)
                    {
                        voice.FadeFramesRead += got;
                        if (voice.FadeFramesRead >= voice.FadeFrameCount)
                        {
                            if (fadeOut)
                            {
                                voice.Active = false;
                                voice.FadeOutPlaying = false;
                                voice.ExitPlaying = false;
                                _diagnostic(
                                    $"provider.overlay-fade-out-complete voice={voice.VoiceId}");
                            }
                            else
                            {
                                voice.FadeInPlaying = false;
                                _diagnostic(
                                    $"provider.overlay-fade-in-complete voice={voice.VoiceId}");
                            }
                        }
                    }
                }
            }
        }

        private void BeginOverlayExitOnLoopWrap(
            OverlayPlaylistVoice voice,
            LoopPlaybackPlan loop)
        {
            if (!_playExitLayer || !loop.HasExit)
            {
                lock (_gate)
                {
                    voice.ExitPlaying = false;
                }

                return;
            }

            SeekToSample(voice.ExitReader, loop.LoopEndSample);
            lock (_gate)
            {
                voice.ExitStartSample = loop.LoopEndSample;
                voice.ExitEndSample = loop.ExitEndSample!.Value;
                voice.ExitPlaying = true;
            }

            _diagnostic(
                $"provider.overlay-exit-start voice={voice.VoiceId}"
                + $" start={loop.LoopEndSample} end={loop.ExitEndSample}");
        }

        private int MixOverlayExitLayer(
            OverlayPlaylistVoice voice,
            byte[] dest,
            int frames,
            long exitStart,
            long exitEnd)
        {
            Array.Clear(dest, 0, frames * 8);
            bool playing;
            lock (_gate)
            {
                playing = voice.ExitPlaying;
            }

            if (!playing || frames <= 0)
            {
                return 0;
            }

            var pos = CurrentSample(voice.ExitReader);
            if (pos < exitStart)
            {
                SeekToSample(voice.ExitReader, exitStart);
                pos = exitStart;
            }

            if (pos >= exitEnd)
            {
                lock (_gate)
                {
                    voice.ExitPlaying = false;
                }

                return 0;
            }

            var chunk = (int)Math.Min(frames, exitEnd - pos);
            var got = ReadDecodedFrames(voice.ExitReader, dest, 0, chunk);
            if (got <= 0)
            {
                lock (_gate)
                {
                    voice.ExitPlaying = false;
                }

                return 0;
            }

            ApplyRegionEdgeGain(dest, got, pos);
            if (CurrentSample(voice.ExitReader) >= exitEnd)
            {
                lock (_gate)
                {
                    voice.ExitPlaying = false;
                }
            }

            return got;
        }

        public void SetClockPlaylistVoiceId(int voiceId)
        {
            lock (_gate)
            {
                _clockPlaylistVoiceId = voiceId;
            }
        }

        public int GetClockPlaylistVoiceId()
        {
            lock (_gate)
            {
                return _clockPlaylistVoiceId;
            }
        }

        private void ApplyFadeIn(
            byte[] buffer,
            int frames,
            ref bool playing,
            ref long framesRead,
            int frameCount,
            string layer)
        {
            if (!playing || frameCount <= 0 || frames <= 0)
            {
                return;
            }

            for (var i = 0; i < frames; i++)
            {
                var fadeIndex = framesRead + i;
                var gain = frameCount <= 1
                    ? 1f
                    : Math.Min(
                        1f,
                        fadeIndex / (float)(frameCount - 1));
                var at = i * 8;
                BitConverter.TryWriteBytes(
                    buffer.AsSpan(at, 4),
                    BitConverter.ToSingle(buffer, at) * gain);
                BitConverter.TryWriteBytes(
                    buffer.AsSpan(at + 4, 4),
                    BitConverter.ToSingle(buffer, at + 4) * gain);
            }

            framesRead += frames;
            if (framesRead >= frameCount)
            {
                playing = false;
                _diagnostic(
                    $"provider.playlist-fade-in-complete layer={layer}"
                    + $" frames={frameCount}");
            }
        }

        private void ResetMainFadeInNoLock()
        {
            _playlistMainFadeInPlaying = false;
            _playlistMainFadeInFramesRead = 0;
            _playlistMainFadeInFrameCount = 0;
        }

        private void ResetPreRollFadeInNoLock()
        {
            _playlistPreRollFadeInPlaying = false;
            _playlistPreRollFadeInFramesRead = 0;
            _playlistPreRollFadeInFrameCount = 0;
        }

        private void MixExitLayer(byte[] dest, int destOffset, int frames, long exitStart, long exitEnd)
        {
            var written = 0;
            while (written < frames)
            {
                bool playing;
                lock (_gate)
                {
                    playing = _exitPlaying;
                    if (!playing)
                    {
                        return;
                    }
                }

                var pos = CurrentSample(_exitSource);
                if (pos < exitStart)
                {
                    SeekExitToSample(exitStart);
                    pos = exitStart;
                }

                if (pos >= exitEnd)
                {
                    lock (_gate)
                    {
                        _exitPlaying = false;
                    }

                    return;
                }

                var chunk = (int)Math.Min(frames - written, exitEnd - pos);
                var got = ReadDecodedFrames(_exitSource, dest, destOffset + written * 8, chunk);
                if (got <= 0)
                {
                    lock (_gate)
                    {
                        _exitPlaying = false;
                    }

                    return;
                }

                written += got;
                if (CurrentSample(_exitSource) >= exitEnd)
                {
                    lock (_gate)
                    {
                        _exitPlaying = false;
                    }

                    return;
                }
            }
        }

        private int ReadDecodedFrames(WaveFileReader reader, byte[] dest, int destOffset, int frames)
        {
            if (frames <= 0)
            {
                return 0;
            }

            var sourceBytes = frames * _sourceBlockAlign;
            EnsureBuffer(ref _pcmScratch, sourceBytes);

            var got = reader.Read(_pcmScratch, 0, sourceBytes);
            var gotFrames = got / _sourceBlockAlign;
            var writeAt = destOffset;
            for (var i = 0; i < gotFrames; i++)
            {
                var frameOffset = i * _sourceBlockAlign;
                float left;
                float right;
                if (_channels == 1)
                {
                    left = right = _sampleReader(_pcmScratch, frameOffset);
                }
                else
                {
                    left = _sampleReader(_pcmScratch, frameOffset);
                    right = _sampleReader(_pcmScratch, frameOffset + _bytesPerSample);
                    for (var ch = 2; ch < _channels; ch++)
                    {
                        var v = _sampleReader(_pcmScratch, frameOffset + ch * _bytesPerSample) * FoldGain;
                        left += v;
                        right += v;
                    }

                    left *= _normalize;
                    right *= _normalize;
                }

                BitConverter.TryWriteBytes(dest.AsSpan(writeAt, 4), left);
                BitConverter.TryWriteBytes(dest.AsSpan(writeAt + 4, 4), right);
                writeAt += 8;
            }

            return gotFrames;
        }

        private static void EnsureBuffer(ref byte[] buffer, int bytes)
        {
            if (buffer.Length < bytes)
            {
                buffer = new byte[bytes];
            }
        }

        private static float ClampSample(float value) =>
            value < -1f ? -1f : value > 1f ? 1f : value;

        private static bool IsExcludedSample(
            long sample,
            IReadOnlyList<(long Start, long End)> ranges)
        {
            foreach (var (start, end) in ranges)
            {
                if (sample < start)
                {
                    return false;
                }

                if (sample < end)
                {
                    return true;
                }
            }

            return false;
        }

        private long CurrentSample(WaveFileReader reader) =>
            _sourceBlockAlign <= 0 ? 0 : reader.Position / _sourceBlockAlign;

        private void SeekToSample(WaveFileReader reader, long sample)
        {
            var safe = Math.Max(0, sample);
            reader.Position = safe * (long)_sourceBlockAlign;
        }

        private void SeekExitToSample(long sample) => SeekToSample(_exitSource, sample);

        private sealed record PlaylistTransitionRequest(
            long TargetStartSample,
            long TargetEntrySample,
            long TargetEndSample,
            long TriggerSample,
            long SyncBoundarySample,
            long FadeSourceEndSample,
            int FadeInFrameCount,
            int FadeFrameCount,
            LoopPlaybackPlan? TargetPlan,
            long Generation);

        private sealed class OverlayPlaylistVoice(WaveFileReader reader, WaveFileReader exitReader)
        {
            public WaveFileReader Reader { get; } = reader;
            public WaveFileReader ExitReader { get; } = exitReader;
            public byte[] FloatBuffer = [];
            public int VoiceId;
            public long PartStartSample;
            public long PartEndSample;
            public LoopPlaybackPlan? LoopPlan;
            public bool ExitPlaying;
            public long ExitStartSample;
            public long ExitEndSample;
            public bool Active;
            public bool FadeInPlaying;
            public bool FadeOutPlaying;
            public long FadeFramesRead;
            public int FadeFrameCount;
        }
    }

}
