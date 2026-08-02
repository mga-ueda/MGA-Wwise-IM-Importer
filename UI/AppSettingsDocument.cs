using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>%AppData% の settings.json ルート。</summary>
internal sealed class AppSettingsDocument
{
    public AppSettingsData App { get; set; } = new();

    public WindowSettingsData? Window { get; set; }

    public DeveloperSettingsData Developer { get; set; } = new();

    public ProjectsSettingsData Projects { get; set; } = new();

    /// <summary>DEBUG 色パネル用。Release では null／無視。</summary>
    public Dictionary<string, string>? Colors { get; set; }
}

internal sealed class AppSettingsData
{
    public bool AlwaysOnTop { get; set; }

    public string UiLanguage { get; set; } = "ja";

    public string SkippedUpdateVersion { get; set; } = string.Empty;

    public bool ShowTips { get; set; } = true;

    public string AudioApi { get; set; } = "WaveOut";

    public string AudioDeviceId { get; set; } = string.Empty;

    public int WaveformHeightScale { get; set; } = 1;

    public string DefaultWaveformFadeInCurve { get; set; } =
        nameof(RegionFadeCurveKind.SCurve);

    public string DefaultWaveformFadeOutCurve { get; set; } =
        nameof(RegionFadeCurveKind.SCurve);

    public string DefaultPlaylistFadeInCurve { get; set; } =
        nameof(RegionFadeCurveKind.SCurve);

    public string DefaultPlaylistFadeOutCurve { get; set; } =
        nameof(RegionFadeCurveKind.SCurve);

    public uint ExpectedSampleRateHz { get; set; } = 48_000;

    public ushort ExpectedBitsPerSample { get; set; } = 24;

    public ushort ExpectedChannels { get; set; } = 2;

    public float MetronomeVolume { get; set; } = 0.3f;
}

internal sealed class WindowSettingsData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

internal sealed class DeveloperSettingsData
{
    public bool DetailedPlaybackLog { get; set; } = true;

    public int UiScaleSimulateDpi { get; set; }
}

internal sealed class ProjectsSettingsData
{
    public string Active { get; set; } = ProjectSettingsStore.DefaultName;

    public List<ProjectProfileData> Items { get; set; } = [];
}

/// <summary>JSON 用のプロジェクトプロファイル（有効フラグは文字列長から復元）。</summary>
internal sealed class ProjectProfileData
{
    public string Name { get; set; } = ProjectSettingsStore.DefaultName;

    public double FadeInSeconds { get; set; }

    public double FadeOutSeconds { get; set; }

    public string FadeInCurve { get; set; } = nameof(RegionFadeCurveKind.SCurve);

    public string FadeOutCurve { get; set; } = nameof(RegionFadeCurveKind.SCurve);

    public string ExitSourceAt { get; set; } = nameof(PlaylistExitSourceMode.Immediate);

    public bool PlayPostExit { get; set; } = true;

    public string GridOverride { get; set; } = nameof(MarkerGridOverrideMode.Bar);

    public int CommentDigits { get; set; } = 3;

    public bool CommentZeroPad { get; set; } = true;

    public string CommentPrefix { get; set; } = string.Empty;

    public string CommentSuffix { get; set; } = string.Empty;

    public string CommentJoiner { get; set; } = string.Empty;

    public bool CommentResetPerPart { get; set; } = true;

    public bool CompactFileNumbers { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;

    public bool StreamEnabled { get; set; } = true;

    public bool AutoActive { get; set; } = true;

    public int LookAheadMs { get; set; } = 500;

    public int PrefetchLengthMs { get; set; } = 500;

    public bool LoudnessPreserveGroupBalance { get; set; }

    public bool MoreOptionsExpanded { get; set; } = true;

    public bool KeepLastSession { get; set; } = true;

    public string LastWavePath { get; set; } = string.Empty;

    public string LastWavePaths { get; set; } = string.Empty;

    public bool KeepTarget { get; set; }

    public string KeptTargetPath { get; set; } = string.Empty;

    public string KeptTargetProjectFilePath { get; set; } = string.Empty;

    public static ProjectProfileData FromProfile(ProjectProfile profile) => new()
    {
        Name = profile.Name,
        FadeInSeconds = profile.FadeInSeconds,
        FadeOutSeconds = profile.FadeOutSeconds,
        FadeInCurve = profile.FadeInCurve,
        FadeOutCurve = profile.FadeOutCurve,
        ExitSourceAt = profile.ExitSourceAt.ToString(),
        PlayPostExit = profile.PlayPostExit,
        GridOverride = profile.GridOverride.ToString(),
        CommentDigits = Math.Clamp(
            profile.CommentDigits,
            MarkerSettings.CommentDigitsMin,
            MarkerSettings.CommentDigitsMax),
        CommentZeroPad = profile.CommentZeroPad,
        CommentPrefix = profile.CommentPrefix ?? string.Empty,
        CommentSuffix = profile.CommentSuffix ?? string.Empty,
        CommentJoiner = profile.CommentJoiner ?? string.Empty,
        CommentResetPerPart = profile.CommentResetPerPart,
        CompactFileNumbers = profile.CompactFileNumbers,
        OutputDirectory = profile.OutputDirectory ?? string.Empty,
        StreamEnabled = profile.StreamEnabled,
        AutoActive = profile.AutoActive,
        LookAheadMs = Math.Clamp(profile.LookAheadMs, 0, 9999),
        PrefetchLengthMs = Math.Clamp(profile.PrefetchLengthMs, 0, 9999),
        LoudnessPreserveGroupBalance = profile.LoudnessPreserveGroupBalance,
        MoreOptionsExpanded = profile.MoreOptionsExpanded,
        KeepLastSession = profile.KeepLastSession,
        LastWavePath = profile.LastWavePath ?? string.Empty,
        LastWavePaths = profile.LastWavePaths ?? string.Empty,
        KeepTarget = profile.KeepTarget,
        KeptTargetPath = profile.KeptTargetPath ?? string.Empty,
        KeptTargetProjectFilePath = profile.KeptTargetProjectFilePath ?? string.Empty,
    };

    public ProjectProfile ToProfile()
    {
        var profile = ProjectSettingsStore.CreateAppDefaults(Name);
        profile.Name = string.IsNullOrWhiteSpace(Name) ? ProjectSettingsStore.DefaultName : Name.Trim();
        profile.FadeInSeconds = FadeInSeconds;
        profile.FadeOutSeconds = FadeOutSeconds;
        if (!string.IsNullOrWhiteSpace(FadeInCurve))
        {
            profile.FadeInCurve = FadeInCurve.Trim();
        }

        if (!string.IsNullOrWhiteSpace(FadeOutCurve))
        {
            profile.FadeOutCurve = FadeOutCurve.Trim();
        }

        if (Enum.TryParse<PlaylistExitSourceMode>(ExitSourceAt, ignoreCase: true, out var exitMode))
        {
            profile.ExitSourceAt = exitMode;
        }

        profile.PlayPostExit = PlayPostExit;
        if (Enum.TryParse<MarkerGridOverrideMode>(GridOverride, ignoreCase: true, out var gridMode))
        {
            profile.GridOverride = gridMode;
        }

        profile.CommentDigits = Math.Clamp(
            CommentDigits,
            MarkerSettings.CommentDigitsMin,
            MarkerSettings.CommentDigitsMax);
        profile.CommentZeroPad = CommentZeroPad;
        profile.CommentPrefix = CommentPrefix ?? string.Empty;
        profile.CommentSuffix = CommentSuffix ?? string.Empty;
        profile.CommentJoiner = CommentJoiner ?? string.Empty;
        profile.CommentPrefixEnabled = profile.CommentPrefix.Length > 0;
        profile.CommentSuffixEnabled = profile.CommentSuffix.Length > 0;
        profile.CommentJoinerEnabled = profile.CommentJoiner.Length > 0;
        profile.CommentResetPerPart = CommentResetPerPart;
        profile.CompactFileNumbers = CompactFileNumbers;
        profile.OutputDirectory = OutputDirectory ?? string.Empty;
        profile.StreamEnabled = StreamEnabled;
        profile.AutoActive = AutoActive;
        profile.LookAheadMs = Math.Clamp(LookAheadMs, 0, 9999);
        profile.PrefetchLengthMs = Math.Clamp(PrefetchLengthMs, 0, 9999);
        profile.LoudnessPreserveGroupBalance = LoudnessPreserveGroupBalance;
        profile.MoreOptionsExpanded = MoreOptionsExpanded;
        profile.KeepLastSession = KeepLastSession;
        profile.LastWavePath = (LastWavePath ?? string.Empty).Trim().Trim('"');
        profile.LastWavePaths = (LastWavePaths ?? string.Empty).Trim().Trim('"');
        profile.KeepTarget = KeepTarget;
        profile.KeptTargetPath = (KeptTargetPath ?? string.Empty).Trim().Trim('"');
        profile.KeptTargetProjectFilePath = (KeptTargetProjectFilePath ?? string.Empty).Trim().Trim('"');
        return profile;
    }
}
