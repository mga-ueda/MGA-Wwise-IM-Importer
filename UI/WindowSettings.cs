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

    public static WindowSettings FromForm(Form form)
    {
        var bounds = form.WindowState == FormWindowState.Normal
            ? form.Bounds
            : form.RestoreBounds;

        return new WindowSettings
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
        };
    }

    public bool TryApply(Form form)
    {
        if (Width < form.MinimumSize.Width || Height < form.MinimumSize.Height)
        {
            return false;
        }

        var bounds = new Rectangle(X, Y, Width, Height);
        if (!IsVisibleOnAnyScreen(bounds))
        {
            return false;
        }

        form.StartPosition = FormStartPosition.Manual;
        form.WindowState = FormWindowState.Normal;
        form.Bounds = bounds;
        return true;
    }

    private static bool IsVisibleOnAnyScreen(Rectangle bounds)
    {
        const int margin = 40;
        var visibleArea = new Rectangle(
            bounds.X + margin,
            bounds.Y + margin,
            Math.Max(1, bounds.Width - margin * 2),
            Math.Max(1, bounds.Height - margin * 2));

        return Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(visibleArea));
    }
}
