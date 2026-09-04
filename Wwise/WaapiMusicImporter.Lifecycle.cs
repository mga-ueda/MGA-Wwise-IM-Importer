using System.Text;
using System.Text.Json;
using MgaWwiseIMImporter.Domain;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.Wwise;

internal static partial class WaapiMusicImporter
{
    /// <summary>
    /// プロジェクトのクローズ／ロード中に WAAPI の HTTP 接続が一時的に切れたときの例外か。
    /// （HttpRequestException=接続断、TaskCanceledException=HttpClient タイムアウト）
    /// </summary>
    private static bool IsTransientHttpError(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException;

    private static async Task WaitForProjectClosedAsync(WaapiHttpClient client)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var result = await client.CallAsync(
                        WaapiUris.CoreObjectGet,
                        new Dictionary<string, object?> { ["waql"] = "$ from type Project" },
                        new Dictionary<string, object?> { ["return"] = ReturnFieldsId },
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
                ex.Message.Contains(WaapiUris.Locked, StringComparison.OrdinalIgnoreCase)
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
            catch (Exception ex) when (IsTransientHttpError(ex))
            {
                // クローズ中は HTTP 接続自体が一瞬落ちることがある。待って再確認する。
            }

            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
        }

        throw new InvalidOperationException(UiStrings.ErrProjectCloseTimeout);
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
                        WaapiUris.CoreObjectGet,
                        new Dictionary<string, object?> { ["waql"] = "$ from type Project" },
                        new Dictionary<string, object?> { ["return"] = ReturnFieldsFilePath },
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
            catch (Exception ex) when (IsTransientHttpError(ex))
            {
                // ロード中は HTTP 接続自体が一瞬落ちることがある。待って再確認する。
            }

            await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Wwise が排他ロック中（WaapiUris.Locked、プロジェクトのクローズ／ロード進行中）の間、
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
                && (ex.Message.Contains(WaapiUris.Locked, StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("exclusive lock", StringComparison.OrdinalIgnoreCase)))
            {
                await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                DateTime.UtcNow < deadline && IsTransientHttpError(ex))
            {
                // クローズ／ロード直後は HTTP 接続が一瞬落ちることがある。リトライする。
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
                WaapiUris.CoreObjectGet,
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
                WaapiUris.CoreObjectGet,
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
                WaapiUris.CoreObjectSetProperty,
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
                WaapiUris.CoreObjectGet,
                new Dictionary<string, object?>
                {
                    ["waql"] = "$ from type MusicClip",
                },
                new Dictionary<string, object?>
                {
                    ["return"] = ReturnFieldsIdNameTypePath,
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
                        WaapiUris.CoreObjectSet,
                        new Dictionary<string, object?>
                        {
                            ["objects"] = new object[]
                            {
                                new Dictionary<string, object?>
                                {
                                    ["object"] = segmentPath,
                                    [WaapiPropertyNames.Cues] = cues,
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
                [WaapiPropertyNames.CueType] = 0,
                [WaapiPropertyNames.TimeMs] = entryLocal,
            },
            new Dictionary<string, object?>
            {
                ["type"] = "MusicCue",
                ["name"] = string.Empty,
                [WaapiPropertyNames.CueType] = 1,
                [WaapiPropertyNames.TimeMs] = exitLocal,
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
                [WaapiPropertyNames.CueType] = 2,
                [WaapiPropertyNames.TimeMs] = Math.Max(0.0, custom.TimeMs - origin),
            });
        }

        return cues;
    }
}
