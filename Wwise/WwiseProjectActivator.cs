using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// ロック中 Wwise プロジェクトを開く／既に開いていれば前面化する。
/// WAAPI が使えるときは RPC、だめなときは Wwise.exe を直接起動（なければ .wproj の関連付け）。
/// </summary>
internal static class WwiseProjectActivator
{
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    public static async Task<(bool Ok, string Message)> OpenOrFocusAsync(
        WaapiSettings settings,
        string projectFilePath,
        CancellationToken cancellationToken = default)
    {
        var path = projectFilePath.Trim();
        if (path.Length == 0)
        {
            return (false, UiStrings.LogWwiseProjectPathMissing);
        }

        if (!File.Exists(path))
        {
            return (false, UiStrings.LogWwiseProjectFileMissing(path));
        }

        var waapiReachable = false;
        try
        {
            using var client = new WaapiHttpClient(
                settings.Url,
                TimeSpan.FromMilliseconds(Math.Max(settings.TimeoutMs, 10000)));

            var info = await WaapiCoreCalls.GetInfoAsync(client, cancellationToken)
                .ConfigureAwait(false);
            waapiReachable = true;
            if (TryGetProcessId(info, out var processId))
            {
                _ = AllowSetForegroundWindow(processId);
            }

            var currentPath = string.Empty;
            try
            {
                var project = await WaapiCoreCalls.GetProjectInfoAsync(client, cancellationToken)
                    .ConfigureAwait(false);
                currentPath = WaapiJson.ReadProjectFilePath(project);
            }
            catch
            {
                // プロジェクト未ロード
            }

            if (PathsEqual(currentPath, path))
            {
                await WaapiCoreCalls.BringToForegroundAsync(client, cancellationToken)
                    .ConfigureAwait(false);
                return (true, UiStrings.LogWwiseProjectBroughtToFront(Path.GetFileNameWithoutExtension(path)));
            }

            await client.CallAsync(
                    WaapiUris.UiProjectOpen,
                    new Dictionary<string, object?> { ["path"] = path },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (TryGetProcessId(info, out processId))
            {
                _ = AllowSetForegroundWindow(processId);
            }

            try
            {
                await client.CallAsync(
                        WaapiUris.UiBringToForeground,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // open 直後は前面化に失敗することがある（ロード中など）。開ければ成功扱い。
            }

            return (true, UiStrings.LogWwiseProjectOpened(Path.GetFileNameWithoutExtension(path)));
        }
        catch (Exception ex)
        {
            // getInfo 応答済み＝Wwise は起動中。project.open の遅延／タイムアウトで
            // シェル起動へ落とすと Wwise が二重起動するため、失敗として返す。
            if (waapiReachable)
            {
                return (false, UiStrings.LogWwiseProjectOpenRequestFailed(ex.Message));
            }

            return OpenViaAuthoringOrShell(path);
        }
    }

    /// <summary>
    /// 接続中の Wwise Authoring を前面化する（プロジェクトの開閉はしない）。
    /// </summary>
    public static async Task<(bool Ok, string Message)> BringToForegroundAsync(
        WaapiSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new WaapiHttpClient(
                settings.Url,
                TimeSpan.FromMilliseconds(Math.Max(settings.TimeoutMs, 10000)));

            var info = await WaapiCoreCalls.GetInfoAsync(client, cancellationToken)
                .ConfigureAwait(false);
            if (TryGetProcessId(info, out var processId))
            {
                _ = AllowSetForegroundWindow(processId);
            }

            await WaapiCoreCalls.BringToForegroundAsync(client, cancellationToken)
                .ConfigureAwait(false);
            return (true, UiStrings.LogWwiseBroughtToFront);
        }
        catch (Exception ex)
        {
            return (false, UiStrings.LogWwiseBringToFrontFailed(ex.Message));
        }
    }

    /// <summary>
    /// WAAPI 不通時: インストール済み Wwise.exe を .wproj の版に合わせて直接起動する。
    /// （.wproj の既定関連付けは Wwise Launcher のため、シェル実行だけでは Authoring が開かないことがある。）
    /// </summary>
    private static (bool Ok, string Message) OpenViaAuthoringOrShell(string path)
    {
        try
        {
            if (TryFindWwiseExecutable(path, out var wwiseExe))
            {
                var start = new ProcessStartInfo
                {
                    FileName = wwiseExe,
                    UseShellExecute = false,
                };
                start.ArgumentList.Add(path);
                Process.Start(start);
                return (true, UiStrings.LogWwiseProjectShellOpen(Path.GetFileNameWithoutExtension(path)));
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return (true, UiStrings.LogWwiseProjectShellOpen(Path.GetFileNameWithoutExtension(path)));
        }
        catch (Exception ex)
        {
            return (false, UiStrings.LogWwiseProjectOpenFailed(ex.Message));
        }
    }

    private static bool TryFindWwiseExecutable(string projectFilePath, out string wwiseExe)
    {
        wwiseExe = string.Empty;
        TryReadProjectVersion(projectFilePath, out var version, out var build);

        var versionKey = version.Trim().TrimStart('v', 'V');
        string? exact = null;
        string? versionMatch = null;

        foreach (var root in EnumerateAudiokineticRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var dir in Directory.EnumerateDirectories(root, "Wwise*"))
            {
                var exe = Path.Combine(dir, "Authoring", "x64", "Release", "bin", "Wwise.exe");
                if (!File.Exists(exe))
                {
                    continue;
                }

                var folder = Path.GetFileName(dir) ?? string.Empty;
                if (versionKey.Length > 0
                    && build.Length > 0
                    && folder.Equals($"Wwise{versionKey}.{build}", StringComparison.OrdinalIgnoreCase))
                {
                    exact = exe;
                    break;
                }

                if (versionMatch is null
                    && versionKey.Length > 0
                    && folder.StartsWith($"Wwise{versionKey}", StringComparison.OrdinalIgnoreCase))
                {
                    versionMatch = exe;
                }
            }

            if (exact is not null)
            {
                break;
            }
        }

        wwiseExe = exact ?? versionMatch ?? string.Empty;
        return wwiseExe.Length > 0;
    }

    private static IEnumerable<string> EnumerateAudiokineticRoots()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Audiokinetic");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Audiokinetic");
    }

    private static bool TryReadProjectVersion(
        string projectFilePath,
        out string version,
        out string build)
    {
        version = string.Empty;
        build = string.Empty;
        try
        {
            using var reader = XmlReader.Create(
                projectFilePath,
                new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true,
                    DtdProcessing = DtdProcessing.Prohibit,
                });
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (string.Equals(reader.Name, "WwiseDocument", StringComparison.Ordinal))
                {
                    version = reader.GetAttribute("WwiseVersion")?.Trim() ?? string.Empty;
                    build = reader.GetAttribute("WwiseBuild")?.Trim() ?? string.Empty;
                    return version.Length > 0 || build.Length > 0;
                }

                // ルート以外に進んだら諦める（巨大 .wproj を全部読まない）。
                if (reader.Depth > 0)
                {
                    break;
                }
            }
        }
        catch
        {
            // 版が取れなくてもシェル関連付けへフォールバックできる。
        }

        return false;
    }

    private static bool PathsEqual(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryGetProcessId(JsonElement info, out int processId)
    {
        processId = 0;
        if (info.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (info.TryGetProperty("processId", out var prop)
            && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetInt32(out processId)
            && processId > 0)
        {
            return true;
        }

        if (info.TryGetProperty("pid", out prop)
            && prop.ValueKind == JsonValueKind.Number
            && prop.TryGetInt32(out processId)
            && processId > 0)
        {
            return true;
        }

        return false;
    }
}
