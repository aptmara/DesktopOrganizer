using System.Runtime.InteropServices;
using DesktopOrganizer.Core.Interop;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.Core.Services;

public class MonitorService : IMonitorService
{
    public List<MonitorItem> GetMonitors()
    {
        var monitors = new List<MonitorItem>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData)
            {
                var mi = new NativeMethods.MONITORINFOEX();
                mi.Size = Marshal.SizeOf(mi);

                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    uint dpiX = 96, dpiY = 96;
                    try
                    {
                        NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.Monitor_DPI_Type.MDT_Effective_DPI, out dpiX, out dpiY);
                    }
                    catch
                    {
                        // 失敗した場合は無視（古いOSなど）
                    }

                    monitors.Add(new MonitorItem
                    {
                        Handle = hMonitor,
                        DeviceName = mi.DeviceName,
                        Bounds = mi.Monitor,
                        WorkArea = mi.WorkArea,
                        IsPrimary = (mi.Flags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                        DpiScaleX = dpiX / 96.0,
                        DpiScaleY = dpiY / 96.0
                    });
                }
                return true;
            }, IntPtr.Zero);

        return monitors;
    }
}
