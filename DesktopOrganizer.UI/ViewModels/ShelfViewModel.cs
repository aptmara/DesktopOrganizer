using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using DesktopOrganizer.Core.Models;

namespace DesktopOrganizer.UI.ViewModels;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ShelfViewModel : ViewModelBase
{
    private readonly Shelf _model;

    private ObservableCollection<ShelfItemViewModel> _items = new();
    public ObservableCollection<ShelfItemViewModel> Items => _items;

    private readonly Action? _saveLayoutAction;

    public ShelfViewModel(Shelf model, Action? saveLayoutAction = null)
    {
        _model = model;
        _saveLayoutAction = saveLayoutAction;
        foreach (var item in model.Items)
        {
            _items.Add(new ShelfItemViewModel(item));
        }
    }

    public void SavePosition()
    {
        // プロパティからモデルを更新（getter/setterで同期済みだが、正規化は外部で実施）
        // Viewは論理ピクセルを持つ。Modelは正規化座標を持つ。
        // 保存前にModelの正規化座標を更新する必要がある。
        // モニターサイズの知識が必要なため、コールバックで委譲する。

        _saveLayoutAction?.Invoke();
    }

    // 戦略変更:
    // ViewModelプロパティ(Left/Top)はViewバインディング専用。
    // 移動終了時に、現在のView矩形を渡すコールバックを発火させる。
    // アプリレベルで正規化座標に戻して保存する。

    public event Action<double, double, double, double>? ShelfMoved;

    public void OnMoved()
    {
        ShelfMoved?.Invoke(Left, Top, Width, Height);
    }

    public string Title
    {
        get => _model.Title;
        set { _model.Title = value; OnPropertyChanged(); }
    }

    // WPF論理ピクセル(DIPs)での座標
    private double _left;
    public double Left
    {
        get => _left;
        set { _left = value; OnPropertyChanged(); }
    }

    private double _top;
    public double Top
    {
        get => _top;
        set { _top = value; OnPropertyChanged(); }
    }

    private double _width;
    public double Width
    {
        get => _width;
        set { _width = value; OnPropertyChanged(); }
    }

    private double _height;
    public double Height
    {
        get => _height;
        set { _height = value; OnPropertyChanged(); }
    }

    public void AddFile(string path)
    {
        // 簡易的な型検出
        var type = ShelfItemType.File;
        if (Directory.Exists(path)) type = ShelfItemType.Folder;
        else if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Shortcut;
        else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Executable;
        else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Url;

        var item = new ShelfItem
        {
            Title = Path.GetFileNameWithoutExtension(path),
            TargetPath = path,
            Type = type,
            OriginalIconPath = path // 抽出用設定
        };

        _model.Items.Add(item);
        _items.Add(new ShelfItemViewModel(item));

        // 保存をトリガー
        OnMoved();
    }

    public void RemoveItem(ShelfItemViewModel item)
    {
        _items.Remove(item);

        // モデルからも削除
        // ViewModelに対応するModel項目を検索して削除
        var modelItem = _model.Items.FirstOrDefault(i => i.TargetPath == item.TargetPath && i.Title == item.Title);
        if (modelItem != null)
        {
            _model.Items.Remove(modelItem);
        }

        _saveLayoutAction?.Invoke();
    }
}

public class ShelfItemViewModel : ViewModelBase
{
    private readonly ShelfItem _model;

    public string Title => _model.Title;
    public string TargetPath => _model.TargetPath;

    private ImageSource? _icon;
    public ImageSource? Icon
    {
        get
        {
            if (_icon == null && !string.IsNullOrEmpty(_model.TargetPath))
            {
                _icon = DesktopOrganizer.UI.Utilities.IconUtilities.GetIconFromPath(_model.TargetPath);
            }
            return _icon;
        }
    }

    public ShelfItemViewModel(ShelfItem model)
    {
        _model = model;
    }

    public bool IsBroken
    {
        get
        {
            if (string.IsNullOrEmpty(_model.TargetPath)) return true;
            return !File.Exists(_model.TargetPath) && !Directory.Exists(_model.TargetPath);
        }
    }
}
