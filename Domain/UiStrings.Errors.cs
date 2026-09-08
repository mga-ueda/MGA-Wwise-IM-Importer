namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
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

    public static string ErrMarkerTrackLinearTimeBase => Get(
        "マーカートラックがリニア時間基準（時計）のため、テンポ／拍子が XML に含まれていません。"
        + "ミュージカル（音符）に切り替えてから、マーカートラックを XML で書き出してください。",
        "The marker track is in Linear time base (clock), so the XML has no tempo or time signature. "
        + "Switch it to Musical (note) and export the marker track as XML again.");

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

    public static string ErrPlaybackMemoryTooLong => Get(
        "再生用にメモリへ展開するには波形が長すぎます。",
        "The wave is too long to load into memory for playback.");

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

    public static string ErrWwiseObjectNameRejectedHeader(string name) => Format(
        "Wwise が「{0}」を作成できませんでした。",
        "Wwise could not create “{0}”.",
        name);

    public static string ErrWwiseObjectNameRejectedDigit => Get(
        "・Wwise は先頭が数字の名前を拒否します。Music Switch / Playlist / Segment / Track / State Group のいずれも付けられません。",
        "• Wwise rejects names that start with a digit. This applies to Music Switch, Playlist, Segment, Track, and State Group.");

    public static string ErrWwiseObjectNameRejectedTwoByte => Get(
        "・State Group 名は 2 バイト文字を扱えません。Wwise が _ に置換するため、作ったオブジェクトをパスで参照できなくなります。",
        "• State Group names cannot use 2-byte characters. Wwise replaces them with _, so the created object cannot be found by path.");

    public static string ErrWwiseObjectNameRejectedFallbackLimit(string name) => Format(
        "Switch State を Music_01 などに差し替える処理は、Playlist 切替用の State 名だけです。"
        + Environment.NewLine
        + "Segment 名（{0}_1 など）や State Group 名には効かないため、この名前ではインポートを完了できません。",
        "Falling back Switch States to Music_01 etc. only changes Playlist switch State names."
        + Environment.NewLine
        + "It does not apply to Segment names (such as {0}_1) or the State Group name, so import cannot finish with this name.",
        name);

    public static string ErrWwiseObjectNameRejectedWhatToDo => Get(
        "対応: 波形名をダブルクリックして、先頭が数字でない名前に変えてから EXPORT し直してください。"
        + Environment.NewLine
        + "State Group にも同じ名前を使う場合は、ASCII のみにしてください（例: bgm_test）。",
        "What to do: double-click the waveform name, change it so it does not start with a digit, then EXPORT again."
        + Environment.NewLine
        + "If the same name is also used as a State Group, use ASCII only (for example bgm_test).");
}
