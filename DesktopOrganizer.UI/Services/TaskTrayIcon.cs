using System.Windows.Forms;
using System.Drawing;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.Services;

public class TaskTrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;

    public event EventHandler? ToggleEditModeRequested;
    public event EventHandler? CreateShelfRequested;
    public event EventHandler<ShelfType>? CreateTypedShelfRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Renderer = new TaskTrayMenuRenderer(); // Apply Dark Theme

        var editItem = new ToolStripMenuItem("🛠  編集モード切替 (Ctrl+Alt+Space)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Toggle Edit Mode requested");
            ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
        });

        // 新規シェル作成サブメニュー
        var createShelfMenu = new ToolStripMenuItem("➕  新規シェルの作成");

        var createManualItem = new ToolStripMenuItem("📁  通常のシェル", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Manual Shelf requested");
            CreateShelfRequested?.Invoke(this, EventArgs.Empty);
        });

        var createSmartItem = new ToolStripMenuItem("📂  スマートシェル (フォルダ同期)...", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Smart Shelf requested");
            CreateTypedShelfRequested?.Invoke(this, ShelfType.SmartFolder);
        });

        var createRecentsItem = new ToolStripMenuItem("🕒  最近使ったファイル", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Recents Shelf requested");
            CreateTypedShelfRequested?.Invoke(this, ShelfType.Recents);
        });

        var createTempItem = new ToolStripMenuItem("⏳  一時保管シェル (24h)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Temp Shelf requested");
            CreateTypedShelfRequested?.Invoke(this, ShelfType.Temp);
        });

        var createMemoItem = new ToolStripMenuItem("📝  クイックメモ", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Memo Shelf requested");
            CreateTypedShelfRequested?.Invoke(this, ShelfType.Memo);
        });

        createShelfMenu.DropDownItems.Add(createManualItem);
        createShelfMenu.DropDownItems.Add(createSmartItem);
        createShelfMenu.DropDownItems.Add(new ToolStripSeparator());
        createShelfMenu.DropDownItems.Add(createRecentsItem);
        createShelfMenu.DropDownItems.Add(createTempItem);
        createShelfMenu.DropDownItems.Add(createMemoItem);

        var exitItem = new ToolStripMenuItem("❌  終了", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Exit requested");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        });

        _contextMenu.Items.Add(editItem);
        _contextMenu.Items.Add(createShelfMenu);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application, // Fallback icon, ideally use app resource
            Visible = true,
            Text = "デスクトップ整理ツール", // Localized Title
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
