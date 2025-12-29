using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.Utilities;

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

    // Global Settings for Grid Snap
    public static double GridSize { get; set; } = 20.0;
    public static bool IsGridSnapEnabled { get; set; } = false; // Default off, allow toggle

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

        DesktopOrganizer.Core.Utilities.Logger.Log($"Initializing ShelfViewModel: {model.Title} (Type: {model.Type}, Path: {model.DirectoryPath ?? "null"})");

        // ShelfType に応じた初期化
        switch (model.Type)
        {
            case ShelfType.SmartFolder:
                if (!string.IsNullOrEmpty(model.DirectoryPath))
                {
                    InitializeSmartShelf();
                }
                break;
            case ShelfType.Recents:
                InitializeRecentsShelf();
                break;
            case ShelfType.Temp:
                InitializeTempShelf();
                break;
            case ShelfType.Memo:
                InitializeMemoShelf();
                break;
            case ShelfType.Manual:
            default:
                // DirectoryPath が設定されている場合は SmartFolder として扱う（後方互換性）
                if (!string.IsNullOrEmpty(model.DirectoryPath))
                {
                    InitializeSmartShelf();
                }
                else
                {
                    foreach (var item in model.Items)
                    {
                        _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
                    }
                }
                break;
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
    // Rename signal is handled by UI binding to IsRenaming now, 
    // but RequestRename from ContextMenu still useful to toggle it.
    public void RequestRename() => IsRenaming = true;

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set { _isRenaming = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 名前変更完了時にBehaviorから呼ばれるコマンド
    /// </summary>
    public ICommand EndRenameCommand => new RelayCommand(() => IsRenaming = false);

    public string Title
    {
        get => _model.Title;
        set { _model.Title = value; OnPropertyChanged(); _saveLayoutAction?.Invoke(); }
    }

    // WPF論理ピクセル(DIPs)での座標
    private double _left;
    public double Left
    {
        get => _left;
        set
        {
            if (_left != value)
            {
                DesktopOrganizer.Core.Utilities.Logger.Log($"ShelfVM '{Title}' Left: {_left} -> {value}");
                if (value == 0 && _left != 0)
                {
                    DesktopOrganizer.Core.Utilities.Logger.Log($"[STACKTRACE] Left set to 0: {Environment.StackTrace}");
                }
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
                DesktopOrganizer.Core.Utilities.Logger.Log($"ShelfVM '{Title}' Top: {_top} -> {value}");
                if (value == 0 && _top != 0)
                {
                    DesktopOrganizer.Core.Utilities.Logger.Log($"[STACKTRACE] Top set to 0: {Environment.StackTrace}");
                }
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

    // Phase 4: Roll-up Shelf
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

    /// <summary>
    /// 折りたたみ状態をトグルするコマンド（キーボードショートカット用）
    /// </summary>
    public ICommand ToggleCollapsedCommand => new RelayCommand(() => IsCollapsed = !IsCollapsed);

    // Phase 4: Smart Sorting
    public DesktopOrganizer.Core.Models.ShelfSortOption SortOption
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

    // Phase 4: Ghost Mode
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
            SortItems();
        });
    }

    /// <summary>
    /// アイテムを現在のSortOptionに従ってソートする。
    /// </summary>
    private void SortItems()
    {
        if (SortOption == DesktopOrganizer.Core.Models.ShelfSortOption.None) return;

        List<ShelfItemViewModel> sorted;
        switch (SortOption)
        {
            case DesktopOrganizer.Core.Models.ShelfSortOption.Name:
                sorted = _items.OrderBy(i => i.Title).ToList();
                break;
            case DesktopOrganizer.Core.Models.ShelfSortOption.DateModified:
                sorted = _items.OrderByDescending(i =>
                {
                    try
                    {
                        return File.Exists(i.TargetPath) ? File.GetLastWriteTime(i.TargetPath) : DateTime.MinValue;
                    }
                    catch { return DateTime.MinValue; }
                }).ToList();
                break;
            case DesktopOrganizer.Core.Models.ShelfSortOption.Type:
                sorted = _items.OrderBy(i => Path.GetExtension(i.TargetPath)).ThenBy(i => i.Title).ToList();
                break;
            default:
                return;
        }

        // ObservableCollectionを再構築（Move連発よりシンプルで安全）
        _items.Clear();
        foreach (var item in sorted)
        {
            _items.Add(item);
        }
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
        _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
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

    /// <summary>
    /// 他の棚からアイテムを受け入れる（棚間移動用）
    /// </summary>
    public void AcceptItem(ShelfItemViewModel sourceItem)
    {
        // Smart Shelfへの追加は不可
        if (!string.IsNullOrEmpty(DirectoryPath)) return;

        // 新しいShelfItemを作成（元のパス情報を引き継ぐ）
        var newItem = new ShelfItem
        {
            Title = sourceItem.Title,
            TargetPath = sourceItem.TargetPath,
            Type = ShelfItemType.File, // 型は簡易判定
            OriginalIconPath = sourceItem.TargetPath
        };

        // 型を正しく設定
        if (Directory.Exists(sourceItem.TargetPath))
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

    #region Recents Shelf (最近使ったファイル棚)

    /// <summary>
    /// 最近使ったファイル棚を初期化
    /// </summary>
    private void InitializeRecentsShelf()
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("InitializeRecentsShelf");
        _items.Clear();
        _model.Items.Clear();

        try
        {
            var recentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!Directory.Exists(recentsPath))
            {
                DesktopOrganizer.Core.Utilities.Logger.Log($"Recents folder not found: {recentsPath}");
                return;
            }

            // 最新のファイル/ショートカットを取得（最大20件）
            var recentFiles = new DirectoryInfo(recentsPath)
                .GetFiles("*.lnk")
                .OrderByDescending(f => f.LastWriteTime)
                .Take(20);

            foreach (var file in recentFiles)
            {
                AddFileInternal(file.FullName);
            }

            // Recents フォルダを監視
            DirectoryPath = recentsPath;
            _watcher = new FileSystemWatcher(recentsPath, "*.lnk");
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.Created += OnRecentsFileChanged;
            _watcher.Deleted += OnRecentsFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            DesktopOrganizer.Core.Utilities.Logger.LogError("Failed to initialize Recents shelf", ex);
        }
    }

    private void OnRecentsFileChanged(object sender, FileSystemEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // 再同期（シンプルに全件再取得）
            _items.Clear();
            _model.Items.Clear();
            try
            {
                var recentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
                var recentFiles = new DirectoryInfo(recentsPath)
                    .GetFiles("*.lnk")
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(20);
                foreach (var file in recentFiles)
                {
                    AddFileInternal(file.FullName);
                }
            }
            catch { }
        });
    }

    #endregion

    #region Temp Shelf (一時保管棚)

    private System.Windows.Threading.DispatcherTimer? _expirationTimer;

    /// <summary>
    /// 一時保管棚を初期化（既存アイテムをロード＋タイマー開始）
    /// </summary>
    private void InitializeTempShelf()
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("InitializeTempShelf");

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
            DesktopOrganizer.Core.Utilities.Logger.Log($"Pruned {expiredVMs.Count} expired items from Temp shelf.");
        }
    }

    /// <summary>
    /// 一時保管棚にアイテムを追加（有効期限を設定）
    /// </summary>
    public void AddTempItem(string path, TimeSpan? expiresIn = null)
    {
        if (_model.Type != ShelfType.Temp) return;

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

    #endregion

    #region Memo Shelf (クイックメモ棚)

    /// <summary>
    /// メモ棚を初期化
    /// </summary>
    private void InitializeMemoShelf()
    {
        DesktopOrganizer.Core.Utilities.Logger.Log("InitializeMemoShelf");

        // 既存メモをロード
        foreach (var item in _model.Items)
        {
            _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
        }
    }

    /// <summary>
    /// 新しいメモを追加
    /// </summary>
    public void AddMemo(string title = "新しいメモ", string content = "")
    {
        if (_model.Type != ShelfType.Memo) return;

        var item = new ShelfItem
        {
            Title = title,
            MemoContent = content,
            Type = ShelfItemType.File // アイコン用（実際にはメモアイコンを使用）
        };

        _model.Items.Add(item);
        _items.Add(new ShelfItemViewModel(item, _saveLayoutAction));
        _saveLayoutAction?.Invoke();
    }

    #endregion

    /// <summary>
    /// 棚のタイプを取得
    /// </summary>
    public ShelfType ShelfType => _model.Type;
}

public class ShelfItemViewModel : ViewModelBase
{
    private readonly ShelfItem _model;

    private readonly Action? _saveLayoutAction;

    /// <summary>
    /// アイテムの一意識別子
    /// </summary>
    public string Id => _model.Id;

    /// <summary>
    /// メモ棚用：メモの本文テキスト
    /// </summary>
    public string? MemoContent
    {
        get => _model.MemoContent;
        set
        {
            if (_model.MemoContent != value)
            {
                _model.MemoContent = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        set { _isRenaming = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// 名前変更完了時にBehaviorから呼ばれるコマンド
    /// </summary>
    public ICommand EndRenameCommand => new RelayCommand(() => IsRenaming = false);

    public string Title
    {
        get => _model.Title;
        set
        {
            if (_model.Title != value)
            {
                _model.Title = value;
                OnPropertyChanged();
                _saveLayoutAction?.Invoke();
            }
        }
    }

    public string TargetPath => _model.TargetPath;

    // Smart Shelfやファイルドロップ時の自動命名用
    public void UpdateTitle(string newTitle)
    {
        _model.Title = newTitle;
        OnPropertyChanged(nameof(Title));
    }

    private ImageSource? _icon;
    public ImageSource? Icon => _icon;

    public ShelfItemViewModel(ShelfItem model, Action? saveLayoutAction = null)
    {
        _model = model;
        _saveLayoutAction = saveLayoutAction;
        LoadIconAsync();
    }

    private void LoadIconAsync()
    {
        if (string.IsNullOrEmpty(_model.TargetPath)) return;

        Task.Run(async () =>
        {
            try
            {
                // 起動直後はファイルシステムがまだ準備できていない可能性があるため少し待機
                await Task.Delay(50);

                var icon = DesktopOrganizer.UI.Utilities.IconUtilities.GetIconFromPath(_model.TargetPath);

                // アイコン取得失敗時はリトライ（最大3回）
                for (int retry = 0; retry < 3 && icon == null; retry++)
                {
                    await Task.Delay(200 * (retry + 1));
                    icon = DesktopOrganizer.UI.Utilities.IconUtilities.GetIconFromPath(_model.TargetPath);
                }

                if (icon != null)
                {
                    // Application.Currentのnullチェック
                    var app = System.Windows.Application.Current;
                    if (app != null)
                    {
                        app.Dispatcher.Invoke(() =>
                        {
                            _icon = icon;
                            OnPropertyChanged(nameof(Icon));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DesktopOrganizer.Core.Utilities.Logger.LogError($"Failed to load icon: {_model.TargetPath}", ex);
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
