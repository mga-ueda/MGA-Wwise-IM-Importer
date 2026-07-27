using System.Text;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wave;

/// <summary>WAV 埋め込みの単発マーカー（cue + adtl）。</summary>
internal readonly record struct WavCueMarker(
    uint CueId,
    long SampleOffset,
    string Label,
    string Note)
{
    public string DisplayComment
    {
        get
        {
            var note = Note.Trim();
            if (note.Length > 0)
            {
                return note;
            }

            return Label.Trim();
        }
    }
}

/// <summary>WAV 埋め込みのリージョン（cue + adtl <c>ltxt</c>）。</summary>
internal readonly record struct WavCueRegion(
    uint CueId,
    long StartSampleOffset,
    uint LengthSamples,
    string Label,
    string Note);

/// <summary>WAV <c>smpl</c> チャンクのループ 1 件。</summary>
internal readonly record struct WavSmplLoop(
    uint Identifier,
    uint Type,
    uint StartSample,
    uint EndSample,
    uint PlayCount);

/// <summary>
/// WAV の cue / adtl / smpl から埋め込マーカー情報を読む（Wave 単体モード用）。
/// Nuendo/Cubase XML 経路とは独立。
/// </summary>
internal sealed class WavEmbeddedMarkerInfo
{
    public required IReadOnlyList<WavCueMarker> PointMarkers { get; init; }
    public required IReadOnlyList<WavCueRegion> Regions { get; init; }
    public required IReadOnlyList<WavSmplLoop> SmplLoops { get; init; }

    public bool HasRegions => Regions.Count > 0;
    public bool HasSmplLoops => SmplLoops.Count > 0;

    public static WavEmbeddedMarkerInfo Read(string path)
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

        var cuePositions = new Dictionary<uint, long>();
        var labels = new Dictionary<uint, string>();
        var notes = new Dictionary<uint, string>();
        var regionLengths = new Dictionary<uint, uint>();
        var smplLoops = new List<WavSmplLoop>();

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = WavRiff.ReadFourCc(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkDataStart = stream.Position;
            if (chunkDataStart + chunkSize > stream.Length)
            {
                break;
            }

            if (chunkId == "cue ")
            {
                ReadCueChunk(reader, chunkDataStart, chunkSize, cuePositions);
            }
            else if (chunkId == "smpl")
            {
                ReadSmplChunk(reader, chunkDataStart, chunkSize, smplLoops);
            }
            else if (chunkId == "LIST" && chunkSize >= 4)
            {
                var listType = WavRiff.ReadFourCc(reader);
                if (listType == "adtl")
                {
                    ReadAdtlList(reader, chunkDataStart + 4, chunkSize - 4, labels, notes, regionLengths);
                }
            }

            var paddedSize = chunkSize + (chunkSize & 1);
            stream.Position = chunkDataStart + paddedSize;
        }

        var regions = new List<WavCueRegion>();
        var regionIds = new HashSet<uint>();
        foreach (var (cueId, length) in regionLengths)
        {
            if (length == 0 || !cuePositions.TryGetValue(cueId, out var start))
            {
                continue;
            }

            regionIds.Add(cueId);
            regions.Add(new WavCueRegion(
                cueId,
                start,
                length,
                labels.GetValueOrDefault(cueId, string.Empty),
                notes.GetValueOrDefault(cueId, string.Empty)));
        }

        regions.Sort((a, b) => a.StartSampleOffset.CompareTo(b.StartSampleOffset));

        var points = new List<WavCueMarker>();
        foreach (var (cueId, sampleOffset) in cuePositions)
        {
            if (regionIds.Contains(cueId))
            {
                continue;
            }

            points.Add(new WavCueMarker(
                cueId,
                sampleOffset,
                labels.GetValueOrDefault(cueId, string.Empty),
                notes.GetValueOrDefault(cueId, string.Empty)));
        }

        points.Sort((a, b) => a.SampleOffset.CompareTo(b.SampleOffset));

        return new WavEmbeddedMarkerInfo
        {
            PointMarkers = points,
            Regions = regions,
            SmplLoops = smplLoops,
        };
    }

    private static void ReadCueChunk(
        BinaryReader reader,
        long chunkDataStart,
        uint chunkSize,
        Dictionary<uint, long> cuePositions)
    {
        if (chunkSize < 4)
        {
            return;
        }

        var count = reader.ReadUInt32();
        for (uint i = 0; i < count; i++)
        {
            var remaining = chunkDataStart + chunkSize - reader.BaseStream.Position;
            if (remaining < 24)
            {
                break;
            }

            var cueId = reader.ReadUInt32();
            var position = reader.ReadUInt32();
            var fccChunk = WavRiff.ReadFourCc(reader);
            _ = reader.ReadUInt32(); // dwChunkStart
            _ = reader.ReadUInt32(); // dwBlockStart
            var sampleOffset = reader.ReadUInt32();

            // PCM では data チャンク内オフセットを優先。無い／非 data なら dwPosition。
            long sample = fccChunk == "data" ? sampleOffset : position;
            if (fccChunk == "data" && sampleOffset == 0 && position != 0)
            {
                sample = position;
            }

            cuePositions[cueId] = sample;
        }
    }

    private static void ReadSmplChunk(
        BinaryReader reader,
        long chunkDataStart,
        uint chunkSize,
        List<WavSmplLoop> smplLoops)
    {
        // smpl 固定ヘッダ 36 bytes + cSampleLoops / cbSamplerData
        if (chunkSize < 36 + 8)
        {
            return;
        }

        // 固定ヘッダ 7×DWORD の直後が cSampleLoops / cbSamplerData
        reader.BaseStream.Position = chunkDataStart + 28;
        var sampleLoopCount = reader.ReadUInt32();
        var samplerDataSize = reader.ReadUInt32();
        var loopsEnd = chunkDataStart + chunkSize - samplerDataSize;

        for (uint i = 0; i < sampleLoopCount; i++)
        {
            if (reader.BaseStream.Position + 24 > loopsEnd)
            {
                break;
            }

            var identifier = reader.ReadUInt32();
            var type = reader.ReadUInt32();
            var start = reader.ReadUInt32();
            var end = reader.ReadUInt32();
            _ = reader.ReadUInt32(); // dwFraction
            var playCount = reader.ReadUInt32();
            smplLoops.Add(new WavSmplLoop(identifier, type, start, end, playCount));
        }
    }

    private static void ReadAdtlList(
        BinaryReader reader,
        long listDataStart,
        uint listDataSize,
        Dictionary<uint, string> labels,
        Dictionary<uint, string> notes,
        Dictionary<uint, uint> regionLengths)
    {
        var stream = reader.BaseStream;
        var listEnd = listDataStart + listDataSize;
        stream.Position = listDataStart;

        while (stream.Position + 8 <= listEnd)
        {
            var subId = WavRiff.ReadFourCc(reader);
            var subSize = reader.ReadUInt32();
            var subStart = stream.Position;
            if (subStart + subSize > listEnd)
            {
                break;
            }

            if (subId == "labl" && subSize >= 4)
            {
                var cueId = reader.ReadUInt32();
                labels[cueId] = ReadZString(reader, subStart + subSize);
            }
            else if (subId == "note" && subSize >= 4)
            {
                var cueId = reader.ReadUInt32();
                notes[cueId] = ReadZString(reader, subStart + subSize);
            }
            else if (subId == "ltxt" && subSize >= 20)
            {
                var cueId = reader.ReadUInt32();
                var sampleLength = reader.ReadUInt32();
                if (sampleLength > 0)
                {
                    regionLengths[cueId] = sampleLength;
                }
            }

            var padded = subSize + (subSize & 1);
            stream.Position = subStart + padded;
        }
    }

    private static string ReadZString(BinaryReader reader, long endExclusive)
    {
        var stream = reader.BaseStream;
        var bytes = new List<byte>();
        while (stream.Position < endExclusive)
        {
            var b = reader.ReadByte();
            if (b == 0)
            {
                break;
            }

            bytes.Add(b);
        }

        if (bytes.Count == 0)
        {
            return string.Empty;
        }

        var raw = bytes.ToArray();
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Default.GetString(raw);
        }
    }

}
