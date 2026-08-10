namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Dialogs ---
    public static string DialogExitTitle => Get("終了確認", "Confirm exit");
    public static string DialogExitBody => Get(
        "アプリケーションを終了しますか？",
        "Do you want to exit the application?");

    public static string DialogDeleteProjectTitle => Get("プロジェクト削除", "Delete project");
    public static string DialogDeleteProjectBody(string name) => Format(
        "プロジェクト「{0}」を削除しますか？",
        "Delete project “{0}”?",
        name);

    public static string DialogCreateProjectFailedTitle => Get(
        "プロジェクトの作成に失敗",
        "Failed to create project");

    public static string DialogRenameFailedTitle => Get(
        "名前を変更できません",
        "Cannot rename");

    public static string DialogRenameFailedBody => Get(
        "ファイル名として使用できる、拡張子なしの名前を入力してください。"
        + Environment.NewLine
        + "（ \\ / : * ? \" < > | や制御文字、末尾の . ／空白、CON／COM1 などの予約名は不可）",
        "Enter a valid file name without extension."
        + Environment.NewLine
        + "(Cannot use \\ / : * ? \" < > |, control chars, trailing . / spaces, or reserved names such as CON / COM1.)");

    public static string DialogRenameStartsWithDigitBody => Get(
        "Wwise では先頭が数字の名前を付けられません。元の名前に戻します。",
        "Wwise does not allow names that start with a digit. Reverting to the previous name.");

    public static string DialogRenameReservedNameBody => Get(
        "CON／PRN／COM1 など Windows の予約名は使えません。元の名前に戻します。",
        "Windows reserved names such as CON / PRN / COM1 cannot be used. Reverting to the previous name.");

    public static string LogDropNameStartsWithDigit(string baseName) => Format(
        "Message : Wwise では先頭が数字の名前を使えません（拒否）: {0}",
        "Message : Wwise does not allow names that start with a digit (rejected): {0}",
        baseName);

    public static string LogDropNameInvalidFileName(string baseName) => Format(
        "Message : ファイル名として不適切な文字を含むため拒否: {0}",
        "Message : Rejected because the name contains characters invalid for a file name: {0}",
        baseName);

    public static string LogDropNameReservedWindows(string baseName) => Format(
        "Message : Windows 予約名のため拒否: {0}",
        "Message : Rejected because the name is a Windows reserved name: {0}",
        baseName);

    public static string DialogClearProjectFailedTitle => Get(
        "プロジェクトのクリアに失敗",
        "Failed to clear project");

    public static string DialogSaveProjectFailedTitle => Get(
        "プロジェクトの保存に失敗",
        "Failed to save project");

    public static string DialogLogCopyFailedTitle => Get(
        "ログのコピーに失敗",
        "Failed to copy log");

    public static string DialogLogSaveFailedTitle => Get(
        "ログの保存に失敗",
        "Failed to save log");

    public static string DialogLogSaveTitle => Get("ログを保存", "Save log");
    public static string DialogFolderBrowseDescription => Get(
        "波形の書き出し先フォルダを選択",
        "Select the folder for exported audio");

    public static string DialogExportTitle => Get("EXPORT", "EXPORT");
    public static string DialogOpenGithubFailed => Get(
        "GitHub を開けませんでした。",
        "Unable to open GitHub.");

    public static string DialogOpenCompanySiteFailed => Get(
        "ウェブサイトを開けませんでした。",
        "Unable to open the website.");

}
