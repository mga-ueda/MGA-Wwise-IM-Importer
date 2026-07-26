using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>フェードカーブのメニュー用アイコンと選択メニュー。</summary>
internal static class FadeCurveIcons
{
    public const int IconSize = 18;
    public const int LeftMargin = 6;

    public static Image Create(
        RegionFadeCurveKind kind,
        bool isFadeIn,
        bool selected,
        int pixelSize = IconSize,
        int leftMargin = LeftMargin)
    {
        var size = Math.Max(8, pixelSize);
        var margin = Math.Max(0, leftMargin);
        var bmp = new Bitmap(
            size + margin,
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
            var yGain = isFadeIn
                ? RegionEdgeFade.EvaluateRising(kind, t)
                : RegionEdgeFade.EvaluateFalling(kind, t);
            points[i] = new PointF(
                margin + 1.5f + (float)t * (size - 3f),
                size - 2f - (float)(yGain * (size - 4f)));
        }

        g.DrawLines(pen, points);

        if (selected)
        {
            using var selectPen = new Pen(Color.FromArgb(80, 170, 255), 1f);
            g.DrawRectangle(selectPen, margin, 0, size - 1, size - 1);
        }

        return bmp;
    }

    /// <summary>暗いコンテキストメニュー（歯車／カーブ選択共通）。</summary>
    public static ContextMenuStrip CreateDarkMenu() => new()
    {
        ShowImageMargin = true,
        BackColor = UiColors.ForControlBack(UiColors.SurfaceBack),
        ForeColor = UiColors.PrimaryFore,
        Renderer = new DarkFadeCurveMenuRenderer(),
        Padding = new Padding(2, 2, 2, 2),
        ImageScalingSize = new Size(IconSize + LeftMargin, IconSize),
    };

    /// <summary>カーブ一覧をメニュー項目へ追加する。</summary>
    public static void AddCurveChoices(
        ToolStripItemCollection items,
        RegionFadeCurveKind current,
        bool isFadeIn,
        Action<RegionFadeCurveKind> onSelected)
    {
        var order = isFadeIn
            ? RegionEdgeFade.MenuOrderFadeIn
            : RegionEdgeFade.MenuOrderFadeOut;
        foreach (var kind in order)
        {
            var item = new ToolStripMenuItem(UiStrings.LabelRegionFadeCurve(kind))
            {
                Tag = kind,
                Image = Create(kind, isFadeIn, selected: kind == current),
                ImageScaling = ToolStripItemImageScaling.None,
                Padding = new Padding(2, 1, 2, 1),
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
        var menu = CreateDarkMenu();
        menuSlot = menu;
        AddCurveChoices(menu.Items, current, isFadeIn, onSelected);

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
        private const int TextRightGapPx = 8;

        public DarkFadeCurveMenuRenderer()
            : base(new DarkFadeCurveColorTable())
        {
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var r = e.TextRectangle;
            e.TextRectangle = new Rectangle(
                r.X - TextRightGapPx,
                r.Y,
                r.Width + TextRightGapPx,
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
