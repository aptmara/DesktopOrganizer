using System.Collections.ObjectModel;

namespace DesktopOrganizer.UI.ViewModels;

public class OverlayViewModel : ViewModelBase
{
    public ObservableCollection<ShelfViewModel> Shelves { get; } = new();

    public void AddShelf(ShelfViewModel shelf)
    {
        Shelves.Add(shelf);
    }
}
