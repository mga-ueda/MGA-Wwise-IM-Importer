namespace MgaWwiseIMImporter.UI;

/// <summary>
/// クリック可能な表示部品（フェードカーブアイコン等）のホバー／押下 BackColor 配線。
/// </summary>
internal static class ControlHoverChrome
{
    /// <summary>
    /// マウス状態に応じて <paramref name="control"/>.BackColor を切り替える。
    /// ホバー／押下はトランスポート共通色。色はイベント時点で再評価する（テーマ変更に追従）。
    /// </summary>
    public static void WireBackColor(Control control, Func<Color> getIdle)
    {
        static Color Hover() => UiColors.ForControlBack(UiColors.TransportHoverBack);
        static Color Pressed() => UiColors.ForControlBack(UiColors.TransportPressedBack);

        control.MouseEnter += (_, _) => control.BackColor = Hover();
        control.MouseLeave += (_, _) => control.BackColor = getIdle();
        control.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                control.BackColor = Pressed();
            }
        };
        control.MouseUp += (_, _) =>
        {
            var local = control.PointToClient(Control.MousePosition);
            control.BackColor = control.ClientRectangle.Contains(local) ? Hover() : getIdle();
        };
    }
}
