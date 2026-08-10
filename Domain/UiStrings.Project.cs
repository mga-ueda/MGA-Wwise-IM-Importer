namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Project store ---
    public static string ErrProjectNotFound(string name) => Format(
        "プロジェクトが見つかりません: {0}",
        "Project not found: {0}",
        name);

    public static string ErrProjectNameRequired => Get(
        "プロジェクト名を入力してください。",
        "Enter a project name.");

    public static string ErrProjectNameReserved => Get(
        "この名前は予約されています。",
        "This name is reserved.");

    public static string ErrProjectNameExists(string name) => Format(
        "同じ名前のプロジェクトが既にあります: {0}",
        "A project with this name already exists: {0}",
        name);

}
