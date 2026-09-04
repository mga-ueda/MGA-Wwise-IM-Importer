using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Tests;

public class WaveformPreviewSessionTests
{
    [Fact]
    public void ClampWaveOnlyMarkerMove_MultiWave_KeepsEndOfAInsideA()
    {
        var session = CreateTwoWaveSession(
            frameCountA: 1000,
            frameCountB: 800,
            markers:
            [
                new WaveformMarkerMark(200, ""),
                new WaveformMarkerMark(999, ""),
            ]);

        var clamped = session.ClampWaveOnlyMarkerMove(999, 1400);

        Assert.Equal(999, clamped);
        Assert.False(session.TryMoveWaveOnlyMarker(999, 1400));
    }

    [Fact]
    public void ClampWaveOnlyMarkerMove_MultiWave_InteriorOfACannotEnterB()
    {
        var session = CreateTwoWaveSession(
            frameCountA: 1000,
            frameCountB: 800,
            markers: [new WaveformMarkerMark(400, "")]);

        var clamped = session.ClampWaveOnlyMarkerMove(400, 1500);

        Assert.Equal(999, clamped);
        Assert.True(session.TryMoveWaveOnlyMarker(400, 1500));
        Assert.Contains(session.EffectiveMarkers, marker => marker.SampleOffset == 999);
        Assert.DoesNotContain(session.EffectiveMarkers, marker => marker.SampleOffset >= 1000);
    }

    [Fact]
    public void ClampWaveOnlyMarkerMove_MultiWave_MarkerInBCannotEnterA()
    {
        var session = CreateTwoWaveSession(
            frameCountA: 1000,
            frameCountB: 800,
            markers: [new WaveformMarkerMark(1300, "")]);

        var clamped = session.ClampWaveOnlyMarkerMove(1300, 100);

        Assert.Equal(1000, clamped);
        Assert.True(session.TryMoveWaveOnlyMarker(1300, 100));
        Assert.Contains(session.EffectiveMarkers, marker => marker.SampleOffset == 1000);
        Assert.DoesNotContain(session.EffectiveMarkers, marker => marker.SampleOffset < 1000);
    }

    [Fact]
    public void ClampWaveOnlyMarkerMove_MultiWave_StillAllowsMoveInsideA()
    {
        var session = CreateTwoWaveSession(
            frameCountA: 1000,
            frameCountB: 800,
            markers: [new WaveformMarkerMark(400, "")]);

        var clamped = session.ClampWaveOnlyMarkerMove(400, 50);

        Assert.Equal(50, clamped);
        Assert.True(session.TryMoveWaveOnlyMarker(400, 50));
        Assert.Contains(session.EffectiveMarkers, marker => marker.SampleOffset == 50);
    }

    [Fact]
    public void ClampWaveOnlyMarkerMoveWithPrevious_MultiWave_PairStaysInA()
    {
        var session = CreateTwoWaveSession(
            frameCountA: 1000,
            frameCountB: 800,
            markers:
            [
                new WaveformMarkerMark(200, ""),
                new WaveformMarkerMark(999, ""),
            ]);

        var clamped = session.ClampWaveOnlyMarkerMoveWithPrevious(999, 1600);

        Assert.Equal(999, clamped);
        Assert.False(session.TryMoveWaveOnlyMarkerWithPrevious(999, 1600));
    }

    private static WaveformPreviewSession CreateTwoWaveSession(
        long frameCountA,
        long frameCountB,
        IReadOnlyList<WaveformMarkerMark> markers)
    {
        var infoA = CreateInfo("a.wav", frameCountA);
        var infoB = CreateInfo("b.wav", frameCountB);
        var totalFrames = frameCountA + frameCountB;
        var virtualInfo = CreateInfo("a.wav", totalFrames);
        var spans = new[]
        {
            new WaveformSourceSpan("a.wav", infoA, 0, frameCountA),
            new WaveformSourceSpan("b.wav", infoB, frameCountA, frameCountB),
        };
        var built = MultiWaveOnlyRegionBuilder.Build(markers, spans);
        var preview = new WaveformPreviewData(
            new WavPeakData([0f], [0f], totalFrames, 48000),
            "a.wav",
            virtualInfo,
            markers: markers,
            regions: built.Regions,
            outputParts: built.Parts,
            allowsSessionMarkerEdit: true,
            sourceSpans: spans);
        return new WaveformPreviewSession(preview);
    }

    private static WavFileInfo CreateInfo(string path, long frameCount) => new()
    {
        Path = path,
        AudioFormat = 1,
        Channels = 1,
        SampleRate = 48000,
        ByteRate = 96000,
        BlockAlign = 2,
        BitsPerSample = 16,
        DataSizeBytes = checked((uint)(frameCount * 2)),
    };
}
