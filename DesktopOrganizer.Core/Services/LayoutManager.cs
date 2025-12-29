using System.IO;
using System.Text.Json;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.Core.Services;

public class LayoutManager
{
    private const string LAYOUT_FILENAME = "layout.json";
    private readonly string _layoutPath;

    public LayoutData CurrentLayout { get; private set; } = new();

    public LayoutManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDir = Path.Combine(appData, "DesktopOrganizer");
        Directory.CreateDirectory(appDir);
        _layoutPath = Path.Combine(appDir, LAYOUT_FILENAME);
    }

    public void LoadLayout()
    {
        Utilities.Logger.Log($"Loading layout from {_layoutPath}");
        if (File.Exists(_layoutPath))
        {
            try
            {
                var json = File.ReadAllText(_layoutPath);
                CurrentLayout = JsonSerializer.Deserialize<LayoutData>(json) ?? new LayoutData();
                Utilities.Logger.Log($"Layout loaded successfully. {_layoutPath}");
            }
            catch (Exception ex)
            {
                Utilities.Logger.LogError("Failed to load layout. Initializing new layout.", ex);
                CurrentLayout = new LayoutData();
            }
        }
        else
        {
            Utilities.Logger.Log("Layout file not found. Initializing new layout.");
            CurrentLayout = new LayoutData();
        }
    }

    public void SaveLayout()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentLayout, options);
            File.WriteAllText(_layoutPath, json);
            Utilities.Logger.Log($"Layout saved to {_layoutPath}");
        }
        catch (Exception ex)
        {
            Utilities.Logger.LogError("Failed to save layout.", ex);
        }
    }

    /// <summary>
    /// 特定のモニター上の棚の実際の物理ピクセル矩形を計算します。
    /// </summary>
    public NativeMethods.RECT CalculatePhysicalRect(Shelf shelf, MonitorItem monitor)
    {
        var waWidth = monitor.WorkArea.Width;
        var waHeight = monitor.WorkArea.Height;
        var waLeft = monitor.WorkArea.Left;
        var waTop = monitor.WorkArea.Top;

        var x = (int)(waLeft + (shelf.X * waWidth));
        var y = (int)(waTop + (shelf.Y * waHeight));
        var w = (int)(shelf.Width * waWidth);
        var h = (int)(shelf.Height * waHeight);

        // 将来的に可視性チェックを追加する可能性あり
        // 今は単純計算のみ

        return new NativeMethods.RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
    }

    /// <summary>
    /// DeviceIdとフォールバックロジックに基づいて、棚に最適なモニターを見つけます。
    /// </summary>
    public MonitorItem FindBestMonitor(Shelf shelf, List<MonitorItem> monitors)
    {
        // 1. DeviceIDの完全一致
        var exact = monitors.FirstOrDefault(m => m.DeviceName == shelf.TargetMonitorDeviceId); // MonitorInfoEx.DeviceNameは通常内部ID
        if (exact != null) return exact;

        // 2. フォールバック: プライマリモニター
        var primary = monitors.FirstOrDefault(m => m.IsPrimary);
        if (primary != null)
        {
            Utilities.Logger.Log($"Shelf monitor '{shelf.TargetMonitorDeviceId}' not found. Fallback to Primary.");
            return primary;
        }

        // 3. フォールバック: 最初のモニター
        Utilities.Logger.Log($"Shelf monitor '{shelf.TargetMonitorDeviceId}' not found. Fallback to First Main.");
        return monitors.First();
    }

    // 現在の位置（編集中）に基づいて正規化座標を更新するヘルパーメソッド
    public void UpdateShelfPosition(Shelf shelf, NativeMethods.RECT currentRect, MonitorItem monitor)
    {
        var waWidth = monitor.WorkArea.Width;
        var waHeight = monitor.WorkArea.Height;

        // ゼロ除算を防止
        if (waWidth == 0) waWidth = 1920;
        if (waHeight == 0) waHeight = 1080;

        shelf.X = (double)(currentRect.Left - monitor.WorkArea.Left) / waWidth;
        shelf.Y = (double)(currentRect.Top - monitor.WorkArea.Top) / waHeight;
        shelf.Width = (double)currentRect.Width / waWidth;
        shelf.Height = (double)currentRect.Height / waHeight;

        shelf.TargetMonitorDeviceId = monitor.DeviceName;

        Utilities.Logger.Log($"Shelf '{shelf.Title}' position updated. Norm: ({shelf.X:F3}, {shelf.Y:F3}) Monitor: {monitor.DeviceName}");
    }
}
