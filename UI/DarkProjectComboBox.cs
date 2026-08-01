using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// プロジェクト名の編集と選択に使うダークテーマ ComboBox。
/// WM_PAINT を自前で処理し、枠・矢印・項目をフラットに描画する
/// （ネイティブ描画との二度描きによるちらつきを避ける）。
/// </summary>
internal sealed class DarkProjectComboBox : ComboBox
{
    private const int WmEraseBkgnd = 0x0014;
    private const int WmPaint = 0x000F;
    private const int WmNcPaint = 0x0085;
    private const int WmSetFocus = 0x0007;
    private const int WmKillFocus = 0x0008;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonDblClk = 0x0203;
    private const int EmSetSel = 0x00B1;
    private const int CbSetItemHeight = 0x0153;
    private const int CbGetItemHeight = 0x0154;
    private const int GwlStyle = -16;
    private const int EsNoHideSel = 0x0100;

    private bool _hovered;
    private bool _clearingSelection;
    private int? _controlHeightTarget;
    private DropDownBorderWindow? _dropDownBorderWindow;
    private EditSelectionGuard? _editGuard;

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

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern bool HideCaret(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(
        IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static int GetWindowStyle(IntPtr hWnd) =>
        IntPtr.Size == 8
            ? unchecked((int)(long)GetWindowLongPtr64(hWnd, GwlStyle))
            : GetWindowLong32(hWnd, GwlStyle);

    private static void SetWindowStyle(IntPtr hWnd, int style)
    {
        if (IntPtr.Size == 8)
        {
            _ = SetWindowLongPtr64(hWnd, GwlStyle, (IntPtr)style);
        }
        else
        {
            _ = SetWindowLong32(hWnd, GwlStyle, style);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Rectangle ToRectangle() =>
            Rectangle.FromLTRB(Left, Top, Right, Bottom);
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

    public DarkProjectComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDown;
        FlatStyle = FlatStyle.Flat;
        IntegralHeight = false;
        // コンストラクタ時点では親 DPI が未確定なことがある。OnHandleCreated で確定する。
        ApplyListItemHeight();
        MaxDropDownItems = 12;
        ApplyColors();
    }

    /// <summary>ドロップダウン各行の高さ（フォント＋薄い余白。選択欄より太くしない）。</summary>
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

    /// <summary>
    /// 編集欄のテキスト選択ハイライトを解除する。
    /// ComboBox の子 EDIT にも EM_SETSEL を送り、見た目の全選択を確実に消す。
    /// </summary>
    public void ClearTextSelection()
    {
        // 自身が送る EM_SETSEL を EditSelectionGuard が再処理しないよう再入を抑止する。
        if (_clearingSelection)
        {
            return;
        }

        _clearingSelection = true;
        try
        {
            try
            {
                SelectionLength = 0;
                SelectionStart = Math.Min(SelectionStart, Text?.Length ?? 0);
            }
            catch
            {
                // Selection プロパティ失敗時も子 EDIT へ直接送る。
            }

            if (!IsHandleCreated || !TryGetEditHwnd(out var editHwnd))
            {
                return;
            }

            // フォーカス無しでも選択が残るスタイルを毎回剥がす（再作成・テーマ適用で戻ることがある）。
            StripNoHideSel(editHwnd);

            // 選択解除（開始=終了=0）。-1 指定は環境によって全選択に見えることがあるため使わない。
            _ = SendMessage(editHwnd, EmSetSel, IntPtr.Zero, IntPtr.Zero);
            _ = InvalidateRect(editHwnd, IntPtr.Zero, true);

            // アイドル時はキャレットも隠す（フォーカスが残っていても「選択中」に見えにくくする）。
            if (!DroppedDown)
            {
                _ = HideCaret(editHwnd);
            }
        }
        finally
        {
            _clearingSelection = false;
        }
    }

    /// <summary>ドロップダウン選択後など、ユーザー編集以外の全選択を打ち消す。</summary>
    public void DismissTransientSelection()
    {
        _editGuard?.ResetUserSelectAllowance();
        ClearTextSelection();
    }

    private static void StripNoHideSel(IntPtr editHwnd)
    {
        var style = GetWindowStyle(editHwnd);
        if ((style & EsNoHideSel) != 0)
        {
            SetWindowStyle(editHwnd, style & ~EsNoHideSel);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyListItemHeight();
        // ドロップダウン一覧のスクロールバーへ対応 OS のダークテーマを適用する。
        _ = SetWindowTheme(Handle, "DarkMode_CFD", null);
        ApplyControlHeightTarget();
        CenterEditChild();
        AttachEditGuard();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DetachEditGuard();
        base.OnHandleDestroyed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CenterEditChild();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyListItemHeight();
        CenterEditChild();
    }

    /// <summary>
    /// コントロール全体の高さを指定値に合わせる。
    /// ドロップダウン項目高（ItemHeight）は変えず、選択欄だけを調整する。
    /// </summary>
    public void SetControlHeight(int targetHeight)
    {
        _controlHeightTarget = targetHeight;
        if (targetHeight > 0 && Dock == DockStyle.None && Height != targetHeight)
        {
            Height = targetHeight;
        }

        ApplyControlHeightTarget();
    }

    private void ApplyControlHeightTarget()
    {
        if (_controlHeightTarget is not int target || !IsHandleCreated)
        {
            return;
        }

        if (Dock == DockStyle.None && Height != target)
        {
            Height = target;
        }

        // 選択フィールド高。枠分を確保しないと下端の枠線が欠ける。
        var chrome = DesignMetrics.Px(6, this);
        var fieldHeight = (int)SendMessage(Handle, CbGetItemHeight, (IntPtr)(-1), IntPtr.Zero);
        if (fieldHeight > 0 && Height > fieldHeight)
        {
            chrome = Math.Max(chrome, Height - fieldHeight);
        }

        var desired = Math.Max(Font.Height, target - chrome);
        if (desired != fieldHeight)
        {
            _ = SendMessage(Handle, CbSetItemHeight, (IntPtr)(-1), (IntPtr)desired);
        }

        if (Dock == DockStyle.None && Height != target)
        {
            Height = target;
        }

        CenterEditChild();
        Invalidate();
    }

    /// <summary>子 EDIT の上下中央を再適用する（バーレイアウト後用）。</summary>
    public void RefreshEditAlignment() => CenterEditChild();

    /// <summary>
    /// 子 EDIT を選択フィールド内で上下中央に置く。
    /// 単一行 EDIT は領域が高くても文字が上寄せ描画されるため、ウィンドウ自体を中央へ移す。
    /// </summary>
    private void CenterEditChild()
    {
        if (!TryGetEditInfo(out var info) || info.HwndItem == IntPtr.Zero)
        {
            return;
        }

        var item = info.RcItem.ToRectangle();
        var button = info.RcButton.ToRectangle();
        if (item.Width <= 0 || Height <= 2)
        {
            return;
        }

        var border = DesignMetrics.Px(1, this);
        var fieldTop = border;
        var fieldHeight = Height - border * 2;
        // パス欄と同じ GDI 行高で中央を取り、光学補正ぶんだけ上げる（子 EDIT は下寄りに見えるため）。
        var cellHeight = TextBoxVerticalAlign.MeasureGdiLineHeight(this, Font);
        var editHeight = Math.Min(fieldHeight, Math.Max(1, cellHeight));
        var top = fieldTop + Math.Max(0, (fieldHeight - editHeight) / 2)
            - TextBoxVerticalAlign.OpticalNudge(this);
        top = Math.Max(fieldTop, top);
        var left = Math.Max(border, item.Left);
        var right = button.Width > 0 ? button.Left : Width - border;
        var width = Math.Max(0, right - left);
        if (width <= 0)
        {
            return;
        }

        if (!GetWindowRect(info.HwndItem, out var currentScreen))
        {
            return;
        }

        var currentLoc = PointToClient(new Point(currentScreen.Left, currentScreen.Top));
        var currentW = currentScreen.Right - currentScreen.Left;
        var currentH = currentScreen.Bottom - currentScreen.Top;
        if (currentLoc.X == left
            && currentLoc.Y == top
            && currentW == width
            && currentH == editHeight)
        {
            return;
        }

        _ = MoveWindow(info.HwndItem, left, top, width, editHeight, true);
        Invalidate();
    }

    private bool TryGetEditHwnd(out IntPtr editHwnd)
    {
        editHwnd = IntPtr.Zero;
        if (!TryGetEditInfo(out var info) || info.HwndItem == IntPtr.Zero)
        {
            return false;
        }

        editHwnd = info.HwndItem;
        return true;
    }

    private bool TryGetEditInfo(out ComboBoxInfo info)
    {
        info = new ComboBoxInfo { CbSize = Marshal.SizeOf<ComboBoxInfo>() };
        return IsHandleCreated && GetComboBoxInfo(Handle, ref info);
    }

    private void AttachEditGuard()
    {
        DetachEditGuard();
        if (!TryGetEditHwnd(out var editHwnd))
        {
            return;
        }

        // フォーカスが無くても選択ハイライトを残すスタイルを外す。
        StripNoHideSel(editHwnd);

        _editGuard = new EditSelectionGuard(editHwnd, this);
        ClearTextSelection();
    }

    private void DetachEditGuard()
    {
        _editGuard?.ReleaseHandle();
        _editGuard = null;
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
        // 一覧から選ぶと全選択になるため解除する。
        BeginInvoke(DismissTransientSelection);
        Invalidate();
        base.OnDropDownClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachEditGuard();
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

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        // 起動時／タブ移動の自動全選択は消す（マウスクリック編集は Guard 側で許可）。
        BeginInvoke(ClearTextSelection);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        DismissTransientSelection();
        base.OnLostFocus(e);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        // SelectedIndex 設定でも全選択になるため、遅延で消す。
        if (IsHandleCreated)
        {
            BeginInvoke(DismissTransientSelection);
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
        Invalidate();
        base.OnMouseLeave(e);
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

        var textPad = DesignMetrics.Px(12, this);
        var textBounds = new Rectangle(
            e.Bounds.Left + textPad,
            e.Bounds.Top,
            Math.Max(0, e.Bounds.Width - textPad * 2),
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

    /// <summary>
    /// 子 EDIT のフォーカス／選択を監視し、勝手な全選択ハイライトを抑止する。
    /// </summary>
    private sealed class EditSelectionGuard : NativeWindow
    {
        private readonly DarkProjectComboBox _owner;
        private bool _allowUserSelect;
        private bool _clearPosted;

        public EditSelectionGuard(IntPtr handle, DarkProjectComboBox owner)
        {
            _owner = owner;
            AssignHandle(handle);
        }

        public void ResetUserSelectAllowance() => _allowUserSelect = false;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg is WmLButtonDown or WmLButtonDblClk)
            {
                // ユーザーがドラッグ／ダブルクリックで選ぶときは消さない。
                _allowUserSelect = true;
            }

            base.WndProc(ref m);

            if (m.Msg == WmKillFocus)
            {
                _allowUserSelect = false;
                _owner.ClearTextSelection();
                return;
            }

            if (_allowUserSelect || _clearPosted || _owner._clearingSelection)
            {
                return;
            }

            // フォーカス取得や外部からの EM_SETSEL（全選択）を次ループで打ち消す。
            // ClearTextSelection 内の EM_SETSEL は _clearingSelection で無視する。
            if (m.Msg is not (WmSetFocus or EmSetSel))
            {
                return;
            }

            // キャレット移動だけの EM_SETSEL(start==end) は無視。
            if (m.Msg == EmSetSel && m.WParam == m.LParam)
            {
                return;
            }

            _clearPosted = true;
            _owner.BeginInvoke(new Action(() =>
            {
                _clearPosted = false;
                if (!_allowUserSelect)
                {
                    _owner.ClearTextSelection();
                }
            }));
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
                // 全面を WM_PAINT で塗るため、消去はスキップしてちらつきを抑える。
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

        // 編集子ウィンドウの領域は塗らない（テキスト表示は EDIT 側が担当）。
        var info = new ComboBoxInfo { CbSize = Marshal.SizeOf<ComboBoxInfo>() };
        Rectangle? editBounds = null;
        Rectangle buttonBounds;
        if (GetComboBoxInfo(Handle, ref info))
        {
            buttonBounds = info.RcButton.ToRectangle();
            // MoveWindow 後の実位置を使う（rcItem が古い広い領域のままだと余白が塗られない）。
            if (info.HwndItem != IntPtr.Zero && GetWindowRect(info.HwndItem, out var editScreen))
            {
                var topLeft = PointToClient(new Point(editScreen.Left, editScreen.Top));
                editBounds = new Rectangle(
                    topLeft.X,
                    topLeft.Y,
                    editScreen.Right - editScreen.Left,
                    editScreen.Bottom - editScreen.Top);
            }
            else
            {
                editBounds = info.RcItem.ToRectangle();
            }
        }
        else
        {
            var buttonWidth = Math.Min(DesignMetrics.Px(36, this), Width);
            var edge = DesignMetrics.Px(1, this);
            buttonBounds = new Rectangle(
                Width - buttonWidth,
                edge,
                Math.Max(1, buttonWidth - edge),
                Math.Max(1, Height - edge * 2));
        }

        var state = g.Save();
        if (editBounds is { } edit)
        {
            g.ExcludeClip(edit);
        }

        var inputBack = UiColors.ForControlBack(UiColors.ProjectBarInputBack);
        using (var backBrush = new SolidBrush(inputBack))
        {
            g.FillRectangle(backBrush, ClientRectangle);
        }

        if (Enabled && (_hovered || DroppedDown))
        {
            using var hoverBrush = new SolidBrush(
                UiColors.ForControlBack(UiColors.TransportHoverBack));
            g.FillRectangle(hoverBrush, buttonBounds);
        }

        var fore = Enabled ? UiColors.ProjectBarInputFore : UiColors.ChromeDim;
        var centerX = buttonBounds.Left + buttonBounds.Width / 2f;
        var centerY = buttonBounds.Top + buttonBounds.Height / 2f;
        // 150% 設計: 半幅 6 / 縦 3 / ペン 2.4（旧 4 / 2 / 1.6 @96）。
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

        g.Restore(state);

        // 整数座標の DrawRectangle は下端 1px が欠けやすいので、0.5 オフセットで描く。
        using var borderPen = new Pen(
            Enabled ? UiColors.ProjectBarBorder : UiColors.ChromeMid)
        {
            Alignment = PenAlignment.Inset,
        };
        g.DrawRectangle(borderPen, 0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
    }
}
