using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>フェードカーブのメニュー用アイコンと選択メニュー。</summary>
internal static class FadeCurveIcons
{
    public const int IconSize = 18;
    public const int LeftMargin = 6;

    /// <summary>アイコンは正方形（高さ＝幅）。</summary>
    public static int WidthFor(int pixelSize) => Math.Max(8, pixelSize);

    public static Image Create(
        RegionFadeCurveKind kind,
        bool isFadeIn,
        bool selected,
        int pixelSize = IconSize,
        int leftMargin = LeftMargin)
    {
        var size = Math.Max(8, pixelSize);
        var width = WidthFor(size);
        var margin = Math.Max(0, leftMargin);
        var bmp = new Bitmap(
            width + margin,
            size,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.FromArgb(220, 220, 220), 1.4f);
        var points = new PointF[17];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (double)(points.Length - 1);
            var rising = IconRising(kind, t);
            var yGain = isFadeIn ? rising : 1d - rising;
            points[i] = new PointF(
                margin + 1.5f + (float)t * (width - 3f),
                size - 2f - (float)(yGain * (size - 4f)));
        }

        g.DrawLines(pen, points);

        if (selected)
        {
            using var selectPen = new Pen(Color.FromArgb(80, 170, 255), 1f);
            g.DrawRectangle(selectPen, margin, 0, width - 1, size - 1);
        }

        return bmp;
    }

    /// <summary>
    /// アイコン描画専用の立ち上がり形状。実フェード演算（<see cref="RegionEdgeFade"/>）は変えず、
    /// S / Inverted S のみ同型カーブを二重適用して曲率を誇張する
    /// （実式の smoothstep は小さなアイコンではほぼ直線に見えるため）。
    /// </summary>
    private static double IconRising(RegionFadeCurveKind kind, double t)
    {
        var once = RegionEdgeFade.EvaluateRising(kind, t);
        return kind is RegionFadeCurveKind.SCurve or RegionFadeCurveKind.InvertedSCurve
            ? RegionEdgeFade.EvaluateRising(kind, once)
            : once;
    }

    /// <summary>暗いコンテキストメニュー（歯車／カーブ選択共通）。</summary>
    public static ContextMenuStrip CreateDarkMenu(Control? scaleRef = null)
    {
        // IconSize / LeftMargin は 150% 設計値。メインフォーム同様 DesignMetrics で換算する
        // （固定だと 100% で行が間延びして密度が落ちる）。
        var icon = DesignMetrics.Px(IconSize, scaleRef);
        var margin = DesignMetrics.Px(LeftMargin, scaleRef);
        return new ContextMenuStrip
        {
            ShowImageMargin = true,
            BackColor = UiColors.ForControlBack(UiColors.SurfaceBack),
            ForeColor = UiColors.PrimaryFore,
            Renderer = new DarkFadeCurveMenuRenderer(),
            Padding = DesignMetrics.Pad(2, scaleRef),
            ImageScalingSize = new Size(WidthFor(icon) + margin, icon),
        };
    }

    /// <summary>カーブ一覧をメニュー項目へ追加する。</summary>
    public static void AddCurveChoices(
        ToolStripItemCollection items,
        RegionFadeCurveKind current,
        bool isFadeIn,
        Action<RegionFadeCurveKind> onSelected,
        Control? scaleRef = null)
    {
        var iconSize = DesignMetrics.Px(IconSize, scaleRef);
        var leftMargin = DesignMetrics.Px(LeftMargin, scaleRef);
        var padding = DesignMetrics.Pad(2, 1, 2, 1, scaleRef);
        var order = isFadeIn
            ? RegionEdgeFade.MenuOrderFadeIn
            : RegionEdgeFade.MenuOrderFadeOut;
        foreach (var kind in order)
        {
            var item = new ToolStripMenuItem(UiStrings.LabelRegionFadeCurve(kind))
            {
                Tag = kind,
                Image = Create(kind, isFadeIn, selected: kind == current, iconSize, leftMargin),
                ImageScaling = ToolStripItemImageScaling.None,
                Padding = padding,
            };
            var captured = kind;
            item.Click += (_, _) => onSelected(captured);
            items.Add(item);
        }
    }

    /// <summary>
    /// フェードカーブ選択メニューを表示する。選択時は <paramref name="onSelected"/> を呼ぶ。
    /// </summary>
    public static ContextMenuStrip ShowPicker(
        Control owner,
        Point clientLocation,
        RegionFadeCurveKind current,
        bool isFadeIn,
        Action<RegionFadeCurveKind> onSelected,
        ref ContextMenuStrip? menuSlot)
    {
        menuSlot?.Dispose();
        var menu = CreateDarkMenu(owner);
        menuSlot = menu;
        AddCurveChoices(menu.Items, current, isFadeIn, onSelected, owner);

        menu.PerformLayout();
        var preferred = menu.PreferredSize;
        int trimRight;
        using (var g = menu.CreateGraphics())
        {
            trimRight = TextRenderer.MeasureText(
                g,
                "MM",
                menu.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
        }

        menu.AutoSize = false;
        menu.Size = new Size(
            Math.Max(preferred.Width - trimRight, 120),
            preferred.Height);
        menu.Show(owner, clientLocation);
        return menu;
    }

    private sealed class DarkFadeCurveMenuRenderer : ToolStripProfessionalRenderer
    {
        /// <summary>テキストをアイコン側へ寄せる量（150% 設計 8）。</summary>
        private const int TextRightGapDesignPx = 8;

        public DarkFadeCurveMenuRenderer()
            : base(new DarkFadeCurveColorTable())
        {
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var gap = DesignMetrics.Px(TextRightGapDesignPx, e.ToolStrip);
            var r = e.TextRectangle;
            e.TextRectangle = new Rectangle(
                r.X - gap,
                r.Y,
                r.Width + gap,
                r.Height);
            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkFadeCurveColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(70, 70, 74);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemBorder => Color.FromArgb(90, 90, 94);
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientBegin => ToolStripDropDownBackground;
        public override Color ImageMarginGradientMiddle => ToolStripDropDownBackground;
        public override Color ImageMarginGradientEnd => ToolStripDropDownBackground;
        public override Color SeparatorDark => Color.FromArgb(60, 60, 64);
        public override Color SeparatorLight => SeparatorDark;
    }
}
