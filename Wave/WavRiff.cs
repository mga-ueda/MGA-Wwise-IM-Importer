using System.IO;
using System.Text;
using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wave;

/// <summary>チャンクサイズがファイル末尾を超えたときの扱い。</summary>
internal enum WavRiffOverrunPolicy
{
    /// <summary>走査を打ち切る。</summary>
    Stop,

    /// <summary><see cref="UiStrings.ErrChunkSizeInvalid"/> を throw。</summary>
    Throw,

    /// <summary>サイズ超過チェックをしない。</summary>
    Ignore,
}

/// <summary>WAVE 内の 1 チャンク。</summary>
/// <param name="Id">FourCC。</param>
/// <param name="Size">データ部バイト数（パディング不含）。</param>
/// <param name="DataStart">データ先頭のストリーム位置。</param>
internal readonly record struct WavChunk(string Id, uint Size, long DataStart);

/// <summary>RIFF/WAV バイナリ読み取りの共通ヘルパー。</summary>
internal static class WavRiff
{
    /// <summary>チャンク ID 等の FourCC を ASCII 4 文字として読む（EOF 際は短い文字列になる）。</summary>
    public static string ReadFourCc(BinaryReader reader) =>
        Encoding.ASCII.GetString(reader.ReadBytes(4));

    public static long PaddedSize(uint size) => size + (size & 1);

    /// <summary>ストリーム先頭で RIFF/WAVE を検証。失敗時は既存メッセージで throw。</summary>
    public static void EnsureWaveHeader(Stream stream, BinaryReader reader)
    {
        stream.Position = 0;
        if (ReadFourCc(reader) != "RIFF")
        {
            throw new InvalidDataException(UiStrings.ErrNotRiffHeader);
        }

        _ = reader.ReadUInt32();
        if (ReadFourCc(reader) != "WAVE")
        {
            throw new InvalidDataException(UiStrings.ErrNotWaveFormat);
        }
    }

    /// <summary>EnsureWaveHeader 相当。失敗時は false。</summary>
    public static bool TryEnsureWaveHeader(Stream stream, BinaryReader reader)
    {
        try
        {
            stream.Position = 0;
            if (ReadFourCc(reader) != "RIFF")
            {
                return false;
            }

            _ = reader.ReadUInt32();
            return ReadFourCc(reader) == "WAVE";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// WAVE ボディを走査する。visit 時点で Position は DataStart。
    /// visit 後に API がパディング込みで次チャンクへ進める。
    /// visit が false を返すと早期終了。
    /// </summary>
    public static void WalkChunks(
        Stream stream,
        BinaryReader reader,
        Func<WavChunk, bool> visit,
        WavRiffOverrunPolicy overrun = WavRiffOverrunPolicy.Stop)
    {
        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = ReadFourCc(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkDataStart = stream.Position;

            if (overrun != WavRiffOverrunPolicy.Ignore
                && chunkDataStart + chunkSize > stream.Length)
            {
                if (overrun == WavRiffOverrunPolicy.Throw)
                {
                    throw new InvalidDataException(UiStrings.ErrChunkSizeInvalid(chunkId));
                }

                break;
            }

            var chunk = new WavChunk(chunkId, chunkSize, chunkDataStart);
            var keepGoing = visit(chunk);
            stream.Position = chunkDataStart + PaddedSize(chunkSize);
            if (!keepGoing)
            {
                break;
            }
        }
    }

    public static bool TryFindChunk(
        Stream stream,
        BinaryReader reader,
        string chunkId,
        out WavChunk chunk,
        WavRiffOverrunPolicy overrun = WavRiffOverrunPolicy.Stop,
        bool ensureHeader = true)
    {
        chunk = default;
        if (ensureHeader)
        {
            if (overrun == WavRiffOverrunPolicy.Ignore)
            {
                if (!TryEnsureWaveHeader(stream, reader))
                {
                    return false;
                }
            }
            else
            {
                EnsureWaveHeader(stream, reader);
            }
        }

        WavChunk found = default;
        var hit = false;
        WalkChunks(
            stream,
            reader,
            c =>
            {
                if (!string.Equals(c.Id, chunkId, StringComparison.Ordinal))
                {
                    return true;
                }

                found = c;
                hit = true;
                return false;
            },
            overrun);

        chunk = found;
        return hit;
    }

    public static bool TryFindDataChunk(
        Stream stream,
        BinaryReader reader,
        out long dataStart,
        out uint dataSize,
        WavRiffOverrunPolicy overrun = WavRiffOverrunPolicy.Ignore,
        bool ensureHeader = true)
    {
        dataStart = -1;
        dataSize = 0;
        if (!TryFindChunk(stream, reader, "data", out var chunk, overrun, ensureHeader))
        {
            return false;
        }

        dataStart = chunk.DataStart;
        dataSize = chunk.Size;
        return true;
    }
}
