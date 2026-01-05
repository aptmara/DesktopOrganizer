using DesktopOrganizer.Core.Models;
using DesktopOrganizer.Core.Services;
using DesktopOrganizer.UI.ViewModels;

namespace DesktopOrganizer.UI.Services;

/// <summary>
/// ShelfViewModelのファクトリ。
/// シェルフタイプに応じた適切なViewModelを生成する。
/// </summary>
public class ShelfViewModelFactory
{
    private readonly ILayoutManager _layoutManager;

    public ShelfViewModelFactory(ILayoutManager layoutManager)
    {
        _layoutManager = layoutManager;
    }

    /// <summary>
    /// シェルフモデルからViewModelを生成する
    /// </summary>
    /// <param name="model">シェルフモデル</param>
    /// <param name="monitor">対象モニター</param>
    /// <param name="saveLayoutAction">レイアウト保存アクション</param>
    /// <returns>生成されたViewModel</returns>
    public ShelfViewModelBase Create(
        Shelf model,
        MonitorItem? monitor = null,
        Action? saveLayoutAction = null)
    {
        ShelfViewModelBase viewModel = model.Type switch
        {
            ShelfType.SmartFolder => new SmartFolderViewModel(model, saveLayoutAction),
            ShelfType.Recents => new RecentsShelfViewModel(model, saveLayoutAction),
            ShelfType.Temp => new TempShelfViewModel(model, saveLayoutAction),
            ShelfType.Memo => new MemoShelfViewModel(model, saveLayoutAction),
            ShelfType.Clock => new ClockShelfViewModel(model, saveLayoutAction),
            ShelfType.AnalogClock => new AnalogClockShelfViewModel(model, saveLayoutAction),
            _ => new ManualShelfViewModel(model, saveLayoutAction)
        };

        // モニターが指定されている場合、物理座標を計算
        if (monitor != null)
        {
            var rect = _layoutManager.CalculatePhysicalRect(model, monitor);
            // OverlayWindowはBounds(0,0)基準で配置されているため、ViewModelのLeft/TopもBounds基準にする必要がある
            viewModel.Left = (rect.Left - monitor.Bounds.Left) / monitor.DpiScaleX;
            viewModel.Top = (rect.Top - monitor.Bounds.Top) / monitor.DpiScaleY;
            viewModel.Width = rect.Width / monitor.DpiScaleX;
            viewModel.Height = rect.Height / monitor.DpiScaleY;
        }

        return viewModel;
    }

    /// <summary>
    /// 新規シェルフを作成する
    /// </summary>
    /// <param name="type">シェルフタイプ</param>
    /// <param name="title">タイトル</param>
    /// <param name="position">正規化座標位置</param>
    /// <param name="saveLayoutAction">レイアウト保存アクション</param>
    /// <returns>モデルとViewModelのタプル</returns>
    public (Shelf Model, ShelfViewModelBase ViewModel) CreateNew(
        ShelfType type,
        string title,
        (double X, double Y) position,
        Action? saveLayoutAction = null)
    {
        var model = new Shelf
        {
            Title = title,
            Type = type,
            X = position.X,
            Y = position.Y,
            Width = 0.15,  // 15% of screen
            Height = 0.2   // 20% of screen
        };

        // ViewModel生成はCreateメソッドに委譲
        var viewModel = Create(model, null, saveLayoutAction);

        return (model, viewModel);
    }
}
