using System.Windows;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Services;

/// <summary>
/// オーバーレイウィンドウのライフサイクル管理サービス。
/// モニター検出結果に応じてウィンドウの作成・破棄を担当。
/// </summary>
public class OverlayWindowManager
{
    private readonly Dictionary<string, (OverlayWindow Window, OverlayViewModel ViewModel)> _overlays = new();
    private readonly ILayoutManager _layoutManager;
    private readonly IMonitorService _monitorService;
    public OverlayWindowManager(
        ILayoutManager layoutManager,
        IMonitorService monitorService)
    {
        _layoutManager = layoutManager;
        _monitorService = monitorService;
    }

    /// <summary>
    /// 現在のオーバーレイ辞書
    /// </summary>
    public IReadOnlyDictionary<string, (OverlayWindow Window, OverlayViewModel ViewModel)> Overlays => _overlays;

    /// <summary>
    /// モニター情報に基づいてオーバーレイウィンドウを更新する
    /// </summary>
    public List<MonitorItem> UpdateOverlays()
    {
        var monitors = _monitorService.GetMonitors();

        // 現在の OverlayWindow を全て閉じるか、差分管理するか。
        // ここでは差分管理を行う（複雑だが再描画を減らす）
        var currentDeviceIds = new HashSet<string>(_overlays.Keys);
        var newDeviceIds = new HashSet<string>(monitors.Select(m => m.DeviceName));

        // 削除: 存在しなくなったモニター
        foreach (var id in currentDeviceIds.Except(newDeviceIds))
        {
            if (_overlays.TryGetValue(id, out var overlay))
            {
                overlay.Window.Close();
                _overlays.Remove(id);
            }
        }

        // 追加/更新: 新しいまたは既存モニター
        foreach (var monitor in monitors)
        {
            if (!_overlays.ContainsKey(monitor.DeviceName))
            {
                CreateOverlayForMonitor(monitor);
            }
            else
            {
                // モニターサイズ変更対応（位置・サイズ更新）
                UpdateOverlayPosition(_overlays[monitor.DeviceName].Window, monitor);
            }
        }

        return monitors;
    }

    /// <summary>
    /// 特定モニター用のオーバーレイウィンドウを作成
    /// </summary>
    private void CreateOverlayForMonitor(MonitorItem monitor)
    {
        var overlayVm = new OverlayViewModel();
        var overlayWindow = new OverlayWindow
        {
            DataContext = overlayVm
        };

        // ウィンドウ位置設定（DPI考慮）
        UpdateOverlayPosition(overlayWindow, monitor);

        overlayWindow.Show();
        _overlays[monitor.DeviceName] = (overlayWindow, overlayVm);
    }

    /// <summary>
    /// オーバーレイウィンドウの位置・サイズを更新
    /// </summary>
    private void UpdateOverlayPosition(OverlayWindow window, MonitorItem monitor)
    {
        window.Left = monitor.Bounds.Left / monitor.DpiScaleX;
        window.Top = monitor.Bounds.Top / monitor.DpiScaleY;
        window.Width = monitor.Bounds.Width / monitor.DpiScaleX;
        window.Height = monitor.Bounds.Height / monitor.DpiScaleY;
    }

    /// <summary>
    /// シェルフをオーバーレイに配置する
    /// </summary>
    public void PlaceShelfOnOverlay(ShelfViewModelBase shelfVm, MonitorItem monitor)
    {
        if (_overlays.TryGetValue(monitor.DeviceName, out var overlay))
        {
            overlay.ViewModel.AddShelf(shelfVm);
        }
    }

    /// <summary>
    /// 編集モードを全オーバーレイに適用
    /// </summary>
    public void SetEditMode(bool isEditMode)
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.ViewModel.IsEditMode = isEditMode;
            // 注意: ウィンドウスタイルの変更はApp.xaml.cs側に残る（Window Handle必要）
        }
    }

    /// <summary>
    /// 全オーバーレイをクリア
    /// </summary>
    public void ClearAll()
    {
        foreach (var overlay in _overlays.Values)
        {
            foreach (var shelf in overlay.ViewModel.Shelves.ToList())
            {
                shelf.Dispose();
            }
            overlay.ViewModel.Shelves.Clear();
        }
    }

    /// <summary>
    /// 全オーバーレイウィンドウを閉じる
    /// </summary>
    public void CloseAll()
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.Window.Close();
        }
        _overlays.Clear();
    }
}
