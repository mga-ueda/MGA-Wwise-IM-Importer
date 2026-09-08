using System.IO;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Processing;

namespace MgaWwiseIMImporter.Tests;

public class DroppedFilesProcessorTests
{
    [Fact]
    public void Process_MixedValidAndDigitName_RejectsEntireDrop()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mga-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var validA = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "battle.wav"), 48000, 480);
            var validB = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "loop.wav"), 48000, 480);
            var invalid = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "1bad.wav"), 48000, 480);

            var report = DroppedFilesProcessor.Process(
                [validA, validB, invalid],
                out var preview);

            Assert.Null(preview);
            Assert.Contains(UiStrings.LogDropNameStartsWithDigit("1bad"), report);
            Assert.Contains(UiStrings.LogDropAllRejectedDueToInvalidName, report);
            Assert.DoesNotContain(UiStrings.LogMultiWaveOnlyHeader, report);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Process_TwoValidWaves_LoadsMultiWave()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mga-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var validA = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "battle.wav"), 48000, 480);
            var validB = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "loop.wav"), 48000, 480);

            var report = DroppedFilesProcessor.Process(
                [validA, validB],
                out var preview);

            Assert.NotNull(preview);
            Assert.True(preview.IsMultiWaveOnly);
            Assert.DoesNotContain(UiStrings.LogDropAllRejectedDueToInvalidName, report);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
