using System.Globalization;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// アプリ全体の作業設定（exe 横 INI の [App]）。プロジェクト切替では変わらない。
/// </summary>
internal sealed class AppSettings
{
    public const string Section = "App";

    public bool AlwaysOnTop { get; set; }

    /// <summary>UI／ログの表示言語（既定 ja）。</summary>
    public UiLanguage UiLanguage { get; set; } = UiLanguage.Japanese;

    /// <summary>
    /// アップデート案内をスキップしたリモート SemVer（空なら未スキップ）。
    /// より新しい版が出たら再度案内する。
    /// </summary>
    public string SkippedUpdateVersion { get; set; } = string.Empty;

    /// <summary>Tips 枠の表示（既定オン）。</summary>
    public bool ShowTips { get; set; } = true;

    /// <summary>再生出力 API（既定 WaveOut）。</summary>
    public AudioOutputApi AudioApi { get; set; } = AudioOutputApi.WaveOut;

    /// <summary>出力デバイス識別子（API 依存。空はシステム既定）。</summary>
    public string AudioDeviceId { get; set; } = string.Empty;

    /// <summary>波形表示エリア高さの倍率（1 / 2 / 3。既定 1）。</summary>
    public int WaveformHeightScale { get; set; } = 1;

    /// <summary>波形リージョン端 Fade In の既定カーブ。</summary>
    public RegionFadeCurveKind DefaultWaveformFadeInCurve { get; set; } =
        RegionEdgeFade.BuiltinWaveformFadeInCurve;

    /// <summary>波形リージョン端 Fade Out の既定カーブ。</summary>
    public RegionFadeCurveKind DefaultWaveformFadeOutCurve { get; set; } =
        RegionEdgeFade.BuiltinWaveformFadeOutCurve;

    /// <summary>Playlist 遷移 Fade In の既定カーブ。</summary>
    public RegionFadeCurveKind DefaultPlaylistFadeInCurve { get; set; } =
        RegionEdgeFade.BuiltinPlaylistFadeInCurve;

    /// <summary>Playlist 遷移 Fade Out の既定カーブ。</summary>
    public RegionFadeCurveKind DefaultPlaylistFadeOutCurve { get; set; } =
        RegionEdgeFade.BuiltinPlaylistFadeOutCurve;

    /// <summary>規定 Sample Rate（Hz。既定 48000）。</summary>
    public uint ExpectedSampleRateHz { get; set; } = ExpectedWaveformFormat.Default.SampleRateHz;

    /// <summary>規定 Bit Depth（既定 24）。</summary>
    public ushort ExpectedBitsPerSample { get; set; } = ExpectedWaveformFormat.Default.BitsPerSample;

    /// <summary>規定チャンネル数（既定 2）。</summary>
    public ushort ExpectedChannels { get; set; } = ExpectedWaveformFormat.Default.Channels;

    public AudioOutputSettings ToAudioOutputSettings() => new(AudioApi, AudioDeviceId ?? string.Empty);

    public ExpectedWaveformFormat ToExpectedWaveformFormat() =>
        ExpectedWaveformFormat.Normalize(
            (int)ExpectedSampleRateHz,
            ExpectedBitsPerSample,
            ExpectedChannels);

    public static AppSettings Load()
    {
        var values = IniFile.ReadSection(Section);
        var settings = Parse(values);
        if (!HasKnownKeys(values))
        {
            settings.Save();
        }

        return settings;
    }

    public void Save() => IniFile.WriteSection(Section, ToDictionary());

    public void SaveAlwaysOnTop(bool enabled)
    {
        AlwaysOnTop = enabled;
        Save();
    }

    public void SaveUiLanguage(UiLanguage language)
    {
        UiLanguage = language;
        Save();
    }

    public void SaveSkippedUpdateVersion(string? semVer)
    {
        SkippedUpdateVersion = AppVersion.NormalizeTag(semVer);
        Save();
    }

    public void SaveAudioOutput(AudioOutputApi api, string? deviceId)
    {
        AudioApi = api;
        AudioDeviceId = deviceId ?? string.Empty;
        Save();
    }

    public void SaveShowTips(bool enabled)
    {
        ShowTips = enabled;
        Save();
    }

    public void SaveWaveformHeightScale(int scale)
    {
        WaveformHeightScale = NormalizeWaveformHeightScale(scale);
        Save();
    }

    public void SaveDefaultFadeCurves(
        RegionFadeCurveKind waveformFadeIn,
        RegionFadeCurveKind waveformFadeOut,
        RegionFadeCurveKind playlistFadeIn,
        RegionFadeCurveKind playlistFadeOut)
    {
        DefaultWaveformFadeInCurve = waveformFadeIn;
        DefaultWaveformFadeOutCurve = waveformFadeOut;
        DefaultPlaylistFadeInCurve = playlistFadeIn;
        DefaultPlaylistFadeOutCurve = playlistFadeOut;
        Save();
    }

    public void SaveExpectedWaveformFormat(ExpectedWaveformFormat format)
    {
        var normalized = ExpectedWaveformFormat.Normalize(
            (int)format.SampleRateHz,
            format.BitsPerSample,
            format.Channels);
        ExpectedSampleRateHz = normalized.SampleRateHz;
        ExpectedBitsPerSample = normalized.BitsPerSample;
        ExpectedChannels = normalized.Channels;
        Save();
    }

    private Dictionary<string, string> ToDictionary() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["AlwaysOnTop"] = AlwaysOnTop ? "1" : "0",
        ["UiLanguage"] = UiStrings.ToIniValue(UiLanguage),
        ["SkippedUpdateVersion"] = SkippedUpdateVersion ?? string.Empty,
        ["ShowTips"] = ShowTips ? "1" : "0",
        ["AudioApi"] = AudioOutputSettings.ToIniValue(AudioApi),
        ["AudioDeviceId"] = AudioDeviceId ?? string.Empty,
        ["WaveformHeightScale"] = WaveformHeightScale.ToString(CultureInfo.InvariantCulture),
        ["DefaultWaveformFadeInCurve"] = DefaultWaveformFadeInCurve.ToString(),
        ["DefaultWaveformFadeOutCurve"] = DefaultWaveformFadeOutCurve.ToString(),
        ["DefaultPlaylistFadeInCurve"] = DefaultPlaylistFadeInCurve.ToString(),
        ["DefaultPlaylistFadeOutCurve"] = DefaultPlaylistFadeOutCurve.ToString(),
        ["ExpectedSampleRateHz"] = ExpectedSampleRateHz.ToString(CultureInfo.InvariantCulture),
        ["ExpectedBitsPerSample"] = ExpectedBitsPerSample.ToString(CultureInfo.InvariantCulture),
        ["ExpectedChannels"] = ExpectedChannels.ToString(CultureInfo.InvariantCulture),
    };

    private static AppSettings Parse(Dictionary<string, string> values) => new()
    {
        AlwaysOnTop = IniFile.ReadBool(values, "AlwaysOnTop", defaultValue: false),
        UiLanguage = values.TryGetValue("UiLanguage", out var languageText)
            ? UiStrings.ParseLanguage(languageText)
            : UiLanguage.Japanese,
        SkippedUpdateVersion = values.TryGetValue("SkippedUpdateVersion", out var skipped)
            ? AppVersion.NormalizeTag(skipped)
            : string.Empty,
        ShowTips = IniFile.ReadBool(values, "ShowTips", defaultValue: true),
        AudioApi = values.TryGetValue("AudioApi", out var audioApiText)
            ? AudioOutputSettings.ParseApi(audioApiText)
            : AudioOutputApi.WaveOut,
        AudioDeviceId = values.TryGetValue("AudioDeviceId", out var deviceId)
            ? deviceId
            : string.Empty,
        WaveformHeightScale = values.TryGetValue("WaveformHeightScale", out var scaleText)
            ? NormalizeWaveformHeightScale(scaleText)
            : 1,
        DefaultWaveformFadeInCurve = ParseFadeCurve(
            values,
            "DefaultWaveformFadeInCurve",
            RegionEdgeFade.BuiltinWaveformFadeInCurve),
        DefaultWaveformFadeOutCurve = ParseFadeCurve(
            values,
            "DefaultWaveformFadeOutCurve",
            RegionEdgeFade.BuiltinWaveformFadeOutCurve),
        DefaultPlaylistFadeInCurve = ParseFadeCurve(
            values,
            "DefaultPlaylistFadeInCurve",
            RegionEdgeFade.BuiltinPlaylistFadeInCurve),
        DefaultPlaylistFadeOutCurve = ParseFadeCurve(
            values,
            "DefaultPlaylistFadeOutCurve",
            RegionEdgeFade.BuiltinPlaylistFadeOutCurve),
        ExpectedSampleRateHz = ParseExpectedUInt(
            values,
            "ExpectedSampleRateHz",
            ExpectedWaveformFormat.Default.SampleRateHz),
        ExpectedBitsPerSample = (ushort)ParseExpectedUInt(
            values,
            "ExpectedBitsPerSample",
            ExpectedWaveformFormat.Default.BitsPerSample),
        ExpectedChannels = (ushort)ParseExpectedUInt(
            values,
            "ExpectedChannels",
            ExpectedWaveformFormat.Default.Channels),
    };

    private static bool HasKnownKeys(Dictionary<string, string> values) =>
        values.ContainsKey("AlwaysOnTop")
        || values.ContainsKey("UiLanguage")
        || values.ContainsKey("SkippedUpdateVersion")
        || values.ContainsKey("ShowTips")
        || values.ContainsKey("AudioApi")
        || values.ContainsKey("AudioDeviceId")
        || values.ContainsKey("WaveformHeightScale")
        || values.ContainsKey("DefaultWaveformFadeInCurve")
        || values.ContainsKey("DefaultWaveformFadeOutCurve")
        || values.ContainsKey("DefaultPlaylistFadeInCurve")
        || values.ContainsKey("DefaultPlaylistFadeOutCurve")
        || values.ContainsKey("ExpectedSampleRateHz")
        || values.ContainsKey("ExpectedBitsPerSample")
        || values.ContainsKey("ExpectedChannels");

    private static uint ParseExpectedUInt(
        Dictionary<string, string> values,
        string key,
        uint fallback)
    {
        if (!values.TryGetValue(key, out var text)
            || !uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return fallback;
        }

        return key switch
        {
            "ExpectedSampleRateHz" => ExpectedWaveformFormat.Normalize((int)value, 24, 2).SampleRateHz,
            "ExpectedBitsPerSample" => ExpectedWaveformFormat.Normalize(48_000, (int)value, 2).BitsPerSample,
            "ExpectedChannels" => ExpectedWaveformFormat.Normalize(48_000, 24, (int)value).Channels,
            _ => value,
        };
    }

    private static RegionFadeCurveKind ParseFadeCurve(
        Dictionary<string, string> values,
        string key,
        RegionFadeCurveKind fallback) =>
        values.TryGetValue(key, out var text)
        && Enum.TryParse<RegionFadeCurveKind>(text, ignoreCase: true, out var kind)
            ? kind
            : fallback;

    /// <summary>波形高さ倍率を 1〜3 に正規化する。</summary>
    public static int NormalizeWaveformHeightScale(int scale) =>
        scale is >= 1 and <= 3 ? scale : 1;

    private static int NormalizeWaveformHeightScale(string? text)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var scale))
        {
            return NormalizeWaveformHeightScale(scale);
        }

        return 1;
    }
}
