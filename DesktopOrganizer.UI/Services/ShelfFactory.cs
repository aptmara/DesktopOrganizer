using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.UI.ViewModels;
using DesktopOrganizer.UI.Controls;

namespace DesktopOrganizer.UI.Services;

/// <summary>
/// シェルフ作成のファクトリクラス。
/// シェルフViewModel/Modelの生成と初期設定を担当。
/// </summary>
public class ShelfFactory
{
    private readonly ILayoutManager _layoutManager;
    private readonly IMonitorService _monitorService;

    public ShelfFactory(ILayoutManager layoutManager, IMonitorService monitorService)
    {
        _layoutManager = layoutManager;
        _monitorService = monitorService;
    }

    /// <summary>
    /// 通常シェルフを作成する
    /// </summary>
    public (Shelf Model, ShelfViewModel ViewModel) CreateShelf(
        MonitorItem monitor,
        System.Windows.Point? position,
        OverlayViewModel overlayVm)
    {
        var (x, y) = CalculateNormalizedPosition(monitor, position);

        var newShelf = new Shelf
        {
            Title = "New Shelf",
            X = x,
            Y = y,
            Width = 0.2,
            Height = 0.2,
            Items = new List<ShelfItem>(),
            TargetMonitorDeviceId = monitor.DeviceName,
            ZIndex = overlayVm.Shelves.Any() ? overlayVm.Shelves.Max(s => s.ZIndex) + 1 : 0
        };

        var shelfVm = CreateViewModel(newShelf, monitor);
        return (newShelf, shelfVm);
    }

    /// <summary>
    /// タイプ指定シェルフを作成する
    /// </summary>
    public (Shelf Model, ShelfViewModel ViewModel)? CreateTypedShelf(
        MonitorItem monitor,
        System.Windows.Point? position,
        OverlayViewModel overlayVm,
        ShelfType shelfType)
    {
        string? directoryPath = null;

        // スマートシェルの場合はフォルダ選択ダイアログを表示
        if (shelfType == ShelfType.SmartFolder)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "同期するフォルダを選択してください"
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return null; // キャンセル時は何もしない
            }
            directoryPath = dialog.SelectedPath;
        }

        // タイプ別のタイトルを設定
        var title = shelfType switch
        {
            ShelfType.SmartFolder => System.IO.Path.GetFileName(directoryPath) ?? "スマートシェル",
            ShelfType.Recents => "最近使ったファイル",
            ShelfType.Temp => "一時保管 (24h)",
            ShelfType.Memo => "クイックメモ",
            _ => "New Shelf"
        };

        // タイプ別のテーマカラーを設定
        var themeColor = shelfType switch
        {
            ShelfType.SmartFolder => "#CC224466", // ブルーグレー
            ShelfType.Recents => "#CC004466",     // ダークブルー
            ShelfType.Temp => "#CC664400",        // オレンジ
            ShelfType.Memo => "#CC446600",        // オリーブ
            _ => "#CC1E1E24"
        };

        var (x, y) = CalculateNormalizedPosition(monitor, position);

        var newShelf = new Shelf
        {
            Title = title,
            Type = shelfType,
            ThemeColor = themeColor,
            DirectoryPath = directoryPath,
            X = x,
            Y = y,
            Width = 0.2,
            Height = 0.2,
            Items = new List<ShelfItem>(),
            TargetMonitorDeviceId = monitor.DeviceName,
            ZIndex = overlayVm.Shelves.Any() ? overlayVm.Shelves.Max(s => s.ZIndex) + 1 : 0
        };

        var shelfVm = CreateViewModel(newShelf, monitor);
        return (newShelf, shelfVm);
    }

    /// <summary>
    /// 既存モデルからViewModelを作成する
    /// </summary>
    public ShelfViewModel CreateViewModel(Shelf shelfModel, MonitorItem monitor)
    {
        var physRect = _layoutManager.CalculatePhysicalRect(shelfModel, monitor);

        return new ShelfViewModel(shelfModel, _layoutManager.SaveLayout)
        {
            Left = (physRect.Left - monitor.Bounds.Left) / monitor.DpiScaleX,
            Top = (physRect.Top - monitor.Bounds.Top) / monitor.DpiScaleY,
            Width = physRect.Width / monitor.DpiScaleX,
            Height = physRect.Height / monitor.DpiScaleY
        };
    }

    /// <summary>
    /// 正規化座標を計算する
    /// </summary>
    private (double X, double Y) CalculateNormalizedPosition(MonitorItem monitor, System.Windows.Point? position)
    {
        if (position.HasValue)
        {
            var pX = position.Value.X * monitor.DpiScaleX + monitor.Bounds.Left;
            var pY = position.Value.Y * monitor.DpiScaleY + monitor.Bounds.Top;

            var waWidth = monitor.WorkArea.Width;
            var waHeight = monitor.WorkArea.Height;
            if (waWidth == 0) waWidth = 1920;
            if (waHeight == 0) waHeight = 1080;

            return ((pX - monitor.WorkArea.Left) / waWidth, (pY - monitor.WorkArea.Top) / waHeight);
        }

        return (0.4, 0.4);
    }
}
