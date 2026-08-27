using System.Text;
using System.Text.Json;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// <see cref="WwiseMusicPlan"/> を WAAPI（WaapiUris.CoreObjectSet）で Wwise へ流し込む。
/// <para>
/// 1. 各 Music Segment 用の WAV を用意する。
///    出力パートがソース全長なら元ファイルをコピーして共有し、区間は MusicClip トリム。
///    XML の複数曲マスターなど、パートがソースの一部分なら曲（パート）ごとに切り出す。
///    ゲインは焼き込まない。
/// 2. 複数パート時は State Group／State を作成または更新し、Music Switch Container に割当。
/// 3. object.set で Playlist／Segment／Track（＋WAV）と Cue を作成。
///    Layer Music Option（Keep Layer Balance）オン時は、グループ内の相対バランスを
///    Music Track の Make-Up Gain へ載せる。
///    Wwise の Loudness Normalization チェックは付けない。
/// 4. グループ化 Playlist はグループ名の State Group（State A/B/C…）を作り、
///    Group Fade が全員同一なら Default Transition Time のみ、異なれば Custom Transition Time
///    （遷移先 State ごと。このとき Default は Wwise 既定 1 秒のまま）、
///    各 Music Track へ割当し、対応 State のみ Volume 0dB・他は -108dB を設定する
///    （Additive Layers 時は累積再生: 下位レイヤー以降を 0dB）。
///    完了後に現在 State を先頭へ設定し、作成した Switch／Playlist を選択する（プレビュー用）。
/// 5. 必要なら MusicClip トリムとリージョン端フェード（非破壊）を設定する。
///    Fade Duration が WAAPI 上限（3.6 秒）を超える場合は WWU 直接編集で本値を書く。
/// 6. Playlist 遷移の MusicFade（Time）・Playlist Container 既定ルール（Any to Any）の
///    Play post-exit・Group State の TransitionList／Track State Volume は
///    WAAPI 非対応のため、同系統の WWU 直編集で書く。
/// </para>
/// </summary>
internal static partial class WaapiMusicImporter
{
    /// <summary>WAAPI が受け付ける MusicClip Fade Duration の上限（ミリ秒＝3.6 秒）。</summary>
    private const double WaapiMusicClipFadeMaxMs = 3600;

    /// <summary>
    /// Playlist 先頭セグメント（Zero latency）の Look-ahead Time（ms）。
    /// 極端な音量低下時に減衰が追いつかないケースを避けるため 0 ではなく 50 を使う。
    /// </summary>
    private const int FirstSegmentLookAheadMs = 50;

    /// <summary>MusicClip FadeInMode / FadeOutMode: Manual。</summary>
    private const int MusicClipFadeModeManual = 1;

    /// <summary>MusicFade.FadeType: Fade-out。</summary>
    private const int MusicFadeTypeOut = 1;

    private static readonly string[] ReturnFieldsIdNameTypePath =
        ["id", "name", "type", "path"];

    private static readonly string[] MusicTransitionReturnFields =
    [
        "id",
        "name",
        WaapiPropertyNames.DestinationContextType,
        WaapiPropertyNames.DestinationContextObject,
        WaapiPropertyNames.DestinationContextObjectName,
        WaapiPropertyNames.DestinationContextObjectPath,
        WaapiPropertyNames.DestinationContextObjectId,
    ];

    private static readonly string[] ReturnFieldsIdPath = ["id", "path"];

    private static readonly string[] StatePropertiesVolume = ["Volume"];

    private static readonly string[] ReturnFieldsIdNameType = ["id", "name", "type"];

    private static readonly string[] ReturnFieldsId = ["id"];

    private static readonly string[] ReturnFieldsFilePath = ["filePath"];

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
        bool loudnessPreserveGroupBalance = false,
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
        if (loudnessPreserveGroupBalance)
        {
            Log(UiStrings.LogLoudnessGroupBalanceOn);
            partGains = LoudnessMeter.ComputeGroupBalanceGains(
                sourceWavPath,
                wavInfo,
                outputParts,
                partGroupIds,
                Log);
        }

        var applyMakeUpGain = loudnessPreserveGroupBalance
            && partGains is not null
            && partGains.Count > 0;

        // 中間パート WAV は作らず、元 WAV を共有して MusicClip トリムで範囲を合わせる。
        // リージョン端フェードは WAV へ焼き込まず、後段で MusicClip 非破壊フェードとして設定する。
        // Layer Music Option は Make-Up Gain へ載せ、WAV ゲインは変えない。
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
            Log);

        // タイムアウトは import を含むので長めに取る
        var timeout = TimeSpan.FromMilliseconds(Math.Max(waapiSettings.TimeoutMs, 30000));
        using var client = new WaapiHttpClient(waapiSettings.Url, timeout);
        var returnOptions = new Dictionary<string, object>
        {
            ["return"] = ReturnFieldsIdNameTypePath,
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
                            applyMakeUpGain,
                            partGains,
                            Log),
                        returnOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            // Playlist 未作成時に WaapiPropertyNames.AudioNode / Destination を張ると空参照になるため、子作成後に結ぶ。
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
                        applyMakeUpGain,
                        partGains,
                        Log),
                    returnOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Log(UiStrings.LogWwiseObjectsCreated);

        // MusicSegment は作成時に既定の Entry/Exit を持つ。
        // 作成と同時の WaapiPropertyNames.Cues 追加は二重化するため、作成後に replaceAll で差し替える。
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
        // Playlist Container 自身の Play post-exit、
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
        // Play -E は各 Music Playlist Container 内の既定ルール（Any to Any）にも反映する。
        var playlistPostExitPatches = plan.Playlists
            .Select(p => new PlaylistPostExitPatch(
                plan.IsMultiPart ? p.Name : plan.ContainerName,
                p.PlayPostExit))
            .ToList();
        await ApplyWorkUnitPatchesAsync(
                client,
                musicRootPath,
                playAtFixes,
                fadeDurationFixes,
                transitionFadePatches,
                playlistPostExitPatches,
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
                        UiStrings.LabelExitSource(playlist.ExitSourceAt),
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
                            ? media.ReusedOriginal ? "  [copy+trim]" : "  [slice+trim]"
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
                if (groupState.AdditiveLayers)
                {
                    sb.AppendLine("  Additive Layers: ON (cumulative State Volume)");
                }
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

}
