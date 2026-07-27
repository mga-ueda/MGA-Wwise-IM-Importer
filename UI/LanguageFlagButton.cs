using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 表示言語切替（スペクトラム左）。JP／EN をトランスポート同様に画像描画し、薄い枠付きの正方形。
/// </summary>
internal sealed class LanguageFlagButton : SquareToolbarButton
{
    public LanguageFlagButton()
    {
        Margin = new Padding(8, 0, 4, 0);
        ApplyColors();
        RefreshAppearance();
    }

    public void RefreshAppearance()
    {
        AccessibleName = UiStrings.IsJapanese
            ? UiStrings.LanguageBadgeJapanese
            : UiStrings.LanguageBadgeEnglish;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        // 従来どおり右下 1px を空けた矩形で塗る（見た目を維持）。
        var fill = ResolveFillColor();
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var fillBrush = new SolidBrush(fill))
        {
            g.FillRectangle(fillBrush, bounds);
        }

        var label = UiStrings.IsJapanese
            ? UiStrings.LanguageBadgeJapanese
            : UiStrings.LanguageBadgeEnglish;
        using var font = new Font("Yu Gothic UI", 7.5F, FontStyle.Bold);
        var textSize = TextRenderer.MeasureText(
            g,
            label,
            font,
            Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        var textX = (Width - textSize.Width) / 2;
        var textY = (Height - textSize.Height) / 2;
        TextRenderer.DrawText(
            g,
            label,
            font,
            new Point(textX, textY),
            ForeColor,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
}
