using Microsoft.Win32;
using System.Diagnostics;

namespace DesktopOrganizer.Core.Services;

public class StartupManager
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DesktopOrganizer";

    public bool IsAutoStartEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public void SetAutoStart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
        if (key == null) return;

        if (enable)
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                // .NET 6/8+ app might be wrapped, verify this path. For now assuming main exe.
                // If it's a dll, we need special handling, but typical WPF app publish produces exe.
                key.SetValue(AppName, $"\"{exePath}\"");
            }
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
