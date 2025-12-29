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

public class ShelfViewModel : ViewModelBase, IDisposable
{
    private readonly Shelf _model;

    public void Dispose()
    {
        _watcher?.Dispose();
    }

    private ObservableCollection<ShelfItemViewModel> _items = new();
    public ObservableCollection<ShelfItemViewModel> Items => _items;

    private readonly Action? _saveLayoutAction;

    public ShelfViewModel(Shelf model, Action? saveLayoutAction = null)
    {
        _model = model;
        _saveLayoutAction = saveLayoutAction;

        DesktopOrganizer.Core.Utilities.Logger.Log($"Initializing ShelfViewModel: {model.Title} (Path: {model.DirectoryPath ?? "null"})");

        if (!string.IsNullOrEmpty(model.DirectoryPath))
        {
            // 既存アイテムはクリアして再同期（整合性確保）
            InitializeSmartShelf();
        }
        else
        {
            foreach (var item in model.Items)
            {
                _items.Add(new ShelfItemViewModel(item));
            }
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
    public event EventHandler? DeleteRequested;
    public event EventHandler? RenameRequested;

    public void OnMoved()
    {
        ShelfMoved?.Invoke(Left, Top, Width, Height);
    }

    public void RequestDelete()
    {
        Dispose();
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }
    public void RequestRename() => RenameRequested?.Invoke(this, EventArgs.Empty);

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

    // Phase 3: Theming
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

    public System.Windows.Media.Brush BackgroundBrush
    {
        get
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_model.ThemeColor);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch
            {
                return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CC1E1E24"));
            }
        }
    }

    // Phase 3: Smart Shelf
    public string? DirectoryPath
    {
        get => _model.DirectoryPath;
        set
        {
            if (_model.DirectoryPath != value)
            {
                _model.DirectoryPath = value;
                OnPropertyChanged();
                InitializeSmartShelf();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    private FileSystemWatcher? _watcher;

    private void InitializeSmartShelf()
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"InitializeSmartShelf: {DirectoryPath}");
        _watcher?.Dispose();
        _items.Clear();
        _model.Items.Clear();

        if (string.IsNullOrEmpty(DirectoryPath) || !Directory.Exists(DirectoryPath))
        {
            DesktopOrganizer.Core.Utilities.Logger.Log($"Directory not found: {DirectoryPath}");
            return;
        }

        // 初期同期
        SyncFromDirectory();

        // 監視開始
        try
        {
            _watcher = new FileSystemWatcher(DirectoryPath);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.EnableRaisingEvents = true;
            DesktopOrganizer.Core.Utilities.Logger.Log("FileSystemWatcher Started.");
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError("Failed to start FileSystemWatcher", ex);
        }
    }

    private void SyncFromDirectory()
    {
        if (string.IsNullOrEmpty(DirectoryPath)) return;

        var files = Directory.GetFiles(DirectoryPath);
        // 更新頻度が高い場合のパフォーマンスを考慮し、一旦クリアして再構築（簡易実装）
        // 本来はDiffを取るべきだが、アイテム数が少なめと想定。

        // UIスレッドで実行
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _items.Clear();
            _model.Items.Clear();
            foreach (var file in files)
            {
                AddFileInternal(file);
            }
        });
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        DesktopOrganizer.Core.Utilities.Logger.Log($"FileChanged: {e.ChangeType} - {e.FullPath}");
        // 頻繁な更新を防ぐためのデバウンスが必要かもしれないが、まずは直接同期呼び出し
        // 実際にはファイルのロック等で失敗する可能性があるため、少し遅延させると良いが、
        // ここではシンプルに再同期をかける。
        // 個別の追加・削除を行う方が効率的。

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                AddFileInternal(e.FullPath);
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                var vm = _items.FirstOrDefault(i => i.TargetPath == e.FullPath);
                if (vm != null) RemoveItemInternal(vm);
            }
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var vm = _items.FirstOrDefault(i => i.TargetPath == e.OldFullPath);
            if (vm != null) RemoveItemInternal(vm);
            AddFileInternal(e.FullPath);
        });
    }

    private void AddFileInternal(string path)
    {
        // 簡易的な型検出
        var type = ShelfItemType.File;
        // ... (AddFileロジックの再利用) ...
        // AddFileメソッドのリファクタが必要。
        // ここではAddFileのロジックをコピーしておく（後で共通化推奨）

        if (Directory.Exists(path)) type = ShelfItemType.Folder;
        else if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Shortcut;
        else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Executable;
        else if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) type = ShelfItemType.Url;

        var item = new ShelfItem
        {
            Title = Path.GetFileNameWithoutExtension(path),
            TargetPath = path,
            Type = type,
            OriginalIconPath = path
        };

        _model.Items.Add(item);
        _items.Add(new ShelfItemViewModel(item));
    }

    private void RemoveItemInternal(ShelfItemViewModel vm)
    {
        _items.Remove(vm);
        var modelItem = _model.Items.FirstOrDefault(i => i.TargetPath == vm.TargetPath);
        if (modelItem != null) _model.Items.Remove(modelItem);
    }

    // Public method for Drag&Drop (Manual Add)
    // Smart Shelfの場合は無視するか、コピーするか。
    // 今回は「Smart Shelfなら何もしない（Watcherに任せる）」とする。
    public void AddFile(string path)
    {
        if (!string.IsNullOrEmpty(DirectoryPath))
        {
            // Smart Shelfの場合、実ファイルをコピーまたは移動する必要があるが、
            // UX的にドラッグでファイル移動は慎重に行うべき。
            // ここでは未実装（手動追加不可）とする。
            return;
        }

        AddFileInternal(path);
        OnMoved(); // Save trigger
    }

    public void MoveItem(ShelfItemViewModel source, ShelfItemViewModel target)
    {
        int oldIndex = _items.IndexOf(source);
        int newIndex = _items.IndexOf(target);

        if (oldIndex != -1 && newIndex != -1)
        {
            _items.Move(oldIndex, newIndex);

            // Modelも同期
            var modelSource = _model.Items[oldIndex];
            _model.Items.RemoveAt(oldIndex);
            _model.Items.Insert(newIndex, modelSource);

            _saveLayoutAction?.Invoke();
        }
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
    public ImageSource? Icon => _icon;

    public ShelfItemViewModel(ShelfItem model)
    {
        _model = model;
        LoadIconAsync();
    }

    private void LoadIconAsync()
    {
        if (string.IsNullOrEmpty(_model.TargetPath)) return;

        Task.Run(() =>
        {
            var icon = DesktopOrganizer.UI.Utilities.IconUtilities.GetIconFromPath(_model.TargetPath);
            if (icon != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _icon = icon;
                    OnPropertyChanged(nameof(Icon));
                });
            }
        });
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
