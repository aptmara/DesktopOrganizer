using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DesktopOrganizer.UI.Utilities;

public static class IconUtilities
{
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // スレッドセーフなキャッシュ
    private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();

    /// <summary>
    /// パスからアイコンを取得する。
    /// キャッシュ済みの場合はキャッシュから返す。
    /// </summary>
    public static ImageSource? GetIconFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // キャッシュチェック
        if (_iconCache.TryGetValue(path, out var cached)) return cached;

        // ファイル/ディレクトリ存在チェック
        if (!File.Exists(path) && !Directory.Exists(path)) return null;

        try
        {
            ImageSource? image = null;

            // .urlファイル（インターネットショートカット）の特別処理
            if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                image = GetIconFromUrlFile(path);
            }

            // 通常の方法で取得
            if (image == null)
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    image = ToImageSource(icon);
                }
            }

            if (image != null)
            {
                image.Freeze(); // 異なるスレッドからアクセス可能にするために必須
                _iconCache.TryAdd(path, image);
            }

            return image;
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError($"Failed to get icon for: {path}", ex);
            return null;
        }
    }

    /// <summary>
    /// .urlファイル（インターネットショートカット）からアイコンを取得。
    /// Steam等のカスタムアイコンに対応。
    /// </summary>
    private static ImageSource? GetIconFromUrlFile(string urlFilePath)
    {
        try
        {
            var lines = File.ReadAllLines(urlFilePath);
            string? iconFile = null;
            int iconIndex = 0;

            foreach (var line in lines)
            {
                // IconFile=C:\path\to\icon.exe または IconFile=C:\path\to\icon.ico
                if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                {
                    iconFile = line.Substring("IconFile=".Length).Trim();
                }
                // IconIndex=0
                else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Substring("IconIndex=".Length).Trim(), out var idx))
                    {
                        iconIndex = idx;
                    }
                }
            }

            if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
            {
                // .icoファイルの場合
                if (iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var icon = new Icon(iconFile);
                    return ToImageSource(icon);
                }

                // .exe/.dllからアイコン抽出
                IntPtr hIcon = ExtractIcon(IntPtr.Zero, iconFile, iconIndex);
                if (hIcon != IntPtr.Zero && hIcon.ToInt64() > 1)
                {
                    try
                    {
                        using var icon = Icon.FromHandle(hIcon);
                        return ToImageSource(icon);
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError($"Failed to get icon from URL file: {urlFilePath}", ex);
        }

        return null;
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

            // 異なるスレッドからアクセス可能にするために必須
            wpfBitmap.Freeze();
            return wpfBitmap;
        }
        finally
        {
            DeleteObject(hBitmap);
            bitmap.Dispose();
        }
    }
}
