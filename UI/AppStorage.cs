namespace MgaWwiseIMImporter.UI;

/// <summary>
/// ユーザー設定の配置場所（Local AppData）。exe 横には書かない。
/// </summary>
internal static class AppStorage
{
    public const string CompanyFolderName = "MGA";
    public const string AppFolderName = "MGA Wwise IM Importer";
    public const string SettingsFileName = "settings.json";
    public const string SessionsFolderName = "sessions";

    /// <summary>
    /// ディレクトリ作成と settings.json 読み込み。
    /// <see cref="MainWindow"/> 生成より前に呼ぶこと。
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(SessionsDirectory);
        JsonSettingsStore.Load();
    }

    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        CompanyFolderName,
        AppFolderName);

    public static string SettingsPath => Path.Combine(RootDirectory, SettingsFileName);

    public static string SessionsDirectory => Path.Combine(RootDirectory, SessionsFolderName);
}
