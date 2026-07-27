using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// Tips 枠表示のオン／オフ切替（歯車の左）。吹き出しを描画し、オフ時はグレーアウトする。
/// 見た目は <see cref="SettingsGearButton"/> と揃えた薄い枠付きの正方形。
/// 自身の Tips は全体オフ時も常に表示する。
/// </summary>
internal sealed class TipsToggleButton : SquareToolbarButton
{
    private bool _checked = true;

    public TipsToggleButton()
    {
        Margin = new Padding(0, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

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

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        PaintFillBackground(g);

        // オフ時はアイコンをグレーアウトするだけ。
        var fill = ResolveFillColor();
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
