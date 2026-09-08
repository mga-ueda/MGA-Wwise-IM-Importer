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
/// Switch State は <see cref="BuildFallbackSwitchStateName"/>、
/// 複数波形の Music Switch／その State Group は <see cref="ResolveMultiWaveContainerName"/>。
/// </remarks>
internal static class WwiseObjectNames
{
    /// <summary>
    /// 複数波形モードの Music Switch／その State Group 名のフォールバック。
    /// 希望名が State Group に使えない文字（2 バイト）を含むときに使う。
    /// </summary>
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
    /// リネーム名の正規化と検証。
    /// 半角英数字と <c>_</c> 以外の ASCII（スペース・括弧・ハイフン等）は <c>_</c> へ置換する。
    /// 日本語などの 2 バイト文字は表示名として許可し、State Group は
    /// <see cref="ResolveMultiWaveContainerName"/> 側で <c>Multi_Wave</c> へ落とす。
    /// 半角カナは不可。先頭の数字も不可（Wwise が拒否）。
    /// Windows 予約名は書き出し WAV 名を兼ねるため不可。
    /// </summary>
    public static bool TryNormalizeRenameName(
        string? name,
        out string normalized,
        out WwiseBaseNameRejectReason reason)
    {
        normalized = (name ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            reason = WwiseBaseNameRejectReason.Empty;
            return false;
        }

        // 救済: ドロップ可能な ASCII 記号は _ に置換してから再判定する。
        normalized = SalvageRenameSeparatorChars(normalized);

        if (StartsWithDigit(normalized))
        {
            reason = WwiseBaseNameRejectReason.StartsWithDigit;
            return false;
        }

        foreach (var ch in normalized)
        {
            if (!char.IsAscii(ch))
            {
                // 日本語は許可。半角カナだけは State Group／表示のどちらでも使わない。
                if (IsHalfwidthKatakana(ch))
                {
                    reason = WwiseBaseNameRejectReason.NonAsciiChars;
                    return false;
                }

                continue;
            }

            if (!char.IsAsciiLetterOrDigit(ch) && ch != '_')
            {
                reason = WwiseBaseNameRejectReason.SymbolChars;
                return false;
            }
        }

        if (IsReservedWindowsFileName(normalized))
        {
            reason = WwiseBaseNameRejectReason.ReservedWindowsName;
            return false;
        }

        reason = WwiseBaseNameRejectReason.None;
        return true;
    }

    /// <summary>
    /// ファイル名では使えるが State Group では使えない ASCII 記号を <c>_</c> にする。
    /// 非 ASCII はそのまま残し、呼び出し側で拒否する。連続・末尾の <c>_</c> は畳む。
    /// </summary>
    private static string SalvageRenameSeparatorChars(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;
        var lastUnderscore = false;
        foreach (var ch in value)
        {
            if (!char.IsAscii(ch))
            {
                buffer[length++] = ch;
                lastUnderscore = false;
                continue;
            }

            if (char.IsAsciiLetterOrDigit(ch) || ch == '_')
            {
                buffer[length++] = ch;
                lastUnderscore = ch == '_';
                continue;
            }

            if (lastUnderscore)
            {
                continue;
            }

            buffer[length++] = '_';
            lastUnderscore = true;
        }

        // song(loop) → song_loop_ にならないよう、救済で付いた末尾 _ だけ落とす。
        while (length > 1 && buffer[length - 1] == '_')
        {
            length--;
        }

        return new string(buffer, 0, length);
    }

    /// <summary>半角カナ（U+FF61..FF9F）。UTF-8 では非 ASCII。</summary>
    private static bool IsHalfwidthKatakana(char ch) => ch is >= '\uFF61' and <= '\uFF9F';

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

    /// <summary>
    /// 複数波形の Music Switch／その State Group 名。
    /// 希望名が空、または State Group に使えない文字（2 バイト＝非 ASCII）を含むときは
    /// <see cref="MultiWaveContainerName"/>。それ以外は Trim した希望名。
    /// </summary>
    public static string ResolveMultiWaveContainerName(string? desiredName)
    {
        if (string.IsNullOrWhiteSpace(desiredName))
        {
            return MultiWaveContainerName;
        }

        var value = desiredName.Trim();
        return ContainsUnusableStateNameChars(value)
            ? MultiWaveContainerName
            : value;
    }

    /// <summary>
    /// 複数波形コンテナ名。<c>Multi_Wave</c> が 1 件目、<c>Multi_Wave_2</c> が 2 件目。
    /// </summary>
    public static string BuildMultiWaveContainerName(int oneBasedIndex)
    {
        var index = Math.Max(1, oneBasedIndex);
        return index == 1
            ? MultiWaveContainerName
            : MultiWaveContainerName + "_" + index.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary><c>Multi_Wave</c> / <c>Multi_Wave_2</c> 形式なら番号を返す（1 始まり）。</summary>
    public static bool TryParseMultiWaveContainerName(string? name, out int index)
    {
        index = 0;
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        if (string.Equals(name, MultiWaveContainerName, StringComparison.Ordinal))
        {
            index = 1;
            return true;
        }

        var prefix = MultiWaveContainerName + "_";
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = name[prefix.Length..];
        return digits.Length > 0
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out index)
            && index >= 2;
    }

    /// <summary>複数波形コンテナ名の番号を 1 つ繰り上げる。<c>Multi_Wave</c> → <c>Multi_Wave_2</c>。</summary>
    public static string NextMultiWaveContainerName(string current)
    {
        if (!TryParseMultiWaveContainerName(current, out var index))
        {
            throw new ArgumentException(
                "Multi-wave container name must be Multi_Wave or Multi_Wave_N.",
                nameof(current));
        }

        return BuildMultiWaveContainerName(checked(index + 1));
    }

    /// <summary>
    /// <paramref name="taken"/> に無い複数波形コンテナ名を返す。希望名が使用中なら番号を繰り上げる。
    /// </summary>
    public static string AllocateUnusedMultiWaveName(string desired, ISet<string> taken)
    {
        var name = desired;
        for (var i = 0; i < MaxFallbackNameIncrement; i++)
        {
            if (!taken.Contains(name))
            {
                return name;
            }

            name = NextMultiWaveContainerName(name);
        }

        throw new InvalidOperationException(
            UiStrings.ErrMultiWaveNameExhausted(desired));
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

        // 基底名（拡張子除く）の . は Wwise オブジェクト名／拡張子解釈が壊れるため不可。
        // % はパイプラインの変数展開と衝突しやすいため不可。
        if (name.Contains('.') || name.Contains('%'))
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

    /// <summary>2 バイト文字・半角カナなど非 ASCII（リネーム検証のみ）。</summary>
    NonAsciiChars,

    /// <summary>アンダースコア以外の記号（リネーム検証のみ）。</summary>
    SymbolChars,
}
