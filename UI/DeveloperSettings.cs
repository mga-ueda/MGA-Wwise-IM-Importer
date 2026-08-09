namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 開発者向け設定（AppData の settings.json / developer）。
/// </summary>
internal sealed class DeveloperSettings
{
    /// <summary>Playlist／再生エンジンの詳細診断ログを出すか。既定はオン。</summary>
    public bool DetailedPlaybackLog { get; init; } = true;

    public static DeveloperSettings Load()
    {
        var data = JsonSettingsStore.Document.Developer ?? new DeveloperSettingsData();
        return new DeveloperSettings
        {
            DetailedPlaybackLog = data.DetailedPlaybackLog,
            // DeveloperSettingsData.UiScaleSimulateDpi は WPF 移行で廃止。
            // 旧 settings.json に残っていても読み捨て（キー削除は互換のため行わない）。
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
}
