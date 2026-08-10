namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- MainWindow ポート追加分 ---
    public static string SelectOutputFolderTitle => Get("書き出し先フォルダを選択", "Select output folder");

    public static string ErrSelectFolderFailed(string message) => Format(
        "フォルダ選択に失敗しました: {0}",
        "Failed to select folder: {0}",
        message);

    public static string ErrProjectDeleteLastOne => Get(
        "最後の 1 件は削除できません。",
        "The last remaining project cannot be deleted.");

    public static string LabelDeleteProjectTitle => Get("プロジェクトを削除", "Delete Project");

    public static string ConfirmDeleteProject(string name) => Format(
        "プロジェクト「{0}」を削除しますか？",
        "Delete project \"{0}\"?",
        name);

    public static string ErrLogDownloadFailed(string message) => Format(
        "ログの保存に失敗しました: {0}",
        "Failed to save the log: {0}",
        message);

    public static string LogUpdateAvailable(string version) => Format(
        "新しいバージョンがあります: {0}",
        "Update available: {0}",
        version);

    public static string UpdateAvailableTitle => Get("アップデートのお知らせ", "Update Available");

    public static string UpdateAvailableMessage(string version) => Format(
        "新しいバージョン {0} が公開されています。ダウンロードページを開きますか？",
        "A new version {0} is available. Open the download page?",
        version);

    public static string TipPlaylistButtonRightClickDisable => Get(
        "右クリックで書き出し対象から除外／復帰します。",
        "Right-click to exclude/include this part from export.");

    public static string LogAutoLoadTargetMissing => Get(
        "自動読み込み対象が見つかりません。",
        "Auto-load target was not found.");

    public static string ErrExportFailed(string message) => Format(
        "書き出しに失敗しました: {0}",
        "Export failed: {0}",
        message);

}
