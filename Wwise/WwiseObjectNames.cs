namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// Wwise オブジェクト名／書き出し基底名の固定・整形・制約チェック。
/// </summary>
/// <remarks>
/// Wwise は先頭が数字のオブジェクト名を拒否する。文字種の網羅的な公式リストは
/// 公開 Help の命名規約（ベストプラクティス）中心のため、書き出し WAV 名としても
/// 使う本アプリでは Windows ファイル名として不適切な文字・予約名も拒否する。
/// </remarks>
internal static class WwiseObjectNames
{
    /// <summary>複数波形モードの Music Switch / State Group 名。</summary>
    public const string MultiWaveContainerName = "Multi_Wave";

    private static readonly HashSet<string> ReservedWindowsFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>複数波形モードの Music Switch / State Group 名を返す。</summary>
    public static string MakeMultiWaveContainerName() => MultiWaveContainerName;

    /// <summary>
    /// Wwise は先頭が数字のオブジェクト名を付けられない。
    /// 空文字は「数字始まり」とはみなさない（呼び出し側で別判定）。
    /// </summary>
    public static bool StartsWithDigit(string? name)
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

    /// <summary>Windows 予約デバイス名（CON / COM1 など）か。</summary>
    public static bool IsReservedWindowsFileName(string name)
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
