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
    public void Process_PercentAndDotNames_RejectsEntireDrop()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mga-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var valid = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "battle.wav"), 48000, 480);
            var percent = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "bad%name.wav"), 48000, 480);
            var dotted = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "bad.take2.wav"), 48000, 480);

            var report = DroppedFilesProcessor.Process(
                [valid, percent, dotted],
                out var preview);

            Assert.Null(preview);
            Assert.Contains(UiStrings.LogDropNameInvalidFileName("bad%name"), report);
            Assert.Contains(UiStrings.LogDropNameInvalidFileName("bad.take2"), report);
            Assert.Contains(UiStrings.LogDropAllRejectedDueToInvalidName, report);
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

    [Fact]
    public void Process_PcmAndExtensibleSameLayout_LoadsMultiWave()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mga-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pcm = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "battle.wav"), 48000, 480);
            var extensible = TestWavFactory.WriteSilentExtensiblePcm16Mono(
                Path.Combine(dir, "loop.wav"), 48000, 480);

            var report = DroppedFilesProcessor.Process(
                [pcm, extensible],
                out var preview);

            Assert.NotNull(preview);
            Assert.True(preview.IsMultiWaveOnly);
            Assert.DoesNotContain(UiStrings.LogMultiWaveOnlyFormatMismatch(pcm, extensible), report);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Process_PcmAndIeeeFloat_RejectsWithFormatTags()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mga-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pcm = TestWavFactory.WriteSilentPcm16Mono(
                Path.Combine(dir, "battle.wav"), 48000, 480);
            var ieee = TestWavFactory.WriteSilentIeeeFloat32Mono(
                Path.Combine(dir, "loop.wav"), 48000, 480);

            var report = DroppedFilesProcessor.Process(
                [pcm, ieee],
                out var preview);

            Assert.Null(preview);
            Assert.Contains(UiStrings.LogMultiWaveOnlyFormatMismatch(pcm, ieee), report);
            Assert.Contains("PCM (1)", report);
            Assert.Contains("IEEE Float (3)", report);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
