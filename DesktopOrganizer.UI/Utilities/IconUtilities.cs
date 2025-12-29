using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace DesktopOrganizer.UI.Utilities;

public static class IconUtilities
{
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    private static readonly Dictionary<string, ImageSource> _iconCache = new();

    public static ImageSource? GetIconFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (_iconCache.TryGetValue(path, out var cached)) return cached;

        if (!File.Exists(path) && !Directory.Exists(path)) return null;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;

            var image = ToImageSource(icon);
            image.Freeze(); // 異なるスレッドからアクセス可能にするために必須

            _iconCache[path] = image;
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource ToImageSource(Icon icon)
    {
        Bitmap bitmap = icon.ToBitmap();
        IntPtr hBitmap = bitmap.GetHbitmap();

        try
        {
            ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return wpfBitmap;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }
}
