using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>通常ログとエクスポートオーバーレイで共有するログ行の色定義。</summary>
internal static class LogColorHelper
{
    /// <summary>
    /// ヘッダ行で警告／エラーブロックへ入り、別の === ヘッダで抜ける。
    /// </summary>
    public static LogColorSection AdvanceLogColorSection(string line, LogColorSection current)
    {
        var t = line.TrimStart();
        if (t.StartsWith("=== 警告", StringComparison.Ordinal)
            || t.StartsWith("=== Warning", StringComparison.OrdinalIgnoreCase))
        {
            return LogColorSection.Warning;
        }

        if (t.StartsWith("=== エラー", StringComparison.Ordinal)
            || t.StartsWith("=== Error", StringComparison.OrdinalIgnoreCase))
        {
            return LogColorSection.Error;
        }

        if (t.StartsWith("===", StringComparison.Ordinal))
        {
            return LogColorSection.Default;
        }

        return current;
    }

    public static Color ColorForLogLine(string line, LogColorSection section = LogColorSection.Default)
    {
        var t = line.TrimStart();
        if (t.Length == 0)
        {
            return UiColors.LogDefault;
        }

        if (t.StartsWith("Status  : OK", StringComparison.Ordinal))
        {
            return UiColors.SeekCyan;
        }

        if (t.StartsWith("Message : マーカー名を変更しました:", StringComparison.Ordinal)
            || t.StartsWith("Message : Marker renamed:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Message : 新しいバージョンがあります:", StringComparison.Ordinal)
            || t.StartsWith("Message : Update available:", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Message : 規定フォーマット", StringComparison.Ordinal)
            || t.StartsWith("Message : Wave format differs from expected", StringComparison.OrdinalIgnoreCase)
            || t.Contains(UiStrings.LogWaveFormatOffSpecSuffix, StringComparison.Ordinal))
        {
            return UiColors.LogWarning;
        }

        if (section == LogColorSection.Warning)
        {
            return UiColors.LogWarning;
        }

        if (section == LogColorSection.Error)
        {
            return UiColors.LogError;
        }

        if (t.StartsWith("[警告]", StringComparison.Ordinal)
            || t.StartsWith("=== 警告", StringComparison.Ordinal)
            || t.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("=== Warning", StringComparison.OrdinalIgnoreCase))
        {
            return UiColors.LogWarning;
        }

        if (t.StartsWith("=== エラー", StringComparison.Ordinal)
            || t.StartsWith("=== Error", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Status  : 接続失敗", StringComparison.Ordinal)
            || t.StartsWith("Status  : connection failed", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Status  : Disconnected", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Status  : NG", StringComparison.Ordinal)
            || t.StartsWith("自動読み込み対象が見つかりません", StringComparison.Ordinal)
            || t.StartsWith("Auto-load target was not found", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("Target  : （未選択）", StringComparison.Ordinal)
            || t.StartsWith("Target  : (none selected)", StringComparison.OrdinalIgnoreCase)
            || (t.StartsWith("Wave :", StringComparison.Ordinal)
                && (t.Contains("(なし)", StringComparison.Ordinal)
                    || t.Contains("(missing)", StringComparison.OrdinalIgnoreCase)))
            || IsErrorMessageLine(t))
        {
            return UiColors.LogError;
        }

        if (t.StartsWith("Target  :", StringComparison.Ordinal))
        {
            return UiColors.SeekCyan;
        }

        if (t.StartsWith("===", StringComparison.Ordinal))
        {
            return UiColors.LogHeader;
        }

        if (t.StartsWith("- ", StringComparison.Ordinal)
            || t.StartsWith("Dropped files:", StringComparison.OrdinalIgnoreCase))
        {
            return UiColors.LogMuted;
        }

        return UiColors.LogDefault;
    }

    private static bool IsErrorMessageLine(string trimmedLine)
    {
        if (!trimmedLine.StartsWith("Message :", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 「失敗」単体はファイル名（例: ○○ 失敗ジングル）に誤ヒットするため、
        // エラー文言の定型「〜に失敗」で判定する。
        return trimmedLine.Contains("に失敗", StringComparison.Ordinal)
            || trimmedLine.Contains("エラー", StringComparison.Ordinal)
            || trimmedLine.Contains("見つかりません", StringComparison.Ordinal)
            || trimmedLine.Contains("未達", StringComparison.Ordinal)
            || trimmedLine.Contains("形式不正", StringComparison.Ordinal)
            || trimmedLine.Contains("復元しません", StringComparison.Ordinal)
            || trimmedLine.Contains("スキップしました", StringComparison.Ordinal)
            || trimmedLine.Contains("ドロップしてください", StringComparison.Ordinal)
            || trimmedLine.Contains("必要です", StringComparison.Ordinal)
            || trimmedLine.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("Error", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("requirements not met", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("required", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("invalid format", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("was not restored", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("Skipped", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("Drop", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("Cannot connect", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("Disconnected", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Contains("none selected", StringComparison.OrdinalIgnoreCase);
    }
}
