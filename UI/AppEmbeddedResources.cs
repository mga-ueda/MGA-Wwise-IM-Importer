using System.Reflection;

namespace MgaWwiseIMImporter.UI;

/// <summary>exe に埋め込んだブランディング／フォント／クリック音資産へのアクセス。</summary>
internal static class AppEmbeddedResources
{
    private const string LogoName = "MgaWwiseIMImporter.Branding.MiyabiGameAudio.png";
    private const string WindowIconName = "MgaWwiseIMImporter.Branding.MgaWwiseIMImporter.ico";
    private const string LogFontName = "MgaWwiseIMImporter.Fonts.UDEVGothic-Regular.ttf";
    private const string MetronomeHighName = "MgaWwiseIMImporter.Wave.High.wav";
    private const string MetronomeLowName = "MgaWwiseIMImporter.Wave.Low.wav";

    public static Stream? OpenLogo() => Open(LogoName);

    public static Stream? OpenWindowIcon() => Open(WindowIconName);

    public static Stream? OpenLogFont() => Open(LogFontName);

    public static Stream? OpenMetronomeHigh() => Open(MetronomeHighName);

    public static Stream? OpenMetronomeLow() => Open(MetronomeLowName);

    private static Stream? Open(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream(name);
    }
}
