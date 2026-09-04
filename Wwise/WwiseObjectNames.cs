using System.Globalization;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// Wwise オブジェクト名／書き出し基底名の固定・整形・制約チェック。
/// </summary>
/// <remarks>
/// Wwise は先頭が数字のオブジェクト名を拒否する。文字種の網羅的な公式リストは
/// 公開 Help の命名規約（ベストプラクティス）中心のため、書き出し WAV 名としても
/// 使う本アプリでは Windows ファイル名として不適切な文字・予約名も拒否する。
/// State 名は 2 バイト文字を扱えず <c>_</c> に置換されるため、該当時は
/// <see cref="BuildFallbackSwitchStateName"/> を使う。
/// </remarks>
internal static class WwiseObjectNames
{
    /// <summary>複数波形モードの Music Switch / State Group 名。</summary>
    public const string MultiWaveContainerName = "Multi_Wave";

    /// <summary>2 バイト文字を含むファイル名があるときの Switch State 名プレフィックス。</summary>
    public const string FallbackSwitchStatePrefix = "Music_";

    private static readonly HashSet<string> ReservedWindowsFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Wwise は先頭が数字のオブジェクト名を付けられない。
    /// 空文字は「数字始まり」とはみなさない（呼び出し側で別判定）。
    /// </summary>
    private static bool StartsWithDigit(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return char.IsAsciiDigit(name[0]);
    }

    /// <summary>
    /// 書き出し基底名／リネーム候補として使えるか。
    /// Wwise の数字始まり制約＋ Windows ファイル名として不適切な文字・予約名を見る。
    /// </summary>
    public static bool TryValidateBaseName(string? name, out WwiseBaseNameRejectReason reason)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = WwiseBaseNameRejectReason.Empty;
            return false;
        }

        // 呼び出し側で Trim 済みを想定するが、空白のみは Empty 扱い。
        var value = name.Trim();
        if (value.Length == 0)
        {
            reason = WwiseBaseNameRejectReason.Empty;
            return false;
        }

        if (StartsWithDigit(value))
        {
            reason = WwiseBaseNameRejectReason.StartsWithDigit;
            return false;
        }

        if (IsReservedWindowsFileName(value))
        {
            reason = WwiseBaseNameRejectReason.ReservedWindowsName;
            return false;
        }

        if (ContainsInvalidFileNameContent(value))
        {
            reason = WwiseBaseNameRejectReason.InvalidFileNameChars;
            return false;
        }

        reason = WwiseBaseNameRejectReason.None;
        return true;
    }

    /// <summary>
    /// Wwise の State 名として使えない文字（2 バイト文字＝非 ASCII）を含むか。
    /// Wwise は該当文字を <c>_</c> に置換するため、パス参照がずれる。
    /// </summary>
    public static bool ContainsUnusableStateNameChars(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var ch in name)
        {
            if (!char.IsAscii(ch))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ドロップファイル名など、State 名の候補に 2 バイト文字が 1 つでもあれば true。
    /// </summary>
    public static bool ShouldUseFallbackSwitchStateNames(IEnumerable<string?> names)
    {
        foreach (var name in names)
        {
            if (ContainsUnusableStateNameChars(name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Switch State の代替名。<paramref name="count"/> が 1 桁なら <c>Music_1</c>、
    /// 2 桁なら <c>Music_01</c>、3 桁なら <c>Music_001</c>。
    /// </summary>
    /// <param name="oneBasedIndex">1 始まりの番号。</param>
    /// <param name="count">State 総数（桁数の根拠）。</param>
    public static string BuildFallbackSwitchStateName(int oneBasedIndex, int count)
    {
        var total = Math.Max(1, count);
        var index = Math.Max(1, oneBasedIndex);
        var width = total.ToString(CultureInfo.InvariantCulture).Length;
        return FallbackSwitchStatePrefix
            + index.ToString("D" + width, CultureInfo.InvariantCulture);
    }

    /// <summary>Windows 予約デバイス名（CON / COM1 など）か。</summary>
    private static bool IsReservedWindowsFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // CON.txt → CON として予約扱い（基底名に拡張子が混ざるケース用）。
        var stem = name;
        var dot = name.IndexOf('.');
        if (dot > 0)
        {
            stem = name[..dot];
        }

        return ReservedWindowsFileNames.Contains(stem);
    }

    private static bool ContainsInvalidFileNameContent(string name)
    {
        // Windows: 末尾の空白・ピリオドは実体ファイル名として不適切。
        if (name.EndsWith(' ') || name.EndsWith('.'))
        {
            return true;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return true;
        }

        foreach (var ch in name)
        {
            if (char.IsControl(ch))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary><see cref="WwiseObjectNames.TryValidateBaseName"/> の拒否理由。</summary>
internal enum WwiseBaseNameRejectReason
{
    None = 0,
    Empty,
    StartsWithDigit,
    InvalidFileNameChars,
    ReservedWindowsName,
}
