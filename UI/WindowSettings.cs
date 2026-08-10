using System.Runtime.InteropServices;
using System.Windows;

namespace MgaWwiseIMImporter.UI;

internal sealed class WindowSettings
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public static WindowSettings? Load()
    {
        try
        {
            var data = JsonSettingsStore.Document.Window;
            if (data is null
                || data.Width <= 0
                || data.Height <= 0)
            {
                return null;
            }

            return new WindowSettings
            {
                X = data.X,
                Y = data.Y,
                Width = data.Width,
                Height = data.Height,
            };
        }
        catch
        {
            return null;
        }
    }

    public void Save()
    {
        var data = new WindowSettingsData
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
        };
        JsonSettingsStore.Update(doc => doc.Window = data);
    }

    public static WindowSettings FromWindow(Window window)
    {
        Rect bounds;
        if (window.WindowState == WindowState.Normal)
        {
            bounds = new Rect(window.Left, window.Top, window.Width, window.Height);
        }
        else
        {
            bounds = window.RestoreBounds;
        }

        return new WindowSettings
        {
            X = (int)Math.Round(bounds.X),
            Y = (int)Math.Round(bounds.Y),
            Width = (int)Math.Round(bounds.Width),
            Height = (int)Math.Round(bounds.Height),
        };
    }

    public bool TryApply(Window window)
    {
        if (Width < window.MinWidth || Height < window.MinHeight)
        {
            return false;
        }

        var bounds = new Rect(X, Y, Width, Height);
        if (!IsVisibleOnAnyScreen(bounds))
        {
            return false;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = bounds.X;
        window.Top = bounds.Y;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        return true;
    }

    /// <summary>
    /// いずれかのモニターの作業領域と重なるか（WinForms Screen.AllScreens 相当）。
    /// 仮想スクリーンの外接矩形だけで判定すると、L 字配置などでモニターの無い
    /// 空白域に復元してしまうため、モニター単位で判定する。
    /// </summary>
    private static bool IsVisibleOnAnyScreen(Rect bounds)
    {
        const int margin = 40;
        var visibleArea = new Rect(
            bounds.X + margin,
            bounds.Y + margin,
            Math.Max(1, bounds.Width - margin * 2),
            Math.Max(1, bounds.Height - margin * 2));

        // Window.Left/Top は DIP、モニター矩形は物理 px。プライマリ DPI 比で換算する。
        var scale = GetPrimaryScreenScale();
        var pixelArea = new Rect(
            visibleArea.X * scale,
            visibleArea.Y * scale,
            Math.Max(1, visibleArea.Width * scale),
            Math.Max(1, visibleArea.Height * scale));

        var intersects = false;
        _ = EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr monitor, IntPtr _, ref NativeRect _, IntPtr _) =>
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    var work = new Rect(
                        info.Work.Left,
                        info.Work.Top,
                        Math.Max(1, info.Work.Right - info.Work.Left),
                        Math.Max(1, info.Work.Bottom - info.Work.Top));
                    if (work.IntersectsWith(pixelArea))
                    {
                        intersects = true;
                        return false;
                    }
                }

                return true;
            },
            IntPtr.Zero);
        return intersects;
    }

    private static double GetPrimaryScreenScale()
    {
        const int SmCxScreen = 0;
        var dipWidth = SystemParameters.PrimaryScreenWidth;
        var pixelWidth = GetSystemMetrics(SmCxScreen);
        return dipWidth > 0 && pixelWidth > 0 ? pixelWidth / dipWidth : 1d;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref NativeRect rect, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
