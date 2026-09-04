using System.Text;
using System.Text.Json;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Wwise;

internal static partial class WaapiMusicImporter
{
    private static async Task CallObjectSetAsync(
        WaapiHttpClient client,
        Dictionary<string, object?> setArgs,
        Dictionary<string, object> returnOptions,
        CancellationToken cancellationToken)
    {
        _ = await client.CallAsync(
                WaapiUris.CoreObjectSet,
                setArgs,
                returnOptions,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Any → Playlist トランジションの Destination 参照を setReference で結ぶ。
    /// DestinationContextObject は Reference のため、ネスト作成だけでは空になり得る
    /// （作成時の WaapiPropertyNames.DestinationContextObject で足りる場合もある）。
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
                        WaapiUris.CoreObjectSetProperty,
                        new Dictionary<string, object?>
                        {
                            ["object"] = transitionId,
                            ["property"] = "DestinationContextType",
                            ["value"] = 2,
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                await client.CallAsync(
                        WaapiUris.CoreObjectSetReference,
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
                WaapiUris.CoreObjectGet,
                new Dictionary<string, object?>
                {
                    ["waql"] = $"$ \"{escaped}\" select descendants where type = \"MusicTransition\"",
                },
                new Dictionary<string, object?>
                {
                    ["return"] = MusicTransitionReturnFields,
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
            if (item.TryGetProperty(WaapiPropertyNames.DestinationContextType, out var typeEl)
                && typeEl.ValueKind == JsonValueKind.Number)
            {
                destinationType = typeEl.GetInt32();
            }

            string? destinationId = null;
            string? destinationName = null;
            string? destinationPath = null;
            if (item.TryGetProperty(WaapiPropertyNames.DestinationContextObject, out var destEl))
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

            if (item.TryGetProperty(WaapiPropertyNames.DestinationContextObjectId, out var flatId)
                && flatId.ValueKind == JsonValueKind.String)
            {
                destinationId ??= flatId.GetString();
            }

            if (item.TryGetProperty(WaapiPropertyNames.DestinationContextObjectName, out var flatName)
                && flatName.ValueKind == JsonValueKind.String)
            {
                destinationName ??= flatName.GetString();
            }

            if (item.TryGetProperty(WaapiPropertyNames.DestinationContextObjectPath, out var flatPath)
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
                    WaapiUris.CoreObjectGet,
                    new Dictionary<string, object?>
                    {
                        ["waql"] = $"$ \"{escaped}\"",
                    },
                    new Dictionary<string, object?>
                    {
                        ["return"] = ReturnFieldsIdPath,
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
    /// パートがソース全長なら元 WAV をコピーして共有する。
    /// パートがソースの一部分（XML の曲ごとなど）なら、その範囲だけ切り出して共有する。
    /// セグメント区間は MusicClip トリムで合わせる。
    /// </summary>
    internal static Dictionary<string, TrackMediaBinding> SliceSegmentWavs(
        WwiseMusicPlan plan,
        string sourceWavPath,
        string outputDirectory,
        IReadOnlyList<WaveformOutputPart> outputParts,
        uint sampleRate,
        ushort blockAlign,
        WavFileInfo wavInfo,
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
        var reusedDestBySource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slicedPartDest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

                    var partLocalStart = part.ResolveLocalStart();
                    var partLocalEnd = part.ResolveLocalEnd();
                    var partCoversSource = partLocalStart == 0
                        && partLocalEnd == sliceInfo.FrameCount;
                    var effectiveRate = sliceInfo.SampleRate != 0
                        ? sliceInfo.SampleRate
                        : sampleRate;

                    // Wave 単体／複数波形: パート＝ソース全長なら元 WAV をコピーして共有。
                    // セグメント範囲は MusicClip トリム（イントロ／ループで切らない）。
                    if (partCoversSource
                        && CanReuseSourceWav(sliceSourcePath, localStart, localEnd, sliceInfo))
                    {
                        var dest = CopySourceWavOnce(
                            sliceSourcePath,
                            outputDirectory,
                            part,
                            track.Name,
                            usedFileNames,
                            reusedDestBySource);
                        var needsTrim = localStart != 0 || localEnd != sliceInfo.FrameCount;
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
                        continue;
                    }

                    // XML 複数曲など: パート（曲）範囲だけ切り出し、曲内セグメントはトリム。
                    if (CanSlicePartRange(
                            sliceSourcePath,
                            partLocalStart,
                            partLocalEnd,
                            localStart,
                            localEnd,
                            sliceInfo))
                    {
                        var partKey = part.Number + "\u001f" + Path.GetFullPath(sliceSourcePath);
                        if (!slicedPartDest.TryGetValue(partKey, out var destPart))
                        {
                            var desiredPartName = string.IsNullOrWhiteSpace(part.FileName)
                                ? $"{track.Name}.wav"
                                : part.FileName;
                            if (!desiredPartName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                            {
                                desiredPartName += ".wav";
                            }

                            var partFileName = UniqueSliceFileName(desiredPartName, usedFileNames);
                            destPart = Path.GetFullPath(Path.Combine(outputDirectory, partFileName));
                            WriteSegmentSafely(
                                sliceSourcePath,
                                destPart,
                                partLocalStart,
                                partLocalEnd,
                                sliceBlockAlign);
                            slicedPartDest[partKey] = destPart;
                            log(UiStrings.LogWavSliceWritten(partFileName));
                        }

                        var relativeStart = localStart - partLocalStart;
                        var relativeEnd = localEnd - partLocalStart;
                        var partFrames = partLocalEnd - partLocalStart;
                        var needsPartTrim = relativeStart != 0 || relativeEnd != partFrames;
                        map[trackKey] = new TrackMediaBinding(
                            destPart,
                            relativeStart,
                            relativeEnd,
                            partFrames,
                            effectiveRate,
                            ApplyClipTrim: needsPartTrim,
                            ReusedOriginal: false);
                        log(
                            UiStrings.LogTrackMediaBinding(
                                segment.Name,
                                track.Name,
                                Path.GetFileName(destPart),
                                relativeStart,
                                relativeEnd,
                                reusedOriginal: false,
                                applyClipTrim: needsPartTrim));
                        continue;
                    }

                    // ソース範囲がファイル／パートに収まらないときだけセグメント単位で切り出す。
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
                        sliceBlockAlign);
                    var writtenInfo = WavFileInfo.Read(destSlice);
                    map[trackKey] = new TrackMediaBinding(
                        Path.GetFullPath(destSlice),
                        0,
                        writtenInfo.FrameCount,
                        writtenInfo.FrameCount,
                        writtenInfo.SampleRate != 0 ? writtenInfo.SampleRate : sampleRate,
                        ApplyClipTrim: false,
                        ReusedOriginal: false);
                    log(UiStrings.LogWavSliceWritten(fileName));
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
    /// 元 WAV を共有できるか。部分範囲は MusicClip トリムで合わせる。
    /// </summary>
    private static bool CanReuseSourceWav(
        string sliceSourcePath,
        long localStart,
        long localEnd,
        WavFileInfo sliceInfo)
    {
        if (sliceInfo.FrameCount <= 0 || !File.Exists(sliceSourcePath))
        {
            return false;
        }

        return localStart >= 0
            && localEnd <= sliceInfo.FrameCount
            && localEnd > localStart;
    }

    /// <summary>
    /// パート範囲を曲ファイルとして切り出せるか。セグメントはその中に収まっていること。
    /// </summary>
    private static bool CanSlicePartRange(
        string sliceSourcePath,
        long partLocalStart,
        long partLocalEnd,
        long localStart,
        long localEnd,
        WavFileInfo sliceInfo)
    {
        if (sliceInfo.FrameCount <= 0 || !File.Exists(sliceSourcePath))
        {
            return false;
        }

        if (partLocalStart < 0
            || partLocalEnd > sliceInfo.FrameCount
            || partLocalEnd <= partLocalStart)
        {
            return false;
        }

        return localStart >= partLocalStart
            && localEnd <= partLocalEnd
            && localEnd > localStart;
    }

    private static string CopySourceWavOnce(
        string sliceSourcePath,
        string outputDirectory,
        WaveformOutputPart part,
        string trackName,
        HashSet<string> usedFileNames,
        Dictionary<string, string> reusedDestBySource)
    {
        var sourceFull = Path.GetFullPath(sliceSourcePath);
        if (reusedDestBySource.TryGetValue(sourceFull, out var dest))
        {
            return dest;
        }

        var desiredFileName = Path.GetFileName(sliceSourcePath);
        if (string.IsNullOrWhiteSpace(desiredFileName))
        {
            desiredFileName = string.IsNullOrWhiteSpace(part.FileName)
                ? $"{trackName}.wav"
                : part.FileName;
        }

        if (!desiredFileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            desiredFileName += ".wav";
        }

        var destName = UniqueSliceFileName(desiredFileName, usedFileNames);
        dest = Path.GetFullPath(Path.Combine(outputDirectory, destName));
        if (!string.Equals(sourceFull, dest, StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(dest)
                || new FileInfo(dest).Length != new FileInfo(sourceFull).Length
                || File.GetLastWriteTimeUtc(dest) != File.GetLastWriteTimeUtc(sourceFull))
            {
                File.Copy(sourceFull, dest, overwrite: true);
            }
        }

        reusedDestBySource[sourceFull] = dest;
        return dest;
    }

    internal readonly record struct TrackMediaBinding(
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
        ushort blockAlign)
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
                blockAlign);
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
                blockAlign);
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
                ["name"] = p.StateName,
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
                    $"$ \"{stateGroupPath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
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
                    $"$ \"{stateGroupPath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
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
                            WaapiUris.CoreObjectSetStateGroups,
                            new Dictionary<string, object?>
                            {
                                ["object"] = trackPath,
                                ["stateGroups"] = new object[] { stateGroupPath },
                            },
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    // Volume を State 連動プロパティとして有効化する。
                    await client.CallAsync(
                            WaapiUris.CoreObjectSetStateProperties,
                            new Dictionary<string, object?>
                            {
                                ["object"] = trackPath,
                                ["stateProperties"] = StatePropertiesVolume,
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
                        GroupStateMuteVolumeDb,
                        groupState.AdditiveLayers));
                    volumePatches.Add(new MusicTrackStateVolumePatch(
                        trackWwuPath,
                        trackId,
                        track.Name,
                        groupState.Name,
                        stateGroupId,
                        stateIds,
                        groupState.StateNames,
                        activeState,
                        groupState.AdditiveLayers,
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
                    plan.Playlists[0].StateName,
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
            WaapiUris.UiCommandsExecute,
            new Dictionary<string, object?>
            {
                ["command"] = WaapiSelection.FindInProjectExplorerCommand,
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
                            [WaapiPropertyNames.DefaultTransitionTime] = defaultTransitionSeconds,
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
                WaapiUris.CoreObjectGet,
                new Dictionary<string, object?>
                {
                    ["waql"] = $"$ \"{escaped}\" select children where type = \"State\"",
                },
                new Dictionary<string, object?>
                {
                    ["return"] = ReturnFieldsIdNameType,
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
    /// Music Track の State Volume（排他: 対応 State=0dB・他=-108dB／
    /// Additive: 下位レイヤー以降=0dB）と
    /// Change Occurs At（StateGroupInfo/@MusicSyncType）を WWU へ書くためのパッチ。
    /// </summary>
    private readonly record struct MusicTrackStateVolumePatch(
        string WwuPath,
        string TrackId,
        string TrackName,
        string StateGroupName,
        string StateGroupId,
        IReadOnlyDictionary<string, string> StateIdsByName,
        IReadOnlyList<string> OrderedStateNames,
        string LayerStateName,
        bool AdditiveLayers,
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
                            [WaapiPropertyNames.Arguments] = new[] { stateGroupPath },
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
                [WaapiPropertyNames.EntryPath] = new[] { $"{stateGroupPath}\\{p.StateName}" },
                [WaapiPropertyNames.AudioNode] = $"{containerPath}\\{p.Name}",
            })
            .ToList();

        return new Dictionary<string, object?>
        {
            ["objects"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["object"] = containerPath,
                    [WaapiPropertyNames.Entries] = entries,
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
                    [WaapiPropertyNames.TransitionRoot] = WaapiMusicTransitionDefaults.BuildTransitionRoot(
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
        bool applyMakeUpGain,
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
                            applyMakeUpGain,
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
        bool applyMakeUpGain,
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
                            applyMakeUpGain,
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
        bool applyMakeUpGain,
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
                prefetchLengthMs,
                applyMakeUpGain,
                partGains,
                log));
            itemDefs.Add(new Dictionary<string, object?>
            {
                ["type"] = "MusicPlaylistItem",
                ["name"] = string.Empty,
                [WaapiPropertyNames.PlaylistItemType] = 1,
                [WaapiPropertyNames.LoopCount] = segment.LoopInfinite ? 0 : 1,
                [WaapiPropertyNames.Segment] = $"{playlistPath}\\{segment.Name}",
            });
        }

        var name = isMultiPart ? playlist.Name : plan.ContainerName;
        return new Dictionary<string, object?>
        {
            ["type"] = "MusicPlaylistContainer",
            ["name"] = name,
            ["children"] = segmentDefs,
            [WaapiPropertyNames.PlaylistRoot] = new Dictionary<string, object?>
            {
                ["type"] = "MusicPlaylistItem",
                ["name"] = string.Empty,
                [WaapiPropertyNames.PlaylistItemType] = 0,
                [WaapiPropertyNames.PlayMode] = 0,
                [WaapiPropertyNames.LoopCount] = 1,
                ["children"] = itemDefs,
            },
        };
    }

    private static Dictionary<string, object?> BuildSegmentDef(
        WwiseSegmentPlan segment,
        IReadOnlyDictionary<string, TrackMediaBinding> trackMedia,
        bool isFirstSegment,
        bool streamEnabled,
        int lookAheadMs,
        int prefetchLengthMs,
        bool applyMakeUpGain,
        IReadOnlyDictionary<int, float>? partGains,
        Action<string> log)
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
            // Zero latency ＋ Prefetch を付け、Look-ahead は 50ms（減衰追従用）。
            // 2 番目以降は Look-ahead のみ（UI 設定値）。
            var zeroLatency = streamEnabled && isFirstSegment;
            var trackProps = new Dictionary<string, object?>
            {
                ["type"] = "MusicTrack",
                ["name"] = track.Name,
                [WaapiPropertyNames.IsStreamingEnabled] = streamEnabled,
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
                trackProps[WaapiPropertyNames.IsZeroLatency] = zeroLatency;
                trackProps[WaapiPropertyNames.LookAheadTime] = zeroLatency ? FirstSegmentLookAheadMs : lookAheadMs;
                if (zeroLatency)
                {
                    trackProps[WaapiPropertyNames.PreFetchLength] = prefetchLengthMs;
                }
            }

            trackDefs.Add(trackProps);
        }

        // Entry/Exit/Custom Cue は作成後に listMode=replaceAll で一括設定する
        // （ここへ WaapiPropertyNames.Cues を載せる・既定 Cue と二重になる）。
        var def = new Dictionary<string, object?>
        {
            ["type"] = "MusicSegment",
            ["name"] = segment.Name,
            [WaapiPropertyNames.OverrideClockSettings] = true,
            [WaapiPropertyNames.Tempo] = segment.TempoBpm,
            [WaapiPropertyNames.TimeSignatureUpper] = segment.TimeSignatureUpper,
            [WaapiPropertyNames.TimeSignatureLower] = segment.TimeSignatureLower,
            [WaapiPropertyNames.EndPosition] = endLocal,
            ["children"] = trackDefs,
        };

        if (applyMakeUpGain && partGains is not null)
        {
            ApplySegmentMakeUpGains(trackDefs, segment, partGains, log);
        }

        return def;
    }

    /// <summary>
    /// グループ相対ゲインを各 Music Track の Make-Up Gain へ載せる。
    /// </summary>
}
