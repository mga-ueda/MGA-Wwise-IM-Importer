using System.Text;

namespace MgaWwiseIMImporter.Wave;

/// <summary>RIFF/WAV バイナリ読み取りの共通ヘルパー。</summary>
internal static class WavRiff
{
    /// <summary>チャンク ID 等の FourCC を ASCII 4 文字として読む（EOF 際は短い文字列になる）。</summary>
    public static string ReadFourCc(BinaryReader reader) =>
        Encoding.ASCII.GetString(reader.ReadBytes(4));
}
