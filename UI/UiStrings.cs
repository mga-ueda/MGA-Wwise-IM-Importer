namespace MgaWwiseIMImporter.UI;

/// <summary>
/// ユーザーに見えるすべての表示テキスト（Tips・ダイアログ・ログ・ラベル・ボタン・
/// アクセシビリティ名・開発者パネルなど）を一箇所に集約する。画面の固定ラベルも例外ではない。
/// 新しい表示テキストを追加するときは、必ずこのファイルへプロパティ／メソッドを追加してから参照する。
/// </summary>
internal static class UiStrings
{
    public static UiLanguage Language { get; private set; } = UiLanguage.Japanese;

    public static event EventHandler? LanguageChanged;

    public static bool IsJapanese => Language == UiLanguage.Japanese;

    public static void SetLanguage(UiLanguage language)
    {
        if (Language == language)
        {
            return;
        }

        Language = language;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static UiLanguage ParseLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return UiLanguage.Japanese;
        }

        var trimmed = value.Trim();
        if (trimmed.Equals("en", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("english", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(nameof(UiLanguage.English), StringComparison.OrdinalIgnoreCase))
        {
            return UiLanguage.English;
        }

        return UiLanguage.Japanese;
    }

    public static string ToIniValue(UiLanguage language) =>
        language == UiLanguage.English ? "en" : "ja";

    public static string Get(string japanese, string english) =>
        IsJapanese ? japanese : english;

    public static string Format(string japaneseFormat, string englishFormat, params object[] args) =>
        string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            Get(japaneseFormat, englishFormat),
            args);

    // --- Language toggle ---
    public static string TipLanguageJapanese => Get(
        "現在: 日本語。クリックで English に切り替えます。",
        "Current: Japanese. Click to switch to English.");

    public static string TipLanguageEnglish => Get(
        "現在: English。クリックで日本語に切り替えます。",
        "Current: English. Click to switch to Japanese.");

    public static string TipAudioSettings => Get(
        "音声出力とフェードカーブの既定値を設定します。",
        "Configure audio output and default fade curves.");

    public static string AccessibleAudioSettingsButton => Get(
        "設定",
        "Settings");

    public static string DialogSettingsTitle => Get(
        "設定",
        "Settings");

    public static string LabelFadeCurveDefaults => Get(
        "フェードカーブ既定",
        "Default Fade Curves");

    public static string LabelDefaultWaveformFadeIn => Get(
        "波形フェードイン",
        "Waveform Fade In");

    public static string LabelDefaultWaveformFadeOut => Get(
        "波形フェードアウト",
        "Waveform Fade Out");

    public static string LabelDefaultPlaylistFadeIn => Get(
        "プレイリスト遷移フェードイン",
        "Playlist Transition Fade In");

    public static string LabelDefaultPlaylistFadeOut => Get(
        "プレイリスト遷移フェードアウト",
        "Playlist Transition Fade Out");

    public static string LabelTips => Get(
        "Tips",
        "Tips");

    public static string LabelLog => Get(
        "Log",
        "Log");

    public static string TipTipsToggle => Get(
        "Tips 枠の表示をオン／オフします。",
        "Turn the Tips panel on or off.");

    public static string AccessibleTipsToggleButton => Get(
        "Tips 表示切替",
        "Toggle Tips");

    public static string TipManualHelp => Get(
        "ユーザーマニュアル（GitHub Pages）をブラウザで開きます（表示言語に合わせて日本語／英語）。",
        "Open the user manual on GitHub Pages in your browser (Japanese or English matching the UI language).");

    public static string AccessibleManualHelpButton => Get(
        "マニュアル",
        "Manual");

    public static string DialogManualTitle => Get(
        "マニュアル",
        "Manual");

    public static string ErrManualOpenFailed(string detail) => Format(
        "マニュアルを開けませんでした。\n{0}",
        "Could not open the manual.\n{0}",
        detail);

    // --- Audio settings dialog ---
    public static string LabelAudioApi => Get(
        "Output API",
        "Output API");

    public static string LabelAudioDevice => Get(
        "Output Device",
        "Output Device");

    public static string LabelAudioApiWaveOut => Get("WaveOut", "WaveOut");
    public static string LabelAudioApiWasapi => Get("WASAPI", "WASAPI");
    public static string LabelAudioApiAsio => Get("ASIO", "ASIO");

    public static string ButtonAudioSettingsOk => Get("OK", "OK");
    public static string ButtonAudioSettingsCancel => Get("CANCEL", "CANCEL");

    public static string ErrAudioOutputApplyFailed(string detail) => Format(
        "出力設定の適用に失敗しました。\n{0}",
        "Failed to apply audio output settings.\n{0}",
        detail);

    // --- Action bar Tips ---
    public static string TipDebugLog => Get(
        "重ね再生／シークバーのアクションログ（AI 解析用 JSON）を出します。",
        "Emit layered-playback / seekbar action logs (AI-oriented JSON).");

    public static string TipCompactFileNumbers => Get(
        "ON: 無効化した Playlist があっても、書き出す WAV の番号を 1 から詰めます。"
        + Environment.NewLine
        + "OFF: 元の番号を維持します（欠番が残ります）。",
        "ON: Renumber exported WAV files from 1, skipping disabled playlists."
        + Environment.NewLine
        + "OFF: Keep original numbers (gaps remain).");

    public static string TipKeepLastSession => Get(
        "起動時およびこのプロジェクトへ戻ったときに、最後の作業セッション（波形・グループ／無効化／追加マーカー／Fade・Exit Source At）を復元します（プロジェクト設定・既定オン）。",
        "On startup and when returning to this project, restore the last session (wave, groups, disables, markers, Fade / Exit Source At). Project setting (default on).");

    public static string TipAlwaysOnTop => Get(
        "ウィンドウを常に最前面へ表示します（アプリ設定）。",
        "Keep the window always on top (app setting).");

    public static string TipClear => Get(
        "波形・セッション・ログをクリアし、選択中プロジェクトの設定をアプリ既定へ戻します。"
        + Environment.NewLine
        + "書き出し先フォルダ・WAAPI Keep Target・Always on Top は変わりません。"
        + " プロジェクト自体は削除しません。",
        "Clear wave, session, and log, and reset the active project settings to app defaults."
        + Environment.NewLine
        + "Export folder, WAAPI Keep Target, and Always on Top are unchanged."
        + " The project itself is not deleted.");

    public static string TipReload => Get(
        "最後にドロップまたは自動読み込みした WAV／XML を、元のファイルから再読み込みします。"
        + Environment.NewLine
        + "ログは消去します。サイドカー JSON があれば、グループ／無効化／追加マーカー／Fade・Exit Source At を復元します。",
        "Reload the last dropped or auto-loaded WAV/XML from the original files."
        + Environment.NewLine
        + "The log is cleared. If a sidecar JSON exists, grouping, disables, added markers, and Fade / Exit Source At are restored.");

    public static string TipExport => Get(
        "[Ctrl+Shift+E] 分割 WAV を書き出し、続けて Wwise へインポートします。"
        + Environment.NewLine
        + "無効化した Playlist は書き出し対象外です。",
        "[Ctrl+Shift+E] Export split WAVs and import them into Wwise."
        + Environment.NewLine
        + "Disabled playlists are excluded.");

    public static string TipProjectFolder => Get(
        "波形の書き出し先フォルダを選択します（接続中 Wwise プロジェクトの Originals 配下）。",
        "Choose the export folder (must be under the connected Wwise project's Originals).");

    public static string TipProjectDelete => Get(
        "[Del] 選択中のプロジェクトを削除します。",
        "[Del] Delete the selected project.");

    public static string TipProjectName => Get(
        "プロジェクト名の選択と編集。末尾の「+ New Project」で新規作成します。",
        "Select or edit the project name. Use “+ New Project” at the end to create one.");

    public static string TipProjectOutputPath => Get(
        "分割 WAV の書き出し先フォルダです。横のフォルダボタンで変更できます。",
        "Folder for exported split WAVs. Change it with the folder button.");

    public static string TipSpectrum => Get(
        "再生出力の簡易スペクトラム表示です。",
        "Simple spectrum meter for playback output.");

    public static string TipLogEditor => Get(
        "操作・EXPORT・接続などのログです。右下のアイコンで消去・コピー・保存できます。",
        "Log for operations, EXPORT, and connection. Use the icons to clear, copy, or save.");

    public static string TipLogClear => Get(
        "ログ表示だけを消去します（ファイルは消しません）。",
        "Clear the log display only (does not delete files).");

    public static string TipLogCopy => Get(
        "ログ全文をクリップボードへコピーします。",
        "Copy the full log to the clipboard.");

    public static string TipLogDownload => Get(
        "ログをファイルへ保存します。",
        "Save the log to a file.");

    public static string TipCopyright => Get(
        "著作権・ライセンス情報（GitHub）を開きます。",
        "Open copyright / license information on GitHub.");

    public static string TipPlaylistHeader => Get(
        "遷移先として選ぶ Music Playlist の一覧です。クリックで Fade／Exit Source At を反映し、再生中は遷移を予約します。",
        "List of Music Playlists to jump to. Click to apply Fade / Exit Source At; while playing, schedules a transition.");

    public static string TipPlaylistItem(string playlistName) => Format(
        "{0}{1}"
        + "[Shift]+クリック／ドラッグ: グループ化（既存グループも新しい ID で上書き可）{1}"
        + "[Ctrl]+クリック／ドラッグ: グループ解除{1}"
        + "[Ctrl+Shift]+クリック／ドラッグ: 無効化／再有効化",
        "{0}{1}"
        + "[Shift]+click/drag: group (can overwrite an existing group with a new ID){1}"
        + "[Ctrl]+click/drag: ungroup{1}"
        + "[Ctrl+Shift]+click/drag: disable / re-enable",
        playlistName,
        Environment.NewLine);

    public static string TipWaveformEditSourceName => Get(
        "ダブルクリックで書き出しファイル名・Playlist名・Switch名を編集",
        "Double-click to edit the export file name, Playlist name, or Switch name");

    public static string TipWaveformMarkerLane => Get(
        "[Shift]+クリック／ドラッグ: マーカーを連続付与"
        + Environment.NewLine
        + "[Ctrl]+クリック／ドラッグ: マーカーを連続削除",
        "[Shift]+click/drag: add markers continuously"
        + Environment.NewLine
        + "[Ctrl]+click/drag: remove markers continuously");

    public static string TipWaveformMarkerLaneSessionEdit => Get(
        "▼ドラッグ: マーカーを移動"
        + Environment.NewLine
        + "[Alt]+▼ドラッグ: 一つ前のマーカーも同量移動"
        + Environment.NewLine
        + "[←] / [→]: シークバーを 1px 移動"
        + Environment.NewLine
        + "[Alt]+[←] / [→]: シーク位置のマーカーを 1px 移動（シークも連動）"
        + Environment.NewLine
        + "[Ctrl+Alt]+[←] / [→]: シーク位置のマーカー＋一つ前を 1px 同時移動"
        + Environment.NewLine
        + "▼／コメントをダブルクリック: コメントを編集"
        + Environment.NewLine
        + "[Ctrl+Shift+R] シーク位置のマーカーをリネーム"
        + Environment.NewLine
        + "[Ctrl]+[←] / [→]: 前後の Playlist 先頭／末尾、またはマーカーへ移動"
        + Environment.NewLine
        + "[Ctrl+Shift]+[←] / [→]: 前後のマーカーへ移動（Playlist 境界は飛ばす）"
        + Environment.NewLine
        + "[Delete] / [Ctrl+Del] 選択したマーカーを削除（アプリ上のみ）"
        + Environment.NewLine
        + "[Insert] シーク位置にマーカー追加（コメントなし）"
        + Environment.NewLine
        + "[Ctrl+Z] / [Ctrl+Shift+Z] / [Ctrl+Y] Undo / Redo"
        + Environment.NewLine
        + "コメント -L: 無限ループ / -R: リムーブ / -E: Exit Cue 以降 / -A: Entry Cue 前"
        + Environment.NewLine
        + "[0〜9] 表示中画面内の 0%〜90% へジャンプ（数字キー／テンキー）"
        + Environment.NewLine
        + "[C] / [.] シーク位置を変えずに表示を中央寄せ",
        "Drag ▼: move marker"
        + Environment.NewLine
        + "[Alt]+drag ▼: also move previous marker by the same delta"
        + Environment.NewLine
        + "[←] / [→]: nudge seek bar by 1px"
        + Environment.NewLine
        + "[Alt]+[←] / [→]: nudge marker at seek by 1px (seek follows)"
        + Environment.NewLine
        + "[Ctrl+Alt]+[←] / [→]: nudge marker at seek and the previous one by 1px"
        + Environment.NewLine
        + "Double-click ▼ / comment: edit comment"
        + Environment.NewLine
        + "[Ctrl+Shift+R] rename marker at seek"
        + Environment.NewLine
        + "[Ctrl]+[←] / [→]: jump to previous / next Playlist start/end or marker"
        + Environment.NewLine
        + "[Ctrl+Shift]+[←] / [→]: jump to previous / next marker (skip Playlist edges)"
        + Environment.NewLine
        + "[Delete] / [Ctrl+Del] remove selected marker (app session only)"
        + Environment.NewLine
        + "[Insert] add marker at seek position (no comment)"
        + Environment.NewLine
        + "[Ctrl+Z] / [Ctrl+Shift+Z] / [Ctrl+Y] Undo / Redo"
        + Environment.NewLine
        + "Comment -L: loop / -R: remove / -E: after Exit Cue / -A: before Entry Cue"
        + Environment.NewLine
        + "[0–9] jump to 0%–90% within the current view (number keys)"
        + Environment.NewLine
        + "[C] / [.] center the view on the seek position (seek unchanged)");

    public static string TipWaveformRegionFadeHandle => Get(
        "白三角をドラッグ: リージョン端フェード（非破壊）"
        + Environment.NewLine
        + "フェード範囲を右クリック: カーブを選択（Wwise と同じ名前・並び）"
        + Environment.NewLine
        + "Fade In は先頭 Music Segment 内、Fade Out は末尾 Music Segment 内に制限（-A/-E は同一セグメント）"
        + Environment.NewLine
        + "EXPORT 時は MusicClip の非破壊フェードとして設定（WAV へ焼き込みません）"
        + Environment.NewLine
        + "Playlist 遷移フェードとは別物で、重ねがけされます"
        + Environment.NewLine
        + "[Ctrl+Z] / [Ctrl+Y] Undo / Redo",
        "Drag white triangle: region-edge fade (non-destructive)"
        + Environment.NewLine
        + "Right-click fade area: choose curve (same names/order as Wwise)"
        + Environment.NewLine
        + "Fade In stays in the first Music Segment; Fade Out in the last (-A/-E are one segment)"
        + Environment.NewLine
        + "On EXPORT, applied as MusicClip non-destructive fades (not baked into WAVs)"
        + Environment.NewLine
        + "Independent from Playlist transition fades; gains multiply"
        + Environment.NewLine
        + "[Ctrl+Z] / [Ctrl+Y] Undo / Redo");

    public static string LabelRegionFadeCurve(RegionFadeCurveKind kind) => kind switch
    {
        RegionFadeCurveKind.LogarithmicBase3 => Get(
            "Logarithmic (Base 3)",
            "Logarithmic (Base 3)"),
        RegionFadeCurveKind.SineConstantPowerFadeIn => Get(
            "Sine (Constant Power Fade In)",
            "Sine (Constant Power Fade In)"),
        RegionFadeCurveKind.LogarithmicBase141 => Get(
            "Logarithmic (Base 1.41)",
            "Logarithmic (Base 1.41)"),
        RegionFadeCurveKind.InvertedSCurve => Get(
            "Inverted S-Curve",
            "Inverted S-Curve"),
        RegionFadeCurveKind.Linear => Get(
            "Linear",
            "Linear"),
        RegionFadeCurveKind.SCurve => Get(
            "S-Curve",
            "S-Curve"),
        RegionFadeCurveKind.ExponentialBase141 => Get(
            "Exponential (Base 1.41)",
            "Exponential (Base 1.41)"),
        RegionFadeCurveKind.SineConstantPowerFadeOut => Get(
            "Sine (Constant Power Fade Out)",
            "Sine (Constant Power Fade Out)"),
        RegionFadeCurveKind.ExponentialBase3 => Get(
            "Exponential (Base 3)",
            "Exponential (Base 3)"),
        _ => kind.ToString(),
    };

    /// <summary>全モード共通の波形シーク系ショートカット（タイムライン Tips 用）。</summary>
    public static string TipWaveformCommonKeys => Get(
        "[0〜9] 表示中画面内の 0%〜90% へジャンプ（数字キー／テンキー）"
        + Environment.NewLine
        + "[C] / [.] シーク位置を変えずに表示を中央寄せ"
        + Environment.NewLine
        + "[L] ループエンドの 1 小節前へ（小節管理がないときは 3 秒前）"
        + Environment.NewLine
        + "[E] 再生中（またはシークバー位置）の Playlist の Play -E をトグル"
        + Environment.NewLine
        + "[Z] 波形表示エリアの高さを 1倍 → 2倍 → 3倍 → 1倍",
        "[0–9] jump to 0%–90% within the current view (number keys)"
        + Environment.NewLine
        + "[C] / [.] center the view on the seek position (seek unchanged)"
        + Environment.NewLine
        + "[L] jump to 1 bar before loop end (or 3 seconds before without bar data)"
        + Environment.NewLine
        + "[E] toggle Play -E for the playing (or seek-bar) playlist"
        + Environment.NewLine
        + "[Z] cycle waveform height 1× → 2× → 3× → 1×");

    public static string TipWaveformZoomFitAll => Get(
        "ダブルクリックでタイムライン全体を表示",
        "Double-click to show the full timeline");

    public static string TipWaveformZoomPlaylist => Get(
        "ダブルクリックで Music Playlist を拡大表示",
        "Double-click to zoom the Music Playlist");

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
        + "EXPORT 時は遷移先向け Any→Object ルールの Play post-exit へ反映します。"
        + Environment.NewLine
        + "[E] 再生中（またはシークバー位置）の Playlist をトグル",
        "When on, dual-plays -E on -L loop wrap (Wwise Play post-exit)."
        + Environment.NewLine
        + "EXPORT writes Play post-exit on Any→Object transition rules for the destination."
        + Environment.NewLine
        + "[E] toggle for the playing (or seek-bar) playlist");

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

    // --- Marker options (existing) ---
    public static string TipStreamHeader => Get(
        "Wwise Music Track のストリーミング関連設定です。",
        "Streaming settings for Wwise Music Tracks.");

    public static string TipStreamEnabled => Get(
        "オンの場合、Music Track をストリーミング有効で作成します（既定オン）。"
        + " オフのときは Look-ahead Time／Prefetch Length は適用されません。",
        "When on, create Music Tracks with streaming enabled (default on)."
        + " When off, Look-ahead Time / Prefetch Length are not applied.");

    public static string TipLookAheadLabel => Get(
        "2 番目以降のセグメントの Look-ahead Time（ms、0〜9999。既定 500）。"
        + " Stream オン時のみ有効。"
        + " 先頭セグメント内の全トラック（グループ化レイヤー含む）は Zero latency のため UI 値ではなく 50ms 固定です"
        + "（極端な音量低下時の減衰追従用）。",
        "Look-ahead Time for the 2nd and later segments (ms, 0–9999, default 500)."
        + " Only when Stream is on."
        + " All tracks in the first segment (including layered groups) use Zero latency with a fixed 50 ms"
        + " Look-ahead (not the UI value), to keep up with extreme volume drops.");

    public static string TipLookAheadBox => Get(
        "Look-ahead Time（ms）。0〜9999。既定は 500 です。Stream オン時のみ有効。"
        + " 先頭セグメントには適用されません（固定 50ms）。",
        "Look-ahead Time (ms). 0–9999. Default 500. Only when Stream is on."
        + " Not applied to the first segment (fixed 50 ms).");

    public static string TipLookAheadUnit => Get(
        "単位はミリ秒（ms）です。",
        "Unit is milliseconds (ms).");

    public static string TipPrefetchLabel => Get(
        "Playlist 先頭セグメント内の全トラック（グループ化レイヤー含む）の Prefetch Length（ms、0〜9999。既定 500）。"
        + " Stream オン時のみ有効。Zero latency と同じ範囲に適用します。",
        "Prefetch Length for all tracks in the first playlist segment, including layered groups"
        + " (ms, 0–9999, default 500). Only when Stream is on. Same scope as Zero latency.");

    public static string TipPrefetchBox => Get(
        "Prefetch Length（ms）。0〜9999。既定は 500 です。"
        + " Playlist 先頭セグメント内の全トラックに反映されます。Stream オン時のみ有効。",
        "Prefetch Length (ms). 0–9999. Default 500."
        + " Applied to all tracks in the first playlist segment. Only when Stream is on.");

    public static string TipPrefetchUnit => Get(
        "単位はミリ秒（ms）です。",
        "Unit is milliseconds (ms).");

    public static string TipLoudnessHeader => Get(
        "Layer Music Option。"
        + " Wwise の Loudness Normalization を利用しているときはオンを推奨します。"
        + " グループ内の相対バランスを、Music Track の Make-Up Gain で非破壊維持します（WAV へは焼き込みません）。",
        "Layer Music Option."
        + " Recommended on when using Wwise Loudness Normalization."
        + " Keeps relative balance within a group nondestructively via Music Track Make-Up Gain"
        + " (not baked into WAV).");

    public static string TipLoudnessGroupBalance => Get(
        "オンの場合、グループ内各パートの Integrated Loudness（LKFS）を計測し、"
        + "最も大きいパートの Make-Up Gain を 0 dB、それ以外は相対差だけ下げます（既定オフ）。"
        + " Wwise の Loudness Normalization 利用時にオンを推奨。"
        + " 補正は Music Track の Make-Up Gain へ非破壊で書き込み、WAV へは焼き込みません。"
        + " グループ（2 パート以上）が無いときは操作できません。",
        "When on, measures each grouped part’s Integrated Loudness (LKFS),"
        + " sets Make-Up Gain of the loudest part to 0 dB and lowers the others by the relative difference"
        + " (default off)."
        + " Recommended when using Wwise Loudness Normalization."
        + " Writes Make-Up Gain on the Music Track nondestructively (not baked into WAV)."
        + " Disabled when no group of 2+ parts exists.");

    public static string TipMoreOptionsHeader => Get(
        "Stream／Layer Music Option／Marker Comment／Marker Grid を開閉します（既定は開いた状態）。"
        + " 開閉状態はプロジェクト設定へ自動保存されます。"
        + " 開閉しても Music Playlist の高さは変わりません。",
        "Expand/collapse Stream / Layer Music Option / Marker Comment / Marker Grid (default open)."
        + " Expansion is saved per project."
        + " Playlist height is unchanged when toggling.");


    public static string TipMarkerGridHeader => Get(
        "マーカーをドラッグで付与するときのスナップ間隔を指定します。縦線の描画には影響しません。",
        "Snap interval when dragging markers. Does not affect grid line drawing.");

    public static string TipMarkerGridTimeline => Get(
        "現在タイムラインに表示されているグリッドへスナップします。従来と同じ動作です。",
        "Snap to the grid currently shown on the timeline (legacy behavior).");

    public static string TipMarkerGridBar => Get(
        "タイムラインの表示倍率に関係なく、必ず小節単位でマーカーを付与します。",
        "Always snap markers to bars, regardless of zoom.");

    public static string TipMarkerGridBeat => Get(
        "タイムラインの表示倍率に関係なく、必ず拍単位でマーカーを付与します。",
        "Always snap markers to beats, regardless of zoom.");

    public static string TipMarkerCommentHeader => Get(
        "追加マーカーから生成する Wwise Custom Cue 名の規則を設定します。",
        "Rules for Wwise Custom Cue names generated from added markers.");

    public static string TipCommentDigits => Get(
        "連番の桁数を 1～6 で指定します。空欄または 0 の場合は連番自体を付けません。"
        + " 1 以上のときは、その桁で表せる最大値までしかマーカーを追加できません（例: 3 → 999 件）。",
        "Digit count 1–6. Empty or 0 disables numbering."
        + " When 1+, you can only add as many markers as that digit width allows (e.g. 3 → 999).");

    public static string TipCommentDigitsBox => Get(
        "連番の桁数です。空欄または 0 で連番なし、1～6 で連番ありになります。"
        + " 桁数を超える連番は追加できません。",
        "Digit count. Empty or 0 = no number; 1–6 enables numbering."
        + " Numbers beyond the digit width cannot be added.");

    public static string TipCommentZeroPad => Get(
        "オンの場合、Digits の桁数まで常に 0 で埋めます"
        + "（例: Digits=2 → 01、Digits=3 → 001、Digits=4 → 0001）。"
        + "オフのときは桁埋めせず 1, 2, 3… と表示します。",
        "When on, zero-pad to Digits (e.g. Digits=2 → 01, Digits=3 → 001)."
        + " When off, show 1, 2, 3… without padding.");

    public static string TipCommentResetPerPart => Get(
        "オンの場合、Music Playlist の各パート（書き出しファイル）ごとに連番を 1 へ戻します。",
        "When on, reset the serial number to 1 for each Music Playlist part (export file).");

    public static string TipCommentPrefix => Get(
        "入力がある場合、連番の前に接頭語を追加します。Digits が空欄または 0 のときは必須です。",
        "Optional prefix before the number. Required when Digits is empty or 0.");

    public static string TipCommentPrefixBox => Get(
        "Custom Cue 名の先頭に付ける文字列を入力します。空欄なら接頭語なし。"
        + " Digits が空欄または 0 のときは必須です。",
        "Text prepended to the Custom Cue name. Empty = no prefix."
        + " Required when Digits is empty or 0.");

    public static string TipCommentSuffix => Get(
        "入力がある場合、連番の後ろに接尾語を追加します。",
        "Optional suffix after the number.");

    public static string TipCommentSuffixBox => Get(
        "Custom Cue 名の連番より後ろに付ける文字列を入力します。空欄なら接尾語なし。Unicode 文字を使用できます。",
        "Text after the number in the Custom Cue name. Empty = no suffix. Unicode allowed.");

    public static string TipCommentSeparator => Get(
        "入力がある場合、接頭語／接尾語と連番の間に区切り文字を追加します。",
        "Optional separator between prefix/suffix and the number.");

    public static string TipCommentSeparatorBox => Get(
        "接頭語／接尾語と連番を繋ぐ文字列を入力します（例: _ または -）。空欄なら区切りなし。",
        "Separator between prefix/suffix and number (e.g. _ or -). Empty = none.");

    public static string TipCommentPreview => Get(
        "生成される Wwise Custom Cue 名の例と、名前が有効かどうかを表示します。",
        "Shows an example Wwise Custom Cue name and whether it is valid.");

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

    // --- Transport ---
    private static string WithKeyRepeat(string japanese, string english) =>
        Get(
            japanese + Environment.NewLine + "長押しでキーリピート",
            english + Environment.NewLine + "Hold for key repeat");

    public static string TipTransportPlayPause => Get(
        "[Space] 再生 / 一時停止"
        + Environment.NewLine
        + "[Alt+Enter] / Alt+クリック 直前の開始位置から再生し直し"
        + Environment.NewLine
        + "[Ctrl+Space] / Ctrl+クリック 3秒前から再生",
        "[Space] Play / Pause"
        + Environment.NewLine
        + "[Alt+Enter] / Alt+click Restart from last start"
        + Environment.NewLine
        + "[Ctrl+Space] / Ctrl+click Play from 3 seconds earlier");

    public static string TipTransportJumpToBar => Get(
        "[G] 小節番号を指定して移動",
        "[G] Jump to bar number");

    public static string TipTransportGoToStart => WithKeyRepeat(
        "[Ctrl+Home] 先頭へ移動",
        "[Ctrl+Home] Go to start");

    public static string TipTransportPreviousPage => WithKeyRepeat(
        "[Page Up] 前の表示ページ",
        "[Page Up] Previous view page");

    public static string TipTransportPreviousPlaylist => WithKeyRepeat(
        "[Ctrl+←] 前の Music Playlist 先頭／末尾、またはマーカーへ移動",
        "[Ctrl+←] Previous Music Playlist start/end or marker");

    public static string TipTransportPreviousMarker => WithKeyRepeat(
        "[Ctrl+Shift+←] 前のマーカーへ移動",
        "[Ctrl+Shift+←] Previous marker");

    public static string TipTransportPreviousBar => WithKeyRepeat(
        "[Home] 前の小節",
        "[Home] Previous bar");

    public static string TipTransportNextBar => WithKeyRepeat(
        "[End] 次の小節",
        "[End] Next bar");

    public static string TipTransportPreviousViewStep => WithKeyRepeat(
        "[Home] 表示の約 5% 前へ移動",
        "[Home] Move back about 5% of the view");

    public static string TipTransportNextViewStep => WithKeyRepeat(
        "[End] 表示の約 5% 先へ移動",
        "[End] Move forward about 5% of the view");

    public static string TipTransportNextPlaylist => WithKeyRepeat(
        "[Ctrl+→] 次の Music Playlist 先頭／末尾、またはマーカーへ移動",
        "[Ctrl+→] Next Music Playlist start/end or marker");

    public static string TipTransportNextMarker => WithKeyRepeat(
        "[Ctrl+Shift+→] 次のマーカーへ移動",
        "[Ctrl+Shift+→] Next marker");

    public static string TipTransportNextPage => WithKeyRepeat(
        "[Page Down] 次の表示ページ",
        "[Page Down] Next view page");

    public static string TipTransportGoToEnd => WithKeyRepeat(
        "[Ctrl+End] 末尾へ移動",
        "[Ctrl+End] Go to end");

    public static string TipTransportTimeZoomIn => WithKeyRepeat(
        "[↑] 時間軸を拡大",
        "[↑] Zoom in time");

    public static string TipTransportTimeZoomOut => WithKeyRepeat(
        "[↓] 時間軸を縮小",
        "[↓] Zoom out time");

    public static string TipTransportTimeZoomMax => WithKeyRepeat(
        "[Ctrl+↑] 時間軸を最大拡大",
        "[Ctrl+↑] Max time zoom");

    public static string TipTransportTimeZoomReset => WithKeyRepeat(
        "[Ctrl+↓] 時間軸を全体表示",
        "[Ctrl+↓] Fit time to view");

    public static string TipTransportAmpZoomIn => WithKeyRepeat(
        "[Shift+↑] 振幅を拡大",
        "[Shift+↑] Zoom in amplitude");

    public static string TipTransportAmpZoomOut => WithKeyRepeat(
        "[Shift+↓] 振幅を縮小",
        "[Shift+↓] Zoom out amplitude");

    public static string TipTransportAmpZoomMax => WithKeyRepeat(
        "[Ctrl+Shift+↑] 振幅を最大拡大",
        "[Ctrl+Shift+↑] Max amplitude zoom");

    public static string TipTransportAmpZoomReset => WithKeyRepeat(
        "[Ctrl+Shift+↓] 振幅を既定に戻す",
        "[Ctrl+Shift+↓] Reset amplitude zoom");

    public static string TipTransportCycleWaveformHeight => Get(
        "[Z] 波形表示エリアの高さを切替（1倍→2倍→3倍）",
        "[Z] Cycle waveform height (1×→2×→3×)");

    public static string TipForTransportCommand(
        TransportCommand command,
        bool waveOnlyViewStep = false,
        bool waveOnlyMarkerNav = false) => command switch
    {
        TransportCommand.TogglePlayback => TipTransportPlayPause,
        TransportCommand.JumpToBar => TipTransportJumpToBar,
        TransportCommand.GoToStart => TipTransportGoToStart,
        TransportCommand.PreviousPage => TipTransportPreviousPage,
        TransportCommand.PreviousPlaylist => waveOnlyMarkerNav
            ? TipTransportPreviousMarker
            : TipTransportPreviousPlaylist,
        TransportCommand.PreviousBar => waveOnlyViewStep
            ? TipTransportPreviousViewStep
            : TipTransportPreviousBar,
        TransportCommand.NextBar => waveOnlyViewStep
            ? TipTransportNextViewStep
            : TipTransportNextBar,
        TransportCommand.NextPlaylist => waveOnlyMarkerNav
            ? TipTransportNextMarker
            : TipTransportNextPlaylist,
        TransportCommand.NextPage => TipTransportNextPage,
        TransportCommand.GoToEnd => TipTransportGoToEnd,
        TransportCommand.TimeZoomIn => TipTransportTimeZoomIn,
        TransportCommand.TimeZoomOut => TipTransportTimeZoomOut,
        TransportCommand.TimeZoomMax => TipTransportTimeZoomMax,
        TransportCommand.TimeZoomReset => TipTransportTimeZoomReset,
        TransportCommand.AmpZoomIn => TipTransportAmpZoomIn,
        TransportCommand.AmpZoomOut => TipTransportAmpZoomOut,
        TransportCommand.AmpZoomMax => TipTransportAmpZoomMax,
        TransportCommand.AmpZoomReset => TipTransportAmpZoomReset,
        TransportCommand.CycleWaveformHeight => TipTransportCycleWaveformHeight,
        _ => string.Empty,
    };

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
        double muteDb) => Format(
        "Group State Volume: Track '{0}'  {1}=0dB / others={2:0.###}dB",
        "Group State Volume: Track '{0}'  {1}=0dB / others={2:0.###}dB",
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

    // --- Status / empty UI ---
    public static string StatusChecking => Get("確認中…", "Checking…");
    public static string StatusStartupCheckOff => Get("起動時チェックオフ", "Startup check off");
    public static string StatusDisconnected => Get("未接続", "Disconnected");
    public static string StatusNoneSelected => Get("（未選択）", "(none selected)");
    public static string StatusNoProject => Get("(プロジェクトなし)", "(no project)");

    public static string WaveformEmptyHint => Get(
        "Wave / XML をドロップすると波形と小節線を表示します",
        "Drop a Wave / XML file to show the waveform and bar lines");

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
        "Message : 出力パート {0} 件。［EXPORT］で分割 WAV を書き出し、Wwise へ登録できます。",
        "Message : {0} output part(s). Use [EXPORT] to write split WAVs and register in Wwise.",
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

    // --- Drop / analyze ---
    public static string LogErrorHeader => Get("=== エラー ===", "=== Error ===");
    public static string LogWarningHeader => Get("=== 警告 ===", "=== Warning ===");
    public static string LogDropNeedWavOrXml => Get(
        "Message : .wav または .xml をドロップしてください。",
        "Message : Drop a .wav or .xml file.");

    public static string LogWaveMissing(string path) => Format(
        "Wave : {0} (なし)",
        "Wave : {0} (missing)",
        path);

    public static string LogWaveRequired => Get(
        "Message : 波形表示には .wav が必要です。",
        "Message : A .wav file is required to show the waveform.");

    public static string LogIxmlTimeRefMissing => Get(
        "Message : iXML の TimeReference が取れません（無し、または 0）。"
        + Environment.NewLine
        + "Message : アウフタクト判定と小節位置の対応には iXML TimeReference が必要です。"
        + " 0 のときは波形先頭＝PPQ 0 とみなします。",
        "Message : iXML TimeReference is missing (absent or 0)."
        + Environment.NewLine
        + "Message : Anacrusis detection and bar positions need iXML TimeReference."
        + " When 0, the wave start is treated as PPQ 0.");

    public static string LogXmlMissing(string path) => Format(
        "Xml  : {0} (なし)",
        "Xml  : {0} (missing)",
        path);

    public static string LogXmlMissingBars => Get(
        "Message : 同名 .xml が無いため小節線は表示しません。",
        "Message : No matching .xml; bar lines will not be shown.");

    public static string LogXmlPairHeader => Get(
        "=== Nuendo XML モード ===",
        "=== Nuendo XML mode ===");

    public static string LogXmlPairModeName => Get(
        "Mode : WAV + 同名 XML（Nuendo トラックリスト）",
        "Mode : WAV + matching XML (Nuendo tracklist)");

    public static string LogWaveOnlyHeader => Get(
        "=== Wave 単体モード ===",
        "=== Wave-only mode ===");

    public static string LogWaveOnlyModeName(WaveOnlyMode mode) => mode switch
    {
        WaveOnlyMode.MarkersOnly => Get(
            "Mode : マーカーのみ／無し（cue + adtl）",
            "Mode : Markers only / none (cue + adtl)"),
        WaveOnlyMode.SmplLoop => Get(
            "Mode : smpl ループ",
            "Mode : smpl loop"),
        WaveOnlyMode.Regions => Get(
            "Mode : リージョン（cue + adtl）",
            "Mode : Regions (cue + adtl)"),
        _ => Get($"Mode : {mode}", $"Mode : {mode}"),
    };

    public static string LogWaveOnlyMarkersOnlySummary(int markerCount) => markerCount == 0
        ? Get(
            "Message : 埋め込みマーカーはありません（許容）。冒頭を Entry Cue、末尾を Exit Cue とします。",
            "Message : No embedded markers (allowed). Using start as Entry Cue and end as Exit Cue.")
        : Format(
            "Message : 埋め込みマーカーを表示します（{0} 件）。",
            "Message : Showing embedded markers ({0}).",
            markerCount);

    public static string LogWaveOnlySmplLoopSummary(int acceptedLoopCount, int skippedLoopCount)
    {
        if (acceptedLoopCount == 0 && skippedLoopCount == 0)
        {
            return Get(
                "Message : smpl ループはありません。",
                "Message : No smpl loops.");
        }

        if (skippedLoopCount == 0)
        {
            return Format(
                "Message : smpl ループの Start / End を -L / -E マーカーへ差し替えました（ループ {0} 件）。",
                "Message : Replaced smpl loop Start / End with -L / -E markers ({0} loop(s)).",
                acceptedLoopCount);
        }

        return Format(
            "Message : smpl ループの Start / End を -L / -E マーカーへ差し替えました（採用 {0} 件、範囲外／無効でスキップ {1} 件）。",
            "Message : Replaced smpl loop Start / End with -L / -E markers ({0} accepted, {1} skipped as out of range / invalid).",
            acceptedLoopCount,
            skippedLoopCount);
    }

    public static string LogWaveOnlyDiscardedEmbeddedSummary(int count) => Format(
        "Message : smpl ループ以外の埋め込みマーカー類を破棄しました（{0} 件）。",
        "Message : Discarded embedded markers other than smpl loops ({0}).",
        count);

    public static string LogWaveOnlyDiscardedEmbeddedItem(
        string kind,
        long sampleOffset,
        string comment)
    {
        var kindLabel = kind.Equals("region", StringComparison.OrdinalIgnoreCase)
            ? Get("リージョン", "region")
            : Get("マーカー", "marker");
        var name = FormatMarkerNameForLog(comment);
        return Format(
            "  - 破棄 {0} sample={1:N0} 「{2}」",
            "  - Discarded {0} sample={1:N0} “{2}”",
            kindLabel,
            sampleOffset,
            name);
    }

    public static string LogWaveOnlyLoopRegions(int regionCount) => Format(
        "Message : コメントが -L のみのマーカーから無限ループリージョンを {0} 区画構築しました。",
        "Message : Built {0} infinite-loop region(s) from markers whose comment is only -L.",
        regionCount);

    public static string LogWaveOnlyMarkerDuplicate => Get(
        "Message : 同じ位置にマーカーは置けません。",
        "Message : A marker already exists at this position.");

    public static string LogWaveOnlyMarkerRenamed(string fromName, string toName) => Format(
        "Message : マーカー名を変更しました: 「{0}」→「{1}」",
        "Message : Marker renamed: “{0}” → “{1}”.",
        FormatMarkerNameForLog(fromName),
        FormatMarkerNameForLog(toName));

    private static string FormatMarkerNameForLog(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length == 0
            ? Get("（空）", "(empty)")
            : trimmed;
    }

    public static string LogWaveOnlyRemoveRegions(int regionCount) => Format(
        "Message : コメントが -R のみのマーカーからリムーブ範囲を {0} 区画構築しました。",
        "Message : Built {0} remove region(s) from markers whose comment is only -R.",
        regionCount);

    public static string LogWaveOnlyExitRegions(int regionCount) => Format(
        "Message : コメントが -E のみのマーカーから Exit Cue 以降の範囲を {0} 区画構築しました。",
        "Message : Built {0} after-Exit-Cue region(s) from markers whose comment is only -E.",
        regionCount);

    public static string LogWaveOnlyAnacrusisRegions(int regionCount) => Format(
        "Message : コメントが -A のみのマーカーからアウフタクト（Entry Cue 前）範囲を {0} 区画構築しました。",
        "Message : Built {0} anacrusis (pre-Entry-Cue) region(s) from markers whose comment is only -A.",
        regionCount);

    public static string LogWaveOnlyOutputParts(int partCount) => Format(
        "Message : Music Playlist / Segment 名の判定が可能です（出力パート {0}）。",
        "Message : Music Playlist / Segment names can be resolved ({0} output part(s)).",
        partCount);

    public static string LogWaveOnlyModeNotImplemented => Get(
        "Message : このモードは未実装です（後続対応）。",
        "Message : This mode is not implemented yet.");

    public static string LogMultiWaveOnlyHeader => Get(
        "=== 複数波形モード（XML なし） ===",
        "=== Multi-wave mode (no XML) ===");

    public static string LogMultiWaveOnlyModeName(int waveCount) => Format(
        "Mode : 複数 WAV を仮想タイムラインへ連結（{0} 本）",
        "Mode : Concatenate multiple WAVs on a virtual timeline ({0} file(s))",
        waveCount);

    public static string LogMultiWaveOnlyFileHeader(int index, int total, string path) => Format(
        "--- ファイル {0}/{1} : {2} ---",
        "--- File {0}/{1} : {2} ---",
        index,
        total,
        path);

    public static string LogMultiWaveOnlySpanSummary(
        long virtualStart,
        long virtualEnd,
        int partCount) => Format(
        "Message : 仮想 samples=[{0:N0} .. {1:N0}) / 出力パート {2}",
        "Message : Virtual samples=[{0:N0} .. {1:N0}) / output part(s) {2}",
        virtualStart,
        virtualEnd,
        partCount);

    public static string LogMultiWaveOnlyVirtualSource(int waveCount) => Format(
        "仮想連結（{0} 本）",
        "Virtual concat ({0} file(s))",
        waveCount);

    public static string LogMultiWaveOnlyFormatMismatch(string firstPath, string secondPath) => Format(
        "Message : フォーマットが一致しないため複数波形モードを中止します。{0} と {1}",
        "Message : Aborting multi-wave mode because formats do not match. {0} vs {1}",
        firstPath,
        secondPath);

    public static string LogMultiWaveOnlyFormatDetail(
        uint sampleRateA,
        ushort channelsA,
        ushort bitsA,
        uint sampleRateB,
        ushort channelsB,
        ushort bitsB) => Format(
        "Message : A={0} Hz / {1} ch / {2} bit  vs  B={3} Hz / {4} ch / {5} bit",
        "Message : A={0} Hz / {1} ch / {2} bit  vs  B={3} Hz / {4} ch / {5} bit",
        sampleRateA,
        channelsA,
        bitsA,
        sampleRateB,
        channelsB,
        bitsB);

    public static string LogMultiWaveOnlyEmptyWave => Get(
        "Message : フレーム数が 0 の WAV は複数波形モードに含められません。",
        "Message : A WAV with 0 frames cannot be included in multi-wave mode.");

    public static string ErrMultiWaveOnlyTooLong => Get(
        "連結後の波形が長すぎます（4 GiB 超）。",
        "Concatenated wave is too long (over 4 GiB).");

    public static string ErrMultiWaveOnlyNoSpans => Get(
        "複数波形のソース区間がありません。",
        "No multi-wave source spans.");

    public static string ErrMultiWaveOnlyConcatRange => Get(
        "複数波形の一時連結中にデータ範囲外へ達しました。",
        "Reached out-of-range data while building multi-wave playback concat.");

    public static string LogOutsideWaveHeader => Get(
        "=== 波形範囲外（無視） ===",
        "=== Outside wave range (ignored) ===");

    public static string LogOutsideWaveMessage => Get(
        "Message : 波形タイムライン外のマーカー／サイクルは描画せず、出力にも含めません。",
        "Message : Markers/cycles outside the wave timeline are not drawn or exported.");

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

    // --- Importer exceptions ---
    public static string ErrBadSampleRate => Get(
        "サンプルレートまたは BlockAlign が不正です。",
        "Sample rate or BlockAlign is invalid.");

    public static string ErrStateGroupPathRequired => Get(
        "複数パート時は State Group パスが必要です。",
        "A State Group path is required for multi-part projects.");

    public static string ErrNoTracks(string segmentName) => Format(
        "トラックがありません: {0}",
        "No tracks: {0}",
        segmentName);

    public static string ErrCannotResolveOutputPart(string path) => Format(
        "出力パートを特定できません: {0}",
        "Cannot identify output part: {0}",
        path);

    public static string ErrTrackRangeEmpty(string segmentName, string trackName, string rangeMs) => Format(
        "トラック範囲が空です: {0}/{1} ({2})",
        "Track range is empty: {0}/{1} ({2})",
        segmentName,
        trackName,
        rangeMs);

    public static string ErrSlicedWavMissing(string segmentName, string trackName) => Format(
        "切り出し WAV が見つかりません: {0}/{1}",
        "Sliced WAV not found: {0}/{1}",
        segmentName,
        trackName);

    public static string ListJoinAnd => Get(" と ", " and ");

    public static string ErrRegionOverlap(string detail) => Format(
        "リージョン範囲が重なっています: {0}。"
        + " -R / -L / -E（および内部生成の -A）は重ならないようにマーカーを配置してください。",
        "Region ranges overlap: {0}."
        + " Place -R / -L / -E (and internally generated -A) so they do not overlap.",
        detail);

    public static string ReasonOutsideTimeline => Get(
        "波形タイムライン範囲外（描画・出力計画の対象外）",
        "Outside wave timeline (excluded from draw/export plan)");

    public static string ReasonOutsideSamples => Get(
        "波形サンプル範囲外（描画・出力計画の対象外）",
        "Outside wave sample range (excluded from draw/export plan)");

    public static string ReasonNoOverlap => Get(
        "波形と有効な重なりなし（描画・出力計画の対象外）",
        "No valid overlap with the wave (excluded from draw/export plan)");

    public static string ErrNoTempoEvents => Get(
        "テンポイベントがありません。",
        "No tempo events.");

    public static string ErrTempoTrackMissing => Get(
        "MTempoTrackEvent (Tempo Track) が見つかりません。",
        "MTempoTrackEvent (Tempo Track) was not found.");

    public static string ErrTempoEventNoBpm => Get(
        "TempoEvent に BPM がありません。",
        "TempoEvent has no BPM.");

    public static string ErrTempoEventNoPpq => Get(
        "TempoEvent に PPQ がありません。",
        "TempoEvent has no PPQ.");

    public static string ErrSampleRateZero => Get(
        "サンプルレートが 0 です。",
        "Sample rate is 0.");

    public static string ErrNoOutputParts => Get(
        "出力パートがありません。",
        "No output parts.");

    public static string ErrEmptyWaapiResponse => Get(
        "空の応答を受信しました。",
        "Received an empty response.");

    public static string ErrNotRiffHeader => Get(
        "RIFF ヘッダーではありません。",
        "Not a RIFF header.");

    public static string ErrNotWaveFormat => Get(
        "WAVE 形式ではありません。",
        "Not WAVE format.");

    public static string ErrFmtChunkMissing => Get(
        "fmt チャンクが見つかりません。",
        "fmt chunk not found.");

    public static string ErrFmtChunkInvalid => Get(
        "fmt チャンクが不正です。",
        "fmt chunk is invalid.");

    public static string ErrDataChunkMissing => Get(
        "data チャンクが見つかりません。",
        "data chunk not found.");

    public static string ErrDataChunkTruncated => Get(
        "data チャンクの読み取りが途中で終了しました。",
        "Reading the data chunk ended unexpectedly.");

    public static string ErrChunkSizeInvalid(string id) => Format(
        "チャンクサイズが不正です: {0}",
        "Invalid chunk size: {0}",
        id);

    public static string ErrBitsPerSampleInvalid => Get(
        "BitsPerSample が不正です。",
        "BitsPerSample is invalid.");

    public static string ErrUnsupportedBitDepth(int bits) => Format(
        "未対応のビット深度です: {0}",
        "Unsupported bit depth: {0}",
        bits);

    public static string ErrUnsupportedWavFormat(string name) => Format(
        "未対応の WAV 形式です: {0}",
        "Unsupported WAV format: {0}",
        name);

    public static string ErrWaveFormatInvalid => Get(
        "波形フォーマットが不正です。",
        "Wave format is invalid.");

    public static string ErrEmptyData => Get(
        "データが空です。",
        "Data is empty.");

    public static string ErrBlockAlignInvalid => Get(
        "BlockAlign が不正です。",
        "BlockAlign is invalid.");

    public static string ErrExportRangeEmpty => Get(
        "書き出し範囲が空です。",
        "Export range is empty.");

    public static string ErrExportRangeBeforeData(long start, long end) => Format(
        "書き出し範囲が data 外です: samples=[{0}..{1})",
        "Export range is outside data: samples=[{0}..{1})",
        start,
        end);

    public static string ErrSampleFormatInvalid => Get(
        "WAV のサンプル形式が不正です。",
        "WAV sample format is invalid.");

    public static string ErrPcmBitUnsupported(int bits) => Format(
        "{0} bit PCM は未対応です。",
        "{0}-bit PCM is not supported.",
        bits);

    public static string ErrAudioFormatUnsupported(int format) => Format(
        "AudioFormat={0} は波形表示未対応です。",
        "AudioFormat={0} is not supported for waveform display.",
        format);

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
    public static string LabelEntryCue => Get("Entry Cue", "Entry Cue");
    public static string LabelSameTime => Get("Same Time", "Same Time");
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

    /// <summary>遷移先同期モードの表示名（ログ・診断用）。</summary>
    public static string LabelDestinationSync(PlaylistDestinationSyncMode mode) => mode switch
    {
        PlaylistDestinationSyncMode.EntryCue => LabelEntryCue,
        PlaylistDestinationSyncMode.SameTime => LabelSameTime,
        _ => mode.ToString(),
    };

    /// <summary>Marker Grid ラジオの表示名。</summary>
    public static string LabelMarkerGrid(MarkerGridOverrideMode mode) => mode switch
    {
        MarkerGridOverrideMode.Default => LabelTimeline,
        MarkerGridOverrideMode.Bar => LabelBar,
        MarkerGridOverrideMode.Beat => LabelBeat,
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

    public static string CopyrightText => Get(
        "© 2026 MIYABI GAME AUDIO INC.  GitHub"
        + "\nWwise® and Audiokinetic® are trademarks of Audiokinetic Inc.",
        "© 2026 MIYABI GAME AUDIO INC.  GitHub"
        + "\nWwise® and Audiokinetic® are trademarks of Audiokinetic Inc.");

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
        "=== Nuendo Tempo Track ===",
        "=== Nuendo Tempo Track ===");

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
    public static string KeySlices => Get("Slices  :", "Slices  :");
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
        int groupStateTransitionCount) => Format(
        "Message : WWU 直接編集を開始します（PlayAt={0} 件 / Clip Fade 超過={1} 件 / Playlist 遷移 Fade={2} 件 / Group State Transition={3} 件）。保存→クローズ→パッチ→再オープンを行います。",
        "Message : Starting WWU patch (PlayAt={0}, Clip Fade over limit={1}, Playlist transition Fade={2}, Group State Transition={3}). save → close → patch → reopen.",
        playAtCount,
        clipFadeCount,
        transitionFadeCount,
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

    // --- ColorDev panel ---
    public static string ColorDevTitle => Get("色調整（開発者）", "Color Adjustment (Developer)");
    public static string ColorDevClose => Get("閉じる", "Close");
    public static string ColorDevResetToDefaults => Get("既定に戻す", "Reset to Defaults");

    /// <summary>色調整パネルの表示名（キー → 日英ラベル）。<see cref="UiColors.Entries"/> の Key と対応。</summary>
    public static string ColorLabel(string key) => key switch
    {
        "PrimaryFore" => Get("共通・標準文字／アイコン", "Common - Primary Text / Icon"),
        "MutedFore" => Get("共通・弱い文字／枠", "Common - Muted Text / Border"),
        "AccentCyan" => Get("共通・シアンアクセント", "Common - Cyan Accent"),
        "SurfaceBack" => Get("共通・基本背景", "Common - Base Background"),
        "ChromeBack" => Get("共通・クローム背景", "Common - Chrome Background"),
        "ChromeBorder" => Get("共通・クローム境界線／ホバー", "Common - Chrome Border / Hover"),
        "ChromeMid" => Get("共通・中間グレー（押下／つまみ）", "Common - Mid Gray (Pressed / Thumb)"),
        "ChromeDim" => Get("共通・やや明るいグレー（無効／ホバー）", "Common - Dim Gray (Disabled / Hover)"),
        "WaveformBack" => Get("波形エリア背景", "Waveform Area Background"),
        "EmptyHint" => Get("空状態ヒント", "Empty State Hint"),
        "TempoBg" => Get("テンポ・背景", "Tempo - Background"),
        "SignatureBg" => Get("拍子・背景", "Signature - Background"),
        "MarkerRowBg" => Get("マーカー行・背景", "Marker Row - Background"),
        "MarkerTriangle" => Get("マーカー三角", "Marker Triangle"),
        "BarLine" => Get("小節線", "Bar Line"),
        "BeatLine" => Get("拍線", "Beat Line"),
        "TempoChangeLine" => Get("テンポ変更線", "Tempo Change Line"),
        "WaveFill" => Get("波形", "Waveform Fill"),
        "WaveZeroDbLine" => Get("波形 0dB 線", "Waveform 0 dB Line"),
        "WaveformSourceMeterTrack" => Get("波形メーター・トラック", "Waveform Meter - Track"),
        "WaveformSourceMeterMinimum" => Get("波形メーター・最小", "Waveform Meter - Minimum"),
        "WaveformSourceMeterMaximum" => Get("波形メーター・最大", "Waveform Meter - Maximum"),
        "RegionWaveFillGray" => Get("波形リージョン塗り（通常）", "Region Fill (Normal)"),
        "RegionWaveFillExcluded" => Get("波形リージョン塗り（-R）", "Region Fill (-R)"),
        "RegionWaveFillLoop" => Get("波形リージョン塗り（-L）", "Region Fill (-L)"),
        "RegionWaveFillAnacrusis" => Get("波形リージョン塗り（-A）", "Region Fill (-A)"),
        "RegionWaveFillExit" => Get("波形リージョン塗り（-E）", "Region Fill (-E)"),
        "RegionBoundaryMarker" => Get("リージョン境界マーカー", "Region Boundary Marker"),
        "EntryCueMarker" => Get("Entry Cue マーカー", "Entry Cue Marker"),
        "ExitCueMarker" => Get("Exit Cue マーカー", "Exit Cue Marker"),
        "RegionFadeCurve" => Get("リージョン端フェード曲線", "Region Edge Fade Curve"),
        "OutputPartShadow" => Get("出力パート名・影", "Output Part Name - Shadow"),
        "MusicSegmentLaneBg" => Get("Music Segment Name・背景", "Music Segment Name - Background"),
        "MusicPlaylistLaneBg" => Get("Music Playlist Name・背景", "Music Playlist Name - Background"),
        "SeekExit" => Get("Exit 二重再生ヘッド", "Exit Dual Playhead"),
        "SeekAnacrusis" => Get("アウフタクト先行再生ヘッド", "Anacrusis Lead-in Playhead"),
        "SeekFadeOut" => Get("遷移元フェードアウトヘッド", "Source Fade-out Playhead"),
        "MouseGuide" => Get("マウスガイド", "Mouse Guide"),
        "WaveformScrollTrack" => Get("波形スクロール・トラック", "Waveform Scrollbar - Track"),
        "TransportBadgeBack" => Get("Transport・ズーム記号背景", "Transport - Zoom Badge Background"),
        "LogDefault" => Get("ログ文字（既定）", "Log Text (Default)"),
        "LogHeader" => Get("ログ文字（ヘッダ）", "Log Text (Header)"),
        "LogWarning" => Get("ログ文字（警告）", "Log Text (Warning)"),
        "LogError" => Get("ログ文字（エラー）", "Log Text (Error)"),
        "OptionGlyphCheckMark" => Get("オプション・チェック線", "Option - Check Mark"),
        "SectionHeaderBack" => Get("セクション見出し・背景", "Section Header - Background"),
        "PlaylistAutoBack" => Get("Playlist・自動再生開始フェード塗り", "Playlist - Auto Start Fade Fill"),
        "PlaylistManualBack" => Get("Playlist・手動再生開始フェード塗り", "Playlist - Manual Start Fade Fill"),
        "PlaylistManualBorder" => Get("Playlist・手動再生中枠", "Playlist - Manual Playing Border"),
        "MarkerCommentErrorFore" => Get("Marker Comment・エラー文字", "Marker Comment - Error Text"),
        "ActionLinkFore" => Get("Action Bar・リンク文字", "Action Bar - Link Text"),
        "ReloadButtonFill" => Get("RELOAD・塗り", "RELOAD - Fill"),
        "ReloadButtonHoverFill" => Get("RELOAD・ホバー塗り", "RELOAD - Hover Fill"),
        "ReloadButtonBack" => Get("RELOAD・枠", "RELOAD - Border"),
        "ReloadButtonHoverBack" => Get("RELOAD・ホバー枠", "RELOAD - Hover Border"),
        "ReloadButtonPressedBack" => Get("RELOAD・押下枠", "RELOAD - Pressed Border"),
        "ClearButtonFill" => Get("CLEAR・塗り", "CLEAR - Fill"),
        "ClearButtonHoverFill" => Get("CLEAR・ホバー塗り", "CLEAR - Hover Fill"),
        "ClearButtonBack" => Get("CLEAR・枠", "CLEAR - Border"),
        "ClearButtonHoverBack" => Get("CLEAR・ホバー枠", "CLEAR - Hover Border"),
        "ClearButtonPressedBack" => Get("CLEAR・押下枠", "CLEAR - Pressed Border"),
        "ExportButtonFill" => Get("EXPORT・塗り", "EXPORT - Fill"),
        "ExportButtonHoverFill" => Get("EXPORT・ホバー塗り", "EXPORT - Hover Fill"),
        "ExportButtonBack" => Get("EXPORT・枠", "EXPORT - Border"),
        "ExportButtonHoverBack" => Get("EXPORT・ホバー枠", "EXPORT - Hover Border"),
        "ExportButtonPressedBack" => Get("EXPORT・押下枠", "EXPORT - Pressed Border"),
        "SpectrumBar" => Get("スペアナ・バー", "Spectrum - Bar"),
        "StatusBarBack" => Get("WAAPI Status・背景", "WAAPI Status - Background"),
        "StatusBarConnectedBadgeBack" => Get("WAAPI Status・接続バッジ背景", "WAAPI Status - Connected Badge Background"),
        "StatusBarDisconnectedBadgeBack" => Get(
            "WAAPI Status・切断バッジ背景",
            "WAAPI Status - Disconnected Badge Background"),
        "StatusBarErrorDetailFore" => Get("WAAPI Status・エラー詳細文字", "WAAPI Status - Error Detail Text"),
        "KeepTargetLockFore" => Get("Keep Target・施錠", "Keep Target - Locked"),
        "KeepTargetLockHoverFore" => Get("Keep Target・施錠ホバー", "Keep Target - Locked Hover"),
        "KeepTargetUnlockFore" => Get("Keep Target・開錠", "Keep Target - Unlocked"),
        "KeepTargetUnlockHoverFore" => Get("Keep Target・開錠ホバー", "Keep Target - Unlocked Hover"),
        "DialogBodyBack" => Get("Go To Measure・背景", "Go To Measure - Background"),
        "DialogInputBack" => Get("Go To Measure・入力背景", "Go To Measure - Input Background"),
        "DialogShadow" => Get("Go To Measure・影", "Go To Measure - Shadow"),
        "ColorPanelBack" => Get("色設定パネル・背景", "Color Panel - Background"),
        "ColorPanelListBack" => Get("色設定パネル・一覧背景", "Color Panel - List Background"),
        "ColorPanelInputBack" => Get("色設定パネル・入力背景", "Color Panel - Input Background"),
        _ => key,
    };
}
