using System.Text;
using System.Text.RegularExpressions;
using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// WAAPI の生エラーを、Wwise オブジェクト名の制約として読める文言へ落とす。
/// </summary>
internal static class WwiseImportFailure
{
    private static readonly Regex UnknownObjectPath = new(
        @"Object\s+(?<path>.+?)\s+is unknown",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// 計画上のコンテナ名が Wwise で付けられない（先頭数字）なら、EXPORT 前に出す説明。
    /// </summary>
    public static bool TryDescribeInvalidPlanName(WwiseMusicPlan plan, out string message)
    {
        message = string.Empty;
        var name = plan.ContainerName;
        if (string.IsNullOrWhiteSpace(name)
            || WwiseObjectNames.TryValidateBaseName(name, out var reason)
            || reason != WwiseBaseNameRejectReason.StartsWithDigit)
        {
            return false;
        }

        message = FormatRejectedName(
            name,
            startsWithDigit: true,
            unusableStateChars: WwiseObjectNames.ContainsUnusableStateNameChars(name),
            rawWaapiMessage: null);
        return true;
    }

    /// <summary>
    /// WAAPI 失敗を、名前制約で説明できるなら差し替える。
    /// </summary>
    public static string Format(string rawMessage, WwiseMusicPlan? plan)
    {
        var name = plan?.ContainerName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = TryExtractObjectLeafName(rawMessage);
        }

        if (string.IsNullOrWhiteSpace(name) || !LooksLikeUnknownObject(rawMessage))
        {
            return rawMessage;
        }

        var startsWithDigit = !WwiseObjectNames.TryValidateBaseName(name, out var reason)
            && reason == WwiseBaseNameRejectReason.StartsWithDigit;
        var unusableStateChars = WwiseObjectNames.ContainsUnusableStateNameChars(name);
        if (!startsWithDigit && !unusableStateChars)
        {
            return rawMessage;
        }

        return FormatRejectedName(name, startsWithDigit, unusableStateChars, rawMessage);
    }

    internal static string FormatRejectedName(
        string name,
        bool startsWithDigit,
        bool unusableStateChars,
        string? rawWaapiMessage)
    {
        var sb = new StringBuilder();
        sb.Append(UiStrings.ErrWwiseObjectNameRejectedHeader(name));
        sb.AppendLine();
        sb.AppendLine();
        if (startsWithDigit)
        {
            sb.AppendLine(UiStrings.ErrWwiseObjectNameRejectedDigit);
        }

        if (unusableStateChars)
        {
            sb.AppendLine(UiStrings.ErrWwiseObjectNameRejectedTwoByte);
        }

        sb.AppendLine();
        sb.AppendLine(UiStrings.ErrWwiseObjectNameRejectedFallbackLimit(name));
        sb.AppendLine();
        sb.Append(UiStrings.ErrWwiseObjectNameRejectedWhatToDo);
        if (!string.IsNullOrWhiteSpace(rawWaapiMessage))
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(rawWaapiMessage.Trim());
        }

        return sb.ToString();
    }

    internal static string? TryExtractObjectLeafName(string message)
    {
        var match = UnknownObjectPath.Match(message);
        if (!match.Success)
        {
            return null;
        }

        var path = match.Groups["path"].Value.Trim().Trim('"');
        path = path.Replace(@"\\", @"\", StringComparison.Ordinal);
        var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts[^1];
    }

    private static bool LooksLikeUnknownObject(string message) =>
        message.Contains("is unknown", StringComparison.OrdinalIgnoreCase)
        || message.Contains("ak.wwise.invalid_arguments", StringComparison.OrdinalIgnoreCase);
}
