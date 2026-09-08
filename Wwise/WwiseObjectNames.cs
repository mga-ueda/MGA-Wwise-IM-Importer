using System.Globalization;
using MgaWwiseIMImporter.Domain;

namespace MgaWwiseIMImporter.Wwise;

/// <summary>
/// Wwise オブジェクト名／書き出し基底名の固定・整形・制約チェック。
/// </summary>
/// <remarks>
/// Wwise は先頭が数字のオブジェクト名を拒否する。文字種の網羅的な公式リストは
/// 公開 Help の命名規約（ベストプラクティス）中心のため、書き出し WAV 名としても
/// 使う本アプリでは Windows ファイル名として不適切な文字・予約名も拒否する。
/// State 名および State Group 名は 2 バイト文字を扱えず <c>_</c> に置換されるため、
/// 該当時は <see cref="BuildFallbackSwitchStateName"/> を使う。
/// </remarks>
internal static class WwiseObjectNames
{
    /// <summary>複数波形モードの Music Switch / State Group 名。</summary>
    public const string MultiWaveContainerName = "Multi_Wave";

    /// <summary>2 バイト文字を含むときの Switch State / State Group 名プレフィックス。</summary>
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
    /// Wwise の State / State Group 名として使えない文字（2 バイト文字＝非 ASCII）を含むか。
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
    /// Switch State / State Group の代替名。<paramref name="count"/> が 1 桁なら <c>Music_1</c>、
    /// 2 桁なら <c>Music_01</c>、3 桁なら <c>Music_001</c>。
    /// </summary>
    /// <param name="oneBasedIndex">1 始まりの番号。</param>
    /// <param name="count">総数（桁数の根拠）。</param>
    public static string BuildFallbackSwitchStateName(int oneBasedIndex, int count)
    {
        var total = Math.Max(1, count);
        var index = Math.Max(1, oneBasedIndex);
        var width = total.ToString(CultureInfo.InvariantCulture).Length;
        return FallbackSwitchStatePrefix
            + index.ToString("D" + width, CultureInfo.InvariantCulture);
    }

    /// <summary>フォールバック名の番号を繰り上げる上限（無限ループ防止）。</summary>
    public const int MaxFallbackNameIncrement = 999;

    /// <summary>
    /// 希望名が State / State Group として使えるならそのまま、2 バイト文字を含むなら
    /// <see cref="BuildFallbackSwitchStateName"/>。
    /// </summary>
    public static string ResolveUsableStateObjectName(string? name, int oneBasedIndex, int count) =>
        ContainsUnusableStateNameChars(name)
            ? BuildFallbackSwitchStateName(oneBasedIndex, count)
            : name ?? string.Empty;

    /// <summary><c>Music_1</c> / <c>Music_01</c> 形式なら番号を返す。</summary>
    public static bool TryParseFallbackSwitchStateName(string? name, out int index, out int digitWidth)
    {
        index = 0;
        digitWidth = 0;
        if (string.IsNullOrEmpty(name)
            || !name.StartsWith(FallbackSwitchStatePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = name[FallbackSwitchStatePrefix.Length..];
        if (digits.Length == 0
            || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out index)
            || index < 1)
        {
            return false;
        }

        digitWidth = digits.Length;
        return true;
    }

    /// <summary>フォールバック名の番号を 1 つ繰り上げる。<c>Music_9</c> → <c>Music_10</c>。</summary>
    public static string NextFallbackSwitchStateName(string current)
    {
        if (!TryParseFallbackSwitchStateName(current, out var index, out var digitWidth))
        {
            throw new ArgumentException(
                "Fallback State Group name must be Music_N.",
                nameof(current));
        }

        var next = checked(index + 1);
        var width = Math.Max(digitWidth, next.ToString(CultureInfo.InvariantCulture).Length);
        return FallbackSwitchStatePrefix
            + next.ToString("D" + width, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// <paramref name="taken"/> に無いフォールバック名を返す。希望名が使用中なら番号を繰り上げる。
    /// </summary>
    public static string AllocateUnusedFallbackName(string desired, ISet<string> taken)
    {
        var name = desired;
        for (var i = 0; i < MaxFallbackNameIncrement; i++)
        {
            if (!taken.Contains(name))
            {
                return name;
            }

            name = NextFallbackSwitchStateName(name);
        }

        throw new InvalidOperationException(
            UiStrings.ErrGroupStateFallbackNameExhausted(desired));
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
