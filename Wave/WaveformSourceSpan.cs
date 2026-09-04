namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// 複数波形モードの仮想タイムライン上で、1 本のソース WAV が占める区間。
/// </summary>
/// <param name="Path">ソース WAV フルパス。</param>
/// <param name="WavInfo">当該ファイルのフォーマット情報。</param>
/// <param name="VirtualStartSample">仮想タイムライン上の開始（含む）。</param>
/// <param name="FrameCount">このファイルのフレーム数（長さ）。</param>
internal readonly record struct WaveformSourceSpan(
    string Path,
    WavFileInfo WavInfo,
    long VirtualStartSample,
    long FrameCount)
{
    public long VirtualEndSample => VirtualStartSample + FrameCount;

    /// <summary>半開区間 [VirtualStart, VirtualEnd) に含まれるか。</summary>
    public bool ContainsSample(long sampleOffset) =>
        sampleOffset >= VirtualStartSample && sampleOffset < VirtualEndSample;

    /// <summary>マーカー移動用の inclusive 範囲。フレームが無ければ false。</summary>
    public bool TryGetInclusiveSampleRange(out long rangeMinInclusive, out long rangeMaxInclusive)
    {
        rangeMinInclusive = VirtualStartSample;
        rangeMaxInclusive = VirtualEndSample - 1;
        return FrameCount > 0 && rangeMaxInclusive >= rangeMinInclusive;
    }

    /// <summary>仮想サンプルが属するソース区間を返す。境界は半開 [start, end)。</summary>
    public static bool TryFindContaining(
        IReadOnlyList<WaveformSourceSpan> spans,
        long sampleOffset,
        out WaveformSourceSpan span)
    {
        foreach (var candidate in spans)
        {
            if (candidate.ContainsSample(sampleOffset))
            {
                span = candidate;
                return true;
            }
        }

        span = default;
        return false;
    }
}
