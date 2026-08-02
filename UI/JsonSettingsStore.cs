using System.Text.Json;
using System.Text.Json.Serialization;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// AppData の settings.json をメモリ保持し、更新のたびに原子的に書き込む。
/// </summary>
internal static class JsonSettingsStore
{
    private static readonly object Gate = new();
    private static AppSettingsDocument _document = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static AppSettingsDocument Document
    {
        get
        {
            lock (Gate)
            {
                return _document;
            }
        }
    }

    public static void Load()
    {
        lock (Gate)
        {
            _document = ReadFromDisk() ?? CreateDefaultDocument();
#if !DEBUG
            _document.Colors = null;
#endif
            if (!File.Exists(AppStorage.SettingsPath))
            {
                SaveUnlocked();
            }
        }
    }

    public static void Update(Action<AppSettingsDocument> mutator)
    {
        ArgumentNullException.ThrowIfNull(mutator);
        lock (Gate)
        {
            mutator(_document);
#if !DEBUG
            _document.Colors = null;
#endif
            SaveUnlocked();
        }
    }

    public static void Save()
    {
        lock (Gate)
        {
#if !DEBUG
            _document.Colors = null;
#endif
            SaveUnlocked();
        }
    }

    private static AppSettingsDocument CreateDefaultDocument()
    {
        var doc = new AppSettingsDocument();
        var defaults = ProjectSettingsStore.CreateAppDefaults();
        doc.Projects.Active = defaults.Name;
        doc.Projects.Items = [ProjectProfileData.FromProfile(defaults)];
        return doc;
    }

    private static AppSettingsDocument? ReadFromDisk()
    {
        var path = AppStorage.SettingsPath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = TextFileUtf8.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AppSettingsDocument>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveUnlocked()
    {
        Directory.CreateDirectory(AppStorage.RootDirectory);
        var path = AppStorage.SettingsPath;
        var json = JsonSerializer.Serialize(_document, JsonOptions);
        var tempPath = path + ".tmp";
        TextFileUtf8.WriteAllText(tempPath, json, emitBom: false);
        File.Copy(tempPath, path, overwrite: true);
        try
        {
            File.Delete(tempPath);
        }
        catch
        {
            // 一時ファイル削除失敗は無視する。
        }
    }
}
