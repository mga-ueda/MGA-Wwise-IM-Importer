namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
    // --- Language toggle ---
    public static string TipLanguageJapanese => Get(
        "現在: 日本語。クリックで英語に切り替えます。",
        "Current: Japanese. Click to switch to English.");

    public static string TipLanguageEnglish => Get(
        "現在: 英語。クリックで日本語に切り替えます。",
        "Current: English. Click to switch to Japanese.");

    public static string TipAudioSettings => Get(
        "音声出力・フェードカーブ既定・波形フォーマット規定を設定します。",
        "Configure audio output, default fade curves, and the expected waveform format.");

    public static string AccessibleAudioSettingsButton => Get(
        "設定",
        "Settings");

    public static string DialogSettingsTitle => Get(
        "設定",
        "Settings");

    public static string LabelFadeCurveDefaults => Get(
        "フェードカーブ既定",
        "Default Fade Curves");

    public static string LabelExpectedWaveformFormat => Get(
        "波形フォーマット規定",
        "Expected Waveform Format");

    public static string LabelExpectedSampleRateHz => Get(
        "Sample Rate (Hz)",
        "Sample Rate (Hz)");

    public static string LabelExpectedBitDepth => Get(
        "Bit Depth",
        "Bit Depth");

    public static string LabelExpectedChannels => Get(
        "Channels",
        "Channels");

    public static string TipExpectedWaveformFormat => Get(
        "ドロップした WAV がこの規定と一致しないとき、波形ビューとログを警告色で示します。"
        + " 数値は手入力です（既定 48000 / 24 / 2）。",
        "When a dropped WAV differs from this expected format, the waveform view and log use the warning color."
        + " Enter values manually (default 48000 / 24 / 2).");

    public static string LogWaveFormatOffSpecSuffix => Get(
        "[規定外]",
        "[off-spec]");

    public static string LogWaveFormatOffSpec(string expected, string actual) => Format(
        "Message : 規定フォーマット（{0}）と異なります: {1}",
        "Message : Wave format differs from expected ({0}): {1}",
        expected,
        actual);

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

}
