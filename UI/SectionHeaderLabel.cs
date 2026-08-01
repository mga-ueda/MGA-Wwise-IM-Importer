namespace MgaWwiseIMImporter.UI;

/// <summary>
/// セクション見出しラベル。行の中へ上下左右にマージンを取った
/// 一段低いグレー帯（BarColor）を描き、隣接する列の帯どうしが接しないようにする。
/// </summary>
internal sealed class SectionHeaderLabel : Label
{
    private Color _barColor = UiColors.SectionHeaderBack;
    private int _barMarginTop = 3;
    private int _barMarginBottom = 7;
    private int _barRightInsetExtra;

    /// <summary>見出し帯の塗り色。周囲は BackColor で塗られる。</summary>
    public Color BarColor
    {
        get => _barColor;
        set
        {
            if (_barColor == value)
            {
                return;
            }

            _barColor = value;
            Invalidate();
        }
    }

    /// <summary>帯上側の余白（150% 設計 px。描画時に DesignMetrics で換算）。</summary>
    public int BarMarginTop
    {
        get => _barMarginTop;
        set
        {
            var next = Math.Max(0, value);
            if (_barMarginTop == next)
            {
                return;
            }

            _barMarginTop = next;
            Invalidate();
        }
    }

    /// <summary>帯下側の余白（150% 設計 px。描画時に DesignMetrics で換算）。</summary>
    public int BarMarginBottom
    {
        get => _barMarginBottom;
        set
        {
            var next = Math.Max(0, value);
            if (_barMarginBottom == next)
            {
                return;
            }

            _barMarginBottom = next;
            Invalidate();
        }
    }

    /// <summary>帯右端をさらに内側へ寄せる量（デバイス px）。</summary>
    public int BarRightInsetExtra
    {
        get => _barRightInsetExtra;
        set
        {
            var next = Math.Max(0, value);
            if (_barRightInsetExtra == next)
            {
                return;
            }

            _barRightInsetExtra = next;
            Invalidate();
        }
    }

    public SectionHeaderLabel()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>見出し帯（BarColor）の描画矩形。</summary>
    public Rectangle GetBarBounds()
    {
        // BarMargin* は従来 96dpi 基準で渡されるため From96。左右の 3 も同様。
        var marginLeft = DesignMetrics.From96(3, this);
        var marginRight = DesignMetrics.From96(3, this) + _barRightInsetExtra;
        var marginTop = DesignMetrics.From96(_barMarginTop, this);
        var marginBottom = DesignMetrics.From96(_barMarginBottom, this);
        return new Rectangle(
            marginLeft,
            marginTop,
            Math.Max(0, Width - marginLeft - marginRight),
            Math.Max(0, Height - marginTop - marginBottom));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);

        var bar = GetBarBounds();
        using (var brush = new SolidBrush(_barColor))
        {
            e.Graphics.FillRectangle(brush, bar);
        }

        // テキスト位置は Padding 基準（下の選択肢と左端を揃える）。帯右端は超えない。
        var textBounds = Rectangle.FromLTRB(
            Padding.Left,
            bar.Top,
            Math.Max(Padding.Left, bar.Right),
            bar.Bottom);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            Enabled ? ForeColor : UiColors.ActionButtonDisabledFore,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.NoPadding
            | TextFormatFlags.SingleLine);
    }
}
