using System.Windows.Forms;
using System.Drawing;

namespace DesktopOrganizer.UI.Services;

public class TaskTrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;

    public event EventHandler? ToggleEditModeRequested;
    public event EventHandler? CreateShelfRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        _contextMenu = new ContextMenuStrip();
        var editItem = new ToolStripMenuItem("Toggle Edit Mode (Ctrl+Alt+Space)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Toggle Edit Mode requested");
            ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
        });
        var createShelfItem = new ToolStripMenuItem("Create New Shelf", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create New Shelf requested");
            CreateShelfRequested?.Invoke(this, EventArgs.Empty);
        });
        var exitItem = new ToolStripMenuItem("Exit", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Exit requested");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        });

        _contextMenu.Items.Add(editItem);
        _contextMenu.Items.Add(createShelfItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // Fallback icon, ideally use app resource
            Visible = true,
            Text = "Desktop Organizer",
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) => ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowNotification(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_contextMenu != null)
        {
            _contextMenu.Dispose();
            _contextMenu = null;
        }
    }
}
