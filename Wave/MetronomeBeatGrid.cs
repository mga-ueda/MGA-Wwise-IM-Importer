namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// 小節内の途中テンポを踏まえて、音楽的な拍境界をサンプル位置へ展開する。
/// 小節全体の均等割りだと大きなテンポ変更後に拍（とくに１拍目）がずれる／欠けるため。
/// </summary>
internal static class MetronomeBeatGrid
{
    /// <summary>
    /// <paramref name="positionSample"/> 時点の小節・拍・テンポを解決する。
    /// </summary>
    public static bool TryResolve(
        IReadOnlyList<WaveformBarMark> bars,
        long positionSample,
        long frameCount,
        int sampleRate,
        out int barNumber,
        out int beat,
        out double bpm,
        out int numerator,
        out int denominator,
        out long barStartSample,
        out long barEndSample,
        out long beatStartSample,
        out long nextBeatSample)
    {
        barNumber = 0;
        beat = 1;
        bpm = 0d;
        numerator = 0;
        denominator = 0;
        barStartSample = 0;
        barEndSample = 0;
        beatStartSample = 0;
        nextBeatSample = 0;

        if (frameCount <= 0 || bars.Count == 0 || sampleRate <= 0)
        {
            return false;
        }

        positionSample = Math.Clamp(positionSample, 0L, frameCount - 1);

        if (!TryFindBarContext(
                bars,
                positionSample,
                frameCount,
                sampleRate,
                out var bar,
                out var state,
                out barStartSample,
                out barEndSample))
        {
            return false;
        }

        numerator = Math.Max(1, state.Numerator);
        denominator = state.Denominator > 0 ? state.Denominator : bar.Denominator;
        if (denominator <= 0 || state.Bpm <= 0d && bar.Bpm <= 0d)
        {
            return false;
        }

        var beatStarts = BuildBeatStarts(
            bars,
            barStartSample,
            barEndSample,
            bar.Bpm > 0d ? bar.Bpm : state.Bpm,
            numerator,
            denominator > 0 ? denominator : 4,
            sampleRate);

        var beatZeroBased = 0;
        for (var i = beatStarts.Length - 1; i >= 0; i--)
        {
            if (positionSample >= beatStarts[i])
            {
                beatZeroBased = i;
                break;
            }
        }

        barNumber = Math.Max(0, bar.BarNumber);
        beat = beatZeroBased + 1;
        bpm = state.Bpm > 0d ? state.Bpm : bar.Bpm;
        denominator = denominator > 0 ? denominator : 4;
        beatStartSample = beatStarts[beatZeroBased];
        nextBeatSample = beatZeroBased + 1 < beatStarts.Length
            ? beatStarts[beatZeroBased + 1]
            : barEndSample;
        return bpm > 0d && denominator > 0;
    }

    /// <summary>
    /// キャッシュ再利用用に、指定小節の拍開始サンプル列を構築する。
    /// </summary>
    public static long[] BuildBeatStarts(
        IReadOnlyList<WaveformBarMark> bars,
        long barStartSample,
        long barEndSample,
        double startBpm,
        int numerator,
        int denominator,
        int sampleRate)
    {
        numerator = Math.Max(1, numerator);
        denominator = Math.Max(1, denominator);
        var beatStarts = new long[numerator];
        if (sampleRate <= 0 || barEndSample <= barStartSample)
        {
            for (var i = 0; i < numerator; i++)
            {
                beatStarts[i] = barStartSample;
            }

            return beatStarts;
        }

        var tempoChanges = CollectTempoChangesInBar(bars, barStartSample, barEndSample);
        var cursor = barStartSample;
        var bpm = startBpm > 0d ? startBpm : 120d;
        var tempoIdx = 0;

        // 小節頭より前に食い込んだテンポ変更は開始 BPM に織り込み済みとみなす。
        while (tempoIdx < tempoChanges.Count && tempoChanges[tempoIdx].Sample <= barStartSample)
        {
            var markedBpm = tempoChanges[tempoIdx].Bpm;
            if (markedBpm > 0d)
            {
                bpm = markedBpm;
            }

            tempoIdx++;
        }

        for (var beat = 0; beat < numerator; beat++)
        {
            beatStarts[beat] = Math.Clamp(cursor, barStartSample, Math.Max(barStartSample, barEndSample - 1));
            if (beat == numerator - 1)
            {
                break;
            }

            cursor = AdvanceOneBeat(
                cursor,
                barEndSample,
                ref bpm,
                denominator,
                sampleRate,
                tempoChanges,
                ref tempoIdx);
            if (cursor <= beatStarts[beat])
            {
                cursor = Math.Min(barEndSample, beatStarts[beat] + 1);
            }
        }

        return beatStarts;
    }

    public static bool TryFindBarContext(
        IReadOnlyList<WaveformBarMark> bars,
        long positionSample,
        long frameCount,
        int sampleRate,
        out WaveformBarMark bar,
        out WaveformBarMark state,
        out long barStartSample,
        out long barEndSample)
    {
        bar = default;
        state = default;
        barStartSample = 0;
        barEndSample = 0;

        WaveformBarMark? activeBar = null;
        WaveformBarMark? activeState = null;
        WaveformBarMark? nextBar = null;
        foreach (var mark in bars)
        {
            if (mark.SampleOffset <= positionSample)
            {
                activeState = mark;
                if (!mark.IsTempoChangeOnly)
                {
                    activeBar = mark;
                }

                continue;
            }

            if (!mark.IsTempoChangeOnly)
            {
                nextBar = mark;
                break;
            }
        }

        activeBar ??= bars.FirstOrDefault(mark => !mark.IsTempoChangeOnly);
        activeState ??= activeBar;
        if (activeBar is not { } foundBar || activeState is not { } foundState)
        {
            return false;
        }

        bar = foundBar;
        state = foundState;
        barStartSample = foundBar.SampleOffset;

        var bpmForEstimate = foundState.Bpm > 0d ? foundState.Bpm : foundBar.Bpm;
        var denomForEstimate = foundState.Denominator > 0 ? foundState.Denominator : foundBar.Denominator;
        var numerator = Math.Max(1, foundState.Numerator > 0 ? foundState.Numerator : foundBar.Numerator);
        var estimatedBarSamples = bpmForEstimate > 0d && denomForEstimate > 0 && sampleRate > 0
            ? (long)Math.Round(
                60d / bpmForEstimate
                * numerator
                * 4d / denomForEstimate
                * sampleRate)
            : frameCount - foundBar.SampleOffset;
        barEndSample = nextBar?.SampleOffset
            ?? Math.Min(frameCount, foundBar.SampleOffset + Math.Max(1L, estimatedBarSamples));
        return true;
    }

    private static List<(long Sample, double Bpm)> CollectTempoChangesInBar(
        IReadOnlyList<WaveformBarMark> bars,
        long barStartSample,
        long barEndSample)
    {
        var list = new List<(long Sample, double Bpm)>();
        foreach (var mark in bars)
        {
            if (mark.SampleOffset <= barStartSample || mark.SampleOffset >= barEndSample)
            {
                continue;
            }

            if (mark.Bpm <= 0d)
            {
                continue;
            }

            // 小節途中のテンポ変更（および万一の途中小節線）を拍進行に反映する。
            list.Add((mark.SampleOffset, mark.Bpm));
        }

        return list;
    }

    private static long AdvanceOneBeat(
        long startSample,
        long barEndSample,
        ref double bpm,
        int denominator,
        int sampleRate,
        List<(long Sample, double Bpm)> tempoChanges,
        ref int tempoIdx)
    {
        var remainingMusical = 1d;
        var cursor = startSample;

        while (remainingMusical > 1e-9)
        {
            while (tempoIdx < tempoChanges.Count && tempoChanges[tempoIdx].Sample <= cursor)
            {
                var markedBpm = tempoChanges[tempoIdx].Bpm;
                if (markedBpm > 0d)
                {
                    bpm = markedBpm;
                }

                tempoIdx++;
            }

            long? nextTempoAt = null;
            double nextTempoBpm = bpm;
            if (tempoIdx < tempoChanges.Count)
            {
                nextTempoAt = tempoChanges[tempoIdx].Sample;
                nextTempoBpm = tempoChanges[tempoIdx].Bpm;
            }

            if (bpm <= 0d)
            {
                bpm = 120d;
            }

            var samplesPerBeat = Math.Max(
                1d,
                60d / bpm * (4d / Math.Max(1, denominator)) * sampleRate);

            if (nextTempoAt is long tempoAt && tempoAt > cursor)
            {
                var samplesToTempo = tempoAt - cursor;
                var musicalToTempo = samplesToTempo / samplesPerBeat;
                if (musicalToTempo >= remainingMusical - 1e-12)
                {
                    cursor += (long)Math.Round(remainingMusical * samplesPerBeat);
                    remainingMusical = 0d;
                }
                else
                {
                    remainingMusical -= musicalToTempo;
                    cursor = tempoAt;
                    if (nextTempoBpm > 0d)
                    {
                        bpm = nextTempoBpm;
                    }

                    tempoIdx++;
                }

                continue;
            }

            cursor += (long)Math.Round(remainingMusical * samplesPerBeat);
            remainingMusical = 0d;
        }

        return Math.Clamp(cursor, startSample, barEndSample);
    }
}
