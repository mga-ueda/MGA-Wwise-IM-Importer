using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>FlatOptionRadioButton / FlatOptionCheckBox 共通のグリフ配色・DPI 換算。</summary>
internal static class FlatOptionGlyph
{
    /// <summary>ラジオ外円・チェック枠スロット（150% 設計）。</summary>
    public const int LayoutGlyphSizeDesign = 21;

    /// <summary>チェック枠の描画サイズ（150% 設計）。</summary>
    public const int DrawnGlyphSizeDesign = 15;

    public const int GlyphGapDesign = 9;
    public const int TextGapDesign = 11;

    /// <summary>プレイリスト項目と同じ行高（150% 設計）。</summary>
    public const int RowHeightDesign = 30;

    public static Color ResolveBorderColor(bool enabled, bool isChecked, bool hovered)
    {
        if (!enabled)
        {
            return UiColors.OptionGlyphDisabled;
        }

        if (isChecked)
        {
            return UiColors.OptionGlyphChecked;
        }

        return hovered ? UiColors.OptionGlyphHover : UiColors.OptionGlyphBorder;
    }

    public static int RowHeight(Control? c = null) => DesignMetrics.Px(RowHeightDesign, c);

    public static int LayoutGlyphSize(Control? c = null) => DesignMetrics.Px(LayoutGlyphSizeDesign, c);

    public static int DrawnGlyphSize(Control? c = null) => DesignMetrics.Px(DrawnGlyphSizeDesign, c);

    public static int GlyphGap(Control? c = null) => DesignMetrics.Px(GlyphGapDesign, c);

    public static int TextGap(Control? c = null) => DesignMetrics.Px(TextGapDesign, c);
}

internal sealed class FlatOptionRadioButton : RadioButton
{
    /// <summary>プレイリスト項目と同じ行高（150% 設計値。適用時は DesignMetrics で換算）。</summary>
    public const int RowHeightDesign = FlatOptionGlyph.RowHeightDesign;

    public static int GetRowHeight(Control? c = null) => FlatOptionGlyph.RowHeight(c);

    private bool _hovered;

    public FlatOptionRadioButton()
    {
        AutoSize = false;
        Height = FlatOptionGlyph.RowHeight(this);
        Margin = DesignMetrics.Pad(3, 1, 3, 1, this);
        FlatStyle = FlatStyle.Flat;
        TabStop = false;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        // クリックでフォーカスを奪わず、↑↓ 等の波形ショートカットを阻害しない。
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override bool ShowFocusCues => false;

    public void ApplyColors() => Invalidate();

    /// <summary>DPI / シミュレート変更後に行高・余白を再適用する。</summary>
    public void ApplyFixedLayout()
    {
        Height = FlatOptionGlyph.RowHeight(this);
        Margin = DesignMetrics.Pad(3, 1, 3, 1, this);
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var glyph = FlatOptionGlyph.LayoutGlyphSize(this);
        var gap = FlatOptionGlyph.GlyphGap(this);
        var text = TextRenderer.MeasureText(
            Text,
            Font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        return new Size(
            glyph + gap + text.Width + DesignMetrics.Px(3, this),
            FlatOptionGlyph.RowHeight(this));
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        // ランタイム生成のプレイリスト行と行間を揃えるため、縦方向の AutoScale を打ち消し DesignMetrics で再適用。
        ApplyFixedLayout();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var glyphSize = FlatOptionGlyph.LayoutGlyphSize(this);
        var glyph = new RectangleF(
            DesignMetrics.Px(2, this),
            (Height - glyphSize) / 2f,
            glyphSize - 1f,
            glyphSize - 1f);
        var borderColor = ResolveBorderColor();
        var penW = Math.Max(1f, DesignMetrics.From96F(1.4f, this));
        using (var border = new Pen(borderColor, penW))
        {
            g.DrawEllipse(border, glyph);
        }

        if (Checked)
        {
            var inset = DesignMetrics.From96F(4f, this);
            var dot = RectangleF.Inflate(glyph, -inset, -inset);
            using var fill = new SolidBrush(UiColors.OptionGlyphChecked);
            g.FillEllipse(fill, dot);
        }

        DrawText(g, glyphSize);
    }

    private Color ResolveBorderColor() =>
        FlatOptionGlyph.ResolveBorderColor(Enabled, Checked, _hovered);

    private void DrawText(Graphics g, int glyphSize)
    {
        var textLeft = glyphSize + FlatOptionGlyph.TextGap(this);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height),
            Enabled ? ForeColor : UiColors.OptionGlyphDisabled,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.SingleLine);
    }
}

internal sealed class FlatOptionCheckBox : CheckBox
{
    private bool _hovered;

    public FlatOptionCheckBox()
    {
        AutoSize = true;
        FlatStyle = FlatStyle.Flat;
        TabStop = false;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        // クリックでフォーカスを奪わず、↑↓ 等の波形ショートカットを阻害しない。
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override bool ShowFocusCues => false;

    public void ApplyColors() => Invalidate();

    /// <summary>DPI / シミュレート変更後に再描画する（AutoSize は PreferredSize に追従）。</summary>
    public void ApplyFixedLayout()
    {
        if (AutoSize)
        {
            Size = GetPreferredSize(Size.Empty);
        }

        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        // コントロール寸法とテキスト位置は従来どおりに保ち、枠だけを小さく描画する。
        var glyph = FlatOptionGlyph.LayoutGlyphSize(this);
        var gap = FlatOptionGlyph.GlyphGap(this);
        var textFlags = GetTextFormatFlags();
        var text = TextRenderer.MeasureText(
            Text,
            Font,
            // 改行ありは自然な複数行サイズを測る。
            textFlags.HasFlag(TextFormatFlags.SingleLine)
                ? Size.Empty
                : new Size(short.MaxValue, short.MaxValue),
            textFlags);
        return new Size(
            Padding.Horizontal + glyph + gap + text.Width + DesignMetrics.Px(3, this),
            Padding.Vertical + Math.Max(glyph, text.Height) + DesignMetrics.Px(6, this));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var glyphSlotSize = FlatOptionGlyph.LayoutGlyphSize(this);
        var glyphSize = FlatOptionGlyph.DrawnGlyphSize(this);
        var glyph = new RectangleF(
            Padding.Left + DesignMetrics.Px(2, this) + (glyphSlotSize - glyphSize) / 2f,
            (Height - glyphSize) / 2f,
            glyphSize - 1f,
            glyphSize - 1f);
        var borderColor = ResolveBorderColor();
        var penW = Math.Max(1f, DesignMetrics.From96F(1.4f, this));
        if (Checked)
        {
            using var fill = new SolidBrush(Enabled
                ? UiColors.OptionGlyphChecked
                : UiColors.OptionGlyphDisabled);
            g.FillRectangle(fill, glyph);
        }

        using (var border = new Pen(borderColor, penW))
        {
            g.DrawRectangle(border, glyph.X, glyph.Y, glyph.Width, glyph.Height);
        }

        if (Checked)
        {
            using var check = new Pen(
                UiColors.OptionGlyphCheckMark,
                Math.Max(1f, DesignMetrics.From96F(1.8f, this)))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };
            g.DrawLines(check,
            [
                new PointF(glyph.Left + glyph.Width * 0.22f, glyph.Top + glyph.Height * 0.52f),
                new PointF(glyph.Left + glyph.Width * 0.43f, glyph.Top + glyph.Height * 0.73f),
                new PointF(glyph.Left + glyph.Width * 0.80f, glyph.Top + glyph.Height * 0.29f),
            ]);
        }

        var textLeft = Padding.Left + glyphSlotSize + FlatOptionGlyph.TextGap(this);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            new Rectangle(textLeft, 0, Math.Max(0, Width - textLeft), Height),
            Enabled ? ForeColor : UiColors.OptionGlyphDisabled,
            GetTextFormatFlags());
    }

    private TextFormatFlags GetTextFormatFlags()
    {
        var flags = TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding
            | TextFormatFlags.NoPrefix;
        if (Text.Contains('\n', StringComparison.Ordinal)
            || Text.Contains('\r', StringComparison.Ordinal))
        {
            return flags;
        }

        return flags | TextFormatFlags.SingleLine;
    }

    private Color ResolveBorderColor() =>
        FlatOptionGlyph.ResolveBorderColor(Enabled, Checked, _hovered);
}
