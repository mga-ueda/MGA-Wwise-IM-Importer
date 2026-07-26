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

    /// <summary>ツールチップ表示（既定オン）。</summary>
    public bool ShowToolTips { get; set; } = true;

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

    public AudioOutputSettings ToAudioOutputSettings() => new(AudioApi, AudioDeviceId ?? string.Empty);

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

    public void Save() => WriteValues(ToDictionary());

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

    public void SaveShowToolTips(bool enabled)
    {
        ShowToolTips = enabled;
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

    private Dictionary<string, string> ToDictionary() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["AlwaysOnTop"] = AlwaysOnTop ? "1" : "0",
        ["UiLanguage"] = UiStrings.ToIniValue(UiLanguage),
        ["SkippedUpdateVersion"] = SkippedUpdateVersion ?? string.Empty,
        ["ShowToolTips"] = ShowToolTips ? "1" : "0",
        ["AudioApi"] = AudioOutputSettings.ToIniValue(AudioApi),
        ["AudioDeviceId"] = AudioDeviceId ?? string.Empty,
        ["WaveformHeightScale"] = WaveformHeightScale.ToString(CultureInfo.InvariantCulture),
        ["DefaultWaveformFadeInCurve"] = DefaultWaveformFadeInCurve.ToString(),
        ["DefaultWaveformFadeOutCurve"] = DefaultWaveformFadeOutCurve.ToString(),
        ["DefaultPlaylistFadeInCurve"] = DefaultPlaylistFadeInCurve.ToString(),
        ["DefaultPlaylistFadeOutCurve"] = DefaultPlaylistFadeOutCurve.ToString(),
    };

    private static void WriteValues(Dictionary<string, string> values)
    {
        IniFile.WriteSection(Section, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlwaysOnTop"] = values.TryGetValue("AlwaysOnTop", out var alwaysOnTop) ? alwaysOnTop : "0",
            ["UiLanguage"] = values.TryGetValue("UiLanguage", out var language) ? language : "ja",
            ["SkippedUpdateVersion"] = values.TryGetValue("SkippedUpdateVersion", out var skipped)
                ? skipped
                : string.Empty,
            ["ShowToolTips"] = values.TryGetValue("ShowToolTips", out var showToolTips) ? showToolTips : "1",
            ["AudioApi"] = values.TryGetValue("AudioApi", out var audioApi) ? audioApi : "WaveOut",
            ["AudioDeviceId"] = values.TryGetValue("AudioDeviceId", out var deviceId) ? deviceId : string.Empty,
            ["WaveformHeightScale"] = values.TryGetValue("WaveformHeightScale", out var scale)
                ? NormalizeWaveformHeightScale(scale).ToString(CultureInfo.InvariantCulture)
                : "1",
            ["DefaultWaveformFadeInCurve"] = values.TryGetValue("DefaultWaveformFadeInCurve", out var wIn)
                ? wIn
                : RegionEdgeFade.BuiltinWaveformFadeInCurve.ToString(),
            ["DefaultWaveformFadeOutCurve"] = values.TryGetValue("DefaultWaveformFadeOutCurve", out var wOut)
                ? wOut
                : RegionEdgeFade.BuiltinWaveformFadeOutCurve.ToString(),
            ["DefaultPlaylistFadeInCurve"] = values.TryGetValue("DefaultPlaylistFadeInCurve", out var pIn)
                ? pIn
                : RegionEdgeFade.BuiltinPlaylistFadeInCurve.ToString(),
            ["DefaultPlaylistFadeOutCurve"] = values.TryGetValue("DefaultPlaylistFadeOutCurve", out var pOut)
                ? pOut
                : RegionEdgeFade.BuiltinPlaylistFadeOutCurve.ToString(),
        });
    }

    private static AppSettings Parse(Dictionary<string, string> values) => new()
    {
        AlwaysOnTop = ReadBool(values, "AlwaysOnTop", defaultValue: false),
        UiLanguage = values.TryGetValue("UiLanguage", out var languageText)
            ? UiStrings.ParseLanguage(languageText)
            : UiLanguage.Japanese,
        SkippedUpdateVersion = values.TryGetValue("SkippedUpdateVersion", out var skipped)
            ? AppVersion.NormalizeTag(skipped)
            : string.Empty,
        ShowToolTips = ReadBool(values, "ShowToolTips", defaultValue: true),
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
    };

    private static bool HasKnownKeys(Dictionary<string, string> values) =>
        values.ContainsKey("AlwaysOnTop")
        || values.ContainsKey("UiLanguage")
        || values.ContainsKey("SkippedUpdateVersion")
        || values.ContainsKey("ShowToolTips")
        || values.ContainsKey("AudioApi")
        || values.ContainsKey("AudioDeviceId")
        || values.ContainsKey("WaveformHeightScale")
        || values.ContainsKey("DefaultWaveformFadeInCurve")
        || values.ContainsKey("DefaultWaveformFadeOutCurve")
        || values.ContainsKey("DefaultPlaylistFadeInCurve")
        || values.ContainsKey("DefaultPlaylistFadeOutCurve");

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

    private static bool ReadBool(Dictionary<string, string> values, string key, bool defaultValue)
    {
        if (!values.TryGetValue(key, out var text))
        {
            return defaultValue;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number != 0;
        }

        return bool.TryParse(text, out var flag) ? flag : defaultValue;
    }
}
