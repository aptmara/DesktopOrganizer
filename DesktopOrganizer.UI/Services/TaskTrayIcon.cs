using System.Windows.Forms;
using System.Drawing;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Services;

namespace DesktopOrganizer.UI.Services;

public class TaskTrayIcon : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private readonly ILayoutManager _layoutManager;
    private readonly StartupManager _startupManager;

    public event EventHandler? ToggleEditModeRequested;
    public event EventHandler? CreateShelfRequested;
    public event EventHandler<ShelfType>? CreateTypedShelfRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<string>? LoadProfileRequested;
    public event EventHandler<string>? SaveProfileRequested;
    public event EventHandler? ToggleThemeRequested;
    public event EventHandler? ReloadLayoutRequested;

    public TaskTrayIcon(ILayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
        _startupManager = new StartupManager();
    }

    public void Initialize()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Renderer = new TaskTrayMenuRenderer(); // Apply Dark Theme

        var editItem = new ToolStripMenuItem("🛠  編集モード切替 (Ctrl+Alt+Space)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Toggle Edit Mode requested");
            ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
        });

        // スタートアップ設定
        var startupItem = new ToolStripMenuItem("🚀  Windows起動時に実行", null, (s, e) =>
        {
            var item = (ToolStripMenuItem)s!;
            item.Checked = !item.Checked;
            _startupManager.SetAutoStart(item.Checked);
            DesktopOrganizer.Core.Utilities.Logger.Log($"Tray: Auto-Start set to {item.Checked}");
        });
        startupItem.Checked = _startupManager.IsAutoStartEnabled;

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

        var createClockItem = new ToolStripMenuItem("🕐  時計ウィジェット (デジタル)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Clock Widget requested");
            CreateTypedShelfRequested?.Invoke(this, ShelfType.Clock);
        });

        var createAnalogClockItem = new ToolStripMenuItem("🕐  時計ウィジェット (アナログ)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Create Analog Clock Widget requested");
            CreateTypedShelfRequested?.Invoke(this, ShelfType.AnalogClock);
        });

        createShelfMenu.DropDownItems.Add(createManualItem);
        createShelfMenu.DropDownItems.Add(createSmartItem);
        createShelfMenu.DropDownItems.Add(new ToolStripSeparator());
        createShelfMenu.DropDownItems.Add(createRecentsItem);
        createShelfMenu.DropDownItems.Add(createTempItem);
        createShelfMenu.DropDownItems.Add(createMemoItem);
        createShelfMenu.DropDownItems.Add(new ToolStripSeparator());
        createShelfMenu.DropDownItems.Add(createClockItem);
        createShelfMenu.DropDownItems.Add(createAnalogClockItem);

        // レイアウトプロファイルサブメニュー
        var profileMenu = new ToolStripMenuItem("💾  レイアウトプロファイル");
        profileMenu.DropDownOpening += (s, e) => RefreshProfileMenu(profileMenu);

        // テーマ切替
        var themeItem = new ToolStripMenuItem("🌓  テーマ切替 (ライト/ダーク)", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Theme Toggle requested");
            ToggleThemeRequested?.Invoke(this, EventArgs.Empty);
        });

        var exitItem = new ToolStripMenuItem("❌  終了", null, (s, e) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Tray: Exit requested");
            ExitRequested?.Invoke(this, EventArgs.Empty);
        });

        _contextMenu.Items.Add(editItem);
        _contextMenu.Items.Add(startupItem);
        _contextMenu.Items.Add(createShelfMenu);
        _contextMenu.Items.Add(profileMenu);
        _contextMenu.Items.Add(themeItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        Icon trayIcon = SystemIcons.Application;
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/icon.png");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                using var bitmap = new Bitmap(stream);
                trayIcon = Icon.FromHandle(bitmap.GetHicon());
            }
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.Log($"Tray: Failed to load custom icon: {ex.Message}");
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Visible = true,
            Text = "デスクトップ整理ツール", // Localized Title
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) => ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshProfileMenu(ToolStripMenuItem profileMenu)
    {
        profileMenu.DropDownItems.Clear();

        // 現在を保存
        var saveItem = new ToolStripMenuItem("💾  現在のレイアウトを保存...", null, (s, e) =>
        {
            var dialog = new SaveProfileDialog();
            if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.ProfileName))
            {
                _layoutManager.SaveProfileAs(dialog.ProfileName);
                DesktopOrganizer.Core.Utilities.Logger.Log($"Tray: Profile saved: {dialog.ProfileName}");
            }
        });
        profileMenu.DropDownItems.Add(saveItem);
        profileMenu.DropDownItems.Add(saveItem);
        profileMenu.DropDownItems.Add(new ToolStripSeparator());

        // バックアップ操作
        var exportItem = new ToolStripMenuItem("📤  バックアップをエクスポート...", null, (s, e) =>
        {
            var sfd = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"desktop_organizer_backup_{DateTime.Now:yyyyMMdd}.json",
                Title = "レイアウトのバックアップを保存"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _layoutManager.ExportLayout(sfd.FileName);
                    ShowNotification("成功", "バックアップを保存しました。");
                }
                catch
                {
                    ShowNotification("エラー", "バックアップの保存に失敗しました。");
                }
            }
        });

        var importItem = new ToolStripMenuItem("📥  バックアップをインポート...", null, (s, e) =>
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "レイアウトのバックアップを読み込む"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                if (MessageBox.Show("現在のレイアウトは上書きされます。よろしいですか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    try
                    {
                        _layoutManager.ImportLayout(ofd.FileName);
                        ShowNotification("成功", "レイアウトを復元しました。");
                        ReloadLayoutRequested?.Invoke(this, EventArgs.Empty);
                    }
                    catch
                    {
                        ShowNotification("エラー", "読み込みに失敗しました。");
                    }
                }
            }
        });

        profileMenu.DropDownItems.Add(exportItem);
        profileMenu.DropDownItems.Add(importItem);
        profileMenu.DropDownItems.Add(new ToolStripSeparator());

        // 既存プロファイル
        var profiles = _layoutManager.GetProfileNames();
        if (profiles.Count == 0)
        {
            var emptyItem = new ToolStripMenuItem("(プロファイルなし)") { Enabled = false };
            profileMenu.DropDownItems.Add(emptyItem);
        }
        else
        {
            foreach (var profile in profiles)
            {
                var item = new ToolStripMenuItem($"📑  {profile}", null, (s, e) =>
                {
                    LoadProfileRequested?.Invoke(this, profile);
                });
                profileMenu.DropDownItems.Add(item);
            }
        }
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
