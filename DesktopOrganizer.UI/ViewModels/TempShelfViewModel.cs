using DesktopOrganizer.Core.Models;
using System.IO;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// 一時保管シェルフ。期限切れアイテムを自動削除する。
/// </summary>
public class TempShelfViewModel : ShelfViewModelBase
{
    private System.Windows.Threading.DispatcherTimer? _expirationTimer;

    public TempShelfViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
        InitializeTempShelf();
    }

    private void InitializeTempShelf()
    {
        // 既存アイテムをロード（期限切れは除外）
        var now = DateTime.Now;
        foreach (var item in _model.Items.ToList())
        {
            if (item.ExpiresAt.HasValue && item.ExpiresAt.Value <= now)
            {
                // 期限切れ: モデルから削除
                _model.Items.Remove(item);
            }
            else
            {
                _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
            }
        }

        // 1分ごとに期限切れチェック
        _expirationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _expirationTimer.Tick += OnExpirationTimerTick;
        _expirationTimer.Start();
    }

    private void OnExpirationTimerTick(object? sender, EventArgs e)
    {
        PruneExpiredItems();
    }

    /// <summary>
    /// 期限切れアイテムを削除
    /// </summary>
    public void PruneExpiredItems()
    {
        var now = DateTime.Now;
        var expiredVMs = _items.Where(vm =>
        {
            var modelItem = _model.Items.FirstOrDefault(i => i.Id == vm.Id);
            return modelItem?.ExpiresAt.HasValue == true && modelItem.ExpiresAt.Value <= now;
        }).ToList();

        foreach (var vm in expiredVMs)
        {
            RemoveItemInternal(vm);
        }

        if (expiredVMs.Count > 0)
        {
            _saveLayoutAction?.Invoke();
        }
    }

    /// <summary>
    /// 一時保管棚にアイテムを追加（有効期限を設定）
    /// </summary>
    public void AddTempItem(string path, TimeSpan? expiresIn = null)
    {
        var expiration = DateTime.Now.Add(expiresIn ?? TimeSpan.FromHours(24));

        var type = ShelfItemType.File;
        if (Directory.Exists(path)) type = ShelfItemType.Folder;
        else if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Shortcut;
        else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Executable;

        var item = new ShelfItem
        {
            Title = Path.GetFileNameWithoutExtension(path),
            TargetPath = path,
            Type = type,
            OriginalIconPath = path,
            ExpiresAt = expiration
        };

        _model.Items.Add(item);
        _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
        _saveLayoutAction?.Invoke();
    }

    public override void AddFile(string path)
    {
        AddTempItem(path, null); // デフォルト24時間
        OnMoved(); // Save trigger
    }

    public override void Dispose()
    {
        _expirationTimer?.Stop();
        base.Dispose();
    }
}
