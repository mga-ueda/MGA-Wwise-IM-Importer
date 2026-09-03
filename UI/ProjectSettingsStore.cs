namespace MgaWwiseIMImporter.UI;

/// <summary>
/// プロジェクト単位の作業設定（変更時オートセーブ）。AppData の settings.json / projects。
/// </summary>
internal sealed class ProjectProfile
{
    public string Name { get; set; } = ProjectSettingsStore.DefaultName;

    public double FadeInSeconds { get; set; }

    public double FadeOutSeconds { get; set; }

    public string FadeInCurve { get; set; } = nameof(RegionFadeCurveKind.SCurve);

    public string FadeOutCurve { get; set; } = nameof(RegionFadeCurveKind.SCurve);

    public PlaylistExitSourceMode ExitSourceAt { get; set; } = PlaylistExitSourceMode.Immediate;

    /// <summary>Play -E（Wwise Play post-exit。既定オフ）。</summary>
    public bool PlayPostExit { get; set; }

    public MarkerGridOverrideMode GridOverride { get; set; } = MarkerGridOverrideMode.Bar;

    public int CommentDigits { get; set; } = 3;

    public bool CommentZeroPad { get; set; } = true;

    public bool CommentPrefixEnabled { get; set; }

    public string CommentPrefix { get; set; } = string.Empty;

    public bool CommentSuffixEnabled { get; set; }

    public string CommentSuffix { get; set; } = string.Empty;

    public bool CommentJoinerEnabled { get; set; }

    public string CommentJoiner { get; set; } = string.Empty;

    public bool CommentResetPerPart { get; set; } = true;

    public bool CompactFileNumbers { get; set; }

    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Music Track のストリーミング有効（互換用。正本はアプリ設定）。</summary>
    public bool StreamEnabled { get; set; } = true;

    /// <summary>EXPORT 完了時に Wwise を前面化するか（既定オン）。</summary>
    public bool AutoActive { get; set; } = true;

    /// <summary>
    /// 2 番目以降のセグメントの Look-ahead time（ms）。互換用。正本はアプリ設定。
    /// </summary>
    public int LookAheadMs { get; set; } = 500;

    /// <summary>Playlist 先頭セグメント内全トラックの Prefetch Length（ms）。互換用。正本はアプリ設定。</summary>
    public int PrefetchLengthMs { get; set; } = 500;

    /// <summary>
    /// Layer Music Option / Keep Layer Balance（互換用。正本はアプリ設定）。
    /// </summary>
    public bool LoudnessPreserveGroupBalance { get; set; }

    /// <summary>More Options パネルを開いた状態にするか（既定オン）。</summary>
    public bool MoreOptionsExpanded { get; set; } = true;

    /// <summary>起動時／プロジェクト復帰時に最後のセッションを復元するか（既定オン）。</summary>
    public bool KeepLastSession { get; set; } = true;

    /// <summary>最後に正常に読み込んだ波形のフルパス。</summary>
    public string LastWavePath { get; set; } = string.Empty;

    /// <summary>
    /// 複数波形モード時の全ソース WAV（| 区切り）。空なら <see cref="LastWavePath"/> のみ。
    /// </summary>
    public string LastWavePaths { get; set; } = string.Empty;

    /// <summary>Wwise 作成先パスをこのプロジェクトで固定するか（既定オフ）。</summary>
    public bool KeepTarget { get; set; }

    /// <summary>固定中の Wwise オブジェクトパス。</summary>
    public string KeptTargetPath { get; set; } = string.Empty;

    /// <summary>固定時の Wwise プロジェクトファイルパス（不一致なら再選択しない）。</summary>
    public string KeptTargetProjectFilePath { get; set; } = string.Empty;

    /// <summary>このプロファイルで最後に確認した Wwise プロジェクト名（未接続時の表示用）。</summary>
    public string LastKnownWwiseProjectName { get; set; } = string.Empty;

    /// <summary>このプロファイルで最後に確認した Wwise プロジェクトファイルパス。</summary>
    public string LastKnownWwiseProjectFilePath { get; set; } = string.Empty;

    public ProjectProfile Clone() => new()
    {
        Name = Name,
        FadeInSeconds = FadeInSeconds,
        FadeOutSeconds = FadeOutSeconds,
        FadeInCurve = FadeInCurve,
        FadeOutCurve = FadeOutCurve,
        ExitSourceAt = ExitSourceAt,
        PlayPostExit = PlayPostExit,
        GridOverride = GridOverride,
        CommentDigits = CommentDigits,
        CommentZeroPad = CommentZeroPad,
        CommentPrefixEnabled = CommentPrefixEnabled,
        CommentPrefix = CommentPrefix,
        CommentSuffixEnabled = CommentSuffixEnabled,
        CommentSuffix = CommentSuffix,
        CommentJoinerEnabled = CommentJoinerEnabled,
        CommentJoiner = CommentJoiner,
        CommentResetPerPart = CommentResetPerPart,
        CompactFileNumbers = CompactFileNumbers,
        OutputDirectory = OutputDirectory,
        StreamEnabled = StreamEnabled,
        AutoActive = AutoActive,
        LookAheadMs = LookAheadMs,
        PrefetchLengthMs = PrefetchLengthMs,
        LoudnessPreserveGroupBalance = LoudnessPreserveGroupBalance,
        MoreOptionsExpanded = MoreOptionsExpanded,
        KeepLastSession = KeepLastSession,
        LastWavePath = LastWavePath,
        LastWavePaths = LastWavePaths,
        KeepTarget = KeepTarget,
        KeptTargetPath = KeptTargetPath,
        KeptTargetProjectFilePath = KeptTargetProjectFilePath,
        LastKnownWwiseProjectName = LastKnownWwiseProjectName,
        LastKnownWwiseProjectFilePath = LastKnownWwiseProjectFilePath,
    };

    public void CopyMarkerInto(MarkerSettings markers)
    {
        markers.GridOverride = GridOverride;
        markers.CommentDigits = Math.Clamp(
            CommentDigits,
            MarkerSettings.CommentDigitsMin,
            MarkerSettings.CommentDigitsMax);
        markers.CommentZeroPad = CommentZeroPad;
        markers.CommentPrefix = CommentPrefixEnabled ? CommentPrefix : string.Empty;
        markers.CommentSuffix = CommentSuffixEnabled ? CommentSuffix : string.Empty;
        markers.CommentJoiner = CommentJoinerEnabled ? CommentJoiner : string.Empty;
        markers.CommentResetPerPart = CommentResetPerPart;
        markers.SyncCommentOptionalEnabledFlags();
    }

    public void CopyMarkerFrom(MarkerSettings markers)
    {
        markers.SyncCommentOptionalEnabledFlags();
        GridOverride = markers.GridOverride;
        CommentDigits = markers.CommentDigits;
        CommentZeroPad = markers.CommentZeroPad;
        CommentPrefix = markers.CommentPrefix;
        CommentSuffix = markers.CommentSuffix;
        CommentJoiner = markers.CommentJoiner;
        CommentPrefixEnabled = markers.CommentPrefixEnabled;
        CommentSuffixEnabled = markers.CommentSuffixEnabled;
        CommentJoinerEnabled = markers.CommentJoinerEnabled;
        CommentResetPerPart = markers.CommentResetPerPart;
    }
}

/// <summary>
/// プロジェクト一覧と Active の読み書き。変更時はプロファイルを即時保存する。
/// </summary>
internal sealed class ProjectSettingsStore
{
    public const string DefaultName = "Default";

    public static string NewProjectMenuItem => UiStrings.ProjectNewProjectMenuItem;

    private readonly List<string> _names = [];
    private readonly Dictionary<string, ProjectProfile> _profiles =
        new(StringComparer.OrdinalIgnoreCase);

    public string ActiveName { get; private set; } = DefaultName;

    public IReadOnlyList<string> Names => _names;

    public static ProjectProfile CreateAppDefaults(string name = DefaultName) => new()
    {
        Name = name,
        FadeInSeconds = 0d,
        FadeOutSeconds = 0d,
        FadeInCurve = RegionEdgeFade.BuiltinPlaylistFadeInCurve.ToString(),
        FadeOutCurve = RegionEdgeFade.BuiltinPlaylistFadeOutCurve.ToString(),
        ExitSourceAt = PlaylistExitSourceMode.Immediate,
        PlayPostExit = false,
        GridOverride = MarkerGridOverrideMode.Bar,
        CommentDigits = 3,
        CommentZeroPad = true,
        CommentPrefixEnabled = false,
        CommentPrefix = string.Empty,
        CommentSuffixEnabled = false,
        CommentSuffix = string.Empty,
        CommentJoinerEnabled = false,
        CommentJoiner = string.Empty,
        CommentResetPerPart = true,
        CompactFileNumbers = false,
        OutputDirectory = string.Empty,
        StreamEnabled = true,
        AutoActive = true,
        LookAheadMs = 500,
        PrefetchLengthMs = 500,
        LoudnessPreserveGroupBalance = false,
        MoreOptionsExpanded = true,
        KeepLastSession = true,
        LastWavePath = string.Empty,
        LastWavePaths = string.Empty,
        KeepTarget = false,
        KeptTargetPath = string.Empty,
        KeptTargetProjectFilePath = string.Empty,
        LastKnownWwiseProjectName = string.Empty,
        LastKnownWwiseProjectFilePath = string.Empty,
    };

    public static ProjectSettingsStore Load()
    {
        var store = new ProjectSettingsStore();
        var projects = JsonSettingsStore.Document.Projects ?? new ProjectsSettingsData();
        if (projects.Items.Count == 0)
        {
            store.EnsureDefaultExists();
            store.WriteAll();
            return store;
        }

        foreach (var item in projects.Items)
        {
            var profile = item.ToProfile();
            var name = profile.Name;
            if (store._profiles.ContainsKey(name))
            {
                continue;
            }

            store._names.Add(name);
            store._profiles[name] = profile;
        }

        if (store._names.Count == 0)
        {
            store.EnsureDefaultExists();
            store.WriteAll();
            return store;
        }

        var active = string.IsNullOrWhiteSpace(projects.Active)
            ? DefaultName
            : projects.Active.Trim();
        store.ActiveName = store._profiles.ContainsKey(active)
            ? store._names.First(n => string.Equals(n, active, StringComparison.OrdinalIgnoreCase))
            : store._names[0];

        return store;
    }

    public ProjectProfile GetActive() => GetRequired(ActiveName);

    public ProjectProfile GetRequired(string name)
    {
        if (_profiles.TryGetValue(name, out var profile))
        {
            return profile.Clone();
        }

        throw new InvalidOperationException(UiStrings.ErrProjectNotFound(name));
    }

    public bool ContainsName(string name) =>
        !string.IsNullOrWhiteSpace(name) && _profiles.ContainsKey(name.Trim());

    public void SetActive(string name)
    {
        var trimmed = name.Trim();
        if (!_profiles.ContainsKey(trimmed))
        {
            throw new InvalidOperationException(UiStrings.ErrProjectNotFound(trimmed));
        }

        ActiveName = _names.First(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));
        WriteIndex();
    }

    /// <summary>終了時など、Active 名だけ更新する（プロファイルは書かない）。</summary>
    public void SaveActiveNameOnly()
    {
        WriteIndex();
    }

    public void SaveKeepTarget(
        string name,
        bool enabled,
        string keptTargetPath,
        string keptTargetProjectFilePath)
    {
        if (!_profiles.TryGetValue(name.Trim(), out var profile))
        {
            return;
        }

        profile.KeepTarget = enabled;
        profile.KeptTargetPath = keptTargetPath?.Trim() ?? string.Empty;
        profile.KeptTargetProjectFilePath = keptTargetProjectFilePath?.Trim() ?? string.Empty;
        WriteProfile(name, profile);
    }

    public void SaveLastKnownWwiseProject(
        string name,
        string projectName,
        string projectFilePath)
    {
        if (!_profiles.TryGetValue(name.Trim(), out var profile))
        {
            return;
        }

        profile.LastKnownWwiseProjectName = projectName?.Trim() ?? string.Empty;
        profile.LastKnownWwiseProjectFilePath = projectFilePath?.Trim() ?? string.Empty;
        WriteProfile(name, profile);
    }

    public void SaveLastWaveSession(string name, LastWaveSessionState state)
    {
        var trimmed = name.Trim();
        if (!_profiles.ContainsKey(trimmed))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppStorage.SessionsDirectory);
            var path = LastWaveSessionState.SessionPath(trimmed);
            TextFileUtf8.WriteAllText(path, state.ToJson(), emitBom: false);
        }
        catch
        {
            // オートセーブ失敗は作業を止めない。
        }
    }

    public bool TryReadLastWaveSession(string name, out LastWaveSessionState? state)
    {
        state = null;
        var trimmed = name.Trim();
        if (!_profiles.ContainsKey(trimmed))
        {
            return false;
        }

        var path = LastWaveSessionState.SessionPath(trimmed);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = TextFileUtf8.ReadAllText(path);
            return LastWaveSessionState.TryParse(json, out state);
        }
        catch
        {
            return false;
        }
    }

    public static void DeleteLastWaveSessionFile(string projectName)
    {
        try
        {
            var path = LastWaveSessionState.SessionPath(projectName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 削除失敗は無視する。
        }
    }

    private static void RenameLastWaveSessionFile(string oldName, string newName)
    {
        try
        {
            var oldPath = LastWaveSessionState.SessionPath(oldName);
            var newPath = LastWaveSessionState.SessionPath(newName);
            if (!File.Exists(oldPath))
            {
                return;
            }

            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (File.Exists(newPath))
            {
                File.Delete(newPath);
            }

            File.Move(oldPath, newPath);
        }
        catch
        {
            // 改名失敗は無視する。
        }
    }

    /// <summary>
    /// 現在の UI 状態を保存する。newName が異なれば改名（旧名セクションは削除）。
    /// creatingNew のときは新規追加。
    /// </summary>
    public string SaveProfile(
        string currentName,
        string newName,
        ProjectProfile profile,
        bool creatingNew)
    {
        var trimmedNew = NormalizeName(newName);
        if (trimmedNew.Length == 0)
        {
            throw new InvalidOperationException(UiStrings.ErrProjectNameRequired);
        }

        if (string.Equals(trimmedNew, NewProjectMenuItem, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(UiStrings.ErrProjectNameReserved);
        }

        if (creatingNew)
        {
            if (_profiles.ContainsKey(trimmedNew))
            {
                throw new InvalidOperationException(UiStrings.ErrProjectNameExists(trimmedNew));
            }

            profile.Name = trimmedNew;
            _names.Add(trimmedNew);
            _profiles[trimmedNew] = profile.Clone();
            ActiveName = trimmedNew;
            WriteProfile(trimmedNew, _profiles[trimmedNew]);
            WriteIndex();
            return trimmedNew;
        }

        var trimmedCurrent = currentName.Trim();
        if (!_profiles.ContainsKey(trimmedCurrent))
        {
            throw new InvalidOperationException(UiStrings.ErrProjectNotFound(trimmedCurrent));
        }

        var rename = !string.Equals(trimmedCurrent, trimmedNew, StringComparison.OrdinalIgnoreCase);
        if (rename && _profiles.ContainsKey(trimmedNew))
        {
            throw new InvalidOperationException(UiStrings.ErrProjectNameExists(trimmedNew));
        }

        profile.Name = trimmedNew;
        if (rename)
        {
            var index = _names.FindIndex(n =>
                string.Equals(n, trimmedCurrent, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _names[index] = trimmedNew;
            }

            _profiles.Remove(trimmedCurrent);
            RenameLastWaveSessionFile(trimmedCurrent, trimmedNew);
        }

        _profiles[trimmedNew] = profile.Clone();
        ActiveName = trimmedNew;
        WriteProfile(trimmedNew, _profiles[trimmedNew]);
        WriteIndex();
        return trimmedNew;
    }

    public ProjectProfile Delete(string name)
    {
        var trimmed = name.Trim();
        if (!_profiles.ContainsKey(trimmed))
        {
            throw new InvalidOperationException(UiStrings.ErrProjectNotFound(trimmed));
        }

        _names.RemoveAll(n => string.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));
        _profiles.Remove(trimmed);
        DeleteLastWaveSessionFile(trimmed);

        if (_names.Count == 0)
        {
            EnsureDefaultExists();
            WriteAll();
            return GetActive();
        }

        if (string.Equals(ActiveName, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            ActiveName = _names[0];
        }

        WriteIndex();
        return GetActive();
    }

    public string SuggestNewProjectName()
    {
        var baseName = UiStrings.ProjectNewProjectBaseName;
        if (!_profiles.ContainsKey(baseName))
        {
            return baseName;
        }

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!_profiles.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName} {DateTime.Now:yyyyMMddHHmmss}";
    }

    private void EnsureDefaultExists()
    {
        _names.Clear();
        _profiles.Clear();
        var profile = CreateAppDefaults();
        _names.Add(DefaultName);
        _profiles[DefaultName] = profile;
        ActiveName = DefaultName;
    }

    private void WriteAll() => PersistProjects();

    private void WriteIndex() => PersistProjects();

    private void WriteProfile(string name, ProjectProfile profile)
    {
        _profiles[name] = profile.Clone();
        PersistProjects();
    }

    private void PersistProjects()
    {
        var items = new List<ProjectProfileData>(_names.Count);
        foreach (var name in _names)
        {
            if (_profiles.TryGetValue(name, out var profile))
            {
                items.Add(ProjectProfileData.FromProfile(profile));
            }
        }

        var active = ActiveName;
        JsonSettingsStore.Update(doc =>
        {
            doc.Projects = new ProjectsSettingsData
            {
                Active = active,
                Items = items,
            };
        });
    }

    private static string NormalizeName(string name) => name.Trim();

}
