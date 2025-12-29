using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.Core.Models;

public class MonitorItem
{
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty; // 必要に応じてDisplayConfigで実装予定。現在はDeviceNameを使用。
    public NativeMethods.RECT Bounds { get; set; }
    public NativeMethods.RECT WorkArea { get; set; }
    public bool IsPrimary { get; set; }
    public double DpiScaleX { get; set; } = 1.0;
    public double DpiScaleY { get; set; } = 1.0;
    public IntPtr Handle { get; set; }
}
