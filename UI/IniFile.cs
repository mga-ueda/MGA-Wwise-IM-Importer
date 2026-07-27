using System.Globalization;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 簡易 INI（セクション単位のキー=値）。コメント行と未知セクションは保持する。
/// 読み書きは UTF-8（BOM 付きで保存）。日本語パス等の文字化けを防ぐ。
/// </summary>
internal static class IniFile
{
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "MgaWwiseIMImporter.ini");

    /// <summary>
    /// INI の真偽値を読む。整数なら 0 以外を true、それ以外は <see cref="bool.TryParse"/>。
    /// </summary>
    public static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool defaultValue)
    {
        if (!values.TryGetValue(key, out var text))
        {
            return defaultValue;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number != 0;
        }

        return bool.TryParse(text, out var flag) ? flag : defaultValue;
    }

    public static Dictionary<string, string> ReadSection(string section)
    {
        var path = Path;
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inSection = false;

        foreach (var rawLine in TextFileUtf8.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSection = string.Equals(line[1..^1], section, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            values[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        return values;
    }

    public static void WriteSection(string section, IReadOnlyDictionary<string, string> values)
    {
        var path = Path;
        var lines = File.Exists(path)
            ? TextFileUtf8.ReadAllLines(path).ToList()
            : [];

        var sectionHeader = $"[{section}]";
        var (start, end) = FindSectionRange(lines, sectionHeader);

        var replacement = new List<string> { sectionHeader };
        foreach (var pair in values)
        {
            replacement.Add($"{pair.Key}={pair.Value}");
        }

        if (start < 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(replacement);
        }
        else
        {
            lines.RemoveRange(start, end - start);
            // 直前が空行でない場合はそのまま挿入（後続セクションとの区切りは既存に委ねる）
            lines.InsertRange(start, replacement);
            if (start + replacement.Count < lines.Count
                && lines[start + replacement.Count].Trim().Length > 0
                && lines[start + replacement.Count].Trim().StartsWith('['))
            {
                lines.Insert(start + replacement.Count, string.Empty);
            }
        }

        TextFileUtf8.WriteAllLines(path, lines, emitBom: true);
    }

    /// <summary>指定セクションを丸ごと削除する（存在しなければ何もしない）。</summary>
    public static void RemoveSection(string section)
    {
        var path = Path;
        if (!File.Exists(path))
        {
            return;
        }

        var lines = TextFileUtf8.ReadAllLines(path).ToList();
        var (start, end) = FindSectionRange(lines, $"[{section}]");

        if (start < 0)
        {
            return;
        }

        lines.RemoveRange(start, end - start);
        while (start < lines.Count && lines[start].Trim().Length == 0)
        {
            lines.RemoveAt(start);
        }

        TextFileUtf8.WriteAllLines(path, lines, emitBom: true);
    }

    /// <summary>
    /// セクションヘッダー行の位置と、次セクション開始（または末尾）までの範囲を返す。
    /// 見つからなければ start は -1。
    /// </summary>
    private static (int Start, int End) FindSectionRange(List<string> lines, string sectionHeader)
    {
        var start = -1;
        var end = lines.Count;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (start >= 0)
                {
                    end = i;
                    break;
                }

                if (string.Equals(trimmed, sectionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                }
            }
        }

        return (start, end);
    }
}
