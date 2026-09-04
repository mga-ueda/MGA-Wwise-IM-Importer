namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
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
        "起動時およびこのプロジェクトへ戻ったときに、最後の作業セッションを復元します（プロジェクト設定・既定オン）。",
        "On startup and when returning to this project, restore the last session. Project setting (default on).");

    public static string TipAlwaysOnTop => Get(
        "ウィンドウを常に最前面へ表示します。",
        "Keep the window always on top (app setting).");

    public static string TipClear => Get(
        "波形・セッション・ログをクリアし、選択中プロジェクトの設定をアプリ既定へ戻します。"
        + Environment.NewLine
        + "書き出し先フォルダ・WAAPI Keep Target・Always on Top・"
        + "Stream／Look-ahead／Prefetch／Keep Layer Balance（アプリ設定）は変わりません。"
        + " プロジェクト自体は削除しません。",
        "Clear wave, session, and log, and reset the active project settings to app defaults."
        + Environment.NewLine
        + "Export folder, WAAPI Keep Target, Always on Top, and"
        + " Stream / Look-ahead / Prefetch / Keep Layer Balance (app settings) are unchanged."
        + " The project itself is not deleted.");

    public static string TipReload => Get(
        "最後に読み込んだ WAV／XML を、元のファイルから再読み込みします。"
        + Environment.NewLine
        + "DAW などで波形を更新したあとに使います。"
        + " グループ／無効化／追加マーカー／Fade・Exit Source At など、いまの作業内容は可能な範囲で維持します（ログは消しません）。"
        + Environment.NewLine
        + "波形の内容が大幅に変わっているとおかしくなることがあるので、そのときは手動ドロップでやり直してください。",
        "Re-read the last loaded WAV/XML from the original files."
        + Environment.NewLine
        + "Use this after updating the wave in a DAW (or similar)."
        + " Current groups, disables, added markers, and Fade / Exit Source At are kept when possible (the log is not cleared)."
        + Environment.NewLine
        + "If the wave content changed substantially, that may look wrong — drop the files again to start fresh.");

    public static string TipExport => Get(
        "[Ctrl+Shift+E] 元 WAV を Originals へコピーし（XML の複数曲は曲ごとに切り出し）、続けて Wwise へインポートします。"
        + Environment.NewLine
        + "無効化した Playlist は書き出し対象外です。",
        "[Ctrl+Shift+E] Copy the source WAV(s) into Originals (XML multi-song masters are split per song) and import them into Wwise."
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
        "元 WAV のコピー先フォルダです。横のフォルダボタンで変更できます。",
        "Folder where source WAV(s) are copied. Change it with the folder button.");

    public static string TipSpectrum => Get(
        "再生出力の簡易スペクトラム表示です。",
        "Simple spectrum meter for playback output.");

    public static string TipLogEditor => Get(
        "操作・EXPORT・接続などのログです。右下のアイコンで消去・コピー・保存できます。"
        + Environment.NewLine
        + "[Ctrl]+ホイール / [Ctrl]+[+][-]: ログと Tips の文字サイズ（6〜18pt。[Ctrl]+[0] で既定 8.5pt。アプリ設定に保存）",
        "Log for operations, EXPORT, and connection. Use the icons to clear, copy, or save."
        + Environment.NewLine
        + "[Ctrl]+wheel / [Ctrl]+[+][-]: log and Tips font size (6–18pt; [Ctrl]+[0] resets to 8.5pt; saved in app settings)");

    public static string TipLogTipsFontSize => Get(
        "[Ctrl]+ホイール / [Ctrl]+[+][-]: ログと Tips の文字サイズ（6〜18pt。[Ctrl]+[0] で既定 8.5pt。アプリ設定に保存）",
        "[Ctrl]+wheel / [Ctrl]+[+][-]: log and Tips font size (6–18pt; [Ctrl]+[0] resets to 8.5pt; saved in app settings)");

    public static string TipLogClear => Get(
        "ログ表示を消去します。",
        "Clear the log display.");

    public static string TipLogCopy => Get(
        "ログ全文をクリップボードへコピーします。",
        "Copy the full log to the clipboard.");

    public static string TipLogDownload => Get(
        "ログをファイルへ保存します。",
        "Save the log to a file.");

    public static string TipCopyright => Get(
        "GitHub を開くか、SIL Open Font License 全文をログに表示します。",
        "Open GitHub, or show the SIL Open Font License text in the log.");

    public static string TipBrandLogo => Get(
        "MIYABI GAME AUDIO のウェブサイトを開きます。",
        "Open the MIYABI GAME AUDIO website.");

    public static string TipPlaylistHeader => Get(
        "遷移先として選ぶ Music Playlist の一覧です。クリックで Fade／Exit Source At を反映し、再生中は遷移を予約します。",
        "List of Music Playlists to jump to. Click to apply Fade / Exit Source At; while playing, schedules a transition.");

    public static string TipPlaylistItem(string playlistName, bool additiveLayers = false) => Format(
        "{0}{1}"
        + "[Shift]+クリック／ドラッグ: グループ化（既存グループも新しい ID で上書き可）{1}"
        + "[Ctrl]+クリック／ドラッグ: グループ解除{1}"
        + "[Ctrl+Shift]+クリック／ドラッグ: 無効化／再有効化{1}"
        + (additiveLayers
            ? "クリック: グループ内で追加再生のオン／オフ（再生中・同一グループ時）"
            : "[Alt]+クリック: グループ内で重ね再生（再クリックで個別停止）"),
        "{0}{1}"
        + "[Shift]+click/drag: group (can overwrite an existing group with a new ID){1}"
        + "[Ctrl]+click/drag: ungroup{1}"
        + "[Ctrl+Shift]+click/drag: disable / re-enable{1}"
        + (additiveLayers
            ? "Click: toggle additive layer playback within a group (while playing in that group)"
            : "[Alt]+click: layer playback within a group (click again to stop that layer)"),
        playlistName,
        Environment.NewLine);

    public static string TipWaveformEditSourceName => Get(
        "ダブルクリックで書き出しファイル名・Playlist名・Switch名を編集",
        "Double-click to edit the export file name, Playlist name, or Switch name");

    public static string TipWaveformDropZone => Get(
        ".wav または .xml をドロップして下さい。"
        + Environment.NewLine
        + "・.wav 1 本（同名 .xml なし）→ Wave 単体モード（埋め込みマーカー。小節線なし）"
        + Environment.NewLine
        + "・.wav 2 本以上（いずれも同名 .xml なし）→ 複数波形モード（仮想タイムラインへ連結）"
        + Environment.NewLine
        + "・同名の .wav + .xml → Nuendo/Cubase XML モード（小節・テンポ・拍子・マーカー）"
        + Environment.NewLine
        + "・.xml のみ → エラー（波形表示には .wav が必要）"
        + Environment.NewLine
        + "・XML あり／なしなど独立ペアが複数 → ログは全部、プレビューは最後の 1 件のみ",
        "Drop .wav or .xml."
        + Environment.NewLine
        + "• One .wav (no matching .xml) → Wave-only mode (embedded markers; no bar lines)"
        + Environment.NewLine
        + "• Two or more .wav (none with matching .xml) → Multi-wave mode (concatenated timeline)"
        + Environment.NewLine
        + "• Matching .wav + .xml → Nuendo/Cubase XML mode (bars, tempo, signature, markers)"
        + Environment.NewLine
        + "• .xml alone → error (.wav is required to show the waveform)"
        + Environment.NewLine
        + "• Multiple independent pairs (e.g. with/without XML) → full log; preview keeps the last one only");

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
        + "[←] / [→]: シークバーを 1px 移動（サンプル点の表示中は 1 サンプル）"
        + Environment.NewLine
        + "[Alt]+[←] / [→]: シーク位置のマーカーを 1px 移動（+[Shift] で 3px、シークも連動）"
        + Environment.NewLine
        + "[Ctrl+Alt]+[←] / [→]: シーク位置のマーカー＋一つ前を 1px 同時移動（+[Shift] で 3px）"
        + Environment.NewLine
        + "▼／コメントをダブルクリック: コメントを編集"
        + Environment.NewLine
        + "[Ctrl+Shift+R] シーク位置のマーカーをリネーム"
        + Environment.NewLine
        + "[Ctrl]+[←] / [→]: 前後の Playlist 先頭、またはマーカーへ移動"
        + Environment.NewLine
        + "[Ctrl+Shift]+[←] / [→]: 前後のマーカーへ移動（Playlist 境界は飛ばす）"
        + Environment.NewLine
        + "[Delete] / [Ctrl+Del] 選択したマーカーを削除（アプリ上のみ）"
        + Environment.NewLine
        + "[Insert] シーク位置にマーカー追加"
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
        + "[←] / [→]: nudge seek bar by 1px (1 sample while sample points are shown)"
        + Environment.NewLine
        + "[Alt]+[←] / [→]: nudge marker at seek by 1px (+[Shift] for 3px; seek follows)"
        + Environment.NewLine
        + "[Ctrl+Alt]+[←] / [→]: nudge marker at seek and the previous one by 1px (+[Shift] for 3px)"
        + Environment.NewLine
        + "Double-click ▼ / comment: edit comment"
        + Environment.NewLine
        + "[Ctrl+Shift+R] rename marker at seek"
        + Environment.NewLine
        + "[Ctrl]+[←] / [→]: jump to previous / next Playlist start or marker"
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
        + "フェード範囲を右クリック: カーブを選択"
        + Environment.NewLine
        + "Fade In は先頭 Music Segment 内、Fade Out は末尾 Music Segment 内に制限（-A/-E は同一セグメント）"
        + Environment.NewLine
        + "EXPORT 時は MusicClip の非破壊フェードとして設定"
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

}
