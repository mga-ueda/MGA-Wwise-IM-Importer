namespace MgaWwiseIMImporter.Wave;

/// <summary>リージョン端フェードのカーブ形状（Wwise MusicClip FadeIn/OutShape 相当）。</summary>
internal enum RegionFadeCurveKind
{
    LogarithmicBase3,
    SineConstantPowerFadeIn,
    LogarithmicBase141,
    InvertedSCurve,
    Linear,
    SCurve,
    ExponentialBase141,
    SineConstantPowerFadeOut,
    ExponentialBase3,
}

/// <summary>
/// 連続リージョン固まり（非除外ラン）のイン／アウト端フェード。
/// ソース WAV・EXPORT 分割 WAV とも非破壊。プレビュー表示・再生と MusicClip Fade に適用する。
/// Fade In は固まり先頭 Music Segment 内、Fade Out は末尾 Music Segment 内に制限する
/// （<c>-A</c>／<c>-E</c> は隣接リージョンと同一セグメントのため、その範囲もフェード可能）。
/// </summary>
internal readonly record struct RegionEdgeFade(
    long InSample,
    long OutSample,
    long? FadeInEndSample,
    long? FadeOutStartSample,
    RegionFadeCurveKind FadeInCurve = RegionFadeCurveKind.SCurve,
    RegionFadeCurveKind FadeOutCurve = RegionFadeCurveKind.SCurve)
{
    /// <summary>Wwise CurveIn ドロップダウン順（上→下）。</summary>
    public static IReadOnlyList<RegionFadeCurveKind> MenuOrderFadeIn { get; } =
    [
        RegionFadeCurveKind.LogarithmicBase3,
        RegionFadeCurveKind.SineConstantPowerFadeIn,
        RegionFadeCurveKind.LogarithmicBase141,
        RegionFadeCurveKind.InvertedSCurve,
        RegionFadeCurveKind.Linear,
        RegionFadeCurveKind.SCurve,
        RegionFadeCurveKind.ExponentialBase141,
        RegionFadeCurveKind.SineConstantPowerFadeOut,
        RegionFadeCurveKind.ExponentialBase3,
    ];

    /// <summary>
    /// Wwise CurveOut ドロップダウン順（上→下）。
    /// 山なり（Exp3）→ 谷なり（Log3）。中央の InvS / Linear / S は CurveIn と同じ相対順。
    /// </summary>
    public static IReadOnlyList<RegionFadeCurveKind> MenuOrderFadeOut { get; } =
    [
        RegionFadeCurveKind.ExponentialBase3,
        RegionFadeCurveKind.SineConstantPowerFadeOut,
        RegionFadeCurveKind.ExponentialBase141,
        RegionFadeCurveKind.InvertedSCurve,
        RegionFadeCurveKind.Linear,
        RegionFadeCurveKind.SCurve,
        RegionFadeCurveKind.LogarithmicBase141,
        RegionFadeCurveKind.SineConstantPowerFadeIn,
        RegionFadeCurveKind.LogarithmicBase3,
    ];

    /// <summary>波形リージョン端フェードのアプリ既定（In／Out とも S-Curve）。</summary>
    public static RegionFadeCurveKind BuiltinWaveformFadeInCurve => RegionFadeCurveKind.SCurve;

    /// <summary>波形リージョン端フェードのアプリ既定（In／Out とも S-Curve）。</summary>
    public static RegionFadeCurveKind BuiltinWaveformFadeOutCurve => RegionFadeCurveKind.SCurve;

    /// <summary>Playlist 遷移フェードのアプリ既定（In／Out とも S-Curve）。</summary>
    public static RegionFadeCurveKind BuiltinPlaylistFadeInCurve => RegionFadeCurveKind.SCurve;

    /// <summary>Playlist 遷移フェードのアプリ既定（In／Out とも S-Curve）。</summary>
    public static RegionFadeCurveKind BuiltinPlaylistFadeOutCurve => RegionFadeCurveKind.SCurve;

    public long EffectiveFadeInEnd =>
        FadeInEndSample is { } end && end > InSample ? end : InSample;

    public long EffectiveFadeOutStart =>
        FadeOutStartSample is { } start && start < OutSample ? start : OutSample;

    public bool HasFadeIn => EffectiveFadeInEnd > InSample;

    public bool HasFadeOut => EffectiveFadeOutStart < OutSample;

    public bool HasAnyFade => HasFadeIn || HasFadeOut;

    /// <summary>
    /// 食い込みを解消した正規化済みフェードを返す。
    /// <paramref name="firstSegmentEndSample"/> / <paramref name="lastSegmentStartSample"/> があるとき、
    /// Fade In は先頭 Music Segment 末尾まで、Fade Out は末尾 Music Segment 先頭からに制限する。
    /// </summary>
    public RegionEdgeFade Normalized(
        long? firstSegmentEndSample = null,
        long? lastSegmentStartSample = null)
    {
        if (OutSample <= InSample)
        {
            return new RegionEdgeFade(InSample, InSample, null, null, FadeInCurve, FadeOutCurve);
        }

        var fadeInEnd = EffectiveFadeInEnd;
        var fadeOutStart = EffectiveFadeOutStart;

        if (firstSegmentEndSample is { } firstEnd)
        {
            var clampEnd = Math.Clamp(firstEnd, InSample, OutSample);
            fadeInEnd = Math.Min(fadeInEnd, clampEnd);
        }

        if (lastSegmentStartSample is { } lastStart)
        {
            var clampStart = Math.Clamp(lastStart, InSample, OutSample);
            fadeOutStart = Math.Max(fadeOutStart, clampStart);
        }

        if (fadeInEnd > fadeOutStart)
        {
            var mid = InSample + (OutSample - InSample) / 2;
            if (firstSegmentEndSample is { } fe && lastSegmentStartSample is { } ls
                && fe <= ls)
            {
                // セグメント境界が分かれているときは跨ぎを優先して解消する。
                fadeInEnd = Math.Min(fadeInEnd, Math.Clamp(fe, InSample, OutSample));
                fadeOutStart = Math.Max(fadeOutStart, Math.Clamp(ls, InSample, OutSample));
                if (fadeInEnd > fadeOutStart)
                {
                    fadeInEnd = fadeOutStart;
                }
            }
            else
            {
                fadeInEnd = Math.Min(fadeInEnd, mid);
                fadeOutStart = Math.Max(fadeOutStart, mid);
            }
        }

        fadeInEnd = Math.Clamp(fadeInEnd, InSample, OutSample);
        fadeOutStart = Math.Clamp(fadeOutStart, InSample, OutSample);
        if (fadeInEnd > fadeOutStart)
        {
            fadeInEnd = fadeOutStart;
        }

        return new RegionEdgeFade(
            InSample,
            OutSample,
            fadeInEnd > InSample ? fadeInEnd : null,
            fadeOutStart < OutSample ? fadeOutStart : null,
            FadeInCurve,
            FadeOutCurve);
    }

    public RegionEdgeFade WithCurves(RegionFadeCurveKind fadeInCurve, RegionFadeCurveKind fadeOutCurve) =>
        new(InSample, OutSample, FadeInEndSample, FadeOutStartSample, fadeInCurve, fadeOutCurve);

    /// <summary>Wwise MusicClip FadeInShape / FadeOutShape の列挙値。</summary>
    public static int ToWwiseShape(RegionFadeCurveKind kind) => kind switch
    {
        RegionFadeCurveKind.LogarithmicBase3 => 0,
        RegionFadeCurveKind.SineConstantPowerFadeIn => 1,
        RegionFadeCurveKind.LogarithmicBase141 => 2,
        RegionFadeCurveKind.InvertedSCurve => 3,
        RegionFadeCurveKind.Linear => 4,
        RegionFadeCurveKind.SCurve => 6,
        RegionFadeCurveKind.ExponentialBase141 => 7,
        RegionFadeCurveKind.SineConstantPowerFadeOut => 8,
        RegionFadeCurveKind.ExponentialBase3 => 9,
        _ => 6,
    };

    /// <summary>Wwise MusicFade.FadeCurve の列挙値（Transition Fade Editor）。</summary>
    public static int ToMusicFadeCurve(RegionFadeCurveKind kind) => kind switch
    {
        RegionFadeCurveKind.LogarithmicBase3 => 0,
        RegionFadeCurveKind.SineConstantPowerFadeIn => 1,
        RegionFadeCurveKind.LogarithmicBase141 => 2,
        RegionFadeCurveKind.InvertedSCurve => 3,
        RegionFadeCurveKind.Linear => 4,
        RegionFadeCurveKind.SCurve => 5,
        RegionFadeCurveKind.ExponentialBase141 => 6,
        RegionFadeCurveKind.SineConstantPowerFadeOut => 7,
        RegionFadeCurveKind.ExponentialBase3 => 8,
        _ => 4,
    };

    /// <summary>
    /// カーブ形状に応じたゲイン。範囲外は 1。
    /// </summary>
    public float GainAt(long sample)
    {
        if (sample < InSample || sample >= OutSample)
        {
            return 1f;
        }

        var gain = 1f;
        var fadeInEnd = EffectiveFadeInEnd;
        if (fadeInEnd > InSample && sample < fadeInEnd)
        {
            var t = (sample - InSample) / (double)(fadeInEnd - InSample);
            gain *= EvaluateRising(FadeInCurve, t);
        }

        var fadeOutStart = EffectiveFadeOutStart;
        if (fadeOutStart < OutSample && sample >= fadeOutStart)
        {
            var t = (sample - fadeOutStart) / (double)(OutSample - fadeOutStart);
            // フェードアウト: 立ち上がり式を 1-f(t) で 1→0（Wwise CurveOut アイコンと同じ形）
            gain *= EvaluateFalling(FadeOutCurve, t);
        }

        return gain;
    }

    /// <summary>
    /// t∈[0,1] の立ち下がり（フェードアウト／CurveOut アイコン）。1 - EvaluateRising。
    /// </summary>
    public static float EvaluateFalling(RegionFadeCurveKind kind, double t) =>
        1f - EvaluateRising(kind, t);

    /// <summary>
    /// t∈[0,1] の立ち上がりカーブ（メニューアイコン左下→右上／イン側）。
    /// Wwise Authoring の補間に合わせる（いわゆる Base N は対数ではなく冪 N）。
    /// </summary>
    public static float EvaluateRising(RegionFadeCurveKind kind, double t)
    {
        t = Math.Clamp(t, 0d, 1d);
        return kind switch
        {
            // Logarithmic (Base 3) = 1-(1-t)^3 … 最も急な立ち上がり
            RegionFadeCurveKind.LogarithmicBase3 => (float)(1d - Math.Pow(1d - t, 3d)),
            // Sine (Constant Power Fade In)
            RegionFadeCurveKind.SineConstantPowerFadeIn => SinRising(t),
            // Logarithmic (Base 1.41) = 1-(1-t)^1.41
            RegionFadeCurveKind.LogarithmicBase141 => (float)(1d - Math.Pow(1d - t, 1.41d)),
            RegionFadeCurveKind.InvertedSCurve => InvertedSCurve(t),
            RegionFadeCurveKind.Linear => (float)t,
            RegionFadeCurveKind.SCurve => SCurve(t),
            // Exponential (Base 1.41) = t^1.41
            RegionFadeCurveKind.ExponentialBase141 => (float)Math.Pow(t, 1.41d),
            // Sine (Constant Power Fade Out) = Reciprocal Sine
            RegionFadeCurveKind.SineConstantPowerFadeOut => (float)(1d - Math.Cos(t * (Math.PI * 0.5))),
            // Exponential (Base 3) = t^3 … 最も遅い立ち上がり
            RegionFadeCurveKind.ExponentialBase3 => (float)Math.Pow(t, 3d),
            _ => SinRising(t),
        };
    }

    /// <summary>Constant Power Fade In（凸の弧）。</summary>
    private static float SinRising(double t) =>
        (float)Math.Sin(t * (Math.PI * 0.5));

    /// <summary>
    /// S-Curve: Hermite smoothstep（端で傾き 0＝水平に出入りする典型的な S）。
    /// </summary>
    private static float SCurve(double t) =>
        (float)(t * t * (3d - 2d * t));

    /// <summary>
    /// Inverted S-Curve: 両端が急・中央がゆるい（smoothstep とは逆の曲率）。
    /// </summary>
    private static float InvertedSCurve(double t) =>
        (float)(t * (2d - 3d * t + 2d * t * t));

    /// <summary>複数フェードがあるとき、sample が属する最初の固まりのゲイン。</summary>
    public static float GainAt(long sample, IReadOnlyList<RegionEdgeFade> fades)
    {
        foreach (var fade in fades)
        {
            if (sample >= fade.InSample && sample < fade.OutSample)
            {
                return fade.GainAt(sample);
            }
        }

        return 1f;
    }

    /// <summary>
    /// 除外で区切られた連続リージョン固まり。
    /// Fade In 上限＝先頭 Music Segment 終端、Fade Out 下限＝末尾 Music Segment 始端。
    /// Music Segment は <c>-A</c>／<c>-E</c> を隣接リージョンと束ねた単位（独立リージョンではない）。
    /// </summary>
    public readonly record struct RunSegmentLimits(
        long InSample,
        long OutSample,
        long FirstSegmentEndSample,
        long LastSegmentStartSample);

    /// <summary>
    /// 固まり境界と、先頭／末尾 Music Segment 境界を返す。
    /// セグメント束ねは Wwise 取り込みと同じ（<c>-A</c> は次と、<c>-E</c> は直前と同グループ）。
    /// </summary>
    public static IReadOnlyList<RunSegmentLimits> CollectRunSegmentLimits(
        IReadOnlyList<WaveformRegionMark> regions)
    {
        var runs = new List<RunSegmentLimits>();
        List<WaveformRegionMark>? current = null;
        foreach (var region in regions)
        {
            if (region.IsExcluded)
            {
                if (current is { Count: > 0 })
                {
                    runs.Add(BuildRunSegmentLimits(current));
                }

                current = null;
                continue;
            }

            current ??= [];
            current.Add(region);
        }

        if (current is { Count: > 0 })
        {
            runs.Add(BuildRunSegmentLimits(current));
        }

        return runs;
    }

    private static RunSegmentLimits BuildRunSegmentLimits(IReadOnlyList<WaveformRegionMark> runRegions)
    {
        var inSample = runRegions[0].StartSampleOffset;
        var outSample = runRegions[^1].EndSampleOffset;
        var segments = GroupRegionsIntoMusicSegments(runRegions);
        var first = segments[0];
        var last = segments[^1];
        return new RunSegmentLimits(
            inSample,
            outSample,
            first[^1].EndSampleOffset,
            last[0].StartSampleOffset);
    }

    /// <summary>
    /// リージョンを Music Segment 単位に束ねる。<c>-A</c> は次と、続く <c>-E</c> は同グループへ。
    /// Wwise 取り込み時のセグメント分割と同じ規則。
    /// </summary>
    public static List<List<WaveformRegionMark>> GroupRegionsIntoMusicSegments(
        IReadOnlyList<WaveformRegionMark> regions)
    {
        var groups = new List<List<WaveformRegionMark>>();
        var i = 0;
        while (i < regions.Count)
        {
            var group = new List<WaveformRegionMark> { regions[i] };

            if (IsAnacrusisRegion(regions[i]) && i + 1 < regions.Count)
            {
                i++;
                group.Add(regions[i]);
            }

            if (i + 1 < regions.Count && IsExitTailRegion(regions[i + 1]))
            {
                i++;
                group.Add(regions[i]);
            }

            groups.Add(group);
            i++;
        }

        return groups;
    }

    private static bool IsAnacrusisRegion(WaveformRegionMark region) =>
        region.NameSuffix.Equals(
            WaveformRegionBuilder.AnacrusisSuffix,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsExitTailRegion(WaveformRegionMark region) =>
        region.NameSuffix.Equals(
            WaveformRegionBuilder.LoopEndSuffix,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 指定固まりの先頭／末尾セグメント境界を探す。見つからなければ false。
    /// </summary>
    public static bool TryGetRunSegmentLimits(
        IReadOnlyList<WaveformRegionMark> regions,
        long inSample,
        long outSample,
        out RunSegmentLimits limits)
    {
        foreach (var run in CollectRunSegmentLimits(regions))
        {
            if (run.InSample == inSample && run.OutSample == outSample)
            {
                limits = run;
                return true;
            }
        }

        limits = default;
        return false;
    }

    /// <summary>
    /// 現在の固まり境界に合わせてフェードを再マップする。
    /// 完全一致を優先し、一致しない場合は重なる固まりへ引き継ぐ
    /// （例: <c>-E</c> 内に <c>-R</c> を打って末尾が縮んでも先頭 Fade In を残す。
    /// 固まりが分割されたときは Fade In／Out を先頭／末尾ランへ振り分ける）。
    /// </summary>
    public static IReadOnlyList<RegionEdgeFade> RemapToRuns(
        IReadOnlyList<RegionEdgeFade> existing,
        IReadOnlyList<WaveformRegionMark> regions)
    {
        if (existing.Count == 0)
        {
            return [];
        }

        var runs = CollectRunSegmentLimits(regions);
        if (runs.Count == 0)
        {
            return [];
        }

        var runByBounds = runs.ToDictionary(r => (r.InSample, r.OutSample));
        var kept = new List<RegionEdgeFade>();
        var claimedBounds = new HashSet<(long In, long Out)>();

        foreach (var fade in existing)
        {
            if (runByBounds.TryGetValue((fade.InSample, fade.OutSample), out var exact))
            {
                TryAddRemapped(kept, claimedBounds, fade, exact);
                continue;
            }

            var overlapping = runs
                .Where(r => r.InSample < fade.OutSample && fade.InSample < r.OutSample)
                .OrderBy(r => r.InSample)
                .ToList();
            if (overlapping.Count == 0)
            {
                continue;
            }

            var fadeInRun = FindFadeInRun(overlapping, fade.InSample);
            var fadeOutRun = FindFadeOutRun(overlapping, fade.OutSample);

            if (fadeInRun.InSample == fadeOutRun.InSample
                && fadeInRun.OutSample == fadeOutRun.OutSample)
            {
                TryAddRemapped(kept, claimedBounds, fade, fadeInRun);
                continue;
            }

            // -R などで固まりが割れた: Fade In は先頭側、Fade Out は末尾側へ。
            if (fade.HasFadeIn)
            {
                var inOnly = new RegionEdgeFade(
                    fade.InSample,
                    fade.OutSample,
                    fade.FadeInEndSample,
                    null,
                    fade.FadeInCurve,
                    fade.FadeOutCurve);
                TryAddRemapped(kept, claimedBounds, inOnly, fadeInRun);
            }

            if (fade.HasFadeOut)
            {
                var outOnly = new RegionEdgeFade(
                    fade.InSample,
                    fade.OutSample,
                    null,
                    fade.FadeOutStartSample,
                    fade.FadeInCurve,
                    fade.FadeOutCurve);
                TryAddRemapped(kept, claimedBounds, outOnly, fadeOutRun);
            }
        }

        return kept;
    }

    private static RunSegmentLimits FindFadeInRun(
        IReadOnlyList<RunSegmentLimits> overlapping,
        long fadeInSample)
    {
        foreach (var run in overlapping)
        {
            if (run.InSample == fadeInSample)
            {
                return run;
            }
        }

        foreach (var run in overlapping)
        {
            if (run.InSample <= fadeInSample && fadeInSample < run.OutSample)
            {
                return run;
            }
        }

        return overlapping[0];
    }

    private static RunSegmentLimits FindFadeOutRun(
        IReadOnlyList<RunSegmentLimits> overlapping,
        long fadeOutSample)
    {
        for (var i = overlapping.Count - 1; i >= 0; i--)
        {
            if (overlapping[i].OutSample == fadeOutSample)
            {
                return overlapping[i];
            }
        }

        for (var i = overlapping.Count - 1; i >= 0; i--)
        {
            var run = overlapping[i];
            if (run.InSample < fadeOutSample && fadeOutSample <= run.OutSample)
            {
                return run;
            }
        }

        return overlapping[^1];
    }

    private static void TryAddRemapped(
        List<RegionEdgeFade> kept,
        HashSet<(long In, long Out)> claimedBounds,
        RegionEdgeFade source,
        RunSegmentLimits limits)
    {
        var key = (limits.InSample, limits.OutSample);
        if (claimedBounds.Contains(key))
        {
            return;
        }

        var remapped = new RegionEdgeFade(
            limits.InSample,
            limits.OutSample,
            source.FadeInEndSample,
            source.FadeOutStartSample,
            source.FadeInCurve,
            source.FadeOutCurve).Normalized(
            limits.FirstSegmentEndSample,
            limits.LastSegmentStartSample);
        if (!remapped.HasAnyFade)
        {
            return;
        }

        claimedBounds.Add(key);
        kept.Add(remapped);
    }

    public static RegionEdgeFade WithFadeInEnd(
        long inSample,
        long outSample,
        long fadeInEnd,
        long? fadeOutStart,
        RegionFadeCurveKind fadeInCurve = RegionFadeCurveKind.SCurve,
        RegionFadeCurveKind fadeOutCurve = RegionFadeCurveKind.SCurve,
        long? firstSegmentEndSample = null,
        long? lastSegmentStartSample = null)
    {
        return new RegionEdgeFade(
            inSample,
            outSample,
            fadeInEnd,
            fadeOutStart,
            fadeInCurve,
            fadeOutCurve).Normalized(firstSegmentEndSample, lastSegmentStartSample);
    }

    public static RegionEdgeFade WithFadeOutStart(
        long inSample,
        long outSample,
        long? fadeInEnd,
        long fadeOutStart,
        RegionFadeCurveKind fadeInCurve = RegionFadeCurveKind.SCurve,
        RegionFadeCurveKind fadeOutCurve = RegionFadeCurveKind.SCurve,
        long? firstSegmentEndSample = null,
        long? lastSegmentStartSample = null)
    {
        return new RegionEdgeFade(
            inSample,
            outSample,
            fadeInEnd,
            fadeOutStart,
            fadeInCurve,
            fadeOutCurve).Normalized(firstSegmentEndSample, lastSegmentStartSample);
    }
}

/// <summary>リージョン端フェードの Undo / Redo スナップショット。</summary>
internal sealed class RegionEdgeFadeHistory
{
    private readonly Stack<IReadOnlyList<RegionEdgeFade>> _undo = new();
    private readonly Stack<IReadOnlyList<RegionEdgeFade>> _redo = new();

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void PushBeforeChange(IReadOnlyList<RegionEdgeFade> before)
    {
        _undo.Push(Clone(before));
        _redo.Clear();
    }

    public bool TryUndo(
        IReadOnlyList<RegionEdgeFade> current,
        out IReadOnlyList<RegionEdgeFade> restored)
    {
        if (_undo.Count == 0)
        {
            restored = [];
            return false;
        }

        _redo.Push(Clone(current));
        restored = _undo.Pop();
        return true;
    }

    public bool TryRedo(
        IReadOnlyList<RegionEdgeFade> current,
        out IReadOnlyList<RegionEdgeFade> restored)
    {
        if (_redo.Count == 0)
        {
            restored = [];
            return false;
        }

        _undo.Push(Clone(current));
        restored = _redo.Pop();
        return true;
    }

    private static IReadOnlyList<RegionEdgeFade> Clone(IReadOnlyList<RegionEdgeFade> source) =>
        source.ToArray();
}
