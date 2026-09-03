using NAudio.Wave;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wave;

internal enum PlaylistDestinationSyncMode
{
    EntryCue,
    SameTime,
}

/// <summary>
/// Wave ファイルの再生。位置は Position で取得する。
/// 変換は自前で行い ACM ドライバに依存しない（マルチチャンネル／Extensible 対応）。
/// <c>-L</c> 連続区間は <see cref="SetLoopPlans"/> で登録し、
/// <see cref="ArmLoopAtProgress"/> で有効化した区間だけ末尾→先頭へシームレス折り返す。
/// 直後が <c>-E</c> のときは、ループ末端で頭へ戻る瞬間に Exit をワンショット二重再生する（Wwise 相当）。
/// <see cref="PlayExitLayer"/> が false のときは -L ループのみ（-E は鳴らさない）。
/// シークで区間外へ出るとループ／Exit とも直ちに解除される。
/// </summary>
internal sealed partial class WaveAudioPlayer : IDisposable
{
    /// <summary>グループ内重ね再生の最大本数（クロック＋上乗せ）。</summary>
    public const int MaxPlaylistVoices = 8;

    private WaveFileReader? _reader;
    private WaveFileReader? _exitReader;
    private WaveFileReader? _playlistFadeReader;
    private WaveFileReader? _playlistExitFadeReader;
    private WaveFileReader? _playlistPreRollReader;
    private readonly WaveFileReader?[] _overlayReaders = new WaveFileReader?[MaxPlaylistVoices - 1];
    private readonly WaveFileReader?[] _overlayExitReaders = new WaveFileReader?[MaxPlaylistVoices - 1];
    private IWavePlayer? _output;
    private StereoFloatWaveProvider? _provider;
    private string? _path;
    /// <summary>再生専用の一時コピー。元ファイルをロックしない。</summary>
    private string? _playbackCopyPath;
    private bool _isPlaying;
    private bool _disposed;
    private bool _suppressPlaybackEnded;
    /// <summary>ループ折り返し時に -E を二重再生するか（既定 false）。</summary>
    private bool _playExitLayer;
    /// <summary>
    /// 次の Play 前に出力デバイスを作り直し、先読みバッファを破棄するか。
    /// Pause／Stop／一時停止中のシーク後はデバイス側に旧位置が残り得る（ASIO で顕著）。
    /// 再生中シークでは連続読み出しで自然に切り替わるため、ここでは立てない。
    /// </summary>
    private bool _discardOutputBufferBeforePlay;
    private LoopPlaybackPlan[] _loopPlans = [];
    private LoopPlaybackPlan? _activePlan;
    private AudioOutputSettings _outputSettings = AudioOutputSettings.Default;
    private float[] _metronomeHigh = [];
    private float[] _metronomeLow = [];
    private int _metronomeClickSampleRate;
    private bool _metronomeEnabled;
    private float _metronomeVolume = MetronomePlayer.DefaultVolume;
    private IReadOnlyList<WaveformBarMark> _metronomeBars = [];

    public event EventHandler? PlaybackEnded;
    public event EventHandler<string>? Diagnostic;

    public bool IsPlaying => _isPlaying;


    public bool HasSource => !string.IsNullOrEmpty(_path);

    public TimeSpan Position => _reader?.CurrentTime ?? TimeSpan.Zero;

    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    /// <summary>直近に生成した出力バッファのピーク値（0〜1）。</summary>
    public float OutputPeak => _provider?.OutputPeak ?? 0f;

    /// <summary>出力フォーマットのサンプルレート。未ロード時は 0。</summary>
    public int OutputSampleRate => _provider?.WaveFormat.SampleRate ?? 0;

    /// <summary>
    /// 直近の出力サンプル（モノラルミックス）を destination の末尾詰めでコピーする。
    /// スペアナ表示用。戻り値は書き込んだサンプル数。
    /// </summary>
    public int ReadRecentOutputSamples(float[] destination) =>
        _provider?.CopyRecentOutputSamples(destination) ?? 0;

    /// <summary>0〜1。長さ不明時は 0。</summary>
    public double Progress
    {
        get
        {
            var duration = Duration;
            if (duration <= TimeSpan.Zero)
            {
                return 0;
            }

            return Math.Clamp(Position.TotalSeconds / duration.TotalSeconds, 0d, 1d);
        }
    }

    /// <summary>
    /// <c>-L</c> 連続区間と、直後の連続 <c>-E</c>（あれば）を再生プランにする。
    /// </summary>
    public static LoopPlaybackPlan[] BuildLoopPlans(IReadOnlyList<WaveformRegionMark> regions)
    {
        if (regions.Count == 0)
        {
            return [];
        }

        var plans = new List<LoopPlaybackPlan>();
        long? runStart = null;
        long runEnd = 0;
        var runEndIndex = -1;

        void FlushLoopRun()
        {
            if (runStart is not long start || runEnd <= start || runEndIndex < 0)
            {
                runStart = null;
                runEndIndex = -1;
                return;
            }

            long? exitEnd = null;
            var expectedStart = runEnd;
            for (var j = runEndIndex + 1; j < regions.Count; j++)
            {
                var region = regions[j];
                if (region.IsExcluded
                    || !region.NameSuffix.Equals(
                        WaveformRegionBuilder.LoopEndSuffix,
                        StringComparison.OrdinalIgnoreCase)
                    || region.StartSampleOffset != expectedStart)
                {
                    break;
                }

                exitEnd = region.EndSampleOffset;
                expectedStart = region.EndSampleOffset;
            }

            plans.Add(new LoopPlaybackPlan(start, runEnd, exitEnd));
            runStart = null;
            runEndIndex = -1;
        }

        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            var isLoop = !region.IsExcluded
                && region.NameSuffix.Equals(
                    WaveformRegionBuilder.LoopLeftSuffix,
                    StringComparison.OrdinalIgnoreCase);
            if (isLoop)
            {
                if (runStart is null)
                {
                    runStart = region.StartSampleOffset;
                }

                runEnd = region.EndSampleOffset;
                runEndIndex = i;
                continue;
            }

            FlushLoopRun();
        }

        FlushLoopRun();
        return plans.ToArray();
    }

    public void SetLoopPlans(IReadOnlyList<LoopPlaybackPlan> plans)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _loopPlans = plans.Count == 0 ? [] : plans.ToArray();
        _activePlan = null;
        PushActivePlanToProvider();
    }

    /// <summary>
    /// リージョン端フェード（プレビュー用）。Playlist 遷移フェードと乗算で重ねがけする。
    /// </summary>
    public void SetRegionEdgeFades(IReadOnlyList<RegionEdgeFade>? fades)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _provider?.SetRegionEdgeFades(fades);
    }

    /// <summary>
    /// プレビュー再生で無音にする除外（-R）区間を登録する。
    /// </summary>
    public void SetExcludedRegions(IReadOnlyList<WaveformRegionMark>? regions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _provider?.SetExcludedRegions(regions);
    }

    /// <summary>
    /// ループ折り返し時の -E 二重再生を行うか。
    /// false にすると進行中の Exit／上乗せ Exit も直ちに止める。
    /// </summary>
    public bool PlayExitLayer
    {
        get => _playExitLayer;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _playExitLayer = value;
            _provider?.SetPlayExitLayer(value);
        }
    }

    /// <summary>
    /// 現在位置がループ区間内ならその区間だけを有効化。外ならループ／Exit 解除。
    /// シークで別位置へ飛んだときは必ず呼び、区間外なら二重再生の Exit も直ちに止める。
    /// </summary>
    public void ArmLoopAtProgress(double progress)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _activePlan = FindPlanAtProgress(progress);
        if (_activePlan is { } plan)
        {
            // アーム時点では Exit は始めない。ループ末端→頭の折り返しで開始する。
            _provider?.SetActivePlan(plan);
        }
        else
        {
            _provider?.SetActivePlan(null);
            _provider?.StopExitLayer();
        }
    }

    /// <summary>有効中のループ区間（進捗 0〜1）。未アームなら false。</summary>
    public bool TryGetActiveLoopProgress(out double start, out double end)
    {
        start = 0;
        end = 0;
        if (!TryGetActiveLoopSamples(out var startSample, out var endSample))
        {
            return false;
        }

        var frameCount = FrameCount;
        start = startSample / (double)frameCount;
        end = endSample / (double)frameCount;
        return end > start;
    }

    /// <summary>有効中のループ区間（サンプル）。未アームなら false。</summary>
    public bool TryGetActiveLoopSamples(out long startSample, out long endSample)
    {
        startSample = 0;
        endSample = 0;
        // Provider が存在する場合、その null は「現在の Playlist に有効なループなし」を意味する。
        // ?? で _activePlan へ戻すと、Playlist 遷移前の古いループを UI が再利用してしまう。
        var plan = _provider is not null
            ? _provider.GetActivePlan()
            : _activePlan;
        if (_reader is null || plan is not { } activePlan)
        {
            return false;
        }

        if (FrameCount <= 0 || activePlan.LoopEndSample <= activePlan.LoopStartSample)
        {
            return false;
        }

        startSample = activePlan.LoopStartSample;
        endSample = activePlan.LoopEndSample;
        return true;
    }

    /// <summary>
    /// カタログ上、<paramref name="progress"/> が含まれるループ区間があるか（アーム状態は問わない）。
    /// </summary>
    public bool TryGetLoopProgress(double progress, out double start, out double end)
    {
        start = 0;
        end = 0;
        if (!TryGetLoopSamples(progress, out var startSample, out var endSample))
        {
            return false;
        }

        var frameCount = FrameCount;
        start = startSample / (double)frameCount;
        end = endSample / (double)frameCount;
        return end > start;
    }

    /// <summary>
    /// カタログ上、<paramref name="progress"/> が含まれるループ区間のサンプル範囲。
    /// </summary>
    public bool TryGetLoopSamples(double progress, out long startSample, out long endSample)
    {
        startSample = 0;
        endSample = 0;
        if (FindPlanAtProgress(progress) is not { } plan || FrameCount <= 0)
        {
            return false;
        }

        if (plan.LoopEndSample <= plan.LoopStartSample)
        {
            return false;
        }

        startSample = plan.LoopStartSample;
        endSample = plan.LoopEndSample;
        return true;
    }

    /// <summary>
    /// Exit 二重再生ヘッドの位置（0〜1）。再生していなければ false。
    /// </summary>
    public bool TryGetExitPlaybackProgress(out double progress)
    {
        progress = 0;
        if (_provider is null || _reader is null)
        {
            return false;
        }

        var frameCount = FrameCount;
        var sampleRate = _reader.WaveFormat.SampleRate;
        return _provider.TryGetExitPlaybackProgress(frameCount, sampleRate, out progress);
    }

    public bool TryGetPlaylistFadePlaybackProgress(
        out double progress,
        out bool isExit)
    {
        progress = 0d;
        isExit = false;
        if (_provider is null || _reader is null)
        {
            return false;
        }

        return _provider.TryGetPlaylistFadePlaybackProgress(
            FrameCount,
            _reader.WaveFormat.SampleRate,
            out progress,
            out isExit);
    }

    private long FrameCount =>
        _reader is null
            ? 0
            : _reader.Length / Math.Max(1, _reader.WaveFormat.BlockAlign);

    public long CurrentMainSample => _provider?.CurrentMainSample ?? 0L;

    private LoopPlaybackPlan? FindPlanAtProgress(double progress)
    {
        if (_reader is null || _loopPlans.Length == 0)
        {
            return null;
        }

        var frameCount = FrameCount;
        if (frameCount <= 0)
        {
            return null;
        }

        var sample = (long)Math.Clamp(Math.Floor(Math.Clamp(progress, 0d, 1d) * frameCount), 0, frameCount - 1);
        foreach (var plan in _loopPlans)
        {
            if (sample >= plan.LoopStartSample && sample < plan.LoopEndSample)
            {
                return plan;
            }
        }

        return null;
    }

    private LoopPlaybackPlan? FindPlanAtSample(long sample)
    {
        foreach (var plan in _loopPlans)
        {
            if (sample >= plan.LoopStartSample && sample < plan.LoopEndSample)
            {
                return plan;
            }
        }

        return null;
    }

    private void PushActivePlanToProvider()
    {
        if (_provider is null)
        {
            return;
        }

        if (_activePlan is { } plan)
        {
            _provider.SetActivePlan(plan);
        }
        else
        {
            _provider.SetActivePlan(null);
            _provider.StopExitLayer();
        }
    }

    /// <summary>
    /// 停止／一時停止中に Playlist 範囲の先頭へ移動して再生を開始する。
    /// 範囲は開始を含み、終了を含まないソース WAV のサンプル位置。
    /// </summary>
    public bool StartPlaylistRange(long startSample, long endSample, int clockVoiceId = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isPlaying
            || _provider is null
            || _output is null
            || !IsValidPlaylistRange(startSample, endSample))
        {
            Trace($"playlist.start rejected playing={_isPlaying} provider={_provider is not null} output={_output is not null} start={startSample} end={endSample} frames={FrameCount}");
            return false;
        }

        // Pause／Stop 後はデバイス側の先読みを捨てるため、出力を作り直してから再生する。
        _provider.ClearPlaylistPlayback();
        RecreateOutputDevice();
        if (_output is null || _provider is null)
        {
            Trace($"playlist.start rejected after recreate provider={_provider is not null} output={_output is not null}");
            return false;
        }

        var plan = FindPlanAtSample(startSample);
        _provider.StartPlaylistRange(startSample, endSample, plan, clockVoiceId);
        _activePlan = plan;
        _discardOutputBufferBeforePlay = false;
        _output.Play();
        _isPlaying = true;
        Trace($"playlist.start accepted start={startSample} end={endSample} voice={clockVoiceId} loopPlan={plan?.ToString() ?? "none"}");
        return true;
    }

    /// <summary>
    /// 退出境界の手前へアウフタクトを重ね、境界でメインを切り替えて
    /// 旧 Playlist のフェードを始める。退出境界が null なら即時遷移。
    /// </summary>
    public bool TrySchedulePlaylistTransition(
        long startSample,
        long endSample,
        long? sourceExitSample,
        long sourcePartStartSample,
        PlaylistDestinationSyncMode destinationSyncMode,
        long preRollFrameCount,
        bool allowShortPreRoll,
        long fadeSourceEndSample,
        double fadeInSeconds,
        double fadeSeconds,
        out PlaylistTransitionSchedule schedule)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        schedule = default;
        if (!_isPlaying
            || _provider is null
            || sourceExitSample is < 0
            || sourceExitSample > FrameCount
            || sourcePartStartSample < 0
            || sourcePartStartSample > FrameCount
            || preRollFrameCount < 0
            || fadeSourceEndSample > FrameCount
            || !double.IsFinite(fadeInSeconds)
            || fadeInSeconds < 0d
            || !double.IsFinite(fadeSeconds)
            || fadeSeconds < 0d
            || !IsValidPlaylistRange(startSample, endSample))
        {
            Trace($"playlist.schedule rejected playing={_isPlaying} start={startSample} end={endSample} sourceExit={sourceExitSample?.ToString() ?? "immediate"} sourcePartStart={sourcePartStartSample} destinationSync={destinationSyncMode} preRoll={preRollFrameCount} allowShortPreRoll={allowShortPreRoll} fadeEnd={fadeSourceEndSample} fadeInSeconds={fadeInSeconds:R} fadeOutSeconds={fadeSeconds:R} frames={FrameCount}");
            return false;
        }

        var fadeInFrameCount = SecondsToFadeFrames(fadeInSeconds);
        var fadeFrameCount = SecondsToFadeFrames(fadeSeconds);
        var accepted = _provider.TrySchedulePlaylistTransition(
            startSample,
            endSample,
            sourceExitSample,
            sourcePartStartSample,
            destinationSyncMode,
            preRollFrameCount,
            allowShortPreRoll,
            fadeSourceEndSample,
            fadeInFrameCount,
            fadeFrameCount,
            FindPlanAtSample,
            out schedule);
        Trace($"playlist.schedule accepted={accepted} generation={schedule.Generation} start={startSample} end={endSample} destinationSync={destinationSyncMode} sourceRelative={schedule.SourceRelativeSample} trigger={schedule.TriggerSample} sync={schedule.SyncBoundarySample} targetSwitch={schedule.TargetSwitchSample} rejection={schedule.RejectionReason ?? "none"} startedImmediately={schedule.StartedImmediately} fadeEnd={fadeSourceEndSample} fadeInSeconds={fadeInSeconds:R} fadeInFrames={fadeInFrameCount} fadeOutSeconds={fadeSeconds:R} fadeOutFrames={fadeFrameCount}");
        return accepted;
    }

    /// <summary>未開始の予約と進行中の旧 Playlist フェードを解除する。</summary>
    public void CancelPlaylistTransition()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _provider?.CancelPlaylistTransition();
        Trace("playlist.cancel-transition");
    }

    /// <summary>
    /// 重ね再生を維持したまま、クロックと全上乗せを同一相対オフセットへシークする。
    /// <see cref="ClearPlaylistPlayback"/> は呼ばない（上乗せボイスを消さない）。
    /// </summary>
    /// <param name="relativeSample">各 Playlist 開始からの相対サンプル位置。</param>
    /// <param name="clockProgress">シーク後のクロック絶対進捗（0〜1）。</param>
    public bool TrySeekPlaylistLayersToRelative(long relativeSample, out double clockProgress)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        clockProgress = 0d;
        if (_provider is null || _reader is null || FrameCount <= 0)
        {
            return false;
        }

        if (!_provider.TrySeekPlaylistLayersToRelative(
                relativeSample,
                FindPlanAtSample,
                out var clockSample))
        {
            Trace($"playlist.layer-seek-relative rejected relative={relativeSample}");
            return false;
        }

        clockProgress = Math.Clamp(clockSample / (double)FrameCount, 0d, 1d);
        if (!_isPlaying)
        {
            _discardOutputBufferBeforePlay = true;
        }

        Trace(
            $"playlist.layer-seek-relative relative={relativeSample}"
            + $" clockSample={clockSample} clockProgress={clockProgress:R}");
        return true;
    }

    /// <summary>Form1 のポーリング用 Playlist 遷移状態。</summary>
    public bool TryGetPlaylistTransitionState(out PlaylistTransitionState state)
    {
        state = default;
        return _provider?.TryGetPlaylistTransitionState(out state) == true;
    }

    /// <summary>
    /// 再生中の位置を変えずに、現在パートをクロック Playlist 範囲として採用する。
    /// Space 再生などから Alt+上乗せへ入るときに使う。
    /// </summary>
    public bool TryAdoptClockPlaylistRange(
        long startSample,
        long endSample,
        int clockVoiceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isPlaying
            || _provider is null
            || !IsValidPlaylistRange(startSample, endSample))
        {
            Trace(
                $"playlist.adopt-clock rejected playing={_isPlaying}"
                + $" start={startSample} end={endSample} voice={clockVoiceId}");
            return false;
        }

        var plan = FindPlanAtSample(CurrentMainSample);
        var accepted = _provider.TryAdoptClockPlaylistRange(
            startSample,
            endSample,
            clockVoiceId,
            plan);
        if (accepted)
        {
            _activePlan = plan;
        }

        Trace(
            $"playlist.adopt-clock accepted={accepted} start={startSample} end={endSample}"
            + $" voice={clockVoiceId} sample={CurrentMainSample}");
        return accepted;
    }

    /// <summary>
    /// グループ内上乗せボイスを Same Time 相対で追加する。
    /// 既に同一 voiceId があれば false。合計 <see cref="MaxPlaylistVoices"/> 本まで。
    /// </summary>
    public bool TryAddOverlayPlaylistVoice(
        int voiceId,
        long startSample,
        long endSample,
        double fadeInSeconds,
        out string? rejectReason)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        rejectReason = null;
        if (!_isPlaying || _provider is null || !IsValidPlaylistRange(startSample, endSample))
        {
            rejectReason = "not-playing-or-invalid-range";
            return false;
        }

        var fadeInFrameCount = SecondsToFadeFrames(fadeInSeconds);
        var accepted = _provider.TryAddOverlayPlaylistVoice(
            voiceId,
            startSample,
            endSample,
            fadeInFrameCount,
            FindPlanAtSample,
            out rejectReason);
        Trace(
            $"playlist.overlay-add accepted={accepted} voice={voiceId}"
            + $" start={startSample} end={endSample}"
            + $" fadeInSeconds={fadeInSeconds:R}"
            + $" reason={rejectReason ?? "ok"}");
        return accepted;
    }

    /// <summary>上乗せボイスを Group Fade Out で停止する。最後の1本なら再生終了扱い。</summary>
    public bool TryFadeOutOverlayPlaylistVoice(int voiceId, double fadeOutSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_provider is null)
        {
            return false;
        }

        var fadeOutFrameCount = SecondsToFadeFrames(fadeOutSeconds);
        var accepted = _provider.TryFadeOutOverlayPlaylistVoice(voiceId, fadeOutFrameCount);
        Trace(
            $"playlist.overlay-fade-out accepted={accepted} voice={voiceId}"
            + $" fadeOutSeconds={fadeOutSeconds:R}");
        return accepted;
    }

    /// <summary>クロック側 Playlist を Group Fade Out で止め、上乗せがあれば先頭を昇格する。</summary>
    public bool TryFadeOutClockPlaylistVoice(
        double fadeOutSeconds,
        out int? promotedVoiceId,
        out bool playbackWillEnd)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        promotedVoiceId = null;
        playbackWillEnd = false;
        if (_provider is null)
        {
            return false;
        }

        var fadeOutFrameCount = SecondsToFadeFrames(fadeOutSeconds);
        var accepted = _provider.TryFadeOutClockPlaylistVoice(
            fadeOutFrameCount,
            FindPlanAtSample,
            out promotedVoiceId,
            out playbackWillEnd);
        Trace(
            $"playlist.clock-fade-out accepted={accepted}"
            + $" promoted={promotedVoiceId?.ToString() ?? "none"}"
            + $" willEnd={playbackWillEnd} fadeOutSeconds={fadeOutSeconds:R}");
        return accepted;
    }

    /// <summary>上乗せボイスをすべて Group Fade Out する（遷移前の一括停止用）。</summary>
    public void FadeOutAllOverlayPlaylistVoices(double fadeOutSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_provider is null)
        {
            return;
        }

        var fadeOutFrameCount = SecondsToFadeFrames(fadeOutSeconds);
        _provider.FadeOutAllOverlayPlaylistVoices(fadeOutFrameCount);
        Trace($"playlist.overlay-fade-out-all fadeOutSeconds={fadeOutSeconds:R}");
    }

    /// <summary>上乗せを即時クリアする。</summary>
    public void ClearOverlayPlaylistVoices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _provider?.ClearOverlayPlaylistVoices();
    }

    public bool HasOverlayPlaylistVoice(int voiceId) =>
        _provider?.HasOverlayPlaylistVoice(voiceId) == true;

    public int ActiveOverlayPlaylistVoiceCount =>
        _provider?.ActiveOverlayPlaylistVoiceCount ?? 0;

    public int TotalActivePlaylistVoiceCount =>
        (_provider?.HasClockPlaylistRange == true ? 1 : 0)
        + ActiveOverlayPlaylistVoiceCount;

    public bool HasClockPlaylistRange =>
        _provider?.HasClockPlaylistRange == true;

    /// <summary>
    /// 上乗せボイスの絶対進捗（0〜1）を destination へ書き込む。
    /// Fade Out 中は含めない（白いフェードバー側へ分ける）。戻り値は本数。
    /// </summary>
    public int CopyOverlayPlaylistVoiceProgresses(double[] destination) =>
        CopyOverlayPlaylistVoiceProgresses(destination, voiceIds: null);

    /// <summary>
    /// 上乗せボイスの進捗と voiceId を同じ順で書き込む。Fade Out 中は含めない。
    /// </summary>
    public int CopyOverlayPlaylistVoiceProgresses(double[] destination, int[]? voiceIds) =>
        _provider?.CopyOverlayPlaylistVoiceProgresses(destination, voiceIds, FrameCount) ?? 0;

    /// <summary>
    /// Group Fade Out 中の上乗せボイス進捗（0〜1）。白いシークバー用。
    /// </summary>
    public int CopyOverlayFadeOutProgresses(double[] destination) =>
        CopyOverlayFadeOutProgresses(destination, voiceIds: null);

    /// <summary>Group Fade Out 中の上乗せ進捗と voiceId を同じ順で書き込む。</summary>
    public int CopyOverlayFadeOutProgresses(double[] destination, int[]? voiceIds) =>
        _provider?.CopyOverlayFadeOutProgresses(destination, voiceIds, FrameCount) ?? 0;

    /// <summary>上乗せボイスの -E 二重再生進捗（0〜1）を destination へ書き込む。</summary>
    public int CopyOverlayExitProgresses(double[] destination) =>
        CopyOverlayExitProgresses(destination, voiceIds: null);

    /// <summary>上乗せ -E 進捗と voiceId を同じ順で書き込む。</summary>
    public int CopyOverlayExitProgresses(double[] destination, int[]? voiceIds) =>
        _provider?.CopyOverlayExitProgresses(destination, voiceIds, FrameCount) ?? 0;

    /// <summary>最終クロックの Group Fade Out 中の進捗（0〜1）。</summary>
    public bool TryGetClockFadeOutPlaybackProgress(out double progress)
    {
        progress = 0d;
        if (_provider is null || FrameCount <= 0)
        {
            return false;
        }

        return _provider.TryGetClockFadeOutPlaybackProgress(FrameCount, out progress);
    }

    /// <summary>有効な上乗せ voiceId を destination へ書き込む。戻り値は本数。</summary>
    public int CopyActiveOverlayPlaylistVoiceIds(int[] destination) =>
        _provider?.CopyActiveOverlayPlaylistVoiceIds(destination) ?? 0;

    public void SetClockPlaylistVoiceId(int voiceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _provider?.SetClockPlaylistVoiceId(voiceId);
    }

    public int GetClockPlaylistVoiceId() =>
        _provider?.GetClockPlaylistVoiceId() ?? 0;

    private int SecondsToFadeFrames(double seconds)
    {
        if (_provider is null || seconds <= 0d || !double.IsFinite(seconds))
        {
            return 0;
        }

        return Math.Max(
            1,
            (int)Math.Min(
                int.MaxValue,
                Math.Round(_provider.WaveFormat.SampleRate * seconds)));
    }

    private bool IsValidPlaylistRange(long startSample, long endSample) =>
        startSample >= 0 && endSample > startSample && endSample <= FrameCount;

    public void Load(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StopAndRelease();
        try
        {
            _path = path;
            // 元 WAV を掴み続けないよう、再生用に一時コピーを開く。
            // （外部アプリが同じファイルへ上書き保存できるようにする）
            _playbackCopyPath = CreatePlaybackCopy(path);
            OpenReadersFromPlaybackCopy(path);
        }
        catch
        {
            // 半開きのリーダー・一時コピー・HasSource 不整合を残さない。
            StopAndRelease();
            _path = null;
            throw;
        }
    }

    /// <summary>
    /// 複数波形の仮想タイムライン再生用。ソースを一時連結 WAV にして開く（Export 元には使わない）。
    /// </summary>
    public void LoadVirtualConcat(IReadOnlyList<WaveformSourceSpan> spans)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (spans.Count == 0)
        {
            throw new ArgumentException(UiStrings.ErrMultiWaveOnlyNoSpans);
        }

        StopAndRelease();
        try
        {
            _path = spans[0].Path;
            _playbackCopyPath = WavConcatWriter.WriteTempConcat(spans);
            OpenReadersFromPlaybackCopy(_playbackCopyPath);
        }
        catch
        {
            // 半開きのリーダー・一時連結 WAV・HasSource 不整合を残さない。
            StopAndRelease();
            _path = null;
            throw;
        }
    }

    private void OpenReadersFromPlaybackCopy(string formatSourcePath)
    {
        // AudioFileReader は多チャンネル Extensible の float 変換で
        // ACM（acmFormatSuggest）に頼り NoDriver で失敗するため、変換は自前で行う
        var info = WavFileInfo.Read(formatSourcePath);
        _reader = new WaveFileReader(_playbackCopyPath!);
        _exitReader = new WaveFileReader(_playbackCopyPath!);
        _playlistFadeReader = new WaveFileReader(_playbackCopyPath!);
        _playlistExitFadeReader = new WaveFileReader(_playbackCopyPath!);
        _playlistPreRollReader = new WaveFileReader(_playbackCopyPath!);
        for (var i = 0; i < _overlayReaders.Length; i++)
        {
            _overlayReaders[i] = new WaveFileReader(_playbackCopyPath!);
            _overlayExitReaders[i] = new WaveFileReader(_playbackCopyPath!);
        }

        _provider = new StereoFloatWaveProvider(
            _reader,
            _exitReader,
            _playlistFadeReader,
            _playlistExitFadeReader,
            _playlistPreRollReader,
            _overlayReaders!,
            _overlayExitReaders!,
            info,
            message => Trace(message));
        _provider.SetPlayExitLayer(_playExitLayer);
        PushActivePlanToProvider();
        ApplyMetronomeToProvider();
        InitOutputDevice();
    }

    /// <summary>メトロノームクリック波形（ソース SR）を登録する。再生側で出力 SR へリサンプルする。</summary>
    public void SetMetronomeClicks(IReadOnlyList<float> high, IReadOnlyList<float> low, int sampleRate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sampleRate <= 0 || high.Count == 0 || low.Count == 0)
        {
            _metronomeHigh = [];
            _metronomeLow = [];
            _metronomeClickSampleRate = 0;
        }
        else
        {
            _metronomeHigh = CopyMonoSamples(high);
            _metronomeLow = CopyMonoSamples(low);
            _metronomeClickSampleRate = sampleRate;
        }

        ApplyMetronomeToProvider();
    }

    private static float[] CopyMonoSamples(IReadOnlyList<float> source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        if (source is float[] array)
        {
            return (float[])array.Clone();
        }

        var copy = new float[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            copy[i] = source[i];
        }

        return copy;
    }

    public void SetMetronomeEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _metronomeEnabled = enabled;
        _provider?.SetMetronomeEnabled(enabled);
    }

    public void SetMetronomeVolume(float volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _metronomeVolume = Math.Clamp(volume, MetronomePlayer.MinVolume, MetronomePlayer.MaxVolume);
        _provider?.SetMetronomeVolume(_metronomeVolume);
    }

    public void SetMetronomeBars(IReadOnlyList<WaveformBarMark> bars)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _metronomeBars = bars ?? [];
        _provider?.SetMetronomeBars(_metronomeBars);
    }

    private void ApplyMetronomeToProvider()
    {
        if (_provider is null)
        {
            return;
        }

        if (_metronomeClickSampleRate <= 0
            || _metronomeHigh.Length == 0
            || _metronomeLow.Length == 0)
        {
            _provider.SetMetronomeClicks([], []);
        }
        else
        {
            var targetRate = _provider.WaveFormat.SampleRate;
            var high = MetronomePlayer.ResampleMono(
                _metronomeHigh,
                _metronomeClickSampleRate,
                targetRate);
            var low = MetronomePlayer.ResampleMono(
                _metronomeLow,
                _metronomeClickSampleRate,
                targetRate);
            _provider.SetMetronomeClicks(high, low);
        }

        _provider.SetMetronomeVolume(_metronomeVolume);
        _provider.SetMetronomeBars(_metronomeBars);
        _provider.SetMetronomeEnabled(_metronomeEnabled);
    }

    /// <summary>
    /// 出力 API／デバイスを差し替える。ソース未ロード時は次回 <see cref="Load"/> で反映。
    /// ロード済みなら再生位置を保って出力だけ再初期化する。
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StopAndRelease();
        _path = null;
        _loopPlans = [];
        _activePlan = null;
    }

    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        EnsureOutputDevice();
        if (_output is null || _reader is null)
        {
            return;
        }

        if (_reader.Position >= _reader.Length)
        {
            _reader.Position = 0;
        }

        // ASIO 等は Stop/Play だけではハード／ドライバ先読みが残ることがあるため、
        // 出力デバイスを作り直してから再生する。
        if (_discardOutputBufferBeforePlay)
        {
            RecreateOutputDevice();
            if (_output is null)
            {
                return;
            }
        }

        _discardOutputBufferBeforePlay = false;
        _output.Play();
        _isPlaying = true;
        Trace($"transport.play sample={_provider?.CurrentMainSample ?? 0}");
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_output is null || !_isPlaying)
        {
            return;
        }

        // ASIO の Pause はドライバ停止だけでハードウェア先読みを捨てない。
        // Stop 相当にしてから、次の再生でデバイス再生成する。
        _suppressPlaybackEnded = true;
        try
        {
            _output.Stop();
        }
        finally
        {
            _suppressPlaybackEnded = false;
        }

        _isPlaying = false;
        _discardOutputBufferBeforePlay = true;
        _provider?.ResetOutputPeak();
        Trace($"transport.pause sample={_provider?.CurrentMainSample ?? 0}");
    }

    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_output is null || _reader is null)
        {
            _isPlaying = false;
            return;
        }

        _provider?.ClearPlaylistPlayback();
        _suppressPlaybackEnded = true;
        try
        {
            _output.Stop();
        }
        finally
        {
            _suppressPlaybackEnded = false;
        }

        _reader.Position = 0;
        _provider?.StopExitLayer();
        _isPlaying = false;
        _discardOutputBufferBeforePlay = true;
        _provider?.ResetOutputPeak();
        Trace("transport.stop");
    }

    /// <summary>再生中なら一時停止、停止中なら再生。</summary>
    public void Toggle()
    {
        if (_isPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    /// <summary>位置を 0〜1 でシークする。</summary>
    public void Seek(double progress)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_reader is null)
        {
            return;
        }

        // ジャンプ時は Exit／Playlist 遷移を直ちに止める（Arm 前でも確実に）
        _provider?.ClearPlaylistPlayback();
        _provider?.StopExitLayer();

        var duration = _reader.TotalTime;
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var clamped = Math.Clamp(progress, 0d, 1d);
        // 終端ぴったりだと即 MediaEnded 扱いになることがあるためわずかに手前へ
        var ticks = (long)(duration.Ticks * clamped);
        if (clamped >= 1d && duration.Ticks > 0)
        {
            ticks = Math.Max(0, duration.Ticks - 1);
        }

        _reader.CurrentTime = TimeSpan.FromTicks(ticks);
        // 不連続シーク後は着地拍をサイレントアーム（ジャンプ抑制で１拍目を落とさない）。
        _provider?.ResetMetronomeSchedule();

        // 再生中は連続読み出しで自然に切り替わる。毎回デバイス再作成すると
        // シークバードラッグのスクラブが極端に重くなる。
        // 一時停止中は先読みバッファに旧位置が残るため、次の再生で破棄する。
        if (!_isPlaying)
        {
            _discardOutputBufferBeforePlay = true;
        }

        Trace($"transport.seek progress={clamped:R} sample={_provider?.CurrentMainSample ?? 0}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAndRelease();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_suppressPlaybackEnded || _reader is null)
        {
            return;
        }

        // 末尾到達時のみ終了扱い（Stop 呼び出しでも発火するため位置で判定）
        // ループ中はプロバイダが折り返すので、ここに来るのは真の EOF／Stop
        var playlistEnded = _provider?.TryResetPlaylistAfterEnd() == true;
        // 最終クロックの Group Fade Out 完了で Read が 0 を返した場合、
        // リーダはファイル中盤のままなので位置判定では終了にならない。フラグで回収する。
        var clockFadeOutEnded = _provider?.ConsumeForceEndAfterClockFadeOut() == true;
        if (playlistEnded
            || clockFadeOutEnded
            || _reader.Position >= _reader.Length
            || _reader.CurrentTime >= _reader.TotalTime)
        {
            CompletePlaybackEnded(playlistEnded || clockFadeOutEnded);
        }
    }

    /// <summary>
    /// ASIO（AutoStop=false）の終端を UI スレッドから回収する。
    /// 終了処理を行ったら true。
    /// </summary>
    public bool TryCompletePlaybackIfEnded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isPlaying || _reader is null || _output is null)
        {
            return false;
        }

        // ASIO: コールバック内 Stop を避け、HasReachedEnd を UI から回収する
        if (_output is AsioOut { HasReachedEnd: true })
        {
            var playlistEnded = _provider?.TryResetPlaylistAfterEnd() == true;
            // クロック FO 由来の終了フラグを消費し、次の Play が即終了しないようにする。
            var clockFadeOutEnded = _provider?.ConsumeForceEndAfterClockFadeOut() == true;
            _suppressPlaybackEnded = true;
            try
            {
                _output.Stop();
            }
            finally
            {
                _suppressPlaybackEnded = false;
            }

            CompletePlaybackEnded(playlistEnded || clockFadeOutEnded);
            return true;
        }

        return false;
    }

    private void CompletePlaybackEnded(bool playlistEnded)
    {
        _provider?.StopExitLayer();
        _isPlaying = false;
        _provider?.ResetOutputPeak();
        Trace($"playback.ended playlistEnded={playlistEnded} sample={_provider?.CurrentMainSample ?? 0}");
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void StopAndRelease()
    {
        _isPlaying = false;
        _discardOutputBufferBeforePlay = false;
        _provider?.ClearPlaylistPlayback();
        _provider?.StopExitLayer();
        _provider?.ResetOutputPeak();
        _provider = null;

        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        if (_reader is not null)
        {
            _reader.Dispose();
            _reader = null;
        }

        if (_exitReader is not null)
        {
            _exitReader.Dispose();
            _exitReader = null;
        }

        if (_playlistFadeReader is not null)
        {
            _playlistFadeReader.Dispose();
            _playlistFadeReader = null;
        }

        if (_playlistExitFadeReader is not null)
        {
            _playlistExitFadeReader.Dispose();
            _playlistExitFadeReader = null;
        }

        if (_playlistPreRollReader is not null)
        {
            _playlistPreRollReader.Dispose();
            _playlistPreRollReader = null;
        }

        for (var i = 0; i < _overlayReaders.Length; i++)
        {
            _overlayReaders[i]?.Dispose();
            _overlayReaders[i] = null;
            _overlayExitReaders[i]?.Dispose();
            _overlayExitReaders[i] = null;
        }

        TryDeleteFile(_playbackCopyPath);
        _playbackCopyPath = null;
    }

    private void Trace(string message) => Diagnostic?.Invoke(this, message);

    /// <summary>
    /// PCM / IEEE float の WAV を ACM を使わずステレオ float に変換する再生用プロバイダ。
    /// メインはループ折り返し、Exit は別リーダでワンショット二重再生してミックスする。
    /// グループ重ね再生は最大 <see cref="MaxPlaylistVoices"/> − 1 本の上乗せリーダを加算する。
    /// </summary>
}

internal readonly record struct LoopPlaybackPlan(
    long LoopStartSample,
    long LoopEndSample,
    long? ExitEndSample)
{
    public bool HasExit => ExitEndSample is long end && end > LoopEndSample;
}

/// <summary>音声スレッドが確定した Playlist 遷移タイミング。</summary>
internal readonly record struct PlaylistTransitionSchedule(
    long Generation,
    long TriggerSample,
    long SyncBoundarySample,
    long TargetSwitchSample,
    bool StartedImmediately,
    long SourceRelativeSample,
    string? RejectionReason);

/// <summary>Playlist 遷移のポーリング用スナップショット。</summary>
internal readonly record struct PlaylistTransitionState(
    long TargetStartSample,
    long TargetEndSample,
    long? PendingBoundarySample,
    long RequestGeneration,
    long StartedGeneration,
    bool IsOldPlaylistFading);
