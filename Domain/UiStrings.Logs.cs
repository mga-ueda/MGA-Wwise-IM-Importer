namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Logs (user-facing) ---
    public static string LogKeepTargetNeedSelection => Get(
        "Keep Target : 作成先が表示されていないためオンにできません。"
        + " Wwise で作成先を選んでから再度オンにしてください。",
        "Keep Target : cannot enable because no target is shown."
        + " Select a target in Wwise, then enable again.");

    public static string LogKeepTargetOff => Get(
        "Keep Target : OFF（Wwise の選択に追従します）",
        "Keep Target : OFF (follows Wwise selection)");

    public static string LogKeepTargetOn(string path) => Format(
        "Keep Target : ON（このパスへ書き出します → {0}）",
        "Keep Target : ON (export to → {0})",
        path);

    public static string LogProjectCreated(string name) => Format(
        "=== Project ==={0}Message : プロジェクト「{1}」を作成しました（アプリ既定）。{0}{0}",
        "=== Project ==={0}Message : Created project “{1}” (app defaults).{0}{0}",
        Environment.NewLine,
        name);

    public static string LogProjectDeleted(string name) => Format(
        "=== Project ==={0}Message : プロジェクト「{1}」を削除しました。{0}{0}",
        "=== Project ==={0}Message : Deleted project “{1}”.{0}{0}",
        Environment.NewLine,
        name);

    public static string LogProjectCleared(string name) => Format(
        "=== Project ==={0}Message : プロジェクト「{1}」をクリアしました（アプリ既定）。{0}{0}",
        "=== Project ==={0}Message : Cleared project “{1}” (app defaults).{0}{0}",
        Environment.NewLine,
        name);

    public static string LogProjectSwitched(string fromName, string toName) => Format(
        "=== Project ==={0}Message : プロジェクト「{1}」から「{2}」に切り替えました。{0}{0}",
        "=== Project ==={0}Message : Switched project from “{1}” to “{2}”.{0}{0}",
        Environment.NewLine,
        fromName,
        toName);

    public static string DialogDeleteProjectFailedTitle => Get(
        "プロジェクトの削除に失敗",
        "Failed to delete project");

    public static string LogExportPreflightHeader => Get("=== Export Preflight ===", "=== Export Preflight ===");
    public static string LogStatusOk => Get("OK", "OK");
    public static string LogStatusNg => Get("NG", "NG");
    public static string LogTargetUnselected => Get("（未選択）", "(none selected)");

    public static string PreflightNoParts => Get(
        "有効な出力パートがありません。",
        "No enabled output parts.");

    public static string PreflightNoOutputDir => Get(
        "書き出し先が未指定です。プロジェクト設定でフォルダを選択してください。",
        "Export folder is not set. Choose a folder in project settings.");

    public static string PreflightBadOutputPath(string message) => Format(
        "書き出し先パスが不正です: {0}",
        "Invalid export path: {0}",
        message);

    public static string PreflightOutputMissing => Get(
        "書き出し先フォルダが存在しません。",
        "Export folder does not exist.");

    public static string PreflightWaapiDisconnected => Get(
        "Wwise に接続されていません。WAAPI 有効化と Wwise の起動を確認してください。",
        "Not connected to Wwise. Enable WAAPI and ensure Wwise is running.");

    public static string PreflightKeepTargetNoPath => Get(
        "Keep Target がオンですが作成先パスが未設定です。"
        + " Wwise で作成先を選んでから Keep Target をオンにしてください。",
        "Keep Target is on but no target path is set."
        + " Select a target in Wwise, then enable Keep Target.");

    public static string PreflightNoSelection => Get(
        "Wwise 上で作成先オブジェクトが選択されていません。",
        "No destination object is selected in Wwise.");

    public static string PreflightNoProjectPath => Get(
        "Wwise プロジェクトのパスを取得できません。プロジェクトを開いているか確認してください。",
        "Cannot get the Wwise project path. Ensure a project is open.");

    public static string PreflightNoProjectRoot => Get(
        "Wwise プロジェクトのルートを解決できません。",
        "Cannot resolve the Wwise project root.");

    public static string PreflightOriginalsResolveFailed(string message) => Format(
        "Originals パスの解決に失敗: {0}",
        "Failed to resolve Originals path: {0}",
        message);

    public static string PreflightNotUnderOriginals => Get(
        "書き出し先は接続中 Wwise プロジェクトの Originals 配下である必要があります。",
        "Export folder must be under the connected Wwise project’s Originals.");

    public static string PreflightOkKeepTarget(string path) => Format(
        "書き出し可能です（Keep Target → {0} へ作成します）。",
        "Ready to export (Keep Target → create under {0}).",
        path);

    public static string PreflightOk => Get(
        "書き出し可能です。",
        "Ready to export.");

    // --- Wwise import progress (common lines) ---
    public static string LogWwiseImportHeader => Get("=== Wwise Import ===", "=== Wwise Import ===");
    public static string LogWwiseImportComplete => Get(
        "=== Wwise Import complete ===",
        "=== Wwise Import complete ===");

    public static string LogWwiseObjectsCreated => Get(
        "Wwise objects created.",
        "Wwise objects created.");

    public static string LogStateGroupUpdateExisting => Get(
        "StateGrp : 既存オブジェクトを変更",
        "StateGrp : updating existing object");

    public static string LogStateGroupCreateNew => Get(
        "StateGrp : 新規作成",
        "StateGrp : creating new");

    public static string LogCreatingStateGroup => Get(
        "Creating State Group...",
        "Creating State Group...");

    public static string LogCreatingGroupStateGroup(
        string groupName,
        string stateNames,
        string fadeSummary,
        bool useDefaultOnly,
        double defaultTransitionSeconds) => Format(
        useDefaultOnly
            ? "Creating Group State Group: {0} [{1}]  fades=[{2}]  Default only={3:0.###}s (no Custom TransitionList)"
            : "Creating Group State Group: {0} [{1}]  fades=[{2}]  Custom TransitionList  Default=Wwise {3:0.###}s",
        useDefaultOnly
            ? "Creating Group State Group: {0} [{1}]  fades=[{2}]  Default only={3:0.###}s (no Custom TransitionList)"
            : "Creating Group State Group: {0} [{1}]  fades=[{2}]  Custom TransitionList  Default=Wwise {3:0.###}s",
        groupName,
        stateNames,
        fadeSummary,
        defaultTransitionSeconds);

    public static string LogAssignGroupStateToTrack(
        string trackName,
        string segmentName,
        string groupName) => Format(
        "Assigning State Group '{2}' to Music Track '{0}' (Segment '{1}')...",
        "Assigning State Group '{2}' to Music Track '{0}' (Segment '{1}')...",
        trackName,
        segmentName,
        groupName);

    public static string LogGroupStateSetInitial(string groupName, string stateName) => Format(
        "Message : Group State '{0}' の現在値を '{1}' に設定しました（プレビュー用）。",
        "Message : Set Group State '{0}' current value to '{1}' (for preview).",
        groupName,
        stateName);

    public static string LogGroupStateSetInitialFailed(
        string groupName,
        string stateName,
        string message) => Format(
        "Message : Group State '{0}' を '{1}' に設定できませんでした（続行）: {2}",
        "Message : Could not set Group State '{0}' to '{1}' (continuing): {2}",
        groupName,
        stateName,
        message);

    public static string LogImportedObjectSelected(string objectKind, string path) => Format(
        "Message : 転送オブジェクトを選択しました（{0}）→ {1}",
        "Message : Selected imported object ({0}) → {1}",
        objectKind,
        path);

    public static string LogImportedObjectSelectFailed(string path, string message) => Format(
        "Message : 転送オブジェクトを選択できませんでした（続行）: {0} / {1}",
        "Message : Could not select imported object (continuing): {0} / {1}",
        path,
        message);

    public static string LogGroupStateTrackVolumePlan(
        string trackName,
        string activeState,
        double muteDb,
        bool additiveLayers = false) => Format(
        additiveLayers
            ? "Group State Volume (Additive): Track '{0}'  unmuted from {1} onward / muted={2:0.###}dB"
            : "Group State Volume: Track '{0}'  {1}=0dB / others={2:0.###}dB",
        additiveLayers
            ? "Group State Volume (Additive): Track '{0}'  unmuted from {1} onward / muted={2:0.###}dB"
            : "Group State Volume: Track '{0}'  {1}=0dB / others={2:0.###}dB",
        trackName,
        activeState,
        muteDb);

    public static string LogGroupStateSummary(
        string groupName,
        string stateNames,
        string fadeSummary,
        bool useDefaultOnly,
        double defaultTransitionSeconds) => Format(
        useDefaultOnly
            ? "  Group State: {0} [{1}]  Default only={3:0.###}s  fades=[{2}]"
            : "  Group State: {0} [{1}]  Custom fades=[{2}]  Default=Wwise {3:0.###}s",
        useDefaultOnly
            ? "  Group State: {0} [{1}]  Default only={3:0.###}s  fades=[{2}]"
            : "  Group State: {0} [{1}]  Custom fades=[{2}]  Default=Wwise {3:0.###}s",
        groupName,
        stateNames,
        fadeSummary,
        defaultTransitionSeconds);

    public static string LogGroupStateTransitionClearFile(string fileName, int clearedCount) => Format(
        "Message : WWU の State Group Custom TransitionList をクリアしました（Default のみ）→ {0}（{1} 件）",
        "Message : Cleared State Group Custom TransitionList (Default only) in work unit → {0} ({1})",
        fileName,
        clearedCount);

    public static string LogGroupStateTransitionPatchFile(string fileName, int ruleCount) => Format(
        "Message : WWU の State Group Custom Transition Time を更新しました → {0}（{1} ルール）",
        "Message : Patched State Group Custom Transition Times in work unit → {0} ({1} rule(s))",
        fileName,
        ruleCount);

    public static string LogGroupStateTransitionPatchDone(int groupCount) => Format(
        "Message : Group State TransitionList パッチ完了（{0} State Group）。",
        "Message : Group State TransitionList patch done ({0} State Group(s)).",
        groupCount);

    public static string LogGroupStateVolumePatchFile(string fileName, int trackCount) => Format(
        "Message : WWU の Music Track State Volume を直接更新しました → {0}（Track {1} 件）",
        "Message : Patched Music Track State Volume in work unit → {0} ({1} track(s))",
        fileName,
        trackCount);

    public static string LogGroupStateVolumePatchDone(int trackCount) => Format(
        "Message : Group State Volume WWU パッチ完了（{0} Track）。",
        "Message : Group State Volume WWU patch done ({0} track(s)).",
        trackCount);

    public static string ErrGroupStateWorkUnitNotFound(string stateGroupPath) => Format(
        "グループ State Group の Work Unit が見つかりません: {0}",
        "Group State Group work unit not found: {0}",
        stateGroupPath);

    public static string ErrGroupStateTrackWorkUnitNotFound(string trackPath) => Format(
        "Music Track の Work Unit が見つかりません: {0}",
        "Music Track work unit not found: {0}",
        trackPath);

    public static string ErrGroupStateTrackActiveMissing(
        string trackName,
        string segmentName,
        string activeState) => Format(
        "Music Track '{0}' (Segment '{1}') のレイヤー State '{2}' が不正です。",
        "Music Track '{0}' (Segment '{1}') has invalid layer State '{2}'.",
        trackName,
        segmentName,
        activeState);

    public static string ErrGroupStateMissing(string stateGroupPath, string stateName) => Format(
        "グループ State Group '{0}' に State '{1}' がありません。",
        "State '{1}' is missing from Group State Group '{0}'.",
        stateGroupPath,
        stateName);

    public static string ErrGroupStateXmlMissing(string stateGroupName, string wwuPath) => Format(
        "WWU 内に State Group '{0}' が見つかりません: {1}",
        "State Group '{0}' not found in work unit: {1}",
        stateGroupName,
        wwuPath);

    public static string ErrGroupStateTrackXmlMissing(
        string trackName,
        string trackId,
        string wwuPath) => Format(
        "WWU 内に Music Track '{0}' ({1}) が見つかりません: {2}",
        "Music Track '{0}' ({1}) not found in work unit: {2}",
        trackName,
        trackId,
        wwuPath);

    public static string ErrGroupStateTransitionVerifyFailed(
        string stateGroupName,
        int expected,
        int actual) => Format(
        "State Group '{0}' の TransitionList 件数が不一致です（expected={1}, actual={2}）。",
        "State Group '{0}' TransitionList count mismatch (expected={1}, actual={2}).",
        stateGroupName,
        expected,
        actual);

    public static string ErrGroupStateTransitionRuleMissing(
        string stateGroupName,
        string fromName,
        string toName) => Format(
        "State Group '{0}' に Transition ルール {1} → {2} がありません。",
        "State Group '{0}' is missing transition rule {1} → {2}.",
        stateGroupName,
        fromName,
        toName);

    public static string ErrGroupStateTransitionTimeVerifyFailed(
        string stateGroupName,
        string fromName,
        string toName,
        double expected,
        string? actual) => Format(
        "State Group '{0}' の Transition {1} → {2} の Time が不一致です（expected={3:0.###}, actual={4}）。",
        "State Group '{0}' transition {1} → {2} Time mismatch (expected={3:0.###}, actual={4}).",
        stateGroupName,
        fromName,
        toName,
        expected,
        actual ?? "(null)");

    public static string ErrGroupStateVolumeVerifyFailed(
        string trackName,
        string stateName,
        double expectedDb,
        string actual) => Format(
        "Music Track '{0}' の State '{1}' Volume が不一致です（expected={2:0.###}dB, actual={3}）。",
        "Music Track '{0}' State '{1}' Volume mismatch (expected={2:0.###}dB, actual={3}).",
        trackName,
        stateName,
        expectedDb,
        actual);

    public static string ErrGroupStateMusicSyncTypeVerifyFailed(
        string trackName,
        string stateGroupName,
        int expected,
        int? actual) => Format(
        "Music Track '{0}' の State Group '{1}' Change Occurs At (MusicSyncType) が不一致です（expected={2}, actual={3}）。",
        "Music Track '{0}' State Group '{1}' Change Occurs At (MusicSyncType) mismatch (expected={2}, actual={3}).",
        trackName,
        stateGroupName,
        expected,
        actual?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(null)");

    public static string LogCreatingMusicSwitch => Get(
        "Creating Music Switch Container...",
        "Creating Music Switch Container...");

    public static string LogCreatingPlaylist(int index, int total, string name) => Format(
        "Creating playlist {0}/{1}: {2}...",
        "Creating playlist {0}/{1}: {2}...",
        index,
        total,
        name);

    public static string LogBindingStates => Get(
        "Binding States to Playlists...",
        "Binding States to Playlists...");

    public static string LogConfiguringTransitions => Get(
        "Configuring transitions...",
        "Configuring transitions...");

    public static string LogCreatingWwiseObjects => Get(
        "Creating Wwise objects...",
        "Creating Wwise objects...");

    public static string LogTransitionAnyToPlaylist(
        string name,
        string exitSourceAt,
        double fadeInSeconds,
        double fadeOutSeconds) => Format(
        "Transition : Any → {0} / Exit Source at={1} / Destination Sync To=Entry Cue / Fade-in={2} / Fade-out={3}",
        "Transition : Any → {0} / Exit Source at={1} / Destination Sync To=Entry Cue / Fade-in={2} / Fade-out={3}",
        name,
        exitSourceAt,
        FormatFadeSecondsLog(fadeInSeconds),
        FormatFadeSecondsLog(fadeOutSeconds));

    private static string FormatFadeSecondsLog(double seconds) =>
        seconds > 0
            ? string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:0.#}s",
                seconds)
            : "None";

    public static string LogTransitionDestinationSet(string name) => Format(
        "Transition : Any → {0} の Destination を設定",
        "Transition : set Destination for Any → {0}",
        name);

    // --- WAAPI / Keep Target logs ---
    public static string LogTargetKeepOn(string path) => Format(
        "Target  : Keep → {0}（このパスへ書き出します）",
        "Target  : Keep → {0} (export to this path)",
        path);

    public static string LogTargetKeepUnset => Get(
        "Target  : Keep → （未設定）",
        "Target  : Keep → (not set)");

    public static string LogTargetNoneSelected => Get(
        "Target  : （未選択）",
        "Target  : (none selected)");

    public static string LogKeepTargetPathUnset => Get(
        "Keep Target : 作成先パスが未設定です。",
        "Keep Target : destination path is not set.");

    public static string LogKeepTargetReselected(string path) => Format(
        "Keep Target : Wwise 上でも作成先を合わせました → {0}",
        "Keep Target : also reselected destination in Wwise → {0}",
        path);

    public static string LogKeepTargetExportPath(string path) => Format(
        "Keep Target : EXPORT はこのパスへ書き出します → {0}",
        "Keep Target : EXPORT will write to → {0}",
        path);

    public static string LogKeepTargetExportRegardless(string path) => Format(
        "Keep Target : Wwise 上の選択に関わらず、EXPORT はこのパスへ書き出します → {0}",
        "Keep Target : EXPORT will write to this path regardless of Wwise selection → {0}",
        path);

    public static string LogWaapiConnectFailed => Get(
        "接続できません。Wwise 起動と WAAPI 有効化を確認してください。",
        "Cannot connect. Ensure Wwise is running and WAAPI is enabled.");

    public static string LogWwiseProjectPathMissing => Get(
        "Keep Target : ロック中 Wwise プロジェクトのパスがありません。",
        "Keep Target : locked Wwise project path is missing.");

    public static string LogWwiseProjectFileMissing(string path) => Format(
        "Keep Target : Wwise プロジェクトファイルが見つかりません → {0}",
        "Keep Target : Wwise project file not found → {0}",
        path);

    public static string LogWwiseProjectBroughtToFront(string projectName) => Format(
        "Keep Target : Wwise を前面に表示しました → {0}",
        "Keep Target : brought Wwise to the front → {0}",
        projectName);

    public static string LogWwiseBroughtToFront => Get(
        "Message : Wwise を前面にしました。",
        "Message : Brought Wwise to the foreground.");

    public static string LogWwiseBringToFrontFailed(string detail) => Format(
        "Message : Wwise の前面化に失敗しました: {0}",
        "Message : Failed to bring Wwise to the foreground: {0}",
        detail);

    public static string LogWwiseProjectOpened(string projectName) => Format(
        "Keep Target : Wwise プロジェクトを開きました → {0}",
        "Keep Target : opened Wwise project → {0}",
        projectName);

    public static string LogWwiseProjectOpenRequestFailed(string detail) => Format(
        "Keep Target : Wwise への WAAPI 呼び出しに失敗しました（起動済みのため二重起動は行いません）: {0}",
        "Keep Target : WAAPI call to Wwise failed (skipped launching another instance because Wwise is already running): {0}",
        detail);

    public static string LogWwiseProjectShellOpen(string projectName) => Format(
        "Keep Target : Wwise プロジェクトを起動しました → {0}",
        "Keep Target : launched Wwise project → {0}",
        projectName);

    public static string LogWwiseProjectOpenFailed(string message) => Format(
        "Keep Target : Wwise プロジェクトを開けませんでした → {0}",
        "Keep Target : failed to open Wwise project → {0}",
        message);

    public static string LogWaapiTimeout => Get(
        "タイムアウト。Wwise の起動と WAAPI（HTTP）有効化を確認してください。",
        "Timed out. Ensure Wwise is running and WAAPI (HTTP) is enabled.");

    public static string LogKeepTargetMemoryEmpty => Get(
        "Keep Target がオンですが、記憶パスが空です。",
        "Keep Target is on but the remembered path is empty.");

    public static string LogKeepTargetOtherProject => Get(
        "Keep Target の記憶パスは別プロジェクト向けのため再選択しませんでした。",
        "Keep Target path belongs to another project; did not reselect.");

    public static string LogKeepTargetReselectOk(string path) => Format(
        "Keep Target : 再選択しました → {0}",
        "Keep Target : reselected → {0}",
        path);

    public static string LogKeepTargetObjectMissing(string path) => Format(
        "Keep Target : オブジェクトが見つかりません → {0}",
        "Keep Target : object not found → {0}",
        path);

    public static string LogKeepTargetReselectFailed(string message) => Format(
        "Keep Target : 再選択に失敗 → {0}",
        "Keep Target : failed to reselect → {0}",
        message);

    public static string LogPlaylistScheduleFailed(string fileName) => Format(
        "Playlist 遷移を予約できませんでした: {0}",
        "Could not schedule playlist transition: {0}",
        fileName);

    public static string LogSameTimeOutOfRange(string fileName, long sample, long duration) => Format(
        "Same Time の遷移位置が遷移先の範囲外です: {0} (位置={1}, 長さ={2})",
        "Same Time transition position is outside the destination range: {0} (pos={1}, len={2})",
        fileName,
        sample,
        duration);

    public static string LogLastWaveBadPath(string message) => Format(
        "前回読み込んだ波形のパスが不正です: {0}",
        "Last loaded wave path is invalid: {0}",
        message);

    public static string LogLastWaveMissing(string path) => Format(
        "前回読み込んだ波形が見つかりません: {0}",
        "Last loaded wave was not found: {0}",
        path);

    public static string LogPlaybackPrepareFailed(string message) => Format(
        "=== エラー ==={0}Message : 再生の準備に失敗: {1}{0}{0}",
        "=== Error ==={0}Message : Failed to prepare playback: {1}{0}{0}",
        Environment.NewLine,
        message);

    public static string LogExportReady(int partCount) => Format(
        "Message : 出力パート {0} 件。［EXPORT］で元 WAV を Originals へコピーし、Wwise へ登録できます。",
        "Message : {0} output part(s). Use [EXPORT] to copy the source WAV(s) into Originals and register in Wwise.",
        partCount);

    public static string LogExportBlocked(int partCount, string reason) => Format(
        "Message : 出力パート {0} 件。書き出し条件未達: {1}",
        "Message : {0} output part(s). Export requirements not met: {1}",
        partCount,
        reason);

    public static string LogExportSaveTo(string directory) => Format(
        "保存先  : {0}",
        "Output  : {0}",
        directory);

    public static string LogLastSessionCorrupt => Get(
        "Message : 前回セッションの読み込みに失敗しました（形式不正）。",
        "Message : Failed to load the last session (invalid format).");

    public static string LogManualDropSessionDiscarded => Get(
        "Message : 手動ドロップのため前回セッションは復元せず、新規の作業として開始しました。",
        "Message : Manual drop detected; starting fresh without restoring the previous session.");

    public static string LogLastSessionPartMismatch => Get(
        "Message : 前回セッションはパート構成が一致しないため復元しませんでした。",
        "Message : Last session was not restored because the part layout does not match.");

    public static string LogLastSessionPartial(
        int groupApplied,
        int groupRequested,
        int disabledApplied,
        int disabledRequested,
        int markerApplied,
        int markerRequested,
        int exitApplied,
        int exitRequested,
        int fadeInApplied,
        int fadeInRequested,
        int fadeOutApplied,
        int fadeOutRequested,
        int groupFadeApplied,
        int groupFadeRequested) => Format(
        "Message : 前回セッションを部分復元: グループ {0}/{1}、無効 {2}/{3}、マーカー {4}/{5}、"
        + "Exit Source {6}/{7}、Fade In {8}/{9}、Fade Out {10}/{11}、Group Fade {12}/{13}",
        "Message : Partially restored last session: groups {0}/{1}, disabled {2}/{3}, markers {4}/{5}, "
        + "Exit Source {6}/{7}, Fade In {8}/{9}, Fade Out {10}/{11}, Group Fade {12}/{13}",
        groupApplied,
        groupRequested,
        disabledApplied,
        disabledRequested,
        markerApplied,
        markerRequested,
        exitApplied,
        exitRequested,
        fadeInApplied,
        fadeInRequested,
        fadeOutApplied,
        fadeOutRequested,
        groupFadeApplied,
        groupFadeRequested);

    public static string PresentYes => Get("あり", "present");
    public static string PresentNo => Get("なし", "missing");

    public static string LogWaapiStateFailed(string message) => Format(
        "Message : Wwise 状態の取得に失敗: {0}",
        "Message : Failed to get Wwise state: {0}",
        message);

    public static string LogImportSkippedNoSelection => Get(
        "Message : Wwise 上で作成先オブジェクトが選択されていないためスキップしました。",
        "Message : Skipped because no destination object is selected in Wwise.");

    public static string LogImportPlanFailed(string message) => Format(
        "Message : インポート計画の作成に失敗: {0}",
        "Message : Failed to build the import plan: {0}",
        message);

    public static string LogStateGroupCheckFailed(string message) => Format(
        "Message : State Group の存在確認に失敗: {0}",
        "Message : Failed to check State Group: {0}",
        message);

    public static string LogBarNotFound(int barNumber) => Format(
        "Message : 小節 {0} が見つかりません。",
        "Message : Bar {0} was not found.",
        barNumber);

}
