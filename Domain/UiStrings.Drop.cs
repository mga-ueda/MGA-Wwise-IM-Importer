namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
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

    public static string LogMultiplePairsPreviewDiscarded(int pairCount, string keptWavPath) => Format(
        "Message : 独立したペアが {0} 件あります。波形プレビューに残るのは最後の 1 件だけです（他はログのみ／破棄）。"
        + Environment.NewLine
        + "Message : 採用: {1}",
        "Message : {0} independent pair(s) found. Only the last one is kept in the waveform preview (others are log-only / discarded)."
        + Environment.NewLine
        + "Message : Kept: {1}",
        pairCount,
        keptWavPath);

    public static string LogMultiplePairsMixedXmlModes => Get(
        "Message : 同名 XML ありのペアと無しのペアが混在しています。複数波形モードには連結されません。",
        "Message : Pairs with and without matching XML are mixed. They are not merged into multi-wave mode.");

    public static string LogXmlPairHeader => Get(
        "=== Nuendo/Cubase XML モード ===",
        "=== Nuendo/Cubase XML mode ===");

    public static string LogXmlPairModeName => Get(
        "Mode : WAV + 同名 XML（Nuendo/Cubase トラックリスト）",
        "Mode : WAV + matching XML (Nuendo/Cubase tracklist)");

    public static string LogWaveOnlyHeader => Get(
        "=== Wave 単体モード ===",
        "=== Wave-only mode ===");

    public static string LogWaveOnlyModeName(WaveOnlyMode mode) => mode switch
    {
        WaveOnlyMode.MarkersOnly => Get(
            "Mode : マーカーのみ／無し（cue + adtl）",
            "Mode : Markers only / none (cue + adtl)"),
        WaveOnlyMode.SmplLoop => Get(
            "Mode : サステインループ（smpl）",
            "Mode : Sustain loop (smpl)"),
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
                "Message : サステインループ（smpl）はありません。",
                "Message : No sustain loops (smpl).");
        }

        if (skippedLoopCount == 0)
        {
            return Format(
                "Message : サステインループの Start / End を -L / -E として採用しました（ループ {0} 件）。通常マーカーより優先します。",
                "Message : Applied sustain-loop Start / End as -L / -E ({0} loop(s)). These take priority over normal markers.",
                acceptedLoopCount);
        }

        return Format(
            "Message : サステインループの Start / End を -L / -E として採用しました（採用 {0} 件、範囲外／無効でスキップ {1} 件）。通常マーカーより優先します。",
            "Message : Applied sustain-loop Start / End as -L / -E ({0} accepted, {1} skipped as out of range / invalid). These take priority over normal markers.",
            acceptedLoopCount,
            skippedLoopCount);
    }

    public static string LogWaveOnlyDiscardedEmbeddedSummary(int count) => Format(
        "Message : サステインループの Start / End と同タイミングの埋め込みマーカーを破棄しました（{0} 件）。",
        "Message : Discarded embedded marker(s) at the same timing as sustain-loop Start / End ({0}).",
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

}
