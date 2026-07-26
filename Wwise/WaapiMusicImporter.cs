using System.Text;
using System.Text.Json;
using MgaWwiseIMImporter.UI;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// <see cref="WwiseMusicPlan"/> を WAAPI（ak.wwise.core.object.set）で Wwise へ流し込む。
/// <para>
/// 1. 各 Music Segment 用の WAV を用意する。複数波形で焼き込み不要なら元ファイルを
///    outputDirectory へコピーして共有し、MusicClip の Begin/End Offset で範囲を合わせる。
///    ラウドネス焼き込みが必要なときだけ切り出し WAV を書く。
/// 2. 複数パート時は State Group／State を作成または更新し、Music Switch Container に割当。
/// 3. object.set で Playlist／Segment／Track（＋WAV）と Cue を作成。
/// 4. グループ化 Playlist はグループ名の State Group（State A/B/C…）を作り、
///    Group Fade が全員同一なら Default Transition Time のみ、異なれば Custom Transition Time
///    （遷移先 State ごと。このとき Default は Wwise 既定 1 秒のまま）、
///    各 Music Track へ割当し、対応 State のみ Volume 0dB・他は -108dB を設定する。
///    完了後に現在 State を先頭へ設定し、作成した Switch／Playlist を選択する（プレビュー用）。
/// 5. 必要なら MusicClip トリムとリージョン端フェード（非破壊）を設定する。
///    Fade Duration が WAAPI 上限（3.6 秒）を超える場合は WWU 直接編集で本値を書く。
/// 6. Playlist 遷移の MusicFade（Time）と Group State の TransitionList／
///    Track State Volume は WAAPI 非対応のため、同系統の WWU 直編集で書く。
/// </para>
/// </summary>
internal static class WaapiMusicImporter
{
    /// <summary>WAAPI が受け付ける MusicClip Fade Duration の上限（ミリ秒＝3.6 秒）。</summary>
    private const double WaapiMusicClipFadeMaxMs = 3600;

    /// <summary>MusicClip FadeInMode / FadeOutMode: Manual。</summary>
    private const int MusicClipFadeModeManual = 1;

    /// <summary>MusicFade.FadeType: Fade-out。</summary>
    private const int MusicFadeTypeOut = 1;
    public static async Task<string> ImportAsync(
        WaapiSettings waapiSettings,
        WwiseImportSettings importSettings,
        WwiseMusicPlan plan,
        string parentPath,
        string sourceWavPath,
        string outputDirectory,
        IReadOnlyList<WaveformOutputPart> outputParts,
        WavFileInfo wavInfo,
        IReadOnlyDictionary<int, int>? partGroupIds = null,
        bool loudnessNormalizeEnabled = false,
        double loudnessTargetLkfs = -24.0,
        bool loudnessPreserveGroupBalance = true,
        bool autoVolumeEnabled = true,
        AutoVolumeTarget autoVolumeTarget = AutoVolumeTarget.MakeUpGain,
        bool updateExistingStateGroup = false,
        IReadOnlyList<RegionEdgeFade>? regionEdgeFades = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (wavInfo.SampleRate == 0 || wavInfo.BlockAlign == 0)
        {
            throw new ArgumentException(UiStrings.ErrBadSampleRate);
        }

        var sampleRate = wavInfo.SampleRate;
        var blockAlign = wavInfo.BlockAlign;

        var sb = new StringBuilder();
        void Log(string line = "")
        {
            sb.AppendLine(line);
            progress?.Report(line);
        }

        Log(UiStrings.LogWwiseImportHeader);
        Log($"{UiStrings.KeyTarget} {parentPath}");
        Log(
            $"{UiStrings.KeyMode} "
            + (plan.IsMultiPart
                ? UiStrings.LabelMusicSwitchContainer
                : UiStrings.LabelMusicPlaylistContainer));
        Log($"{UiStrings.KeyName} {plan.ContainerName}");

        string? stateGroupPath = null;
        if (plan.IsMultiPart)
        {
            stateGroupPath = importSettings.ResolveStateGroupPath(plan.ContainerName);
            Log($"{UiStrings.KeyStateGrp} {stateGroupPath}");
            if (updateExistingStateGroup)
            {
                // onNameConflict=merge により、State Group 自体を維持したまま
                // State 一覧を現在の Playlist 構成へ同期する。
                Log(UiStrings.LogStateGroupUpdateExisting);
            }
            else
            {
                Log(UiStrings.LogStateGroupCreateNew);
            }
        }

        Dictionary<int, float>? partGains = null;
        if (loudnessNormalizeEnabled)
        {
            Log(UiStrings.LogLoudnessNormalizeOn(loudnessTargetLkfs, loudnessPreserveGroupBalance));
            partGains = LoudnessMeter.ComputePartGains(
                sourceWavPath,
                wavInfo,
                outputParts,
                partGroupIds,
                loudnessTargetLkfs,
                loudnessPreserveGroupBalance,
                Log);
            if (autoVolumeEnabled)
            {
                Log(
                    UiStrings.LogAutoVolumeOn(
                        autoVolumeTarget == AutoVolumeTarget.VoiceVolume
                            ? UiStrings.LabelVoiceVolume
                            : UiStrings.LabelMakeUpGain));
            }
            else
            {
                Log(UiStrings.LogAutoVolumeOff);
            }
        }

        var applyAutoVolume = loudnessNormalizeEnabled && autoVolumeEnabled && partGains is not null;

        // 中間パート WAV は作らず、元 WAV から最終セグメント WAV を直接切り出す。
        // リージョン端フェードは WAV へ焼き込まず、後段で MusicClip 非破壊フェードとして設定する。
        var fadesForClip = regionEdgeFades?
            .Select(fade => fade.Normalized())
            .Where(fade => fade.HasAnyFade)
            .ToList() ?? [];

        var segmentMedia = SliceSegmentWavs(
            plan,
            sourceWavPath,
            outputDirectory,
            outputParts,
            sampleRate,
            blockAlign,
            wavInfo,
            partGains,
            Log);

        // タイムアウトは import を含むので長めに取る
        var timeout = TimeSpan.FromMilliseconds(Math.Max(waapiSettings.TimeoutMs, 30000));
        using var client = new WaapiHttpClient(waapiSettings.Url, timeout);
        var returnOptions = new Dictionary<string, object>
        {
            ["return"] = new[] { "id", "name", "type", "path" },
        };

        // 一括 object.set だと長時間 UI が止まったように見えるため、段階的に投げて進捗を出す。
        if (plan.IsMultiPart)
        {
            if (stateGroupPath is null || stateGroupPath.Length == 0)
            {
                throw new InvalidOperationException(UiStrings.ErrStateGroupPathRequired);
            }

            Log(UiStrings.LogCreatingStateGroup);
            await CallObjectSetAsync(
                    client,
                    BuildStateGroupSetArgs(plan, importSettings),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            var containerPath = $"{parentPath.TrimEnd('\\')}\\{plan.ContainerName}";
            Log(UiStrings.LogCreatingMusicSwitch);
            await CallObjectSetAsync(
                    client,
                    BuildMusicSwitchShellSetArgs(plan, parentPath, stateGroupPath),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            for (var i = 0; i < plan.Playlists.Count; i++)
            {
                var playlist = plan.Playlists[i];
                Log(UiStrings.LogCreatingPlaylist(i + 1, plan.Playlists.Count, playlist.Name));
                await CallObjectSetAsync(
                        client,
                        BuildPlaylistAppendSetArgs(
                            plan,
                            containerPath,
                            playlist,
                            segmentMedia,
                            importSettings,
                            applyAutoVolume,
                            autoVolumeTarget,
                            partGains,
                            Log),
                        returnOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            // Playlist 未作成時に @AudioNode / Destination を張ると空参照になるため、子作成後に結ぶ。
            Log(UiStrings.LogBindingStates);
            await CallObjectSetAsync(
                    client,
                    BuildMusicSwitchEntriesSetArgs(plan, containerPath, stateGroupPath),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            Log(UiStrings.LogConfiguringTransitions);
            await CallObjectSetAsync(
                    client,
                    BuildMusicSwitchTransitionsSetArgs(plan, containerPath),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            // DestinationContextObject は Reference のため、ネスト作成だけでは空になり得る。
            await BindTransitionDestinationsAsync(
                    client,
                    containerPath,
                    plan,
                    Log,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            Log(UiStrings.LogCreatingWwiseObjects);
            await CallObjectSetAsync(
                    client,
                    BuildSinglePlaylistSetArgs(
                        plan,
                        parentPath,
                        segmentMedia,
                        importSettings,
                        applyAutoVolume,
                        autoVolumeTarget,
                        partGains,
                        Log),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Log(UiStrings.LogWwiseObjectsCreated);

        // MusicSegment は作成時に既定の Entry/Exit を持つ。
        // 作成と同時の @Cues 追加は二重化するため、作成後に replaceAll で差し替える。
        var musicRootPath = $"{parentPath.TrimEnd('\\')}\\{plan.ContainerName}";
        await ReplaceAllSegmentCuesAsync(
                client,
                plan,
                musicRootPath,
                cancellationToken)
            .ConfigureAwait(false);

        // グループ化 Playlist: State Group（A/B/C…）作成 → Music Track へ割当。
        // TransitionList / State Volume は WWU 直編集で後段に書く。
        var (groupStateTransitionPatches, groupStateVolumePatches) = await ApplyGroupStateGroupsAsync(
                client,
                plan,
                musicRootPath,
                importSettings,
                returnOptions,
                Log,
                cancellationToken)
            .ConfigureAwait(false);

        var playAtFixes = await ApplyMusicClipTrimsAsync(
                client,
                plan,
                musicRootPath,
                segmentMedia,
                Log,
                cancellationToken)
            .ConfigureAwait(false);

        var fadeDurationFixes = await ApplyMusicClipFadesAsync(
                client,
                plan,
                musicRootPath,
                segmentMedia,
                fadesForClip,
                Log,
                cancellationToken)
            .ConfigureAwait(false);

        // 負の PlayAt、WAAPI 上限超の Clip Fade Duration、Playlist 遷移 MusicFade、
        // Group State の TransitionList / Track State Volume は
        // プロジェクトを保存→クローズし、WWU（XML）を直接書き換えてから再オープンする。
        var transitionFadePatches = plan.IsMultiPart
            ? plan.Playlists
                .Select(p => new MusicTransitionFadePatch(
                    p.Name,
                    p.FadeInSeconds,
                    p.FadeOutSeconds,
                    p.FadeInCurve,
                    p.FadeOutCurve,
                    p.PlayPostExit))
                .ToList()
            : [];
        await ApplyWorkUnitPatchesAsync(
                client,
                musicRootPath,
                playAtFixes,
                fadeDurationFixes,
                transitionFadePatches,
                groupStateTransitionPatches,
                groupStateVolumePatches,
                Log,
                cancellationToken)
            .ConfigureAwait(false);

        // プロジェクト再オープン後に、State の現在値を先頭へ揃える（プレビュー用）。
        // 失敗してもインポート自体は続行。
        await SetInitialStatesForPreviewAsync(
                client,
                plan,
                importSettings,
                Log,
                cancellationToken)
            .ConfigureAwait(false);

        // 転送した Switch（複数 Playlist）または単一 Playlist を Project Explorer で選択。
        // Switch 時は一度子を選んで展開してから Switch へ戻す。
        await TrySelectImportedObjectAsync(
                client,
                musicRootPath,
                plan,
                Log,
                cancellationToken)
            .ConfigureAwait(false);

        if (plan.IsMultiPart)
        {
            foreach (var playlist in plan.Playlists)
            {
                Log(
                    UiStrings.LogTransitionAnyToPlaylist(
                        playlist.Name,
                        playlist.ExitSourceAt.ToUiName(),
                        playlist.FadeInSeconds,
                        playlist.FadeOutSeconds));
            }
        }

        foreach (var playlist in plan.Playlists)
        {
            if (playlist.GroupState is { } groupState)
            {
                Log(
                    UiStrings.LogGroupStateSummary(
                        groupState.Name,
                        string.Join(", ", groupState.StateNames),
                        FormatGroupStateFadeSummary(groupState),
                        groupState.UseDefaultTransitionOnly,
                        groupState.UseDefaultTransitionOnly
                            ? groupState.DefaultTransitionSeconds
                            : WwiseDefaultStateTransitionSeconds));
            }

            Log(UiStrings.LogPlaylistSummary(playlist.Name, playlist.Segments.Count));
            for (var segmentIndex = 0; segmentIndex < playlist.Segments.Count; segmentIndex++)
            {
                var segment = playlist.Segments[segmentIndex];
                var flags = new List<string>();
                if (segment.LoopInfinite)
                {
                    flags.Add("loop=∞");
                }

                if (segment.EntryCueMs > segment.ClipStartMs)
                {
                    flags.Add("anacrusis");
                }

                if (segment.ExitCueMs < segment.ClipEndMs)
                {
                    flags.Add("exit-tail");
                }

                if (segment.CustomCues.Count > 0)
                {
                    flags.Add($"cues={segment.CustomCues.Count}");
                }

                flags.Add($"tracks={segment.Tracks.Count}");

                var durationMs = Math.Max(0.0, segment.ClipEndMs - segment.ClipStartMs);
                var entryLocal = Math.Max(0.0, segment.EntryCueMs - segment.ClipStartMs);
                Log(
                    $"  [{segmentIndex + 1}/{playlist.Segments.Count}] {segment.Name}"
                    + $"  len={durationMs:0}ms"
                    + (entryLocal > 0.5 ? $"  entry=+{entryLocal:0}ms" : string.Empty)
                    + $"  T{segment.TempoBpm:0.##}-{segment.TimeSignatureUpper}/{segment.TimeSignatureLower}"
                    + $"  ({string.Join(", ", flags)})");

                foreach (var track in segment.Tracks)
                {
                    var key = TrackSliceKey(segment.Name, track.Name);
                    if (!segmentMedia.TryGetValue(key, out var media))
                    {
                        Log($"    Track {track.Name}: (media missing)");
                        continue;
                    }

                    var beginMs = media.SampleRate == 0
                        ? 0.0
                        : media.SourceStartSample * 1000.0 / media.SampleRate;
                    var endMs = media.SampleRate == 0
                        ? 0.0
                        : media.SourceEndSample * 1000.0 / media.SampleRate;
                    Log(
                        $"    Track {track.Name}"
                        + (string.IsNullOrEmpty(track.LayerStateName)
                            ? string.Empty
                            : $"  state={track.LayerStateName}")
                        + $": {Path.GetFileName(media.WavPath)}"
                        + $"  src=[{media.SourceStartSample} .. {media.SourceEndSample})"
                        + $"  ({beginMs:0.###} .. {endMs:0.###} ms)"
                        + (media.ApplyClipTrim
                            ? "  [copy+trim]"
                            : media.ReusedOriginal
                                ? "  [copy]"
                                : "  [slice]"));
                }
            }
        }

        var uniqueWavCount = segmentMedia.Values
            .Select(binding => binding.WavPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        Log($"{UiStrings.KeySlices} {uniqueWavCount}");
        Log(UiStrings.LogWwiseImportComplete);
        Log();
        return sb.ToString();
    }

    /// <summary>
    /// EXPORT 直前に Playlist / Segment / Track 構成をログへ出す。
    /// </summary>
    public static string FormatPlanSummary(WwiseMusicPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine(UiStrings.LogImportPlanHeader);
        sb.AppendLine(UiStrings.LogImportPlanPlaylists(plan.Playlists.Count, plan.ContainerName));
        foreach (var playlist in plan.Playlists)
        {
            sb.AppendLine(UiStrings.LogPlaylistSummary(playlist.Name, playlist.Segments.Count));
            if (playlist.GroupState is { } groupState)
            {
                sb.AppendLine(
                    UiStrings.LogGroupStateSummary(
                        groupState.Name,
                        string.Join(", ", groupState.StateNames),
                        FormatGroupStateFadeSummary(groupState),
                        groupState.UseDefaultTransitionOnly,
                        groupState.UseDefaultTransitionOnly
                            ? groupState.DefaultTransitionSeconds
                            : WwiseDefaultStateTransitionSeconds));
            }

            for (var i = 0; i < playlist.Segments.Count; i++)
            {
                var segment = playlist.Segments[i];
                var flags = new List<string>();
                if (segment.LoopInfinite)
                {
                    flags.Add("loop=∞");
                }

                if (segment.EntryCueMs > segment.ClipStartMs)
                {
                    flags.Add("anacrusis");
                }

                if (segment.ExitCueMs < segment.ClipEndMs)
                {
                    flags.Add("exit-tail");
                }

                flags.Add($"tracks={segment.Tracks.Count}");
                var durationMs = Math.Max(0.0, segment.ClipEndMs - segment.ClipStartMs);
                var entryLocal = Math.Max(0.0, segment.EntryCueMs - segment.ClipStartMs);
                sb.AppendLine(
                    $"  [{i + 1}/{playlist.Segments.Count}] {segment.Name}"
                    + $"  len={durationMs:0}ms"
                    + (entryLocal > 0.5 ? $"  entry=+{entryLocal:0}ms" : string.Empty)
                    + $"  ({string.Join(", ", flags)})");
                foreach (var track in segment.Tracks)
                {
                    var layer = string.IsNullOrEmpty(track.LayerStateName)
                        ? string.Empty
                        : $"  state={track.LayerStateName}";
                    sb.AppendLine(
                        $"    Track {track.Name}{layer}"
                        + $"  clip=[{track.ClipStartMs:0.###} .. {track.ClipEndMs:0.###}] ms"
                        + $"  samples=[{track.AbsoluteStartSample} .. {track.AbsoluteEndSample})");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static async Task CallObjectSetAsync(
        WaapiHttpClient client,
        Dictionary<string, object?> setArgs,
        Dictionary<string, object> returnOptions,
        CancellationToken cancellationToken)
    {
        _ = await client.CallAsync(
                "ak.wwise.core.object.set",
                setArgs,
                returnOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Any → Playlist トランジションの Destination 参照を setReference で結ぶ。
    /// 作成時の @DestinationContextObject で足りる場合もあるが、Reference は空のままのことがある。
    /// </summary>
    /// <summary>
    /// DestinationContextObject は Reference のため、ネスト作成だけでは空になり得る。
    /// ルール名はすべて Transition のため、Destination 参照／種別で対象を特定する。
    /// </summary>
    private static async Task BindTransitionDestinationsAsync(
        WaapiHttpClient client,
        string containerPath,
        WwiseMusicPlan plan,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var transitions = await QueryMusicTransitionDestinationsAsync(
                client,
                containerPath,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var playlist in plan.Playlists)
        {
            var playlistPath = $"{containerPath}\\{playlist.Name}";
            var playlistId = await TryGetObjectIdAsync(client, playlistPath, cancellationToken)
                .ConfigureAwait(false);
            var destination = !string.IsNullOrEmpty(playlistId) ? playlistId : playlistPath;

            var matchedIds = transitions
                .Where(t => IsTransitionDestinationForPlaylist(t, playlist.Name, playlistPath, playlistId))
                .Select(t => t.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (matchedIds.Count == 0)
            {
                // object.set 時の Destination 参照が効いていることがある。
                continue;
            }

            foreach (var transitionId in matchedIds)
            {
                await client.CallAsync(
                        "ak.wwise.core.object.setProperty",
                        new Dictionary<string, object?>
                        {
                            ["object"] = transitionId,
                            ["property"] = "DestinationContextType",
                            ["value"] = 2,
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await client.CallAsync(
                        "ak.wwise.core.object.setReference",
                        new Dictionary<string, object?>
                        {
                            ["object"] = transitionId,
                            ["reference"] = "DestinationContextObject",
                            ["value"] = destination,
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            log(UiStrings.LogTransitionDestinationSet(playlist.Name));
        }
    }

    private readonly record struct MusicTransitionDestinationInfo(
        string Id,
        int? DestinationContextType,
        string? DestinationId,
        string? DestinationName,
        string? DestinationPath);

    private static async Task<List<MusicTransitionDestinationInfo>> QueryMusicTransitionDestinationsAsync(
        WaapiHttpClient client,
        string containerPath,
        CancellationToken cancellationToken)
    {
        var list = new List<MusicTransitionDestinationInfo>();
        var escaped = containerPath.Replace("\"", "\\\"", StringComparison.Ordinal);
        var result = await client.CallAsync(
                "ak.wwise.core.object.get",
                new Dictionary<string, object?>
                {
                    ["waql"] = $"$ \"{escaped}\" select descendants where type = \"MusicTransition\"",
                },
                new Dictionary<string, object?>
                {
                    ["return"] = new[]
                    {
                        "id",
                        "name",
                        "@DestinationContextType",
                        "@DestinationContextObject",
                        "@DestinationContextObject.name",
                        "@DestinationContextObject.path",
                        "@DestinationContextObject.id",
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.TryGetProperty("return", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idEl))
            {
                continue;
            }

            var id = idEl.GetString();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            // TransitionRoot フォルダ（空名）は除外。名前 Transition のルールは残す。
            if (item.TryGetProperty("name", out var nameEl)
                && string.IsNullOrEmpty(nameEl.GetString()))
            {
                continue;
            }

            int? destinationType = null;
            if (item.TryGetProperty("@DestinationContextType", out var typeEl)
                && typeEl.ValueKind == JsonValueKind.Number)
            {
                destinationType = typeEl.GetInt32();
            }

            string? destinationId = null;
            string? destinationName = null;
            string? destinationPath = null;
            if (item.TryGetProperty("@DestinationContextObject", out var destEl))
            {
                if (destEl.ValueKind == JsonValueKind.String)
                {
                    destinationPath = destEl.GetString();
                }
                else if (destEl.ValueKind == JsonValueKind.Object)
                {
                    if (destEl.TryGetProperty("id", out var destIdEl))
                    {
                        destinationId = destIdEl.GetString();
                    }

                    if (destEl.TryGetProperty("name", out var destNameEl))
                    {
                        destinationName = destNameEl.GetString();
                    }

                    if (destEl.TryGetProperty("path", out var destPathEl))
                    {
                        destinationPath = destPathEl.GetString();
                    }
                }
            }

            if (item.TryGetProperty("@DestinationContextObject.id", out var flatId)
                && flatId.ValueKind == JsonValueKind.String)
            {
                destinationId ??= flatId.GetString();
            }

            if (item.TryGetProperty("@DestinationContextObject.name", out var flatName)
                && flatName.ValueKind == JsonValueKind.String)
            {
                destinationName ??= flatName.GetString();
            }

            if (item.TryGetProperty("@DestinationContextObject.path", out var flatPath)
                && flatPath.ValueKind == JsonValueKind.String)
            {
                destinationPath ??= flatPath.GetString();
            }

            list.Add(new MusicTransitionDestinationInfo(
                id,
                destinationType,
                destinationId,
                destinationName,
                destinationPath));
        }

        return list;
    }

    private static bool IsTransitionDestinationForPlaylist(
        MusicTransitionDestinationInfo transition,
        string playlistName,
        string playlistPath,
        string? playlistId)
    {
        // Any→Any（DestinationContextType=Any）は対象外。
        if (transition.DestinationContextType is 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(playlistId)
            && string.Equals(transition.DestinationId, playlistId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(transition.DestinationName, playlistName, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(transition.DestinationPath)
            && (string.Equals(transition.DestinationPath, playlistPath, StringComparison.OrdinalIgnoreCase)
                || transition.DestinationPath.EndsWith("\\" + playlistName, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static async Task<string?> TryGetObjectIdAsync(
        WaapiHttpClient client,
        string objectPath,
        CancellationToken cancellationToken)
    {
        var escaped = objectPath.Replace("\"", "\\\"", StringComparison.Ordinal);
        try
        {
            var result = await client.CallAsync(
                    "ak.wwise.core.object.get",
                    new Dictionary<string, object?>
                    {
                        ["waql"] = $"$ \"{escaped}\"",
                    },
                    new Dictionary<string, object?>
                    {
                        ["return"] = new[] { "id", "path" },
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.TryGetProperty("return", out var arr)
                || arr.ValueKind != JsonValueKind.Array
                || arr.GetArrayLength() == 0)
            {
                return null;
            }

            var first = arr[0];
            return first.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        }
        catch (WaapiException)
        {
            return null;
        }
    }

    /// <summary>
    /// 各トラックのメディアを用意する。返り値は TrackSliceKey → バインディング。
    /// 複数波形で焼き込み不要なら元 WAV を outputDirectory へコピーして再利用する。
    /// </summary>
    private static Dictionary<string, TrackMediaBinding> SliceSegmentWavs(
        WwiseMusicPlan plan,
        string sourceWavPath,
        string outputDirectory,
        IReadOnlyList<WaveformOutputPart> outputParts,
        uint sampleRate,
        ushort blockAlign,
        WavFileInfo wavInfo,
        IReadOnlyDictionary<int, float>? partGains,
        Action<string> log)
    {
        Directory.CreateDirectory(outputDirectory);
        log($"{UiStrings.KeyOutput} {outputDirectory}");

        var partByPath = outputParts.ToDictionary(
            part => Path.GetFullPath(Path.Combine(outputDirectory, part.FileName)),
            part => part,
            StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, TrackMediaBinding>(StringComparer.OrdinalIgnoreCase);
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loggedReusePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var playlist in plan.Playlists)
        {
            foreach (var segment in playlist.Segments)
            {
                if (segment.Tracks.Count == 0)
                {
                    throw new InvalidOperationException(UiStrings.ErrNoTracks(segment.Name));
                }

                foreach (var track in segment.Tracks)
                {
                    var partPath = Path.GetFullPath(track.SourceWavPath);
                    if (!partByPath.TryGetValue(partPath, out var part))
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrCannotResolveOutputPart(track.SourceWavPath));
                    }

                    var startSample = track.AbsoluteStartSample;
                    var endSample = track.AbsoluteEndSample;
                    if (endSample <= startSample)
                    {
                        startSample = checked(
                            part.StartSampleOffset + MsToSample(track.ClipStartMs, sampleRate));
                        endSample = checked(
                            part.StartSampleOffset + MsToSample(track.ClipEndMs, sampleRate));
                    }

                    if (endSample <= startSample)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrTrackRangeEmpty(
                                segment.Name,
                                track.Name,
                                $"{track.ClipStartMs}..{track.ClipEndMs} ms"));
                    }

                    var gain = 1f;
                    if (partGains is not null
                        && partGains.TryGetValue(part.Number, out var partGain))
                    {
                        gain = partGain;
                    }

                    var trackKey = TrackSliceKey(segment.Name, track.Name);
                    var sliceSourcePath = part.ResolveSourcePath(sourceWavPath);
                    var localStart = part.VirtualToLocal(startSample);
                    var localEnd = part.VirtualToLocal(endSample);
                    var sliceInfo = part.HasDedicatedSource
                        ? WavFileInfo.Read(sliceSourcePath)
                        : wavInfo;
                    var sliceBlockAlign = sliceInfo.BlockAlign != 0
                        ? sliceInfo.BlockAlign
                        : blockAlign;

                    // 複数波形: 焼き込み不要なら元 WAV を outputDirectory へコピーして共有（2 本のまま）。
                    // セグメントごとの範囲は MusicClip Begin/End Offset で合わせる（手動作業の自動化）。
                    if (CanReuseDedicatedSourceWav(
                            part,
                            sliceSourcePath,
                            localStart,
                            localEnd,
                            gain,
                            sliceInfo))
                    {
                        var desiredFileName = string.IsNullOrWhiteSpace(part.FileName)
                            ? $"{track.Name}.wav"
                            : part.FileName;
                        if (!desiredFileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        {
                            desiredFileName += ".wav";
                        }

                        var dest = Path.GetFullPath(Path.Combine(outputDirectory, desiredFileName));
                        var sourceFull = Path.GetFullPath(sliceSourcePath);
                        if (!string.Equals(sourceFull, dest, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!File.Exists(dest)
                                || new FileInfo(dest).Length != new FileInfo(sourceFull).Length
                                || File.GetLastWriteTimeUtc(dest) != File.GetLastWriteTimeUtc(sourceFull))
                            {
                                File.Copy(sourceFull, dest, overwrite: true);
                            }
                        }

                        var needsTrim = localStart != part.ResolveLocalStart()
                            || localEnd != part.ResolveLocalEnd();
                        var effectiveRate = sliceInfo.SampleRate != 0
                            ? sliceInfo.SampleRate
                            : sampleRate;
                        map[trackKey] = new TrackMediaBinding(
                            dest,
                            localStart,
                            localEnd,
                            sliceInfo.FrameCount,
                            effectiveRate,
                            ApplyClipTrim: needsTrim,
                            ReusedOriginal: true);
                        if (loggedReusePaths.Add(dest))
                        {
                            log(UiStrings.LogWavSourceReused(Path.GetFileName(dest)));
                        }

                        log(
                            UiStrings.LogTrackMediaBinding(
                                segment.Name,
                                track.Name,
                                Path.GetFileName(dest),
                                localStart,
                                localEnd,
                                reusedOriginal: true,
                                applyClipTrim: needsTrim));
                        usedFileNames.Add(Path.GetFileName(dest));
                        continue;
                    }

                    // ラウドネス等のゲイン焼き込み時のみ切り出し。
                    var desiredSliceName = string.IsNullOrWhiteSpace(part.FileName)
                        ? $"{track.Name}.wav"
                        : part.FileName;
                    if (!desiredSliceName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    {
                        desiredSliceName += ".wav";
                    }

                    var fileName = UniqueSliceFileName(desiredSliceName, usedFileNames);
                    var destSlice = Path.Combine(outputDirectory, fileName);
                    WriteSegmentSafely(
                        sliceSourcePath,
                        destSlice,
                        localStart,
                        localEnd,
                        sliceBlockAlign,
                        gain,
                        sliceInfo);
                    var writtenInfo = WavFileInfo.Read(destSlice);
                    map[trackKey] = new TrackMediaBinding(
                        Path.GetFullPath(destSlice),
                        0,
                        writtenInfo.FrameCount,
                        writtenInfo.FrameCount,
                        writtenInfo.SampleRate != 0 ? writtenInfo.SampleRate : sampleRate,
                        ApplyClipTrim: false,
                        ReusedOriginal: false);
                    log(Math.Abs(gain - 1f) < 0.000001f
                        ? UiStrings.LogWavSliceWritten(fileName)
                        : UiStrings.LogWavSliceWrittenWithGain(fileName, gain));
                    log(
                        UiStrings.LogTrackMediaBinding(
                            segment.Name,
                            track.Name,
                            fileName,
                            localStart,
                            localEnd,
                            reusedOriginal: false,
                            applyClipTrim: false));
                }
            }
        }

        return map;
    }

    /// <summary>
    /// 複数波形の専用ソースで、焼き込みなしに元 WAV を共有できるか。
    /// 部分範囲は MusicClip トリムで合わせる前提（手動と同じ 2 波形構成）。
    /// </summary>
    private static bool CanReuseDedicatedSourceWav(
        WaveformOutputPart part,
        string sliceSourcePath,
        long localStart,
        long localEnd,
        float gain,
        WavFileInfo sliceInfo)
    {
        if (!part.HasDedicatedSource || sliceInfo.FrameCount <= 0)
        {
            return false;
        }

        var localMin = part.ResolveLocalStart();
        var localMax = part.ResolveLocalEnd();
        if (localStart < localMin || localEnd > localMax || localEnd <= localStart)
        {
            return false;
        }

        if (Math.Abs(gain - 1f) >= 0.000001f)
        {
            return false;
        }

        return File.Exists(sliceSourcePath);
    }

    private readonly record struct TrackMediaBinding(
        string WavPath,
        long SourceStartSample,
        long SourceEndSample,
        long SourceFrameCount,
        uint SampleRate,
        bool ApplyClipTrim,
        bool ReusedOriginal);

    private static void WriteSegmentSafely(
        string sourcePath,
        string destinationPath,
        long startSample,
        long endSample,
        ushort blockAlign,
        float gain,
        WavFileInfo wavInfo)
    {
        if (!string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
        {
            WavSegmentWriter.WriteSegment(
                sourcePath,
                destinationPath,
                startSample,
                endSample,
                blockAlign,
                gain,
                wavInfo);
            return;
        }

        var temporaryPath = destinationPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            WavSegmentWriter.WriteSegment(
                sourcePath,
                temporaryPath,
                startSample,
                endSample,
                blockAlign,
                gain,
                wavInfo);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string TrackSliceKey(string segmentName, string trackName) =>
        segmentName + "\u001f" + trackName;

    private static string UniqueSliceFileName(string desired, HashSet<string> used)
    {
        var name = desired;
        var stem = Path.GetFileNameWithoutExtension(desired);
        var ext = Path.GetExtension(desired);
        var suffix = 2;
        while (!used.Add(name))
        {
            name = $"{stem}_{suffix++}{ext}";
        }

        return name;
    }

    private static long MsToSample(double ms, uint sampleRate) =>
        (long)Math.Round(ms * sampleRate / 1000.0, MidpointRounding.AwayFromZero);

    private static Dictionary<string, object?> BuildStateGroupSetArgs(
        WwiseMusicPlan plan,
        WwiseImportSettings importSettings)
    {
        var stateChildren = plan.Playlists
            .Select(p => (object)new Dictionary<string, object?>
            {
                ["type"] = "State",
                ["name"] = p.Name,
            })
            .ToList();

        return new Dictionary<string, object?>
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = importSettings.StateGroupParentPath.TrimEnd('\\'),
                    ["children"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "StateGroup",
                            ["name"] = plan.ContainerName,
                            ["children"] = stateChildren,
                        },
                    },
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "replaceAll",
        };
    }

    /// <summary>Wwise State Group の Default Transition Time 既定値（秒）。</summary>
    private const double WwiseDefaultStateTransitionSeconds = 1;

    /// <summary>グループ State で非アクティブなレイヤーへ載せる音量（dB）。</summary>
    private const double GroupStateMuteVolumeDb = -108;

    /// <summary>
    /// グループ化 Playlist ごとに State Group（A/B/C…）を作り、各 Music Track へ割当する。
    /// Group Fade が全員同一なら Default Transition Time のみ、異なれば Custom TransitionList。
    /// TransitionList / State Volume は WWU 直編集用パッチとして返す。
    /// </summary>
    private static async Task<(
        List<StateGroupTransitionPatch> TransitionPatches,
        List<MusicTrackStateVolumePatch> VolumePatches)> ApplyGroupStateGroupsAsync(
        WaapiHttpClient client,
        WwiseMusicPlan plan,
        string musicRootPath,
        WwiseImportSettings importSettings,
        Dictionary<string, object> returnOptions,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var transitionPatches = new List<StateGroupTransitionPatch>();
        var volumePatches = new List<MusicTrackStateVolumePatch>();
        var grouped = plan.Playlists
            .Where(playlist => playlist.GroupState is not null)
            .ToList();
        if (grouped.Count == 0)
        {
            return (transitionPatches, volumePatches);
        }

        foreach (var playlist in grouped)
        {
            var groupState = playlist.GroupState!;
            var stateGroupPath = importSettings.ResolveStateGroupPath(groupState.Name);
            log(UiStrings.LogCreatingGroupStateGroup(
                groupState.Name,
                string.Join(", ", groupState.StateNames),
                FormatGroupStateFadeSummary(groupState),
                groupState.UseDefaultTransitionOnly,
                groupState.UseDefaultTransitionOnly
                    ? groupState.DefaultTransitionSeconds
                    : WwiseDefaultStateTransitionSeconds));

            await CallObjectSetAsync(
                    client,
                    BuildGroupStateGroupSetArgs(groupState, importSettings),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            var stateIds = await QueryStateChildrenAsync(
                    client,
                    stateGroupPath,
                    groupState.StateNames,
                    cancellationToken)
                .ConfigureAwait(false);
            var stateGroupId = await QuerySingleReturnStringAsync(
                    client,
                    $"$ \"{stateGroupPath}\"",
                    "id",
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(stateGroupId))
            {
                throw new InvalidOperationException(
                    UiStrings.ErrGroupStateMissing(stateGroupPath, "(id)"));
            }

            var statesWwuPath = await QuerySingleReturnStringAsync(
                    client,
                    $"$ \"{stateGroupPath}\"",
                    "filePath",
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(statesWwuPath) || !File.Exists(statesWwuPath))
            {
                throw new InvalidOperationException(
                    UiStrings.ErrGroupStateWorkUnitNotFound(stateGroupPath));
            }

            transitionPatches.Add(new StateGroupTransitionPatch(
                statesWwuPath,
                groupState.Name,
                stateIds,
                groupState.TransitionSecondsByState,
                groupState.UseDefaultTransitionOnly));

            var playlistPath = ResolvePlaylistObjectPath(plan, musicRootPath, playlist);
            foreach (var segment in playlist.Segments)
            {
                var segmentPath = $"{playlistPath}\\{segment.Name}";
                foreach (var track in segment.Tracks)
                {
                    var activeState = track.LayerStateName;
                    if (string.IsNullOrEmpty(activeState)
                        || !stateIds.ContainsKey(activeState))
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrGroupStateTrackActiveMissing(
                                track.Name,
                                segment.Name,
                                activeState ?? "(null)"));
                    }

                    var trackPath = $"{segmentPath}\\{track.Name}";
                    log(UiStrings.LogAssignGroupStateToTrack(
                        track.Name,
                        segment.Name,
                        groupState.Name));
                    await client.CallAsync(
                            "ak.wwise.core.object.setStateGroups",
                            new Dictionary<string, object?>
                            {
                                ["object"] = trackPath,
                                ["stateGroups"] = new object[] { stateGroupPath },
                            },
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    // Volume を State 連動プロパティとして有効化する。
                    await client.CallAsync(
                            "ak.wwise.core.object.setStateProperties",
                            new Dictionary<string, object?>
                            {
                                ["object"] = trackPath,
                                ["stateProperties"] = new[] { "Volume" },
                            },
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    var trackId = await QuerySingleReturnStringAsync(
                            client,
                            $"$ \"{trackPath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
                            "id",
                            cancellationToken)
                        .ConfigureAwait(false);
                    var trackWwuPath = await QuerySingleReturnStringAsync(
                            client,
                            $"$ \"{trackPath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
                            "filePath",
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (string.IsNullOrEmpty(trackId)
                        || string.IsNullOrEmpty(trackWwuPath)
                        || !File.Exists(trackWwuPath))
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrGroupStateTrackWorkUnitNotFound(trackPath));
                    }

                    log(UiStrings.LogGroupStateTrackVolumePlan(
                        track.Name,
                        activeState,
                        GroupStateMuteVolumeDb));
                    volumePatches.Add(new MusicTrackStateVolumePatch(
                        trackWwuPath,
                        trackId,
                        track.Name,
                        groupState.Name,
                        stateGroupId,
                        stateIds,
                        activeState,
                        GroupStateMuteVolumeDb,
                        WaapiMusicTransitionDefaults.ToWaapiMusicSyncType(
                            track.ChangeOccursAt ?? PlaylistExitSourceMode.Immediate)));
                }
            }
        }

        return (transitionPatches, volumePatches);
    }

    /// <summary>
    /// Authoring プレビュー用に、現在 State を先頭へ設定する。
    /// Music Switch 用 State Group → 先頭 Playlist 名、グループ用 → A。
    /// </summary>
    private static async Task SetInitialStatesForPreviewAsync(
        WaapiHttpClient client,
        WwiseMusicPlan plan,
        WwiseImportSettings importSettings,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (plan.IsMultiPart && plan.Playlists.Count > 0)
        {
            await TrySetSoundEngineStateAsync(
                    client,
                    importSettings.ResolveStateGroupPath(plan.ContainerName),
                    plan.Playlists[0].Name,
                    log,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var playlist in plan.Playlists)
        {
            if (playlist.GroupState is not { } groupState
                || groupState.StateNames.Count == 0)
            {
                continue;
            }

            await TrySetSoundEngineStateAsync(
                    client,
                    importSettings.ResolveStateGroupPath(groupState.Name),
                    groupState.StateNames[0],
                    log,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task TrySetSoundEngineStateAsync(
        WaapiHttpClient client,
        string stateGroupPath,
        string stateName,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        // ak.soundengine.setState はパス不可。State Group／State の name・GUID・Short ID のみ。
        var groupName = stateGroupPath.Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? stateGroupPath;
        try
        {
            await client.CallAsync(
                    "ak.soundengine.setState",
                    new Dictionary<string, object?>
                    {
                        ["stateGroup"] = groupName,
                        ["state"] = stateName,
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            log(UiStrings.LogGroupStateSetInitial(groupName, stateName));
        }
        catch (Exception ex)
        {
            log(UiStrings.LogGroupStateSetInitialFailed(groupName, stateName, ex.Message));
        }
    }

    /// <summary>
    /// インポートした最上位オブジェクト（Switch または単一 Playlist）を Project Explorer で選択する。
    /// Switch の場合は、先に先頭 Playlist を Find してツリーを展開してから Switch を選び直す。
    /// </summary>
    private static async Task TrySelectImportedObjectAsync(
        WaapiHttpClient client,
        string musicRootPath,
        WwiseMusicPlan plan,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        try
        {
            if (plan.IsMultiPart && plan.Playlists.Count > 0)
            {
                // 子を Find すると祖先ノードが展開される。続けて Switch を選び直す。
                var firstChildPath = $"{musicRootPath}\\{plan.Playlists[0].Name}";
                await FindInProjectExplorerAsync(client, firstChildPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            await FindInProjectExplorerAsync(client, musicRootPath, cancellationToken)
                .ConfigureAwait(false);

            log(UiStrings.LogImportedObjectSelected(
                plan.IsMultiPart
                    ? UiStrings.LabelMusicSwitchContainer
                    : UiStrings.LabelMusicPlaylistContainer,
                musicRootPath));
        }
        catch (Exception ex)
        {
            log(UiStrings.LogImportedObjectSelectFailed(musicRootPath, ex.Message));
        }
    }

    private static Task FindInProjectExplorerAsync(
        WaapiHttpClient client,
        string objectPath,
        CancellationToken cancellationToken) =>
        client.CallAsync(
            "ak.wwise.ui.commands.execute",
            new Dictionary<string, object?>
            {
                ["command"] = "FindInProjectExplorerSyncGroup1",
                ["objects"] = new[] { objectPath },
            },
            cancellationToken: cancellationToken);

    private static string FormatGroupStateFadeSummary(WwiseGroupStatePlan groupState)
    {
        if (groupState.StateNames.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ", ",
            groupState.StateNames.Select(name =>
            {
                var seconds = groupState.TransitionSecondsByState.TryGetValue(name, out var value)
                    ? value
                    : 0d;
                return $"{name}={seconds:0.###}s";
            }));
    }

    private static Dictionary<string, object?> BuildGroupStateGroupSetArgs(
        WwiseGroupStatePlan groupState,
        WwiseImportSettings importSettings)
    {
        var stateChildren = groupState.StateNames
            .Select(name => (object)new Dictionary<string, object?>
            {
                ["type"] = "State",
                ["name"] = name,
            })
            .ToList();

        // 全員同一 → Default のみ。個別 Custom 時は Default を Wwise 既定（1s）へ戻し、
        // 以前のフォールバック最大値が残らないようにする。
        var defaultTransitionSeconds = groupState.UseDefaultTransitionOnly
            ? groupState.DefaultTransitionSeconds
            : WwiseDefaultStateTransitionSeconds;

        return new Dictionary<string, object?>
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = importSettings.StateGroupParentPath.TrimEnd('\\'),
                    ["children"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "StateGroup",
                            ["name"] = groupState.Name,
                            ["@DefaultTransitionTime"] = defaultTransitionSeconds,
                            ["children"] = stateChildren,
                        },
                    },
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "replaceAll",
        };
    }

    private static async Task<IReadOnlyDictionary<string, string>> QueryStateChildrenAsync(
        WaapiHttpClient client,
        string stateGroupPath,
        IReadOnlyList<string> expectedNames,
        CancellationToken cancellationToken)
    {
        var escaped = stateGroupPath.Replace("\"", "\\\"", StringComparison.Ordinal);
        var result = await client.CallAsync(
                "ak.wwise.core.object.get",
                new Dictionary<string, object?>
                {
                    ["waql"] = $"$ \"{escaped}\" select children where type = \"State\"",
                },
                new Dictionary<string, object?>
                {
                    ["return"] = new[] { "id", "name", "type" },
                },
                cancellationToken)
            .ConfigureAwait(false);

        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        if (result.TryGetProperty("return", out var arr)
            && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : null;
                var id = item.TryGetProperty("id", out var idEl)
                    ? idEl.GetString()
                    : null;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                {
                    found[name] = id;
                }
            }
        }

        foreach (var expected in expectedNames)
        {
            if (!found.ContainsKey(expected))
            {
                throw new InvalidOperationException(
                    UiStrings.ErrGroupStateMissing(stateGroupPath, expected));
            }
        }

        // ルールは予定 State（A/B/C…）同士のみ。None 等は含めない。
        return expectedNames.ToDictionary(
            name => name,
            name => found[name],
            StringComparer.Ordinal);
    }

    private static string ResolvePlaylistObjectPath(
        WwiseMusicPlan plan,
        string musicRootPath,
        WwisePlaylistPlan playlist) =>
        plan.IsMultiPart
            ? $"{musicRootPath}\\{playlist.Name}"
            : musicRootPath;

    private readonly record struct StateGroupTransitionPatch(
        string WwuPath,
        string StateGroupName,
        IReadOnlyDictionary<string, string> StateIdsByName,
        IReadOnlyDictionary<string, double> TransitionSecondsByState,
        bool UseDefaultTransitionOnly);

    /// <summary>
    /// Music Track の State Volume（対応 State=0dB、他=-108dB）と
    /// Change Occurs At（StateGroupInfo/@MusicSyncType）を WWU へ書くためのパッチ。
    /// </summary>
    private readonly record struct MusicTrackStateVolumePatch(
        string WwuPath,
        string TrackId,
        string TrackName,
        string StateGroupName,
        string StateGroupId,
        IReadOnlyDictionary<string, string> StateIdsByName,
        string ActiveStateName,
        double MuteVolumeDb,
        int MusicSyncType);

    /// <summary>
    /// Music Switch Container 本体（Playlist 子は空、State 割当は後段）。
    /// children を replaceAll で空にすることで、再 EXPORT 時の古い Playlist を落とす。
    /// </summary>
    private static Dictionary<string, object?> BuildMusicSwitchShellSetArgs(
        WwiseMusicPlan plan,
        string parentPath,
        string stateGroupPath) =>
        new()
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = parentPath,
                    ["children"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "MusicSwitchContainer",
                            ["name"] = plan.ContainerName,
                            ["@Arguments"] = new[] { stateGroupPath },
                            ["children"] = Array.Empty<object>(),
                        },
                    },
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "replaceAll",
        };

    /// <summary>
    /// Playlist 作成後に State→Playlist 割当を結ぶ。
    /// </summary>
    private static Dictionary<string, object?> BuildMusicSwitchEntriesSetArgs(
        WwiseMusicPlan plan,
        string containerPath,
        string stateGroupPath)
    {
        var entries = plan.Playlists
            .Select(p => (object)new Dictionary<string, object?>
            {
                ["type"] = "MultiSwitchEntry",
                // 再 EXPORT 時に merge できるよう、Playlist 名で安定させる。
                ["name"] = p.Name,
                ["@EntryPath"] = new[] { $"{stateGroupPath}\\{p.Name}" },
                ["@AudioNode"] = $"{containerPath}\\{p.Name}",
            })
            .ToList();

        return new Dictionary<string, object?>
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = containerPath,
                    ["@Entries"] = entries,
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "replaceAll",
        };
    }

    /// <summary>
    /// Playlist 作成後にトランジション（Any→Any + Any→各 Playlist）を結ぶ。
    /// DestinationContextObject は実在する Playlist パスを参照する必要がある。
    /// </summary>
    private static Dictionary<string, object?> BuildMusicSwitchTransitionsSetArgs(
        WwiseMusicPlan plan,
        string containerPath) =>
        new()
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = containerPath,
                    ["@TransitionRoot"] = WaapiMusicTransitionDefaults.BuildTransitionRoot(
                        containerPath,
                        plan.Playlists),
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "replaceAll",
        };

    private static Dictionary<string, object?> BuildPlaylistAppendSetArgs(
        WwiseMusicPlan plan,
        string containerPath,
        WwisePlaylistPlan playlist,
        IReadOnlyDictionary<string, TrackMediaBinding> segmentMedia,
        WwiseImportSettings importSettings,
        bool applyAutoVolume,
        AutoVolumeTarget autoVolumeTarget,
        IReadOnlyDictionary<int, float>? partGains,
        Action<string> log) =>
        new()
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = containerPath,
                    ["children"] = new object[]
                    {
                        BuildPlaylistDef(
                            plan,
                            containerPath,
                            playlist,
                            segmentMedia,
                            importSettings,
                            isMultiPart: true,
                            applyAutoVolume,
                            autoVolumeTarget,
                            partGains,
                            log),
                    },
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "append",
        };

    private static Dictionary<string, object?> BuildSinglePlaylistSetArgs(
        WwiseMusicPlan plan,
        string parentPath,
        IReadOnlyDictionary<string, TrackMediaBinding> segmentMedia,
        WwiseImportSettings importSettings,
        bool applyAutoVolume,
        AutoVolumeTarget autoVolumeTarget,
        IReadOnlyDictionary<int, float>? partGains,
        Action<string> log)
    {
        var containerPath = $"{parentPath}\\{plan.ContainerName}";
        return new Dictionary<string, object?>
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = parentPath,
                    ["children"] = new object[]
                    {
                        BuildPlaylistDef(
                            plan,
                            containerPath,
                            plan.Playlists[0],
                            segmentMedia,
                            importSettings,
                            isMultiPart: false,
                            applyAutoVolume,
                            autoVolumeTarget,
                            partGains,
                            log),
                    },
                },
            },
            ["onNameConflict"] = "merge",
            ["listMode"] = "replaceAll",
        };
    }

    private static Dictionary<string, object?> BuildPlaylistDef(
        WwiseMusicPlan plan,
        string containerPath,
        WwisePlaylistPlan playlist,
        IReadOnlyDictionary<string, TrackMediaBinding> segmentMedia,
        WwiseImportSettings importSettings,
        bool isMultiPart,
        bool applyAutoVolume,
        AutoVolumeTarget autoVolumeTarget,
        IReadOnlyDictionary<int, float>? partGains,
        Action<string> log)
    {
        var streamEnabled = importSettings.StreamEnabled;
        var lookAheadMs = importSettings.LookAheadMs;
        var prefetchLengthMs = importSettings.PrefetchLengthMs;
        var playlistPath = isMultiPart
            ? $"{containerPath}\\{playlist.Name}"
            : containerPath;

        var segmentDefs = new List<object>();
        var itemDefs = new List<object>();
        for (var i = 0; i < playlist.Segments.Count; i++)
        {
            var segment = playlist.Segments[i];
            segmentDefs.Add(BuildSegmentDef(
                segment,
                segmentMedia,
                isFirstSegment: i == 0,
                streamEnabled,
                lookAheadMs,
                prefetchLengthMs));
            itemDefs.Add(new Dictionary<string, object?>
            {
                ["type"] = "MusicPlaylistItem",
                ["name"] = string.Empty,
                ["@PlaylistItemType"] = 1,
                ["@LoopCount"] = segment.LoopInfinite ? 0 : 1,
                ["@Segment"] = $"{playlistPath}\\{segment.Name}",
            });
        }

        var name = isMultiPart ? playlist.Name : plan.ContainerName;
        var def = new Dictionary<string, object?>
        {
            ["type"] = "MusicPlaylistContainer",
            ["name"] = name,
            ["children"] = segmentDefs,
            ["@PlaylistRoot"] = new Dictionary<string, object?>
            {
                ["type"] = "MusicPlaylistItem",
                ["name"] = string.Empty,
                ["@PlaylistItemType"] = 0,
                ["@PlayMode"] = 0,
                ["@LoopCount"] = 1,
                ["children"] = itemDefs,
            },
        };

        if (applyAutoVolume && partGains is not null)
        {
            var compensationDb = ResolveAutoVolumeCompensationDb(playlist, partGains, log);
            if (autoVolumeTarget == AutoVolumeTarget.VoiceVolume)
            {
                def["@Volume"] = compensationDb;
                def["@MakeUpGain"] = 0f;
                log(
                    $"Auto Volume: playlist {name} → Voice Volume {compensationDb:0.##} dB"
                    + " (Make-Up Gain = 0)");
            }
            else
            {
                def["@MakeUpGain"] = compensationDb;
                def["@Volume"] = 0f;
                log(
                    $"Auto Volume: playlist {name} → Make-Up Gain {compensationDb:0.##} dB"
                    + " (Voice Volume = 0)");
            }
        }

        return def;
    }

    /// <summary>
    /// Playlist 構成パートの線形ゲインから補償 dB を求める。
    /// 複数パートでゲインが食い違う場合は先頭メンバーを使い警告する。
    /// </summary>
    private static float ResolveAutoVolumeCompensationDb(
        WwisePlaylistPlan playlist,
        IReadOnlyDictionary<int, float> partGains,
        Action<string> log)
    {
        if (playlist.SourcePartNumbers.Count == 0)
        {
            return 0f;
        }

        float? firstGain = null;
        var mismatched = false;
        foreach (var partNumber in playlist.SourcePartNumbers)
        {
            if (!partGains.TryGetValue(partNumber, out var gain))
            {
                continue;
            }

            if (firstGain is null)
            {
                firstGain = gain;
                continue;
            }

            if (Math.Abs(gain - firstGain.Value) > 1e-4f)
            {
                mismatched = true;
            }
        }

        if (firstGain is null)
        {
            return 0f;
        }

        if (mismatched)
        {
            log(UiStrings.LogAutoVolumeGainMismatch(playlist.Name, playlist.SourcePartNumbers[0]));
        }

        return CompensationDb(firstGain.Value);
    }

    private static float CompensationDb(float linearGain) =>
        linearGain <= 0f || Math.Abs(linearGain - 1f) < 1e-6f
            ? 0f
            : (float)(-20.0 * Math.Log10(linearGain));

    private static Dictionary<string, object?> BuildSegmentDef(
        WwiseSegmentPlan segment,
        IReadOnlyDictionary<string, TrackMediaBinding> trackMedia,
        bool isFirstSegment,
        bool streamEnabled,
        int lookAheadMs,
        int prefetchLengthMs)
    {
        // 切り出し WAV の先頭がセグメント 0。Cue は相対時刻。
        // 元 WAV 再利用時は作成後に MusicClip トリム＋WWU の PlayAt パッチで範囲を合わせる。
        var origin = segment.ClipStartMs;
        var exitLocal = Math.Max(0.0, segment.ExitCueMs - origin);
        var endLocal = Math.Max(exitLocal, segment.ClipEndMs - origin);

        var trackDefs = new List<object>();
        for (var t = 0; t < segment.Tracks.Count; t++)
        {
            var track = segment.Tracks[t];
            var key = TrackSliceKey(segment.Name, track.Name);
            if (!trackMedia.TryGetValue(key, out var media))
            {
                throw new InvalidOperationException(
                    UiStrings.ErrSlicedWavMissing(segment.Name, track.Name));
            }

            // 先頭セグメント内の全トラック（グループ化レイヤー含む）に
            // Zero latency ＋ Prefetch を付け、2 番目以降は Look-ahead のみ。
            var zeroLatency = streamEnabled && isFirstSegment;
            var trackProps = new Dictionary<string, object?>
            {
                ["type"] = "MusicTrack",
                ["name"] = track.Name,
                ["@IsStreamingEnabled"] = streamEnabled,
                ["import"] = new Dictionary<string, object?>
                {
                    ["files"] = new object[]
                    {
                        new Dictionary<string, object?> { ["audioFile"] = media.WavPath },
                    },
                },
            };
            if (streamEnabled)
            {
                trackProps["@IsZeroLatency"] = zeroLatency;
                trackProps["@LookAheadTime"] = zeroLatency ? 0 : lookAheadMs;
                if (zeroLatency)
                {
                    trackProps["@PreFetchLength"] = prefetchLengthMs;
                }
            }

            trackDefs.Add(trackProps);
        }

        // Entry/Exit/Custom Cue は作成後に listMode=replaceAll で一括設定する
        // （ここへ @Cues を載せる・既定 Cue と二重になる）。
        return new Dictionary<string, object?>
        {
            ["type"] = "MusicSegment",
            ["name"] = segment.Name,
            ["@OverrideClockSettings"] = true,
            ["@Tempo"] = segment.TempoBpm,
            ["@TimeSignatureUpper"] = segment.TimeSignatureUpper,
            ["@TimeSignatureLower"] = segment.TimeSignatureLower,
            ["@EndPosition"] = endLocal,
            ["children"] = trackDefs,
        };
    }

    /// <summary>
    /// 共有 WAV のセグメント範囲を MusicClip Begin/End Offset（ミリ秒）で合わせる。
    /// MusicClip は Track の descendants に出ないことがあるため、from type MusicClip で探す。
    /// 頭トリムしたクリップを 0 位置へ寄せる PlayAt（負値）は WAAPI の制約
    /// [0, 1e10] で設定できないため、必要なパッチ一覧を返し WWU 直接更新へ回す。
    /// </summary>
    private static async Task<List<MusicClipPlayAtFix>> ApplyMusicClipTrimsAsync(
        WaapiHttpClient client,
        WwiseMusicPlan plan,
        string musicRootPath,
        IReadOnlyDictionary<string, TrackMediaBinding> segmentMedia,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var playAtFixes = new List<MusicClipPlayAtFix>();
        var anyTrim = segmentMedia.Values.Any(m => m.ApplyClipTrim);
        if (!anyTrim)
        {
            return playAtFixes;
        }

        // Track 配下の descendants では取れないことがあるため、プロジェクト全体から取る。
        var allClips = await QueryAllMusicClipsAsync(client, cancellationToken)
            .ConfigureAwait(false);
        log(UiStrings.LogMusicClipCatalog(allClips.Count));

        foreach (var playlist in plan.Playlists)
        {
            var playlistPath = plan.IsMultiPart
                ? $"{musicRootPath}\\{playlist.Name}"
                : musicRootPath;
            foreach (var segment in playlist.Segments)
            {
                var segmentPath = $"{playlistPath}\\{segment.Name}";
                foreach (var track in segment.Tracks)
                {
                    var key = TrackSliceKey(segment.Name, track.Name);
                    if (!segmentMedia.TryGetValue(key, out var media) || !media.ApplyClipTrim)
                    {
                        continue;
                    }

                    if (media.SampleRate == 0 || media.SourceFrameCount <= 0)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrMusicClipTrimMissingRate(track.Name, segment.Name));
                    }

                    var beginMs = media.SourceStartSample * 1000.0 / media.SampleRate;
                    // MusicClip のプロパティ規約（WWU 実測）:
                    //   BeginTrimOffset / EndTrimOffset : ソース内の開始／終了位置（絶対ミリ秒）
                    //   PlayAt : ソース先頭のタイムライン位置。手動編集同様、トリム後の内容を
                    //            0 に詰めるには -Begin（負値）が必要だが WAAPI では書けないので
                    //            後段の WWU 直接パッチで設定する。
                    var endMs = media.SourceEndSample * 1000.0 / media.SampleRate;
                    var trackPath = $"{segmentPath}\\{track.Name}";
                    var clipIds = FindMusicClipsForTrack(
                        allClips,
                        trackPath,
                        Path.GetFileNameWithoutExtension(media.WavPath));
                    if (clipIds.Count == 0)
                    {
                        // パス推定でもう一度直接 get を試す。
                        var guessed = await TryGetObjectIdAsync(
                                client,
                                $"{trackPath}\\{Path.GetFileNameWithoutExtension(media.WavPath)}",
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(guessed))
                        {
                            clipIds.Add(guessed);
                        }
                    }

                    if (clipIds.Count == 0)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrMusicClipNotFound(trackPath));
                    }

                    if (clipIds.Count > 1)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrMusicClipAmbiguous(trackPath, clipIds.Count));
                    }

                    var clipId = clipIds[0];
                    await SetClipPropertyAsync(
                            client, clipId, "BeginTrimOffset", beginMs, cancellationToken)
                        .ConfigureAwait(false);
                    await SetClipPropertyAsync(
                            client, clipId, "EndTrimOffset", endMs, cancellationToken)
                        .ConfigureAwait(false);
                    if (beginMs > 0.0005)
                    {
                        playAtFixes.Add(new MusicClipPlayAtFix(clipId, -beginMs));
                    }

                    log(
                        UiStrings.LogMusicClipTrimApplied(
                            track.Name,
                            segment.Name,
                            beginMs,
                            endMs));
                }
            }
        }

        return playAtFixes;
    }

    private readonly record struct MusicClipPlayAtFix(string ClipId, double PlayAtMs);

    private readonly record struct MusicClipFadeDurationFix(
        string ClipId,
        double? FadeInDurationMs,
        double? FadeOutDurationMs);

    /// <summary>
    /// リージョン端フェードを MusicClip の非破壊 Fade として設定する。
    /// WAAPI 上限（3.6 秒）までの Duration と Mode／Shape は API で書き、
    /// 超過分は WWU パッチ用に返す。
    /// </summary>
    private static async Task<List<MusicClipFadeDurationFix>> ApplyMusicClipFadesAsync(
        WaapiHttpClient client,
        WwiseMusicPlan plan,
        string musicRootPath,
        IReadOnlyDictionary<string, TrackMediaBinding> segmentMedia,
        IReadOnlyList<RegionEdgeFade> fades,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var durationFixes = new List<MusicClipFadeDurationFix>();
        if (fades.Count == 0)
        {
            return durationFixes;
        }

        var allClips = await QueryAllMusicClipsAsync(client, cancellationToken)
            .ConfigureAwait(false);
        log(UiStrings.LogMusicClipFadeCatalog(allClips.Count));

        var pendingByClip = new Dictionary<string, MusicClipFadeDurationFix>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var playlist in plan.Playlists)
        {
            var playlistPath = plan.IsMultiPart
                ? $"{musicRootPath}\\{playlist.Name}"
                : musicRootPath;
            foreach (var segment in playlist.Segments)
            {
                var segmentPath = $"{playlistPath}\\{segment.Name}";
                foreach (var track in segment.Tracks)
                {
                    var key = TrackSliceKey(segment.Name, track.Name);
                    if (!segmentMedia.TryGetValue(key, out var media))
                    {
                        continue;
                    }

                    var rate = media.SampleRate;
                    if (rate == 0)
                    {
                        continue;
                    }

                    RegionEdgeFade? matchedFadeIn = null;
                    RegionEdgeFade? matchedFadeOut = null;
                    foreach (var fade in fades)
                    {
                        if (fade.HasFadeIn && track.AbsoluteStartSample == fade.InSample)
                        {
                            matchedFadeIn = fade;
                        }

                        if (fade.HasFadeOut && track.AbsoluteEndSample == fade.OutSample)
                        {
                            matchedFadeOut = fade;
                        }
                    }

                    if (matchedFadeIn is null && matchedFadeOut is null)
                    {
                        continue;
                    }

                    var trackPath = $"{segmentPath}\\{track.Name}";
                    var clipIds = FindMusicClipsForTrack(
                        allClips,
                        trackPath,
                        Path.GetFileNameWithoutExtension(media.WavPath));
                    if (clipIds.Count == 0)
                    {
                        var guessed = await TryGetObjectIdAsync(
                                client,
                                $"{trackPath}\\{Path.GetFileNameWithoutExtension(media.WavPath)}",
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(guessed))
                        {
                            clipIds.Add(guessed);
                        }
                    }

                    if (clipIds.Count == 0)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrMusicClipNotFound(trackPath));
                    }

                    if (clipIds.Count > 1)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrMusicClipAmbiguous(trackPath, clipIds.Count));
                    }

                    var clipId = clipIds[0];
                    double? trueFadeInMs = null;
                    double? trueFadeOutMs = null;
                    double? loggedFadeInMs = null;
                    double? loggedFadeOutMs = null;

                    if (matchedFadeIn is { } fadeIn)
                    {
                        var samples = fadeIn.EffectiveFadeInEnd - fadeIn.InSample;
                        loggedFadeInMs = samples * 1000.0 / rate;
                        var waapiMs = Math.Min(loggedFadeInMs.Value, WaapiMusicClipFadeMaxMs);
                        await SetClipPropertyAsync(
                                client, clipId, "FadeInMode", MusicClipFadeModeManual, cancellationToken)
                            .ConfigureAwait(false);
                        await SetClipPropertyAsync(
                                client,
                                clipId,
                                "FadeInShape",
                                RegionEdgeFade.ToWwiseShape(fadeIn.FadeInCurve),
                                cancellationToken)
                            .ConfigureAwait(false);
                        await SetClipPropertyAsync(
                                client, clipId, "FadeInDuration", waapiMs, cancellationToken)
                            .ConfigureAwait(false);
                        if (loggedFadeInMs.Value > WaapiMusicClipFadeMaxMs)
                        {
                            trueFadeInMs = loggedFadeInMs;
                        }
                    }

                    if (matchedFadeOut is { } fadeOut)
                    {
                        var samples = fadeOut.OutSample - fadeOut.EffectiveFadeOutStart;
                        loggedFadeOutMs = samples * 1000.0 / rate;
                        var waapiMs = Math.Min(loggedFadeOutMs.Value, WaapiMusicClipFadeMaxMs);
                        await SetClipPropertyAsync(
                                client, clipId, "FadeOutMode", MusicClipFadeModeManual, cancellationToken)
                            .ConfigureAwait(false);
                        await SetClipPropertyAsync(
                                client,
                                clipId,
                                "FadeOutShape",
                                RegionEdgeFade.ToWwiseShape(fadeOut.FadeOutCurve),
                                cancellationToken)
                            .ConfigureAwait(false);
                        await SetClipPropertyAsync(
                                client, clipId, "FadeOutDuration", waapiMs, cancellationToken)
                            .ConfigureAwait(false);
                        if (loggedFadeOutMs.Value > WaapiMusicClipFadeMaxMs)
                        {
                            trueFadeOutMs = loggedFadeOutMs;
                        }
                    }

                    log(
                        UiStrings.LogMusicClipFadeApplied(
                            track.Name,
                            segment.Name,
                            loggedFadeInMs,
                            loggedFadeOutMs));

                    if (trueFadeInMs is not null || trueFadeOutMs is not null)
                    {
                        if (pendingByClip.TryGetValue(clipId, out var existing))
                        {
                            pendingByClip[clipId] = new MusicClipFadeDurationFix(
                                clipId,
                                trueFadeInMs ?? existing.FadeInDurationMs,
                                trueFadeOutMs ?? existing.FadeOutDurationMs);
                        }
                        else
                        {
                            pendingByClip[clipId] = new MusicClipFadeDurationFix(
                                clipId,
                                trueFadeInMs,
                                trueFadeOutMs);
                        }
                    }
                }
            }
        }

        durationFixes.AddRange(pendingByClip.Values);
        if (durationFixes.Count > 0)
        {
            log(UiStrings.LogMusicClipFadeExceedsWaapi(durationFixes.Count, WaapiMusicClipFadeMaxMs));
        }

        return durationFixes;
    }

    private readonly record struct MusicTransitionFadePatch(
        string TransitionName,
        double FadeInSeconds,
        double FadeOutSeconds,
        RegionFadeCurveKind FadeInCurve,
        RegionFadeCurveKind FadeOutCurve,
        bool PlayPostExit);

    /// <summary>
    /// 負の PlayAt・WAAPI 上限超の Clip Fade Duration・Playlist 遷移 MusicFade・
    /// Group State の旧 TransitionList クリア／Track State Volume を WWU 直接編集で設定する。
    /// 手順: project.save → 対象 WWU 特定 → project.close → XML パッチ → project.open。
    /// </summary>
    private static async Task ApplyWorkUnitPatchesAsync(
        WaapiHttpClient client,
        string musicRootPath,
        IReadOnlyList<MusicClipPlayAtFix> playAtFixes,
        IReadOnlyList<MusicClipFadeDurationFix> fadeFixes,
        IReadOnlyList<MusicTransitionFadePatch> transitionFades,
        IReadOnlyList<StateGroupTransitionPatch> groupStateTransitions,
        IReadOnlyList<MusicTrackStateVolumePatch> groupStateVolumes,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (playAtFixes.Count == 0
            && fadeFixes.Count == 0
            && transitionFades.Count == 0
            && groupStateTransitions.Count == 0
            && groupStateVolumes.Count == 0)
        {
            return;
        }

        var patches = new Dictionary<string, MusicClipWorkUnitPatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var fix in playAtFixes)
        {
            if (!patches.TryGetValue(fix.ClipId, out var patch))
            {
                patch = new MusicClipWorkUnitPatch(fix.ClipId);
                patches[fix.ClipId] = patch;
            }

            patch.PlayAtMs = fix.PlayAtMs;
        }

        foreach (var fix in fadeFixes)
        {
            if (!patches.TryGetValue(fix.ClipId, out var patch))
            {
                patch = new MusicClipWorkUnitPatch(fix.ClipId);
                patches[fix.ClipId] = patch;
            }

            if (fix.FadeInDurationMs is { } fadeIn)
            {
                patch.FadeInDurationMs = fadeIn;
            }

            if (fix.FadeOutDurationMs is { } fadeOut)
            {
                patch.FadeOutDurationMs = fadeOut;
            }
        }

        var patchList = patches.Values.ToList();
        log(UiStrings.LogWorkUnitPatchStart(
            playAtFixes.Count,
            fadeFixes.Count,
            transitionFades.Count,
            groupStateTransitions.Count + groupStateVolumes.Count));

        var clipFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var patch in patchList)
        {
            var filePath = await QuerySingleReturnStringAsync(
                    client,
                    $"$ \"{patch.ClipId}\"",
                    "filePath",
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new InvalidOperationException(
                    UiStrings.ErrPlayAtWorkUnitNotFound(patch.ClipId));
            }

            clipFiles[patch.ClipId] = filePath;
        }

        // MusicTransition は TransitionRoot 配下で name 照会が不安定なため、
        // Switch Container 自体の WWU を開き、Destination 参照でルールを特定する。
        string? transitionWwuPath = null;
        if (transitionFades.Count > 0)
        {
            transitionWwuPath = await QuerySingleReturnStringAsync(
                    client,
                    $"$ \"{musicRootPath}\"",
                    "filePath",
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(transitionWwuPath) || !File.Exists(transitionWwuPath))
            {
                throw new InvalidOperationException(
                    UiStrings.ErrMusicTransitionWorkUnitNotFound(musicRootPath));
            }
        }

        var projectPath = await QuerySingleReturnStringAsync(
                client,
                "$ from type Project",
                "filePath",
                cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath))
        {
            throw new InvalidOperationException(UiStrings.ErrPlayAtProjectPathUnknown);
        }

        await client.CallAsync(
                "ak.wwise.core.project.save",
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await client.CallAsync(
                "ak.wwise.ui.project.close",
                new Dictionary<string, object?> { ["bypassSave"] = true },
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);

        try
        {
            await WaitForProjectClosedAsync(client).ConfigureAwait(false);

            foreach (var group in patchList.GroupBy(p => clipFiles[p.ClipId], StringComparer.OrdinalIgnoreCase))
            {
                PatchMusicClipPropertiesInWorkUnitFile(group.Key, group.ToList(), log);
            }

            if (transitionWwuPath is not null)
            {
                PatchMusicTransitionFadesInWorkUnitFile(transitionWwuPath, transitionFades, log);
                // MusicFade / Enable は TransitionInfo 配下で WAAPI 照会が不安定なため、
                // 再オープン前に WWU 上で検証する。
                VerifyMusicTransitionFadesInWorkUnitFile(transitionWwuPath, transitionFades);
            }

            if (groupStateTransitions.Count > 0)
            {
                foreach (var group in groupStateTransitions.GroupBy(
                             p => p.WwuPath,
                             StringComparer.OrdinalIgnoreCase))
                {
                    PatchStateGroupTransitionListInWorkUnitFile(
                        group.Key,
                        group.ToList(),
                        log);
                    VerifyStateGroupTransitionListInWorkUnitFile(
                        group.Key,
                        group.ToList());
                }
            }

            if (groupStateVolumes.Count > 0)
            {
                foreach (var group in groupStateVolumes.GroupBy(
                             p => p.WwuPath,
                             StringComparer.OrdinalIgnoreCase))
                {
                    PatchMusicTrackStateVolumesInWorkUnitFile(
                        group.Key,
                        group.ToList(),
                        log);
                    VerifyMusicTrackStateVolumesInWorkUnitFile(
                        group.Key,
                        group.ToList());
                }
            }
        }
        finally
        {
            log(UiStrings.LogPlayAtProjectReopen(Path.GetFileName(projectPath)));
            await CallWithLockRetryAsync(
                    client,
                    "ak.wwise.ui.project.open",
                    new Dictionary<string, object?>
                    {
                        ["path"] = projectPath,
                        ["bypassSave"] = true,
                    })
                .ConfigureAwait(false);
        }

        await WaitForProjectLoadedAsync(client, projectPath).ConfigureAwait(false);

        foreach (var patch in patchList)
        {
            if (patch.PlayAtMs is { } playAtMs)
            {
                await VerifyClipReal64PropertyAsync(
                        client,
                        patch.ClipId,
                        "@PlayAt",
                        playAtMs,
                        (expected, actual) =>
                            UiStrings.ErrPlayAtVerifyFailed(patch.ClipId, expected, actual))
                    .ConfigureAwait(false);
            }

            if (patch.FadeInDurationMs is { } fadeInMs)
            {
                await VerifyClipReal64PropertyAsync(
                        client,
                        patch.ClipId,
                        "@FadeInDuration",
                        fadeInMs,
                        (expected, actual) =>
                            UiStrings.ErrMusicClipFadeVerifyFailed(
                                patch.ClipId, "FadeInDuration", expected, actual))
                    .ConfigureAwait(false);
            }

            if (patch.FadeOutDurationMs is { } fadeOutMs)
            {
                await VerifyClipReal64PropertyAsync(
                        client,
                        patch.ClipId,
                        "@FadeOutDuration",
                        fadeOutMs,
                        (expected, actual) =>
                            UiStrings.ErrMusicClipFadeVerifyFailed(
                                patch.ClipId, "FadeOutDuration", expected, actual))
                    .ConfigureAwait(false);
            }
        }

        if (patchList.Count > 0)
        {
            log(UiStrings.LogMusicClipWorkUnitPatchDone(patchList.Count));
        }

        if (transitionFades.Count > 0)
        {
            log(UiStrings.LogMusicTransitionFadePatchDone(transitionFades.Count));
        }

        if (groupStateTransitions.Count > 0)
        {
            log(UiStrings.LogGroupStateTransitionPatchDone(groupStateTransitions.Count));
        }

        if (groupStateVolumes.Count > 0)
        {
            log(UiStrings.LogGroupStateVolumePatchDone(groupStateVolumes.Count));
        }
    }

    /// <summary>
    /// Group Fade が全員同一なら TransitionList をクリア（Default のみ）。
    /// 異なれば Custom Transition Time ルールを書く（From→To の Time は遷移先 To）。
    /// Default Transition Time は WAAPI 側で設定済み。
    /// </summary>
    private static void PatchStateGroupTransitionListInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<StateGroupTransitionPatch> patches,
        Action<string> log)
    {
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);
        var ruleCount = 0;
        var clearedGroups = 0;

        foreach (var patch in patches)
        {
            var stateGroup = FindStateGroupElement(doc, patch.StateGroupName)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrGroupStateXmlMissing(patch.StateGroupName, wwuPath));

            if (patch.UseDefaultTransitionOnly)
            {
                var existing = stateGroup.SelectSingleNode("TransitionList") as System.Xml.XmlElement;
                if (existing is not null)
                {
                    stateGroup.RemoveChild(existing);
                    clearedGroups++;
                }

                continue;
            }

            var names = patch.StateIdsByName.Keys.ToList();
            var transitionList = stateGroup.SelectSingleNode("TransitionList") as System.Xml.XmlElement;
            if (transitionList is null)
            {
                transitionList = doc.CreateElement("TransitionList");
                var childrenList = stateGroup.SelectSingleNode("ChildrenList");
                if (childrenList?.NextSibling is System.Xml.XmlNode insertBefore)
                {
                    stateGroup.InsertBefore(transitionList, insertBefore);
                }
                else
                {
                    stateGroup.AppendChild(transitionList);
                }
            }
            else
            {
                transitionList.RemoveAll();
            }

            foreach (var fromName in names)
            {
                foreach (var toName in names)
                {
                    if (string.Equals(fromName, toName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var fromId = patch.StateIdsByName[fromName];
                    var toId = patch.StateIdsByName[toName];
                    var seconds = ResolveTransitionSecondsForDestination(patch, toName);
                    var transition = doc.CreateElement("Transition");

                    var startState = doc.CreateElement("StartState");
                    startState.SetAttribute("Name", fromName);
                    startState.SetAttribute("ID", fromId);
                    transition.AppendChild(startState);

                    var endState = doc.CreateElement("EndState");
                    endState.SetAttribute("Name", toName);
                    endState.SetAttribute("ID", toId);
                    transition.AppendChild(endState);

                    var time = doc.CreateElement("Time");
                    time.InnerText = FormatTransitionTime(seconds);
                    transition.AppendChild(time);

                    var isShared = doc.CreateElement("IsShared");
                    isShared.InnerText = "false";
                    transition.AppendChild(isShared);

                    transitionList.AppendChild(transition);
                    ruleCount++;
                }
            }
        }

        doc.Save(wwuPath);
        if (clearedGroups > 0)
        {
            log(UiStrings.LogGroupStateTransitionClearFile(Path.GetFileName(wwuPath), clearedGroups));
        }

        if (ruleCount > 0)
        {
            log(UiStrings.LogGroupStateTransitionPatchFile(Path.GetFileName(wwuPath), ruleCount));
        }
    }

    private static void VerifyStateGroupTransitionListInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<StateGroupTransitionPatch> patches)
    {
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var stateGroup = FindStateGroupElement(doc, patch.StateGroupName)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrGroupStateXmlMissing(patch.StateGroupName, wwuPath));

            var expected = CountStateTransitionRules(patch);
            var transitions = stateGroup.SelectNodes("TransitionList/Transition");
            var actual = transitions?.Count ?? 0;
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    UiStrings.ErrGroupStateTransitionVerifyFailed(
                        patch.StateGroupName,
                        expected,
                        actual));
            }

            if (patch.UseDefaultTransitionOnly)
            {
                continue;
            }

            var names = patch.StateIdsByName.Keys.ToList();
            foreach (var fromName in names)
            {
                foreach (var toName in names)
                {
                    if (string.Equals(fromName, toName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var node = FindStateTransitionElement(stateGroup, fromName, toName)
                        ?? throw new InvalidOperationException(
                            UiStrings.ErrGroupStateTransitionRuleMissing(
                                patch.StateGroupName,
                                fromName,
                                toName));

                    var expectedSeconds = ResolveTransitionSecondsForDestination(patch, toName);
                    var timeText = node.SelectSingleNode("Time")?.InnerText;
                    if (!double.TryParse(
                            timeText,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var seconds)
                        || Math.Abs(seconds - expectedSeconds) > 1e-6)
                    {
                        throw new InvalidOperationException(
                            UiStrings.ErrGroupStateTransitionTimeVerifyFailed(
                                patch.StateGroupName,
                                fromName,
                                toName,
                                expectedSeconds,
                                timeText));
                    }
                }
            }
        }
    }

    private static System.Xml.XmlElement? FindStateGroupElement(
        System.Xml.XmlDocument doc,
        string stateGroupName)
    {
        var nodes = doc.SelectNodes("//StateGroup");
        if (nodes is null)
        {
            return null;
        }

        foreach (System.Xml.XmlNode node in nodes)
        {
            if (node is System.Xml.XmlElement element
                && string.Equals(
                    element.GetAttribute("Name"),
                    stateGroupName,
                    StringComparison.Ordinal))
            {
                return element;
            }
        }

        return null;
    }

    private static System.Xml.XmlElement? FindStateTransitionElement(
        System.Xml.XmlElement stateGroup,
        string fromName,
        string toName)
    {
        var nodes = stateGroup.SelectNodes("TransitionList/Transition");
        if (nodes is null)
        {
            return null;
        }

        foreach (System.Xml.XmlNode node in nodes)
        {
            if (node is not System.Xml.XmlElement transition)
            {
                continue;
            }

            var start = transition.SelectSingleNode("StartState") as System.Xml.XmlElement;
            var end = transition.SelectSingleNode("EndState") as System.Xml.XmlElement;
            if (start is null || end is null)
            {
                continue;
            }

            if (string.Equals(start.GetAttribute("Name"), fromName, StringComparison.Ordinal)
                && string.Equals(end.GetAttribute("Name"), toName, StringComparison.Ordinal))
            {
                return transition;
            }
        }

        return null;
    }

    private static int CountStateTransitionRules(StateGroupTransitionPatch patch)
    {
        if (patch.UseDefaultTransitionOnly)
        {
            return 0;
        }

        var n = patch.StateIdsByName.Count;
        return n <= 1 ? 0 : n * (n - 1);
    }

    private static double ResolveTransitionSecondsForDestination(
        StateGroupTransitionPatch patch,
        string toStateName) =>
        patch.TransitionSecondsByState.TryGetValue(toStateName, out var seconds)
            ? Math.Max(0, seconds)
            : 0;

    private static string FormatTransitionTime(double seconds) =>
        seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Music Track の StateInfo／CustomStateList に Volume を書く。
    /// 対応 State は 0dB（Property 省略）、他 State は MuteVolumeDb。
    /// </summary>
    private static void PatchMusicTrackStateVolumesInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<MusicTrackStateVolumePatch> patches,
        Action<string> log)
    {
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var track = FindMusicTrackElementById(doc, patch.TrackId)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrGroupStateTrackXmlMissing(patch.TrackName, patch.TrackId, wwuPath));

            var stateInfo = track.SelectSingleNode("StateInfo") as System.Xml.XmlElement;
            if (stateInfo is null)
            {
                stateInfo = doc.CreateElement("StateInfo");
                var objectLists = track.SelectSingleNode("ObjectLists");
                if (objectLists is not null)
                {
                    track.InsertBefore(stateInfo, objectLists);
                }
                else
                {
                    track.AppendChild(stateInfo);
                }
            }

            // StateGroupList を確実に用意する（setStateGroups 済みでも欠けている場合に備える）。
            var stateGroupList = stateInfo.SelectSingleNode("StateGroupList") as System.Xml.XmlElement;
            if (stateGroupList is null)
            {
                stateGroupList = doc.CreateElement("StateGroupList");
                var customListExisting = stateInfo.SelectSingleNode("CustomStateList");
                if (customListExisting is not null)
                {
                    stateInfo.InsertBefore(stateGroupList, customListExisting);
                }
                else
                {
                    stateInfo.AppendChild(stateGroupList);
                }
            }

            if (!StateGroupListContains(stateGroupList, patch.StateGroupName))
            {
                stateGroupList.RemoveAll();
                var groupInfo = doc.CreateElement("StateGroupInfo");
                var groupRef = doc.CreateElement("StateGroupRef");
                groupRef.SetAttribute("Name", patch.StateGroupName);
                groupRef.SetAttribute("ID", patch.StateGroupId);
                groupInfo.AppendChild(groupRef);
                stateGroupList.AppendChild(groupInfo);
            }

            ApplyMusicSyncTypeToStateGroupInfo(stateGroupList, patch);

            var customStateList = stateInfo.SelectSingleNode("CustomStateList") as System.Xml.XmlElement;
            if (customStateList is null)
            {
                customStateList = doc.CreateElement("CustomStateList");
                stateInfo.AppendChild(customStateList);
            }
            else
            {
                customStateList.RemoveAll();
            }

            foreach (var (stateName, stateId) in patch.StateIdsByName)
            {
                var isActive = string.Equals(
                    stateName,
                    patch.ActiveStateName,
                    StringComparison.Ordinal);
                customStateList.AppendChild(
                    BuildCustomStateVolumeElement(
                        doc,
                        stateName,
                        stateId,
                        isActive ? null : patch.MuteVolumeDb));
            }
        }

        doc.Save(wwuPath);
        log(UiStrings.LogGroupStateVolumePatchFile(Path.GetFileName(wwuPath), patches.Count));
    }

    private static void VerifyMusicTrackStateVolumesInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<MusicTrackStateVolumePatch> patches)
    {
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var track = FindMusicTrackElementById(doc, patch.TrackId)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrGroupStateTrackXmlMissing(patch.TrackName, patch.TrackId, wwuPath));

            foreach (var (stateName, _) in patch.StateIdsByName)
            {
                var isActive = string.Equals(
                    stateName,
                    patch.ActiveStateName,
                    StringComparison.Ordinal);
                var expected = isActive ? 0.0 : patch.MuteVolumeDb;
                var actual = ReadCustomStateVolume(track, stateName);
                if (actual is null
                    || Math.Abs(actual.Value - expected) > 1e-6)
                {
                    throw new InvalidOperationException(
                        UiStrings.ErrGroupStateVolumeVerifyFailed(
                            patch.TrackName,
                            stateName,
                            expected,
                            actual?.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)
                            ?? "(null)"));
                }
            }

            var syncType = ReadStateGroupMusicSyncType(track, patch.StateGroupName);
            if (syncType != patch.MusicSyncType)
            {
                throw new InvalidOperationException(
                    UiStrings.ErrGroupStateMusicSyncTypeVerifyFailed(
                        patch.TrackName,
                        patch.StateGroupName,
                        patch.MusicSyncType,
                        syncType));
            }
        }
    }

    private static System.Xml.XmlElement BuildCustomStateVolumeElement(
        System.Xml.XmlDocument doc,
        string stateName,
        string stateId,
        double? volumeDb)
    {
        var wrapper = doc.CreateElement("CustomState");
        var stateRef = doc.CreateElement("StateRef");
        stateRef.SetAttribute("Name", stateName);
        stateRef.SetAttribute("ID", stateId);
        wrapper.AppendChild(stateRef);

        var custom = doc.CreateElement("CustomState");
        custom.SetAttribute("Name", string.Empty);
        custom.SetAttribute("ID", $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}");
        if (volumeDb is { } db)
        {
            var propertyList = doc.CreateElement("PropertyList");
            var property = doc.CreateElement("Property");
            property.SetAttribute("Name", "Volume");
            property.SetAttribute("Type", "Real64");
            property.SetAttribute(
                "Value",
                db.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            propertyList.AppendChild(property);
            custom.AppendChild(propertyList);
        }

        wrapper.AppendChild(custom);
        return wrapper;
    }

    private static double? ReadCustomStateVolume(System.Xml.XmlElement track, string stateName)
    {
        var nodes = track.SelectNodes("StateInfo/CustomStateList/CustomState");
        if (nodes is null)
        {
            return null;
        }

        foreach (System.Xml.XmlNode node in nodes)
        {
            if (node is not System.Xml.XmlElement wrapper)
            {
                continue;
            }

            var stateRef = wrapper.SelectSingleNode("StateRef") as System.Xml.XmlElement;
            if (stateRef is null
                || !string.Equals(
                    stateRef.GetAttribute("Name"),
                    stateName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var volumeNode = wrapper.SelectSingleNode(
                "CustomState/PropertyList/Property[@Name='Volume']") as System.Xml.XmlElement;
            if (volumeNode is null)
            {
                // Property 省略 = 0 dB
                return 0.0;
            }

            var value = volumeNode.GetAttribute("Value");
            if (double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var db))
            {
                return db;
            }

            return null;
        }

        return null;
    }

    private static System.Xml.XmlElement? FindMusicTrackElementById(
        System.Xml.XmlDocument doc,
        string trackId)
    {
        var nodes = doc.SelectNodes("//MusicTrack");
        if (nodes is null)
        {
            return null;
        }

        foreach (System.Xml.XmlNode node in nodes)
        {
            if (node is System.Xml.XmlElement element
                && string.Equals(
                    element.GetAttribute("ID"),
                    trackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        return null;
    }

    private static bool StateGroupListContains(
        System.Xml.XmlElement stateGroupList,
        string stateGroupName)
    {
        var refs = stateGroupList.SelectNodes("StateGroupInfo/StateGroupRef");
        if (refs is null)
        {
            return false;
        }

        foreach (System.Xml.XmlNode node in refs)
        {
            if (node is System.Xml.XmlElement element
                && string.Equals(
                    element.GetAttribute("Name"),
                    stateGroupName,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// StateGroupInfo/@MusicSyncType（UI: Change Occurs At）を設定する。
    /// </summary>
    private static void ApplyMusicSyncTypeToStateGroupInfo(
        System.Xml.XmlElement stateGroupList,
        MusicTrackStateVolumePatch patch)
    {
        var infos = stateGroupList.SelectNodes("StateGroupInfo");
        if (infos is null)
        {
            return;
        }

        foreach (System.Xml.XmlNode node in infos)
        {
            if (node is not System.Xml.XmlElement groupInfo)
            {
                continue;
            }

            var groupRef = groupInfo.SelectSingleNode("StateGroupRef") as System.Xml.XmlElement;
            if (groupRef is null
                || !string.Equals(
                    groupRef.GetAttribute("Name"),
                    patch.StateGroupName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            groupInfo.SetAttribute(
                "MusicSyncType",
                patch.MusicSyncType.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
    }

    private static int? ReadStateGroupMusicSyncType(
        System.Xml.XmlElement track,
        string stateGroupName)
    {
        var infos = track.SelectNodes("StateInfo/StateGroupList/StateGroupInfo");
        if (infos is null)
        {
            return null;
        }

        foreach (System.Xml.XmlNode node in infos)
        {
            if (node is not System.Xml.XmlElement groupInfo)
            {
                continue;
            }

            var groupRef = groupInfo.SelectSingleNode("StateGroupRef") as System.Xml.XmlElement;
            if (groupRef is null
                || !string.Equals(
                    groupRef.GetAttribute("Name"),
                    stateGroupName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var raw = groupInfo.GetAttribute("MusicSyncType");
            if (string.IsNullOrEmpty(raw))
            {
                // スキーマ既定は Immediate (0)。
                return 0;
            }

            return int.TryParse(
                raw,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;
        }

        return null;
    }

    private static void VerifyMusicTransitionFadesInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<MusicTransitionFadePatch> patches)
    {
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var transitionNode = FindMusicTransitionElement(doc, patch.TransitionName)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrMusicTransitionXmlMissing(patch.TransitionName, wwuPath));

            VerifyBoolProperty(
                transitionNode,
                "EnableSourceFadeOut",
                patch.FadeOutSeconds > 0,
                patch.TransitionName);
            VerifyBoolProperty(
                transitionNode,
                "EnableDestinationFadeIn",
                patch.FadeInSeconds > 0,
                patch.TransitionName);
            VerifyBoolProperty(
                transitionNode,
                "PlaySourcePostExit",
                patch.PlayPostExit,
                patch.TransitionName);

            if (patch.FadeOutSeconds > 0)
            {
                VerifyMusicFadeTimeInXml(
                    transitionNode,
                    "SourceFadeOut",
                    patch.FadeOutSeconds,
                    patch.TransitionName,
                    "Source Fade-out");
            }
            else if (transitionNode.SelectSingleNode("TransitionInfo/SourceFadeOut") is not null)
            {
                throw new InvalidOperationException(
                    UiStrings.ErrMusicTransitionFadeTimeVerifyFailed(
                        patch.TransitionName, "Source Fade-out", 0, null));
            }

            if (patch.FadeInSeconds > 0)
            {
                VerifyMusicFadeTimeInXml(
                    transitionNode,
                    "DestinationFadeIn",
                    patch.FadeInSeconds,
                    patch.TransitionName,
                    "Destination Fade-in");
            }
            else if (transitionNode.SelectSingleNode("TransitionInfo/DestinationFadeIn") is not null)
            {
                throw new InvalidOperationException(
                    UiStrings.ErrMusicTransitionFadeTimeVerifyFailed(
                        patch.TransitionName, "Destination Fade-in", 0, null));
            }
        }
    }

    private static void VerifyBoolProperty(
        System.Xml.XmlElement transitionNode,
        string propertyName,
        bool expected,
        string transitionName)
    {
        var prop = transitionNode.SelectSingleNode($"PropertyList/Property[@Name='{propertyName}']")
            as System.Xml.XmlElement;
        var actualText = prop?.GetAttribute("Value");
        var actual = string.Equals(actualText, "True", StringComparison.OrdinalIgnoreCase)
            || actualText == "1";
        if (prop is null)
        {
            // 未記載は false 扱い。
            actual = false;
        }

        if (actual != expected)
        {
            throw new InvalidOperationException(
                UiStrings.ErrMusicTransitionFadeVerifyFailed(
                    transitionName, propertyName, expected, actual));
        }
    }

    private static void VerifyMusicFadeTimeInXml(
        System.Xml.XmlElement transitionNode,
        string wrapperName,
        double expectedSeconds,
        string transitionName,
        string fadeName)
    {
        var timeProp = transitionNode.SelectSingleNode(
                $"TransitionInfo/{wrapperName}/MusicFade/PropertyList/Property[@Name='FadeTime']")
            as System.Xml.XmlElement;
        if (timeProp is null
            || !double.TryParse(
                timeProp.GetAttribute("Value"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var actual)
            || Math.Abs(actual - expectedSeconds) > 0.01)
        {
            double? actualNullable = timeProp is not null
                && double.TryParse(
                    timeProp.GetAttribute("Value"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed)
                ? parsed
                : null;
            throw new InvalidOperationException(
                UiStrings.ErrMusicTransitionFadeTimeVerifyFailed(
                    transitionName, fadeName, expectedSeconds, actualNullable));
        }
    }

    /// <summary>WWU（XML）内の MusicTransition に MusicFade Time を直接書き込む。</summary>
    private static void PatchMusicTransitionFadesInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<MusicTransitionFadePatch> patches,
        Action<string> log)
    {
        WaitForExclusiveFileAccess(wwuPath);

        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var transitionNode = FindMusicTransitionElement(doc, patch.TransitionName);
            if (transitionNode is null)
            {
                throw new InvalidOperationException(
                    UiStrings.ErrMusicTransitionXmlMissing(patch.TransitionName, wwuPath));
            }

            var propertyList = EnsureChildElement(doc, transitionNode, "PropertyList", prepend: true);
            // ルール名は Playlist 名に書き換えない。空なら Transition に揃える。
            if (string.IsNullOrWhiteSpace(transitionNode.GetAttribute("Name")))
            {
                transitionNode.SetAttribute("Name", WaapiMusicTransitionDefaults.DefaultAnyToAnyName);
            }

            UpsertBoolProperty(doc, propertyList, "EnableSourceFadeOut", patch.FadeOutSeconds > 0);
            UpsertBoolProperty(doc, propertyList, "EnableDestinationFadeIn", patch.FadeInSeconds > 0);
            // UI「Play post-exit」＝ WObjects の PlaySourcePostExit（@PlayPostExit は無効）。
            UpsertBoolProperty(doc, propertyList, "PlaySourcePostExit", patch.PlayPostExit);

            var transitionInfo = EnsureChildElement(doc, transitionNode, "TransitionInfo", prepend: false);
            UpsertMusicFade(
                doc,
                transitionInfo,
                wrapperName: "SourceFadeOut",
                fadeName: "Source Fade-out",
                fadeType: MusicFadeTypeOut,
                fadeTimeSeconds: patch.FadeOutSeconds,
                // Source Fade-out は Offset も Time と同じ秒数にする。
                fadeOffsetSeconds: patch.FadeOutSeconds,
                fadeCurve: RegionEdgeFade.ToMusicFadeCurve(patch.FadeOutCurve),
                enabled: patch.FadeOutSeconds > 0);
            UpsertMusicFade(
                doc,
                transitionInfo,
                wrapperName: "DestinationFadeIn",
                fadeName: "Destination Fade-in",
                fadeType: null,
                fadeTimeSeconds: patch.FadeInSeconds,
                fadeOffsetSeconds: 0,
                fadeCurve: RegionEdgeFade.ToMusicFadeCurve(patch.FadeInCurve),
                enabled: patch.FadeInSeconds > 0);
        }

        doc.Save(wwuPath);
        log(UiStrings.LogMusicTransitionFadePatchFile(Path.GetFileName(wwuPath), patches.Count));
    }

    /// <summary>
    /// Playlist 向け Any→Object ルールを探す。
    /// WAAPI では名前が <c>Transition</c> のまま残ることがあるため、
    /// DestinationContextObject の ObjectRef 名を優先し、Name 属性は次点とする。
    /// </summary>
    private static System.Xml.XmlElement? FindMusicTransitionElement(
        System.Xml.XmlDocument doc,
        string playlistName)
    {
        var nodes = doc.SelectNodes("//MusicTransition");
        if (nodes is null)
        {
            return null;
        }

        System.Xml.XmlElement? byName = null;
        foreach (System.Xml.XmlNode node in nodes)
        {
            if (node is not System.Xml.XmlElement element
                || IsMusicTransitionFolder(element))
            {
                continue;
            }

            var destinationName = element.SelectSingleNode(
                    "ReferenceList/Reference[@Name='DestinationContextObject']/ObjectRef")
                as System.Xml.XmlElement;
            if (destinationName is not null
                && string.Equals(
                    destinationName.GetAttribute("Name"),
                    playlistName,
                    StringComparison.Ordinal))
            {
                return element;
            }

            if (byName is null
                && string.Equals(
                    element.GetAttribute("Name"),
                    playlistName,
                    StringComparison.Ordinal))
            {
                byName = element;
            }
        }

        return byName;
    }

    private static bool IsMusicTransitionFolder(System.Xml.XmlElement element)
    {
        var isFolder = element.SelectSingleNode("PropertyList/Property[@Name='IsFolder']")
            as System.Xml.XmlElement;
        return isFolder is not null
            && string.Equals(isFolder.GetAttribute("Value"), "True", StringComparison.OrdinalIgnoreCase);
    }

    private static void UpsertMusicFade(
        System.Xml.XmlDocument doc,
        System.Xml.XmlElement transitionInfo,
        string wrapperName,
        string fadeName,
        int? fadeType,
        double fadeTimeSeconds,
        double fadeOffsetSeconds,
        int fadeCurve,
        bool enabled)
    {
        var wrapper = transitionInfo.SelectSingleNode(wrapperName) as System.Xml.XmlElement;
        if (!enabled)
        {
            wrapper?.ParentNode?.RemoveChild(wrapper);
            return;
        }

        if (wrapper is null)
        {
            wrapper = doc.CreateElement(wrapperName);
            transitionInfo.AppendChild(wrapper);
        }

        var fade = wrapper.SelectSingleNode("MusicFade") as System.Xml.XmlElement;
        if (fade is null)
        {
            fade = doc.CreateElement("MusicFade");
            fade.SetAttribute("Name", fadeName);
            fade.SetAttribute("ID", $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}");
            wrapper.AppendChild(fade);
        }
        else
        {
            if (string.IsNullOrEmpty(fade.GetAttribute("Name")))
            {
                fade.SetAttribute("Name", fadeName);
            }

            if (string.IsNullOrEmpty(fade.GetAttribute("ID")))
            {
                fade.SetAttribute("ID", $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}");
            }
        }

        var propertyList = EnsureChildElement(doc, fade, "PropertyList", prepend: true);
        UpsertInt16Property(doc, propertyList, "FadeCurve", fadeCurve);
        UpsertReal64Property(doc, propertyList, "FadeTime", fadeTimeSeconds);
        UpsertReal64Property(doc, propertyList, "FadeOffset", fadeOffsetSeconds);
        if (fadeType is { } type)
        {
            UpsertInt16Property(doc, propertyList, "FadeType", type);
        }
    }

    private static System.Xml.XmlElement EnsureChildElement(
        System.Xml.XmlDocument doc,
        System.Xml.XmlElement parent,
        string name,
        bool prepend)
    {
        if (parent.SelectSingleNode(name) is System.Xml.XmlElement existing)
        {
            return existing;
        }

        var created = doc.CreateElement(name);
        if (prepend && parent.HasChildNodes)
        {
            parent.InsertBefore(created, parent.FirstChild);
        }
        else
        {
            parent.AppendChild(created);
        }

        return created;
    }

    private static void UpsertBoolProperty(
        System.Xml.XmlDocument doc,
        System.Xml.XmlElement propertyList,
        string name,
        bool value)
    {
        var text = value ? "True" : "False";
        if (propertyList.SelectSingleNode($"Property[@Name='{name}']")
            is System.Xml.XmlElement existing)
        {
            existing.SetAttribute("Type", "bool");
            existing.SetAttribute("Value", text);
            return;
        }

        var property = doc.CreateElement("Property");
        property.SetAttribute("Name", name);
        property.SetAttribute("Type", "bool");
        property.SetAttribute("Value", text);
        propertyList.AppendChild(property);
    }

    private static void UpsertInt16Property(
        System.Xml.XmlDocument doc,
        System.Xml.XmlElement propertyList,
        string name,
        int value)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (propertyList.SelectSingleNode($"Property[@Name='{name}']")
            is System.Xml.XmlElement existing)
        {
            existing.SetAttribute("Type", "int16");
            existing.SetAttribute("Value", text);
            return;
        }

        var property = doc.CreateElement("Property");
        property.SetAttribute("Name", name);
        property.SetAttribute("Type", "int16");
        property.SetAttribute("Value", text);
        propertyList.AppendChild(property);
    }

    private sealed class MusicClipWorkUnitPatch(string clipId)
    {
        public string ClipId { get; } = clipId;
        public double? PlayAtMs { get; set; }
        public double? FadeInDurationMs { get; set; }
        public double? FadeOutDurationMs { get; set; }
    }

    private static async Task VerifyClipReal64PropertyAsync(
        WaapiHttpClient client,
        string clipId,
        string returnField,
        double expected,
        Func<double, double?, string> errorFactory)
    {
        double? actual = null;
        var verifyDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            actual = await QueryClipReal64Async(client, clipId, returnField)
                .ConfigureAwait(false);
            if ((actual is not null && Math.Abs(actual.Value - expected) <= 0.01)
                || DateTime.UtcNow >= verifyDeadline)
            {
                break;
            }

            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
        }

        if (actual is null || Math.Abs(actual.Value - expected) > 0.01)
        {
            throw new InvalidOperationException(errorFactory(expected, actual));
        }
    }

    /// <summary>
    /// プロジェクトが完全に閉じるまで待つ。
    /// クローズ進行中は ak.wwise.locked、完了後は「プロジェクト未ロード」系エラーか空結果になる。
    /// </summary>
    private static async Task WaitForProjectClosedAsync(WaapiHttpClient client)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var result = await client.CallAsync(
                        "ak.wwise.core.object.get",
                        new Dictionary<string, object?> { ["waql"] = "$ from type Project" },
                        new Dictionary<string, object?> { ["return"] = new[] { "id" } },
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (!result.TryGetProperty("return", out var arr)
                    || arr.ValueKind != JsonValueKind.Array
                    || arr.GetArrayLength() == 0)
                {
                    return;
                }
            }
            catch (WaapiException ex) when (
                ex.Message.Contains("ak.wwise.locked", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("exclusive lock", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("in progress", StringComparison.OrdinalIgnoreCase))
            {
                // クローズ進行中。待って再確認する。
            }
            catch (WaapiException)
            {
                // 「プロジェクトが読み込まれていない」等 → クローズ完了とみなす。
                return;
            }

            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 再オープンしたプロジェクトのロード完了（クエリで .wproj パスが返る状態）まで待つ。
    /// タイムアウト時はそのまま返し、後段の検証で失敗として検出する。
    /// </summary>
    private static async Task WaitForProjectLoadedAsync(WaapiHttpClient client, string projectPath)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var result = await client.CallAsync(
                        "ak.wwise.core.object.get",
                        new Dictionary<string, object?> { ["waql"] = "$ from type Project" },
                        new Dictionary<string, object?> { ["return"] = new[] { "filePath" } },
                        CancellationToken.None)
                    .ConfigureAwait(false);
                if (result.TryGetProperty("return", out var arr)
                    && arr.ValueKind == JsonValueKind.Array
                    && arr.GetArrayLength() > 0
                    && arr[0].TryGetProperty("filePath", out var pathEl)
                    && string.Equals(
                        pathEl.GetString(),
                        projectPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch (WaapiException)
            {
                // ロック中／ロード中。待って再確認する。
            }

            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Wwise が排他ロック中（ak.wwise.locked、プロジェクトのクローズ／ロード進行中）の間、
    /// 解除されるまで呼び出しをリトライする。
    /// </summary>
    private static async Task<JsonElement> CallWithLockRetryAsync(
        WaapiHttpClient client,
        string uri,
        object? args = null,
        object? options = null)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (true)
        {
            try
            {
                return await client.CallAsync(uri, args, options, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (WaapiException ex) when (
                DateTime.UtcNow < deadline
                && (ex.Message.Contains("ak.wwise.locked", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("exclusive lock", StringComparison.OrdinalIgnoreCase)))
            {
                await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>再オープン後の MusicClip から Real64 プロパティを読み戻す。</summary>
    private static async Task<double?> QueryClipReal64Async(
        WaapiHttpClient client,
        string clipId,
        string returnField)
    {
        var result = await CallWithLockRetryAsync(
                client,
                "ak.wwise.core.object.get",
                new Dictionary<string, object?> { ["waql"] = $"$ \"{clipId}\"" },
                new Dictionary<string, object?> { ["return"] = new[] { "id", returnField } })
            .ConfigureAwait(false);
        if (!result.TryGetProperty("return", out var arr)
            || arr.ValueKind != JsonValueKind.Array
            || arr.GetArrayLength() == 0)
        {
            return null;
        }

        return arr[0].TryGetProperty(returnField, out var el)
               && el.ValueKind == JsonValueKind.Number
            ? el.GetDouble()
            : null;
    }

    /// <summary>WWU（XML）内の MusicClip に Real64 プロパティを直接書き込む。</summary>
    private static void PatchMusicClipPropertiesInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<MusicClipWorkUnitPatch> patches,
        Action<string> log)
    {
        WaitForExclusiveFileAccess(wwuPath);

        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var clipNode = doc.SelectSingleNode(
                $"//MusicClip[@ID='{patch.ClipId}']") as System.Xml.XmlElement;
            if (clipNode is null)
            {
                throw new InvalidOperationException(
                    UiStrings.ErrPlayAtClipXmlMissing(patch.ClipId, wwuPath));
            }

            var propertyList = clipNode.SelectSingleNode("PropertyList") as System.Xml.XmlElement;
            if (propertyList is null)
            {
                propertyList = doc.CreateElement("PropertyList");
                clipNode.PrependChild(propertyList);
            }

            if (patch.PlayAtMs is { } playAtMs)
            {
                UpsertReal64Property(doc, propertyList, "PlayAt", playAtMs);
            }

            if (patch.FadeInDurationMs is { } fadeInMs)
            {
                UpsertReal64Property(doc, propertyList, "FadeInDuration", fadeInMs);
            }

            if (patch.FadeOutDurationMs is { } fadeOutMs)
            {
                UpsertReal64Property(doc, propertyList, "FadeOutDuration", fadeOutMs);
            }
        }

        doc.Save(wwuPath);
        log(UiStrings.LogPlayAtPatchFile(Path.GetFileName(wwuPath), patches.Count));
    }

    private static void UpsertReal64Property(
        System.Xml.XmlDocument doc,
        System.Xml.XmlElement propertyList,
        string name,
        double value)
    {
        var text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (propertyList.SelectSingleNode($"Property[@Name='{name}']")
            is System.Xml.XmlElement existing)
        {
            existing.SetAttribute("Value", text);
            return;
        }

        var property = doc.CreateElement("Property");
        property.SetAttribute("Name", name);
        property.SetAttribute("Type", "Real64");
        property.SetAttribute("Value", text);
        propertyList.AppendChild(property);
    }

    /// <summary>指定ファイルを排他モードで開けるまで待つ（最大 30 秒）。</summary>
    private static void WaitForExclusiveFileAccess(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(250);
            }
        }
    }

    /// <summary>WAQL で 1 件だけ取得し、指定の return フィールド（文字列）を返す。</summary>
    private static async Task<string?> QuerySingleReturnStringAsync(
        WaapiHttpClient client,
        string waql,
        string field,
        CancellationToken cancellationToken)
    {
        var result = await client.CallAsync(
                "ak.wwise.core.object.get",
                new Dictionary<string, object?> { ["waql"] = waql },
                new Dictionary<string, object?> { ["return"] = new[] { "id", field } },
                cancellationToken)
            .ConfigureAwait(false);
        if (!result.TryGetProperty("return", out var arr)
            || arr.ValueKind != JsonValueKind.Array
            || arr.GetArrayLength() == 0)
        {
            return null;
        }

        return arr[0].TryGetProperty(field, out var el) ? el.GetString() : null;
    }

    private static async Task SetClipPropertyAsync(
        WaapiHttpClient client,
        string clipId,
        string property,
        object value,
        CancellationToken cancellationToken)
    {
        await client.CallAsync(
                "ak.wwise.core.object.setProperty",
                new Dictionary<string, object?>
                {
                    ["object"] = clipId,
                    ["property"] = property,
                    ["value"] = value,
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<List<(string Id, string Path)>> QueryAllMusicClipsAsync(
        WaapiHttpClient client,
        CancellationToken cancellationToken)
    {
        var list = new List<(string Id, string Path)>();
        var result = await client.CallAsync(
                "ak.wwise.core.object.get",
                new Dictionary<string, object?>
                {
                    ["waql"] = "$ from type MusicClip",
                },
                new Dictionary<string, object?>
                {
                    ["return"] = new[] { "id", "name", "type", "path" },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.TryGetProperty("return", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idEl))
            {
                continue;
            }

            var id = idEl.GetString();
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var path = item.TryGetProperty("path", out var pathEl)
                ? pathEl.GetString() ?? string.Empty
                : string.Empty;
            list.Add((id, path));
        }

        return list;
    }

    private static List<string> FindMusicClipsForTrack(
        IReadOnlyList<(string Id, string Path)> allClips,
        string trackPath,
        string wavStem)
    {
        var matches = new List<string>();
        var trackFull = trackPath.TrimEnd('\\');
        var prefix = trackFull + "\\";

        // Track パス直下だけを対象にする。
        // 緩い Contains は bgm_st_0040_a が bgm_st_0040_a_a / _a_b にも
        // マッチして全クリップへトリムが上書きされるため使わない。
        foreach (var (id, path) in allClips)
        {
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, trackFull, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(id);
            }
        }

        if (matches.Count > 0 || string.IsNullOrEmpty(wavStem))
        {
            return matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // フォールバック: Track\\{wavStem} と一致するパスのみ。
        var exactClipPath = prefix + wavStem;
        foreach (var (id, path) in allClips)
        {
            if (string.Equals(path, exactClipPath, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(id);
            }
        }

        return matches.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsReservedCueName(string name) =>
        string.Equals(name, "Entry Cue", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Exit Cue", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Entry", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Exit", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 各 Music Segment の Cue 一覧を Entry / Exit / Custom だけに差し替える。
    /// listMode=replaceAll により既定 Cue との二重化を防ぐ。
    /// </summary>
    private static async Task ReplaceAllSegmentCuesAsync(
        WaapiHttpClient client,
        WwiseMusicPlan plan,
        string musicRootPath,
        CancellationToken cancellationToken)
    {
        foreach (var playlist in plan.Playlists)
        {
            var playlistPath = plan.IsMultiPart
                ? $"{musicRootPath}\\{playlist.Name}"
                : musicRootPath;
            foreach (var segment in playlist.Segments)
            {
                var segmentPath = $"{playlistPath}\\{segment.Name}";
                var origin = segment.ClipStartMs;
                var entryLocal = Math.Max(0.0, segment.EntryCueMs - origin);
                var exitLocal = Math.Max(0.0, segment.ExitCueMs - origin);
                var cues = BuildSegmentCueList(segment, origin, entryLocal, exitLocal);
                await client.CallAsync(
                        "ak.wwise.core.object.set",
                        new Dictionary<string, object?>
                        {
                            ["objects"] = new object[]
                            {
                                new Dictionary<string, object?>
                                {
                                    ["object"] = segmentPath,
                                    ["@Cues"] = cues,
                                },
                            },
                            ["onNameConflict"] = "merge",
                            ["listMode"] = "replaceAll",
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static List<object> BuildSegmentCueList(
        WwiseSegmentPlan segment,
        double origin,
        double entryLocal,
        double exitLocal)
    {
        var cues = new List<object>
        {
            new Dictionary<string, object?>
            {
                ["type"] = "MusicCue",
                ["name"] = string.Empty,
                ["@CueType"] = 0,
                ["@TimeMs"] = entryLocal,
            },
            new Dictionary<string, object?>
            {
                ["type"] = "MusicCue",
                ["name"] = string.Empty,
                ["@CueType"] = 1,
                ["@TimeMs"] = exitLocal,
            },
        };

        foreach (var custom in segment.CustomCues)
        {
            if (IsReservedCueName(custom.Name))
            {
                continue;
            }

            cues.Add(new Dictionary<string, object?>
            {
                ["type"] = "MusicCue",
                ["name"] = custom.Name,
                ["@CueType"] = 2,
                ["@TimeMs"] = Math.Max(0.0, custom.TimeMs - origin),
            });
        }

        return cues;
    }
}
