using System.IO;
using System.Text.Json;
using MgaWwiseIMImporter.Wwise;

namespace MgaWwiseIMImporter.Tests;

public class WaapiJsonTests
{
    [Fact]
    public void ReadProjectFilePath_PrefersFilePathOverObjectPath()
    {
        using var doc = JsonDocument.Parse("""
            {
              "name": "MyGame",
              "path": "\\MyGame",
              "filePath": "D:\\Wwise\\MyGame.wproj"
            }
            """);

        Assert.Equal(@"D:\Wwise\MyGame.wproj", WaapiJson.ReadProjectFilePath(doc.RootElement));
    }

    [Fact]
    public void ReadProjectFilePath_IgnoresWwiseObjectPath()
    {
        using var doc = JsonDocument.Parse("""
            { "name": "MyGame", "path": "\\MyGame" }
            """);

        Assert.Equal(string.Empty, WaapiJson.ReadProjectFilePath(doc.RootElement));
    }

    [Fact]
    public void ReadProjectFilePath_AcceptsPathWhenItIsWproj()
    {
        using var doc = JsonDocument.Parse("""
            { "name": "MyGame", "path": "C:/Wwise/MyGame.wproj" }
            """);

        Assert.Equal("C:/Wwise/MyGame.wproj", WaapiJson.ReadProjectFilePath(doc.RootElement));
    }

    [Fact]
    public void ReadProjectFilePath_BuildsFromDirectoriesRootAndName()
    {
        using var doc = JsonDocument.Parse("""
            {
              "name": "MyGame",
              "directories": { "root": "D:\\Wwise\\MyGame" }
            }
            """);

        var path = WaapiJson.ReadProjectFilePath(doc.RootElement);
        Assert.True(WaapiJson.LooksLikeProjectFilePath(path));
        Assert.Equal("MyGame.wproj", Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"D:\Wwise\MyGame.wproj", true)]
    [InlineData(@"\\MyGame", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void LooksLikeProjectFilePath_RequiresWproj(string? path, bool expected)
    {
        Assert.Equal(expected, WaapiJson.LooksLikeProjectFilePath(path));
    }
}

public class WwiseProjectActivatorPathTests
{
    [Fact]
    public void TryFindProjectFileNearDirectory_FindsWprojAboveOriginals()
    {
        var root = Path.Combine(Path.GetTempPath(), "mga-wproj-" + Guid.NewGuid().ToString("N"));
        var originals = Path.Combine(root, "Originals", "SFX");
        Directory.CreateDirectory(originals);
        var wproj = Path.Combine(root, "MyGame.wproj");
        File.WriteAllText(wproj, "<WwiseDocument />");
        try
        {
            var found = WwiseProjectActivator.TryFindProjectFileNearDirectory(originals);
            Assert.Equal(wproj, found);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
