using System.Text.Json;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>WAAPI 上のオブジェクト存在確認。</summary>
internal static class WaapiObjectUtil
{
    private static readonly string[] ReturnFieldsIdPath = ["id", "path"];

    public static async Task<bool> ExistsAsync(
        WaapiSettings settings,
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return false;
        }

        using var client = new WaapiHttpClient(
            settings.Url,
            TimeSpan.FromMilliseconds(settings.TimeoutMs));

        // path にバックスラッシュが含まれるため GUID/パスはダブルクォートで囲む
        var escaped = objectPath.Replace("\"", "\\\"", StringComparison.Ordinal);
        try
        {
            var result = await client.CallAsync(
                    WaapiUris.CoreObjectGet,
                    new Dictionary<string, object?> { ["waql"] = $"$ \"{escaped}\"" },
                    new Dictionary<string, object?> { ["return"] = ReturnFieldsIdPath },
                    cancellationToken)
                .ConfigureAwait(false);

            return result.TryGetProperty("return", out var arr)
                && arr.ValueKind == JsonValueKind.Array
                && arr.GetArrayLength() > 0;
        }
        catch (WaapiException ex) when (IsObjectNotFound(ex.Message))
        {
            // WAAPI は未存在パスに対し invalid_query / Object not found を返す
            return false;
        }
    }

    /// <summary>親の直下にある子オブジェクト名を列挙する。親が無ければ空。</summary>
    public static async Task<HashSet<string>> QueryChildNamesAsync(
        WaapiSettings settings,
        string parentPath,
        CancellationToken cancellationToken = default)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return names;
        }

        using var client = new WaapiHttpClient(
            settings.Url,
            TimeSpan.FromMilliseconds(settings.TimeoutMs));

        var parent = parentPath.Trim().TrimEnd('\\');
        var escaped = parent.Replace("\"", "\\\"", StringComparison.Ordinal);
        try
        {
            var result = await client.CallAsync(
                    WaapiUris.CoreObjectGet,
                    new Dictionary<string, object?>
                    {
                        ["waql"] = $"$ \"{escaped}\" select children",
                    },
                    new Dictionary<string, object?>
                    {
                        ["return"] = new[] { "name" },
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.TryGetProperty("return", out var arr)
                || arr.ValueKind != JsonValueKind.Array)
            {
                return names;
            }

            foreach (var item in arr.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : null;
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }
        catch (WaapiException ex) when (IsObjectNotFound(ex.Message))
        {
            return names;
        }
    }

    private static bool IsObjectNotFound(string message) =>
        message.Contains("Object not found", StringComparison.OrdinalIgnoreCase)
        || message.Contains("invalid_query", StringComparison.OrdinalIgnoreCase)
        || message.Contains(WaapiUris.QueryInvalidQuery, StringComparison.OrdinalIgnoreCase);
}
