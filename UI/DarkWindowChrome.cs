using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 標準枠付き WPF ウィンドウをアプリのダークテーマに寄せる。
/// </summary>
internal static class DarkWindowChrome
{
    private const int DwmwaUseImmersiveDarkModeBefore20 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    /// <summary>
    /// タイトルバー／枠をダーク化する（Win10 1809+ / Win11）。
    /// <see cref="Window.SourceInitialized"/> 以降、または HWND 取得後に適用する。
    /// </summary>
    public static void ApplyImmersiveDarkTitleBar(Window window)
    {
        void Apply()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var useDarkMode = 1;
            // 20 が失敗する古い Win10 では 19 を試す。
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(
                    hwnd,
                    DwmwaUseImmersiveDarkModeBefore20,
                    ref useDarkMode,
                    sizeof(int));
            }

            // Win11: キャプション色は従来の #1E2026。枠／文字はテーマ色（未対応 OS では失敗して無視）。
            var caption = ToColorRef(UiColors.TitleBarBack);
            var border = ToColorRef(UiColors.ChromeBorder);
            var text = ToColorRef(UiColors.DialogFore);
            _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
            _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
        }

        // SourceInitialized ハンドラ内から呼ばれた場合、IsLoaded はまだ false だが HWND は既にある。
        // IsLoaded で遅延するとイベントを取り逃して白タイトルバーのままになる。
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            Apply();
        }
        else
        {
            window.SourceInitialized += (_, _) => Apply();
        }
    }

    /// <summary>COLORREF（0x00BBGGRR）。</summary>
    private static int ToColorRef(System.Windows.Media.Color color) =>
        color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attr,
        ref int attrValue,
        int attrSize);
}
