using System.IO;
using System.Text.Json;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.Core.Services;

public class LayoutManager : ILayoutManager
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
            DesktopOrganizer.Core.Utilities.Logger.Log($"Layout saved to {_layoutPath}");
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError("Failed to save layout.", ex);
        }
    }

    public void ResetLayout()
    {
        try
        {
            if (File.Exists(_layoutPath))
            {
                File.Delete(_layoutPath);
                DesktopOrganizer.Core.Utilities.Logger.Log($"Layout reset: {_layoutPath}");
            }
            CurrentLayout = new LayoutData();
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError("Failed to reset layout.", ex);
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

        double rawX = (double)(currentRect.Left - monitor.WorkArea.Left) / waWidth;
        double rawY = (double)(currentRect.Top - monitor.WorkArea.Top) / waHeight;
        double rawW = (double)currentRect.Width / waWidth;
        double rawH = (double)currentRect.Height / waHeight;

        // 幅・高さが画面を超えないように制限
        if (rawW > 1.0) rawW = 1.0;
        if (rawH > 1.0) rawH = 1.0;

        // 座標を0.0 - 1.0にクランプ (画面外への消失防止)
        // Note: rawW/rawH <= 1.0 is guaranteed above, so max (1.0 - rawW) >= 0.0
        shelf.X = Math.Clamp(rawX, 0.0, 1.0 - rawW);
        shelf.Y = Math.Clamp(rawY, 0.0, 1.0 - rawH);

        // 幅・高さも異常値を防止
        shelf.Width = Math.Clamp(rawW, 0.05, 1.0);
        shelf.Height = Math.Clamp(rawH, 0.05, 1.0);

        shelf.TargetMonitorDeviceId = monitor.DeviceName;

        Utilities.Logger.Log($"Shelf '{shelf.Title}' position updated. Norm: ({shelf.X:F3}, {shelf.Y:F3}) Monitor: {monitor.DeviceName}");
    }

    #region Profile Management

    private string ProfilesDir => Path.Combine(Path.GetDirectoryName(_layoutPath)!, "profiles");

    public List<string> GetProfileNames()
    {
        if (!Directory.Exists(ProfilesDir))
            return new List<string>();

        return Directory.GetFiles(ProfilesDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();
    }

    public void SaveProfileAs(string name)
    {
        try
        {
            Directory.CreateDirectory(ProfilesDir);
            var profilePath = Path.Combine(ProfilesDir, $"{name}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentLayout, options);
            File.WriteAllText(profilePath, json);
            Utilities.Logger.Log($"Profile saved: {profilePath}");
        }
        catch (Exception ex)
        {
            Utilities.Logger.LogError($"Failed to save profile '{name}'.", ex);
        }
    }

    public void LoadProfile(string name)
    {
        try
        {
            var profilePath = Path.Combine(ProfilesDir, $"{name}.json");
            if (!File.Exists(profilePath))
            {
                Utilities.Logger.Log($"Profile not found: {profilePath}");
                return;
            }

            var json = File.ReadAllText(profilePath);
            CurrentLayout = JsonSerializer.Deserialize<LayoutData>(json) ?? new LayoutData();
            Utilities.Logger.Log($"Profile loaded: {profilePath}");

            // Also save as current layout
            SaveLayout();
        }
        catch (Exception ex)
        {
            Utilities.Logger.LogError($"Failed to load profile '{name}'.", ex);
        }
    }

    public void DeleteProfile(string name)
    {
        try
        {
            var profilePath = Path.Combine(ProfilesDir, $"{name}.json");
            if (File.Exists(profilePath))
            {
                File.Delete(profilePath);
                Utilities.Logger.Log($"Profile deleted: {profilePath}");
            }
        }
        catch (Exception ex)
        {
            Utilities.Logger.LogError($"Failed to delete profile '{name}'.", ex);
        }
    }

    #region Backup & Restore

    public void ExportLayout(string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentLayout, options);
            File.WriteAllText(filePath, json);
            Utilities.Logger.Log($"Layout exported to: {filePath}");
        }
        catch (Exception ex)
        {
            Utilities.Logger.LogError($"Failed to export layout to '{filePath}'.", ex);
            throw; // Re-throw to let UI handle the error message if needed
        }
    }

    public void ImportLayout(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Utilities.Logger.Log($"Backup file not found: {filePath}");
                return;
            }

            var json = File.ReadAllText(filePath);
            CurrentLayout = JsonSerializer.Deserialize<LayoutData>(json) ?? new LayoutData();
            Utilities.Logger.Log($"Layout imported from: {filePath}");

            // Persist immediately as the current layout
            SaveLayout();
        }
        catch (Exception ex)
        {
            Utilities.Logger.LogError($"Failed to import layout from '{filePath}'.", ex);
            throw;
        }
    }

    #endregion

    #endregion
}
