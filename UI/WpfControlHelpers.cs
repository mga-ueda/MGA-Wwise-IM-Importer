using System.Windows;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

internal static class WpfControlHelpers
{
    public static SolidColorBrush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    public static Color BlendColor(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * amount),
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
    }

    public static StreamGeometry RoundedRectGeometry(Rect bounds, double radius)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            AddRoundedRect(ctx, bounds, radius);
        }

        geometry.Freeze();
        return geometry;
    }

    public static void AddRoundedRect(StreamGeometryContext ctx, Rect bounds, double radius)
    {
        radius = Math.Max(0d, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2d));
        if (radius <= 0d)
        {
            ctx.BeginFigure(bounds.TopLeft, isFilled: true, isClosed: true);
            ctx.LineTo(bounds.TopRight, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(bounds.BottomRight, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(bounds.BottomLeft, isStroked: true, isSmoothJoin: false);
            return;
        }

        var x = bounds.X;
        var y = bounds.Y;
        var w = bounds.Width;
        var h = bounds.Height;
        var r = radius;
        ctx.BeginFigure(new Point(x + r, y), isFilled: true, isClosed: true);
        ctx.LineTo(new Point(x + w - r, y), isStroked: true, isSmoothJoin: false);
        ctx.ArcTo(new Point(x + w, y + r), new Size(r, r), 0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
        ctx.LineTo(new Point(x + w, y + h - r), isStroked: true, isSmoothJoin: false);
        ctx.ArcTo(new Point(x + w - r, y + h), new Size(r, r), 0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
        ctx.LineTo(new Point(x + r, y + h), isStroked: true, isSmoothJoin: false);
        ctx.ArcTo(new Point(x, y + h - r), new Size(r, r), 0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
        ctx.LineTo(new Point(x, y + r), isStroked: true, isSmoothJoin: false);
        ctx.ArcTo(new Point(x + r, y), new Size(r, r), 0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
    }

    public static Color DarkenBorder(Color color)
    {
        static byte Darken(byte channel) => (byte)Math.Clamp(channel - 40, 0, 255);
        return Color.FromArgb(color.A, Darken(color.R), Darken(color.G), Darken(color.B));
    }
}
