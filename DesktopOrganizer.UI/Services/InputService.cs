using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using DesktopOrganizer.Core.Interop;

namespace DesktopOrganizer.UI.Services;

public class InputService : IDisposable
{
    private const int HOTKEY_ID = 9000;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_SPACE = 0x20;

    private HwndSource? _source;
    public event EventHandler? ToggleEditModeRequested;

    public void Register(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(HwndHook);

        RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_SPACE);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void Dispose()
    {
        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _source.RemoveHook(HwndHook);
            _source = null;
        }
    }
}
