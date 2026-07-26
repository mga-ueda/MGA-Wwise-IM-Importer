using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// Music Switch Container のトランジション（WAAPI で設定可能な範囲）。
/// <para>
/// 既定の Any → Any（名前 <c>Transition</c>）は必ず先頭に明示する。
/// <c>@TransitionRoot</c> を渡すと Wwise 側の既定ルールが消えることがあるため、
/// Audiokinetic の object.set 例と同様に空の Any→Any を children 先頭へ含める
/// （Exit Source At は Immediate。Wwise 既定の Exit Cue は使わない）。
/// 続けて各 Playlist 向け Any → Object ルールを追加する（Exit Source At は遷移先の記憶値）。
/// Destination Sync To = Entry Cue。
/// Source Fade-out / Destination Fade-in の有効フラグは秒数に応じて立てる。
/// MusicFade の Time / Offset / Curve と Play post-exit（PlaySourcePostExit）は
/// WAAPI 非対応のため EXPORT 後の WWU 直編集で書く。
/// </para>
/// <para>
/// WAAPI 上のプロパティ名は UI 表示名と異なる。
/// Destination Sync To → DestinationJumpPositionPreset（Entry Cue = 0）。
/// </para>
/// </summary>
internal static class WaapiMusicTransitionDefaults
{
    // DestinationJumpPositionPreset: Entry Cue（異なる Playlist 間）
    private const int DestinationJumpPositionEntryCue = 0;
    // Context: Any / Object
    private const int ContextAny = 0;
    private const int ContextObject = 2;

    public const string DefaultAnyToAnyName = "Transition";

    /// <summary>
    /// 既定 Any→Any ＋ 各 Playlist 向け Any→Object を含む TransitionRoot。
    /// </summary>
    public static Dictionary<string, object?> BuildTransitionRoot(
        string containerPath,
        IReadOnlyList<WwisePlaylistPlan> playlists)
    {
        var children = new List<object>(playlists.Count + 1)
        {
            BuildDefaultAnyToAnyRule(),
        };
        foreach (var playlist in playlists)
        {
            children.Add(BuildAnyToPlaylistRule(containerPath, playlist));
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "MusicTransition",
            ["name"] = string.Empty,
            ["@IsFolder"] = true,
            ["children"] = children,
        };
    }

    /// <summary>Any → Any（名前 Transition）。Exit Source At は Immediate。</summary>
    private static Dictionary<string, object?> BuildDefaultAnyToAnyRule() =>
        new()
        {
            ["type"] = "MusicTransition",
            ["name"] = DefaultAnyToAnyName,
            ["@SourceContextType"] = ContextAny,
            ["@DestinationContextType"] = ContextAny,
            ["@ExitSourceAt"] = ToWaapiExitSourceAt(PlaylistExitSourceMode.Immediate),
        };

    private static Dictionary<string, object?> BuildAnyToPlaylistRule(
        string containerPath,
        WwisePlaylistPlan playlist) =>
        new()
        {
            ["type"] = "MusicTransition",
            // Wwise 既定どおり名前は Transition（Playlist 名にはしない）。
            ["name"] = DefaultAnyToAnyName,
            ["@SourceContextType"] = ContextAny,
            ["@DestinationContextType"] = ContextObject,
            ["@DestinationContextObject"] = $"{containerPath}\\{playlist.Name}",
            ["@ExitSourceAt"] = ToWaapiExitSourceAt(playlist.ExitSourceAt),
            ["@DestinationJumpPositionPreset"] = DestinationJumpPositionEntryCue,
            ["@EnableSourceFadeOut"] = playlist.FadeOutSeconds > 0 ? 1 : 0,
            ["@EnableDestinationFadeIn"] = playlist.FadeInSeconds > 0 ? 1 : 0,
        };

    /// <summary>Wwise ExitSourceAt / MusicSyncType（Change Occurs At）列挙値へ変換する。</summary>
    public static int ToWaapiExitSourceAt(PlaylistExitSourceMode mode) => mode switch
    {
        PlaylistExitSourceMode.Immediate => 0,
        PlaylistExitSourceMode.NextBar => 2,
        PlaylistExitSourceMode.NextBeat => 3,
        PlaylistExitSourceMode.NextCue => 4,
        PlaylistExitSourceMode.ExitCue => 7,
        _ => 2,
    };

    /// <summary>
    /// Music Track の StateGroupInfo/@MusicSyncType（UI: Change Occurs At）。
    /// Exit Source At と同じ列挙値を使う。
    /// </summary>
    public static int ToWaapiMusicSyncType(PlaylistExitSourceMode mode) =>
        ToWaapiExitSourceAt(mode);
}
