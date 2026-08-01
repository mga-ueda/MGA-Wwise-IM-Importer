using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// DropDownList 専用のダークテーマ ComboBox。
/// <see cref="DarkProjectComboBox"/> と同じ枠・矢印・一覧の見た目にする。
/// </summary>
internal sealed class DarkDropDownComboBox : ComboBox
{
    private const int WmEraseBkgnd = 0x0014;
    private const int WmPaint = 0x000F;
    private const int WmNcPaint = 0x0085;

    private bool _hovered;
    private DropDownBorderWindow? _dropDownBorderWindow;

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hWnd, out PaintStruct lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hWnd, ref PaintStruct lpPaint);

    [DllImport("user32.dll")]
    private static extern bool GetComboBoxInfo(IntPtr hWnd, ref ComboBoxInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr Hdc;
        public int FErase;
        public NativeRect RcPaint;
        public int FRestore;
        public int FIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] RgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ComboBoxInfo
    {
        public int CbSize;
        public NativeRect RcItem;
        public NativeRect RcButton;
        public int StateButton;
        public IntPtr HwndCombo;
        public IntPtr HwndItem;
        public IntPtr HwndList;
    }

    public DarkDropDownComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        IntegralHeight = false;
        ApplyListItemHeight();
        MaxDropDownItems = 12;
        ApplyColors();
    }

    /// <summary>ドロップダウン各行の高さ（フォント＋薄い余白）。</summary>
    public void ApplyListItemHeight()
    {
        ItemHeight = Math.Max(1, Font.Height + DesignMetrics.Px(6, this));
    }

    public void ApplyColors()
    {
        BackColor = UiColors.ForControlBack(UiColors.ProjectBarInputBack);
        ForeColor = UiColors.ProjectBarInputFore;
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyListItemHeight();
        _ = SetWindowTheme(Handle, "DarkMode_CFD", null);
    }

    protected override void OnDropDown(EventArgs e)
    {
        AttachDropDownBorder();
        Invalidate();
        base.OnDropDown(e);
    }

    protected override void OnDropDownClosed(EventArgs e)
    {
        _dropDownBorderWindow?.ReleaseHandle();
        _dropDownBorderWindow = null;
        Invalidate();
        base.OnDropDownClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dropDownBorderWindow?.ReleaseHandle();
            _dropDownBorderWindow = null;
        }

        base.Dispose(disposing);
    }

    private void AttachDropDownBorder()
    {
        var info = new ComboBoxInfo { CbSize = Marshal.SizeOf<ComboBoxInfo>() };
        if (!GetComboBoxInfo(Handle, ref info) || info.HwndList == IntPtr.Zero)
        {
            return;
        }

        _dropDownBorderWindow?.ReleaseHandle();
        _dropDownBorderWindow = new DropDownBorderWindow(info.HwndList);
        PaintWindowBorder(info.HwndList);
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        var itemText = GetItemText(Items[e.Index]);
        var back = selected
            ? UiColors.ForControlBack(UiColors.TransportHoverBack)
            : UiColors.ForControlBack(UiColors.ProjectBarInputBack);
        var fore = UiColors.ProjectBarInputFore;

        using var backBrush = new SolidBrush(back);
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        var textBounds = new Rectangle(
            e.Bounds.Left + 8,
            e.Bounds.Top,
            Math.Max(0, e.Bounds.Width - 16),
            e.Bounds.Height);
        TextRenderer.DrawText(
            e.Graphics,
            itemText,
            Font,
            textBounds,
            fore,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
    }

    private static void PaintWindowBorder(IntPtr handle)
    {
        if (!GetWindowRect(handle, out var rect))
        {
            return;
        }

        var hdc = GetWindowDC(handle);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            using var g = Graphics.FromHdc(hdc);
            using var pen = new Pen(UiColors.ProjectBarBorder);
            g.DrawRectangle(pen, 0, 0, rect.Right - rect.Left - 1, rect.Bottom - rect.Top - 1);
        }
        finally
        {
            _ = ReleaseDC(handle, hdc);
        }
    }

    private sealed class DropDownBorderWindow : NativeWindow
    {
        public DropDownBorderWindow(IntPtr handle)
        {
            AssignHandle(handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is WmNcPaint or WmPaint)
            {
                PaintWindowBorder(Handle);
            }
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmPaint:
                HandlePaint(ref m);
                return;
            case WmNcPaint:
                m.Result = IntPtr.Zero;
                return;
            case WmEraseBkgnd:
                m.Result = (IntPtr)1;
                return;
        }

        base.WndProc(ref m);
    }

    private void HandlePaint(ref Message m)
    {
        var hdc = BeginPaint(m.HWnd, out var ps);
        try
        {
            using var g = Graphics.FromHdc(hdc);
            DrawFlatChrome(g);
        }
        finally
        {
            EndPaint(m.HWnd, ref ps);
        }

        m.Result = IntPtr.Zero;
    }

    private void DrawFlatChrome(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var info = new ComboBoxInfo { CbSize = Marshal.SizeOf<ComboBoxInfo>() };
        Rectangle itemBounds;
        Rectangle buttonBounds;
        if (GetComboBoxInfo(Handle, ref info))
        {
            itemBounds = Rectangle.FromLTRB(
                info.RcItem.Left,
                info.RcItem.Top,
                info.RcItem.Right,
                info.RcItem.Bottom);
            buttonBounds = Rectangle.FromLTRB(
                info.RcButton.Left,
                info.RcButton.Top,
                info.RcButton.Right,
                info.RcButton.Bottom);
        }
        else
        {
            var buttonWidth = Math.Min(DesignMetrics.Px(36, this), Width);
            var edge = DesignMetrics.Px(1, this);
            var textPad = DesignMetrics.Px(6, this);
            buttonBounds = new Rectangle(
                Width - buttonWidth,
                edge,
                Math.Max(1, buttonWidth - edge),
                Math.Max(1, Height - edge * 2));
            itemBounds = new Rectangle(
                textPad,
                edge,
                Math.Max(0, Width - buttonWidth - textPad * 2),
                Math.Max(1, Height - edge * 2));
        }

        var inputBack = UiColors.ForControlBack(UiColors.ProjectBarInputBack);
        using (var backBrush = new SolidBrush(inputBack))
        {
            g.FillRectangle(backBrush, ClientRectangle);
        }

        if (Enabled && (_hovered || DroppedDown || Focused))
        {
            using var hoverBrush = new SolidBrush(
                UiColors.ForControlBack(UiColors.TransportHoverBack));
            g.FillRectangle(hoverBrush, buttonBounds);
        }

        var fore = Enabled ? UiColors.ProjectBarInputFore : UiColors.ChromeDim;
        var text = SelectedIndex >= 0 && SelectedIndex < Items.Count
            ? GetItemText(Items[SelectedIndex])
            : string.Empty;
        if (!string.IsNullOrEmpty(text))
        {
            var textInset = DesignMetrics.Px(6, this);
            var textBounds = new Rectangle(
                itemBounds.Left + textInset,
                itemBounds.Top,
                Math.Max(0, itemBounds.Width - textInset),
                itemBounds.Height);
            TextRenderer.DrawText(
                g,
                text,
                Font,
                textBounds,
                fore,
                TextFormatFlags.Left
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPrefix);
        }

        var centerX = buttonBounds.Left + buttonBounds.Width / 2f;
        var centerY = buttonBounds.Top + buttonBounds.Height / 2f;
        var ax = DesignMetrics.PxF(6f, this);
        var ay = DesignMetrics.PxF(3f, this);
        var arrow = DroppedDown
            ? new[]
            {
                new PointF(centerX - ax, centerY + ay),
                new PointF(centerX, centerY - ay),
                new PointF(centerX + ax, centerY + ay),
            }
            : new[]
            {
                new PointF(centerX - ax, centerY - ay),
                new PointF(centerX, centerY + ay),
                new PointF(centerX + ax, centerY - ay),
            };
        using (var arrowPen = new Pen(fore, Math.Max(1f, DesignMetrics.PxF(2.4f, this)))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        })
        {
            g.DrawLines(arrowPen, arrow);
        }

        using var borderPen = new Pen(
            Enabled ? UiColors.ProjectBarBorder : UiColors.ChromeMid);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }
}
