using System.Windows;
using DesktopOrganizer.Core.Services;
using System.Diagnostics;

namespace DesktopOrganizer.UI;

public partial class App : System.Windows.Application
{
    private MonitorService _monitorService = new();
    private LayoutManager _layoutManager = new();
    private List<OverlayWindow> _windows = new();

    private Services.InputService _inputService = new();
    private Services.TaskTrayIcon _taskTrayIcon = new();
    private bool _isEditMode = false;

    private Mutex? _mutex;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        const string mutexName = "Global\\DesktopOrganizer_SingleInstance_Mutex";
        bool createdNew;
        _mutex = new Mutex(true, mutexName, out createdNew);

        if (!createdNew)
        {
            DesktopOrganizer.Core.Utilities.Logger.Log("Application is already running. Shutting down.");
            Shutdown();
            return;
        }

        DesktopOrganizer.Core.Utilities.Logger.Initialize(); // Overwrite log on startup
        DesktopOrganizer.Core.Utilities.Logger.Log("Application Started.");

        // 1. Unhandled Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError("Unhandled AppDomain Exception", args.ExceptionObject as Exception);
        };
        DispatcherUnhandledException += (s, args) =>
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError("Unhandled Dispatcher Exception", args.Exception);
        };

        // 2. Initialize Services
        _inputService = new DesktopOrganizer.UI.Services.InputService();
        // Hotkey registration is done in UpdateMonitors (requires Window Handle)

        _monitorService = new DesktopOrganizer.Core.Services.MonitorService();
        _layoutManager = new DesktopOrganizer.Core.Services.LayoutManager();

        _taskTrayIcon = new DesktopOrganizer.UI.Services.TaskTrayIcon();
        _taskTrayIcon.ToggleEditModeRequested += (s, args) => ToggleEditMode();
        _taskTrayIcon.CreateShelfRequested += OnCreateShelfRequested;
        _taskTrayIcon.CreateTypedShelfRequested += OnCreateTypedShelfRequested;
        _taskTrayIcon.ExitRequested += (s, args) => Shutdown();
        _taskTrayIcon.Initialize();

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // 3. Monitor Detection & UI Setup
        UpdateMonitors();

        DesktopOrganizer.Core.Utilities.Logger.Log("Startup Sequence Completed.");
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("Display settings changed. Updating monitors...");
        UpdateMonitors();
    }

    private void UpdateMonitors()
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("Updating Monitors...");
        var monitors = _monitorService.GetMonitors();
        DesktopOrganizer.Core.Utilities.Logger.Log($"Detected {monitors.Count} monitors.");
        foreach (var m in monitors)
        {
            DesktopOrganizer.Core.Utilities.Logger.Log($"Monitor Detected: {m}");
        }

        // Close existing overlays
        foreach (var window in _windows)
        {
            window.Close();
        }
        _windows.Clear();

        // Load layout data
        _layoutManager.LoadLayout();
        DesktopOrganizer.Core.Utilities.Logger.Log($"Layout Loaded. Shelves: {_layoutManager.CurrentLayout.Shelves.Count}");

        // Demo Data if empty
        if (_layoutManager.CurrentLayout.Shelves.Count == 0 && monitors.Count > 0)
        {
            var demoShelf = new Core.Models.Shelf
            {
                Title = "Tools",
                X = 0.1,
                Y = 0.1,
                Width = 0.2,
                Height = 0.4,
                Items = new List<Core.Models.ShelfItem>
                {
                    new Core.Models.ShelfItem { Title = "Notepad", Type = Core.Models.ShelfItemType.Shortcut },
                    new Core.Models.ShelfItem { Title = "Browser", Type = Core.Models.ShelfItemType.Shortcut }
                }
            };
            _layoutManager.CurrentLayout.Shelves.Add(demoShelf);
            _layoutManager.SaveLayout();
            DesktopOrganizer.Core.Utilities.Logger.Log("Demo shelf added and layout saved.");
        }

        // Create Overlay for EACH monitor
        foreach (var monitor in monitors)
        {
            CreateOverlayForMonitor(monitor, monitors);
        }

        if (_windows.Count > 0)
        {
            _inputService.Register(_windows[0]);
            // Re-bind event to avoid duplication if called multiple times? 
            // Logic in InputService handles Handle property change if register called again.
            // But event subscription in App.xaml.cs:
            _inputService.ToggleEditModeRequested -= OnToggleEditModeHandler;
            _inputService.ToggleEditModeRequested += OnToggleEditModeHandler;
        }
    }

    private void OnToggleEditModeHandler(object? sender, EventArgs e) => ToggleEditMode();

    private void CreateOverlayForMonitor(Core.Models.MonitorItem monitor, List<Core.Models.MonitorItem> allMonitors)
    {
        var left = monitor.Bounds.Left / monitor.DpiScaleX;
        var top = monitor.Bounds.Top / monitor.DpiScaleY;
        var width = monitor.Bounds.Width / monitor.DpiScaleX;
        var height = monitor.Bounds.Height / monitor.DpiScaleY;

        var window = new OverlayWindow
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Title = $"Overlay - {monitor.DeviceName}"
        };

        var vm = new ViewModels.OverlayViewModel();

        foreach (var shelfModel in _layoutManager.CurrentLayout.Shelves)
        {
            var bestMonitor = _layoutManager.FindBestMonitor(shelfModel, allMonitors);

            if (bestMonitor.DeviceName == monitor.DeviceName)
            {
                var physRect = _layoutManager.CalculatePhysicalRect(shelfModel, monitor);

                var shelfVm = new ViewModels.ShelfViewModel(shelfModel, _layoutManager.SaveLayout)
                {
                    Left = (physRect.Left - monitor.Bounds.Left) / monitor.DpiScaleX,
                    Top = (physRect.Top - monitor.Bounds.Top) / monitor.DpiScaleY,
                    Width = physRect.Width / monitor.DpiScaleX,
                    Height = physRect.Height / monitor.DpiScaleY
                };

                shelfVm.ShelfMoved += (l, t, w, h) =>
                {
                    var pLeft = (int)(l * monitor.DpiScaleX) + monitor.Bounds.Left;
                    var pTop = (int)(t * monitor.DpiScaleY) + monitor.Bounds.Top;
                    var pWidth = (int)(w * monitor.DpiScaleX);
                    var pHeight = (int)(h * monitor.DpiScaleY);

                    var rect = new Core.Interop.NativeMethods.RECT
                    {
                        Left = pLeft,
                        Top = pTop,
                        Right = pLeft + pWidth,
                        Bottom = pTop + pHeight
                    };

                    _layoutManager.UpdateShelfPosition(shelfModel, rect, monitor);
                    _layoutManager.SaveLayout();
                };

                shelfVm.DeleteRequested += (s, args) =>
                {
                    vm.Shelves.Remove(shelfVm);
                    _layoutManager.CurrentLayout.Shelves.Remove(shelfModel);
                    _layoutManager.SaveLayout();
                };

                shelfVm.RenameRequested += (s, args) =>
                {
                    var dialog = new Controls.RenameDialog(shelfVm.Title);
                    if (dialog.ShowDialog() == true)
                    {
                        shelfVm.Title = dialog.ResultName;
                        _layoutManager.SaveLayout();
                    }
                };

                vm.AddShelf(shelfVm);
            }
        }

        vm.CreateShelfRequested += (s, pos) => CreateShelfInternal(monitor, pos, vm);
        vm.CreateTypedShelfRequested += (s, args) => CreateTypedShelfInternal(monitor, args.Position, vm, args.Type);
        vm.ToggleEditModeRequested += (s, args) => ToggleEditMode();
        vm.ResetAllRequested += (s, args) => ResetAll();

        window.RequestExitEditMode += (s, args) =>
        {
            if (_isEditMode) ToggleEditMode();
        };

        window.DataContext = vm;
        window.Show();
        _windows.Add(window);

        // Restore Edit Mode state
        window.SetEditMode(_isEditMode);
    }

    private void ToggleEditMode()
    {
        _isEditMode = !_isEditMode;
        DesktopOrganizer.Core.Utilities.Logger.Log($"Toggle Edit Mode: {_isEditMode}");
        foreach (var window in _windows)
        {
            window.SetEditMode(_isEditMode);
            if (window.DataContext is ViewModels.OverlayViewModel vm)
            {
                vm.IsEditMode = _isEditMode;
            }
        }
    }

    /// <summary>
    /// すべてのシェルと設定をリセットし、初期状態に戻す
    /// </summary>
    private void ResetAll()
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("Reset All Requested.");

        // 1. レイアウトデータをクリア
        _layoutManager.CurrentLayout.Shelves.Clear();

        // 2. レイアウトファイルを削除
        _layoutManager.ResetLayout();

        // 3. UIを更新（モニターを再スキャンしてオーバーレイを再作成）
        UpdateMonitors();

        DesktopOrganizer.Core.Utilities.Logger.Log("Reset All Completed.");
    }

    private void CreateShelfInternal(Core.Models.MonitorItem monitor, System.Windows.Point? position, ViewModels.OverlayViewModel vm)
    {
        // Calculate Position
        double x, y;
        if (position.HasValue)
        {
            // Convert Point (Relative to Window) to Normalized WorkArea
            var pX = position.Value.X * monitor.DpiScaleX + monitor.Bounds.Left;
            var pY = position.Value.Y * monitor.DpiScaleY + monitor.Bounds.Top;

            var waWidth = monitor.WorkArea.Width;
            var waHeight = monitor.WorkArea.Height;
            if (waWidth == 0) waWidth = 1920;
            if (waHeight == 0) waHeight = 1080;

            x = (pX - monitor.WorkArea.Left) / waWidth;
            y = (pY - monitor.WorkArea.Top) / waHeight;
        }
        else
        {
            x = 0.4;
            y = 0.4;
        }

        var newShelf = new Core.Models.Shelf
        {
            Title = "New Shelf",
            X = x,
            Y = y,
            Width = 0.2,
            Height = 0.2,
            Items = new List<Core.Models.ShelfItem>(),
            TargetMonitorDeviceId = monitor.DeviceName,
            ZIndex = vm.Shelves.Any() ? vm.Shelves.Max(s => s.ZIndex) + 1 : 0
        };

        // LayoutManagerに追加
        _layoutManager.CurrentLayout.Shelves.Add(newShelf);

        var physRect = _layoutManager.CalculatePhysicalRect(newShelf, monitor);

        var shelfVm = new ViewModels.ShelfViewModel(newShelf, _layoutManager.SaveLayout)
        {
            Left = (physRect.Left - monitor.Bounds.Left) / monitor.DpiScaleX,
            Top = (physRect.Top - monitor.Bounds.Top) / monitor.DpiScaleY,
            Width = physRect.Width / monitor.DpiScaleX,
            Height = physRect.Height / monitor.DpiScaleY
        };

        shelfVm.ShelfMoved += (l, t, w, h) =>
        {
            var pLeft = (int)(l * monitor.DpiScaleX) + monitor.Bounds.Left;
            var pTop = (int)(t * monitor.DpiScaleY) + monitor.Bounds.Top;
            var pWidth = (int)(w * monitor.DpiScaleX);
            var pHeight = (int)(h * monitor.DpiScaleY);

            var rect = new Core.Interop.NativeMethods.RECT
            {
                Left = pLeft,
                Top = pTop,
                Right = pLeft + pWidth,
                Bottom = pTop + pHeight
            };

            _layoutManager.UpdateShelfPosition(newShelf, rect, monitor);
            _layoutManager.SaveLayout();
        };

        shelfVm.DeleteRequested += (s, args) =>
        {
            vm.Shelves.Remove(shelfVm);
            _layoutManager.CurrentLayout.Shelves.Remove(newShelf);
            _layoutManager.SaveLayout();
        };

        shelfVm.RenameRequested += (s, args) =>
        {
            var dialog = new Controls.RenameDialog(shelfVm.Title);
            if (dialog.ShowDialog() == true)
            {
                shelfVm.Title = dialog.ResultName;
                _layoutManager.SaveLayout();
            }
        };

        vm.AddShelf(shelfVm);
        _layoutManager.SaveLayout();

        if (!_isEditMode)
        {
            ToggleEditMode();
        }
    }

    private void OnCreateShelfRequested(object? sender, EventArgs e)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("Create Shelf Requested.");
        // 最初のウィンドウ（通常はプライマリ）を取得
        var targetWindow = _windows.FirstOrDefault();
        if (targetWindow == null || targetWindow.DataContext is not ViewModels.OverlayViewModel vm) return;

        var monitors = _monitorService.GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == targetWindow.Title.Replace("Overlay - ", "")) ?? monitors.First();

        CreateShelfInternal(monitor, null, vm);
    }

    private void OnCreateTypedShelfRequested(object? sender, Core.Models.ShelfType shelfType)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"Create Typed Shelf Requested: {shelfType}");
        var targetWindow = _windows.FirstOrDefault();
        if (targetWindow == null || targetWindow.DataContext is not ViewModels.OverlayViewModel vm) return;

        var monitors = _monitorService.GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == targetWindow.Title.Replace("Overlay - ", "")) ?? monitors.First();

        CreateTypedShelfInternal(monitor, null, vm, shelfType);
    }

    private void CreateTypedShelfInternal(Core.Models.MonitorItem monitor, System.Windows.Point? position, ViewModels.OverlayViewModel vm, Core.Models.ShelfType shelfType)
    {
        string? directoryPath = null;

        // スマートシェルの場合はフォルダ選択ダイアログを表示
        if (shelfType == Core.Models.ShelfType.SmartFolder)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "同期するフォルダを選択してください"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return; // キャンセル時は何もしない
            }
            directoryPath = dialog.SelectedPath;
        }

        // タイプ別のタイトルを設定
        var title = shelfType switch
        {
            Core.Models.ShelfType.SmartFolder => System.IO.Path.GetFileName(directoryPath) ?? "スマートシェル",
            Core.Models.ShelfType.Recents => "最近使ったファイル",
            Core.Models.ShelfType.Temp => "一時保管 (24h)",
            Core.Models.ShelfType.Memo => "クイックメモ",
            _ => "New Shelf"
        };

        // タイプ別のテーマカラーを設定
        var themeColor = shelfType switch
        {
            Core.Models.ShelfType.SmartFolder => "#CC224466", // ブルーグレー
            Core.Models.ShelfType.Recents => "#CC004466", // ダークブルー
            Core.Models.ShelfType.Temp => "#CC664400", // オレンジ
            Core.Models.ShelfType.Memo => "#CC446600", // オリーブ
            _ => "#CC1E1E24"
        };

        double x, y;
        if (position.HasValue)
        {
            var pX = position.Value.X * monitor.DpiScaleX + monitor.Bounds.Left;
            var pY = position.Value.Y * monitor.DpiScaleY + monitor.Bounds.Top;
            var waWidth = monitor.WorkArea.Width;
            var waHeight = monitor.WorkArea.Height;
            if (waWidth == 0) waWidth = 1920;
            if (waHeight == 0) waHeight = 1080;
            x = (pX - monitor.WorkArea.Left) / waWidth;
            y = (pY - monitor.WorkArea.Top) / waHeight;
        }
        else
        {
            x = 0.4;
            y = 0.4;
        }

        var newShelf = new Core.Models.Shelf
        {
            Title = title,
            Type = shelfType,
            ThemeColor = themeColor,
            DirectoryPath = directoryPath, // スマートシェル用
            X = x,
            Y = y,
            Width = 0.2,
            Height = 0.2,
            Items = new List<Core.Models.ShelfItem>(),
            TargetMonitorDeviceId = monitor.DeviceName,
            ZIndex = vm.Shelves.Any() ? vm.Shelves.Max(s => s.ZIndex) + 1 : 0
        };

        _layoutManager.CurrentLayout.Shelves.Add(newShelf);

        var physRect = _layoutManager.CalculatePhysicalRect(newShelf, monitor);

        var shelfVm = new ViewModels.ShelfViewModel(newShelf, _layoutManager.SaveLayout)
        {
            Left = (physRect.Left - monitor.Bounds.Left) / monitor.DpiScaleX,
            Top = (physRect.Top - monitor.Bounds.Top) / monitor.DpiScaleY,
            Width = physRect.Width / monitor.DpiScaleX,
            Height = physRect.Height / monitor.DpiScaleY
        };

        shelfVm.ShelfMoved += (l, t, w, h) =>
        {
            var pLeft = (int)(l * monitor.DpiScaleX) + monitor.Bounds.Left;
            var pTop = (int)(t * monitor.DpiScaleY) + monitor.Bounds.Top;
            var pWidth = (int)(w * monitor.DpiScaleX);
            var pHeight = (int)(h * monitor.DpiScaleY);

            var rect = new Core.Interop.NativeMethods.RECT
            {
                Left = pLeft,
                Top = pTop,
                Right = pLeft + pWidth,
                Bottom = pTop + pHeight
            };

            _layoutManager.UpdateShelfPosition(newShelf, rect, monitor);
            _layoutManager.SaveLayout();
        };

        shelfVm.DeleteRequested += (s, args) =>
        {
            vm.Shelves.Remove(shelfVm);
            _layoutManager.CurrentLayout.Shelves.Remove(newShelf);
            _layoutManager.SaveLayout();
        };

        vm.AddShelf(shelfVm);
        _layoutManager.SaveLayout();

        if (!_isEditMode)
        {
            ToggleEditMode();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("Application Exiting...");
        _inputService.Dispose();
        _taskTrayIcon.Dispose();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnExit(e);
    }
}
