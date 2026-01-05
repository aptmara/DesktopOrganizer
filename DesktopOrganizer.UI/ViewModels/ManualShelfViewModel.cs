using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// 手動管理のシェルフ。
/// ユーザーが自由にアイテムを追加・削除・並べ替え可能。
/// </summary>
public class ManualShelfViewModel : ShelfViewModelBase
{
    public ManualShelfViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
    }

    // ManualShelfは基本機能(AddFile, RemoveItem, MoveItem)をそのまま使用
}
