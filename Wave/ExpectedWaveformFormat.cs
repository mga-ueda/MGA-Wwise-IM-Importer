using System.Globalization;

namespace MgaWwiseIMImporter.Wave;

/// <summary>
/// 歯車設定の波形フォーマット規定（Sample Rate / Bit Depth / Channels）。
/// </summary>
internal readonly record struct ExpectedWaveformFormat(
    uint SampleRateHz,
    ushort BitsPerSample,
    ushort Channels)
{
    public static ExpectedWaveformFormat Default { get; } = new(48_000, 24, 2);

    public string ToCompactText() =>
        FormatCompact(SampleRateHz, BitsPerSample, Channels);

    public bool Matches(WavFileInfo info) =>
        info.SampleRate == SampleRateHz
        && info.BitsPerSample == BitsPerSample
        && info.Channels == Channels;

    public static string FormatCompact(WavFileInfo info) =>
        FormatCompact(info.SampleRate, info.BitsPerSample, info.Channels);

    public static string FormatCompact(uint sampleRateHz, ushort bitsPerSample, ushort channels) =>
        $"{FormatSampleRate(sampleRateHz)} {bitsPerSample}bit {channels}ch";

    /// <summary>48000 → 48kHz、44100 → 44.1kHz。</summary>
    public static string FormatSampleRate(uint sampleRateHz)
    {
        if (sampleRateHz == 0)
        {
            return "0Hz";
        }

        if (sampleRateHz % 1000 == 0)
        {
            return $"{sampleRateHz / 1000u}kHz";
        }

        var khz = sampleRateHz / 1000d;
        return khz.ToString("0.###", CultureInfo.InvariantCulture) + "kHz";
    }

    public static ExpectedWaveformFormat Normalize(
        int sampleRateHz,
        int bitsPerSample,
        int channels)
    {
        var rate = (uint)Math.Clamp(sampleRateHz, 1, 384_000);
        var bits = (ushort)Math.Clamp(bitsPerSample, 1, 64);
        var ch = (ushort)Math.Clamp(channels, 1, 64);
        return new ExpectedWaveformFormat(rate, bits, ch);
    }
}
