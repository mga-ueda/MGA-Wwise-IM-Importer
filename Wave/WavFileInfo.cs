using System.Text;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wave;

internal sealed class WavFileInfo
{
    public required string Path { get; init; }
    public long FileSizeBytes { get; init; }
    public ushort AudioFormat { get; init; }
    public ushort Channels { get; init; }
    public uint SampleRate { get; init; }
    public uint ByteRate { get; init; }
    public ushort BlockAlign { get; init; }
    public ushort BitsPerSample { get; init; }
    public uint DataSizeBytes { get; init; }
    public bool HasIXml { get; init; }
    public ulong TimeReferenceSamples { get; init; }

    public string AudioFormatName => UiStrings.AudioFormatName(AudioFormat);

    public long FrameCount => BlockAlign == 0 ? 0 : DataSizeBytes / BlockAlign;

    public TimeSpan Duration => SampleRate == 0
        ? TimeSpan.Zero
        : TimeSpan.FromSeconds(FrameCount / (double)SampleRate);

    public static WavFileInfo Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

        var riff = WavRiff.ReadFourCc(reader);
        if (riff != "RIFF")
        {
            throw new InvalidDataException(UiStrings.ErrNotRiffHeader);
        }

        _ = reader.ReadUInt32();
        var wave = WavRiff.ReadFourCc(reader);
        if (wave != "WAVE")
        {
            throw new InvalidDataException(UiStrings.ErrNotWaveFormat);
        }

        ushort? audioFormat = null;
        ushort channels = 0;
        uint sampleRate = 0;
        uint byteRate = 0;
        ushort blockAlign = 0;
        ushort bitsPerSample = 0;
        uint dataSize = 0;
        var hasIXml = false;
        ulong timeReferenceSamples = 0;

        // data の後ろに iXML が付くことがあるため、全チャンクを走査する
        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = WavRiff.ReadFourCc(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkDataStart = stream.Position;
            if (chunkDataStart + chunkSize > stream.Length)
            {
                break;
            }

            if (chunkId == "fmt ")
            {
                if (chunkSize < 16)
                {
                    throw new InvalidDataException(UiStrings.ErrFmtChunkInvalid);
                }

                audioFormat = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadUInt32();
                byteRate = reader.ReadUInt32();
                blockAlign = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                dataSize = chunkSize;
            }
            else if (chunkId == "iXML")
            {
                hasIXml = true;
                var payload = reader.ReadBytes((int)Math.Min(chunkSize, int.MaxValue));
                if (WavIxmlReader.TryReadTimeReference(payload, out var ixmlRef))
                {
                    timeReferenceSamples = ixmlRef;
                }
            }

            var paddedSize = chunkSize + (chunkSize & 1);
            stream.Position = chunkDataStart + paddedSize;
        }

        if (audioFormat is null)
        {
            throw new InvalidDataException(UiStrings.ErrFmtChunkMissing);
        }

        return new WavFileInfo
        {
            Path = path,
            FileSizeBytes = new FileInfo(path).Length,
            AudioFormat = audioFormat.Value,
            Channels = channels,
            SampleRate = sampleRate,
            ByteRate = byteRate,
            BlockAlign = blockAlign,
            BitsPerSample = bitsPerSample,
            DataSizeBytes = dataSize,
            HasIXml = hasIXml,
            TimeReferenceSamples = timeReferenceSamples,
        };
    }

    public string ToDisplayText(ExpectedWaveformFormat? expectedFormat = null)
    {
        var offSpec = expectedFormat is { } expected && !expected.Matches(this);
        var suffix = offSpec ? $"  {UiStrings.LogWaveFormatOffSpecSuffix}" : string.Empty;
        var expectedChannels = expectedFormat?.Channels;
        var expectedRate = expectedFormat?.SampleRateHz;
        var expectedBits = expectedFormat?.BitsPerSample;

        var sb = new StringBuilder();
        sb.AppendLine(UiStrings.LogWaveHeader);
        sb.AppendLine($"{UiStrings.LabelWavPath} {Path}");
        sb.AppendLine($"{UiStrings.LabelFileSize} {FileSizeBytes:N0} bytes");
        sb.AppendLine($"{UiStrings.LabelFormat} {AudioFormatName} ({AudioFormat})");
        sb.AppendLine(
            $"{UiStrings.LabelChannels} {Channels}"
            + (expectedChannels is ushort ch && Channels != ch ? suffix : string.Empty));
        sb.AppendLine(
            $"{UiStrings.LabelSampleRate} {SampleRate} Hz"
            + (expectedRate is uint rate && SampleRate != rate ? suffix : string.Empty));
        sb.AppendLine(
            $"{UiStrings.LabelBitDepth} {BitsPerSample} bit"
            + (expectedBits is ushort bits && BitsPerSample != bits ? suffix : string.Empty));
        sb.AppendLine(
            $"{UiStrings.LabelWaveFormatCompact} {ExpectedWaveformFormat.FormatCompact(this)}"
            + (offSpec ? suffix : string.Empty));
        sb.AppendLine($"{UiStrings.LabelBlockAlign} {BlockAlign} bytes");
        sb.AppendLine($"{UiStrings.LabelByteRate} {ByteRate:N0} bytes/sec");
        sb.AppendLine($"{UiStrings.LabelDataSize} {DataSizeBytes:N0} bytes");
        sb.AppendLine($"{UiStrings.LabelFrames} {FrameCount:N0}");
        sb.AppendLine($"{UiStrings.LabelDuration} {FormatDuration(Duration)}");
        sb.AppendLine($"{UiStrings.LabelIXml} {(HasIXml ? UiStrings.BoolYes : UiStrings.BoolNo)}");
        sb.AppendLine($"{UiStrings.LabelTimeReference} {TimeReferenceSamples:N0} samples");
        if (offSpec && expectedFormat is { } expectedShown)
        {
            sb.AppendLine(UiStrings.LogWarningHeader);
            sb.AppendLine(
                UiStrings.LogWaveFormatOffSpec(
                    expectedShown.ToCompactText(),
                    ExpectedWaveformFormat.FormatCompact(this)));
        }

        return sb.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}.{duration.Milliseconds:000}"
            + $" ({duration.TotalSeconds:0.000} sec)";
    }

}
