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
        if (!IsVisibleOnVirtualScreen(bounds))
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

    private static bool IsVisibleOnVirtualScreen(Rect bounds)
    {
        const int margin = 40;
        var visibleArea = new Rect(
            bounds.X + margin,
            bounds.Y + margin,
            Math.Max(1, bounds.Width - margin * 2),
            Math.Max(1, bounds.Height - margin * 2));

        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        return virtualScreen.IntersectsWith(visibleArea);
    }
}
