namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 開発者向け設定（AppData の settings.json / developer）。
/// </summary>
internal sealed class DeveloperSettings
{
    /// <summary>Playlist／再生エンジンの詳細診断ログを出すか。既定はオン。</summary>
    public bool DetailedPlaybackLog { get; init; } = true;

    /// <summary>
    /// DEBUG 専用。UI スケールシミュレート対象 DPI。
    /// 0 = ディスプレイどおり、96 = 100% 相当、144 = 150% 相当。
    /// </summary>
    public int UiScaleSimulateDpi { get; init; }

    public static DeveloperSettings Load()
    {
        var data = JsonSettingsStore.Document.Developer ?? new DeveloperSettingsData();
        return new DeveloperSettings
        {
            DetailedPlaybackLog = data.DetailedPlaybackLog,
            UiScaleSimulateDpi = data.UiScaleSimulateDpi,
        };
    }

    /// <summary>DetailedPlaybackLog だけ更新する（他キーは維持）。</summary>
    public static void SaveDetailedPlaybackLog(bool enabled)
    {
        JsonSettingsStore.Update(doc =>
        {
            doc.Developer ??= new DeveloperSettingsData();
            doc.Developer.DetailedPlaybackLog = enabled;
        });
    }

#if DEBUG
    /// <summary>UiScaleSimulateDpi だけ更新する（他キーは維持）。</summary>
    public static void SaveUiScaleSimulateDpi(int dpi)
    {
        JsonSettingsStore.Update(doc =>
        {
            doc.Developer ??= new DeveloperSettingsData();
            doc.Developer.UiScaleSimulateDpi = dpi;
        });
    }
#endif
}
