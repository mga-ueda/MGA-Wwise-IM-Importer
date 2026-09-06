using System.Runtime.InteropServices;
using System.Text;
using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// プレビュー再生用のステレオ float PCM。ロード時に展開し、コールバックではメモリ参照のみ行う。
/// </summary>
internal sealed class PlaybackPcm
{
    private const float FoldGain = 0.7071f;
    private const int DecodeChunkFrames = 4096;

    private PlaybackPcm(float[] interleavedStereo, int sampleRate, long frameCount)
    {
        InterleavedStereo = interleavedStereo;
        SampleRate = sampleRate;
        FrameCount = frameCount;
    }

    public float[] InterleavedStereo { get; }

    public int SampleRate { get; }

    public long FrameCount { get; }

    public TimeSpan Duration => SampleRate <= 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds(FrameCount / (double)SampleRate);

    public static PlaybackPcm FromWave(WavFileInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.FrameCount <= 0)
        {
            throw new InvalidDataException(UiStrings.ErrEmptyData);
        }

        return Decode(info.Path, info, info.FrameCount);
    }

    public static PlaybackPcm FromSpans(IReadOnlyList<WaveformSourceSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        if (spans.Count == 0)
        {
            throw new ArgumentException(UiStrings.ErrMultiWaveOnlyNoSpans);
        }

        var sampleRate = (int)spans[0].WavInfo.SampleRate;
        if (sampleRate <= 0)
        {
            throw new InvalidDataException(UiStrings.ErrWaveFormatInvalid);
        }

        long totalFrames = 0;
        foreach (var span in spans)
        {
            if (span.WavInfo.SampleRate != (uint)sampleRate)
            {
                throw new InvalidDataException(UiStrings.ErrWaveFormatInvalid);
            }

            totalFrames = checked(totalFrames + span.FrameCount);
        }

        if (totalFrames <= 0)
        {
            throw new InvalidDataException(UiStrings.ErrEmptyData);
        }

        var samples = AllocateStereo(totalFrames);
        var writeFloats = 0;
        foreach (var span in spans)
        {
            DecodeInto(span.Path, span.WavInfo, span.FrameCount, samples, writeFloats);
            writeFloats += checked((int)span.FrameCount * 2);
        }

        return new PlaybackPcm(samples, sampleRate, totalFrames);
    }

    public int WriteIeeeFloatStereo(long startSample, byte[] dest, int destOffset, int frames)
    {
        if (frames <= 0 || startSample >= FrameCount || startSample < 0)
        {
            return 0;
        }

        var available = (int)Math.Min(frames, FrameCount - startSample);
        var byteCount = available * 8;
        if (dest.Length < destOffset + byteCount)
        {
            available = Math.Max(0, (dest.Length - destOffset) / 8);
            byteCount = available * 8;
        }

        if (available <= 0)
        {
            return 0;
        }

        var src = InterleavedStereo.AsSpan((int)startSample * 2, available * 2);
        var dst = MemoryMarshal.Cast<byte, float>(dest.AsSpan(destOffset, byteCount));
        src.CopyTo(dst);
        return available;
    }

    private static PlaybackPcm Decode(string path, WavFileInfo info, long frameCount)
    {
        var sampleRate = (int)info.SampleRate;
        if (sampleRate <= 0)
        {
            throw new InvalidDataException(UiStrings.ErrWaveFormatInvalid);
        }

        var samples = AllocateStereo(frameCount);
        DecodeInto(path, info, frameCount, samples, destFloatOffset: 0);
        return new PlaybackPcm(samples, sampleRate, frameCount);
    }

    private static float[] AllocateStereo(long frameCount)
    {
        if (frameCount <= 0 || frameCount > int.MaxValue / 2)
        {
            throw new InvalidDataException(UiStrings.ErrPlaybackMemoryTooLong);
        }

        try
        {
            return new float[(int)frameCount * 2];
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidDataException(UiStrings.ErrPlaybackMemoryTooLong);
        }
    }

    private static void DecodeInto(
        string path,
        WavFileInfo info,
        long frameCount,
        float[] dest,
        int destFloatOffset)
    {
        if (frameCount <= 0)
        {
            return;
        }

        if (info.Channels == 0 || info.BlockAlign == 0 || info.SampleRate == 0)
        {
            throw new InvalidDataException(UiStrings.ErrWaveFormatInvalid);
        }

        var bytesPerSample = info.BitsPerSample / 8;
        if (bytesPerSample <= 0)
        {
            throw new InvalidDataException(UiStrings.ErrBitsPerSampleInvalid);
        }

        var sampleReader = WavPeakReader.CreateSampleReader(info.AudioFormat, info.BitsPerSample);
        var channels = info.Channels;
        var blockAlign = info.BlockAlign;
        var extraChannels = Math.Max(0, channels - 2);
        var normalize = 1f / (1f + extraChannels * FoldGain);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            1 << 16,
            FileOptions.SequentialScan);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (!WavPeakReader.TryFindDataChunk(stream, reader, out var dataStart, out var dataSize))
        {
            throw new InvalidDataException(UiStrings.ErrDataChunkMissing);
        }

        var fileFrames = dataSize / (uint)Math.Max(1, (int)blockAlign);
        if (frameCount > fileFrames)
        {
            throw new InvalidDataException(UiStrings.ErrMultiWaveOnlyConcatRange);
        }

        stream.Position = dataStart;
        var pcmScratch = new byte[DecodeChunkFrames * blockAlign];
        long decoded = 0;
        var writeAt = destFloatOffset;
        while (decoded < frameCount)
        {
            var chunk = (int)Math.Min(DecodeChunkFrames, frameCount - decoded);
            var bytesNeeded = chunk * blockAlign;
            var got = 0;
            while (got < bytesNeeded)
            {
                var n = stream.Read(pcmScratch, got, bytesNeeded - got);
                if (n <= 0)
                {
                    throw new InvalidDataException(UiStrings.ErrMultiWaveOnlyConcatRange);
                }

                got += n;
            }

            for (var i = 0; i < chunk; i++)
            {
                var frameOffset = i * blockAlign;
                float left;
                float right;
                if (channels == 1)
                {
                    left = right = sampleReader(pcmScratch, frameOffset);
                }
                else
                {
                    left = sampleReader(pcmScratch, frameOffset);
                    right = sampleReader(pcmScratch, frameOffset + bytesPerSample);
                    for (var ch = 2; ch < channels; ch++)
                    {
                        var v = sampleReader(pcmScratch, frameOffset + ch * bytesPerSample) * FoldGain;
                        left += v;
                        right += v;
                    }

                    left *= normalize;
                    right *= normalize;
                }

                dest[writeAt++] = left;
                dest[writeAt++] = right;
            }

            decoded += chunk;
        }
    }
}

/// <summary>共有 <see cref="PlaybackPcm"/> 上の独立した再生ヘッド。</summary>
internal sealed class PcmPlayhead
{
    private readonly PlaybackPcm _pcm;

    public PcmPlayhead(PlaybackPcm pcm) => _pcm = pcm;

    public long Sample { get; private set; }

    public void SeekToSample(long sample) =>
        Sample = Math.Clamp(sample, 0L, _pcm.FrameCount);

    public int ReadFrames(byte[] dest, int destOffset, int frames)
    {
        var got = _pcm.WriteIeeeFloatStereo(Sample, dest, destOffset, frames);
        Sample += got;
        return got;
    }
}
