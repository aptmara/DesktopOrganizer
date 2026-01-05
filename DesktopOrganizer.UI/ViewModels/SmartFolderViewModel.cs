using DesktopOrganizer.Core.Models;
using System.IO;
using System.Timers;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// 指定されたフォルダの内容を同期・監視するシェルフ。
/// </summary>
public class SmartFolderViewModel : ShelfViewModelBase
{
    private FileSystemWatcher? _watcher;
    private readonly System.Object _eventLock = new System.Object();
    private readonly List<FileSystemEventArgs> _pendingEvents = new List<FileSystemEventArgs>();
    private System.Timers.Timer? _debounceTimer;

    public SmartFolderViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
        InitializeSmartShelf();
    }

    private void InitializeSmartShelf()
    {
        _watcher?.Dispose();
        _items.Clear();
        _model.Items.Clear();

        if (string.IsNullOrEmpty(DirectoryPath) || !Directory.Exists(DirectoryPath))
        {
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
        }
        catch (Exception)
        {
            // Log error
        }
    }

    private bool IsFileVisible(string filePath)
    {
        if (string.IsNullOrWhiteSpace(FilterPattern)) return true;
        if (FilterPattern == "*.*" || FilterPattern == "*") return true;

        var patterns = FilterPattern.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var fileName = Path.GetFileName(filePath);

        foreach (var pattern in patterns)
        {
            var p = pattern.Trim();
            // Simple wildcard match
            if (p.StartsWith("*"))
            {
                var ext = p.Substring(1); // e.g., ".jpg"
                if (filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
            }
            else
            {
                if (string.Equals(fileName, p, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    protected override void OnFilterPatternChanged()
    {
        InitializeSmartShelf();
    }

    private void SyncFromDirectory()
    {
        if (string.IsNullOrEmpty(DirectoryPath)) return;

        try
        {
            // First get all files, then filter manually to support multiple patterns easier than Directory.GetFiles
            var files = Directory.GetFiles(DirectoryPath);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _items.Clear();
                _model.Items.Clear();
                foreach (var file in files)
                {
                    if (IsFileVisible(file))
                    {
                        AddFileInternal(file);
                    }
                }
                SortItems();
            });
        }
        catch { }
    }

    private void SetupDebounceTimer()
    {
        if (_debounceTimer != null) return;
        _debounceTimer = new System.Timers.Timer(200); // 200ms debounce
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Pre-filter events if convenient, but safer to filter in processing
        lock (_eventLock)
        {
            _pendingEvents.Add(e);
            SetupDebounceTimer();
            _debounceTimer!.Stop();
            _debounceTimer!.Start();
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(e.OldFullPath)!, Path.GetFileName(e.OldFullPath)));
        OnFileChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)));
    }

    private void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        List<FileSystemEventArgs> eventsToProcess;
        lock (_eventLock)
        {
            eventsToProcess = new List<FileSystemEventArgs>(_pendingEvents);
            _pendingEvents.Clear();
        }

        if (eventsToProcess.Count == 0) return;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (eventsToProcess.Count > 50)
            {
                SyncFromDirectory();
            }
            else
            {
                foreach (var ev in eventsToProcess)
                {
                    bool isVisible = IsFileVisible(ev.FullPath);

                    if (ev.ChangeType == WatcherChangeTypes.Created)
                    {
                        if (isVisible && !_items.Any(i => i.TargetPath == ev.FullPath))
                        {
                            AddFileInternal(ev.FullPath);
                        }
                    }
                    else if (ev.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        // Even if not visible now (maybe pattern changed?), remove if present
                        var vm = _items.FirstOrDefault(i => i.TargetPath == ev.FullPath);
                        if (vm != null) RemoveItemInternal(vm);
                    }
                }
                SortItems();
            }
        });
    }

    public override void AddFile(string path)
    {
        // Smart Shelf forbids drop
    }

    public override void RemoveItem(ShelfItemViewModel item)
    {
        // 確認ダイアログ
        var result = System.Windows.MessageBox.Show(
            $"ファイル '{item.Title}' を完全に削除しますか？\nこの操作は元に戻せません。",
            "ファイルの削除確認",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                if (File.Exists(item.TargetPath))
                {
                    File.Delete(item.TargetPath);
                }
                else if (Directory.Exists(item.TargetPath))
                {
                    Directory.Delete(item.TargetPath, true);
                }

                // Watcherが検知する前にUIから消しておくことでレスポンスを良くする
                // Watcherのイベントハンドラ側でも重複削除チェックがあるため問題ない想定
                base.RemoveItem(item);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"削除に失敗しました: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        _debounceTimer?.Dispose();
        base.Dispose();
    }
}
