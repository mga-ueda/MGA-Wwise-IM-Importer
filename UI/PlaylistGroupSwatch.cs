using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// Playlist 行の左外側に置くグループ塗り分け用の四角枠。
/// Form1 同様、色はフィールド保持＋即時ビジュアル更新（OnRender / DP に頼らない）。
/// </summary>
internal sealed class PlaylistGroupSwatch : Border
{
    // WinForms OnPaint の BoxSize=12 / Width=16 はデバイス px 固定。
    public static double BoxSize => DesignMetrics.Dip(12);
    public static double ControlWidth => DesignMetrics.Dip(16);

    private readonly Border _box;
    private Color? _groupColor;

    public PlaylistGroupSwatch()
    {
        Width = ControlWidth;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Cursor = Cursors.Hand;
        Focusable = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        _box = new Border
        {
            Width = BoxSize,
            Height = BoxSize,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        Child = _box;
        ApplyGroupColorVisual();
    }

    /// <summary>グループ枠の塗り色。null のときは空枠のみ表示する。</summary>
    public Color? Fill
    {
        get => _groupColor;
        set
        {
            // 値が同じでもブラシを載せ直す（ドラッグ開始直後の1つ目が描画されない対策）。
            _groupColor = value;
            ApplyGroupColorVisual();
        }
    }

    /// <summary>互換用。Fill の別名（Form1 GroupColor）。</summary>
    public Color? GroupColor
    {
        get => Fill;
        set => Fill = value;
    }

    private void ApplyGroupColorVisual()
    {
        if (_groupColor is Color fillColor)
        {
            _box.Background = WpfControlHelpers.FrozenBrush(fillColor);
            _box.BorderBrush = WpfControlHelpers.FrozenBrush(WpfControlHelpers.DarkenBorder(fillColor));
        }
        else
        {
            _box.Background = Brushes.Transparent;
            _box.BorderBrush = UiColors.Brush(UiColors.PlaylistButtonBorder);
        }
    }
}
