using System.Text.Json;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>WAAPI 応答 JSON の共通読み取りヘルパー。</summary>
internal static class WaapiJson
{
    /// <summary>
    /// getProjectInfo / object.get 応答から .wproj のファイルシステムパスを読む。
    /// <c>path</c> は Wwise オブジェクトパス（\Name）のことがあるため、.wproj だけを採用する。
    /// </summary>
    public static string ReadProjectFilePath(JsonElement project)
    {
        if (TryGetString(project, "filePath", out var filePath) && LooksLikeProjectFilePath(filePath))
        {
            return filePath;
        }

        if (TryGetString(project, "path", out var path) && LooksLikeProjectFilePath(path))
        {
            return path;
        }

        if (TryBuildProjectFilePathFromDirectories(project, out var fromDirectories))
        {
            return fromDirectories;
        }

        return string.Empty;
    }

    /// <summary>.wproj のファイルシステムパスか（Wwise オブジェクトパスは含まない）。</summary>
    public static bool LooksLikeProjectFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim().Trim('"');
        return trimmed.EndsWith(".wproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryBuildProjectFilePathFromDirectories(JsonElement project, out string path)
    {
        path = string.Empty;
        if (project.ValueKind != JsonValueKind.Object
            || !project.TryGetProperty("directories", out var directories))
        {
            return false;
        }

        if (!TryGetString(directories, "root", out var root))
        {
            return false;
        }

        TryGetString(project, "name", out var name);
        try
        {
            var directory = Path.GetFullPath(root.Trim().Trim('"'));
            if (name.Length > 0)
            {
                path = Path.Combine(directory, name + ".wproj");
                return true;
            }

            if (Directory.Exists(directory))
            {
                var matches = Directory.GetFiles(directory, "*.wproj");
                if (matches.Length == 1)
                {
                    path = matches[0];
                    return true;
                }

                if (name.Length > 0)
                {
                    var match = Array.Find(
                        matches,
                        file => string.Equals(
                            Path.GetFileNameWithoutExtension(file),
                            name,
                            StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        path = match;
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
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
