using MgaWwiseIMImporter.Nuendo;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public class WaveformBarOverlayBuilderTests
{
    [Fact]
    public void Build_DoesNotTreatSampleRoundedBarStartAsAnacrusis()
    {
        var bpm = 92.00000762939453125;
        var sampleRate = 48000u;
        var tracklist = CreateTracklist(bpm);
        var tempoMap = new TempoMap(tracklist.TempoEvents, tracklist.SignatureEvents);
        var exactSamples = tempoMap.PpqToSamples(3840, sampleRate);
        var timeRef = (ulong)Math.Round(exactSamples, MidpointRounding.AwayFromZero);

        var result = WaveformBarOverlayBuilder.Build(
            tracklist,
            CreateWav(sampleRate, timeRef, frameCount: 48000 * 70));

        Assert.False(result.HasAnacrusis);
        Assert.Equal(3840d, result.WaveStartPpq, 3);
        Assert.DoesNotContain(
            result.Regions,
            region => region.NameSuffix.Equals("-A", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Build_KeepsTrueAnacrusisOneBeatAfterBar()
    {
        var bpm = 92d;
        var sampleRate = 48000u;
        var tracklist = CreateTracklist(bpm);
        var tempoMap = new TempoMap(tracklist.TempoEvents, tracklist.SignatureEvents);
        var pickupPpq = 3840d + 480d;
        var timeRef = (ulong)Math.Round(
            tempoMap.PpqToSamples(pickupPpq, sampleRate),
            MidpointRounding.AwayFromZero);

        var result = WaveformBarOverlayBuilder.Build(
            tracklist,
            CreateWav(sampleRate, timeRef, frameCount: 48000 * 70));

        Assert.True(result.HasAnacrusis);
        Assert.Contains(
            result.Regions,
            region => region.NameSuffix.Equals("-A", StringComparison.OrdinalIgnoreCase)
                && region.StartSampleOffset == 0);
    }

    private static NuendoTracklistInfo CreateTracklist(double bpm) =>
        new()
        {
            Path = "test.xml",
            TempoEvents =
            [
                new NuendoTempoEvent { Bpm = bpm, Ppq = 0 },
            ],
            SignatureEvents =
            [
                new NuendoSignatureEvent
                {
                    Ppq = 0,
                    Numerator = 4,
                    Denominator = 4,
                    Bar = 0,
                },
            ],
            MarkerEvents =
            [
                new NuendoMarkerEvent
                {
                    Kind = NuendoMarkerKind.CycleRegion,
                    StartPpq = 7680,
                    LengthPpq = 38400,
                    Name = "-L",
                },
            ],
        };

    private static WavFileInfo CreateWav(uint sampleRate, ulong timeReference, int frameCount) =>
        new()
        {
            Path = "test.wav",
            FileSizeBytes = 1,
            AudioFormat = 1,
            Channels = 2,
            SampleRate = sampleRate,
            ByteRate = sampleRate * 6,
            BlockAlign = 6,
            BitsPerSample = 24,
            DataSizeBytes = (uint)(frameCount * 6),
            HasIXml = true,
            TimeReferenceSamples = timeReference,
        };
}
