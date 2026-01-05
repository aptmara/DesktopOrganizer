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
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0; // 32x32
    private const uint SHGFI_SYSICONINDEX = 0x4000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    // Image list types for SHGetImageList
    private const int SHIL_LARGE = 0;      // 32x32
    private const int SHIL_SMALL = 1;      // 16x16
    private const int SHIL_EXTRALARGE = 2; // 48x48
    private const int SHIL_JUMBO = 4;      // 256x256

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig]
        int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig]
        int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig]
        int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig]
        int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig]
        int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig]
        int Draw(ref IMAGELISTDRAWPARAMS pimldp);
        [PreserveSig]
        int Remove(int i);
        [PreserveSig]
        int GetIcon(int i, uint flags, ref IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGELISTDRAWPARAMS
    {
        public int cbSize;
        public IntPtr himl;
        public int i;
        public IntPtr hdcDst;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int xBitmap;
        public int yBitmap;
        public int rgbBk;
        public int rgbFg;
        public int fStyle;
        public int dwRop;
        public int fState;
        public int Frame;
        public int crEffect;
    }

    /// <summary>
    /// LRU Cache Implementation - O(1) operations
    /// Dictionary: path -> (ImageSource, LinkedListNode)
    /// LinkedList: LRU order tracking (most recently used at First)
    /// </summary>
    private const int MaxCacheSize = 500;
    private static readonly Dictionary<string, (ImageSource Image, LinkedListNode<string> Node)> _cache = new();
    private static readonly LinkedList<string> _lruList = new();
    private static readonly object _lock = new();

    // Throttling: Max 4 concurrent icon extractions to prevent thread pool starvation
    private static readonly SemaphoreSlim _semaphore = new(4);

    private static string? _iconCacheDir;
    private static string IconCacheDir
    {
        get
        {
            if (_iconCacheDir == null)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _iconCacheDir = Path.Combine(appData, "DesktopOrganizer", "Icons");
                Directory.CreateDirectory(_iconCacheDir);
            }
            return _iconCacheDir;
        }
    }

    private static string GetCachedIconPath(string targetPath)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(targetPath.ToLowerInvariant());
        var hashBytes = md5.ComputeHash(inputBytes);
        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return Path.Combine(IconCacheDir, $"{hashString}.png");
    }

    /// <summary>
    /// Asynchronously gets an icon from a path with caching and throttling.
    /// </summary>
    public static async Task<ImageSource?> GetIconAsync(string path, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // 1. Memory Cache Check (Fastest) - O(1)
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var cached))
            {
                _lruList.Remove(cached.Node);
                var newNode = _lruList.AddFirst(path);
                _cache[path] = (cached.Image, newNode);
                return cached.Image;
            }
        }

        // 2. Throttling
        await _semaphore.WaitAsync(token).ConfigureAwait(false);

        try
        {
            // 3. Double-check memory cache
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var cached))
                {
                    _lruList.Remove(cached.Node);
                    var newNode = _lruList.AddFirst(path);
                    _cache[path] = (cached.Image, newNode);
                    return cached.Image;
                }
            }

            return await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return null;

                // 4. Disk Cache Check
                var cachePath = GetCachedIconPath(path);
                if (File.Exists(cachePath))
                {
                    try
                    {
                        // Load from disk
                        var diskImage = LoadImageFromDisk(cachePath);
                        if (diskImage != null)
                        {
                            AddToCache(path, diskImage);
                            return diskImage;
                        }
                    }
                    catch
                    {
                        // Corruption or read error, ignore and re-extract
                    }
                }

                // 5. Extraction
                var image = ExtractAndCache(path);

                // 6. Save to Disk Cache
                if (image != null)
                {
                    try
                    {
                        SaveImageToDisk(image, cachePath);
                    }
                    catch
                    {
                        // Disk write failed, but we have the image in memory so it's fine for this session
                    }
                }

                return image;
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError($"Failed to get icon for: {path}", ex);
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static BitmapImage? LoadImageFromDisk(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveImageToDisk(ImageSource image, string path)
    {
        if (image is BitmapSource bitmapSource)
        {
            using var fileStream = new FileStream(path, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);
        }
    }

    // Synchronous fallback (deprecated)
    public static ImageSource? GetIconFromPath(string path)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var cached)) return cached.Image;
        }

        var cachePath = GetCachedIconPath(path);
        if (File.Exists(cachePath))
        {
            var diskImage = LoadImageFromDisk(cachePath);
            if (diskImage != null)
            {
                AddToCache(path, diskImage);
                return diskImage;
            }
        }

        return ExtractAndCache(path);
    }

    private static ImageSource? ExtractAndCache(string path)
    {
        try
        {
            // Validations
            if (!File.Exists(path) && !Directory.Exists(path)) return null;

            ImageSource? image = null;

            if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            {
                image = GetIconFromUrlFile(path);
            }

            // Url file handling might adhere to SHGetFileInfo anyway, but keeping specific logic if preferred.
            // If Url logic returned null, or it wasn't a Url file, try SHGetFileInfo.
            if (image == null)
            {
                image = GetShellIcon(path);
            }

            // Fallback (though SHGetFileInfo handles almost everything)
            if (image == null)
            {
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(path);
                    if (icon != null)
                    {
                        image = ToImageSource(icon);
                    }
                }
                catch { }
            }

            if (image != null)
            {
                image.Freeze();
                AddToCache(path, image);
            }

            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? GetShellIcon(string path)
    {
        try
        {
            // First get the icon index from the system image list
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr hImg = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_SYSICONINDEX);

            if (hImg == IntPtr.Zero) return null;

            int iconIndex = shinfo.iIcon;

            // Try to get jumbo icon (256x256) first, then fallback to extra large (48x48)
            ImageSource? result = GetIconFromImageList(iconIndex, SHIL_JUMBO);
            if (result == null)
            {
                result = GetIconFromImageList(iconIndex, SHIL_EXTRALARGE);
            }
            if (result == null)
            {
                result = GetIconFromImageList(iconIndex, SHIL_LARGE);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? GetIconFromImageList(int iconIndex, int imageListType)
    {
        try
        {
            Guid iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
            int hr = SHGetImageList(imageListType, ref iidImageList, out IImageList imgList);

            if (hr != 0 || imgList == null) return null;

            IntPtr hIcon = IntPtr.Zero;
            hr = imgList.GetIcon(iconIndex, 0, ref hIcon);

            if (hr != 0 || hIcon == IntPtr.Zero) return null;

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
        catch
        {
            return null;
        }
    }

    private static void AddToCache(string path, ImageSource image)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var existing))
            {
                _lruList.Remove(existing.Node);
            }
            else if (_cache.Count >= MaxCacheSize)
            {
                var last = _lruList.Last;
                if (last != null)
                {
                    _cache.Remove(last.Value);
                    _lruList.RemoveLast();
                }
            }

            var node = _lruList.AddFirst(path);
            _cache[path] = (image, node);
        }
    }

    private static ImageSource? GetIconFromUrlFile(string urlFilePath)
    {
        try
        {
            // Simplified: often .url files are just handled by Shell properly.
            // But we keep reading IconFile/IconIndex for manual overriding if set.
            var lines = File.ReadAllLines(urlFilePath);
            string? iconFile = null;
            int iconIndex = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase))
                {
                    iconFile = line.Substring("IconFile=".Length).Trim();
                }
                else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Substring("IconIndex=".Length).Trim(), out var idx))
                    {
                        iconIndex = idx;
                    }
                }
            }

            if (!string.IsNullOrEmpty(iconFile))
            {
                // Resolve environment variables if any
                iconFile = Environment.ExpandEnvironmentVariables(iconFile);

                if (File.Exists(iconFile))
                {
                    // If directly an ICO
                    if (iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using var icon = new Icon(iconFile);
                        return ToImageSource(icon);
                    }

                    // Extract from DLL/EXE
                    // We can reuse SHGetFileInfo or ExtractIconEx, but since we have index, lets use ExtractIcon (the one we removed... wait, we need it if we support index)
                    // ACTUALLY, ExtractIcon is deprecated mostly, but let's re-add it strictly for this URL case if needed.
                    // Or efficient way: use SHGetFileInfo with PIDL? 
                    // Let's rely on standard ExtractAssociatedIcon if simple, but that doesn't take index.
                    // Re-adding ExtractIcon just for this helper.

                    IntPtr hIcon = ExtractIcon(IntPtr.Zero, iconFile, iconIndex);
                    if (hIcon != IntPtr.Zero && hIcon.ToInt64() > 1) // 1 means failure in some docs
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
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError($"Failed to get icon from URL file: {urlFilePath}", ex);
        }

        return null;
    }

    // Re-adding for .url support
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

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
