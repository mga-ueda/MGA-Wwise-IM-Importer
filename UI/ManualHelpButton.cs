using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// ユーザーマニュアルを開くボタン（歯車の左）。「?」を描画し、薄い枠付きの正方形。
/// </summary>
internal sealed class ManualHelpButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public ManualHelpButton()
    {
        AccessibleRole = AccessibleRole.PushButton;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(24, 24);
        Margin = new Padding(0, 0, 4, 0);
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
        ApplyColors();
        RefreshAppearance();
    }

    public Color HoverBackColor { get; set; }
    public Color PressedBackColor { get; set; }
    public Color BorderColor { get; set; }

    public void RefreshAppearance()
    {
        AccessibleName = UiStrings.AccessibleManualHelpButton;
        Invalidate();
    }

    public void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.ProjectBarBack);
        ForeColor = UiColors.LogButtonFore;
        HoverBackColor = UiColors.ForControlBack(UiColors.TransportHoverBack);
        PressedBackColor = UiColors.ForControlBack(UiColors.TransportPressedBack);
        BorderColor = UiColors.ForControlBack(UiColors.ChromeBorder);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        var fill = _pressed
            ? PressedBackColor
            : _hovered
                ? HoverBackColor
                : BackColor;
        using (var fillBrush = new SolidBrush(fill))
        {
            g.FillRectangle(fillBrush, ClientRectangle);
        }

        DrawQuestion(g, ForeColor);
    }

    private void DrawQuestion(Graphics g, Color color)
    {
        var side = Math.Min(Width, Height);
        using var font = new Font("Segoe UI Semibold", Math.Max(9f, side * 0.52f), FontStyle.Bold, GraphicsUnit.Pixel);
        var text = "?";
        var size = g.MeasureString(text, font);
        var x = (Width - size.Width) / 2f;
        var y = (Height - size.Height) / 2f - side * 0.04f;
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y);
    }
}
