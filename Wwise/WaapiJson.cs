using System.Text.Json;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>WAAPI 応答 JSON の共通読み取りヘルパー。</summary>
internal static class WaapiJson
{
    /// <summary>
    /// getProjectInfo 応答からプロジェクト（.wproj）パスを読む。
    /// Wwise 版によりキーが path / filePath のどちらかで返る。
    /// </summary>
    public static string ReadProjectFilePath(JsonElement project)
    {
        if (TryGetString(project, "path", out var path))
        {
            return path;
        }

        if (TryGetString(project, "filePath", out var filePath))
        {
            return filePath;
        }

        return string.Empty;
    }

    /// <summary>非空文字列プロパティのみ true。</summary>
    public static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }
}
