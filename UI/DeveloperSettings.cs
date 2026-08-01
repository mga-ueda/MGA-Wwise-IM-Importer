using System.Globalization;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 開発者向け設定（exe 横の MgaWwiseIMImporter.ini [Developer]）。
/// </summary>
internal sealed class DeveloperSettings
{
    public const string Section = "Developer";

    /// <summary>Playlist／再生エンジンの詳細診断ログを出すか。既定はオン。</summary>
    public bool DetailedPlaybackLog { get; init; } = true;

    /// <summary>
    /// DEBUG 専用。UI スケールシミュレート対象 DPI。
    /// 0 = ディスプレイどおり、96 = 100% 相当、144 = 150% 相当。
    /// </summary>
    public int UiScaleSimulateDpi { get; init; }

    public static DeveloperSettings Load()
    {
        EnsureDefaultsWritten();

        var values = IniFile.ReadSection(Section);
        return new DeveloperSettings
        {
            DetailedPlaybackLog = values.TryGetValue("DetailedPlaybackLog", out var detailedLog)
                ? ParseBool(detailedLog, defaultValue: true)
                : true,
            UiScaleSimulateDpi = values.TryGetValue("UiScaleSimulateDpi", out var dpiText)
                ? ParseInt(dpiText, defaultValue: 0)
                : 0,
        };
    }

    /// <summary>
    /// 不足キーがあれば現状の既定値で書き足す（既存値は維持）。
    /// </summary>
    public static void EnsureDefaultsWritten()
    {
        var values = IniFile.ReadSection(Section);
        var changed = false;
        if (!values.ContainsKey("DetailedPlaybackLog"))
        {
            values["DetailedPlaybackLog"] = "1";
            changed = true;
        }

#if DEBUG
        if (!values.ContainsKey("UiScaleSimulateDpi"))
        {
            values["UiScaleSimulateDpi"] = "0";
            changed = true;
        }
#endif

        if (changed)
        {
            WriteSection(values);
        }
    }

    /// <summary>[Developer] DetailedPlaybackLog だけ更新する（他キーは維持）。</summary>
    public static void SaveDetailedPlaybackLog(bool enabled)
    {
        EnsureDefaultsWritten();
        var values = IniFile.ReadSection(Section);
        values["DetailedPlaybackLog"] = enabled ? "1" : "0";
        WriteSection(values);
    }

#if DEBUG
    /// <summary>[Developer] UiScaleSimulateDpi だけ更新する（他キーは維持）。</summary>
    public static void SaveUiScaleSimulateDpi(int dpi)
    {
        EnsureDefaultsWritten();
        var values = IniFile.ReadSection(Section);
        values["UiScaleSimulateDpi"] = dpi.ToString(CultureInfo.InvariantCulture);
        WriteSection(values);
    }
#endif

    private static void WriteSection(Dictionary<string, string> values)
    {
        var section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DetailedPlaybackLog"] = values.TryGetValue("DetailedPlaybackLog", out var detailedLog)
                ? detailedLog
                : "1",
        };

        // DEBUG で書いたシミュレート値を Release の他キー更新で消さない。
        if (values.TryGetValue("UiScaleSimulateDpi", out var dpiText))
        {
            section["UiScaleSimulateDpi"] = dpiText;
        }

        IniFile.WriteSection(Section, section);
    }

    private static int ParseInt(string text, int defaultValue)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : defaultValue;
    }

    private static bool ParseBool(string text, bool defaultValue)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number != 0;
        }

        if (bool.TryParse(text, out var flag))
        {
            return flag;
        }

        if (string.Equals(text, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(text, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return defaultValue;
    }
}
