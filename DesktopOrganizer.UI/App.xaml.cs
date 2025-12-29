using System.Windows;
using DesktopOrganizer.Core.Services;

namespace DesktopOrganizer.UI;

public partial class App : System.Windows.Application
{
    private readonly MonitorService _monitorService = new();
    private readonly LayoutManager _layoutManager = new();
    private readonly List<OverlayWindow> _windows = new();

    private readonly Services.InputService _inputService = new();
    private readonly Services.TaskTrayIcon _trayIcon = new();
    private bool _isEditMode = false;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        _layoutManager.LoadLayout();

        // データが空の場合初期データをシード設定
        if (_layoutManager.CurrentLayout.Shelves.Count == 0)
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
        }

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _trayIcon.Initialize();
        _trayIcon.ToggleEditModeRequested += OnToggleEditMode;
        _trayIcon.CreateShelfRequested += OnCreateShelfRequested;
        _trayIcon.ExitRequested += (s, args) => Shutdown();

        InitializeOverlayWindows();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // 単純な再読み込み戦略: すべて閉じて再作成
        foreach (var window in _windows)
        {
            window.Close();
        }
        _windows.Clear();

        // 新しいウィンドウを設定
        InitializeOverlayWindows();
    }

    private void InitializeOverlayWindows()
    {
        var monitors = _monitorService.GetMonitors();

        foreach (var monitor in monitors)
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
                var bestMonitor = _layoutManager.FindBestMonitor(shelfModel, monitors);

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

            window.DataContext = vm;
            window.Show();
            _windows.Add(window);

            // 表示後にモードを復元
            window.SetEditMode(_isEditMode);
        }

        if (_windows.Count > 0)
        {
            // プライマリまたは最初のウィンドウに登録
            _inputService.Register(_windows[0]);

            // 多重登録防止
            _inputService.ToggleEditModeRequested -= OnToggleEditMode;
            _inputService.ToggleEditModeRequested += OnToggleEditMode;
        }
    }

    private void OnCreateShelfRequested(object? sender, EventArgs e)
    {
        // 最初のウィンドウ（通常はプライマリ）を取得
        var targetWindow = _windows.FirstOrDefault();
        if (targetWindow == null || targetWindow.DataContext is not ViewModels.OverlayViewModel vm) return;

        // モニタ情報の取得はMonitorServiceからの方が確実だが、
        // ここではWindowに関連付いているLayoutManagerのロジックを再利用したい。
        // 新規棚データ作成
        var newShelf = new Core.Models.Shelf
        {
            Title = "New Shelf",
            X = 0.4, // Center-ish
            Y = 0.4,
            Width = 0.2,
            Height = 0.2,
            Items = new List<Core.Models.ShelfItem>()
        };

        // LayoutManagerに追加
        _layoutManager.CurrentLayout.Shelves.Add(newShelf);
        var monitors = _monitorService.GetMonitors();
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == targetWindow.Title.Replace("Overlay - ", "")) ?? monitors.First();

        // 配置ロジック再利用（既存コードの抽出が必要だが、ここではインラインで複製して実装）
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

        // 編集モードをONにする
        if (!_isEditMode)
        {
            OnToggleEditMode(this, EventArgs.Empty);
        }
    }

    private void OnToggleEditMode(object? sender, EventArgs e)
    {
        _isEditMode = !_isEditMode;
        foreach (var window in _windows)
        {
            window.SetEditMode(_isEditMode);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _inputService.Dispose();
        _trayIcon.Dispose();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnExit(e);
    }
}
