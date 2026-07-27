using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 音声出力設定（言語切替の左）。歯車を描画し、薄い枠付きの正方形。
/// </summary>
internal sealed class SettingsGearButton : SquareToolbarButton
{
    public SettingsGearButton()
    {
        Margin = new Padding(8, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public void RefreshAppearance()
    {
        AccessibleName = UiStrings.AccessibleAudioSettingsButton;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        PaintFillBackground(g);
        DrawGear(g, ForeColor, ResolveFillColor());
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
