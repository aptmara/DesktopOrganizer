using System.Collections.ObjectModel;

namespace DesktopOrganizer.UI.ViewModels;

public class OverlayViewModel : ViewModelBase
{
    public ObservableCollection<ShelfViewModelBase> Shelves { get; } = new();

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
            }
        }
    }

    public void AddShelf(ShelfViewModelBase shelf)
    {
        Shelves.Add(shelf);
    }

    public event EventHandler<System.Windows.Point>? CreateShelfRequested;
    public event EventHandler<(System.Windows.Point Position, DesktopOrganizer.Core.Models.ShelfType Type)>? CreateTypedShelfRequested;
    public event EventHandler? ToggleEditModeRequested;

    public void RequestCreateShelf(System.Windows.Point position)
    {
        CreateShelfRequested?.Invoke(this, position);
    }

    public void RequestCreateTypedShelf(System.Windows.Point position, DesktopOrganizer.Core.Models.ShelfType type)
    {
        CreateTypedShelfRequested?.Invoke(this, (position, type));
    }

    public void RequestToggleEditMode()
    {
        ToggleEditModeRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ResetAllRequested;

    public void RequestResetAll()
    {
        ResetAllRequested?.Invoke(this, EventArgs.Empty);
    }

    public void BringToFront(ShelfViewModelBase shelf)
    {
        if (Shelves.Count == 0) return;
        var maxZ = Shelves.Max(s => s.ZIndex);
        // 同じZIndexのものがいる場合も上げる
        if (shelf.ZIndex < maxZ || Shelves.Any(s => s != shelf && s.ZIndex == maxZ))
        {
            shelf.ZIndex = maxZ + 1;
        }
    }
}
