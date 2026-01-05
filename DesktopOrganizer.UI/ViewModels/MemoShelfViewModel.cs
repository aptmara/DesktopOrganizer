using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// クイックメモシェルフ。
/// テキストメモをアイテムとして管理する。
/// </summary>
public class MemoShelfViewModel : ShelfViewModelBase
{
    public MemoShelfViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
        InitializeMemoShelf();
    }

    private void InitializeMemoShelf()
    {
        // 既存メモをロード
        foreach (var item in _model.Items)
        {
            _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
        }
    }

    /// <summary>メモシェルかどうかを示すフラグ（XAMLバインディング用）</summary>
    public bool IsMemoShelf => true;

    /// <summary>
    /// 新しいメモを追加
    /// </summary>
    public void AddMemo(string title = "新しいメモ", string content = "")
    {
        var item = new ShelfItem
        {
            Title = title,
            MemoContent = content,
            Type = ShelfItemType.Memo
        };

        _model.Items.Add(item);
        _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
        _saveLayoutAction?.Invoke();
    }

    public override void AddFile(string path)
    {
        // メモ棚へのファイルドロップは、とりあえずファイルへのリンクとして扱うか、
        // テキストファイルなら中身を展開するか。
        // ここでは「ファイルリンクメモ」として追加する（Manual相当）
        base.AddFileInternal(path);
        OnMoved();
    }
}
