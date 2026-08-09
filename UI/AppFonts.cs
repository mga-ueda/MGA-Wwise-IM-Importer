using WpfApplication = System.Windows.Application;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfTypeface = System.Windows.Media.Typeface;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// UI は Yu Gothic UI（Bold あり）。ログのみ埋め込み UDEV Gothic Regular。
/// </summary>
internal static class AppFonts
{
    private const string LogFontFamilyName = "UDEV Gothic";
    private const string PackLogFontUri =
        "pack://application:,,,/Assets/Fonts/UDEVGothic-Regular.ttf#" + LogFontFamilyName;

    /// <summary>WinForms 時代と同じ UI フォント。システム同梱で Bold が使える。</summary>
    public static WpfFontFamily UiFamily { get; } = new("Yu Gothic UI");

    private static WpfFontFamily? _logFamily;
    private static WpfTypeface? _logTypeface;

    /// <summary>UI 用 Yu Gothic UI。</summary>
    public static WpfFontFamily AppFamily => UiFamily;

    public static WpfTypeface LogTypeface =>
        _logTypeface ??= new WpfTypeface(
            EnsureLogFamily(),
            System.Windows.FontStyles.Normal,
            System.Windows.FontWeights.Normal,
            System.Windows.FontStretches.Normal);

    /// <summary>ログ用 UDEV Gothic を登録する。UI フォントはシステム依存のため登録不要。</summary>
    public static void EnsureRegistered() => _ = EnsureLogFamily();

    /// <summary>
    /// WinForms pt → WPF DIP（96 DPI 換算）。例: 9pt → 12、8.5pt → 11.333、7pt → 9.333。
    /// </summary>
    public static double DipFromPoints(double points) => points * 96d / 72d;

    private static WpfFontFamily EnsureLogFamily()
    {
        if (_logFamily is not null)
        {
            return _logFamily;
        }

        if (TryRegisterFromPackUri(out var family))
        {
            _logFamily = family;
            RegisterExitCleanup();
            return _logFamily;
        }

        if (TryRegisterFromEmbeddedFile(out family))
        {
            _logFamily = family;
            RegisterExitCleanup();
            return _logFamily;
        }

        _logFamily = new WpfFontFamily("Consolas");
        return _logFamily;
    }

    private static bool TryRegisterFromPackUri(out WpfFontFamily family)
    {
        family = new WpfFontFamily("Consolas");
        try
        {
            var candidate = new WpfFontFamily(PackLogFontUri);
            if (!TryValidateGlyphTypeface(candidate))
            {
                return false;
            }

            family = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRegisterFromEmbeddedFile(out WpfFontFamily family)
    {
        family = new WpfFontFamily("Consolas");
        try
        {
            using var stream = AppEmbeddedResources.OpenLogFont();
            if (stream is null)
            {
                return false;
            }

            var fontData = new byte[stream.Length];
            stream.ReadExactly(fontData);

            var path = EnsureExtractedFontFile(fontData);
            var uri = new Uri(path, UriKind.Absolute);
            var candidate = new WpfFontFamily(uri, LogFontFamilyName);
            if (!TryValidateGlyphTypeface(candidate))
            {
                return false;
            }

            family = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryValidateGlyphTypeface(WpfFontFamily family)
    {
        foreach (var typeface in family.GetTypefaces())
        {
            if (typeface.TryGetGlyphTypeface(out _))
            {
                return true;
            }
        }

        return false;
    }

    private static string EnsureExtractedFontFile(byte[] fontData)
    {
        var dir = Path.Combine(Path.GetTempPath(), "MgaWwiseIMImporter");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "UDEVGothic-Regular.ttf");

        if (File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.AsSpan().SequenceEqual(fontData))
            {
                return path;
            }
        }

        File.WriteAllBytes(path, fontData);
        return path;
    }

    private static void RegisterExitCleanup()
    {
        if (WpfApplication.Current is null)
        {
            return;
        }

        WpfApplication.Current.Exit += (_, _) =>
        {
            _logFamily = null;
            _logTypeface = null;
        };
    }
}
