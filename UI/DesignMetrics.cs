namespace MgaWwiseIMImporter.UI;

/// <summary>
/// レイアウト設計 DPI（150% = 144）基準のピクセルを、現在の DPI へ換算する。
/// 高さ・余白をフォント（pt）と同じ比率で伸ばし、100% / 150% で見た目の比率を揃える。
/// </summary>
internal static class DesignMetrics
{
    public const float DesignDpi = 144f;

    /// <summary>DEBUG シミュレート用。0 以下なら DeviceDpi を使う。</summary>
    public static int LayoutDpiOverride { get; set; }

    public static int LayoutDpi(Control? c = null)
    {
        if (LayoutDpiOverride > 0)
        {
            return LayoutDpiOverride;
        }

        var dpi = c?.DeviceDpi ?? 0;
        return dpi > 0 ? dpi : 96;
    }

    public static int Px(int designPx, Control? c = null)
        => Px(designPx, LayoutDpi(c));

    public static int Px(int designPx, int layoutDpi)
    {
        if (layoutDpi <= 0)
        {
            layoutDpi = 96;
        }

        return Math.Max(1, (int)Math.Round(designPx * layoutDpi / DesignDpi));
    }

    public static float PxF(float designPx, Control? c = null)
    {
        var layoutDpi = LayoutDpi(c);
        if (layoutDpi <= 0)
        {
            layoutDpi = 96;
        }

        return designPx * layoutDpi / DesignDpi;
    }

    /// <summary>
    /// 旧 96 DPI 基準の論理 px を、150% 設計値へ換算してから <see cref="Px"/> する。
    /// <c>value96 * DeviceDpi / 96</c> と同じ実 DPI 結果になり、DEBUG の LayoutDpiOverride にも追従する。
    /// </summary>
    public static int From96(int value96, Control? c = null)
        => Px((int)Math.Round(value96 * DesignDpi / 96f), c);

    public static float From96F(float value96, Control? c = null)
        => PxF(value96 * DesignDpi / 96f, c);

    /// <summary>
    /// Yu Gothic など視覚上寄りの補正（150% 設計 1.5）。
    /// 生ピクセル固定ではなく LayoutDpi に連動させる。
    /// </summary>
    public static float VisualTextNudgeY(Control? c = null) => PxF(1.5f, c);

    public static int VisualTextNudgeYInt(Control? c = null)
        => Math.Max(0, (int)Math.Round(VisualTextNudgeY(c)));

    public static Padding Pad(int all, Control? c = null)
        => new(Px(all, c));

    public static Padding Pad(int left, int top, int right, int bottom, Control? c = null)
        => new(Px(left, c), Px(top, c), Px(right, c), Px(bottom, c));
}
