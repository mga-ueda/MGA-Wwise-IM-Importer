using System.Runtime.InteropServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// FixedDialog などの標準枠付きウィンドウをアプリのダークテーマに寄せる。
/// </summary>
internal static class DarkWindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    /// <summary>
    /// タイトルバー／枠をダーク化する（Win10 1809+ / Win11）。
    /// ハンドル作成前なら <see cref="Control.HandleCreated"/> で適用する。
    /// </summary>
    public static void ApplyImmersiveDarkTitleBar(Form form)
    {
        void Apply()
        {
            if (form.IsDisposed || !form.IsHandleCreated)
            {
                return;
            }

            var useDarkMode = 1;
            _ = DwmSetWindowAttribute(
                form.Handle,
                DwmwaUseImmersiveDarkMode,
                ref useDarkMode,
                sizeof(int));

            // Win11: キャプション／枠／文字色をダイアログ本体に寄せる（未対応 OS では失敗して無視）。
            var caption = ToColorRef(UiColors.DialogBodyBack);
            var border = ToColorRef(UiColors.ChromeBorder);
            var text = ToColorRef(UiColors.DialogFore);
            _ = DwmSetWindowAttribute(form.Handle, DwmwaCaptionColor, ref caption, sizeof(int));
            _ = DwmSetWindowAttribute(form.Handle, DwmwaBorderColor, ref border, sizeof(int));
            _ = DwmSetWindowAttribute(form.Handle, DwmwaTextColor, ref text, sizeof(int));
        }

        if (form.IsHandleCreated)
        {
            Apply();
        }
        else
        {
            form.HandleCreated += (_, _) => Apply();
        }
    }

    /// <summary>COLORREF（0x00BBGGRR）。</summary>
    private static int ToColorRef(Color color) =>
        color.R | (color.G << 8) | (color.B << 16);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attr,
        ref int attrValue,
        int attrSize);
}
