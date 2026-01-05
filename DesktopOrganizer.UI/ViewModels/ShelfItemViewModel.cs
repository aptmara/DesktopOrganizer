using System.Windows.Input;
using System.Windows.Media;
using DesktopOrganizer.Core.Models;
using DesktopOrganizer.UI.Utilities;
using System.IO;

namespace DesktopOrganizer.UI.ViewModels;

public class ShelfItemViewModel : ViewModelBase
{
    private readonly ShelfItem _model;
    private readonly Action? _saveLayoutAction;

    /// <summary>
    /// アイテムの一意識別子
    /// </summary>
    public string Id => _model.Id;

    /// <summary>
    /// アイテム種別
    /// </summary>
    public ShelfItemType Type => _model.Type;

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

    private CancellationTokenSource? _cts;

    private async void LoadIconAsync()
    {
        if (string.IsNullOrEmpty(_model.TargetPath)) return;

        // Cancel previous request if any
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            // 起動直後はファイルシステムがまだ準備できていない可能性があるため少し待機
            await Task.Delay(50, token);

            // Use the new Async Throttled method
            var icon = await IconUtilities.GetIconAsync(_model.TargetPath, token);

            // アイコン取得失敗時はリトライ（最大3回）
            for (int retry = 0; retry < 3 && icon == null; retry++)
            {
                if (token.IsCancellationRequested) return;
                await Task.Delay(200 * (retry + 1), token);
                icon = await IconUtilities.GetIconAsync(_model.TargetPath, token);
            }

            if (icon != null && !token.IsCancellationRequested)
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    // Use Dispatcher Priority Background to not block input
                    await app.Dispatcher.InvokeAsync(() =>
                    {
                        _icon = icon;
                        OnPropertyChanged(nameof(Icon));
                    }, System.Windows.Threading.DispatcherPriority.Background); // Low priority
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            // DesktopOrganizer.Core.Utilities.Logger.LogError($"Failed to load icon: {_model.TargetPath}", ex);
            // Loggerへの依存は避けるか、注入するか。一旦コメントアウトかCore参照
            System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex}");
        }
    }

    // Call this when removing item
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
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
