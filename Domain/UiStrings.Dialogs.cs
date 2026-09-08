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
        "半角英数字とアンダースコア（_）だけの名前を入力してください。"
        + Environment.NewLine
        + "（先頭の数字、CON／COM1 などの予約名は不可。半角の記号・スペースは _ に置換されます）"
        + Environment.NewLine
        + "元の名前に戻します。",
        "Enter a name using only half-width letters, digits, and underscores (_)."
        + Environment.NewLine
        + "(A leading digit and reserved names such as CON / COM1 are not allowed."
        + " Half-width symbols and spaces are replaced with _.)"
        + Environment.NewLine
        + "Reverting to the previous name.");

    public static string DialogRenameStartsWithDigitBody => Get(
        "Wwise では先頭が数字の名前を付けられません。元の名前に戻します。",
        "Wwise does not allow names that start with a digit. Reverting to the previous name.");

    public static string DialogRenameReservedNameBody => Get(
        "CON／PRN／COM1 など Windows の予約名は使えません。元の名前に戻します。",
        "Windows reserved names such as CON / PRN / COM1 cannot be used. Reverting to the previous name.");

    public static string DialogRenameNonAsciiBody => Get(
        "半角カナは使えません。日本語はそのままで構いません（State Group 名だけ Multi_Wave 等へ落とします）。"
        + Environment.NewLine
        + "元の名前に戻します。",
        "Half-width katakana cannot be used. Japanese is fine"
        + " (only the State Group name falls back to Multi_Wave or similar)."
        + Environment.NewLine
        + "Reverting to the previous name.");

    public static string DialogRenameSymbolBody => Get(
        "半角の記号・スペースはアンダースコア（_）に置換しても、この名前は使えません。"
        + Environment.NewLine
        + "元の名前に戻します。",
        "This name cannot be used even after replacing half-width symbols and spaces with _."
        + Environment.NewLine
        + "Reverting to the previous name.");

    public static string LogRenameReverted(string attemptedName) => Format(
        "Message : 名前「{0}」は使えないため、元の名前に戻しました。",
        "Message : Reverted the name; “{0}” cannot be used.",
        attemptedName);

    public static string LogRenameSpacesConverted(string fromName, string toName) => Format(
        "Message : 半角の記号・スペースを _ に置換しました: 「{0}」→「{1}」",
        "Message : Replaced half-width symbols / spaces with _: “{0}” → “{1}”",
        fromName,
        toName);

    public static string LogDropNameStartsWithDigit(string baseName) => Format(
        "Message : Wwise では先頭が数字の名前を使えません（拒否）: {0}",
        "Message : Wwise does not allow names that start with a digit (rejected): {0}",
        baseName);

    public static string LogDropNameInvalidFileName(string baseName) => Format(
        "Message : ファイル名（拡張子を除く）に使えない文字（ : < > * ? \" \\ / | . % や制御文字）を含むため拒否: {0}",
        "Message : Rejected because the base name contains forbidden characters"
        + " ( : < > * ? \" \\ / | . % or control chars): {0}",
        baseName);

    public static string LogDropNameReservedWindows(string baseName) => Format(
        "Message : Windows 予約名のため拒否: {0}",
        "Message : Rejected because the name is a Windows reserved name: {0}",
        baseName);

    public static string LogDropAllRejectedDueToInvalidName => Get(
        "Message : 不正な名前のファイルが 1 件でもあるため、ドロップしたファイルをすべて拒否しました。"
        + Environment.NewLine
        + "Message : 使える名前のファイルだけをドロップし直してください。",
        "Message : One or more files have invalid names, so every dropped file was rejected."
        + Environment.NewLine
        + "Message : Drop only files with usable names.");

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
    public static string DialogWwiseImportFailedTitle => Get(
        "Wwise インポートに失敗",
        "Wwise import failed");
    public static string DialogOpenGithubFailed => Get(
        "GitHub を開けませんでした。",
        "Unable to open GitHub.");

    public static string DialogOpenCompanySiteFailed => Get(
        "ウェブサイトを開けませんでした。",
        "Unable to open the website.");

}
