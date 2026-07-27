using System.Runtime.CompilerServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// コントロールホバー時の説明文をログ上部 Tips 枠へ出す。
/// </summary>
internal static class TipService
{
    private static readonly ConditionalWeakTable<Control, TipBinding> Bindings = new();

    /// <summary>無効コントロール用に MouseMove を張った親（二重購読防止）。</summary>
    private static readonly ConditionalWeakTable<Control, object> WiredParents = new();

    private static readonly TextFormatFlags MeasureFlags =
        TextFormatFlags.WordBreak
        | TextFormatFlags.TextBoxControl
        | TextFormatFlags.NoPrefix
        | TextFormatFlags.NoPadding;

    /// <summary>Tips オン時に必ず確保する最小行数。</summary>
    private const int MinVisibleLines = 5;

    private static Label? _display;
    private static Panel? _host;
    private static object? _activeSource;
    private static int _suspendCount;
    private static bool _layoutWired;
    private static bool _enabled = true;

    /// <summary>Tips 枠の表示が有効なら true（既定 true）。</summary>
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            if (!_enabled)
            {
                Clear();
            }

            RelayoutHost();
        }
    }

    /// <summary>Tips 表示先（ログ上の Label と、高さを可変にするホスト Panel）。</summary>
    public static void BindDisplay(Label display, Panel host)
    {
        _display = display;
        _host = host;
        display.AutoEllipsis = false;
        display.AutoSize = false;
        display.TextAlign = ContentAlignment.TopLeft;
        display.UseMnemonic = false;

        if (!_layoutWired)
        {
            _layoutWired = true;
            host.SizeChanged += (_, _) => RelayoutHost();
            display.FontChanged += (_, _) => RelayoutHost();
        }

        SetDisplayText(null);
    }

    /// <summary>静的文言を紐づける（再呼出しで文言だけ更新可）。</summary>
    public static void Set(Control control, string? tip, bool respectsEnabled = true)
    {
        var binding = Bindings.GetOrCreateValue(control);
        binding.Text = tip ?? string.Empty;
        binding.RespectsEnabled = respectsEnabled;
        EnsureWired(control, binding);
    }

    /// <summary>動的 Tip（波形ヒットテスト等）。</summary>
    public static void Show(string? text, object source, bool respectsEnabled = true)
    {
        if (_suspendCount > 0)
        {
            return;
        }

        if (!_enabled && respectsEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Clear(source);
            return;
        }

        _activeSource = source;
        SetDisplayText(text);
    }

    /// <summary>表示を空にする。</summary>
    public static void Clear()
    {
        _activeSource = null;
        SetDisplayText(null);
    }

    /// <summary><paramref name="source"/> が現在の表示元のときだけクリアする。</summary>
    public static void Clear(object source)
    {
        if (!ReferenceEquals(_activeSource, source))
        {
            return;
        }

        Clear();
    }

    public static void Suspend()
    {
        _suspendCount++;
        if (_suspendCount == 1)
        {
            Clear();
        }
    }

    public static void Resume()
    {
        if (_suspendCount > 0)
        {
            _suspendCount--;
        }
    }

    private static void EnsureWired(Control control, TipBinding binding)
    {
        if (!binding.Wired)
        {
            binding.Wired = true;
            control.MouseEnter += (_, _) =>
            {
                if (_suspendCount > 0)
                {
                    return;
                }

                if (!_enabled && binding.RespectsEnabled)
                {
                    return;
                }

                Show(binding.Text, control, binding.RespectsEnabled);
            };
            control.MouseLeave += (_, _) => Clear(control);
            control.ParentChanged += (_, _) => EnsureParentWired(control);
            control.Disposed += (_, _) =>
            {
                if (ReferenceEquals(_activeSource, control))
                {
                    Clear(control);
                }
            };
        }

        // 無効コントロールは自身へ MouseEnter が来ないため、親の MouseMove で拾う。
        EnsureParentWired(control);
    }

    private static void EnsureParentWired(Control control)
    {
        var parent = control.Parent;
        if (parent is null || parent.IsDisposed)
        {
            return;
        }

        _ = WiredParents.GetValue(parent, static p =>
        {
            p.MouseMove += Parent_MouseMove;
            p.MouseLeave += Parent_MouseLeave;
            return new object();
        });
    }

    private static void Parent_MouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not Control parent || _suspendCount > 0)
        {
            return;
        }

        var hit = FindDisabledTipControl(parent, e.Location);
        if (hit is not null && Bindings.TryGetValue(hit, out var binding))
        {
            if (!_enabled && binding.RespectsEnabled)
            {
                return;
            }

            Show(binding.Text, hit, binding.RespectsEnabled);
            return;
        }

        if (_activeSource is Control { Enabled: false } active
            && ReferenceEquals(active.Parent, parent))
        {
            Clear(active);
        }
    }

    private static void Parent_MouseLeave(object? sender, EventArgs e)
    {
        if (sender is not Control parent)
        {
            return;
        }

        if (_activeSource is not Control { Enabled: false } active
            || !ReferenceEquals(active.Parent, parent))
        {
            return;
        }

        var client = parent.PointToClient(Control.MousePosition);
        if (!parent.ClientRectangle.Contains(client))
        {
            Clear(active);
        }
    }

    /// <summary>
    /// 親クライアント座標上の、Tips 紐づけがある無効コントロールを探す（前面優先）。
    /// </summary>
    private static Control? FindDisabledTipControl(Control parent, Point clientPt)
    {
        for (var i = parent.Controls.Count - 1; i >= 0; i--)
        {
            var child = parent.Controls[i];
            if (!child.Visible || !child.Bounds.Contains(clientPt))
            {
                continue;
            }

            var local = new Point(clientPt.X - child.Left, clientPt.Y - child.Top);
            var nested = FindDisabledTipControl(child, local);
            if (nested is not null)
            {
                return nested;
            }

            if (!child.Enabled && Bindings.TryGetValue(child, out _))
            {
                return child;
            }
        }

        return null;
    }

    private static void SetDisplayText(string? text)
    {
        if (_display is null || _display.IsDisposed)
        {
            return;
        }

        var value = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (_display.InvokeRequired)
        {
            if (!_display.IsHandleCreated)
            {
                return;
            }

            _display.BeginInvoke(() => ApplyDisplayText(value));
            return;
        }

        ApplyDisplayText(value);
    }

    private static void ApplyDisplayText(string value)
    {
        if (_display is null || _display.IsDisposed)
        {
            return;
        }

        if (!string.Equals(_display.Text, value, StringComparison.Ordinal))
        {
            _display.Text = value;
        }

        RelayoutHost();
    }

    private static void RelayoutHost()
    {
        if (_display is null || _host is null
            || _display.IsDisposed || _host.IsDisposed)
        {
            return;
        }

        if (!_enabled)
        {
            SetHostHeight(0);
            return;
        }

        var chromeHeight = MeasureChromeHeight(_host, _display);
        var padding = _display.Padding;
        var contentWidth = Math.Max(1, _host.ClientSize.Width - padding.Horizontal);
        var minContentHeight = MeasureLineHeight(_display.Font) * MinVisibleLines;
        var contentHeight = minContentHeight;

        if (!string.IsNullOrEmpty(_display.Text))
        {
            var measured = TextRenderer.MeasureText(
                _display.Text,
                _display.Font,
                new Size(contentWidth, int.MaxValue),
                MeasureFlags);
            // 計測と実描画の端数差で最終行が欠けないよう 1px 余裕を足す。
            contentHeight = Math.Max(minContentHeight, measured.Height + 1);
        }

        SetHostHeight(chromeHeight + padding.Vertical + contentHeight);
    }

    /// <summary>帯・区切り線など、本文以外の Dock=Top/Bottom 部品の合計高さ。</summary>
    private static int MeasureChromeHeight(Panel host, Control display)
    {
        var height = 0;
        foreach (Control child in host.Controls)
        {
            if (ReferenceEquals(child, display))
            {
                continue;
            }

            if (child.Dock is DockStyle.Top or DockStyle.Bottom)
            {
                height += child.Height;
            }
        }

        return height;
    }

    private static int MeasureLineHeight(Font font)
    {
        // 実フォントの1行高（空行相当）を測り、最小5行分の高さを揃える。
        var measured = TextRenderer.MeasureText(
            "Ag",
            font,
            new Size(int.MaxValue, int.MaxValue),
            MeasureFlags);
        return Math.Max(1, measured.Height);
    }

    private static void SetHostHeight(int height)
    {
        if (_host is null || _host.IsDisposed)
        {
            return;
        }

        if (_host.Height != height)
        {
            _host.Height = height;
        }
    }

    private sealed class TipBinding
    {
        public string Text = string.Empty;
        public bool RespectsEnabled = true;
        public bool Wired;
    }
}
