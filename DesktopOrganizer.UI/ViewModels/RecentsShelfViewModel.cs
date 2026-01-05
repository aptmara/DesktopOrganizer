using DesktopOrganizer.Core.Models;
using System.IO;

namespace DesktopOrganizer.UI.ViewModels;

/// <summary>
/// 「最近使ったファイル」を表示するシェルフ。
/// Recentフォルダを監視し、自動更新する。
/// </summary>
public class RecentsShelfViewModel : ShelfViewModelBase
{
    private FileSystemWatcher? _watcher;

    public RecentsShelfViewModel(Shelf model, Action? saveLayoutAction = null)
        : base(model, saveLayoutAction)
    {
        InitializeRecentsShelf();
    }

    private void InitializeRecentsShelf()
    {
        _items.Clear();
        _model.Items.Clear();

        try
        {
            var recentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (!Directory.Exists(recentsPath)) return;

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
        catch (Exception) { }
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

    public override void AddFile(string path)
    {
        // 自動管理のため手動追加不可
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
