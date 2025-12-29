using System.Collections.ObjectModel;

namespace DesktopOrganizer.UI.ViewModels;

public class OverlayViewModel : ViewModelBase
{
    public ObservableCollection<ShelfViewModel> Shelves { get; } = new();

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

    public void AddShelf(ShelfViewModel shelf)
    {
        Shelves.Add(shelf);
    }
}
