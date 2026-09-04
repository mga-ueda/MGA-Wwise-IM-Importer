using System.Text;
using System.Text.Json;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Wwise;

internal static partial class WaapiMusicImporter
{
    private static async Task ApplyWorkUnitPatchesAsync(
        WaapiHttpClient client,
        string musicRootPath,
        IReadOnlyList<MusicClipPlayAtFix> playAtFixes,
        IReadOnlyList<MusicClipFadeDurationFix> fadeFixes,
        IReadOnlyList<MusicTransitionFadePatch> transitionFades,
        IReadOnlyList<PlaylistPostExitPatch> playlistPostExits,
        IReadOnlyList<StateGroupTransitionPatch> groupStateTransitions,
        IReadOnlyList<MusicTrackStateVolumePatch> groupStateVolumes,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        if (playAtFixes.Count == 0
            && fadeFixes.Count == 0
            && transitionFades.Count == 0
            && playlistPostExits.Count == 0
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
            playlistPostExits.Count,
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
        // コンテナ自体の WWU を開き、Destination 参照等でルールを特定する。
        // Playlist Container の Play post-exit も同じ WWU（musicRootPath の所属先）に載る。
        string? transitionWwuPath = null;
        if (transitionFades.Count > 0 || playlistPostExits.Count > 0)
        {
            transitionWwuPath = await QuerySingleReturnStringAsync(
                    client,
                    $"$ \"{musicRootPath.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
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
                WaapiUris.CoreProjectSave,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await client.CallAsync(
                    WaapiUris.UiProjectClose,
                    new Dictionary<string, object?> { ["bypassSave"] = true },
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsTransientHttpError(ex))
        {
            // クローズ開始と同時に HTTP 接続が切れて応答を受け取れないことがある。
            // 実際にクローズされたかは直後の WaitForProjectClosedAsync で確認する。
        }

        try
        {
            await WaitForProjectClosedAsync(client).ConfigureAwait(false);

            foreach (var group in patchList.GroupBy(p => clipFiles[p.ClipId], StringComparer.OrdinalIgnoreCase))
            {
                PatchMusicClipPropertiesInWorkUnitFile(group.Key, group.ToList(), log);
            }

            if (transitionWwuPath is not null && transitionFades.Count > 0)
            {
                PatchMusicTransitionFadesInWorkUnitFile(transitionWwuPath, transitionFades, log);
                // MusicFade / Enable は TransitionInfo 配下で WAAPI 照会が不安定なため、
                // 再オープン前に WWU 上で検証する。
                VerifyMusicTransitionFadesInWorkUnitFile(transitionWwuPath, transitionFades);
            }

            if (transitionWwuPath is not null && playlistPostExits.Count > 0)
            {
                PatchPlaylistPostExitInWorkUnitFile(transitionWwuPath, playlistPostExits, log);
                VerifyPlaylistPostExitInWorkUnitFile(transitionWwuPath, playlistPostExits);
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
                    WaapiUris.UiProjectOpen,
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
                        WaapiPropertyNames.PlayAt,
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
                        WaapiPropertyNames.FadeInDuration,
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
                        WaapiPropertyNames.FadeOutDuration,
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

        if (playlistPostExits.Count > 0)
        {
            log(UiStrings.LogPlaylistPostExitPatchDone(playlistPostExits.Count));
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
    /// 排他: 対応 State は 0dB（Property 省略）、他 State は MuteVolumeDb。
    /// Additive: 当該レイヤー以降の State は 0dB、それ未満は MuteVolumeDb。
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
                var isUnmuted = IsGroupStateVolumeUnmuted(patch, stateName);
                customStateList.AppendChild(
                    BuildCustomStateVolumeElement(
                        doc,
                        stateName,
                        stateId,
                        isUnmuted ? null : patch.MuteVolumeDb));
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
                var isUnmuted = IsGroupStateVolumeUnmuted(patch, stateName);
                var expected = isUnmuted ? 0.0 : patch.MuteVolumeDb;
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

    /// <summary>
    /// グループ State Volume が 0dB になるか。
    /// 排他: 当該レイヤー State のみ。Additive: 当該レイヤー以降すべて。
    /// </summary>
    private static bool IsGroupStateVolumeUnmuted(
        MusicTrackStateVolumePatch patch,
        string stateName)
    {
        if (!patch.AdditiveLayers)
        {
            return string.Equals(
                stateName,
                patch.LayerStateName,
                StringComparison.Ordinal);
        }

        var layerIndex = IndexOfStateName(patch.OrderedStateNames, patch.LayerStateName);
        var stateIndex = IndexOfStateName(patch.OrderedStateNames, stateName);
        return layerIndex >= 0 && stateIndex >= layerIndex;
    }

    private static int IndexOfStateName(IReadOnlyList<string> stateNames, string stateName)
    {
        for (var i = 0; i < stateNames.Count; i++)
        {
            if (string.Equals(stateNames[i], stateName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
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
    /// Music Playlist Container 自身の既定トランジションルール（Any to Any）へ
    /// Play post-exit（PlaySourcePostExit）を書き込む。WAAPI 非対応のため WWU 直編集。
    /// </summary>
    private static void PatchPlaylistPostExitInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<PlaylistPostExitPatch> patches,
        Action<string> log)
    {
        WaitForExclusiveFileAccess(wwuPath);

        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var rule = FindPlaylistAnyToAnyRule(doc, patch.PlaylistContainerName)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrPlaylistAnyToAnyRuleMissing(
                        patch.PlaylistContainerName, wwuPath));

            var propertyList = EnsureChildElement(doc, rule, "PropertyList", prepend: true);
            // UI「Play post-exit」＝ WObjects の PlaySourcePostExit（@PlayPostExit は無効）。
            UpsertBoolProperty(doc, propertyList, "PlaySourcePostExit", patch.PlayPostExit);
        }

        doc.Save(wwuPath);
        log(UiStrings.LogPlaylistPostExitPatchFile(Path.GetFileName(wwuPath), patches.Count));
    }

    private static void VerifyPlaylistPostExitInWorkUnitFile(
        string wwuPath,
        IReadOnlyList<PlaylistPostExitPatch> patches)
    {
        var doc = new System.Xml.XmlDocument { PreserveWhitespace = true };
        doc.Load(wwuPath);

        foreach (var patch in patches)
        {
            var rule = FindPlaylistAnyToAnyRule(doc, patch.PlaylistContainerName)
                ?? throw new InvalidOperationException(
                    UiStrings.ErrPlaylistAnyToAnyRuleMissing(
                        patch.PlaylistContainerName, wwuPath));

            VerifyBoolProperty(
                rule,
                "PlaySourcePostExit",
                patch.PlayPostExit,
                patch.PlaylistContainerName);
        }
    }

    /// <summary>
    /// Music Playlist Container の TransitionRoot 直下から既定の Any to Any ルールを探す。
    /// コンテナ作成時に Wwise が自動生成するルール（Source / Destination とも Any）が対象。
    /// </summary>
    private static System.Xml.XmlElement? FindPlaylistAnyToAnyRule(
        System.Xml.XmlDocument doc,
        string containerName)
    {
        var containers = doc.SelectNodes("//MusicPlaylistContainer");
        if (containers is null)
        {
            return null;
        }

        foreach (System.Xml.XmlNode node in containers)
        {
            if (node is not System.Xml.XmlElement container
                || !string.Equals(
                    container.GetAttribute("Name"),
                    containerName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var rules = container.SelectNodes(
                "ReferenceList/Reference[@Name='TransitionRoot']/Custom/MusicTransition"
                + "/ChildrenList/MusicTransition");
            if (rules is null)
            {
                return null;
            }

            foreach (System.Xml.XmlNode ruleNode in rules)
            {
                if (ruleNode is not System.Xml.XmlElement rule
                    || IsMusicTransitionFolder(rule))
                {
                    continue;
                }

                if (ReadTransitionContextType(rule, "SourceContextType") == 0
                    && ReadTransitionContextType(rule, "DestinationContextType") == 0)
                {
                    return rule;
                }
            }

            return null;
        }

        return null;
    }

    /// <summary>MusicTransition の Context Type を読む（未記載はスキーマ既定の 0 = Any）。</summary>
    private static int ReadTransitionContextType(
        System.Xml.XmlElement rule,
        string propertyName)
    {
        var property = rule.SelectSingleNode($"PropertyList/Property[@Name='{propertyName}']")
            as System.Xml.XmlElement;
        if (property is null)
        {
            return 0;
        }

        return int.TryParse(
            property.GetAttribute("Value"),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
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
    /// クローズ進行中は WaapiUris.Locked、完了後は「プロジェクト未ロード」系エラーか空結果になる。
    /// 期限内に閉じきらない場合は例外（黙って WWU 直接編集へ進むとプロジェクト破損の危険がある）。
    /// </summary>
}
