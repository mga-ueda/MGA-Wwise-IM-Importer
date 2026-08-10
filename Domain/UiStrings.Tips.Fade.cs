namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Fade / Exit Source Tips ---
    public static string TipFadeInHeader => Get(
        "いま再生しているソース側のフェードイン時間です（次ソースの Destination Fade-in ではありません）。",
        "Fade-in time for the currently playing source (not Wwise Destination Fade-in).");

    public static string TipFadeOutHeader => Get(
        "いま再生しているソース側のフェードアウト時間です。",
        "Fade-out time for the currently playing source.");

    public static string TipExitSourceHeader => Get(
        "再生中に別 Playlist へ移るとき、いまのソースをどのタイミングで退出するかです。",
        "When jumping to another playlist while playing, when the current source should exit.");

    public static string TipFadeNone => Get(
        "フェードなし（即時）。",
        "No fade (immediate).");

    public static string TipFadeSeconds(string seconds) => Format(
        "{0} 秒のフェードです。Playlist を選んでから変更するとそのパート（グループ）に記憶されます。",
        "{0} second fade. Select a playlist first to store it per part (group).",
        seconds);

    public static string TipGroupFadeHeader => Get(
        "同一グループ内の遷移だけで使う Group Fade です。通常の Fade はグループ内では無効になります。"
        + " グループ化していても Playlist ごとに個別設定できます。",
        "Group Fade used only for transitions inside the same group. Normal Fade is disabled within a group."
        + " Even when grouped, each playlist can have its own value.");

    public static string TipOptionsHeader => Get(
        "Options。Playlist ごとの追加設定です（同一グループ ID では共有）。",
        "Options. Extra per-playlist settings (shared within the same group ID).");

    public static string TipPlayMinusE => Get(
        "オンのとき、`-L` ループ折り返しで `-E` を二重再生します（Wwise の Play post-exit 相当）。"
        + Environment.NewLine
        + "同一グループ ID の Playlist では設定を共有します（未グループは Playlist ごと）。"
        + Environment.NewLine
        + "EXPORT 時は遷移先向け Any→Object ルールの Play post-exit へ反映します。"
        + Environment.NewLine
        + "[E] 再生中（またはシークバー位置）の Playlist（グループなら共有値）をトグル",
        "When on, dual-plays -E on -L loop wrap (Wwise Play post-exit)."
        + Environment.NewLine
        + "Shared within the same group ID (per playlist when ungrouped)."
        + Environment.NewLine
        + "EXPORT writes Play post-exit on Any→Object transition rules for the destination."
        + Environment.NewLine
        + "[E] toggle for the playing (or seek-bar) playlist (shared value if grouped)");

    public static string TipAutoActive => Get(
        "オンのとき、EXPORT 完了後に Wwise を前面化します。"
        + " Always on Top がオンのときは、このアプリを最小化してから前面化します。",
        "When on, brings Wwise to the foreground after EXPORT completes."
        + " If Always on Top is on, this app is minimized first.");

    public static string TipChangeOccursAtHeader => Get(
        "Change Occurs At（表示は Chg Occ At）。"
        + " 同一グループ内でレイヤーを切り替える（上乗せ／停止・Same Time 遷移）タイミングです。"
        + " Group Fade と同様、Playlist ごとに個別設定できます。",
        "Change Occurs At (shown as Chg Occ At)."
        + " When layer changes occur within the same group (overlay / stop / Same Time transitions)."
        + " Like Group Fade, each playlist can have its own value.");

    public static string TipExitImmediate => Get(
        "即座に退出して遷移します。",
        "Exit immediately and transition.");

    public static string TipExitNextBar => Get(
        "次の小節境界で退出します。",
        "Exit at the next bar boundary.");

    public static string TipExitNextBeat => Get(
        "次の拍境界で退出します。",
        "Exit at the next beat boundary.");

    public static string TipExitNextCue => Get(
        "次の Custom Cue（単発マーカー）で退出します。",
        "Exit at the next Custom Cue (single marker).");

    public static string TipExitExitCue => Get(
        "Exit Cue で退出します。",
        "Exit at the Exit Cue.");

}
