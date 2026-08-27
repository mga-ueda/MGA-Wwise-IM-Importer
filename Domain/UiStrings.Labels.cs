namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Labels / Buttons / Status / Log keys / Progress / Accessibility / ColorDev ---

    // Form1: action bar / checkboxes / buttons
    public static string LabelKeepLastSession => Get("Keep Last Session", "Keep Last Session");
    public static string LabelAlwaysOnTop => Get("Always on Top", "Always on Top");
    public static string LabelDebugLog => Get("Debug Log", "Debug Log");
    public static string LabelCompactFileNumbers => Get("Compact Num.", "Compact Num.");
    public static string LabelClear => Get("CLEAR", "CLEAR");
    public static string LabelReload => Get("RELOAD", "RELOAD");
    public static string LabelExport => Get("EXPORT", "EXPORT");
    public static string LabelNone => Get("None", "None");

    // Form1: Fade / Exit Source At / Group / Playlist header
    public static string LabelFadeIn => Get("Fade In", "Fade In");
    public static string LabelFadeOut => Get("Fade Out", "Fade Out");
    public static string LabelOptions => Get("Options", "Options");
    public static string LabelPlayMinusE => Get("Play -E", "Play -E");
    public static string LabelAutoActive => Get("Auto Active", "Auto Active");
    public static string LabelGroup => Get("Group", "Group");
    public static string LabelChangeOccursAt => Get("Chg Occ At", "Chg Occ At");
    public static string LabelExitSourceAt => Get("Exit Source At", "Exit Source At");
    public static string LabelMusicPlaylist => Get("Music Playlist", "Music Playlist");
    public static string LabelImmediate => Get("Immediate", "Immediate");
    public static string LabelNextBar => Get("Next Bar", "Next Bar");
    public static string LabelNextBeat => Get("Next Beat", "Next Beat");
    public static string LabelNextCue => Get("Next Cue", "Next Cue");
    public static string LabelExitCue => Get("Exit Cue", "Exit Cue");
    public static string LabelTimeline => Get("Timeline", "Timeline");
    public static string LabelBar => Get("Bar", "Bar");
    public static string LabelBeat => Get("Beat", "Beat");

    /// <summary>Fade ラジオの表示（"1.0 Sec."）。0 以下は <see cref="LabelNone"/>。表記は日英とも英語固定。</summary>
    public static string LabelFadeSeconds(double seconds)
    {
        if (seconds <= 0d)
        {
            return LabelNone;
        }

        var value = seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        return $"{value} Sec.";
    }

    /// <summary>Exit Source At ラジオの表示名。</summary>
    public static string LabelExitSource(PlaylistExitSourceMode mode) => mode switch
    {
        PlaylistExitSourceMode.Immediate => LabelImmediate,
        PlaylistExitSourceMode.NextBar => LabelNextBar,
        PlaylistExitSourceMode.NextBeat => LabelNextBeat,
        PlaylistExitSourceMode.NextCue => LabelNextCue,
        PlaylistExitSourceMode.ExitCue => LabelExitCue,
        _ => mode.ToString(),
    };

    /// <summary>波形一覧で無効化した Playlist の代替表示名（英語固定）。</summary>
    public static string LabelExcludedRegion(int index) =>
        string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Excluded Region {0}",
            index);

    // Form1: フォームタイトル・著作権表記
    public static string FormTitle => AppVersion.FormTitle;

    public static string DialogUpdateAvailableTitle => Get(
        "アップデートのお知らせ",
        "Update available");

    public static string DialogUpdateAvailableBody(
        string localVersion,
        string remoteVersion,
        bool isPrerelease) => Format(
        "新しいバージョンがあります。{0}{0}"
        + "現在: {1}{0}"
        + "最新: {2}{3}{0}{0}"
        + "GitHub のリリースページを開きますか？{0}"
        + "（自動ダウンロードは行いません）",
        "A newer version is available.{0}{0}"
        + "Current: {1}{0}"
        + "Latest: {2}{3}{0}{0}"
        + "Open the GitHub release page?{0}"
        + "(This app does not download updates automatically.)",
        Environment.NewLine,
        localVersion,
        remoteVersion,
        isPrerelease
            ? Get("（プレリリース）", " (pre-release)")
            : string.Empty);

    public static string LogUpdateAvailable(string localVersion, string remoteVersion) =>
        Format(
            "Message : 新しいバージョンがあります: {0} → {1}。",
            "Message : Update available: {0} → {1}.",
            localVersion,
            remoteVersion);

    /// <summary>フッタ権利表記。リンク文言（GitHub / SIL Open Font License）は常に英語。</summary>
    public static string CopyrightText => Get(
        "© 2026 MIYABI GAME AUDIO INC.  GitHub"
        + "\nWwise® and Audiokinetic® are trademarks of Audiokinetic Inc."
        + "\nSIL Open Font License",
        "© 2026 MIYABI GAME AUDIO INC.  GitHub"
        + "\nWwise® and Audiokinetic® are trademarks of Audiokinetic Inc."
        + "\nSIL Open Font License");

    /// <summary>フッタ 3 行目のライセンスリンク文言（常に英語）。</summary>
    public const string CopyrightLicenseLinkText = "SIL Open Font License";

    public static string DialogLicenseTitle => Get(
        CopyrightLicenseLinkText,
        CopyrightLicenseLinkText);

    public static string DialogLicenseMissing => Get(
        "License text is not embedded in this build.",
        "License text is not embedded in this build.");

    // Form1: アクセシビリティ名（元から英語固定）
    public static string AccessibleProjectFolderButton => Get(
        "Select export folder",
        "Select export folder");

    public static string AccessibleProjectDeleteButton => Get(
        "Delete project",
        "Delete project");

    public static string AccessibleSpectrum => Get(
        "Output spectrum",
        "Output spectrum");

    public static string AccessibleLogClear => Get("Clear log", "Clear log");
    public static string AccessibleLogCopy => Get("Copy log", "Copy log");
    public static string AccessibleLogDownload => Get("Download log", "Download log");

    // MarkerOptionsPanel
    public static string LabelStream => Get("Stream", "Stream");
    public static string LabelPrefetchLength => Get("Prefetch Length", "Prefetch Length");
    public static string LabelLookAheadTime => Get("Look-ahead Time", "Look-ahead Time");
    public static string LabelMsUnit => Get("ms", "ms");
    public static string LabelLayerMusicOption => Get(
        "Layer Music Option",
        "Layer Music Option");
    public static string LabelKeepLayerBalance => Get("Keep Layer Balance", "Keep Layer Balance");

    public static string LabelAdditiveLayers => Get("Additive\nLayer", "Additive\nLayer");
    public static string LabelMarkerGridHeader => Get("Marker Grid", "Marker Grid");
    public static string LabelMarkerComment => Get("Marker Comment", "Marker Comment");
    public static string LabelDigits => Get("Digits", "Digits");
    public static string LabelZeroPad => Get("Zero Pad", "Zero Pad");
    public static string LabelResetPerPart => Get("Reset Per Part", "Reset Per Part");
    public static string LabelPrefix => Get("Prefix", "Prefix");
    public static string LabelSuffix => Get("Suffix", "Suffix");
    public static string LabelSeparator => Get("Separator", "Separator");

    /// <summary>More Options 見出し（開閉状態で ▾／▸ を切り替える）。</summary>
    public static string LabelMoreOptions(bool expanded) =>
        (expanded ? "▾ " : "▸ ") + Get("More Options", "More Options");

    /// <summary>Marker Comment のプレビュー例（英語固定 "e.g. {0}"）。</summary>
    public static string LabelPreviewExample(string example) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, "e.g. {0}", example);

    // TransportBar
    public static string LabelTransportGroup => Get("TRANS", "TRANS");
    public static string LabelNavigationGroup => Get("NAV", "NAV");
    public static string LabelTimeZoomGroup => Get("TIME", "TIME");
    public static string LabelAmpZoomGroup => Get("AMP", "AMP");
    public static string LabelWaveformHeightGroup => Get("SIZE", "SIZE");

    public static string AccessibleTransportPositionDisplay => Get(
        "Tempo, time signature, musical position and elapsed time",
        "Tempo, time signature, musical position and elapsed time");

    // WaveformView: 情報レーン行ラベル・下段レーン名
    public static string LabelMeasure => Get("Measure", "Measure");
    public static string LabelTempo => Get("Tempo", "Tempo");
    public static string LabelSignature => Get("Signature", "Signature");
    public static string LabelMarker => Get("Marker", "Marker");
    public static string LabelMusicSegmentName => Get("Music Segment Name", "Music Segment Name");
    public static string LabelMusicPlaylistName => Get("Music Playlist Name", "Music Playlist Name");

    public static IReadOnlyList<string> WaveformInfoRowLabels =>
        [LabelMeasure, LabelTempo, LabelSignature, LabelMarker];

    // WaapiStatusBar
    public static string WaapiTitle => Get("WAAPI", "WAAPI");
    public static string WaapiBadgeConnect => Get("CONNECT", "CONNECT");
    public static string WaapiBadgeDisconnect => Get("DISCONNECT", "DISCONNECT");
    public static string LabelWwise => Get("Wwise", "Wwise");
    public static string LabelUnnamedProject => Get("(unnamed)", "(unnamed)");
    public static string LabelUnnamedMarker => Get("(unnamed)", "(unnamed)");

    /// <summary>波形範囲外マーカーの種別表示（<c>WaveformIgnoredOutsideMark.Kind</c> の内部識別子 → 表示名）。英語固定。</summary>
    public static string LabelIgnoredMarkKind(string kind) => kind switch
    {
        "Cycle" => "Cycle",
        "Marker" => LabelMarker,
        _ => kind,
    };

    // LanguageFlagButton
    public static string LanguageBadgeJapanese => Get("JP", "JP");
    public static string LanguageBadgeEnglish => Get("EN", "EN");

    // ProjectSettingsStore
    public static string ProjectNewProjectMenuItem => Get("+ New Project", "+ New Project");
    public static string ProjectNewProjectBaseName => Get("New Project", "New Project");

    // Progress / busy overlay（元から英語固定）
    public static string OverlayExporting => Get("Exporting", "Exporting");
    public static string OverlayLoading => Get("Loading", "Loading");
    public static string OverlayStarting => Get("Starting", "Starting");
    public static string OverlayLoadingLastSession => Get("Loading Last Session", "Loading Last Session");

    // BarJumpDialog 描画タイトル（ウィンドウ Title は DialogBarJumpTitle。描画は元から英語固定）
    public static string LabelGoToMeasure => Get("Go To Measure", "Go To Measure");

    // Log headers (=== ... ===) — 表記は日英共通
    public static string LogWaapiHeader => Get("=== WAAPI ===", "=== WAAPI ===");
    public static string LogExportHeader => Get("=== Export ===", "=== Export ===");
    public static string LogSessionHeader => Get("=== Session ===", "=== Session ===");
    public static string LogGoToMeasureHeader => Get("=== Go To Measure ===", "=== Go To Measure ===");
    public static string LogWaveHeader => Get("=== Wave ===", "=== Wave ===");
    public static string LogWaveformHeader => Get("=== Waveform ===", "=== Waveform ===");
    public static string LogNuendoTempoTrackHeader => Get(
        "=== Nuendo/Cubase Tempo Track ===",
        "=== Nuendo/Cubase Tempo Track ===");

    // Report / log field keys（固定幅の列見出しは日英共通）
    public static string KeyStatus => Get("Status  :", "Status  :");
    public static string KeyTarget => Get("Target  :", "Target  :");
    public static string KeyType => Get("Type    :", "Type    :");
    public static string KeyMessage => Get("Message :", "Message :");
    public static string KeyDetail => Get("Detail  :", "Detail  :");
    public static string KeyOutput => Get("Output  :", "Output  :");
    public static string KeyOriginals => Get("Originals:", "Originals:");
    public static string KeyProject => Get("Project :", "Project :");
    public static string KeyWwise => Get("Wwise   :", "Wwise   :");
    public static string KeyMode => Get("Mode    :", "Mode    :");
    public static string KeyName => Get("Name    :", "Name    :");
    public static string KeyStateGrp => Get("StateGrp :", "StateGrp :");
    public static string KeySource => Get("Source :", "Source :");
    public static string KeyPeaks => Get("Peaks  :", "Peaks  :");
    public static string KeyRegions => Get("Regions:", "Regions:");
    public static string KeyOutputs => Get("Outputs:", "Outputs:");
    public static string KeyBars => Get("Bars   :", "Bars   :");
    public static string KeyTimeline => Get("Timeline:", "Timeline:");
    public static string KeyPath => Get("Path    :", "Path    :");
    public static string KeySlices => Get("Media   :", "Media   :");
    public static string KeyWavePpq => Get("WavePpq :", "WavePpq :");

    public static string LogDroppedFilesHeader(int count) => Format(
        "Dropped files: {0}",
        "Dropped files: {0}",
        count);

    // 以下の進捗・診断ログは元から英語固定（言語切替でも英語のまま）
    public static string LogAnacrusisYes => Get(
        "Anacrusis : yes (relative Bar 1 @ wave start, next bar line = 2)",
        "Anacrusis : yes (relative Bar 1 @ wave start, next bar line = 2)");

    public static string LogAnacrusisNo => Get(
        "Anacrusis : no (wave starts on a bar line → relative Bar 1)",
        "Anacrusis : no (wave starts on a bar line → relative Bar 1)");

    // Wwise import progress
    public static string LogBuildingImportPlan => Get(
        "Building import plan...",
        "Building import plan...");

    public static string LogPlanReady(int playlistCount) => Format(
        "Plan ready: {0} playlist(s).",
        "Plan ready: {0} playlist(s).",
        playlistCount);

    public static string LogImportPlanHeader => Get(
        "=== Import Plan ===",
        "=== Import Plan ===");

    public static string LogImportPlanPlaylists(int playlistCount, string containerName) => Format(
        "Container: {0} / Playlists: {1}",
        "Container: {0} / Playlists: {1}",
        containerName,
        playlistCount);

    public static string LogExportRegionHeader => Get(
        "=== Export Regions ===",
        "=== Export Regions ===");

    public static string LogExportRegionIncluded(
        int index,
        string suffix,
        long start,
        long end) => Format(
        "  [{0}] {1}  samples=[{2} .. {3})",
        "  [{0}] {1}  samples=[{2} .. {3})",
        index,
        suffix,
        start,
        end);

    public static string LogExportRegionExcluded(int index, long start, long end) => Format(
        "  [{0}] -R  samples=[{1} .. {2})  (excluded)",
        "  [{0}] -R  samples=[{1} .. {2})  (excluded)",
        index,
        start,
        end);

    public static string LogExportRegionTotals(int included, int excluded) => Format(
        "Included: {0} / Excluded(-R): {1}",
        "Included: {0} / Excluded(-R): {1}",
        included,
        excluded);

    public static string LogExportMarkerHeader(int count) => Format(
        "Markers : {0} 件",
        "Markers : {0}",
        count);

    public static string LogExportMarkerLine(long sample, string comment, bool embedded) => Format(
        "  @ {0:N0}  \"{1}\"{2}",
        "  @ {0:N0}  \"{1}\"{2}",
        sample,
        string.IsNullOrEmpty(comment) ? "(名前なし)" : comment,
        embedded ? "  (埋め込み)" : string.Empty);

    public static string LogTrackMediaBinding(
        string segmentName,
        string trackName,
        string fileName,
        long localStart,
        long localEnd,
        bool reusedOriginal,
        bool applyClipTrim = false) => Format(
        "Media : {0} / {1} → {2}  samples=[{3} .. {4})  {5}",
        "Media : {0} / {1} → {2}  samples=[{3} .. {4})  {5}",
        segmentName,
        trackName,
        fileName,
        localStart,
        localEnd,
        applyClipTrim ? "copy+trim" : reusedOriginal ? "copy" : "slice");

    public static string LogMusicClipCatalog(int count) => Format(
        "Message : MusicClip を {0} 件検出しました（トリム対象の検索用）。",
        "Message : Found {0} MusicClip(s) for trim lookup.",
        count);

    public static string LogMusicClipTrimApplied(
        string trackName,
        string segmentName,
        double beginMs,
        double endMs) => Format(
        "Message : Clip trim → {0} @ {1}  Begin={2:0.###}ms  End={3:0.###}ms",
        "Message : Clip trim → {0} @ {1}  Begin={2:0.###}ms  End={3:0.###}ms",
        trackName,
        segmentName,
        beginMs,
        endMs);

    public static string LogMusicClipFadeCatalog(int count) => Format(
        "Message : MusicClip を {0} 件検出しました（リージョン端フェード設定用）。",
        "Message : Found {0} MusicClip(s) for region-edge fade lookup.",
        count);

    public static string LogMusicClipFadeApplied(
        string trackName,
        string segmentName,
        double? fadeInMs,
        double? fadeOutMs)
    {
        var inText = fadeInMs is { } fi ? $"In={fi:0.###}ms" : "In=-";
        var outText = fadeOutMs is { } fo ? $"Out={fo:0.###}ms" : "Out=-";
        return Format(
            "Message : Clip fade → {0} @ {1}  {2}  {3}",
            "Message : Clip fade → {0} @ {1}  {2}  {3}",
            trackName,
            segmentName,
            inText,
            outText);
    }

    public static string LogMusicClipFadeExceedsWaapi(int count, double maxMs) => Format(
        "Message : Fade Duration が WAAPI 上限（{1:0.#}ms）を超えるクリップが {0} 件あります。WWU 直接編集で本値を設定します。",
        "Message : {0} clip(s) exceed WAAPI Fade Duration limit ({1:0.#}ms). True values will be patched via WWU.",
        count,
        maxMs);

    public static string LogWorkUnitPatchStart(
        int playAtCount,
        int clipFadeCount,
        int transitionFadeCount,
        int playlistPostExitCount,
        int groupStateTransitionCount) => Format(
        "Message : WWU 直接編集を開始します（PlayAt={0} 件 / Clip Fade 超過={1} 件 / Playlist 遷移 Fade={2} 件 / Play post-exit={3} 件 / Group State Transition={4} 件）。保存→クローズ→パッチ→再オープンを行います。",
        "Message : Starting WWU patch (PlayAt={0}, Clip Fade over limit={1}, Playlist transition Fade={2}, Play post-exit={3}, Group State Transition={4}). save → close → patch → reopen.",
        playAtCount,
        clipFadeCount,
        transitionFadeCount,
        playlistPostExitCount,
        groupStateTransitionCount);

    public static string LogMusicClipWorkUnitPatchDone(int count) => Format(
        "Message : MusicClip WWU パッチ完了（{0} クリップ）。",
        "Message : MusicClip WWU patch done ({0} clip(s)).",
        count);

    public static string LogMusicTransitionFadePatchFile(string fileName, int count) => Format(
        "Message : WWU の MusicTransition Fade を直接更新しました → {0}（ルール {1} 件）",
        "Message : Patched MusicTransition fades in work unit → {0} ({1} rule(s))",
        fileName,
        count);

    public static string LogMusicTransitionFadePatchDone(int count) => Format(
        "Message : Playlist 遷移 MusicFade WWU パッチ完了（{0} ルール）。",
        "Message : Playlist transition MusicFade WWU patch done ({0} rule(s)).",
        count);

    public static string LogPlaylistPostExitPatchFile(string fileName, int count) => Format(
        "Message : WWU の Playlist Container 既定ルール（Any to Any）へ Play post-exit を書き込みました → {0}（{1} 件）",
        "Message : Patched Play post-exit on playlist container default rule (Any to Any) → {0} ({1} rule(s))",
        fileName,
        count);

    public static string LogPlaylistPostExitPatchDone(int count) => Format(
        "Message : Playlist Container の Play post-exit WWU パッチ完了（{0} 件）。",
        "Message : Playlist container Play post-exit WWU patch done ({0} rule(s)).",
        count);

    public static string ErrPlaylistAnyToAnyRuleMissing(string containerName, string wwuPath) => Format(
        "WWU 内に Music Playlist Container {0} の既定トランジションルール（Any to Any）が見つかりません（{1}）。プロジェクトの保存に失敗している可能性があります。",
        "The default transition rule (Any to Any) of Music Playlist Container {0} was not found in {1}. The project may not have been saved.",
        containerName,
        wwuPath);

    public static string ErrMusicTransitionWorkUnitNotFound(string name) => Format(
        "MusicTransition の所属 WWU ファイルを特定できませんでした（{0}）",
        "Could not resolve the work unit file for MusicTransition ({0})",
        name);

    public static string ErrMusicTransitionXmlMissing(string name, string wwuPath) => Format(
        "WWU 内に MusicTransition {0} が見つかりません（{1}）。プロジェクトの保存に失敗している可能性があります。",
        "MusicTransition {0} was not found in {1}. The project may not have been saved.",
        name,
        wwuPath);

    public static string ErrMusicTransitionFadeVerifyFailed(
        string transitionName,
        string property,
        bool expected,
        bool? actual) => Format(
        "{0} の検証に失敗しました（transition={1} 期待値={2} 実値={3}）。WWU フォーマットが変更された可能性があります。",
        "{0} verification failed (transition={1} expected={2} actual={3}). The WWU format may have changed.",
        property,
        transitionName,
        expected,
        actual is null ? "(なし)" : actual.Value.ToString());

    public static string ErrMusicTransitionFadeTimeVerifyFailed(
        string transitionName,
        string fadeName,
        double expected,
        double? actual) => Format(
        "MusicFade Time の検証に失敗しました（transition={0} fade={1} 期待値={2:0.###}s 実値={3}）。WWU フォーマットが変更された可能性があります。",
        "MusicFade Time verification failed (transition={0} fade={1} expected={2:0.###}s actual={3}). The WWU format may have changed.",
        transitionName,
        fadeName,
        expected,
        actual is null ? "(なし)" : actual.Value.ToString("0.###") + "s");

    public static string ErrMusicClipFadeVerifyFailed(
        string clipId,
        string property,
        double expected,
        double? actual) => Format(
        "{0} の検証に失敗しました（clip={1} 期待値={2:0.###}ms 実値={3}）。WWU フォーマットが変更された可能性があります。",
        "{0} verification failed (clip={1} expected={2:0.###}ms actual={3}). The WWU format may have changed.",
        property,
        clipId,
        expected,
        actual is null ? "(なし)" : actual.Value.ToString("0.###") + "ms");

    public static string LogPlayAtPatchFile(string fileName, int count) => Format(
        "Message : WWU を直接更新しました → {0}（クリップ {1} 件）",
        "Message : Patched work unit → {0} ({1} clip(s))",
        fileName,
        count);

    public static string LogPlayAtProjectReopen(string projectName) => Format(
        "Message : プロジェクトを再オープンしています → {0}",
        "Message : Reopening project → {0}",
        projectName);

    public static string ErrPlayAtWorkUnitNotFound(string clipId) => Format(
        "MusicClip の所属 WWU ファイルを特定できませんでした（{0}）",
        "Could not resolve the work unit file for MusicClip ({0})",
        clipId);

    public static string ErrPlayAtProjectPathUnknown => Format(
        "プロジェクト（.wproj）のパスを取得できなかったため PlayAt を設定できません",
        "Cannot apply PlayAt because the project (.wproj) path could not be resolved");

    public static string ErrProjectCloseTimeout => Format(
        "Wwise プロジェクトのクローズ完了を待機中にタイムアウトしました。"
        + "プロジェクトが開いたままの可能性があるため、WWU 直接編集を中止しました",
        "Timed out while waiting for the Wwise project to finish closing. "
        + "Direct work-unit editing was aborted because the project may still be open");

    public static string ErrPlayAtClipXmlMissing(string clipId, string wwuPath) => Format(
        "WWU 内に MusicClip {0} が見つかりません（{1}）。プロジェクトの保存に失敗している可能性があります。",
        "MusicClip {0} was not found in {1}. The project may not have been saved.",
        clipId,
        wwuPath);

    public static string ErrPlayAtVerifyFailed(string clipId, double expected, double? actual) => Format(
        "PlayAt の検証に失敗しました（clip={0} 期待値={1:0.###}ms 実値={2}）。WWU フォーマットが変更された可能性があります。",
        "PlayAt verification failed (clip={0} expected={1:0.###}ms actual={2}). The WWU format may have changed.",
        clipId,
        expected,
        actual is null ? "(なし)" : actual.Value.ToString("0.###") + "ms");

    public static string ErrMusicClipNotFound(string trackPath) => Format(
        "MusicClip が見つかりませんでした（2 波形＋トリム構成を維持できないため中止）→ {0}",
        "MusicClip not found (aborting to keep 2-wave + trim workflow) → {0}",
        trackPath);

    public static string ErrMusicClipAmbiguous(string trackPath, int count) => Format(
        "MusicClip が Track に対して {1} 件ヒットしました（トリムの取り違え防止のため中止）→ {0}",
        "Ambiguous MusicClip match count={1} for track (aborting) → {0}",
        trackPath,
        count);

    public static string ErrMusicClipTrimMissingRate(string trackName, string segmentName) => Format(
        "MusicClip トリムに必要な SampleRate/FrameCount がありません → {0} @ {1}",
        "Missing SampleRate/FrameCount for MusicClip trim → {0} @ {1}",
        trackName,
        segmentName);

    public static string LogCheckingStateGroup => Get(
        "Checking State Group...",
        "Checking State Group...");

    public static string LogStateGroupExistingFound => Get(
        "Existing State Group found.",
        "Existing State Group found.");

    public static string LogStateGroupAvailable => Get(
        "State Group is available.",
        "State Group is available.");

    public static string LogPlaylistSummary(string name, int segmentCount) => Format(
        "--- Playlist: {0} ({1} segments) ---",
        "--- Playlist: {0} ({1} segments) ---",
        name,
        segmentCount);

    public static string LogWavSliceWritten(string fileName) => Format(
        "WAV: {0}",
        "WAV: {0}",
        fileName);

    public static string LogWavSourceReused(string fileName) => Format(
        "WAV: {0}（元ファイルを出力先へコピーして使用）",
        "WAV: {0} (copied original to output)",
        fileName);

    public static string LogXmlPresence(string path, bool present) => Format(
        "Xml  : {0} ({1})",
        "Xml  : {0} ({1})",
        path,
        present ? PresentYes : PresentNo);

    public static string LogPeaksSummary(int bucketCount, long frameCount) => Format(
        "{0} {1} buckets / {2:N0} frames",
        "{0} {1} buckets / {2:N0} frames",
        KeyPeaks,
        bucketCount,
        frameCount);

    public static string LogLoudnessGroupBalanceOn => Get(
        "Layer Music Option: Keep Layer Balance ON → Make-Up Gain (Music Track)",
        "Layer Music Option: Keep Layer Balance ON → Make-Up Gain (Music Track)");

    public static string LabelMusicSwitchContainer => Get("Music Switch Container", "Music Switch Container");
    public static string LabelMusicPlaylistContainer => Get("Music Playlist Container", "Music Playlist Container");

    public static string LogLoudnessPartSilence(int partNumber) => Format(
        "Layer Music Option: part {0} = (silence)",
        "Layer Music Option: part {0} = (silence)",
        partNumber);

    public static string LogLoudnessPartValue(int partNumber, double lkfs) => Format(
        "Layer Music Option: part {0} = {1:0.00} LKFS",
        "Layer Music Option: part {0} = {1:0.00} LKFS",
        partNumber,
        lkfs);

    public static string LogLoudnessPartGain(int partNumber, float makeUpGainDb) => Format(
        "Layer Music Option: part {0} → Make-Up Gain {1:0.##} dB",
        "Layer Music Option: part {0} → Make-Up Gain {1:0.##} dB",
        partNumber,
        makeUpGainDb);

    public static string LogLoudnessMakeUpGainApplied(string objectName, float makeUpGainDb) => Format(
        "Layer Music Option: {0} → Make-Up Gain {1:0.##} dB",
        "Layer Music Option: {0} → Make-Up Gain {1:0.##} dB",
        objectName,
        makeUpGainDb);

    public static string LogLoudnessGroupSilence(int groupId) => Format(
        "Layer Music Option: group {0} peak = (silence)",
        "Layer Music Option: group {0} peak = (silence)",
        groupId);

    public static string LogLoudnessGroupValue(int groupId, double maxLkfs) => Format(
        "Layer Music Option: group {0} peak = {1:0.00} LKFS (Make-Up Gain 0 dB)",
        "Layer Music Option: group {0} peak = {1:0.00} LKFS (Make-Up Gain 0 dB)",
        groupId,
        maxLkfs);

    // WavFileInfo report（元から Yes / No）
    public static string BoolYes => Get("Yes", "Yes");
    public static string BoolNo => Get("No", "No");

    public static string LabelWavPath => Get("Path           :", "Path           :");
    public static string LabelFileSize => Get("File Size      :", "File Size      :");
    public static string LabelFormat => Get("Format         :", "Format         :");
    public static string LabelChannels => Get("Channels       :", "Channels       :");
    public static string LabelSampleRate => Get("Sample Rate    :", "Sample Rate    :");
    public static string LabelBitDepth => Get("Bit Depth      :", "Bit Depth      :");
    public static string LabelWaveFormatCompact => Get("Wave Format    :", "Wave Format    :");
    public static string LabelBlockAlign => Get("Block Align    :", "Block Align    :");
    public static string LabelByteRate => Get("Byte Rate      :", "Byte Rate      :");
    public static string LabelDataSize => Get("Data Size      :", "Data Size      :");
    public static string LabelFrames => Get("Frames         :", "Frames         :");
    public static string LabelDuration => Get("Duration       :", "Duration       :");
    public static string LabelIXml => Get("iXML           :", "iXML           :");
    public static string LabelTimeReference => Get("Time Reference :", "Time Reference :");

    /// <summary>WAV フォーマット（AudioFormat コード）の表示名。技術用語のため日英共通。</summary>
    public static string AudioFormatName(ushort audioFormat) => audioFormat switch
    {
        1 => "PCM",
        3 => "IEEE Float",
        6 => "A-law",
        7 => "μ-law",
        65534 => "Extensible",
        _ => Format("不明 ({0})", "Unknown ({0})", audioFormat),
    };

    // NuendoTracklistInfo report
    public static string LabelNuendoPath => Get("Path            :", "Path            :");
    public static string LabelRehearsalTempo => Get("Rehearsal Tempo :", "Rehearsal Tempo :");
    public static string LabelPpqResolution => Get("PPQ Resolution  :", "PPQ Resolution  :");
    public static string LabelTempoEvents => Get("Tempo Events    :", "Tempo Events    :");
    public static string LabelSignatures => Get("Signatures      :", "Signatures      :");
    public static string LabelMarkers => Get("Markers         :", "Markers         :");
    public static string LabelRegionKind => Get("Region", "Region");
    public static string LabelMarkerKind => Get("Marker", "Marker");

    public static string LabelPpqResolutionValue(double pulsesPerQuarterNote) => Format(
        "{0:0} パルス / 四分音符",
        "{0:0} pulses / quarter note",
        pulsesPerQuarterNote);

}
