namespace MgaWwiseIMImporter.UI;

/// <summary>
/// プロジェクトバー上の薄い枠付き正方形ツールバーボタン共通基盤。
/// ホバー／押下の塗りと DPI スケール時の正方形維持を担い、アイコン描画は派生側。
/// </summary>
internal abstract class SquareToolbarButton : Button
{
    private bool _hovered;
    private bool _pressed;

    protected SquareToolbarButton()
    {
        AccessibleRole = AccessibleRole.PushButton;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(24, 24);
        Padding = Padding.Empty;
        TabStop = false;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.Selectable, false);
    }

    public Color HoverBackColor { get; set; }

    public Color PressedBackColor { get; set; }

    protected bool IsHovered => _hovered;

    protected bool IsPressed => _pressed;

    /// <summary>プロジェクトバー向けの既定色を適用する。</summary>
    public virtual void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.ProjectBarBack);
        ForeColor = UiColors.LogButtonFore;
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        Invalidate();
    }

    /// <summary>
    /// <see cref="AutoScaleMode.Font"/> は縦横倍率が異なるため、正方形を維持する。
    /// </summary>
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        var keepSquare = Width == Height;
        base.ScaleControl(factor, specified);
        if (keepSquare && Width != Height)
        {
            var side = Math.Min(Width, Height);
            Size = new Size(side, side);
        }
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
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = e.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    /// <summary>押下／ホバー／通常の塗りつぶし色。</summary>
    protected Color ResolveFillColor() =>
        _pressed
            ? PressedBackColor
            : _hovered
                ? HoverBackColor
                : BackColor;

    /// <summary>背景クリア＋ホバー塗り（ClientRectangle 全体）。</summary>
    protected void PaintFillBackground(Graphics g)
    {
        g.Clear(BackColor);
        using var fillBrush = new SolidBrush(ResolveFillColor());
        g.FillRectangle(fillBrush, ClientRectangle);
    }
}
