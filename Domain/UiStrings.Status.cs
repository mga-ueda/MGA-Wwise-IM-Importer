namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Keep Target / status ---
    public static string TipKeepTargetUnlock => Get(
        "いまの作成先パスをこのプロジェクト設定で固定します。"
        + " その後 Wwise 上で選択を変えても、表示と EXPORT 先はこの固定パスのままです。"
        + " 起動時／EXPORT 前には可能なら Wwise 上でも同じパスを再選択します。",
        "Lock the current destination path in this project’s settings."
        + " Later Wwise selection changes will not change the display or EXPORT target."
        + " On startup / before EXPORT, the same path is re-selected in Wwise when possible.");

    public static string TipKeepTargetLock => Get(
        "作成先の固定を解除します（このプロジェクト設定）。",
        "Unlock the destination path (this project setting).");

    public static string TipWwiseProjectNameOpen => Get(
        "[Ctrl+Shift+W] この Wwise プロジェクトを開きます（既に開いていれば前面に表示）。",
        "[Ctrl+Shift+W] Open this Wwise project (or bring it to the front if already open).");

    public static string KeepTargetOnLabel => Get("- Keep Target -", "- Keep Target -");
    public static string KeepTargetOffLabel => Get("- Not Keep Target -", "- Not Keep Target -");

    // --- Status / empty UI ---
    public static string StatusChecking => Get("確認中…", "Checking…");
    public static string StatusStartupCheckOff => Get("起動時チェックオフ", "Startup check off");
    public static string StatusDisconnected => Get("未接続", "Disconnected");
    public static string StatusNoneSelected => Get("（未選択）", "(none selected)");
    public static string StatusNoProject => Get("(プロジェクトなし)", "(no project)");

    public static string WaveformEmptyHint => Get(
        ".wav または .xml をドロップ",
        "Drop .wav or .xml");

    public static string DialogBarJumpTitle => Get("小節へジャンプ", "Jump to bar");

    public static string MarkerCommentNeedPrefix => Get(
        "Digits が 0 のときは Prefix を入力してください",
        "Enter a Prefix when Digits is 0");

    public static string MarkerCommentEmptyName => Get(
        "名前が空です",
        "Name is empty");

    public static string MarkerCommentControlChars => Get(
        "制御文字は使用できません",
        "Control characters are not allowed");

    public static string PlaylistNone => Get("Playlist はありません", "No playlists");
    public static string PlaylistLoading => Get("読み込み中…", "Loading…");
    public static string PlaylistFetchFailed => Get(
        "Playlist を取得できませんでした",
        "Failed to get playlists");

}
