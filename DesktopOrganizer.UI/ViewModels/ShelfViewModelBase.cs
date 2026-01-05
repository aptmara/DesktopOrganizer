using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.Utilities;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// シェルフViewModelの基底クラス。
/// 共通のプロパティ・メソッドを提供する。
/// </summary>
public abstract class ShelfViewModelBase : ViewModelBase, IDisposable
{
    protected readonly Shelf _model;
    protected readonly Action? _saveLayoutAction;
    protected ObservableCollection<ShelfItemViewModel> _items = new();
    private ICollectionView? _filteredItemsView;

    // Global Settings for Grid Snap
    public static double GridSize { get; set; } = 20.0;
    public static bool IsGridSnapEnabled { get; set; } = false;

    protected ShelfViewModelBase(Shelf model, Action? saveLayoutAction = null)
    {
        _model = model;
        _saveLayoutAction = saveLayoutAction;

        // Initialize ViewModel items from Model items
        foreach (var item in _model.Items)
        {
            _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
        }

        // Setup CollectionView for filtering
        _filteredItemsView = CollectionViewSource.GetDefaultView(_items);
        _filteredItemsView.Filter = FilterItem;
    }

    public virtual void Dispose()
    {
        // 派生クラスでオーバーライド
    }

    public ObservableCollection<ShelfItemViewModel> Items => _items;

    /// <summary>
    /// フィルタリングされたアイテムビュー（検索用）
    /// </summary>
    public ICollectionView FilteredItems => _filteredItemsView ??= CollectionViewSource.GetDefaultView(_items);

    private string _searchQuery = string.Empty;
    /// <summary>
    /// 検索クエリ
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                _filteredItemsView?.Refresh();
            }
        }
    }

    private bool FilterItem(object obj)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery)) return true;
        if (obj is ShelfItemViewModel item)
        {
            return item.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    #region イベント

    public event Action<double, double, double, double>? ShelfMoved;
    public event EventHandler? DeleteRequested;


    public void OnMoved() => ShelfMoved?.Invoke(Left, Top, Width, Height);

    public void RequestDelete()
    {
        Dispose();
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestRename() => IsRenaming = true;

    #endregion

    #region プロパティ

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set { _isRenaming = value; OnPropertyChanged(); }
    }

    public ICommand EndRenameCommand => new RelayCommand(() => IsRenaming = false);

    /// <summary>
    /// このシェルがメモシェルかどうか（UIバインディング用）
    /// 基底クラスではfalse。派生クラスでオーバーライドする。
    /// </summary>
    public virtual bool IsMemoShelf => false;

    public string Title
    {
        get => _model.Title;
        set { _model.Title = value; OnPropertyChanged(); _saveLayoutAction?.Invoke(); }
    }

    private double _left;
    public double Left
    {
        get => _left;
        set
        {
            if (_left != value)
            {
                _left = value;
                OnPropertyChanged();
            }
        }
    }

    private double _top;
    public double Top
    {
        get => _top;
        set
        {
            if (_top != value)
            {
                _top = value;
                OnPropertyChanged();
            }
        }
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

    public int ZIndex
    {
        get => _model.ZIndex;
        set
        {
            if (_model.ZIndex != value)
            {
                _model.ZIndex = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public bool IsCollapsed
    {
        get => _model.IsCollapsed;
        set
        {
            if (_model.IsCollapsed != value)
            {
                _model.IsCollapsed = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public ICommand ToggleCollapsedCommand => new RelayCommand(() => IsCollapsed = !IsCollapsed);

    public ShelfSortOption SortOption
    {
        get => _model.SortOption;
        set
        {
            if (_model.SortOption != value)
            {
                _model.SortOption = value;
                OnPropertyChanged();
                SortItems();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public bool IsGhostModeEnabled
    {
        get => _model.IsGhostModeEnabled;
        set
        {
            if (_model.IsGhostModeEnabled != value)
            {
                _model.IsGhostModeEnabled = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public bool IsSearchEnabled
    {
        get => _model.IsSearchEnabled;
        set
        {
            if (_model.IsSearchEnabled != value)
            {
                _model.IsSearchEnabled = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public Core.Models.ShelfDisplayMode DisplayMode
    {
        get => _model.DisplayMode;
        set
        {
            if (_model.DisplayMode != value)
            {
                _model.DisplayMode = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public double IconSize
    {
        get => _model.IconSize;
        set
        {
            if (Math.Abs(_model.IconSize - value) > 0.1)
            {
                _model.IconSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ItemWidth));
                OnPropertyChanged(nameof(ItemHeight));
                _saveLayoutAction?.Invoke();
            }
        }
    }

    // Dynamic sizing based on IconSize
    // Base padding + proportional padding for larger icons
    // Width needs to accommodate text (at least 80px for short names)
    public double ItemWidth => Math.Max(80, IconSize + 32 + (IconSize * 0.2));
    // Height includes icon + 2 lines of text (~36px) + padding
    public double ItemHeight => IconSize + 58 + (IconSize * 0.1);

    public string ThemeColor
    {
        get => _model.ThemeColor;
        set
        {
            if (_model.ThemeColor != value)
            {
                _model.ThemeColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public Brush BackgroundBrush
    {
        get
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(_model.ThemeColor);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC1E1E24"));
            }
        }
    }

    public string? DirectoryPath
    {
        get => _model.DirectoryPath;
        set
        {
            if (_model.DirectoryPath != value)
            {
                _model.DirectoryPath = value;
                OnPropertyChanged();
                OnDirectoryPathChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public string? FilterPattern
    {
        get => _model.FilterPattern;
        set
        {
            if (_model.FilterPattern != value)
            {
                _model.FilterPattern = value;
                OnPropertyChanged();
                OnFilterPatternChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    protected virtual void OnFilterPatternChanged() { }

    public ShelfType ShelfType => _model.Type;

    #endregion

    #region 仮想メソッド（派生クラスでオーバーライド）

    /// <summary>
    /// DirectoryPath変更時のフック
    /// </summary>
    protected virtual void OnDirectoryPathChanged() { }

    /// <summary>
    /// アイテムをソートする（差分更新で再描画を最小化）
    /// </summary>
    protected virtual void SortItems()
    {
        if (SortOption == ShelfSortOption.None) return;

        List<ShelfItemViewModel> sorted = SortOption switch
        {
            ShelfSortOption.Name => _items.OrderBy(i => i.Title).ToList(),
            ShelfSortOption.DateModified => _items.OrderByDescending(i =>
            {
                try { return System.IO.File.Exists(i.TargetPath) ? System.IO.File.GetLastWriteTime(i.TargetPath) : DateTime.MinValue; }
                catch { return DateTime.MinValue; }
            }).ToList(),
            ShelfSortOption.Type => _items.OrderBy(i => System.IO.Path.GetExtension(i.TargetPath)).ThenBy(i => i.Title).ToList(),
            _ => _items.ToList()
        };

        // 差分更新: Move操作のみでソート（再描画を最小化）
        for (int targetIndex = 0; targetIndex < sorted.Count; targetIndex++)
        {
            var item = sorted[targetIndex];
            int currentIndex = _items.IndexOf(item);
            if (currentIndex != targetIndex)
            {
                _items.Move(currentIndex, targetIndex);
            }
        }
    }

    #endregion

    #region アイテム管理

    /// <summary>
    /// ファイルをアイテムとして内部追加する
    /// </summary>
    protected void AddFileInternal(string path)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"[AddFileInternal] Adding file: {path}");

        // ショートカットの解決を試みる（引数なしの場合のみ）
        string resolvedPath = path;
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var target = ShortcutHelper.ResolveShortcut(path);
            if (target != null)
            {
                resolvedPath = target;
            }
        }

        // 解決後のパスでタイプ判定
        var type = ShelfItemType.File;
        if (System.IO.Directory.Exists(resolvedPath)) type = ShelfItemType.Folder;
        else if (resolvedPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Shortcut;
        else if (resolvedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Executable;
        else if (resolvedPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Url;

        var item = new ShelfItem
        {
            Title = System.IO.Path.GetFileNameWithoutExtension(path), // タイトルは元のファイル名を使用
            TargetPath = resolvedPath,
            Type = type,
            OriginalIconPath = resolvedPath // アイコンも解決後のパスから取得
        };

        _model.Items.Add(item);
        var vm = new ShelfItemViewModel(item, _saveLayoutAction);
        _items.Add(vm);
        DesktopOrganizer.Core.Utilities.Logger.Log($"[AddFileInternal] Added item: {item.Title}, Items count: {_items.Count}");
    }

    /// <summary>
    /// アイテムを内部削除する
    /// </summary>
    protected void RemoveItemInternal(ShelfItemViewModel vm)
    {
        vm.Dispose();
        _items.Remove(vm);
        var modelItem = _model.Items.FirstOrDefault(i => i.TargetPath == vm.TargetPath);
        if (modelItem != null) _model.Items.Remove(modelItem);
    }

    /// <summary>
    /// モデルからアイテムViewModelを追加する（Strategy連携用）
    /// </summary>
    public void AddItemFromModel(ShelfItem item, Action? saveLayoutAction)
    {
        _items.Add(new ShelfItemViewModel(item, saveLayoutAction));
    }

    /// <summary>
    /// 外部からファイルを追加（ドラッグ＆ドロップ）
    /// </summary>
    public virtual void AddFile(string path)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"[AddFile] Called with path: {path}");
        AddFileInternal(path);

        // Notify that the collection changed and layout needs refresh
        OnPropertyChanged(nameof(Items));

        OnMoved();
    }

    /// <summary>
    /// アイテムの並び順を変更
    /// </summary>
    /// <summary>
    /// アイテムの並び順を変更
    /// </summary>
    public void MoveItem(ShelfItemViewModel source, ShelfItemViewModel target)
    {
        int oldIndex = _items.IndexOf(source);
        int newIndex = _items.IndexOf(target);

        if (oldIndex != -1 && newIndex != -1)
        {
            MoveItem(source, newIndex);
        }
    }

    /// <summary>
    /// アイテムを指定インデックスに移動
    /// </summary>
    public void MoveItem(ShelfItemViewModel source, int newIndex)
    {
        int oldIndex = _items.IndexOf(source);
        if (oldIndex < 0 || newIndex < 0 || newIndex >= _items.Count) return;

        if (oldIndex != newIndex)
        {
            _items.Move(oldIndex, newIndex);
            var modelSource = _model.Items[oldIndex];
            _model.Items.RemoveAt(oldIndex);

            // モデル側も同期 (Move後のインデックスで挿入)
            // ObservableCollection.Move doesn't affect underlying list usually if manually synced?
            // Wait, logic above was:
            // _model.Items.RemoveAt(oldIndex);
            // _model.Items.Insert(newIndex, modelSource);
            // But ObservableCollection.Move handles the shift.
            // Model (List<ShelfItem>) needs manual update.

            _model.Items.Insert(newIndex, modelSource);
            _saveLayoutAction?.Invoke();
        }
    }

    /// <summary>
    /// アイテムを削除（外部公開）
    /// </summary>
    public virtual void RemoveItem(ShelfItemViewModel item)
    {
        item.Dispose();
        _items.Remove(item);
        var modelItem = _model.Items.FirstOrDefault(i => i.TargetPath == item.TargetPath && i.Title == item.Title);
        if (modelItem != null) _model.Items.Remove(modelItem);

        // Notify UI to refresh
        OnPropertyChanged(nameof(Items));

        _saveLayoutAction?.Invoke();
    }

    /// <summary>
    /// 他棚からアイテムを受け入れる
    /// </summary>
    public virtual void AcceptItem(ShelfItemViewModel sourceItem)
    {
        var newItem = new ShelfItem
        {
            Title = sourceItem.Title,
            TargetPath = sourceItem.TargetPath,
            Type = ShelfItemType.File,
            OriginalIconPath = sourceItem.TargetPath
        };

        if (System.IO.Directory.Exists(sourceItem.TargetPath))
            newItem.Type = ShelfItemType.Folder;
        else if (sourceItem.TargetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            newItem.Type = ShelfItemType.Shortcut;
        else if (sourceItem.TargetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            newItem.Type = ShelfItemType.Executable;
        else if (sourceItem.TargetPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
            newItem.Type = ShelfItemType.Url;

        _model.Items.Add(newItem);
        _items.Add(new ShelfItemViewModel(newItem, _saveLayoutAction));
        _saveLayoutAction?.Invoke();
    }

    #endregion
}
