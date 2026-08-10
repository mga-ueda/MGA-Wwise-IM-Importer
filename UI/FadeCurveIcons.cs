using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MgaWwiseIMImporter.Wave;

namespace MgaWwiseIMImporter.UI;

/// <summary>フェードカーブのメニュー用アイコンと選択メニュー。</summary>
internal static class FadeCurveIcons
{
    public const int IconSize = 18;
    /// <summary>メニューアイコン左側の余白。やや広めだったため 6→3 に削減。</summary>
    public const int LeftMargin = 3;

    /// <summary>描画幅（leftMargin は含めない）。メニューでは WidthFor+leftMargin を Image 幅にする。</summary>
    public static int WidthFor(int pixelSize) => Math.Max(8, pixelSize);

    public static ImageSource Create(
        RegionFadeCurveKind kind,
        bool isFadeIn,
        bool selected,
        int pixelSize = IconSize,
        int leftMargin = LeftMargin)
    {
        var size = Math.Max(8, pixelSize);
        var width = WidthFor(size);
        var margin = Math.Max(0, leftMargin);
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            // 描画境界を size×width で固定し、Image 内で上寄せに見えないようにする。
            dc.DrawRectangle(
                Brushes.Transparent,
                null,
                new Rect(0, 0, margin + width, size));

            var pen = new Pen(new SolidColorBrush(Color.FromArgb(220, 220, 220, 220)), 1.4)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            // カーブ左右の内側余白もわずかに詰める（1.5→1）。
            const double insetX = 1d;
            var points = new Point[17];
            for (var i = 0; i < points.Length; i++)
            {
                var t = i / (double)(points.Length - 1);
                var rising = IconRising(kind, t);
                var yGain = isFadeIn ? rising : 1d - rising;
                points[i] = new Point(
                    margin + insetX + t * (width - insetX * 2),
                    size - 2 - yGain * (size - 4));
            }

            for (var i = 0; i < points.Length - 1; i++)
            {
                dc.DrawLine(pen, points[i], points[i + 1]);
            }

            if (selected)
            {
                // 選択枠は AccentCyan（ホバー／選択アクセントと統一）
                var selectPen = new Pen(UiColors.Brush(UiColors.AccentCyan), 1);
                if (selectPen.CanFreeze)
                {
                    selectPen.Freeze();
                }

                dc.DrawRectangle(null, selectPen, new Rect(margin, 0, width - 1, size - 1));
            }
        }

        drawing.Freeze();
        return new DrawingImage(drawing);
    }

    private static double IconRising(RegionFadeCurveKind kind, double t)
    {
        var once = RegionEdgeFade.EvaluateRising(kind, t);
        return kind is RegionFadeCurveKind.SCurve or RegionFadeCurveKind.InvertedSCurve
            ? RegionEdgeFade.EvaluateRising(kind, once)
            : once;
    }

    public static ContextMenu CreateDarkMenu()
    {
        var menu = new ContextMenu
        {
            Background = UiColors.Brush(UiColors.ForControlBack(UiColors.SurfaceBack)),
            Foreground = UiColors.Brush(UiColors.PrimaryFore),
            BorderBrush = UiColors.Brush(UiColors.ChromeBorder),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(1),
        };
        return menu;
    }

    public static void AddCurveChoices(
        ItemCollection items,
        RegionFadeCurveKind current,
        bool isFadeIn,
        Action<RegionFadeCurveKind> onSelected,
        int iconSize = IconSize,
        int leftMargin = LeftMargin)
    {
        var order = isFadeIn
            ? RegionEdgeFade.MenuOrderFadeIn
            : RegionEdgeFade.MenuOrderFadeOut;
        foreach (var kind in order)
        {
            var captured = kind;
            var item = new MenuItem
            {
                Header = UiStrings.LabelRegionFadeCurve(kind),
                Tag = kind,
                Foreground = UiColors.Brush(UiColors.PrimaryFore),
                Background = Brushes.Transparent,
                Icon = new Image
                {
                    Source = Create(kind, isFadeIn, selected: kind == current, iconSize, leftMargin),
                    Width = WidthFor(iconSize) + leftMargin,
                    Height = iconSize,
                    Stretch = Stretch.Uniform,
                },
                // アイコン↔文言の左右余白を少し詰める
                Padding = new Thickness(1, 1, 1, 1),
            };
            item.Click += (_, _) => onSelected(captured);
            items.Add(item);
        }
    }

    public static ContextMenu ShowPicker(
        FrameworkElement owner,
        Point clientLocation,
        RegionFadeCurveKind current,
        bool isFadeIn,
        Action<RegionFadeCurveKind> onSelected,
        ref ContextMenu? menuSlot)
    {
        if (menuSlot is not null)
        {
            menuSlot.IsOpen = false;
        }

        var menu = CreateDarkMenu();
        menuSlot = menu;
        AddCurveChoices(menu.Items, current, isFadeIn, onSelected);

        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.RelativePoint;
        menu.HorizontalOffset = clientLocation.X;
        menu.VerticalOffset = clientLocation.Y;
        menu.IsOpen = true;
        return menu;
    }
}
