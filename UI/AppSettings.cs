using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// アプリ全体の作業設定（AppData の settings.json / app）。プロジェクト切替では変わらない。
/// </summary>
internal sealed class AppSettings
{
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

    /// <summary>メトロノーム音量（0.1〜1.0。既定 0.3）。</summary>
    public float MetronomeVolume { get; set; } = MetronomePlayer.DefaultVolume;

    /// <summary>Music Track ストリーミング有効（既定オン。CLEAR では戻さない）。</summary>
    public bool StreamEnabled { get; set; } = true;

    /// <summary>2 番目以降セグメントの Look-ahead（ms。既定 500）。</summary>
    public int LookAheadMs { get; set; } = 500;

    /// <summary>先頭セグメント Prefetch Length（ms。既定 500）。</summary>
    public int PrefetchLengthMs { get; set; } = 500;

    /// <summary>Keep Layer Balance（既定オフ。CLEAR では戻さない）。</summary>
    public bool LoudnessPreserveGroupBalance { get; set; }

    public AudioOutputSettings ToAudioOutputSettings() => new(AudioApi, AudioDeviceId ?? string.Empty);

    public ExpectedWaveformFormat ToExpectedWaveformFormat() =>
        ExpectedWaveformFormat.Normalize(
            (int)ExpectedSampleRateHz,
            ExpectedBitsPerSample,
            ExpectedChannels);

    public static AppSettings Load()
    {
        var data = JsonSettingsStore.Document.App;
        var needsStreamingMigrate = data.StreamEnabled is null
            && data.LookAheadMs is null
            && data.PrefetchLengthMs is null
            && data.LoudnessPreserveGroupBalance is null;
        var settings = FromData(data, migrateStreamingFromProjects: needsStreamingMigrate);
        if (needsStreamingMigrate)
        {
            // 移行結果を app へ書き、次回以降はプロジェクト既定で上書きしない。
            settings.Save();
        }

        return settings;
    }

    public void Save()
    {
        var data = ToData();
        JsonSettingsStore.Update(doc => doc.App = data);
    }

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

    public void SaveMetronomeVolume(float volume)
    {
        MetronomeVolume = NormalizeMetronomeVolume(volume);
        Save();
    }

    public void SaveStreamingOptions(
        bool streamEnabled,
        int lookAheadMs,
        int prefetchLengthMs,
        bool loudnessPreserveGroupBalance)
    {
        StreamEnabled = streamEnabled;
        LookAheadMs = Math.Clamp(lookAheadMs, 0, 9999);
        PrefetchLengthMs = Math.Clamp(prefetchLengthMs, 0, 9999);
        LoudnessPreserveGroupBalance = loudnessPreserveGroupBalance;
        Save();
    }

    private AppSettingsData ToData() => new()
    {
        AlwaysOnTop = AlwaysOnTop,
        UiLanguage = UiStrings.ToStoredValue(UiLanguage),
        SkippedUpdateVersion = SkippedUpdateVersion ?? string.Empty,
        ShowTips = ShowTips,
        AudioApi = AudioOutputSettings.ToStoredValue(AudioApi),
        AudioDeviceId = AudioDeviceId ?? string.Empty,
        WaveformHeightScale = WaveformHeightScale,
        DefaultWaveformFadeInCurve = DefaultWaveformFadeInCurve.ToString(),
        DefaultWaveformFadeOutCurve = DefaultWaveformFadeOutCurve.ToString(),
        DefaultPlaylistFadeInCurve = DefaultPlaylistFadeInCurve.ToString(),
        DefaultPlaylistFadeOutCurve = DefaultPlaylistFadeOutCurve.ToString(),
        ExpectedSampleRateHz = ExpectedSampleRateHz,
        ExpectedBitsPerSample = ExpectedBitsPerSample,
        ExpectedChannels = ExpectedChannels,
        MetronomeVolume = MetronomeVolume,
        StreamEnabled = StreamEnabled,
        LookAheadMs = LookAheadMs,
        PrefetchLengthMs = PrefetchLengthMs,
        LoudnessPreserveGroupBalance = LoudnessPreserveGroupBalance,
    };

    private static AppSettings FromData(AppSettingsData data, bool migrateStreamingFromProjects)
    {
        var settings = new AppSettings
        {
            AlwaysOnTop = data.AlwaysOnTop,
            UiLanguage = UiStrings.ParseLanguage(data.UiLanguage),
            SkippedUpdateVersion = AppVersion.NormalizeTag(data.SkippedUpdateVersion),
            ShowTips = data.ShowTips,
            AudioApi = AudioOutputSettings.ParseApi(data.AudioApi),
            AudioDeviceId = data.AudioDeviceId ?? string.Empty,
            WaveformHeightScale = NormalizeWaveformHeightScale(data.WaveformHeightScale),
            DefaultWaveformFadeInCurve = ParseFadeCurve(
                data.DefaultWaveformFadeInCurve,
                RegionEdgeFade.BuiltinWaveformFadeInCurve),
            DefaultWaveformFadeOutCurve = ParseFadeCurve(
                data.DefaultWaveformFadeOutCurve,
                RegionEdgeFade.BuiltinWaveformFadeOutCurve),
            DefaultPlaylistFadeInCurve = ParseFadeCurve(
                data.DefaultPlaylistFadeInCurve,
                RegionEdgeFade.BuiltinPlaylistFadeInCurve),
            DefaultPlaylistFadeOutCurve = ParseFadeCurve(
                data.DefaultPlaylistFadeOutCurve,
                RegionEdgeFade.BuiltinPlaylistFadeOutCurve),
            MetronomeVolume = NormalizeMetronomeVolume(data.MetronomeVolume),
        };

        var format = ExpectedWaveformFormat.Normalize(
            (int)data.ExpectedSampleRateHz,
            data.ExpectedBitsPerSample,
            data.ExpectedChannels);
        settings.ExpectedSampleRateHz = format.SampleRateHz;
        settings.ExpectedBitsPerSample = format.BitsPerSample;
        settings.ExpectedChannels = format.Channels;

        if (migrateStreamingFromProjects)
        {
            TryMigrateStreamingFromProjects(settings);
        }
        else
        {
            settings.StreamEnabled = data.StreamEnabled ?? true;
            settings.LookAheadMs = Math.Clamp(data.LookAheadMs ?? 500, 0, 9999);
            settings.PrefetchLengthMs = Math.Clamp(data.PrefetchLengthMs ?? 500, 0, 9999);
            settings.LoudnessPreserveGroupBalance = data.LoudnessPreserveGroupBalance ?? false;
        }

        return settings;
    }

    private static void TryMigrateStreamingFromProjects(AppSettings settings)
    {
        var projects = JsonSettingsStore.Document.Projects;
        if (projects?.Items is not { Count: > 0 })
        {
            return;
        }

        var active = projects.Active?.Trim() ?? string.Empty;
        var source = projects.Items.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(active)
                && string.Equals(p.Name, active, StringComparison.OrdinalIgnoreCase))
            ?? projects.Items[0];

        settings.StreamEnabled = source.StreamEnabled;
        settings.LookAheadMs = Math.Clamp(source.LookAheadMs, 0, 9999);
        settings.PrefetchLengthMs = Math.Clamp(source.PrefetchLengthMs, 0, 9999);
        settings.LoudnessPreserveGroupBalance = source.LoudnessPreserveGroupBalance;
    }

    private static RegionFadeCurveKind ParseFadeCurve(string? text, RegionFadeCurveKind fallback) =>
        !string.IsNullOrWhiteSpace(text)
        && Enum.TryParse<RegionFadeCurveKind>(text, ignoreCase: true, out var kind)
            ? kind
            : fallback;

    /// <summary>波形高さ倍率を 1〜3 に正規化する。</summary>
    public static int NormalizeWaveformHeightScale(int scale) =>
        scale is >= 1 and <= 3 ? scale : 1;

    /// <summary>メトロノーム音量を 0.1〜1.0（10% 刻み）に正規化する。</summary>
    public static float NormalizeMetronomeVolume(float volume)
    {
        var clamped = Math.Clamp(volume, MetronomePlayer.MinVolume, MetronomePlayer.MaxVolume);
        var stepped = MathF.Round(clamped / MetronomePlayer.VolumeStep) * MetronomePlayer.VolumeStep;
        return Math.Clamp(stepped, MetronomePlayer.MinVolume, MetronomePlayer.MaxVolume);
    }
}
