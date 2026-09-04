using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>Wwise へ作成する Interactive Music 構造の計画。</summary>
internal sealed class WwiseMusicPlan
{
    /// <summary>最上位に作るオブジェクト名（元ファイル名の拡張子抜き）。</summary>
    public required string ContainerName { get; init; }

    /// <summary>true なら Music Switch Container の下に複数 Playlist を作る。</summary>
    public required bool IsMultiPart { get; init; }

    public required IReadOnlyList<WwisePlaylistPlan> Playlists { get; init; }
}

/// <summary>Music Playlist Container 1 つ分。</summary>
internal sealed class WwisePlaylistPlan
{
    public required string Name { get; init; }

    /// <summary>
    /// Music Switch に結ぶ State 名。
    /// ドロップファイル名に 2 バイト文字が 1 つでもあれば <c>Music_1</c> 形式、
    /// それ以外は <see cref="Name"/> と同じ。
    /// </summary>
    public required string StateName { get; init; }

    /// <summary>State 名をファイル名から差し替えたか。</summary>
    public bool UsesFallbackStateName =>
        !string.Equals(Name, StateName, StringComparison.Ordinal);

    /// <summary>
    /// 代表となるエクスポート WAV のフルパス（コピー／ログ用）。
    /// レイヤーグループ時は先頭メンバーのパス。
    /// </summary>
    public required string SourceWavPath { get; init; }

    /// <summary>この Playlist を構成するソースパート番号（先頭が代表）。</summary>
    public required IReadOnlyList<int> SourcePartNumbers { get; init; }

    /// <summary>Any → この Playlist ルールに載せる Exit Source At（遷移先の記憶値）。</summary>
    public required PlaylistExitSourceMode ExitSourceAt { get; init; }

    /// <summary>
    /// Destination Fade-in の秒数（遷移先の記憶値。0＝None）。
    /// WAAPI では MusicFade を作れないため、EXPORT 後に WWU 直編集で書く。
    /// </summary>
    public required double FadeInSeconds { get; init; }

    /// <summary>
    /// Source Fade-out の秒数（遷移先の記憶値。0＝None）。
    /// WAAPI では MusicFade を作れないため、EXPORT 後に WWU 直編集で書く。
    /// </summary>
    public required double FadeOutSeconds { get; init; }

    /// <summary>Destination Fade-in のカーブ（遷移先の記憶値）。</summary>
    public required RegionFadeCurveKind FadeInCurve { get; init; }

    /// <summary>Source Fade-out のカーブ（遷移先の記憶値）。</summary>
    public required RegionFadeCurveKind FadeOutCurve { get; init; }

    /// <summary>
    /// Play post-exit（UI: Play -E。遷移先の記憶値。既定 false）。
    /// Any → この Playlist ルール（複数パート時）と、この Playlist Container 自身の
    /// 既定ルール（Any to Any）の両方に載せる。
    /// WAAPI 非対応のため、EXPORT 後に WWU の <c>PlaySourcePostExit</c> へ直編集する。
    /// </summary>
    public required bool PlayPostExit { get; init; }

    /// <summary>
    /// グループ（2 パート以上）時に作る State Group 計画。
    /// 未グループの単一パート Playlist では null。
    /// </summary>
    public WwiseGroupStatePlan? GroupState { get; init; }

    public required IReadOnlyList<WwiseSegmentPlan> Segments { get; init; }
}

/// <summary>
/// グループ化 Playlist 向け State Group。
/// グループ名と同名の State Group に、メンバー数分の State（A, B, C…）を持つ。
/// </summary>
internal sealed class WwiseGroupStatePlan
{
    /// <summary>State Group 名（通常は Playlist／グループ名と同じ）。</summary>
    public required string Name { get; init; }

    /// <summary>State 名一覧（A, B, C…。メンバー Playlist 数と一致）。</summary>
    public required IReadOnlyList<string> StateNames { get; init; }

    /// <summary>
    /// true なら全 State の Group Fade が同一。
    /// Default Transition Time のみを使い、Custom TransitionList は書かない（既存があればクリア）。
    /// </summary>
    public required bool UseDefaultTransitionOnly { get; init; }

    /// <summary>
    /// <see cref="UseDefaultTransitionOnly"/> 時に Default Transition Time へ載せる秒数。
    /// 個別 Custom 時は未使用（Wwise 既定の 1 秒のまま）。
    /// </summary>
    public required double DefaultTransitionSeconds { get; init; }

    /// <summary>
    /// State 名 → そのレイヤーの Group Fade 秒数。
    /// Custom TransitionList の From→To では <b>To（遷移先 State）</b> の秒数を使う。
    /// </summary>
    public required IReadOnlyDictionary<string, double> TransitionSecondsByState { get; init; }

    /// <summary>
    /// true なら追加再生タイプ。State Volume は累積（下位レイヤー以降を 0dB、それ未満を -108dB）。
    /// false（既定）なら排他切替（対応 State のみ 0dB）。
    /// </summary>
    public bool AdditiveLayers { get; init; }
}

/// <summary>
/// Music Segment 1 つ分。時間はセグメント代表タイムライン基準の絶対 ms（先頭トラックのパート先頭基準）。
/// インポート時は各トラックを自身の Clip 範囲でトリムし、タイムライン先頭へ載せる。
/// </summary>
internal sealed class WwiseSegmentPlan
{
    public required string Name { get; init; }

    /// <summary>セグメント全体の可聴開始（代表タイムライン、通常は 0 相対の基準用）。</summary>
    public required double ClipStartMs { get; init; }

    /// <summary>セグメント全体の可聴終了（EndPosition 算出用）。</summary>
    public required double ClipEndMs { get; init; }

    /// <summary>Entry Cue の絶対時刻（-A があればアウフタクト明け）。</summary>
    public required double EntryCueMs { get; init; }

    /// <summary>Exit Cue の絶対時刻（-E があればその開始）。</summary>
    public required double ExitCueMs { get; init; }

    /// <summary>-L 区間なら true（Playlist Item を無限ループにする）。</summary>
    public required bool LoopInfinite { get; init; }

    public required double TempoBpm { get; init; }
    public required int TimeSignatureUpper { get; init; }
    public required int TimeSignatureLower { get; init; }

    /// <summary>単発マーカー由来の Custom Cue（名前は重複回避済み）。</summary>
    public required IReadOnlyList<WwiseCustomCue> CustomCues { get; init; }

    /// <summary>同一セグメント内で同時再生する Music Track（縦レイヤー）。</summary>
    public required IReadOnlyList<WwiseTrackPlan> Tracks { get; init; }
}

/// <summary>Music Track 1 つ分（1 つのソースパート WAV からの範囲指定）。</summary>
internal sealed class WwiseTrackPlan
{
    public required string Name { get; init; }

    /// <summary>このトラックの元パート番号（Make-Up Gain 照合用）。</summary>
    public required int SourcePartNumber { get; init; }

    /// <summary>エクスポートされたパート WAV のフルパス（コピー前）。</summary>
    public required string SourceWavPath { get; init; }

    /// <summary>ソース WAV 内の可聴開始（切り出し開始）。</summary>
    public required double ClipStartMs { get; init; }

    /// <summary>ソース WAV 内の可聴終了（切り出し終了）。</summary>
    public required double ClipEndMs { get; init; }

    /// <summary>元ファイル絶対サンプル（切り出し開始、フェード照合用）。</summary>
    public required long AbsoluteStartSample { get; init; }

    /// <summary>元ファイル絶対サンプル（切り出し終了・排他、フェード照合用）。</summary>
    public required long AbsoluteEndSample { get; init; }

    /// <summary>
    /// グループ内レイヤーに対応する State 名（A, B, C…）。
    /// 未グループ時は null。
    /// </summary>
    public string? LayerStateName { get; init; }

    /// <summary>
    /// グループ内レイヤー切替の Change Occurs At（StateGroupInfo/@MusicSyncType）。
    /// 未グループ／単一トラック時は null。
    /// </summary>
    public PlaylistExitSourceMode? ChangeOccursAt { get; init; }
}

/// <summary>Custom Cue 1 つ。</summary>
internal readonly record struct WwiseCustomCue(double TimeMs, string Name);
