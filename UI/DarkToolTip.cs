using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// UiColors に追従するダーク配色の ToolTip。
/// OwnerDraw で背景・枠・文字色を描き、複数行テキストにも対応する。
/// Popup でサイズを確定し、OS 既定（白背景）描画へ落ちないよう OwnerDraw を都度再確認する。
/// ToolTipSize 変更後に OS が位置を直さないため、描画時にカーソル付近へ再配置する。
/// </summary>
internal sealed class DarkToolTip : ToolTip
{
    private const int MaxTipWidth = 420;
    private const int PadX = 8;
    private const int PadY = 6;
    private const int CursorOffsetX = 16;
    private const int CursorOffsetY = 16;

    private static readonly List<WeakReference<DarkToolTip>> Instances = [];
    private static readonly PropertyInfo? HandleProperty = typeof(ToolTip).GetProperty(
        "Handle",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static bool _globalActive = true;
    private bool _respectsGlobalActive = true;
    private Point? _desiredScreenLocation;

    /// <summary>
    /// アプリ全体のツールチップ表示。false で <see cref="RespectsGlobalActive"/> が true の
    /// DarkToolTip を無効化する（既定 true）。
    /// </summary>
    public static bool GlobalActive
    {
        get => _globalActive;
        set
        {
            _globalActive = value;
            lock (Instances)
            {
                for (var i = Instances.Count - 1; i >= 0; i--)
                {
                    if (Instances[i].TryGetTarget(out var tip))
                    {
                        if (tip._respectsGlobalActive)
                        {
                            tip.Active = value;
                        }
                    }
                    else
                    {
                        Instances.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// false なら全体オフでも常に表示する（ツールチップ切替ボタン用）。既定 true。
    /// </summary>
    public bool RespectsGlobalActive
    {
        get => _respectsGlobalActive;
        set
        {
            _respectsGlobalActive = value;
            Active = value ? _globalActive : true;
        }
    }

    public DarkToolTip() => Initialize();

    public DarkToolTip(IContainer container)
        : base(container) => Initialize();

    private void Initialize()
    {
        ApplyOwnerDrawMode();
        Popup += OnPopup;
        Draw += OnDraw;
        Active = _globalActive;
        lock (Instances)
        {
            Instances.Add(new WeakReference<DarkToolTip>(this));
        }
    }

    /// <summary>テーマ色や OwnerDraw フラグを再適用する（言語切替・色変更後など）。</summary>
    public void ApplyTheme() => ApplyOwnerDrawMode();

    private void ApplyOwnerDrawMode()
    {
        // IsBalloon が true だと OwnerDraw より優先され OS 既定見た目になる。
        IsBalloon = false;
        OwnerDraw = true;
        // アニメーション／フェード中に OwnerDraw が効かない端末がある。
        UseAnimation = false;
        UseFading = false;
        BackColor = UiColors.ForControlBack(UiColors.ToolTipBack);
        ForeColor = UiColors.ToolTipFore;
    }

    private void OnPopup(object? sender, PopupEventArgs e)
    {
        _desiredScreenLocation = null;
        if (_respectsGlobalActive && !_globalActive)
        {
            e.Cancel = true;
            return;
        }

        ApplyOwnerDrawMode();

        var text = e.AssociatedControl is null
            ? string.Empty
            : GetToolTip(e.AssociatedControl);
        if (string.IsNullOrEmpty(text))
        {
            e.Cancel = true;
            return;
        }

        var font = SystemFonts.StatusFont;
        var ownsFont = font is null;
        font ??= new Font("Yu Gothic UI", 9F);
        try
        {
            var size = MeasureTip(text, font);
            var tipSize = new Size(size.Width + PadX * 2, size.Height + PadY * 2);
            e.ToolTipSize = tipSize;
            _desiredScreenLocation = ResolveScreenLocation(e.AssociatedControl, tipSize);
        }
        finally
        {
            if (ownsFont)
            {
                font.Dispose();
            }
        }
    }

    private void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        ApplyOwnerDrawMode();

        if (_desiredScreenLocation is { } location)
        {
            // Popup 時点の位置は変更前サイズ基準のため、長い Tip だと画面端／遠い位置に残る。
            MoveTipWindow(location, e.Bounds.Size);
        }

        var backColor = UiColors.ForControlBack(UiColors.ToolTipBack);
        var borderColor = UiColors.ForControlBack(UiColors.ToolTipBorder);
        var foreColor = UiColors.ToolTipFore;

        using (var back = new SolidBrush(backColor))
        {
            e.Graphics.FillRectangle(back, e.Bounds);
        }

        using (var border = new Pen(borderColor))
        {
            e.Graphics.DrawRectangle(
                border,
                e.Bounds.X,
                e.Bounds.Y,
                e.Bounds.Width - 1,
                e.Bounds.Height - 1);
        }

        const TextFormatFlags flags =
            TextFormatFlags.Left
            | TextFormatFlags.Top
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.WordBreak
            | TextFormatFlags.TextBoxControl;

        var textBounds = new Rectangle(
            e.Bounds.X + PadX,
            e.Bounds.Y + PadY,
            Math.Max(0, e.Bounds.Width - PadX * 2),
            Math.Max(0, e.Bounds.Height - PadY * 2));

        TextRenderer.DrawText(
            e.Graphics,
            e.ToolTipText,
            e.Font ?? SystemFonts.StatusFont!,
            textBounds,
            foreColor,
            flags);
    }

    /// <summary>カーソル付近を基本に、作業領域内へ収まる位置を返す。</summary>
    private static Point ResolveScreenLocation(Control? control, Size tipSize)
    {
        var cursor = Control.MousePosition;
        var working = control is { IsDisposed: false }
            ? Screen.FromControl(control).WorkingArea
            : Screen.FromPoint(cursor).WorkingArea;

        var x = cursor.X + CursorOffsetX;
        var y = cursor.Y + CursorOffsetY;

        if (x + tipSize.Width > working.Right)
        {
            x = Math.Max(working.Left, cursor.X - tipSize.Width - CursorOffsetX);
        }

        if (y + tipSize.Height > working.Bottom)
        {
            y = Math.Max(working.Top, cursor.Y - tipSize.Height - CursorOffsetY);
        }

        if (x < working.Left)
        {
            x = working.Left;
        }

        if (y < working.Top)
        {
            y = working.Top;
        }

        if (x + tipSize.Width > working.Right)
        {
            x = Math.Max(working.Left, working.Right - tipSize.Width);
        }

        if (y + tipSize.Height > working.Bottom)
        {
            y = Math.Max(working.Top, working.Bottom - tipSize.Height);
        }

        return new Point(x, y);
    }

    private void MoveTipWindow(Point screenLocation, Size size)
    {
        var handle = GetTipHandle();
        if (handle == IntPtr.Zero || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        MoveWindow(handle, screenLocation.X, screenLocation.Y, size.Width, size.Height, false);
    }

    private IntPtr GetTipHandle()
    {
        try
        {
            if (HandleProperty?.GetValue(this) is IntPtr handle)
            {
                return handle;
            }
        }
        catch
        {
            // 内部ハンドルが取れない環境では OS 位置のままにする。
        }

        return IntPtr.Zero;
    }

    private static Size MeasureTip(string text, Font font)
    {
        const TextFormatFlags flags =
            TextFormatFlags.Left
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.WordBreak
            | TextFormatFlags.TextBoxControl;

        return TextRenderer.MeasureText(
            text,
            font,
            new Size(MaxTipWidth, int.MaxValue),
            flags);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(
        IntPtr hWnd,
        int x,
        int y,
        int width,
        int height,
        bool repaint);
}
