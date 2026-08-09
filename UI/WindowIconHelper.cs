using System.Windows;
using System.Windows.Media.Imaging;

namespace MgaWwiseIMImporter.UI;

/// <summary>埋め込み ICO をウィンドウ／タスクバーアイコンへ適用する。</summary>
internal static class WindowIconHelper
{
    private static BitmapFrame? _cached;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (GetIcon() is { } icon)
        {
            window.Icon = icon;
        }
    }

    public static BitmapFrame? GetIcon()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            using var stream = AppEmbeddedResources.OpenWindowIcon();
            if (stream is null)
            {
                return null;
            }

            // BitmapDecoder はストリームを後から読むことがあるため MemoryStream にコピーする。
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            var decoder = BitmapDecoder.Create(copy, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames
                .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                .FirstOrDefault();
            if (frame is null)
            {
                return null;
            }

            frame.Freeze();
            _cached = frame;
            return _cached;
        }
        catch
        {
            return null;
        }
    }
}
