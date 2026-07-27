using System.Drawing.Drawing2D;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// ユーザーマニュアルを開くボタン（歯車の左）。「?」を描画し、薄い枠付きの正方形。
/// </summary>
internal sealed class ManualHelpButton : SquareToolbarButton
{
    public ManualHelpButton()
    {
        Margin = new Padding(0, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public void RefreshAppearance()
    {
        AccessibleName = UiStrings.AccessibleManualHelpButton;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        PaintFillBackground(g);
        DrawQuestion(g, ForeColor);
    }

    private void DrawQuestion(Graphics g, Color color)
    {
        var side = Math.Min(Width, Height);
        using var font = new Font("Segoe UI Semibold", Math.Max(9f, side * 0.52f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
        };
        // MeasureString の余白補正を入れず、描画矩形の幾何中心へ厳密に合わせる。
        g.DrawString("?", font, brush, ClientRectangle, format);
    }
}
