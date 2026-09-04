using System.Text;
using System.Text.Json;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Wwise;

internal static partial class WaapiMusicImporter
{
    private static void ApplySegmentMakeUpGains(
        List<object> trackDefs,
        WwiseSegmentPlan segment,
        IReadOnlyDictionary<int, float> partGains,
        Action<string> log)
    {
        for (var i = 0; i < segment.Tracks.Count; i++)
        {
            var partNumber = segment.Tracks[i].SourcePartNumber;
            if (!partGains.TryGetValue(partNumber, out var linearGain))
            {
                continue;
            }

            if (trackDefs[i] is not Dictionary<string, object?> trackProps)
            {
                continue;
            }

            var gainDb = LoudnessMeter.LinearGainToDb(linearGain);
            trackProps[WaapiPropertyNames.MakeUpGain] = gainDb;
            log(UiStrings.LogLoudnessMakeUpGainApplied(
                $"{segment.Name}/{segment.Tracks[i].Name}",
                gainDb));
        }
    }

    /// <summary>
    /// すべての MusicClip に Begin/End Trim Offset（ミリ秒）を明示設定する。
    /// トリム不要（メディア全長）のクリップも、Wwise 側のメディア長推定が
    /// 不正確な場合に短く切れることがあるため、実測のメディア長で必ず上書きする。
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
        if (segmentMedia.Count == 0)
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
                    if (!segmentMedia.TryGetValue(key, out var media))
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
    /// Music Playlist Container 自身の既定トランジションルール（Any to Any）へ載せる
    /// Play post-exit（UI: Play -E）。WAAPI 非対応のため WWU 直編集で書く。
    /// </summary>
    private readonly record struct PlaylistPostExitPatch(
        string PlaylistContainerName,
        bool PlayPostExit);

    /// <summary>
    /// 負の PlayAt・WAAPI 上限超の Clip Fade Duration・Playlist 遷移 MusicFade・
    /// Playlist Container 既定ルールの Play post-exit・
    /// Group State の旧 TransitionList クリア／Track State Volume を WWU 直接編集で設定する。
    /// 手順: project.save → 対象 WWU 特定 → project.close → XML パッチ → project.open。
    /// </summary>
}
