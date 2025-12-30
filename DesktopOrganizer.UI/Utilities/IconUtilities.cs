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

    /// <summary>
    /// Asynchronously gets an icon from a path with caching and throttling.
    /// </summary>
    public static async Task<ImageSource?> GetIconAsync(string path, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // 1. Fast Cache Check (Sync) - O(1)
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var cached))
            {
                // Move to MRU - O(1) via direct node reference
                _lruList.Remove(cached.Node);
                var newNode = _lruList.AddFirst(path);
                _cache[path] = (cached.Image, newNode);
                return cached.Image;
            }
        }

        // 2. Throttling
        // Wait for a slot, respecting cancellation
        await _semaphore.WaitAsync(token).ConfigureAwait(false);

        try
        {
            // 3. Double-check cache after acquiring semaphore (race condition prevention)
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var cached))
                {
                    // Move to MRU - O(1)
                    _lruList.Remove(cached.Node);
                    var newNode = _lruList.AddFirst(path);
                    _cache[path] = (cached.Image, newNode);
                    return cached.Image;
                }
            }

            // 4. Heavy Extraction (on thread pool implicit via Task.Run if needed, but we are already async)
            // Since ExtractAssociatedIcon is blocking and could take time, wrap in Task.Run if not already on a background thread.
            // But usually this method is called from Task.Run. Let's assume called from ThreadPool.
            // To be safe and non-blocking for the caller, we wrap the IO work.

            return await Task.Run(() =>
            {
                if (token.IsCancellationRequested) return null;
                return ExtractAndCache(path);
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

    // Synchronous fallback (deprecated but kept for compatibility if needed, though we should migrate)
    public static ImageSource? GetIconFromPath(string path)
    {
        // Sync version just calls async loop... bad practice but quick fix for now?
        // No, let's keep the logic simple. If sync is called, we bypass semaphore or block?
        // Let's reimplement sync to use cache but skip semaphore for backward compact OR just block.
        // Better to discourage sync use.

        // For now, simple implementation without throttling for legacy sync calls (risk of blockage)
        // But we added LRU at least.
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var cached)) return cached.Image;
        }

        var img = ExtractAndCache(path);
        return img;
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

    private static void AddToCache(string path, ImageSource image)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var existing))
            {
                // Update existing - O(1)
                _lruList.Remove(existing.Node);
            }
            else if (_cache.Count >= MaxCacheSize)
            {
                // Evict LRU - O(1)
                var last = _lruList.Last;
                if (last != null)
                {
                    _cache.Remove(last.Value);
                    _lruList.RemoveLast();
                }
            }

            // Add new entry - O(1)
            var node = _lruList.AddFirst(path);
            _cache[path] = (image, node);
        }
    }

    private static ImageSource? GetIconFromUrlFile(string urlFilePath)
    {
        try
        {
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

            if (!string.IsNullOrEmpty(iconFile) && File.Exists(iconFile))
            {
                if (iconFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var icon = new Icon(iconFile);
                    return ToImageSource(icon);
                }

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
