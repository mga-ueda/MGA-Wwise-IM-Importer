namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Marker options (existing) ---
    public static string TipStreamHeader => Get(
        "Wwise Music Track のストリーミング関連設定です。",
        "Streaming settings for Wwise Music Tracks.");

    public static string TipStreamEnabled => Get(
        "オンの場合、Music Track をストリーミング有効で作成します（既定オン）。"
        + " オフのときは Look-ahead Time／Prefetch Length は適用されません。"
        + " アプリ設定として保存され、CLEAR では既定に戻りません。",
        "When on, create Music Tracks with streaming enabled (default on)."
        + " When off, Look-ahead Time / Prefetch Length are not applied."
        + " Saved as an app setting; CLEAR does not reset it.");

    public static string TipLookAheadLabel => Get(
        "2 番目以降のセグメントの Look-ahead Time（ms、0〜9999。既定 500）。"
        + " Stream オン時のみ有効。"
        + " 先頭セグメント内の全トラック（グループ化レイヤー含む）は Zero latency のため UI 値ではなく 50ms 固定です"
        + "（極端な音量低下時の減衰追従用）。アプリ設定（CLEAR では維持）。",
        "Look-ahead Time for the 2nd and later segments (ms, 0–9999, default 500)."
        + " Only when Stream is on."
        + " All tracks in the first segment (including layered groups) use Zero latency with a fixed 50 ms"
        + " Look-ahead (not the UI value), to keep up with extreme volume drops."
        + " App setting (kept on CLEAR).");

    public static string TipLookAheadBox => Get(
        "Look-ahead Time（ms）。0〜9999。既定は 500 です。Stream オン時のみ有効。"
        + " 先頭セグメントには適用されません（固定 50ms）。アプリ設定（CLEAR では維持）。",
        "Look-ahead Time (ms). 0–9999. Default 500. Only when Stream is on."
        + " Not applied to the first segment (fixed 50 ms). App setting (kept on CLEAR).");

    public static string TipLookAheadUnit => Get(
        "単位はミリ秒（ms）です。",
        "Unit is milliseconds (ms).");

    public static string TipPrefetchLabel => Get(
        "Playlist 先頭セグメント内の全トラック（グループ化レイヤー含む）の Prefetch Length（ms、0〜9999。既定 500）。"
        + " Stream オン時のみ有効。Zero latency と同じ範囲に適用します。アプリ設定（CLEAR では維持）。",
        "Prefetch Length for all tracks in the first playlist segment, including layered groups"
        + " (ms, 0–9999, default 500). Only when Stream is on. Same scope as Zero latency."
        + " App setting (kept on CLEAR).");

    public static string TipPrefetchBox => Get(
        "Prefetch Length（ms）。0〜9999。既定は 500 です。"
        + " Playlist 先頭セグメント内の全トラックに反映されます。Stream オン時のみ有効。"
        + " アプリ設定（CLEAR では維持）。",
        "Prefetch Length (ms). 0–9999. Default 500."
        + " Applied to all tracks in the first playlist segment. Only when Stream is on."
        + " App setting (kept on CLEAR).");

    public static string TipPrefetchUnit => Get(
        "単位はミリ秒（ms）です。",
        "Unit is milliseconds (ms).");

    public static string TipLoudnessHeader => Get(
        "Layer Music Option。"
        + " Wwise の Loudness Normalization を利用しているときはオンを推奨します。"
        + " グループ内の相対バランスを、Music Track の Make-Up Gain で非破壊維持します。",
        "Layer Music Option."
        + " Recommended on when using Wwise Loudness Normalization."
        + " Keeps relative balance within a group nondestructively via Music Track Make-Up Gain"
        + " (not baked into WAV).");

    public static string TipLoudnessGroupBalance => Get(
        "オンの場合、グループ内各パートの Integrated Loudness（LKFS）を計測し、"
        + "最も大きいパートの Make-Up Gain を 0 dB、それ以外は相対差だけ下げます（既定オフ）。"
        + " Wwise の Loudness Normalization 利用時にオンを推奨。"
        + " 補正は Music Track の Make-Up Gain へ非破壊で書き込みます。"
        + " グループ（2 パート以上）が無いときは操作できません。"
        + " アプリ設定として保存され、CLEAR では既定に戻りません。",
        "When on, measures each grouped part’s Integrated Loudness (LKFS),"
        + " sets Make-Up Gain of the loudest part to 0 dB and lowers the others by the relative difference"
        + " (default off)."
        + " Recommended when using Wwise Loudness Normalization."
        + " Writes Make-Up Gain on the Music Track nondestructively (not baked into WAV)."
        + " Disabled when no group of 2+ parts exists."
        + " Saved as an app setting; CLEAR does not reset it.");

    public static string TipAdditiveLayers => Get(
        "グループをレイヤー切り替えではなく追加再生タイプとして扱います（既定オフ・グループ単位で記憶）。"
        + " オン時、再生中に同一グループの Playlist をクリックすると追加再生のオン／オフができます。"
        + " EXPORT 時、State Volume は累積再生（例: 2 レイヤーなら A=1 本のみ、B=2 本同時）になるよう設定します。"
        + " グループ（2 パート以上）を選んでいるときだけ操作できます。",
        "Treats the group as additive playback instead of exclusive layer switching"
        + " (default off; remembered per group)."
        + " When on, click a playlist in the same group while playing to toggle additive layers (no Alt needed)."
        + " On EXPORT, State Volumes are set for cumulative playback"
        + " (e.g. 2 layers: A = first only, B = both)."
        + " Enabled only when a group of 2+ parts is selected.");

    public static string TipMoreOptionsHeader => Get(
        "Stream／Layer Music Option／Marker Comment／Marker Grid を開閉します（既定は開いた状態）。"
        + " 開閉状態はプロジェクト設定へ自動保存。"
        + " Stream／Keep Layer Balance の値自体はアプリ設定です。",
        "Expand/collapse Stream / Layer Music Option / Marker Comment / Marker Grid (default open)."
        + " Expansion is saved per project."
        + " Stream / Keep Layer Balance values themselves are app settings.");


    public static string TipMarkerGridHeader => Get(
        "マーカーをドラッグで付与するときのスナップ間隔を指定します。",
        "Snap interval when dragging markers. Does not affect grid line drawing.");

    public static string TipMarkerGridTimeline => Get(
        "現在タイムラインに表示されているグリッドへスナップします。",
        "Snap to the grid currently shown on the timeline.");

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
        "連番の桁数を 0～6 で指定します（+/- で変更、既定 3）。"
        + " 0 の場合は連番自体を付けません。"
        + " 1 以上のときは、その桁で表せる最大値までしかマーカーを追加できません（例: 3 → 999 件）。",
        "Digit count 0–6 (+/−, default 3)."
        + " 0 disables numbering."
        + " When 1+, you can only add as many markers as that digit width allows (e.g. 3 → 999).");

    public static string TipCommentDigitsBox => Get(
        "連番の桁数です。+/- で 0～6 を選べます（既定 3）。"
        + " 0 で連番なし、1～6 で連番ありになります。"
        + " 桁数を超える連番は追加できません。",
        "Digit count. Use +/− for 0–6 (default 3)."
        + " 0 = no number; 1–6 enables numbering."
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
        "入力がある場合、連番の前に接頭語を追加します。Digits が 0 のときは必須です。",
        "Optional prefix before the number. Required when Digits is 0.");

    public static string TipCommentPrefixBox => Get(
        "Custom Cue 名の先頭に付ける文字列を入力します。空欄なら接頭語なし。"
        + " Digits が 0 のときは必須です。",
        "Text prepended to the Custom Cue name. Empty = no prefix."
        + " Required when Digits is 0.");

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

}
