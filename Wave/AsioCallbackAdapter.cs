using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// ASIO コールバックへ要求バイト数を必ず返す。途中の短読は無音で埋め、
/// 終端（0 バイト）だけ短く返して <see cref="AsioOut.HasReachedEnd"/> を立てる。
/// あわせてコールバック間隔・Read 所要時間・出力サンプル値を検査し、
/// 異常を「audio.anomaly」、停止時の集計を「audio.stats」として報告する。
/// </summary>
internal sealed class AsioCallbackAdapter : IWaveProvider
{
    private readonly IWaveProvider _source;
    private readonly Action<string>? _diagnostic;
    private long _lastReadEndTimestamp;
    private long _lastAnomalyTimestamp;
    private long _firstReadTimestamp;

    // 計測サマリ（ASIO スレッドが更新、UI スレッドが停止時に読む。診断用途なので tear は許容）
    private long _callbackCount;
    private double _maxGapMs;
    private double _maxReadMs;
    private double _lastBufferMs;
    private int _shortReadCount;
    private int _badSampleCount;
    private int _gapCount;
    private int _gcCount;
    private int _lastGen0;
    private int _lastGen1;
    private int _lastGen2;

    public AsioCallbackAdapter(IWaveProvider source, Action<string>? diagnostic = null)
    {
        _source = source;
        _diagnostic = diagnostic;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Play／Pause 直後の正当な空白を欠落として誤検知しないようリセットする。</summary>
    public void MarkDiscontinuity() => _lastReadEndTimestamp = 0;

    /// <summary>直近の再生区間の計測サマリを返し、カウンタをリセットする。</summary>
    public string DescribeAndResetStats()
    {
        // 実経過時間と音声換算時間。正常なら両者はほぼ一致する。
        // wallMs が audioMs より大幅に短ければ、ドライバがペーシングせず
        // 一気に読み尽くしている（＝無音のまま即終了する）ことの証拠になる。
        var wallMs = _firstReadTimestamp != 0 && _lastReadEndTimestamp != 0
            ? TicksToMs(_lastReadEndTimestamp - _firstReadTimestamp)
            : 0.0;
        var audioMs = _callbackCount * _lastBufferMs;
        var text =
            $"audio.stats callbacks={_callbackCount}"
            + $" bufferMs={_lastBufferMs:F1}"
            + $" wallMs={wallMs:F0}"
            + $" audioMs={audioMs:F0}"
            + $" maxGapMs={_maxGapMs:F1}"
            + $" gaps={_gapCount}"
            + $" maxReadMs={_maxReadMs:F2}"
            + $" shortReads={_shortReadCount}"
            + $" badSamples={_badSampleCount}"
            + $" gcCollections={_gcCount}";
        _callbackCount = 0;
        _maxGapMs = 0;
        _maxReadMs = 0;
        _shortReadCount = 0;
        _badSampleCount = 0;
        _gapCount = 0;
        _gcCount = 0;
        _firstReadTimestamp = 0;
        return text;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var frames = count / 8;
        var bufferMs = frames * 1000.0 / Math.Max(1, WaveFormat.SampleRate);
        _lastBufferMs = bufferMs;
        var startTimestamp = Stopwatch.GetTimestamp();
        if (_firstReadTimestamp == 0)
        {
            _firstReadTimestamp = startTimestamp;
        }

        // 前回コールバック以降に GC が走ったか（ギャップ＝GC 停止かの判定材料）。
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var gcSinceLast = gen0 != _lastGen0 || gen1 != _lastGen1 || gen2 != _lastGen2;
        _lastGen0 = gen0;
        _lastGen1 = gen1;
        _lastGen2 = gen2;

        if (_lastReadEndTimestamp != 0)
        {
            if (gcSinceLast)
            {
                _gcCount++;
            }

            var gapMs = TicksToMs(startTimestamp - _lastReadEndTimestamp);
            _maxGapMs = Math.Max(_maxGapMs, gapMs);
            // 1 コールバック分の取りこぼし（gap≒2×buffer）から検出する。
            if (gapMs > bufferMs * 1.5 + 1.0)
            {
                _gapCount++;
                ReportAnomaly(
                    $"audio.anomaly asio-callback-gap gapMs={gapMs:F1} bufferMs={bufferMs:F1}"
                    + $" gc={(gcSinceLast ? "yes" : "no")}");
            }
        }

        var got = _source.Read(buffer, offset, count);
        var endTimestamp = Stopwatch.GetTimestamp();
        _lastReadEndTimestamp = endTimestamp;
        _callbackCount++;
        var readMs = TicksToMs(endTimestamp - startTimestamp);
        _maxReadMs = Math.Max(_maxReadMs, readMs);
        if (readMs > bufferMs * 0.8)
        {
            ReportAnomaly(
                $"audio.anomaly asio-read-slow readMs={readMs:F2} bufferMs={bufferMs:F1}");
        }

        if (got <= 0)
        {
            return 0;
        }

        if (got < count)
        {
            // ファイル末尾の端数で必ず発生する正常系。異常としては報告せず件数のみ集計する。
            _shortReadCount++;
            Array.Clear(buffer, offset + got, count - got);
        }

        InspectSamples(buffer, offset, got);
        return count;
    }

    /// <summary>NaN／無限大／大きく振り切れた値の混入（＝一発ノイズの実体）を検査する。</summary>
    private void InspectSamples(byte[] buffer, int offset, int bytes)
    {
        var samples = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, bytes));
        foreach (var sample in samples)
        {
            if (float.IsNaN(sample) || float.IsInfinity(sample) || Math.Abs(sample) > 4f)
            {
                _badSampleCount++;
                ReportAnomaly($"audio.anomaly asio-bad-sample value={sample:R}");
                return;
            }
        }
    }

    private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private void ReportAnomaly(string message)
    {
        if (_diagnostic is null)
        {
            return;
        }

        // 連続検知でログが溢れないよう 500ms に 1 件へ間引く。
        var now = Stopwatch.GetTimestamp();
        if (_lastAnomalyTimestamp != 0 && TicksToMs(now - _lastAnomalyTimestamp) < 500.0)
        {
            return;
        }

        _lastAnomalyTimestamp = now;
        _diagnostic(message);
    }
}
