using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 音声出力設定（言語切替の左）。歯車を描画し、薄い枠付きの正方形。
/// </summary>
internal sealed class SettingsGearButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public SettingsGearButton()
    {
        AccessibleRole = AccessibleRole.PushButton;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Size = new Size(24, 24);
        Margin = new Padding(8, 0, 4, 0);
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

    public void RefreshAppearance()
    {
        AccessibleName = UiStrings.AccessibleAudioSettingsButton;
        Invalidate();
    }

    public void ApplyColors()
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

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
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

        DrawGear(g, ForeColor, fill);
    }

    private void DrawGear(Graphics g, Color color, Color holeColor)
    {
        const int teeth = 8;
        var side = Math.Min(Width, Height);
        var cx = Width * 0.5f;
        var cy = Height * 0.5f;
        var outer = side * 0.30f;
        var inner = side * 0.19f;
        var hub = side * 0.09f;
        var points = new PointF[teeth * 4];
        for (var i = 0; i < teeth; i++)
        {
            var baseAngle = (i / (float)teeth) * MathF.PI * 2f - MathF.PI / teeth;
            var step = (MathF.PI * 2f) / teeth;
            points[i * 4] = Polar(cx, cy, inner, baseAngle);
            points[i * 4 + 1] = Polar(cx, cy, outer, baseAngle + step * 0.28f);
            points[i * 4 + 2] = Polar(cx, cy, outer, baseAngle + step * 0.72f);
            points[i * 4 + 3] = Polar(cx, cy, inner, baseAngle + step);
        }

        using (var brush = new SolidBrush(color))
        using (var path = new GraphicsPath())
        {
            path.AddPolygon(points);
            g.FillPath(brush, path);
        }

        using (var holeBrush = new SolidBrush(holeColor))
        {
            g.FillEllipse(holeBrush, cx - hub, cy - hub, hub * 2f, hub * 2f);
        }

        using var ringPen = new Pen(color, Math.Max(1f, side * 0.05f));
        g.DrawEllipse(ringPen, cx - hub * 1.7f, cy - hub * 1.7f, hub * 3.4f, hub * 3.4f);
    }

    private static PointF Polar(float cx, float cy, float radius, float angle) =>
        new(cx + MathF.Cos(angle) * radius, cy + MathF.Sin(angle) * radius);
}
