using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// Tips 枠表示のオン／オフ切替（歯車の左）。吹き出しを描画し、オフ時はグレーアウトする。
/// 見た目は <see cref="SettingsGearButton"/> と揃えた薄い枠付きの正方形。
/// 自身の Tips は全体オフ時も常に表示する。
/// </summary>
internal sealed class TipsToggleButton : Button
{
    private bool _hovered;
    private bool _pressed;
    private bool _checked = true;

    public TipsToggleButton()
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

    /// <summary>Tips 枠表示が有効なら true。</summary>
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            RefreshAppearance();
        }
    }

    public void RefreshAppearance()
    {
        AccessibleName = UiStrings.AccessibleTipsToggleButton;
        TipService.Set(this, UiStrings.TipTipsToggle, respectsEnabled: false);
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

        // オフ時はアイコンをグレーアウトするだけ。
        var iconColor = _checked
            ? ForeColor
            : Color.FromArgb(128, ForeColor);
        DrawBalloon(g, iconColor, fill);
    }

    private void DrawBalloon(Graphics g, Color color, Color holeColor)
    {
        var side = Math.Min(Width, Height);
        var w = side * 0.62f;
        var h = side * 0.42f;
        var x = (Width - w) / 2f;
        var y = Height * 0.24f;
        var radius = h * 0.36f;

        using var path = new GraphicsPath();
        AddRoundedRect(path, x, y, w, h, radius);
        // 吹き出しのしっぽ（左下）
        var tailTopX = x + w * 0.28f;
        path.AddPolygon(
        [
            new PointF(tailTopX, y + h - 1f),
            new PointF(tailTopX + w * 0.18f, y + h - 1f),
            new PointF(tailTopX, y + h + side * 0.14f),
        ]);

        using (var brush = new SolidBrush(color))
        {
            g.FillPath(brush, path);
        }

        // 本文のドット（背景色で抜く）
        using var holeBrush = new SolidBrush(holeColor);
        var dot = Math.Max(1.5f, side * 0.06f);
        var dotY = y + h / 2f - dot / 2f;
        for (var i = 0; i < 3; i++)
        {
            var dotX = x + w * (0.26f + 0.24f * i) - dot / 2f;
            g.FillEllipse(holeBrush, dotX, dotY, dot, dot);
        }
    }

    private static void AddRoundedRect(GraphicsPath path, float x, float y, float w, float h, float r)
    {
        var d = r * 2f;
        path.StartFigure();
        path.AddArc(x, y, d, d, 180f, 90f);
        path.AddArc(x + w - d, y, d, d, 270f, 90f);
        path.AddArc(x + w - d, y + h - d, d, d, 0f, 90f);
        path.AddArc(x, y + h - d, d, d, 90f, 90f);
        path.CloseFigure();
    }
}
