namespace MgaWwiseIMImporter.Domain;

internal static partial class UiStrings
{
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

}
