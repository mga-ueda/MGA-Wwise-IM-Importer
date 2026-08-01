using System.Runtime.InteropServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 単一行 TextBox の文字を上下中央に置く。
/// Win32 の単一行 EDIT は EM_SETRECT を無視するため、Multiline にしたうえで整形矩形を使う。
/// </summary>
internal static class TextBoxVerticalAlign
{
    private const int EmSetRect = 0x00B3;
    private static readonly IntPtr GdiError = new(-1);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativeRect lParam);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool GetTextMetrics(IntPtr hdc, out TextMetric lptm);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TextMetric
    {
        public int tmHeight;
        public int tmAscent;
        public int tmDescent;
        public int tmInternalLeading;
        public int tmExternalLeading;
        public int tmAveCharWidth;
        public int tmMaxCharWidth;
        public int tmWeight;
        public int tmOverhang;
        public int tmDigitizedAspectX;
        public int tmDigitizedAspectY;
        public char tmFirstChar;
        public char tmLastChar;
        public char tmDefaultChar;
        public char tmBreakChar;
        public byte tmItalic;
        public byte tmUnderlined;
        public byte tmStruckOut;
        public byte tmPitchAndFamily;
        public byte tmCharSet;
    }

    /// <summary>
    /// Combo 子 EDIT 用の光学補正（150% 設計 1、切り捨て換算＝150%:1px / 100%:0px）。
    /// Combo は 100% だと丸め誤差の範囲で元々中央に乗るため、補正すると逆に上寄りになる。
    /// </summary>
    public static int OpticalNudge(Control? c = null)
    {
        var dpi = DesignMetrics.LayoutDpi(c);
        return (int)Math.Floor(1.0 * dpi / DesignMetrics.DesignDpi);
    }

    /// <summary>
    /// TextBox（EM_SETRECT 側）用の光学補正。EDIT の整形矩形描画は
    /// 100% でも幾何中央だと下寄りに見えるため、常に最低 1px 上げる。
    /// </summary>
    public static int TextBoxOpticalNudge(Control? c = null)
        => Math.Max(1, OpticalNudge(c));

    /// <summary>中央寄せ可能な単一行風 TextBox として初期化する。</summary>
    public static void Configure(TextBox textBox)
    {
        textBox.Multiline = true;
        textBox.AcceptsReturn = false;
        textBox.AcceptsTab = false;
        textBox.WordWrap = false;
        textBox.HandleCreated += (_, _) => Apply(textBox);
        textBox.SizeChanged += (_, _) => Apply(textBox);
        textBox.FontChanged += (_, _) => Apply(textBox);
        textBox.TextChanged += (_, _) => Apply(textBox);
    }

    public static void Apply(TextBox textBox)
    {
        if (!textBox.IsHandleCreated || textBox.IsDisposed)
        {
            return;
        }

        var client = textBox.ClientSize;
        if (client.Width <= 0 || client.Height <= 0)
        {
            return;
        }

        // EDIT が実際に使う行高（GDI TextMetrics）で中央を取る。
        // GDI+ の Font.Height を使うと行高不足で EM_SETRECT の Top が無視されることがある。
        var cellHeight = MeasureGdiLineHeight(textBox, textBox.Font);
        var lineHeight = Math.Min(client.Height, cellHeight);
        var topInset = Math.Clamp(
            (client.Height - lineHeight) / 2 - TextBoxOpticalNudge(textBox),
            0,
            Math.Max(0, client.Height - lineHeight));

        var sideInset = DesignMetrics.Px(5, textBox);
        var rect = new NativeRect
        {
            Left = sideInset,
            Top = topInset,
            // 下端は詰めない（行高より低い矩形は EDIT に再計算され Top が効かなくなる）。
            Right = Math.Max(sideInset, client.Width - sideInset),
            Bottom = client.Height,
        };
        _ = SendMessage(textBox.Handle, EmSetRect, IntPtr.Zero, ref rect);
        textBox.Invalidate();
    }

    /// <summary>
    /// GDI（EDIT の描画系）でのフォント行高を返す。失敗時は Font.Height。
    /// </summary>
    public static int MeasureGdiLineHeight(Control control, Font? font)
    {
        font ??= control.Font;
        var fallback = Math.Max(1, font.Height);

        IntPtr hFont = IntPtr.Zero;
        try
        {
            using var g = control.CreateGraphics();
            var hdc = g.GetHdc();
            try
            {
                hFont = font.ToHfont();
                var previous = SelectObject(hdc, hFont);
                if (previous == IntPtr.Zero || previous == GdiError)
                {
                    return fallback;
                }

                var ok = GetTextMetrics(hdc, out var tm);
                _ = SelectObject(hdc, previous);
                return ok && tm.tmHeight > 0 ? tm.tmHeight : fallback;
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }
        catch
        {
            return fallback;
        }
        finally
        {
            if (hFont != IntPtr.Zero)
            {
                _ = DeleteObject(hFont);
            }
        }
    }
}
