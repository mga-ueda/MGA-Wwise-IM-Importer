using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// オーナーウィンドウ中央のメッセージボックス。
/// WPF の <see cref="MessageBox.Show"/> はオーナーを渡しても画面中央に出すことがあるため、
/// 表示直前に CBT フックで位置を合わせる。
/// あわせて、WPF が鳴らさないことのあるシステムサウンドも明示再生する。
/// </summary>
/// <remarks>
/// <see cref="SystemSounds"/>（内部 MessageBeep）は一部の Windows 10/11 で無音になるため、
/// winmm の <c>PlaySound</c> + システム別名（SystemAsterisk 等）を使う。
/// </remarks>
internal static class OwnerCenteredMessageBox
{
    private const int WhCbt = 5;
    private const int HcbtActivate = 5;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpNoactivate = 0x0010;
    private const uint SndAsync = 0x0001;
    private const uint SndAlias = 0x00010000;
    private const uint SndSystem = 0x00200000;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static HookProc? _hookProc;
    private static IntPtr _hook;
    private static IntPtr _ownerHwnd;

    public static MessageBoxResult Show(
        Window? owner,
        string text,
        string caption,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        PlaySystemSound(icon);

        if (owner is null)
        {
            return MessageBox.Show(text, caption, buttons, icon, defaultResult);
        }

        var ownerHwnd = new WindowInteropHelper(owner).Handle;
        if (ownerHwnd == IntPtr.Zero || !InstallCenterHook(ownerHwnd))
        {
            return MessageBox.Show(owner, text, caption, buttons, icon, defaultResult);
        }

        try
        {
            return MessageBox.Show(owner, text, caption, buttons, icon, defaultResult);
        }
        finally
        {
            RemoveCenterHook();
        }
    }

    private static bool InstallCenterHook(IntPtr ownerHwnd)
    {
        RemoveCenterHook();
        _ownerHwnd = ownerHwnd;
        _hookProc = OnCbt;
        _hook = SetWindowsHookEx(WhCbt, _hookProc, IntPtr.Zero, GetCurrentThreadId());
        return _hook != IntPtr.Zero;
    }

    private static void RemoveCenterHook()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _hookProc = null;
        _ownerHwnd = IntPtr.Zero;
    }

    private static IntPtr OnCbt(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HcbtActivate && _ownerHwnd != IntPtr.Zero)
        {
            CenterOnOwner(wParam, _ownerHwnd);
            var hook = _hook;
            RemoveCenterHook();
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void CenterOnOwner(IntPtr dialogHwnd, IntPtr ownerHwnd)
    {
        if (!GetWindowRect(dialogHwnd, out var dialog)
            || !GetWindowRect(ownerHwnd, out var owner))
        {
            return;
        }

        var dialogWidth = dialog.Right - dialog.Left;
        var dialogHeight = dialog.Bottom - dialog.Top;
        if (dialogWidth <= 0 || dialogHeight <= 0)
        {
            return;
        }

        var ownerWidth = owner.Right - owner.Left;
        var ownerHeight = owner.Bottom - owner.Top;
        var x = owner.Left + ((ownerWidth - dialogWidth) / 2);
        var y = owner.Top + ((ownerHeight - dialogHeight) / 2);

        var monitor = MonitorFromWindow(ownerHwnd, MonitorDefaultToNearest);
        var info = new MonitorInfo { CbSize = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(monitor, ref info))
        {
            var work = info.RcWork;
            x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - dialogWidth));
            y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - dialogHeight));
        }

        SetWindowPos(
            dialogHwnd,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            SwpNosize | SwpNozorder | SwpNoactivate);
    }

    /// <summary>
    /// <see cref="MessageBoxImage"/> に対応する Windows システムイベント音を再生する。
    /// （Error/Hand、Warning/Exclamation、Information/Asterisk は同一値）。
    /// </summary>
    private static void PlaySystemSound(MessageBoxImage icon)
    {
        // Control Panel「サウンド」に登録されているシステムイベント別名。
        var alias = icon switch
        {
            MessageBoxImage.Error => "SystemHand",
            MessageBoxImage.Question => "SystemQuestion",
            MessageBoxImage.Warning => "SystemExclamation",
            MessageBoxImage.Information => "SystemAsterisk",
            _ => null,
        };
        if (alias is null)
        {
            return;
        }

        // SND_NODEFAULT は付けない: 別名に音が未割当でも既定のシステム音へ落とす。
        if (PlaySound(alias, IntPtr.Zero, SndAlias | SndAsync | SndSystem))
        {
            return;
        }

        // PlaySound が使えない環境向けフォールバック
        switch (icon)
        {
            case MessageBoxImage.Error:
                SystemSounds.Hand.Play();
                break;
            case MessageBoxImage.Question:
                SystemSounds.Question.Play();
                break;
            case MessageBoxImage.Warning:
                SystemSounds.Exclamation.Play();
                break;
            case MessageBoxImage.Information:
                SystemSounds.Asterisk.Play();
                break;
        }
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int CbSize;
        public Rect RcMonitor;
        public Rect RcWork;
        public uint DwFlags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        HookProc proc,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint soundFlags);
}
